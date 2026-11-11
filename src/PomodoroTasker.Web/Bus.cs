namespace PomodoroTasker.Web;

/// <summary>
/// How the timer block and the screen beside it tell each other to reload, in the browser.
/// One listener each: the timer block, and the page that is mounted.
/// </summary>
public static class Bus
{
    /// <summary>The timer changed outside the timer block (a focus started from a task row).</summary>
    public static Action? Timer { get; set; }
    /// <summary>Tasks or tomatoes changed outside the page (a pomodoro was recorded, the day changed).</summary>
    public static Action? Data { get; set; }
}
