namespace PomodoroTasker.Core;

/// <param name="Freq">"daily" or "weekly".</param>
/// <param name="Interval">Every N weeks. Only for weekly.</param>
/// <param name="Days">Bitmask of weekdays, Sunday = 1. Only for weekly.</param>
/// <param name="Anchor">First day the rule applies.</param>
public sealed record Rule(string Freq, int Interval, int Days, string Anchor);

/// <summary>Recurrence rules as pure functions over Days.</summary>
public static class Recurrence
{
    private static string WeekStart(string day) => Day.AddDays(day, -Day.Weekday(day));

    public static bool IsDue(Rule r, string day)
    {
        if (string.CompareOrdinal(day, r.Anchor) < 0) return false;
        if (r.Freq == "daily") return true;
        if ((r.Days & (1 << Day.Weekday(day))) == 0) return false;
        var weeks = Day.DiffDays(WeekStart(day), WeekStart(r.Anchor)) / 7;
        return weeks % Math.Max(1, r.Interval) == 0;
    }

    /// <summary>The next count due days on or after from.</summary>
    public static List<string> NextDue(Rule r, string from, int count = 1)
    {
        var due = new List<string>();
        if (r.Freq == "weekly" && r.Days == 0) return due;
        var d = string.CompareOrdinal(from, r.Anchor) < 0 ? r.Anchor : from;
        // A weekly rule is due at least once every Interval weeks.
        for (var i = 0; due.Count < count && i < 7 * Math.Max(1, r.Interval) * (count + 1); i++, d = Day.AddDays(d, 1))
            if (IsDue(r, d)) due.Add(d);
        return due;
    }

    private static readonly string[] Names = ["domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado"];
    private static readonly string[] Short = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];

    private static string Join(List<string> xs) =>
        xs.Count < 2 ? string.Concat(xs) : $"{string.Join(", ", xs.Take(xs.Count - 1))} e {xs[^1]}";

    /// <summary>"Todo dia", "Toda terça e quinta, a cada 2 semanas", "Seg, qua e sex"</summary>
    public static string Describe(Rule r)
    {
        if (r.Freq == "daily") return "Todo dia";
        var picked = Enumerable.Range(0, 7).Where(i => (r.Days & (1 << i)) != 0).ToList();
        var every = r.Interval > 1 ? $", a cada {r.Interval} semanas" : "";
        if (picked.Count == 7 && r.Interval <= 1) return "Todo dia";
        if (picked.Count > 2)
        {
            var s = Join(picked.Select(i => Short[i]).ToList());
            return char.ToUpper(s[0]) + s[1..] + every;
        }
        // "sábado" and "domingo" are masculine.
        var article = picked[0] is 0 or 6 ? "Todo" : "Toda";
        return $"{article} {Join(picked.Select(i => Names[i]).ToList())}{every}";
    }
}
