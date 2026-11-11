using PomodoroTasker.Core;

namespace PomodoroTasker.Web;

// What a Server Action answers to a screen. Times are epoch milliseconds as doubles.

public sealed class TimerView
{
    public string Day { get; set; } = "";
    public string Phase { get; set; } = "idle";
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = "";
    public double EndsAt { get; set; }
    /// <summary>-1 when not paused.</summary>
    public double PausedLeft { get; set; } = -1;
    public int Cycle { get; set; }
    public int Every { get; set; } = 4;
    public int FocusMinutes { get; set; } = 25;
    /// <summary>Pomodoros recorded by this call; screens reload their tomatoes.</summary>
    public int Recorded { get; set; }
    /// <summary>A phase ended: what the desktop app would notify.</summary>
    public string Flash { get; set; } = "";
    /// <summary>Two or more days were closed while the app was away.</summary>
    public bool Missed { get; set; }
}

public sealed class TodayView
{
    public string Day { get; set; } = "";
    public string DayLabel { get; set; } = "";
    public List<TaskItem> Tasks { get; set; } = [];
    /// <summary>The task of the running focus, 0 when none.</summary>
    public int FocusTaskId { get; set; }
    public bool FocusPaused { get; set; }
}
