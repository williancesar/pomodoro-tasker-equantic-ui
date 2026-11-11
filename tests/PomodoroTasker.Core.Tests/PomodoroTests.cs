using PomodoroTasker.Core;
using static PomodoroTasker.Core.Pomodoro;

namespace PomodoroTasker.Core.Tests;

public class PomodoroTests
{
    private const long Min = 60_000;
    private const long T0 = 1_000_000_000_000;
    private static readonly Durations D = new();

    private static TimerState Focus(TimerState? from = null, long at = T0) =>
        Step(from ?? IdleState, TimerEvent.Start, D, at, taskId: 7).State;

    [Fact]
    public void Focus_ends_in_a_short_break_with_a_completed_record_then_waits_for_the_user()
    {
        var s = Focus();
        Assert.Equal((Phase.Focus, 7, T0 + 25 * Min, 0), (s.Phase, s.TaskId, s.EndsAt, s.Cycle));
        Assert.Same(s, Step(s, TimerEvent.Expire, D, T0 + 24 * Min).State); // not yet

        var end = Step(s, TimerEvent.Expire, D, T0 + 25 * Min);
        Assert.Equal(new PomodoroRecord(7, T0, T0 + 25 * Min, 1500, "completed", ""), end.Record);
        Assert.Equal((Phase.Short, T0 + 30 * Min, 1, 7), (end.State.Phase, end.State.EndsAt, end.State.Cycle, end.State.TaskId));

        s = Step(end.State, TimerEvent.Expire, D, T0 + 30 * Min).State;
        Assert.Equal((Phase.Wait, null, 7, 1), (s.Phase, s.EndsAt, s.TaskId, s.Cycle));
    }

    [Fact]
    public void The_fourth_pomodoro_is_followed_by_a_long_break_and_the_next_focus_starts_a_new_cycle()
    {
        var s = Focus(IdleState with { Cycle = 3 });
        s = Step(s, TimerEvent.Expire, D, s.EndsAt!.Value).State;
        Assert.Equal((Phase.Long, 4, T0 + (25 + 15) * Min), (s.Phase, s.Cycle, s.EndsAt));
        s = Step(s, TimerEvent.Expire, D, s.EndsAt!.Value).State;
        Assert.Equal(0, Focus(s, T0 + 60 * Min).Cycle);
    }

    [Fact]
    public void Pause_freezes_the_clock_and_resume_continues_from_where_it_stopped()
    {
        var s = Focus();
        s = Step(s, TimerEvent.Pause, D, T0 + 10 * Min).State;
        Assert.Equal(15 * Min, Left(s, T0 + 99 * Min));
        Assert.Same(s, Step(s, TimerEvent.Expire, D, T0 + 99 * Min).State); // a paused phase never expires
        s = Step(s, TimerEvent.Resume, D, T0 + 20 * Min).State;
        Assert.Equal(T0 + 35 * Min, s.EndsAt);
    }

    [Fact]
    public void Stopping_a_focus_records_an_interruption_without_advancing_the_cycle()
    {
        var s = Focus(IdleState with { Cycle = 2 });
        s = Step(s, TimerEvent.Pause, D, T0 + 10 * Min).State;
        var r = Step(s, TimerEvent.Stop, D, T0 + 40 * Min, reason: " reunião ");
        Assert.Equal(("interrupted", 600, "reunião", 7), (r.Record!.Status, r.Record.Seconds, r.Record.Reason, r.Record.TaskId));
        Assert.Equal(IdleState with { Cycle = 2 }, r.State);
    }

    [Fact]
    public void A_break_can_be_paused_and_skipped_and_stopping_a_break_records_nothing()
    {
        var s = Step(Focus(), TimerEvent.Expire, D, T0 + 25 * Min).State;
        Assert.Equal(4 * Min, Step(s, TimerEvent.Pause, D, T0 + 26 * Min).State.PausedLeft);
        Assert.Equal(Phase.Wait, Step(s, TimerEvent.Skip, D, T0 + 26 * Min).State.Phase);
        var stop = Step(s, TimerEvent.Stop, D, T0 + 26 * Min);
        Assert.Null(stop.Record);
        Assert.Equal(Phase.Idle, stop.State.Phase);
    }

    [Fact]
    public void Catch_up_applies_every_phase_end_missed_while_the_app_was_closed()
    {
        var r = CatchUp(Focus(), D, T0 + 3 * 60 * Min);
        Assert.Single(r.Records);
        Assert.Equal(T0 + 25 * Min, r.Records[0].EndedAt);
        Assert.Equal((Phase.Wait, 1), (r.State.Phase, r.State.Cycle));
        Assert.Equal(Phase.Short, CatchUp(Focus(), D, T0 + 26 * Min).State.Phase);
    }

    [Fact]
    public void Clock_rounds_up_to_the_next_second()
    {
        Assert.Equal("25:00", Clock(25 * Min));
        Assert.Equal("01:02", Clock(61_001));
        Assert.Equal("00:00", Clock(0));
    }

    [Fact]
    public void A_focus_keeps_the_length_it_started_with_when_the_setting_changes_while_it_runs()
    {
        var s = Focus();
        var longer = D with { Focus = 50 };
        var expired = Step(s, TimerEvent.Expire, longer, s.EndsAt!.Value).Record!;
        Assert.Equal((1500, T0), (expired.Seconds, expired.StartedAt));
        Assert.Equal(600, Step(s, TimerEvent.Stop, longer, T0 + 10 * Min).Record!.Seconds);
    }

    [Fact]
    public void Finishing_the_task_past_half_of_the_focus_counts_the_pomodoro_and_before_half_it_does_not()
    {
        var s = Focus(IdleState with { Cycle = 1 });
        var late = Step(s, TimerEvent.Finish, D, T0 + 13 * Min, reason: "Tarefa concluída");
        Assert.Equal(("completed", 780, ""), (late.Record!.Status, late.Record.Seconds, late.Record.Reason));
        Assert.Equal(IdleState with { Cycle = 2 }, late.State);

        var half = Step(s, TimerEvent.Finish, D, T0 + 25 * Min / 2);
        Assert.Equal("completed", half.Record!.Status); // exactly half counts

        var early = Step(s, TimerEvent.Finish, D, T0 + 12 * Min, reason: "Tarefa concluída");
        Assert.Equal(("interrupted", 720, "Tarefa concluída"), (early.Record!.Status, early.Record.Seconds, early.Record.Reason));
        Assert.Equal(IdleState with { Cycle = 1 }, early.State);

        // paused time does not count as time spent
        var paused = Step(s, TimerEvent.Pause, D, T0 + 5 * Min).State;
        Assert.Equal("interrupted", Step(paused, TimerEvent.Finish, D, T0 + 60 * Min).Record!.Status);

        // during a break there is nothing to record
        var brk = Step(s, TimerEvent.Expire, D, s.EndsAt!.Value).State;
        var done = Step(brk, TimerEvent.Finish, D, brk.EndsAt!.Value - Min);
        Assert.Null(done.Record);
        Assert.Equal(Phase.Idle, done.State.Phase);
    }
}
