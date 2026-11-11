using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using PomodoroTasker.Web.Components;
using PomodoroTasker.Web.Server;
using PomodoroTasker.Web.Theme;

namespace PomodoroTasker.Web.Pages;

/// <summary>Fechar o dia: the review before closing, then the summary of the closed day.</summary>
[Page("/fechar-dia", Title = "Fechar o dia · pomodoro-tasker")]
public sealed class CloseDayPage : StatefulComponent
{
    private CloseView? _view;
    private readonly HashSet<int> _toBacklog = [];

    [ServerAction]
    public Task<CloseView> Load() => Task.FromResult(Api.CloseReview());

    [ServerAction]
    public Task<CloseView> Close(int[] toBacklog) => Task.FromResult(Api.CloseDay(toBacklog));

    protected override void OnMount() => _ = Show(Load());

    private async Task Show(Task<CloseView> change)
    {
        var view = await change;
        SetState(() => _view = view);
    }

    private VisualNode Closed(IAppTheme theme, CloseView v)
    {
        var total = v.DoneCount + v.Postponed;
        var pct = total == 0 ? 0 : (int)Math.Round(v.DoneCount * 100.0 / total);
        var children = new List<VisualNode>
        {
            Parts.Heading(theme, T.CloseClosedTitle, v.DayLabel),
            Parts.Kpis(theme, [
                (v.DoneCount.ToString(), T.CloseDone), (v.Postponed.ToString(), T.ClosePostponed),
                (v.Tomatoes.ToString(), T.CloseTomatoes(v.Tomatoes, v.Focus)),
            ]),
        };
        if (total > 0)
        {
            children.Add(new ProgressBar(pct / 100f));
            children.Add(Parts.Note(theme, $"{T.CloseDonePct(pct)} · {T.ClosePostponedPct(100 - pct)}"));
        }
        else children.Add(Parts.Note(theme, T.CloseNothing));
        if (v.SentBack > 0) children.Add(Parts.Note(theme, T.CloseToBacklog(v.SentBack)));
        if (v.Waiting.Count > 0)
        {
            children.Add(Parts.Section(theme, T.CloseWaiting, v.TomorrowLabel));
            children.Add(new TaskRows { Tasks = v.Waiting, Today = true, ReadOnly = true });
        }
        children.Add(Row(children: [Button(T.CloseBackToToday, onPressed: () => Navigator.Go("/"))]));
        return Column(gap: Space.S4, children: [.. children]);
    }

    private VisualNode Review(IAppTheme theme, CloseView v)
    {
        var children = new List<VisualNode>
        {
            Parts.Heading(theme, T.CloseTitle, v.DayLabel),
            Parts.Kpis(theme, [
                (v.Done.Count.ToString(), T.CloseDone), (v.Open.Count.ToString(), T.CloseOpen),
                (v.Tomatoes.ToString(), T.CloseTomatoes(v.Tomatoes, v.Focus)),
            ]),
        };
        if (v.Open.Count + v.Done.Count == 0) children.Add(Parts.Note(theme, T.CloseNothing));
        if (v.Open.Count > 0)
        {
            children.Add(Parts.Section(theme, T.CloseOpenTitle, T.CloseOpenHint));
            foreach (var task in v.Open)
            {
                var meta = task.Where ?? "";
                if (task.PostponeCount > 0) meta = meta == "" ? T.TaskPostponed(task.PostponeCount) : $"{meta} · {T.TaskPostponed(task.PostponeCount)}";
                children.Add(Parts.Line(theme, task.Title, meta, Box(new BoxStyle { Width = 200 },
                    new SegmentedControl([T.ClosePostpone, T.CloseBacklog], _toBacklog.Contains(task.Id) ? 1 : 0,
                        index => SetState(() =>
                        {
                            if (index == 1) _toBacklog.Add(task.Id);
                            else _toBacklog.Remove(task.Id);
                        })))));
            }
        }
        if (v.Done.Count > 0)
        {
            children.Add(Parts.Section(theme, T.CloseDoneTitle, T.CloseCount(v.Done.Count)));
            children.Add(new TaskRows { Tasks = v.Done, Today = true, ReadOnly = true });
        }
        children.Add(Parts.Note(theme, T.CloseTomorrow(v.Open.Count(t => !_toBacklog.Contains(t.Id)))));
        children.Add(Row(gap: Space.S2, main: MainAlign.End, children: [
            Button(T.CloseBack, Variant.Outline, onPressed: () => Navigator.Go("/")),
            Button(T.CloseTitle, onPressed: () => _ = Show(Close(_toBacklog.ToArray()))),
        ]));
        return Column(gap: Space.S4, children: [.. children]);
    }

    public override VisualNode Build(ComponentContext context) =>
        AppShell("/", _view == null ? Box() : _view.Closed ? Closed(context.Theme, _view) : Review(context.Theme, _view));
}

/// <summary>Dias fechados: what was recorded for the days that ended while nobody was here.</summary>
[Page("/dias-fechados", Title = "Dias fechados · pomodoro-tasker")]
public sealed class MissedDaysPage : StatefulComponent
{
    private MissedView? _view;

    [ServerAction]
    public Task<MissedView> Load() => Task.FromResult(Api.Missed());

    protected override async void OnMount()
    {
        var view = await Load();
        SetState(() => _view = view);
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        if (_view == null) return AppShell("/", Box());
        var children = new List<VisualNode>
        {
            Parts.Heading(theme, T.CloseMissedTitle(_view.Days.Count), T.CloseMissedWhen),
            Parts.Note(theme, T.CloseMissedText),
        };
        foreach (var d in _view.Days) children.Add(Parts.Line(theme, d.Title, d.Text));
        if (_view.Carried.Count > 0)
        {
            children.Add(Parts.Section(theme, T.CloseChanged));
            children.Add(new TaskRows { Tasks = _view.Carried, Today = true, ReadOnly = true });
        }
        children.Add(Row(children: [Button(T.CloseGoToday, onPressed: () => Navigator.Go("/"))]));
        return AppShell("/", Column(gap: Space.S4, children: [.. children]));
    }
}
