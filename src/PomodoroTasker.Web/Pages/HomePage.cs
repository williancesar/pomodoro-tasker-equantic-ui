using eQuantic.UI.Components;
using eQuantic.UI.Lucide;
using eQuantic.UI.Primitives;
using PomodoroTasker.Web.Components;
using PomodoroTasker.Web.Server;

namespace PomodoroTasker.Web.Pages;

/// <summary>Hoje: the tasks of the day, quick capture, and the way into closing the day.</summary>
[Page("/", Title = "Hoje · pomodoro-tasker")]
public sealed class HomePage : StatefulComponent
{
    private TodayView? _view;
    private string _title = "";
    private int _editing;

    [ServerAction]
    public Task<TodayView> Load() => Task.FromResult(Api.Today());

    [ServerAction]
    public Task<TodayView> Add(string title)
    {
        Api.AddTask(title, today: true);
        return Task.FromResult(Api.Today());
    }

    protected override void OnMount()
    {
        Bus.Data = () => _ = Reload();
        _ = Reload();
    }

    protected override void OnUnmount() => Bus.Data = null;

    private async Task Reload()
    {
        var view = await Load();
        SetState(() => _view = view);
    }

    private async Task AddOne()
    {
        if (_title.Trim() == "") return;
        var view = await Add(_title);
        SetState(() => { _view = view; _title = ""; });
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var children = new List<VisualNode>
        {
            Parts.Heading(theme, T.NavToday, _view?.DayLabel),
        };
        if (_view == null) return AppShell("/", Column(gap: Space.S4, children: [.. children]));

        var tasks = _view.Tasks;
        var done = tasks.Count(t => t.DoneAt != null);
        var tomatoes = tasks.Sum(t => t.Toms);
        var carried = tasks.Count(t => t.PostponeCount > 0 && t.DoneAt == null);

        if (tasks.Count > 0)
            children.Add(Row(gap: Space.S5, wrap: true, children: [
                Text(T.TodayDoneOf(done, tasks.Count), TypeRole.BodyM, theme.TextSecondary),
                Text(T.Tomatoes(tomatoes), TypeRole.BodyM, theme.TextSecondary),
                .. carried > 0 ? new VisualNode[] { Text(T.TodayCarried(carried), TypeRole.BodyM, theme.TextSecondary) } : [],
            ]));

        children.Add(Row(gap: Space.S2, cross: CrossAlign.End, children: [
            Flexible(new TextEntry(_title, value => SetState(() => _title = value))
            {
                Placeholder = T.TodayNewTask, Label = T.TodayNewTask, OnSubmit = () => _ = AddOne(),
            }),
            Button(T.Add, onPressed: () => _ = AddOne()),
        ]));

        if (tasks.Count == 0)
        {
            children.Add(new EmptyState(new Icon(LucideIcons.Sun, 32), T.TodayEmptyTitle, T.TodayEmptyText)
            {
                Action = Button(T.TodayPull, Variant.Outline, onPressed: () => Navigator.Go("/projetos")),
            });
        }
        else
        {
            if (done == tasks.Count)
                children.Add(new EmptyState(new Icon(LucideIcons.Sun, 32), T.TodayAllDoneTitle, T.TodayAllDoneText)
                {
                    Action = Button("Fechar o dia", onPressed: () => Navigator.Go("/fechar-dia")),
                    SecondaryAction = Button(T.TodayPull, Variant.Outline, onPressed: () => Navigator.Go("/projetos")),
                });
            children.Add(new TaskRows
            {
                Tasks = tasks, Today = true, OnChanged = archived => _ = Reload(),
                OnEdit = id => SetState(() => _editing = id),
                FocusTaskId = _view.FocusTaskId, FocusPaused = _view.FocusPaused,
            });
            if (done != tasks.Count)
                children.Add(Row(main: MainAlign.End, children: [
                    Button("Fechar o dia", Variant.Outline, onPressed: () => Navigator.Go("/fechar-dia")),
                ]));
        }
        return AppShell("/", Column(gap: Space.S4, children: [.. children]));
    }
}
