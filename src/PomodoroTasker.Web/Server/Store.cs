using eQuantic.UI.Primitives;
using PomodoroTasker.Core;

namespace PomodoroTasker.Web.Server;

/// <summary>The one database of this single-user app, opened once.</summary>
[ServerOnly]
public static class Store
{
    /// <summary>The database file: TASKER_DB, or tasker.db where the server runs.</summary>
    public static string Path => Environment.GetEnvironmentVariable("TASKER_DB") ?? "tasker.db";

    private static readonly Lazy<Db> Lazy = new(() => new Db(Path));

    public static Db Db => Lazy.Value;
}
