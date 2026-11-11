using eQuantic.UI.Primitives;
using PomodoroTasker.Core;

namespace PomodoroTasker.Web.Server;

/// <summary>
/// Everything a Server Action does. One user and one connection, so every call takes the gate,
/// and every call first closes the days that ended and applies the timer phases that passed.
/// </summary>
[ServerOnly]
public static class Api
{
    private static readonly object Gate = new();
    private static string? _day;
    private static bool _missed;
    /// <summary>The days the last boot closed without the user; what Dias fechados lists.</summary>
    private static List<string> _closed = [];
    private static string _flash = "";

    private static Db Db => Store.Db;
    private static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static TResult Run<TResult>(Func<string, TResult> work)
    {
        lock (Gate) return work(Boot());
    }

    public static void Run(Action<string> work) => Run(day => { work(day); return 0; });

    /// <summary>The current Day, after closing every day that ended before it.</summary>
    private static string Boot()
    {
        var now = Day.Today();
        if (_day == now) return now;
        // The timer first, so a pomodoro that ended while nobody looked is in its day's snapshot.
        Sync();
        _closed = Repo.CloseMissedDays(Db, now);
        if (_closed.Count > 1) _missed = true;
        return _day = now;
    }

    // ---------- settings ----------

    public static Prefs Prefs() => Repo.GetSetting<Prefs>(Db, "prefs") ?? new Prefs();
    private static Durations Durations(Prefs p) => new(p.Focus, p.Short, p.Long, p.Every);

    // ---------- timer ----------

    private static TimerState State() => Repo.GetSetting<TimerState>(Db, "timer") ?? Pomodoro.IdleState;

    /// <summary>Stores a transition and its pomodoros. Returns how many were recorded.</summary>
    private static int Commit(TimerState before, TimerState next, IEnumerable<PomodoroRecord> records)
    {
        if (next == before) return 0;
        var count = 0;
        foreach (var r in records)
        {
            Repo.AddPomodoro(Db, r);
            count++;
        }
        // The task was deleted under a running timer: nothing left to time.
        if (next.TaskId != null && Repo.GetTask(Db, next.TaskId.Value) == null)
            next = Pomodoro.IdleState with { Cycle = next.Cycle };
        Repo.SetSetting(Db, "timer", next);
        return count;
    }

    /// <summary>Applies every phase end that has passed, and words what the user should be told.</summary>
    private static int Sync()
    {
        var prefs = Prefs();
        var before = State();
        var (next, records) = Pomodoro.CatchUp(before, Durations(prefs), NowMs);
        if (next != before)
        {
            var title = next.TaskId == null ? "" : Repo.GetTask(Db, next.TaskId.Value)?.Title ?? "";
            _flash = next.Phase switch
            {
                Phase.Short => $"{T.NotifyDoneTitle}. {T.NotifyDoneBody(title, prefs.Short)}",
                Phase.Long => T.NotifyCycle(prefs.Every, prefs.Long),
                Phase.Wait => $"{T.NotifyBreakTitle}. {T.NotifyBreakBody(title)}",
                _ => "",
            };
        }
        return Commit(before, next, records);
    }

    private static TimerView View(string day, int recorded)
    {
        var s = State();
        var prefs = Prefs();
        var view = new TimerView
        {
            Day = day, Phase = s.Phase, TaskId = s.TaskId ?? 0,
            TaskTitle = s.TaskId == null ? "" : Repo.GetTask(Db, s.TaskId.Value)?.Title ?? "",
            EndsAt = s.EndsAt ?? 0, PausedLeft = s.PausedLeft ?? -1, Cycle = s.Cycle,
            Every = prefs.Every, FocusMinutes = prefs.Focus, Recorded = recorded, Flash = _flash, Missed = _missed,
        };
        _flash = "";
        _missed = false;
        return view;
    }

    public static TimerView Timer() => Run(day => View(day, Sync()));

    /// <param name="type">start, primary, pause, resume, stop, skip.</param>
    public static TimerView Send(string type, int taskId = 0, string reason = "") => Run(day =>
    {
        var recorded = Sync();
        var s = State();
        if (type == "start" && taskId != 0)
            Repo.UpdateTask(Db, taskId, new() { ["pomodoro_enabled"] = 1 }); // starting a focus makes it a pomodoro task
        // The main button of the current phase: pause or resume, or start the next focus.
        var e = type switch
        {
            "start" => TimerEvent.Start,
            "primary" when s.Phase == Phase.Wait => TimerEvent.Start,
            "primary" => Pomodoro.Paused(s) ? TimerEvent.Resume : TimerEvent.Pause,
            "stop" => TimerEvent.Stop,
            "skip" => TimerEvent.Skip,
            _ => TimerEvent.Expire,
        };
        if (type == "primary" && s.Phase == Phase.Idle) return View(day, recorded);
        var (next, record) = Pomodoro.Step(s, e, Durations(Prefs()), NowMs, taskId != 0 ? taskId : s.TaskId, reason);
        return View(day, recorded + Commit(s, next, record == null ? [] : [record]));
    });

    // ---------- tasks ----------

    /// <summary>Fills the display values of each task and its sub-tasks.</summary>
    private static List<TaskItem> Decorate(List<TaskItem> tasks, string day, bool today)
    {
        var toms = Repo.Tomatoes(Db, day).ToDictionary(t => t.Id);
        void One(TaskItem t, bool sub)
        {
            t.InToday = t.TodayDate == day;
            if (toms.TryGetValue(t.Id, out var tom)) (t.Toms, t.TomsInterrupted) = (tom.Done, tom.Interrupted);
            if (!sub)
            {
                if (today)
                {
                    var list = t.ProjectName != null ? $"{t.ProjectName} › {t.ListName}"
                        : t.ListName != null ? T.TaskLooseList(t.ListName) : null;
                    t.Where = t.ParentTitle == null ? list : list == null ? t.ParentTitle : $"{list} › {t.ParentTitle}";
                }
                var rule = Repo.RuleOf(t);
                if (rule != null) t.RuleText = Recurrence.Describe(rule);
                if (t.DueDate != null && t.DoneAt == null) (t.DeadlineText, t.DeadlineLate) = Day.DeadlineLabel(t.DueDate, day);
                if (t.PlannedDate != null && t.DoneAt == null && string.CompareOrdinal(t.PlannedDate, day) > 0)
                    t.PlannedText = T.TaskPlanned(Day.ShortDay(t.PlannedDate, day));
            }
            foreach (var s in t.Subs) One(s, true);
        }
        foreach (var t in tasks) One(t, false);
        return tasks;
    }

    public static TodayView Today() => Run(day =>
    {
        Repo.EnterPlanned(Db, day);
        var s = State();
        return new TodayView
        {
            Day = day, DayLabel = Day.LongDay(day), Tasks = Decorate(Repo.TodayTasks(Db, day), day, today: true),
            FocusTaskId = s.Phase == Phase.Focus ? s.TaskId ?? 0 : 0, FocusPaused = Pomodoro.Paused(s),
        };
    });

    public static void AddTask(string title, int listId = 0, int parentId = 0, bool today = false) => Run(day =>
    {
        if (title.Trim() != "")
            Repo.CreateTask(Db, title, listId == 0 ? null : listId, parentId == 0 ? null : parentId, today ? day : null);
    });

    /// <summary>Returns the id of the loose list this archived, or 0.</summary>
    public static int SetDone(int id, bool done) => Run(day =>
    {
        var archived = Repo.SetDone(Db, id, done) ?? 0;
        // If the timer was on that task (or on a sub-task closed with its parent), it stops.
        var s = State();
        if (done && s.TaskId != null && Repo.GetTask(Db, s.TaskId.Value)?.DoneAt != null)
        {
            var (next, record) = Pomodoro.Step(s, TimerEvent.Finish, Durations(Prefs()), NowMs, reason: T.TimerDoneReason);
            Commit(s, next, record == null ? [] : [record]);
        }
        return archived;
    });

    public static void SetToday(int id, bool today) => Run(day => Repo.SetToday(Db, id, today ? day : null));
    public static void DeleteTask(int id) => Run(_ => Repo.DeleteTask(Db, id));
    public static void Reorder(int[] ids, bool today) => Run(_ => Repo.Reorder(Db, ids, today));

    // ---------- task editor ----------

    public static EditorView Editor(int id) => Run(day =>
    {
        var task = Repo.GetTask(Db, id);
        if (task != null) Decorate([task], day, today: false);
        return new EditorView
        {
            Day = day, Task = task, Lists = Repo.ListOptions(Db),
            History = Repo.TaskHistory(Db, id).Select(h => new HistoryLine
            {
                Day = Day.MediumDay(h.Day), Toms = h.Done, TomsInterrupted = h.Interrupted,
                Text = h.Done + h.Interrupted == 0 ? T.HistoryUntouched : string.Join(" · ", new[]
                {
                    T.HistoryPomodoros(h.Done, h.Interrupted), T.HistoryFocus(Day.Duration(h.Seconds)),
                    string.IsNullOrEmpty(h.Reasons) ? "" : T.HistoryReason(h.Reasons), h.Postponed > 0 ? T.HistoryPostponed : "",
                }.Where(x => x != "")),
            }).ToList(),
        };
    });

    public static void SaveTask(int id, TaskEdit e) => Run(_ => Repo.UpdateTask(Db, id, new()
    {
        ["title"] = e.Title.Trim(), ["notes"] = e.Notes, ["priority"] = e.Priority,
        ["tags"] = string.Join(", ", e.Tags.Split(',').Select(x => x.Trim()).Where(x => x != "")),
        ["planned_date"] = e.Planned == "" ? null : e.Planned, ["due_date"] = e.Due == "" ? null : e.Due,
        ["pomodoro_enabled"] = e.Pomodoro ? 1 : 0, ["list_id"] = e.ListId == 0 ? null : e.ListId,
    }));

    private static Rule? RuleFrom(string freq, int interval, int days, string anchor, string day) =>
        freq == "" ? null : new Rule(freq, freq == "weekly" ? interval : 1, freq == "weekly" ? days : 0, anchor == "" ? day : anchor);

    /// <summary>The rule is saved at once, apart from the rest of the form. An empty freq removes it.</summary>
    public static void SetRule(int id, string freq, int interval, int days, string anchor) =>
        Run(day => Repo.SetRecurrence(Db, id, RuleFrom(freq, interval, days, anchor, day), day));

    /// <summary>The next four due days of a draft rule, in words.</summary>
    public static string RulePreview(string freq, int interval, int days, string anchor) => Run(day =>
    {
        var rule = RuleFrom(freq, interval, days, anchor, day);
        if (rule == null) return T.RecNone;
        if (rule.Freq == "weekly" && rule.Days == 0) return T.RecPreviewPending;
        return T.RecNext(string.Join(", ", Recurrence.NextDue(rule, day, 4).Select(d => Day.ShortDay(d, day))));
    });

    // ---------- lists ----------

    public static ListsView Lists() => Run(day => new ListsView
    {
        Lists = Repo.LooseLists(Db, day, archived: false), Archived = Repo.LooseLists(Db, day, archived: true).Count,
    });

    public static List<TaskList> ArchivedLists() => Run(day => Repo.LooseLists(Db, day, archived: true));

    public static LooseListView List(int id) => Run(day =>
    {
        var list = Repo.GetList(Db, day, id);
        return new LooseListView
        {
            List = list,
            ArchivedLabel = list?.ArchivedAt == null ? "" : T.ListsArchivedOn(Day.ShortDay(Day.OfIso(list.ArchivedAt), day)),
            Tasks = list == null ? [] : Decorate(Repo.ListTasks(Db, id), day, today: false),
        };
    });

    public static int CreateList(string name, int projectId = 0) =>
        Run(_ => name.Trim() == "" ? 0 : Repo.CreateList(Db, name, projectId == 0 ? null : projectId));

    public static void RenameList(int id, string name) => Run(_ => { if (name.Trim() != "") Repo.RenameList(Db, id, name); });
    public static void ArchiveList(int id, bool archived) => Run(_ => Repo.SetListArchived(Db, id, archived));
    public static void DeleteList(int id) => Run(_ => Repo.DeleteList(Db, id));

    // ---------- projects ----------

    public static ProjectsView Projects() => Run(day =>
    {
        var all = Repo.Projects(Db, day);
        return new ProjectsView
        {
            Projects = all,
            CompletedLabels = all.Select(p => p.CompletedAt == null ? ""
                : T.ProjectsCompletedOn(Day.ShortDay(Day.OfIso(p.CompletedAt), day))).ToList(),
        };
    });

    public static ProjectView Project(int id) => Run(day => new ProjectView
    {
        Project = Repo.GetProject(Db, day, id),
        Lists = Repo.ProjectLists(Db, day, id).Select(l => new ProjectListView
        {
            List = l, Tasks = Decorate(Repo.ListTasks(Db, l.Id), day, today: false),
        }).ToList(),
    });

    public static int CreateProject(string name) => Run(_ => name.Trim() == "" ? 0 : Repo.CreateProject(Db, name));
    public static void RenameProject(int id, string name) => Run(_ => { if (name.Trim() != "") Repo.RenameProject(Db, id, name); });
    public static void CompleteProject(int id, bool completed) => Run(_ => Repo.SetProjectCompleted(Db, id, completed));
    public static void DeleteProject(int id) => Run(_ => Repo.DeleteProject(Db, id));

    // ---------- routines ----------

    public static List<RoutineLine> Routines() => Run(day => Repo.Routines(Db).Select(r =>
    {
        var rule = Repo.RuleOf(r)!;
        var paused = r.RecPaused != 0;
        var from = r.RecLast != null && string.CompareOrdinal(r.RecLast, day) >= 0 ? Day.AddDays(r.RecLast, 1) : day;
        var next = Recurrence.NextDue(rule, from).FirstOrDefault();
        var stuck = !paused && r.DoneAt == null && r.RecLast != null && string.CompareOrdinal(r.RecLast, day) < 0;
        var parts = new List<string> { Recurrence.Describe(rule) };
        if (paused) parts.Add(T.RecPausedTag);
        else if (stuck) parts.Add(T.RecStillOpen(Day.MediumDay(r.RecLast!)));
        else if (next != null) parts.Add(T.RecNextOn(Day.ShortDay(next, day)));
        if ((r.ProjectName ?? r.ListName) != null) parts.Add((r.ProjectName ?? r.ListName)!);
        return new RoutineLine
        {
            Id = r.Id, Title = r.Title, Paused = paused, Text = string.Join(" · ", parts), Note = stuck ? T.RecStillOpenNote : "",
        };
    }).ToList());

    public static void PauseRoutine(int id, bool paused) => Run(day => Repo.SetRoutinePaused(Db, id, paused, day));

    // ---------- day close ----------

    public static CloseView CloseReview() => Run(day =>
    {
        var tasks = Decorate(Repo.TodayTasks(Db, day), day, today: true);
        var seconds = Repo.DaySummary(Db, day).FocusSeconds;
        return new CloseView
        {
            DayLabel = Day.LongDay(day),
            Open = tasks.Where(t => t.DoneAt == null).ToList(), Done = tasks.Where(t => t.DoneAt != null).ToList(),
            Tomatoes = tasks.Sum(t => t.Toms), Focus = seconds > 0 ? Day.Duration(seconds) : "",
        };
    });

    public static CloseView CloseDay(int[] toBacklog) => Run(day =>
    {
        var open = Repo.TodayTasks(Db, day).Where(t => t.DoneAt == null).Select(t => t.Id).ToHashSet();
        var back = toBacklog.Where(open.Contains).ToList();
        Repo.CloseDay(Db, day, back);
        var summary = Repo.DaySummary(Db, day);
        var tomorrow = Day.AddDays(day, 1);
        return new CloseView
        {
            Closed = true, DayLabel = Day.LongDay(day), TomorrowLabel = Day.MediumDay(tomorrow),
            DoneCount = summary.Done, Postponed = summary.Postponed, Tomatoes = summary.Tomatoes,
            Focus = summary.FocusSeconds > 0 ? Day.Duration(summary.FocusSeconds) : "", SentBack = back.Count,
            Waiting = Decorate(Repo.TodayTasks(Db, tomorrow), day, today: true),
        };
    });

    /// <summary>The days closed without the user, oldest first, and what they left in Today.</summary>
    public static MissedView Missed() => Run(day =>
    {
        var days = _closed.Select(d =>
        {
            var s = Repo.DaySummary(Db, d);
            var text = T.CloseDayLine(s.Done, s.Postponed);
            return new RoutineLine { Title = Day.MediumDay(d), Text = s.Tomatoes > 0 ? $"{text} · {T.Tomatoes(s.Tomatoes)}" : text };
        }).ToList();
        return new MissedView
        {
            Days = days,
            Carried = Decorate(Repo.TodayTasks(Db, day), day, today: true).Where(t => t.PostponeCount > 0 && t.DoneAt == null).ToList(),
        };
    });

    // ---------- reports ----------

    private const int HeatWeeks = 25;

    /// <param name="kind">week, month or custom.</param>
    /// <param name="shift">-1 or 1 moves a week or month; 0 keeps the period. Empty dates mean the current one.</param>
    public static ReportsView Reports(string kind, string from, string to, int shift) => Run(day =>
    {
        if (kind == "week") (from, to) = Core.Reports.WeekOf(from == "" ? day : Day.AddDays(from, 7 * shift));
        else if (kind == "month") (from, to) = Core.Reports.MonthOf(from == "" ? day : Day.AddDays(shift < 0 ? from : to, shift));
        if (from == "" || to == "" || string.CompareOrdinal(from, to) > 0) (from, to) = Core.Reports.WeekOf(day);

        var all = Repo.FocusDays(Db);
        var first = Repo.FirstClosedDay(Db);
        var streak = Core.Reports.Streaks(all.Select(x => x.Day), day);
        var view = new ReportsView
        {
            Empty = first == null && streak.Total == 0, From = from, To = to,
            Range = T.ReportsRange(Day.DayMonth(from, day), Day.DayMonth(to, day)), CanNext = string.CompareOrdinal(to, day) < 0,
            StreakNow = streak.Current, StreakBest = streak.Longest, StreakTotal = streak.Total,
        };

        var bars = Core.Reports.Bars(Repo.DayStats(Db, from, to), from, to);
        view.Weekly = bars.Any(b => b.Days > 1);
        string Label(Bar b) => view.Weekly ? Day.DayMonth(b.From) : bars.Count > 10 ? Day.Date(b.From).Day.ToString() : Day.WeekdayShort(b.From);
        view.Labels = bars.Select(Label).ToList();
        view.Done = bars.Select(b => (double)b.Done).ToList();
        view.Postponed = bars.Select(b => (double)b.Postponed).ToList();
        view.Completed = bars.Select(b => (double)b.Completed).ToList();
        view.Interrupted = bars.Select(b => (double)b.Interrupted).ToList();
        var done = bars.Sum(b => b.Done);
        var postponed = bars.Sum(b => b.Postponed);
        if (done + postponed > 0) view.Rate = (int)Math.Round(done * 100.0 / (done + postponed), MidpointRounding.AwayFromZero);
        var best = view.Weekly ? null : bars.Where(b => b.Done > 0)
            .OrderByDescending(b => (double)b.Done / (b.Done + b.Postponed)).ThenByDescending(b => b.Done).FirstOrDefault();
        if (best != null) view.Best = T.ReportsBest(Day.MediumDay(best.From), best.Done, best.Done + best.Postponed);

        var projects = Repo.FocusByProject(Db, from, to);
        view.FocusTotal = T.ReportsInPeriod(Day.Duration(projects.Sum(p => p.Seconds)));
        view.ProjectNames = projects.Select(p => p.Project ?? T.ReportsNoProject).ToList();
        view.ProjectMinutes = projects.Select(p => Math.Round(p.Seconds / 60.0)).ToList();

        // Columns are weeks, Sunday on top, ending in the current week.
        var end = Day.AddDays(day, 6 - Day.Weekday(day));
        var start = Day.AddDays(end, -(HeatWeeks * 7 - 1));
        var counts = all.ToDictionary(x => x.Day, x => x.Count);
        var max = all.Where(x => string.CompareOrdinal(x.Day, start) >= 0).Select(x => x.Count).DefaultIfEmpty(0).Max();
        view.Heat = Enumerable.Range(0, HeatWeeks * 7).Select(i => Day.AddDays(start, i))
            .Select(d => string.CompareOrdinal(d, day) > 0 ? -1 : Core.Reports.HeatLevel(counts.GetValueOrDefault(d), max)).ToList();

        view.MostPostponed = Repo.MostPostponed(Db, from, to).Select(p => new RankLine
        {
            Title = p.Title, Times = p.Times,
            Text = string.Join(" · ", new[]
            {
                p.Project ?? (p.List != null ? T.TaskLooseList(p.List) : ""),
                p.Exists == 0 ? T.ReportsDeleted : p.DoneAt != null ? T.ReportsDoneOn(Day.ShortDay(Day.OfIso(p.DoneAt), day)) : T.ReportsStillOpen,
            }.Where(x => x != "")),
        }).ToList();
        return view;
    });

    /// <summary>The start of a shortcut period that ends today: days back, "year" or "all".</summary>
    public static string ReportStart(string shortcut) => Run(day => shortcut switch
    {
        "year" => $"{day[..4]}-01-01",
        "all" => Repo.FirstClosedDay(Db) ?? day,
        _ => Day.AddDays(day, -int.Parse(shortcut)),
    });

    public static string TodayDay() => Run(day => day);

    // ---------- settings ----------

    public static SettingsView Settings() => Run(_ =>
    {
        var p = Prefs();
        var path = Path.GetFullPath(Store.Path);
        var bytes = File.Exists(path) ? new FileInfo(path).Length : 0;
        var size = bytes < 1024 * 1024 ? $"{Math.Max(1, bytes / 1024)} KB" : $"{bytes / 1024.0 / 1024.0:0.0} MB";
        return new SettingsView
        {
            Focus = p.Focus, Short = p.Short, Long = p.Long, Every = p.Every, Theme = p.Theme,
            Running = State().Phase != Phase.Idle, Database = $"{path} · {size}",
            About = T.SettingsAbout(typeof(Api).Assembly.GetName().Version?.ToString(3) ?? ""),
        };
    });

    public static SettingsView SaveSettings(int focus, int @short, int @long, int every, string theme)
    {
        Run(_ => Repo.SetSetting(Db, "prefs", new Prefs(
            Math.Clamp(focus, 1, 120), Math.Clamp(@short, 1, 60), Math.Clamp(@long, 1, 120), Math.Clamp(every, 2, 12),
            theme is "light" or "dark" ? theme : "auto")));
        return Settings();
    }
}
