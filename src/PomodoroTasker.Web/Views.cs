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

public sealed class HistoryLine
{
    public string Day { get; set; } = "";
    public string Text { get; set; } = "";
    public int Toms { get; set; }
    public int TomsInterrupted { get; set; }
}

/// <summary>What the task editor shows. Task is null when the task no longer exists.</summary>
public sealed class EditorView
{
    public string Day { get; set; } = "";
    public TaskItem? Task { get; set; }
    public List<ListOption> Lists { get; set; } = [];
    public List<HistoryLine> History { get; set; } = [];
}

/// <summary>The editable fields of a task. Dates are 'YYYY-MM-DD', empty for none; ListId 0 for no list.</summary>
public sealed class TaskEdit
{
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public int ListId { get; set; }
    public int Priority { get; set; } = 1;
    public string Tags { get; set; } = "";
    public string Planned { get; set; } = "";
    public string Due { get; set; } = "";
    public bool Pomodoro { get; set; }
}

/// <summary>Dates between a date picker and the 'YYYY-MM-DD' strings the server speaks.</summary>
public static class Dates
{
    public static DateOnly? Parse(string? day) =>
        string.IsNullOrEmpty(day) ? null
            : new DateOnly(int.Parse(day.Substring(0, 4)), int.Parse(day.Substring(5, 2)), int.Parse(day.Substring(8, 2)));

    public static string Str(DateOnly? date) =>
        date is { } d ? $"{d.Year:0000}-{d.Month:00}-{d.Day:00}" : "";
}

public sealed class ListsView
{
    public List<TaskList> Lists { get; set; } = [];
    public int Archived { get; set; }
}

public sealed class LooseListView
{
    public TaskList? List { get; set; }
    public string ArchivedLabel { get; set; } = "";
    public List<TaskItem> Tasks { get; set; } = [];
}

public sealed class ProjectsView
{
    public List<Project> Projects { get; set; } = [];
    /// <summary>"concluído ontem", by project id, for completed projects.</summary>
    public List<string> CompletedLabels { get; set; } = [];
}

public sealed class ProjectListView
{
    public TaskList List { get; set; } = new();
    public List<TaskItem> Tasks { get; set; } = [];
}

public sealed class ProjectView
{
    public Project? Project { get; set; }
    public List<ProjectListView> Lists { get; set; } = [];
}

public sealed class RoutineLine
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string Note { get; set; } = "";
    public bool Paused { get; set; }
}

public sealed class CloseView
{
    public string DayLabel { get; set; } = "";
    public string TomorrowLabel { get; set; } = "";
    public bool Closed { get; set; }
    public List<TaskItem> Open { get; set; } = [];
    public List<TaskItem> Done { get; set; } = [];
    public List<TaskItem> Waiting { get; set; } = [];
    public int Tomatoes { get; set; }
    public string Focus { get; set; } = "";
    public int DoneCount { get; set; }
    public int Postponed { get; set; }
    public int SentBack { get; set; }
}

public sealed class MissedView
{
    public List<RoutineLine> Days { get; set; } = [];
    public List<TaskItem> Carried { get; set; } = [];
}

public sealed class RankLine
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public int Times { get; set; }
}

public sealed class ReportsView
{
    public bool Empty { get; set; }
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Range { get; set; } = "";
    public bool CanNext { get; set; }
    /// <summary>-1 when nothing was planned in the period.</summary>
    public int Rate { get; set; } = -1;
    public bool Weekly { get; set; }
    public List<string> Labels { get; set; } = [];
    public List<double> Done { get; set; } = [];
    public List<double> Postponed { get; set; } = [];
    public List<double> Completed { get; set; } = [];
    public List<double> Interrupted { get; set; } = [];
    public string Best { get; set; } = "";
    public string FocusTotal { get; set; } = "";
    public List<string> ProjectNames { get; set; } = [];
    public List<double> ProjectMinutes { get; set; } = [];
    /// <summary>Weeks as columns, Sunday on top: 0 to 4, or -1 for a day still to come.</summary>
    public List<int> Heat { get; set; } = [];
    public int StreakNow { get; set; }
    public int StreakBest { get; set; }
    public int StreakTotal { get; set; }
    public List<RankLine> MostPostponed { get; set; } = [];
}

public sealed class SettingsView
{
    public int Focus { get; set; }
    public int Short { get; set; }
    public int Long { get; set; }
    public int Every { get; set; }
    public string Theme { get; set; } = "auto";
    public bool Running { get; set; }
    public string Database { get; set; } = "";
    public string About { get; set; } = "";
}
