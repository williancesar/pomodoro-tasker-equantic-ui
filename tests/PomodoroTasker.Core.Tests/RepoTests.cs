using PomodoroTasker.Core;

namespace PomodoroTasker.Core.Tests;

/// <summary>The data layer against in-memory SQLite, with the same migrations the app runs.</summary>
public class RepoTests : IDisposable
{
    private const string DAY = "2026-11-10";
    private readonly Db db = new(":memory:");

    public void Dispose() => db.Dispose();

    private static string Iso(int day, int hour) => Day.Iso(new DateTime(2026, 11, day, hour, 0, 0, DateTimeKind.Local));
    private static long Ms(int day, int hour) => new DateTimeOffset(new DateTime(2026, 11, day, hour, 0, 0, DateTimeKind.Local)).ToUnixTimeMilliseconds();
    private static PomodoroRecord Rec(int taskId, long at, string status, int seconds = 1500, string reason = "") =>
        new(taskId, at, at + 1_500_000, seconds, status, reason);

    [Fact]
    public void Day_helpers()
    {
        Assert.Equal("2026-12-01", Day.AddDays("2026-11-30", 1));
        Assert.Equal("terça-feira, 10 de novembro", Day.LongDay(DAY));
        Assert.Equal("quinta", Day.ShortDay("2026-11-12", DAY));
        Assert.Equal("25 dez", Day.ShortDay("2026-12-25", DAY));
        Assert.Equal(("prazo quinta", false), Day.DeadlineLabel("2026-11-12", DAY));
        Assert.Equal(("venceu ontem", true), Day.DeadlineLabel("2026-11-09", DAY));
        Assert.Equal(("venceu há 2 dias", true), Day.DeadlineLabel("2026-11-08", DAY));
    }

    [Fact]
    public void Today_shows_pulled_tasks_nests_sub_tasks_and_keeps_a_lone_sub_task_with_its_parent_title()
    {
        var p = Repo.CreateProject(db, "Pessoal");
        var l = Repo.CreateList(db, "Viagem", p);
        var trip = Repo.CreateTask(db, "Planejar viagem", listId: l);
        var sub = Repo.CreateTask(db, "Montar roteiro", parentId: trip);
        var other = Repo.CreateTask(db, "Fica no backlog", listId: l);

        Repo.SetToday(db, trip, DAY);
        var t = Repo.TodayTasks(db, DAY);
        Assert.Equal(["Planejar viagem"], t.Select(x => x.Title));
        Assert.Equal(["Montar roteiro"], t[0].Subs.Select(x => x.Title));
        Assert.Equal("Pessoal", t[0].ProjectName);
        Assert.Equal(l, t[0].Subs[0].ListId); // sub-task inherits the parent's list

        Repo.SetToday(db, trip, null);
        Repo.SetToday(db, sub, DAY);
        t = Repo.TodayTasks(db, DAY);
        Assert.Equal([("Montar roteiro", "Planejar viagem")], t.Select(x => (x.Title, x.ParentTitle!)));
        Assert.Equal([trip, other], Repo.ListTasks(db, l).Select(x => x.Id));
    }

    [Fact]
    public void A_planned_task_enters_Today_on_its_day_and_going_back_to_the_backlog_drops_the_plan()
    {
        var id = Repo.CreateTask(db, "Planejada");
        Repo.UpdateTask(db, id, new() { ["planned_date"] = "2026-11-11" });
        Repo.EnterPlanned(db, DAY);
        Assert.Empty(Repo.TodayTasks(db, DAY));
        Repo.EnterPlanned(db, "2026-11-11");
        Assert.Single(Repo.TodayTasks(db, "2026-11-11"));
        Repo.SetToday(db, id, null);
        Repo.EnterPlanned(db, "2026-11-11");
        Assert.Empty(Repo.TodayTasks(db, "2026-11-11"));
    }

    [Fact]
    public void Done_on_a_parent_closes_its_sub_tasks_and_reopening_the_parent_leaves_them_done()
    {
        var parent = Repo.CreateTask(db, "Pai", todayDate: DAY);
        Repo.CreateTask(db, "Filha", parentId: parent);
        Repo.SetDone(db, parent, true);
        var t = Repo.TodayTasks(db, DAY)[0];
        Assert.NotNull(t.DoneAt);
        Assert.NotNull(t.Subs[0].DoneAt);
        Repo.SetDone(db, parent, false);
        t = Repo.TodayTasks(db, DAY)[0];
        Assert.Null(t.DoneAt);
        Assert.NotNull(t.Subs[0].DoneAt);
    }

    [Fact]
    public void A_loose_list_archives_itself_when_its_last_task_is_done_and_a_project_list_does_not()
    {
        var loose = Repo.CreateList(db, "Compras");
        var a = Repo.CreateTask(db, "Café", listId: loose);
        var b = Repo.CreateTask(db, "Pão", listId: loose);
        Assert.Null(Repo.SetDone(db, a, true));
        Assert.Equal(loose, Repo.SetDone(db, b, true));
        Assert.Empty(Repo.LooseLists(db, DAY, false));
        var archived = Repo.LooseLists(db, DAY, true)[0];
        Assert.Equal(("Compras", 2, 2), (archived.Name, archived.Total, archived.Done));

        var pl = Repo.CreateList(db, "Lista", Repo.CreateProject(db, "Projeto"));
        var c = Repo.CreateTask(db, "Única", listId: pl);
        Assert.Null(Repo.SetDone(db, c, true));
    }

    [Fact]
    public void Reorder_is_independent_in_a_list_and_in_Today()
    {
        var l = Repo.CreateList(db, "L");
        var ids = new[] { "a", "b", "c" }.Select(title => Repo.CreateTask(db, title, listId: l, todayDate: DAY)).ToList();
        Repo.Reorder(db, [ids[2], ids[0], ids[1]], today: true);
        Assert.Equal(["c", "a", "b"], Repo.TodayTasks(db, DAY).Select(t => t.Title));
        Assert.Equal(["a", "b", "c"], Repo.ListTasks(db, l).Select(t => t.Title));
    }

    [Fact]
    public void Project_counts_and_deleting_a_project_removes_its_lists_and_tasks()
    {
        var p = Repo.CreateProject(db, "Finanças");
        var l = Repo.CreateList(db, "Impostos", p);
        var late = Repo.CreateTask(db, "MEI", listId: l, todayDate: DAY);
        Repo.UpdateTask(db, late, new() { ["due_date"] = "2026-11-09" });
        var done = Repo.CreateTask(db, "Feita", listId: l);
        Repo.SetDone(db, done, true);
        var project = Repo.Projects(db, DAY)[0];
        Assert.Equal((1, 1, 1, 1, 1), (project.Lists, project.Open, project.Done, project.InToday, project.Overdue));
        Assert.Equal([(l, "Finanças › Impostos")], Repo.ListOptions(db).Select(o => (o.Id, o.Label)));

        Repo.DeleteProject(db, p);
        Assert.Empty(Repo.Projects(db, DAY));
        Assert.Empty(Repo.TodayTasks(db, DAY));
    }

    private sealed class PomodoroRow
    {
        public int? TaskId { get; set; }
        public string TaskTitle { get; set; } = "";
        public string? ProjectName { get; set; }
    }

    [Fact]
    public void Tomatoes_count_per_day_a_parent_sums_its_sub_tasks_and_records_outlive_the_task()
    {
        var l = Repo.CreateList(db, "Viagem", Repo.CreateProject(db, "Pessoal"));
        var parent = Repo.CreateTask(db, "Planejar viagem", listId: l);
        var sub = Repo.CreateTask(db, "Roteiro", parentId: parent);
        var at = Ms(10, 9);
        Repo.AddPomodoro(db, Rec(sub, at, "completed"));
        Repo.AddPomodoro(db, Rec(sub, at, "interrupted", reason: "reunião"));
        Repo.AddPomodoro(db, Rec(parent, at, "completed"));

        var t = Repo.Tomatoes(db, DAY).ToDictionary(x => x.Id, x => (x.Done, x.Interrupted));
        Assert.Equal((1, 1), t[sub]);
        Assert.Equal((2, 1), t[parent]);
        Assert.Empty(Repo.Tomatoes(db, "2026-11-11"));

        Repo.DeleteTask(db, parent);
        var kept = db.Select<PomodoroRow>("SELECT * FROM pomodoros ORDER BY id");
        Assert.Equal(
            [(null, "Roteiro", "Pessoal"), (null, "Roteiro", "Pessoal"), (null, "Planejar viagem", "Pessoal")],
            kept.Select(k => (k.TaskId, k.TaskTitle, k.ProjectName!)));
    }

    [Fact]
    public void Settings_round_trip_as_JSON()
    {
        Assert.Null(Repo.GetSetting<TimerState>(db, "timer"));
        Repo.SetSetting(db, "timer", new TimerState { Phase = Phase.Focus });
        Repo.SetSetting(db, "timer", new TimerState { Phase = Phase.Idle, Cycle = 2 });
        Assert.Equal(new TimerState { Phase = Phase.Idle, Cycle = 2 }, Repo.GetSetting<TimerState>(db, "timer"));
        // The shape the desktop app writes.
        db.Execute("UPDATE settings SET value = $1", """{"phase":"focus","taskId":3,"endsAt":5,"pausedLeft":null,"cycle":1,"startedAt":2,"focusMs":3}""");
        Assert.Equal(new TimerState { Phase = Phase.Focus, TaskId = 3, EndsAt = 5, Cycle = 1, StartedAt = 2, FocusMs = 3 },
            Repo.GetSetting<TimerState>(db, "timer"));
    }

    private sealed class SnapshotRow
    {
        public string TaskTitle { get; set; } = "";
        public string Outcome { get; set; } = "";
        public int Tomatoes { get; set; }
    }

    [Fact]
    public void Closing_a_day_snapshots_it_rolls_open_tasks_forward_and_counts_the_postponement()
    {
        var done = Repo.CreateTask(db, "Feita", todayDate: DAY);
        var open = Repo.CreateTask(db, "Aberta", todayDate: DAY);
        var back = Repo.CreateTask(db, "Volta ao backlog", todayDate: DAY);
        Repo.SetDone(db, done, true, Iso(10, 15));
        Repo.AddPomodoro(db, Rec(open, Ms(10, 9), "completed"));

        Repo.CloseDay(db, DAY, [back]);
        var s = Repo.DaySummary(db, DAY);
        Assert.Equal((DAY, 1, 1, 1, 1500), (s.Day, s.Done, s.Postponed, s.Tomatoes, s.FocusSeconds));
        Assert.Empty(Repo.TodayTasks(db, DAY));
        Assert.Equal([("Aberta", 1)], Repo.TodayTasks(db, "2026-11-11").Select(t => (t.Title, t.PostponeCount)));
        Assert.Equal([("Feita", "done", 0), ("Aberta", "postponed", 1)],
            db.Select<SnapshotRow>("SELECT * FROM day_snapshots ORDER BY id").Select(x => (x.TaskTitle, x.Outcome, x.Tomatoes)));

        // closing again changes nothing, and history survives edits and deletion
        Repo.CloseDay(db, DAY);
        Repo.UpdateTask(db, open, new() { ["title"] = "Renomeada" });
        Repo.DeleteTask(db, done);
        Assert.Equal(["Feita", "Aberta"], db.Select<string>("SELECT task_title FROM day_snapshots ORDER BY id"));
    }

    [Fact]
    public void Missed_days_are_closed_in_order_and_each_one_counts_a_postponement()
    {
        var id = Repo.CreateTask(db, "Esquecida", todayDate: "2026-11-06");
        Assert.Equal(["2026-11-06", "2026-11-07", "2026-11-08", "2026-11-09"], Repo.CloseMissedDays(db, DAY));
        var t = Repo.TodayTasks(db, DAY)[0];
        Assert.Equal((id, 4), (t.Id, t.PostponeCount));
        Assert.Equal(1, Repo.DaySummary(db, "2026-11-08").Postponed);
        Assert.Empty(Repo.CloseMissedDays(db, DAY)); // nothing left to close
        Assert.Equal([("2026-11-09", 1), ("2026-11-08", 1), ("2026-11-07", 1), ("2026-11-06", 1)],
            Repo.TaskHistory(db, id).Select(h => (h.Day, h.Postponed)));
    }

    [Fact]
    public void A_day_closed_early_by_the_user_is_closed_again_later_for_tasks_added_after()
    {
        Repo.CreateTask(db, "Antes", todayDate: DAY);
        Repo.CloseDay(db, DAY);
        Assert.Empty(Repo.CloseMissedDays(db, DAY));
        Repo.CreateTask(db, "Depois de fechar", todayDate: DAY);
        Assert.Empty(Repo.CloseMissedDays(db, "2026-11-11"));
        Assert.Equal(["Antes", "Depois de fechar"], Repo.TodayTasks(db, "2026-11-11").Select(t => t.Title).Order());
        Assert.Equal(2, Repo.DaySummary(db, DAY).Postponed);
    }

    [Fact]
    public void A_task_finished_straight_from_its_list_joins_the_record_of_that_day()
    {
        var l = Repo.CreateList(db, "L", Repo.CreateProject(db, "P"));
        var id = Repo.CreateTask(db, "Feita na lista", listId: l);
        Repo.SetDone(db, id, true, Iso(10, 12));
        Repo.CloseDay(db, DAY);
        Assert.Equal(1, Repo.DaySummary(db, DAY).Done);
    }

    [Fact]
    public void A_routine_reopens_on_each_due_day_and_never_stacks()
    {
        var id = Repo.CreateTask(db, "Regar as plantas");
        // Tuesday and Thursday, every week. DAY is a Tuesday.
        Repo.SetRecurrence(db, id, new("weekly", 1, (1 << 2) | (1 << 4), DAY), DAY);
        Assert.Equal(["Regar as plantas"], Repo.TodayTasks(db, DAY).Select(t => t.Title));

        Repo.SetDone(db, id, true, Iso(10, 12));
        // Wednesday: not due, the finished occurrence leaves Today.
        Assert.Equal([DAY], Repo.CloseMissedDays(db, "2026-11-11"));
        Assert.Empty(Repo.TodayTasks(db, "2026-11-11"));
        // Thursday: due, it comes back open.
        Repo.CloseMissedDays(db, "2026-11-12");
        var t = Repo.TodayTasks(db, "2026-11-12")[0];
        Assert.Equal(("Regar as plantas", null, 0), (t.Title, t.DoneAt, t.PostponeCount));

        // Not done on Thursday. By next Tuesday it is still one task, postponed 5 times, and each day shows it.
        Repo.CloseMissedDays(db, "2026-11-17");
        var today = Repo.TodayTasks(db, "2026-11-17");
        Assert.Single(today);
        Assert.Equal(5, today[0].PostponeCount);
        Assert.Equal(5, db.Select<int>("SELECT COUNT(*) FROM day_snapshots WHERE outcome = 'postponed'")[0]);
        Assert.Equal(1, db.Select<int>("SELECT COUNT(*) FROM day_snapshots WHERE outcome = 'done'")[0]);
    }

    [Fact]
    public void A_paused_routine_produces_nothing_and_resuming_does_not_bring_back_what_was_missed()
    {
        var id = Repo.CreateTask(db, "Alongamento");
        Repo.SetRecurrence(db, id, new("daily", 1, 0, DAY), DAY);
        Repo.SetDone(db, id, true, Iso(10, 12));
        Repo.SetRoutinePaused(db, id, true, DAY);
        Repo.CloseMissedDays(db, "2026-11-13");
        Assert.Empty(Repo.TodayTasks(db, "2026-11-13"));
        Repo.SetRoutinePaused(db, id, false, "2026-11-13");
        Assert.Equal([("Alongamento", 0)], Repo.TodayTasks(db, "2026-11-13").Select(t => (t.Title, t.PostponeCount)));
        Assert.Equal(["Alongamento"], Repo.Routines(db).Select(t => t.Title));

        Repo.SetRecurrence(db, id, null, "2026-11-13");
        Assert.Empty(Repo.Routines(db));
    }

    [Fact]
    public void Reports_read_snapshots_and_pomodoros_and_keep_deleted_tasks()
    {
        var l = Repo.CreateList(db, "Viagem", Repo.CreateProject(db, "Pessoal"));
        var a = Repo.CreateTask(db, "Roteiro", listId: l, todayDate: "2026-11-09");
        var b = Repo.CreateTask(db, "Sem projeto", todayDate: "2026-11-09");
        var at = Ms(9, 9);
        Repo.AddPomodoro(db, Rec(a, at, "completed"));
        Repo.AddPomodoro(db, Rec(b, at, "interrupted", seconds: 600));
        Repo.SetDone(db, b, true, Iso(9, 12));
        Repo.CloseMissedDays(db, "2026-11-11"); // closes the 9th and the 10th

        Assert.Equal([("2026-11-09", 1, 1, 1, 1), ("2026-11-10", 0, 1, 0, 0)],
            Repo.DayStats(db, "2026-11-09", "2026-11-15").Select(s => (s.Day, s.Done, s.Postponed, s.Completed, s.Interrupted)));
        Assert.Equal([("Pessoal", 1500), (null, 600)],
            Repo.FocusByProject(db, "2026-11-09", "2026-11-15").Select(f => (f.Project, f.Seconds)));
        Assert.Equal([("2026-11-09", 1)], Repo.FocusDays(db).Select(f => (f.Day, f.Count)));
        Assert.Equal("2026-11-09", Repo.FirstClosedDay(db));

        Repo.DeleteTask(db, a);
        var p = Assert.Single(Repo.MostPostponed(db, "2026-11-09", "2026-11-15"));
        Assert.Equal(("Roteiro", "Pessoal", "Viagem", 2, null, 0), (p.Title, p.Project, p.List, p.Times, p.DoneAt, p.Exists));
    }
}
