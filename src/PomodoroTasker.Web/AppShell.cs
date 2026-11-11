using eQuantic.UI.Lucide;
using eQuantic.UI.Primitives;
using PomodoroTasker.Web.Components;
using PomodoroTasker.Web.Theme;

namespace PomodoroTasker.Web;

/// <summary>The frame every page sits in: the black rail, the timer block, then the screen.</summary>
public sealed class AppShell : StatelessComponent
{
    public AppShell(string route, VisualNode child)
    {
        Route = route;
        Child = child;
    }

    public string Route { get; init; }
    public VisualNode Child { get; init; }

    private static readonly (string Href, string Label, IconGlyph Icon)[] Sections =
    [
        ("/", T.NavToday, LucideIcons.Sun),
        ("/listas", T.NavLists, LucideIcons.List),
        ("/projetos", T.NavProjects, LucideIcons.Folder),
        ("/rotinas", T.NavRoutines, LucideIcons.Repeat),
        ("/relatorios", T.NavReports, LucideIcons.ChartColumn),
    ];

    private VisualNode Entry(string href, string label, IconGlyph icon)
    {
        var active = href == Route;
        return new Link(href, Box(new BoxStyle
        {
            Width = 48, Height = 48, Padding = EdgeInsets.All(Space.S3),
            Background = active ? TaskerTheme.Accent : null,
        }, new Icon(icon, 24, active ? TaskerTheme.OnAccent : TaskerTheme.BlockMute)))
        {
            Current = active, Label = label,
        };
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var rail = Box(new BoxStyle { Width = 64, Height = SizeValue.Fill, Background = TaskerTheme.Block, Padding = EdgeInsets.All(Space.S2) },
            Column(gap: Space.S2, children: [
                .. Sections.Select(s => Entry(s.Href, s.Label, s.Icon)),
                Spacer(),
                Entry("/configuracoes", T.NavSettings, LucideIcons.SlidersHorizontal),
            ]));

        return Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill, Background = theme.Background },
            Row(gap: 0, cross: CrossAlign.Stretch, children: [
                rail,
                Flexible(Column(gap: 0, children: [
                    new TimerBlock { Big = Route == "/" },
                    Flexible(ScrollView(Box(new BoxStyle
                    {
                        Width = SizeValue.Fill, MaxWidth = 760, Padding = EdgeInsets.All(Space.S6),
                    }, Child))),
                ])),
            ]));
    }
}
