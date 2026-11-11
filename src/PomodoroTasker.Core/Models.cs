namespace PomodoroTasker.Core;

public sealed class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? CompletedAt { get; set; }
    public int Lists { get; set; }
    public int Open { get; set; }
    public int Done { get; set; }
    public int InToday { get; set; }
    public int Overdue { get; set; }
}

public sealed class TaskList
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public string Name { get; set; } = "";
    public string? ArchivedAt { get; set; }
    public string CreatedAt { get; set; } = "";
    public int Total { get; set; }
    public int Done { get; set; }
    public int InToday { get; set; }
}

public sealed class TaskItem
{
    public int Id { get; set; }
    public int? ListId { get; set; }
    public int? ParentId { get; set; }
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    /// <summary>0 low, 1 normal, 2 high.</summary>
    public int Priority { get; set; }
    public string Tags { get; set; } = "";
    public string? PlannedDate { get; set; }
    public string? DueDate { get; set; }
    public int PomodoroEnabled { get; set; }
    public string? TodayDate { get; set; }
    public string? DoneAt { get; set; }
    public int PostponeCount { get; set; }
    public string? RecFreq { get; set; }
    public int RecInterval { get; set; }
    public int RecDays { get; set; }
    public string? RecAnchor { get; set; }
    public int RecPaused { get; set; }
    public string? RecLast { get; set; }
    public string? ListName { get; set; }
    public string? ProjectName { get; set; }
    public string? ParentTitle { get; set; }
    public List<TaskItem> Subs { get; set; } = [];
}

public sealed class ListOption
{
    public int Id { get; set; }
    public string Label { get; set; } = "";
}

public sealed class Tomatoes
{
    public int Id { get; set; }
    public int Done { get; set; }
    public int Interrupted { get; set; }
}

public sealed class DaySummary
{
    public string Day { get; set; } = "";
    public int Done { get; set; }
    public int Postponed { get; set; }
    public int Tomatoes { get; set; }
    public int FocusSeconds { get; set; }
}

public sealed class HistoryDay
{
    public string Day { get; set; } = "";
    public int Done { get; set; }
    public int Interrupted { get; set; }
    public int Seconds { get; set; }
    public string? Reasons { get; set; }
    public int Postponed { get; set; }
}

public sealed class DayStats
{
    public string Day { get; set; } = "";
    public int Done { get; set; }
    public int Postponed { get; set; }
    public int Completed { get; set; }
    public int Interrupted { get; set; }
}

public sealed class ProjectFocus
{
    public string? Project { get; set; }
    public int Seconds { get; set; }
}

public sealed class FocusDay
{
    public string Day { get; set; } = "";
    public int Count { get; set; }
}

public sealed class Postponed
{
    public string Title { get; set; } = "";
    public string? Project { get; set; }
    public string? List { get; set; }
    public int Times { get; set; }
    /// <summary>null when the task is still open or no longer exists.</summary>
    public string? DoneAt { get; set; }
    public int Exists { get; set; }
}
