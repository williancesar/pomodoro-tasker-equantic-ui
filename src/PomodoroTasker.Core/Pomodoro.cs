namespace PomodoroTasker.Core;

/// <summary>Phases are stored as these strings, the same the desktop app writes.</summary>
public static class Phase
{
    public const string Idle = "idle", Focus = "focus", Short = "short", Long = "long", Wait = "wait";
}

public sealed record TimerState
{
    public string Phase { get; init; } = Core.Phase.Idle;
    public int? TaskId { get; init; }
    /// <summary>When the running phase ends. null while paused, idle or waiting.</summary>
    public long? EndsAt { get; init; }
    /// <summary>Milliseconds left when the phase was paused.</summary>
    public long? PausedLeft { get; init; }
    /// <summary>Completed pomodoros in the current cycle.</summary>
    public int Cycle { get; init; }
    public long? StartedAt { get; init; }
    /// <summary>Length of the running focus in ms. Settings may change while it runs.</summary>
    public long? FocusMs { get; init; }
}

/// <summary>Minutes, and how many pomodoros make a cycle.</summary>
public sealed record Durations(int Focus = 25, int Short = 5, int Long = 15, int Every = 4);

public enum TimerEvent { Start, Pause, Resume, Stop, Finish, Skip, Expire }

public sealed record PomodoroRecord(int TaskId, long StartedAt, long EndedAt, int Seconds, string Status, string Reason);

/// <summary>
/// The pomodoro cycle as a pure state machine. Times are epoch milliseconds.
/// The clock shown to the user is always derived from EndsAt, never counted down.
/// </summary>
public static class Pomodoro
{
    private const long Min = 60_000;
    public static readonly TimerState IdleState = new();

    /// <summary>Milliseconds left in the current phase, or null when no clock is running or paused.</summary>
    public static long? Left(TimerState s, long now) =>
        s.PausedLeft ?? (s.EndsAt == null ? null : Math.Max(0, s.EndsAt.Value - now));

    public static bool Paused(TimerState s) => s.PausedLeft != null;

    private static int Seconds(long ms) => (int)Math.Round(ms / 1000.0, MidpointRounding.AwayFromZero);

    public static (TimerState State, PomodoroRecord? Record) Step(
        TimerState s, TimerEvent e, Durations d, long now, int? taskId = null, string? reason = null)
    {
        var idle = IdleState with { Cycle = s.Cycle };
        switch (e)
        {
            case TimerEvent.Start:
                if (s.Phase == Phase.Focus) return (s, null);
                // A finished cycle starts over with the next focus.
                return (new TimerState
                {
                    Phase = Phase.Focus, TaskId = taskId, EndsAt = now + d.Focus * Min,
                    Cycle = s.Cycle >= d.Every ? 0 : s.Cycle, StartedAt = now, FocusMs = d.Focus * Min,
                }, null);
            case TimerEvent.Pause:
                if (s.EndsAt == null) return (s, null);
                return (s with { PausedLeft = Math.Max(0, s.EndsAt.Value - now), EndsAt = null }, null);
            case TimerEvent.Resume:
                if (s.PausedLeft == null) return (s, null);
                return (s with { EndsAt = now + s.PausedLeft, PausedLeft = null }, null);
            case TimerEvent.Skip:
                if (s.Phase != Phase.Short && s.Phase != Phase.Long) return (s, null);
                return (s with { Phase = Phase.Wait, EndsAt = null, PausedLeft = null }, null);
            case TimerEvent.Stop:
            {
                if (s.Phase != Phase.Focus || s.TaskId == null) return (idle, null);
                // An interrupted pomodoro gives no tomato and does not advance the cycle.
                var seconds = Seconds((s.FocusMs ?? d.Focus * Min) - (Left(s, now) ?? 0));
                return (idle, new(s.TaskId.Value, s.StartedAt ?? now, now, seconds, "interrupted", reason?.Trim() ?? ""));
            }
            case TimerEvent.Finish:
            {
                if (s.Phase != Phase.Focus || s.TaskId == null) return (idle, null);
                // Past the halfway point the pomodoro counts, with the time actually spent; before it, it
                // is an interruption. Either way the timer goes idle: there is no task left to rest from.
                var focusMs = s.FocusMs ?? d.Focus * Min;
                var spent = focusMs - (Left(s, now) ?? 0);
                var counts = spent >= focusMs / 2.0;
                return (idle with { Cycle = counts ? s.Cycle + 1 : s.Cycle },
                    new(s.TaskId.Value, s.StartedAt ?? now, now, Seconds(spent),
                        counts ? "completed" : "interrupted", counts ? "" : reason?.Trim() ?? ""));
            }
            default: // Expire
            {
                if (s.EndsAt == null || s.EndsAt > now) return (s, null);
                if (s.Phase == Phase.Focus && s.TaskId != null)
                {
                    var focusMs = s.FocusMs ?? d.Focus * Min;
                    var cycle = s.Cycle + 1;
                    var isLong = cycle >= d.Every;
                    // The break is anchored to the scheduled end, so a late wake-up does not stretch it.
                    return (s with
                    {
                        Phase = isLong ? Phase.Long : Phase.Short,
                        EndsAt = s.EndsAt + (isLong ? d.Long : d.Short) * Min,
                        Cycle = cycle, StartedAt = s.EndsAt, FocusMs = null,
                    }, new(s.TaskId.Value, s.StartedAt ?? s.EndsAt.Value - focusMs, s.EndsAt.Value, Seconds(focusMs), "completed", ""));
                }
                return (s with { Phase = Phase.Wait, EndsAt = null, PausedLeft = null }, null);
            }
        }
    }

    /// <summary>Applies every phase end that has already passed (late wake-up, or the app was closed).</summary>
    public static (TimerState State, List<PomodoroRecord> Records) CatchUp(TimerState s, Durations d, long now)
    {
        var records = new List<PomodoroRecord>();
        while (s.EndsAt != null && s.EndsAt <= now)
        {
            var (state, record) = Step(s, TimerEvent.Expire, d, now);
            s = state;
            if (record != null) records.Add(record);
        }
        return (s, records);
    }

    public static string Clock(long ms)
    {
        var total = (long)Math.Ceiling(ms / 1000.0);
        return $"{total / 60:00}:{total % 60:00}";
    }
}
