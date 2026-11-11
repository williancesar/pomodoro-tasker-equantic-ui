using System.Globalization;

namespace PomodoroTasker.Core;

/// <summary>A Day is a local calendar date as 'YYYY-MM-DD'. All day arithmetic goes through here.</summary>
public static class Day
{
    public static string Of(DateTime local) => local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string Today() => Of(DateTime.Now);

    /// <summary>The local day of an instant given as epoch milliseconds.</summary>
    public static string OfMs(long ms) => Of(DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime);

    /// <summary>The local day of an ISO instant.</summary>
    public static string OfIso(string iso) =>
        Of(DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).LocalDateTime);

    public static string Iso(DateTimeOffset at) =>
        at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static DateOnly Date(string day) => DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string Of(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string AddDays(string day, int n) => Of(Date(day).AddDays(n));
    public static int DiffDays(string a, string b) => Date(a).DayNumber - Date(b).DayNumber;
    /// <summary>Sunday = 0.</summary>
    public static int Weekday(string day) => (int)Date(day).DayOfWeek;

    private static readonly string[] WeekdayName = ["domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado"];
    private static readonly string[] WeekdayLongName = ["domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado"];
    private static readonly string[] Month = ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];
    public static readonly string[] MonthLong = ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];

    /// <summary>"terça-feira, 10 de novembro"</summary>
    public static string LongDay(string day)
    {
        var d = Date(day);
        return $"{WeekdayLongName[(int)d.DayOfWeek]}, {d.Day} de {MonthLong[d.Month - 1]}";
    }

    /// <summary>"hoje", "amanhã", "ontem", "quinta" (within the next 6 days), otherwise "12 nov"</summary>
    public static string ShortDay(string day, string reference)
    {
        var n = DiffDays(day, reference);
        if (n == 0) return "hoje";
        if (n == 1) return "amanhã";
        if (n == -1) return "ontem";
        var d = Date(day);
        return n is > 1 and < 7 ? WeekdayName[(int)d.DayOfWeek] : $"{d.Day} {Month[d.Month - 1]}";
    }

    /// <summary>Deadline label for a task row. Late is true once the deadline day has passed.</summary>
    public static (string Text, bool Late) DeadlineLabel(string due, string reference)
    {
        var n = DiffDays(due, reference);
        if (n >= 0) return ($"prazo {ShortDay(due, reference)}", false);
        return n == -1 ? ("venceu ontem", true) : ($"venceu há {-n} dias", true);
    }

    /// <summary>"sexta, 6 nov"</summary>
    public static string MediumDay(string day)
    {
        var d = Date(day);
        return $"{WeekdayName[(int)d.DayOfWeek]}, {d.Day} {Month[d.Month - 1]}";
    }

    public static string WeekdayLong(string day) => WeekdayLongName[Weekday(day)];

    /// <summary>9000 -> "2h30", 1500 -> "25 min"</summary>
    public static string Duration(int seconds)
    {
        var m = (int)Math.Round(seconds / 60.0, MidpointRounding.AwayFromZero);
        return m < 60 ? $"{m} min" : $"{m / 60}h{m % 60:00}";
    }

    /// <summary>"10 nov", with the year when it is not the reference's year.</summary>
    public static string DayMonth(string day, string? reference = null)
    {
        var d = Date(day);
        var year = reference != null && reference[..4] != day[..4] ? $" {d.Year}" : "";
        return $"{d.Day} {Month[d.Month - 1]}{year}";
    }

    /// <summary>"seg 9"</summary>
    public static string WeekdayShort(string day)
    {
        var d = Date(day);
        return $"{WeekdayName[(int)d.DayOfWeek][..3]} {d.Day}";
    }
}
