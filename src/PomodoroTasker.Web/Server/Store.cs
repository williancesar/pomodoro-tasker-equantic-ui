using eQuantic.UI.Primitives;
using PomodoroTasker.Core;

namespace PomodoroTasker.Web.Server;

/// <summary>The one database of this single-user app, opened once.</summary>
[ServerOnly]
public static class Store
{
    private static readonly Lazy<Db> Lazy = new(() =>
        new Db(Environment.GetEnvironmentVariable("TASKER_DB") ?? "tasker.db"));

    public static Db Db => Lazy.Value;
}
