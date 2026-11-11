using eQuantic.UI.Components;
using eQuantic.UI.Lucide;
using eQuantic.UI.Primitives;
using PomodoroTasker.Core;
using PomodoroTasker.Web.Server;
using PomodoroTasker.Web.Theme;

namespace PomodoroTasker.Web.Components;

/// <summary>
/// The task editor, a sheet on the trailing edge: fields, sub-tasks, the per-day history, and
/// the recurrence rule, which is edited in place and saved apart from the rest of the form.
/// </summary>
public sealed class TaskEditor : StatefulComponent
{
    private EditorView? _view;
    private TaskEdit _form = new();
    private string _newSub = "";
    private bool _confirming;
    private bool _missingTitle;

    // The rule draft: 0 none, 1 daily, 2 weekly.
    private bool _editingRule;
    private int _kind;
    private int _days;
    private int _interval = 1;
    private string _anchor = "";
    private string _preview = "";

    public int TaskId { get; set; }
    public Action OnClose { get; set; } = () => { };

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not TaskEditor fresh) return;
        TaskId = fresh.TaskId;
        OnClose = fresh.OnClose;
    }

    [ServerAction]
    public Task<EditorView> Load(int id) => Task.FromResult(Api.Editor(id));

    [ServerAction]
    public Task<int> Save(int id, TaskEdit edit) { Api.SaveTask(id, edit); return Task.FromResult(0); }

    [ServerAction]
    public Task<int> Remove(int id) { Api.DeleteTask(id); return Task.FromResult(0); }

    [ServerAction]
    public Task<EditorView> AddSub(int id, string title) { Api.AddTask(title, parentId: id); return Load(id); }

    [ServerAction]
    public Task<EditorView> ToggleSub(int id, int subId, bool done) { Api.SetDone(subId, done); return Load(id); }

    [ServerAction]
    public Task<EditorView> RemoveSub(int id, int subId) { Api.DeleteTask(subId); return Load(id); }

    [ServerAction]
    public Task<EditorView> ApplyRule(int id, string freq, int interval, int days, string anchor)
    {
        Api.SetRule(id, freq, interval, days, anchor);
        return Load(id);
    }

    [ServerAction]
    public Task<string> Preview(string freq, int interval, int days, string anchor) =>
        Task.FromResult(Api.RulePreview(freq, interval, days, anchor));

    protected override async void OnMount()
    {
        var view = await Load(TaskId);
        if (view.Task == null) { OnClose(); return; }
        var t = view.Task;
        SetState(() =>
        {
            _view = view;
            _form = new TaskEdit
            {
                Title = t.Title, Notes = t.Notes, ListId = t.ListId ?? 0, Priority = t.Priority, Tags = t.Tags,
                Planned = t.PlannedDate ?? "", Due = t.DueDate ?? "", Pomodoro = t.PomodoroEnabled != 0,
            };
        });
    }

    /// <summary>Shows what a sub-task or rule change left on the server; the form keeps the user's draft.</summary>
    private async Task Show(Task<EditorView> change)
    {
        var view = await change;
        SetState(() => _view = view);
        Bus.Timer?.Invoke(); // closing a sub-task may have ended the focus on it
    }

    private async Task SaveAndClose()
    {
        if (_form.Title.Trim() == "") { SetState(() => _missingTitle = true); return; }
        await Save(TaskId, _form);
        OnClose();
    }

    private async Task DeleteAndClose()
    {
        await Remove(TaskId);
        OnClose();
    }

    private void AddOne()
    {
        if (_newSub.Trim() == "") return;
        var title = _newSub;
        SetState(() => _newSub = "");
        _ = Show(AddSub(TaskId, title));
    }

    private string Freq => _kind == 1 ? "daily" : _kind == 2 ? "weekly" : "";

    private void OpenRule()
    {
        var t = _view!.Task!;
        SetState(() =>
        {
            _editingRule = true;
            _kind = t.RecFreq == "daily" ? 1 : t.RecFreq == "weekly" ? 2 : 0;
            _days = t.RecDays;
            _interval = Math.Max(1, t.RecInterval);
            _anchor = t.RecAnchor ?? _view.Day;
        });
        _ = Redraw();
    }

    private void Draft(Action change)
    {
        SetState(change);
        _ = Redraw();
    }

    private async Task Redraw()
    {
        var preview = await Preview(Freq, _interval, _days, _anchor);
        SetState(() => _preview = preview);
    }

    private async Task SaveRule()
    {
        var view = await ApplyRule(TaskId, Freq, _interval, _days, _anchor);
        SetState(() => { _view = view; _editingRule = false; });
    }

    private static VisualNode Field(IAppTheme theme, string label, VisualNode control, string? hint = null) =>
        Column(gap: Space.S1, children: [
            Text(label, TypeRole.Label, theme.TextSecondary),
            control,
            .. hint == null ? [] : new VisualNode[] { Text(hint, TypeRole.Caption, theme.TextMuted) },
        ]);

    private VisualNode DateField(IAppTheme theme, string label, string value, Action<string> set) =>
        Row(gap: Space.S2, cross: CrossAlign.End, children: [
            Flexible(new DatePicker(Dates.Parse(value), date => SetState(() => set(Dates.Str(date))), label: label)),
            .. value == "" ? [] : new VisualNode[]
            {
                new IconButton(new Icon(LucideIcons.X, 20), $"{T.DateClear}: {label}", onPressed: () => SetState(() => set(""))),
            },
        ]);

    private VisualNode RuleForm(IAppTheme theme)
    {
        var incomplete = _kind == 2 && _days == 0;
        var children = new List<VisualNode>
        {
            Text(_form.Title, TypeRole.Title, theme.TextPrimary),
            new SegmentedControl([T.RecNone, T.RecDaily, T.RecWeekly], _kind, index => Draft(() => _kind = index)),
        };
        if (_kind == 2)
        {
            children.Add(Field(theme, T.RecWhichDays, Row(gap: Space.S1, wrap: true, children: [
                .. Enumerable.Range(0, 7).Select(i => (VisualNode)Chip(T.Weekdays[i], ChipKind.Filter, (_days & (1 << i)) != 0,
                    () => Draft(() => _days ^= 1 << i))),
            ]), incomplete ? T.RecPickDay : null));
            children.Add(Field(theme, T.RecEveryWeeks,
                new Stepper(_interval, value => Draft(() => _interval = value)) { Min = 1, Max = 52, Label = T.RecEveryWeeks },
                T.RecEveryWeeksHint));
        }
        if (_kind != 0)
        {
            children.Add(new DatePicker(Dates.Parse(_anchor), date => Draft(() => _anchor = Dates.Str(date)), label: T.RecStarts));
            children.Add(Field(theme, T.RecPreview, Text(_preview, TypeRole.BodyM, theme.TextPrimary), T.RecNoStack));
        }
        children.Add(Row(gap: Space.S2, main: MainAlign.End, children: [
            Button(T.RecBack, Variant.Outline, onPressed: () => SetState(() => _editingRule = false)),
            new Button(T.RecApply, onPressed: () => _ = SaveRule()) { Disabled = incomplete },
        ]));
        return Column(gap: Space.S5, children: [.. children]);
    }

    private VisualNode Form(IAppTheme theme)
    {
        var view = _view!;
        var task = view.Task!;
        var top = task.ParentId == null;
        var children = new List<VisualNode>
        {
            new TextInput(_form.Title, value => SetState(() => { _form.Title = value; _missingTitle = false; }), T.TaskTitle,
                error: _missingTitle ? T.TaskTitleRequired : null),
            Field(theme, T.TaskNotes, new TextEntry(_form.Notes, value => SetState(() => _form.Notes = value))
            {
                Label = T.TaskNotes, Lines = 3,
            }),
        };
        if (top)
        {
            var options = new List<string> { T.TaskNoList };
            options.AddRange(view.Lists.Select(l => l.Label));
            var selected = view.Lists.FindIndex(l => l.Id == _form.ListId) + 1;
            children.Add(Field(theme, T.TaskList,
                new Select(options, selected, index => SetState(() => _form.ListId = index == 0 ? 0 : view.Lists[index - 1].Id))));
            children.Add(Field(theme, T.TaskPriority,
                new SegmentedControl(T.TaskPriorities, _form.Priority, index => SetState(() => _form.Priority = index))));
            children.Add(DateField(theme, T.TaskPlannedDate, _form.Planned, value => _form.Planned = value));
            children.Add(DateField(theme, T.TaskDeadline, _form.Due, value => _form.Due = value));
            children.Add(new TextInput(_form.Tags, value => SetState(() => _form.Tags = value), T.TaskTags, helper: T.TaskTagsHint));
        }
        children.Add(Row(gap: Space.S3, cross: CrossAlign.Center, children: [
            Flexible(Column(gap: 0, children: [
                Text(T.TaskPomodoro, TypeRole.BodyM, theme.TextPrimary),
                Text(T.TaskPomodoroHint, TypeRole.Caption, theme.TextMuted),
            ])),
            new Switch(_form.Pomodoro, () => SetState(() => _form.Pomodoro = !_form.Pomodoro)) { Label = T.TaskPomodoro },
        ]));
        if (top)
        {
            children.Add(Row(gap: Space.S3, cross: CrossAlign.Center, children: [
                Flexible(Column(gap: 0, children: [
                    Text(T.RecTitle, TypeRole.BodyM, theme.TextPrimary),
                    Text(task.RuleText ?? T.RecNone, TypeRole.Caption, theme.TextMuted),
                ])),
                Button(task.RuleText != null ? T.RecChange : T.RecDefine, Variant.Outline, onPressed: OpenRule),
            ]));

            var subsDone = task.Subs.Count(s => s.DoneAt != null);
            children.Add(Text(task.Subs.Count > 0 ? $"{T.TaskSubtasks} · {subsDone} de {task.Subs.Count}" : T.TaskSubtasks,
                TypeRole.Title, theme.TextPrimary, headingLevel: 3));
            foreach (var sub in task.Subs)
            {
                var done = sub.DoneAt != null;
                children.Add(Row(gap: Space.S2, cross: CrossAlign.Center, children: [
                    Flexible(new Checkbox(done, () => _ = Show(ToggleSub(TaskId, sub.Id, !done)), sub.Title)),
                    new IconButton(new Icon(LucideIcons.X, 20), $"{T.Delete}: {sub.Title}",
                        onPressed: () => _ = Show(RemoveSub(TaskId, sub.Id))),
                ]));
            }
            children.Add(Row(gap: Space.S2, cross: CrossAlign.End, children: [
                Flexible(new TextEntry(_newSub, value => SetState(() => _newSub = value))
                {
                    Placeholder = T.TaskNewSub, Label = T.TaskNewSub, OnSubmit = AddOne,
                }),
                Button(T.Add, Variant.Outline, onPressed: AddOne),
            ]));
        }
        if (view.History.Count > 0)
        {
            children.Add(Text(T.HistoryTitle, TypeRole.Title, theme.TextPrimary, headingLevel: 3));
            foreach (var h in view.History)
                children.Add(Row(gap: Space.S3, cross: CrossAlign.Center, children: [
                    Flexible(Column(gap: 0, children: [
                        Text(h.Day, TypeRole.BodyM, theme.TextPrimary),
                        Text(h.Text, TypeRole.Caption, theme.TextMuted),
                    ])),
                    Row(gap: 2, children: [
                        .. Enumerable.Range(0, h.Toms).Select(_ => (VisualNode)new Drawing(Vectors.Tomato, width: 16)),
                        .. Enumerable.Range(0, h.TomsInterrupted).Select(_ => (VisualNode)new Drawing(Vectors.TomatoBroken, width: 16)),
                    ]),
                ]));
        }
        children.Add(Row(gap: Space.S2, children: [
            Button(T.Delete, Variant.Destructive, onPressed: () =>
            {
                // Only a task that takes sub-tasks with it asks first.
                if (task.Subs.Count > 0) SetState(() => _confirming = true);
                else _ = DeleteAndClose();
            }),
            Spacer(),
            Button(T.Cancel, Variant.Outline, onPressed: OnClose),
            Button(T.Save, onPressed: () => _ = SaveAndClose()),
        ]));
        return Column(gap: Space.S4, children: [.. children]);
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        if (_view?.Task == null) return Box();

        var sheet = Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill, Background = theme.Background },
            ScrollView(Box(new BoxStyle { Width = SizeValue.Fill, Padding = EdgeInsets.All(Space.S6) },
                Column(gap: Space.S5, children: [
                    Row(cross: CrossAlign.Center, children: [
                        Flexible(Text(_editingRule ? T.RecTitle : T.TaskEdit, TypeRole.Heading, theme.TextPrimary, headingLevel: 2)),
                        new IconButton(new Icon(LucideIcons.X, 24), T.Close, onPressed: OnClose),
                    ]),
                    _editingRule ? RuleForm(theme) : Form(theme),
                ]))));

        var drawer = new Drawer(sheet, true, OnClose) { Edge = DrawerEdge.End, Width = 480 };
        if (!_confirming) return drawer;
        var task = _view.Task;
        return Column(gap: 0, children: [
            drawer,
            new Dialog(T.TaskDeleteTitle, T.TaskDeleteText(task.Title, task.Subs.Count),
                [
                    new DialogAction(T.Cancel, () => SetState(() => _confirming = false), Variant.Outline),
                    new DialogAction(T.Delete, () => _ = DeleteAndClose(), Variant.Destructive),
                ],
                dismissible: true, onDismiss: () => SetState(() => _confirming = false)),
        ]);
    }
}
