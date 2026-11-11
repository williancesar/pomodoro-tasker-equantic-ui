using eQuantic.UI.Components;
using eQuantic.UI.Lucide;
using eQuantic.UI.Primitives;
using PomodoroTasker.Web.Components;
using PomodoroTasker.Web.Server;

namespace PomodoroTasker.Web.Pages;

/// <summary>Rotinas: every task that has a recurrence rule, with its pause switch.</summary>
[Page("/rotinas", Title = "Rotinas · pomodoro-tasker")]
public sealed class RoutinesPage : StatefulComponent
{
    private List<RoutineLine>? _routines;
    private int _editing;

    [ServerAction]
    public Task<List<RoutineLine>> Load() => Task.FromResult(Api.Routines());

    [ServerAction]
    public Task<List<RoutineLine>> Pause(int id, bool paused) { Api.PauseRoutine(id, paused); return Load(); }

    [ServerAction]
    public Task<List<RoutineLine>> Stop(int id) { Api.SetRule(id, "", 1, 0, ""); return Load(); }

    protected override void OnMount()
    {
        Bus.Data = () => _ = Show(Load());
        _ = Show(Load());
    }

    protected override void OnUnmount() => Bus.Data = null;

    private async Task Show(Task<List<RoutineLine>> change)
    {
        var routines = await change;
        SetState(() => _routines = routines);
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var routines = _routines ?? [];
        var active = routines.Count(r => !r.Paused);
        var children = new List<VisualNode>
        {
            Parts.Heading(theme, T.RecRoutines, routines.Count == 0 ? "" : T.RecCounts(active, routines.Count - active)),
        };
        if (_routines is { Count: 0 })
        {
            children.Add(new EmptyState(new Icon(LucideIcons.Repeat, 32), T.RecEmptyTitle, T.RecEmptyText));
            children.Add(Parts.Note(theme, T.RecHow));
        }
        foreach (var r in routines)
        {
            children.Add(Parts.Line(theme, r.Title, r.Text, Row(gap: Space.S2, cross: CrossAlign.Center, children: [
                new Switch(!r.Paused, () => _ = Show(Pause(r.Id, !r.Paused)))
                {
                    Label = $"{(r.Paused ? T.RecPaused : T.RecActive)}: {r.Title}",
                },
                new Menu(new IconButton(new Icon(LucideIcons.Ellipsis, 20), $"{T.More}: {r.Title}"),
                    [new MenuItem(T.RecEditRule), new MenuItem(T.RecStop) { Destructive = true }],
                    index =>
                    {
                        if (index == 0) SetState(() => _editing = r.Id);
                        else _ = Show(Stop(r.Id));
                    }),
            ]), quiet: r.Paused, note: r.Note));
        }
        if (routines.Count > 0) children.Add(Parts.Note(theme, T.RecHow));
        if (_editing != 0)
            children.Add(new TaskEditor
            {
                TaskId = _editing,
                OnClose = () => { SetState(() => _editing = 0); _ = Show(Load()); },
            });
        return AppShell("/rotinas", Column(gap: Space.S4, children: [.. children]));
    }
}
