using eQuantic.UI.Components;
using eQuantic.UI.Lucide;
using eQuantic.UI.Primitives;
using PomodoroTasker.Core;
using PomodoroTasker.Web.Components;
using PomodoroTasker.Web.Server;

namespace PomodoroTasker.Web.Pages;

/// <summary>Listas: the loose lists, with the way to the archived ones.</summary>
[Page("/listas", Title = "Listas · pomodoro-tasker")]
public sealed class ListsPage : StatefulComponent
{
    private ListsView? _view;
    private string _name = "";
    private LooseListView? _justArchived;

    [ServerAction]
    public Task<ListsView> Load() => Task.FromResult(Api.Lists());

    [ServerAction]
    public Task<int> Create(string name) => Task.FromResult(Api.CreateList(name));

    [ServerAction]
    public Task<LooseListView> Find(int id) => Task.FromResult(Api.List(id));

    [ServerAction]
    public Task<ListsView> Unarchive(int id) { Api.ArchiveList(id, false); return Load(); }

    protected override async void OnMount()
    {
        Bus.Data = () => _ = Reload();
        await Reload();
        // Coming from a list that archived itself: say so, and offer the way back.
        if (int.TryParse(RouteValues.Current.Query("arquivada"), out var id))
        {
            var archived = await Find(id);
            SetState(() => _justArchived = archived.List?.ArchivedAt != null ? archived : null);
        }
    }

    protected override void OnUnmount() => Bus.Data = null;

    private async Task Reload()
    {
        var view = await Load();
        SetState(() => _view = view);
    }

    private async Task Add()
    {
        var id = await Create(_name);
        if (id != 0) Navigator.Go($"/listas/{id}");
    }

    private async Task Undo()
    {
        var id = _justArchived!.List!.Id;
        var view = await Unarchive(id);
        SetState(() => { _view = view; _justArchived = null; });
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var children = new List<VisualNode>
        {
            Parts.Heading(theme, T.NavLists, _view == null ? "" : T.ListsActive(_view.Lists.Count)),
            Parts.QuickAdd(_name, T.ListsNewName, T.Create, value => SetState(() => _name = value), () => _ = Add()),
        };
        if (_view != null)
        {
            if (_view.Lists.Count == 0 && _view.Archived == 0)
                children.Add(new EmptyState(new Icon(LucideIcons.List, 32), T.ListsEmptyTitle, T.ListsEmptyText));
            foreach (var l in _view.Lists)
                children.Add(new Link($"/listas/{l.Id}", Parts.Line(theme, l.Name,
                    l.InToday > 0 ? $"{T.ListsItems(l.Done, l.Total)} · {T.InToday(l.InToday)}" : T.ListsItems(l.Done, l.Total),
                    Box(new BoxStyle { Width = 120 }, new ProgressBar(l.Total == 0 ? 0 : (float)l.Done / l.Total)))));
            if (_view.Archived > 0)
            {
                children.Add(Parts.Section(theme, T.ListsArchived));
                children.Add(Row(gap: Space.S3, cross: CrossAlign.Center, children: [
                    Flexible(Parts.Note(theme, T.ListsArchivedHint)),
                    Button(T.ListsSeeArchived(_view.Archived), Variant.Outline, onPressed: () => Navigator.Go("/listas/arquivadas")),
                ]));
            }
        }
        if (_justArchived?.List != null)
            children.Add(new Toast(T.ListsArchivedToast(_justArchived.List.Name), Variant.Success, T.Undo, () => _ = Undo()));
        return AppShell("/listas", Column(gap: Space.S4, children: [.. children]));
    }
}

/// <summary>One loose list: its items, and what can be done with the list itself.</summary>
[Page("/listas/{id:int}", Title = "Lista · pomodoro-tasker")]
public sealed class ListPage : StatefulComponent
{
    private int _id;
    private LooseListView? _view;
    private string _title = "";
    private int _editing;
    private bool _renaming;
    private string _name = "";
    private bool _deleting;

    [ServerAction]
    public Task<LooseListView> Load(int id) => Task.FromResult(Api.List(id));

    [ServerAction]
    public Task<LooseListView> Add(int id, string title) { Api.AddTask(title, listId: id); return Load(id); }

    [ServerAction]
    public Task<LooseListView> Rename(int id, string name) { Api.RenameList(id, name); return Load(id); }

    [ServerAction]
    public Task<LooseListView> Archive(int id, bool archived) { Api.ArchiveList(id, archived); return Load(id); }

    [ServerAction]
    public Task<int> Remove(int id) { Api.DeleteList(id); return Task.FromResult(0); }

    protected override void OnMount()
    {
        _id = int.Parse(RouteValues.Current.Param("id") ?? "0");
        Bus.Data = () => _ = Show(Load(_id));
        _ = Show(Load(_id));
    }

    protected override void OnUnmount() => Bus.Data = null;

    private async Task Show(Task<LooseListView> change)
    {
        var view = await change;
        if (view.List == null) Navigator.Go("/listas");
        else SetState(() => _view = view);
    }

    private void AddOne()
    {
        if (_title.Trim() == "") return;
        var title = _title;
        SetState(() => _title = "");
        _ = Show(Add(_id, title));
    }

    private async Task DeleteList()
    {
        await Remove(_id);
        Navigator.Go(_view?.List?.ArchivedAt != null ? "/listas/arquivadas" : "/listas");
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        if (_view?.List == null) return AppShell("/listas", Box());
        var list = _view.List;
        var archived = list.ArchivedAt != null;

        var menu = new List<(string Label, Action Run)>();
        if (!archived)
        {
            menu.Add((T.Rename, () => SetState(() => { _name = list.Name; _renaming = true; })));
            menu.Add((T.ListsArchiveNow, () => _ = Show(Archive(_id, true))));
        }
        else menu.Add((T.Restore, () => _ = Show(Archive(_id, false))));
        menu.Add((T.Delete, () => SetState(() => _deleting = true)));

        var children = new List<VisualNode>
        {
            Row(gap: Space.S3, cross: CrossAlign.Center, children: [
                new IconButton(new Icon(LucideIcons.ChevronLeft, 24), T.NavLists, onPressed: () => Navigator.Go("/listas")),
                Flexible(Parts.Heading(theme, list.Name, archived ? $"{_view.ArchivedLabel} · {T.ListsReadOnly}" : T.ListsItems(list.Done, list.Total))),
                new Menu(new IconButton(new Icon(LucideIcons.Ellipsis, 24), T.More),
                    menu.Select(m => new MenuItem(m.Label) { Destructive = m.Label == T.Delete }).ToList(),
                    index => menu[index].Run()),
            ]),
        };
        if (!archived)
            children.Add(Parts.QuickAdd(_title, T.ListsNewItem, T.Add, value => SetState(() => _title = value), AddOne));
        children.Add(new TaskRows
        {
            Tasks = _view.Tasks, ReadOnly = archived, OnEdit = id => SetState(() => _editing = id),
            OnChanged = archivedId =>
            {
                // Completing the last item archived the list: back to the lists, with the way to undo.
                if (archivedId == _id) Navigator.Go($"/listas?arquivada={_id}");
                else _ = Show(Load(_id));
            },
        });
        if (_editing != 0)
            children.Add(new TaskEditor
            {
                TaskId = _editing,
                OnClose = () => { SetState(() => _editing = 0); _ = Show(Load(_id)); },
            });
        if (_renaming)
            children.Add(Parts.NameModal(theme, T.Rename, _name, value => SetState(() => _name = value),
                () => { SetState(() => _renaming = false); _ = Show(Rename(_id, _name)); },
                () => SetState(() => _renaming = false)));
        if (_deleting)
            children.Add(new Dialog(T.ListsDeleteTitle, T.ListsDeleteText(list.Name, list.Total),
                [
                    new DialogAction(T.Cancel, () => SetState(() => _deleting = false), Variant.Outline),
                    new DialogAction(T.Delete, () => _ = DeleteList(), Variant.Destructive),
                ],
                dismissible: true, onDismiss: () => SetState(() => _deleting = false)));
        return AppShell("/listas", Column(gap: Space.S4, children: [.. children]));
    }
}

/// <summary>The archived loose lists: restore or delete.</summary>
[Page("/listas/arquivadas", Title = "Listas arquivadas · pomodoro-tasker")]
public sealed class ArchivedListsPage : StatefulComponent
{
    private List<TaskList>? _lists;
    private TaskList? _deleting;

    [ServerAction]
    public Task<List<TaskList>> Load() => Task.FromResult(Api.ArchivedLists());

    [ServerAction]
    public Task<List<TaskList>> Restore(int id) { Api.ArchiveList(id, false); return Load(); }

    [ServerAction]
    public Task<List<TaskList>> Remove(int id) { Api.DeleteList(id); return Load(); }

    protected override void OnMount() => _ = Show(Load());

    private async Task Show(Task<List<TaskList>> change)
    {
        var lists = await change;
        SetState(() => { _lists = lists; _deleting = null; });
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var children = new List<VisualNode>
        {
            Row(gap: Space.S3, cross: CrossAlign.Center, children: [
                new IconButton(new Icon(LucideIcons.ChevronLeft, 24), T.NavLists, onPressed: () => Navigator.Go("/listas")),
                Flexible(Parts.Heading(theme, T.ListsArchived, _lists == null ? "" : T.ListsCount(_lists.Count))),
            ]),
        };
        if (_lists is { Count: 0 }) children.Add(Parts.Note(theme, T.ListsNoArchived));
        foreach (var l in _lists ?? [])
            children.Add(Parts.Line(theme, l.Name, T.ListsItems(l.Done, l.Total), Row(gap: Space.S2, children: [
                Button("Abrir", Variant.Ghost, onPressed: () => Navigator.Go($"/listas/{l.Id}")),
                Button(T.Restore, Variant.Outline, onPressed: () => _ = Show(Restore(l.Id))),
                Button(T.Delete, Variant.Destructive, onPressed: () => SetState(() => _deleting = l)),
            ]), quiet: true));
        if (_deleting != null)
        {
            var list = _deleting;
            children.Add(new Dialog(T.ListsDeleteTitle, T.ListsDeleteText(list.Name, list.Total),
                [
                    new DialogAction(T.Cancel, () => SetState(() => _deleting = null), Variant.Outline),
                    new DialogAction(T.Delete, () => _ = Show(Remove(list.Id)), Variant.Destructive),
                ],
                dismissible: true, onDismiss: () => SetState(() => _deleting = null)));
        }
        return AppShell("/listas", Column(gap: Space.S4, children: [.. children]));
    }
}
