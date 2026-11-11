using PomodoroTasker.Core;

namespace PomodoroTasker.Core.Tests;

public class ReportsTests
{
    private static DayStats Row(string day, int done = 0, int postponed = 0, int completed = 0, int interrupted = 0) =>
        new() { Day = day, Done = done, Postponed = postponed, Completed = completed, Interrupted = interrupted };

    [Fact]
    public void Week_is_Monday_to_Sunday_and_month_is_the_calendar_month()
    {
        Assert.Equal(("2026-11-09", "2026-11-15"), Reports.WeekOf("2026-11-10"));
        Assert.Equal(("2026-11-09", "2026-11-15"), Reports.WeekOf("2026-11-15"));
        Assert.Equal(("2026-02-01", "2026-02-28"), Reports.MonthOf("2026-02-10"));
    }

    [Fact]
    public void A_week_has_one_bar_per_day_including_days_with_nothing()
    {
        var b = Reports.Bars([Row("2026-11-10", 3, 1, 4, 1)], "2026-11-09", "2026-11-15");
        Assert.Equal(7, b.Count);
        Assert.Equal(("2026-11-10", 3, 1, 4, 1), (b[1].From, b[1].Done, b[1].Postponed, b[1].Completed, b[1].Interrupted));
        Assert.Equal(("2026-11-09", 0), (b[0].From, b[0].Done));
    }

    [Fact]
    public void A_period_longer_than_14_days_is_grouped_by_week_with_a_short_first_week()
    {
        // 2026-10-01 is a Thursday.
        var b = Reports.Bars([Row("2026-10-01", 1), Row("2026-10-04", 2), Row("2026-10-05", 4)], "2026-10-01", "2026-10-18");
        Assert.Equal([("2026-10-01", 4, 3), ("2026-10-05", 7, 4), ("2026-10-12", 7, 0)], b.Select(x => (x.From, x.Days, x.Done)));
    }

    [Fact]
    public void Streaks_current_survives_a_today_with_no_pomodoro_yet_and_breaks_on_a_gap()
    {
        string[] days = ["2026-11-01", "2026-11-02", "2026-11-03", "2026-11-08", "2026-11-09"];
        Assert.Equal(new Streaks(2, 3, 5), Reports.Streaks(days, "2026-11-10"));
        Assert.Equal(new Streaks(2, 3, 5), Reports.Streaks(days, "2026-11-09"));
        Assert.Equal(0, Reports.Streaks(days, "2026-11-11").Current);
        Assert.Equal(new Streaks(0, 0, 0), Reports.Streaks([], "2026-11-11"));
    }

    [Fact]
    public void Heat_levels_run_from_0_to_4_relative_to_the_busiest_day()
    {
        Assert.Equal([0, 1, 1, 2, 3, 4], new[] { 0, 1, 2, 4, 6, 8 }.Select(n => Reports.HeatLevel(n, 8)));
        Assert.Equal(0, Reports.HeatLevel(3, 0));
    }
}
