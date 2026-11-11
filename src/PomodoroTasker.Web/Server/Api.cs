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
        if (Repo.CloseMissedDays(Db, now).Count > 1) _missed = true;
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
}
