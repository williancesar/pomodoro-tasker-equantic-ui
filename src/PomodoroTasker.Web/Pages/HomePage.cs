using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using PomodoroTasker.Web.Server;

namespace PomodoroTasker.Web.Pages;

/// <summary>Spike: a Server Action over SQLite, a clock derived from an end timestamp, the theme, a drawing.</summary>
[Page("/", Title = "Hoje")]
public sealed class HomePage : StatefulComponent
{
    private readonly IClock? _clock;
    private IDisposable? _tick;
    private List<string> _titles = [];
    private long _endsAt;
    private long _now;

    public HomePage(IClock? clock = null) => _clock = clock;

    [ServerAction]
    public Task<List<string>> Add(string title)
    {
        Store.Db.Execute("INSERT INTO tasks (title) VALUES ($1)", title);
        return Titles();
    }

    [ServerAction]
    public Task<List<string>> Titles() =>
        Task.FromResult(Store.Db.Select("SELECT title FROM tasks ORDER BY id", r => r.GetString(0)));

    protected override async void OnMount()
    {
        _now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _endsAt = _now + 25 * 60_000;
        _tick = _clock?.Every(TimeSpan.FromMilliseconds(500),
            () => SetState(() => _now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        var titles = await Titles();
        SetState(() => _titles = titles);
    }

    protected override void OnUnmount() => _tick?.Dispose();

    private async Task AddOne()
    {
        var titles = await Add($"Tarefa {_titles.Count + 1}");
        SetState(() => _titles = titles);
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var left = Math.Max(0, (_endsAt - _now) / 1000);

        return AppShell("/", Column(gap: Space.S4, children: [
            Row(gap: Space.S3, cross: CrossAlign.Center, children: [
                new Drawing(Vectors.Tomato, width: 32, label: "Pomodoro"),
                Text($"{left / 60:00}:{left % 60:00}", TypeRole.Display, theme.TextPrimary, maxLines: 1),
            ]),
            Text("Hoje", TypeRole.Heading, theme.TextPrimary, maxLines: 1),
            Button("Adicionar", onPressed: () => _ = AddOne()),
            .. _titles.Select(title => Text(title, TypeRole.BodyM, theme.TextPrimary)),
        ]));
    }
}
