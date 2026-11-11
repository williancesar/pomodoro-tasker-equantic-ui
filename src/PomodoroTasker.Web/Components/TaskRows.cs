using eQuantic.UI.Components;
using eQuantic.UI.Lucide;
using eQuantic.UI.Primitives;
using PomodoroTasker.Core;
using PomodoroTasker.Web.Server;
using PomodoroTasker.Web.Theme;

namespace PomodoroTasker.Web.Components;

/// <summary>
/// Task rows with their sub-tasks nested under them. In Today a row starts a pomodoro; in a list
/// it pulls the task into Today. Order changes through the row menu.
/// </summary>
public sealed class TaskRows : StatefulComponent
{
    private TaskItem? _deleting;

    // No constructor arguments: the server's container builds this to run its Server Actions.
    public List<TaskItem> Tasks { get; set; } = [];
    /// <summary>Rows of Today; otherwise rows of a list.</summary>
    public bool Today { get; set; }
    /// <summary>Called after a change, with the id of the loose list it archived, or 0.</summary>
    public Action<int> OnChanged { get; set; } = _ => { };
    public Action<int> OnEdit { get; set; } = _ => { };
    public bool ReadOnly { get; set; }
    /// <summary>The task of the running focus, 0 when none.</summary>
    public int FocusTaskId { get; set; }
    public bool FocusPaused { get; set; }

    // The mounted instance is kept across the page's rebuilds and learns its new values here.
    public override void AdoptConfig(UiComponent next)
    {
        if (next is not TaskRows fresh) return;
        Tasks = fresh.Tasks;
        Today = fresh.Today;
        OnChanged = fresh.OnChanged;
        OnEdit = fresh.OnEdit;
        ReadOnly = fresh.ReadOnly;
        FocusTaskId = fresh.FocusTaskId;
        FocusPaused = fresh.FocusPaused;
    }

    [ServerAction]
    public Task<int> Done(int id, bool done) => Task.FromResult(Api.SetDone(id, done));

    [ServerAction]
    public Task<int> Pull(int id, bool today) { Api.SetToday(id, today); return Task.FromResult(0); }

    [ServerAction]
    public Task<int> Remove(int id) { Api.DeleteTask(id); return Task.FromResult(0); }

    [ServerAction]
    public Task<int> Order(int[] ids, bool today) { Api.Reorder(ids, today); return Task.FromResult(0); }

    [ServerAction]
    public Task<int> Focus(string type, int id) { Api.Send(type, id); return Task.FromResult(0); }

    /// <summary>Runs a change, then lets the page and the timer block reload.</summary>
    private async Task After(Task<int> change)
    {
        var archived = await change;
        OnChanged(archived);
        Bus.Timer?.Invoke();
    }

    private void Move(int id, int by)
    {
        var ids = Tasks.Select(t => t.Id).ToList();
        var from = ids.IndexOf(id);
        var to = from + by;
        if (to < 0 || to >= ids.Count) return;
        (ids[from], ids[to]) = (ids[to], ids[from]);
        _ = After(Order(ids.ToArray(), Today));
    }

    private void Delete(TaskItem task)
    {
        // Only a task that takes sub-tasks with it asks first.
        if (task.Subs.Count > 0 && _deleting?.Id != task.Id)
        {
            SetState(() => _deleting = task);
            return;
        }
        SetState(() => _deleting = null);
        _ = After(Remove(task.Id));
    }

    private VisualNode TomatoRun(TaskItem task)
    {
        if (task.Toms + task.TomsInterrupted == 0) return Box();
        return Row(gap: 2, children: [
            .. Enumerable.Range(0, task.Toms).Select(i => (VisualNode)new Drawing(Vectors.Tomato, width: 16,
                label: i == 0 ? T.TimerTomatoes(task.Toms, task.TomsInterrupted) : null)),
            .. Enumerable.Range(0, task.TomsInterrupted).Select(_ => (VisualNode)new Drawing(Vectors.TomatoBroken, width: 16)),
        ]);
    }

    private VisualNode Meta(TaskItem task, IAppTheme theme)
    {
        var parts = new List<VisualNode>();
        void Add(string text, ColorToken? color = null) =>
            parts.Add(Text(text, TypeRole.Caption, color ?? theme.TextMuted, maxLines: 1));
        if (task.Priority == 2) Add(T.TaskHigh, TaskerTheme.Accent);
        if (task.Priority == 0) Add(T.TaskLow);
        if (task.Where != null) Add(task.Where);
        if (task.RuleText != null) Add($"Rotina · {task.RuleText.ToLower()}");
        if (!Today && task.InToday && task.DoneAt == null) Add(T.TaskInTodayTag, TaskerTheme.Leaf);
        if (task.Subs.Count > 0) Add(T.TaskSubs(task.Subs.Count(s => s.DoneAt != null), task.Subs.Count));
        if (task.DeadlineText != null) Add(task.DeadlineText, task.DeadlineLate ? TaskerTheme.Accent : null);
        if (task.PlannedText != null) Add(task.PlannedText);
        if (task.PostponeCount > 0) Add(T.TaskPostponed(task.PostponeCount), theme.TextSecondary);
        return parts.Count == 0 ? Box() : Row(gap: Space.S3, wrap: true, runGap: 0, children: [.. parts]);
    }

    private VisualNode RowOf(TaskItem task, bool sub, IAppTheme theme)
    {
        var done = task.DoneAt != null;
        var title = Text(task.Title, sub ? TypeRole.BodyM : TypeRole.BodyL, done ? theme.TextMuted : theme.TextPrimary);
        var cells = new List<VisualNode>
        {
            // The framework's Checkbox draws its label; here the name is the title beside it.
            new Pressable(Box(new BoxStyle
            {
                Width = 20, Height = 20, Background = done ? TaskerTheme.Accent : null,
                BorderWidth = done ? 0 : 2, BorderColor = theme.BorderStrong,
            }, done ? new Icon(Icons.Check, 16, TaskerTheme.OnAccent).Centered() : null),
                ReadOnly ? null : () => _ = After(Done(task.Id, !done)))
            {
                Disabled = ReadOnly, Role = PressableRole.Checkbox, Selected = done, Label = task.Title,
            },
            Flexible(Column(gap: 2, cross: CrossAlign.Start, children: [
                ReadOnly ? title : new Pressable(title, () => OnEdit(task.Id)) { Label = $"{T.TaskEdit}: {task.Title}" },
                sub ? Box() : Meta(task, theme),
            ])),
            TomatoRun(task),
        };
        if (!ReadOnly)
        {
            var items = new List<(string Label, Action Run)> { (T.TaskEdit, () => OnEdit(task.Id)) };
            if (Today && task.InToday) items.Add((T.TaskToBacklog, () => _ = After(Pull(task.Id, false))));
            if (!sub)
            {
                items.Add((T.MoveUp, () => Move(task.Id, -1)));
                items.Add((T.MoveDown, () => Move(task.Id, 1)));
            }
            items.Add((T.Delete, () => Delete(task)));
            cells.Add(new Menu(
                new IconButton(new Icon(LucideIcons.Ellipsis, 20), T.More),
                items.Select(i => new MenuItem(i.Label) { Destructive = i.Label == T.Delete }).ToList(),
                index => items[index].Run()));

            if (!sub && !done && !Today)
                cells.Add(new IconButton(new Icon(LucideIcons.Sun, 20), task.InToday ? T.TaskInToday : T.TaskToToday,
                    task.InToday ? IconButtonKind.Filled : IconButtonKind.Standard,
                    onPressed: () => _ = After(Pull(task.Id, !task.InToday))) { Selected = task.InToday });
            else if (Today && !done && FocusTaskId == task.Id)
                cells.Add(new IconButton(new Icon(FocusPaused ? LucideIcons.Play : LucideIcons.Pause, 20),
                    FocusPaused ? T.TimerResume : T.TimerPause, IconButtonKind.Filled,
                    onPressed: () => _ = After(Focus("primary", 0))));
            else if (Today && !done)
                cells.Add(new IconButton(new Icon(LucideIcons.Play, 20),
                    FocusTaskId != 0 ? T.TimerBusy : T.TimerStartOn(task.Title),
                    onPressed: () => _ = After(Focus("start", task.Id))) { Disabled = FocusTaskId != 0 });
        }

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = new EdgeInsets(sub ? Space.S8 : 0, Space.S2, 0, Space.S2),
            BorderWidth = 1, BorderColor = theme.Border, BorderSides = BorderSides.Bottom,
        }, Row(gap: Space.S3, cross: CrossAlign.Center, children: [.. cells]));
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var rows = new List<VisualNode>();
        foreach (var task in Tasks)
        {
            rows.Add(RowOf(task, false, theme));
            foreach (var sub in task.Subs) rows.Add(RowOf(sub, true, theme));
        }
        if (_deleting != null)
        {
            var task = _deleting;
            rows.Add(new Dialog(T.TaskDeleteTitle, T.TaskDeleteText(task.Title, task.Subs.Count),
                [
                    new DialogAction(T.Cancel, () => SetState(() => _deleting = null), Variant.Outline),
                    new DialogAction(T.Delete, () => Delete(task), Variant.Destructive),
                ],
                dismissible: true, onDismiss: () => SetState(() => _deleting = null)));
        }
        return Column(gap: 0, children: [.. rows]);
    }
}
