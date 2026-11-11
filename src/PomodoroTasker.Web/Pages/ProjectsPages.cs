using eQuantic.UI.Components;
using eQuantic.UI.Lucide;
using eQuantic.UI.Primitives;
using PomodoroTasker.Core;
using PomodoroTasker.Web.Components;
using PomodoroTasker.Web.Server;
using PomodoroTasker.Web.Theme;

namespace PomodoroTasker.Web.Pages;

/// <summary>Projetos: the active ones with their progress, then the completed.</summary>
[Page("/projetos", Title = "Projetos · pomodoro-tasker")]
public sealed class ProjectsPage : StatefulComponent
{
    private ProjectsView? _view;
    private string _name = "";

    [ServerAction]
    public Task<ProjectsView> Load() => Task.FromResult(Api.Projects());

    [ServerAction]
    public Task<int> Create(string name) => Task.FromResult(Api.CreateProject(name));

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

    private async Task Add()
    {
        var id = await Create(_name);
        if (id != 0) Navigator.Go($"/projetos/{id}");
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var all = _view?.Projects ?? [];
        var active = all.Where(p => p.CompletedAt == null).ToList();
        var children = new List<VisualNode>
        {
            Parts.Heading(theme, T.NavProjects, _view == null ? "" : T.ProjectsActive(active.Count)),
            Parts.QuickAdd(_name, T.ProjectsNewName, T.Create, value => SetState(() => _name = value), () => _ = Add()),
        };
        if (_view != null && all.Count == 0)
            children.Add(new EmptyState(new Icon(LucideIcons.Folder, 32), T.ProjectsNoneTitle, T.ProjectsNoneText));
        foreach (var p in active)
        {
            var total = p.Open + p.Done;
            var meta = $"{T.ListsCount(p.Lists)} · {T.ProjectsOpen(p.Open)}";
            if (p.InToday > 0) meta += $" · {T.InToday(p.InToday)}";
            children.Add(new Link($"/projetos/{p.Id}", Parts.Line(theme, p.Name, meta,
                Box(new BoxStyle { Width = 120 }, new ProgressBar(total == 0 ? 0 : (float)p.Done / total)),
                note: p.Overdue > 0 ? T.ProjectsOverdue(p.Overdue) : "")));
        }
        if (all.Count > active.Count)
        {
            children.Add(Parts.Section(theme, T.ProjectsCompleted, T.ProjectsCompletedCount(all.Count - active.Count)));
            for (var i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p.CompletedAt == null) continue;
                children.Add(new Link($"/projetos/{p.Id}", Parts.Line(theme, p.Name,
                    $"{_view!.CompletedLabels[i]} · {T.ProjectsDone(p.Done)}", quiet: true)));
            }
        }
        return AppShell("/projetos", Column(gap: Space.S4, children: [.. children]));
    }
}

/// <summary>One project: its lists, each with its tasks and its own quick add.</summary>
[Page("/projetos/{id:int}", Title = "Projeto · pomodoro-tasker")]
public sealed class ProjectPage : StatefulComponent
{
    private int _id;
    private ProjectView? _view;
    private readonly Dictionary<int, string> _titles = [];
    private string _newList = "";
    private int _editing;
    private bool _deleting;
    private TaskList? _deletingList;
    // What the name modal is renaming: 0 nothing, -1 the project, otherwise a list id.
    private int _renaming;
    private string _name = "";

    [ServerAction]
    public Task<ProjectView> Load(int id) => Task.FromResult(Api.Project(id));

    [ServerAction]
    public Task<ProjectView> AddTask(int id, int listId, string title) { Api.AddTask(title, listId: listId); return Load(id); }

    [ServerAction]
    public Task<ProjectView> AddList(int id, string name) { Api.CreateList(name, id); return Load(id); }

    [ServerAction]
    public Task<ProjectView> Rename(int id, int listId, string name)
    {
        if (listId == -1) Api.RenameProject(id, name);
        else Api.RenameList(listId, name);
        return Load(id);
    }

    [ServerAction]
    public Task<ProjectView> Complete(int id, bool completed) { Api.CompleteProject(id, completed); return Load(id); }

    [ServerAction]
    public Task<ProjectView> RemoveList(int id, int listId) { Api.DeleteList(listId); return Load(id); }

    [ServerAction]
    public Task<int> Remove(int id) { Api.DeleteProject(id); return Task.FromResult(0); }

    protected override void OnMount()
    {
        _id = int.Parse(RouteValues.Current.Param("id") ?? "0");
        Bus.Data = () => _ = Show(Load(_id));
        _ = Show(Load(_id));
    }

    protected override void OnUnmount() => Bus.Data = null;

    private async Task Show(Task<ProjectView> change)
    {
        var view = await change;
        if (view.Project == null) Navigator.Go("/projetos");
        else SetState(() => { _view = view; _deletingList = null; });
    }

    private void AddTo(int listId)
    {
        var title = _titles.GetValueOrDefault(listId, "");
        if (title.Trim() == "") return;
        SetState(() => _titles[listId] = "");
        _ = Show(AddTask(_id, listId, title));
    }

    private void AddOneList()
    {
        if (_newList.Trim() == "") return;
        var name = _newList;
        SetState(() => _newList = "");
        _ = Show(AddList(_id, name));
    }

    private async Task DeleteProject()
    {
        await Remove(_id);
        Navigator.Go("/projetos");
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        if (_view?.Project == null) return AppShell("/projetos", Box());
        var project = _view.Project;
        var completed = project.CompletedAt != null;

        var menu = new List<(string Label, Action Run)>
        {
            (T.Rename, () => SetState(() => { _name = project.Name; _renaming = -1; })),
            (completed ? T.ProjectsReopen : T.ProjectsMarkCompleted, () => _ = Show(Complete(_id, !completed))),
            (T.Delete, () => SetState(() => _deleting = true)),
        };
        var children = new List<VisualNode>
        {
            Row(gap: Space.S3, cross: CrossAlign.Center, children: [
                new IconButton(new Icon(LucideIcons.ChevronLeft, 24), T.NavProjects, onPressed: () => Navigator.Go("/projetos")),
                Flexible(Parts.Heading(theme, project.Name, $"{T.ProjectsOpen(project.Open)} · {T.ProjectsDone(project.Done)}")),
                new Menu(new IconButton(new Icon(LucideIcons.Ellipsis, 24), T.More),
                    menu.Select(m => new MenuItem(m.Label) { Destructive = m.Label == T.Delete }).ToList(),
                    index => menu[index].Run()),
            ]),
        };
        if (project.Overdue > 0)
            children.Add(Text(T.ProjectsOverdue(project.Overdue), TypeRole.BodyM, TaskerTheme.Accent));
        if (_view.Lists.Count == 0)
            children.Add(new EmptyState(new Icon(LucideIcons.List, 32), T.ProjectsEmptyTitle, T.ProjectsEmptyText));

        foreach (var entry in _view.Lists)
        {
            var list = entry.List;
            var listMenu = new List<(string Label, Action Run)>
            {
                (T.ProjectsRenameList, () => SetState(() => { _name = list.Name; _renaming = list.Id; })),
                (T.ProjectsDeleteList, () => SetState(() => _deletingList = list)),
            };
            children.Add(Row(gap: Space.S3, cross: CrossAlign.Center, children: [
                Flexible(Parts.Section(theme, list.Name, T.ProjectsOpen(list.Total - list.Done))),
                new Menu(new IconButton(new Icon(LucideIcons.Ellipsis, 20), $"{T.More}: {list.Name}"),
                    listMenu.Select(m => new MenuItem(m.Label) { Destructive = m.Label == T.ProjectsDeleteList }).ToList(),
                    index => listMenu[index].Run()),
            ]));
            children.Add(new TaskRows
            {
                Tasks = entry.Tasks, OnEdit = id => SetState(() => _editing = id), OnChanged = archived => _ = Show(Load(_id)),
            });
            children.Add(Parts.QuickAdd(_titles.GetValueOrDefault(list.Id, ""), T.ProjectsNewTaskIn(list.Name), T.Add,
                value => SetState(() => _titles[list.Id] = value), () => AddTo(list.Id), Variant.Outline));
        }

        children.Add(Parts.Section(theme, T.ProjectsCreateList));
        children.Add(Parts.QuickAdd(_newList, T.ListsNewName, T.ProjectsCreateList, value => SetState(() => _newList = value),
            AddOneList, Variant.Outline));

        if (_editing != 0)
            children.Add(new TaskEditor
            {
                TaskId = _editing,
                OnClose = () => { SetState(() => _editing = 0); _ = Show(Load(_id)); },
            });
        if (_renaming != 0)
            children.Add(Parts.NameModal(theme, _renaming == -1 ? T.Rename : T.ProjectsRenameList, _name,
                value => SetState(() => _name = value),
                () => { var target = _renaming; SetState(() => _renaming = 0); _ = Show(Rename(_id, target, _name)); },
                () => SetState(() => _renaming = 0)));
        if (_deleting)
            children.Add(new Dialog(T.ProjectsDeleteTitle,
                T.ProjectsDeleteText(project.Name, project.Lists, project.Open + project.Done),
                [
                    new DialogAction(T.Cancel, () => SetState(() => _deleting = false), Variant.Outline),
                    new DialogAction(T.Delete, () => _ = DeleteProject(), Variant.Destructive),
                ],
                dismissible: true, onDismiss: () => SetState(() => _deleting = false)));
        if (_deletingList != null)
        {
            var list = _deletingList;
            children.Add(new Dialog(T.ListsDeleteTitle, T.ListsDeleteText(list.Name, list.Total),
                [
                    new DialogAction(T.Cancel, () => SetState(() => _deletingList = null), Variant.Outline),
                    new DialogAction(T.Delete, () => _ = Show(RemoveList(_id, list.Id)), Variant.Destructive),
                ],
                dismissible: true, onDismiss: () => SetState(() => _deletingList = null)));
        }
        return AppShell("/projetos", Column(gap: Space.S4, children: [.. children]));
    }
}
