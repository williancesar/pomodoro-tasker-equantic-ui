using PomodoroTasker.Core;
using static PomodoroTasker.Core.Recurrence;

namespace PomodoroTasker.Core.Tests;

public class RecurrenceTests
{
    // 2026-11-10 is a Tuesday.
    private const int Tue = 1 << 2, Thu = 1 << 4, Mon = 1 << 1, Wed = 1 << 3, Fri = 1 << 5, Sat = 1 << 6, Sun = 1;
    private static readonly Rule Daily = new("daily", 1, 0, "2026-11-10");
    private static Rule Weekly(int days, int interval = 1, string anchor = "2026-11-10") => new("weekly", interval, days, anchor);

    [Fact]
    public void Daily_is_due_every_day_from_the_anchor_on()
    {
        Assert.False(IsDue(Daily, "2026-11-09"));
        Assert.True(IsDue(Daily, "2026-11-10"));
        Assert.Equal(["2026-11-10", "2026-11-11", "2026-11-12"], NextDue(Daily, "2026-11-10", 3));
    }

    [Fact]
    public void Tuesday_and_Thursday_every_2_weeks()
    {
        var r = Weekly(Tue | Thu, 2);
        Assert.Equal(["2026-11-10", "2026-11-12", "2026-11-24", "2026-11-26", "2026-12-08"], NextDue(r, "2026-11-10", 5));
        Assert.False(IsDue(r, "2026-11-17")); // the off week
        Assert.False(IsDue(r, "2026-11-11")); // a Wednesday
    }

    [Fact]
    public void An_anchor_in_the_middle_of_the_week_still_counts_whole_weeks()
    {
        // Anchor on Thursday, rule on Monday every 2 weeks: the Monday of the anchor week is before
        // the anchor, so the first occurrence is two weeks later.
        Assert.Equal(["2026-11-23", "2026-12-07"], NextDue(Weekly(Mon, 2, "2026-11-12"), "2026-11-12", 2));
    }

    [Fact]
    public void A_weekly_rule_with_no_day_is_never_due() => Assert.Empty(NextDue(Weekly(0), "2026-11-10"));

    [Fact]
    public void Rules_read_in_plain_Portuguese()
    {
        Assert.Equal("Todo dia", Describe(Daily));
        Assert.Equal("Toda terça e quinta", Describe(Weekly(Tue | Thu)));
        Assert.Equal("Toda terça e quinta, a cada 2 semanas", Describe(Weekly(Tue | Thu, 2)));
        Assert.Equal("Seg, qua e sex", Describe(Weekly(Mon | Wed | Fri)));
        Assert.Equal("Todo domingo e sábado", Describe(Weekly(Sat | Sun)));
        Assert.Equal("Toda sexta", Describe(Weekly(Fri)));
        Assert.Equal("Todo dia", Describe(Weekly(127)));
    }
}
