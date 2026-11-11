namespace PomodoroTasker.Core;

public sealed class Bar
{
    /// <summary>First day of the bar.</summary>
    public string From { get; set; } = "";
    public int Days { get; set; }
    public int Done { get; set; }
    public int Postponed { get; set; }
    public int Completed { get; set; }
    public int Interrupted { get; set; }
}

public sealed record Streaks(int Current, int Longest, int Total);

/// <summary>Report shaping as pure functions. The numbers come from day_snapshots and pomodoros only.</summary>
public static class Reports
{
    private static string Monday(string day) => Day.AddDays(day, -((Day.Weekday(day) + 6) % 7));

    /// <summary>Monday to Sunday of the week that holds day.</summary>
    public static (string From, string To) WeekOf(string day)
    {
        var from = Monday(day);
        return (from, Day.AddDays(from, 6));
    }

    public static (string From, string To) MonthOf(string day)
    {
        var d = Day.Date(day);
        return ($"{day[..8]}01", $"{day[..8]}{DateTime.DaysInMonth(d.Year, d.Month):00}");
    }

    /// <summary>One bar per day; periods longer than 14 days get one bar per week (Monday to Sunday).</summary>
    public static List<Bar> Bars(List<DayStats> rows, string from, string to)
    {
        var byDay = rows.ToDictionary(r => r.Day);
        var weekly = Day.DiffDays(to, from) >= 14;
        var bars = new List<Bar>();
        for (var d = from; string.CompareOrdinal(d, to) <= 0; d = Day.AddDays(d, 1))
        {
            var start = !weekly ? d : string.CompareOrdinal(Monday(d), from) < 0 ? from : Monday(d);
            if (bars.Count == 0 || bars[^1].From != start) bars.Add(new Bar { From = start });
            var bar = bars[^1];
            bar.Days++;
            if (!byDay.TryGetValue(d, out var r)) continue;
            bar.Done += r.Done;
            bar.Postponed += r.Postponed;
            bar.Completed += r.Completed;
            bar.Interrupted += r.Interrupted;
        }
        return bars;
    }

    /// <summary>
    /// Streaks of consecutive days with at least one completed pomodoro.
    /// The current streak still counts when today has none yet but yesterday had.
    /// </summary>
    public static Streaks Streaks(IEnumerable<string> days, string now)
    {
        var set = new SortedSet<string>(days, StringComparer.Ordinal);
        int longest = 0, run = 0;
        string? prev = null;
        foreach (var d in set)
        {
            run = prev != null && Day.DiffDays(d, prev) == 1 ? run + 1 : 1;
            longest = Math.Max(longest, run);
            prev = d;
        }
        var current = 0;
        for (var d = set.Contains(now) ? now : Day.AddDays(now, -1); set.Contains(d); d = Day.AddDays(d, -1)) current++;
        return new(current, longest, set.Count);
    }

    /// <summary>0 to 4, relative to the busiest day.</summary>
    public static int HeatLevel(int count, int max) =>
        count <= 0 || max <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(count / (double)max * 4));
}
