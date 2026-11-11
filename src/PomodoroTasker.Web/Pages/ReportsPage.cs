using eQuantic.UI.Charts;
using eQuantic.UI.Components;
using eQuantic.UI.Lucide;
using eQuantic.UI.Primitives;
using PomodoroTasker.Web.Components;
using PomodoroTasker.Web.Server;
using PomodoroTasker.Web.Theme;

namespace PomodoroTasker.Web.Pages;

/// <summary>Relatórios: what the closed days and the pomodoros say about a period.</summary>
[Page("/relatorios", Title = "Relatórios · pomodoro-tasker")]
public sealed class ReportsPage : StatefulComponent
{
    private static readonly string[] Kinds = ["week", "month", "custom"];
    private ReportsView? _view;
    private int _kind;

    [ServerAction]
    public Task<ReportsView> Load(string kind, string from, string to, int shift) =>
        Task.FromResult(Api.Reports(kind, from, to, shift));

    [ServerAction]
    public Task<ReportsView> Shortcut(string shortcut) =>
        Task.FromResult(Api.Reports("custom", Api.ReportStart(shortcut), Api.TodayDay(), 0));

    protected override void OnMount() => _ = Show(Load("week", "", "", 0));

    private async Task Show(Task<ReportsView> change)
    {
        var view = await change;
        SetState(() => _view = view);
    }

    private void SetKind(int kind)
    {
        SetState(() => _kind = kind);
        // Week and month start over from the current one; custom keeps the dates on screen.
        _ = Show(Load(Kinds[kind], kind == 2 ? _view!.From : "", kind == 2 ? _view!.To : "", 0));
    }

    private void Move(int shift) => _ = Show(Load(Kinds[_kind], _view!.From, _view.To, shift));

    /// <summary>An axis for small counts: at least 0 to 5, so the ticks are whole numbers.</summary>
    private static ValueAxis Counts(List<double> a, List<double> b)
    {
        double top = 5;
        for (var i = 0; i < a.Count; i++) top = Math.Max(top, a[i] + b[i]);
        return new ValueAxis(Max: top);
    }

    private static readonly float[] HeatOpacity = [0.08f, 0.3f, 0.5f, 0.75f, 1f];

    private static VisualNode HeatCell(IAppTheme theme, int level) => Box(new BoxStyle
    {
        Width = 14, Height = 14,
        Background = level < 0 ? null : level == 0 ? theme.Border : TaskerTheme.Accent.WithOpacity(HeatOpacity[level]),
    });

    private static VisualNode Heat(IAppTheme theme, List<int> levels)
    {
        // Columns are weeks, Sunday on top.
        var columns = new List<VisualNode>();
        for (var week = 0; week < levels.Count / 7; week++)
        {
            var cells = new List<VisualNode>();
            for (var d = 0; d < 7; d++)
            {
                cells.Add(HeatCell(theme, levels[week * 7 + d]));
            }
            columns.Add(Column(gap: 3, children: [.. cells]));
        }
        return Row(gap: 3, cross: CrossAlign.Start, children: [.. columns]);
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var v = _view;
        var children = new List<VisualNode> { Parts.Heading(theme, T.NavReports, v?.Range) };
        if (v == null) return AppShell("/relatorios", Column(gap: Space.S4, children: [.. children]));
        if (v.Empty)
        {
            children.Add(new EmptyState(new Icon(LucideIcons.ChartColumn, 32), T.ReportsEmptyTitle, T.ReportsEmptyText)
            {
                Action = Button(T.CloseGoToday, Variant.Outline, onPressed: () => Navigator.Go("/")),
            });
            return AppShell("/relatorios", Column(gap: Space.S4, children: [.. children]));
        }

        children.Add(Row(gap: Space.S2, cross: CrossAlign.Center, children: [
            new IconButton(new Icon(LucideIcons.ChevronLeft, 20), T.ReportsPrev, onPressed: () => Move(-1)) { Disabled = _kind == 2 },
            Flexible(new SegmentedControl([T.ReportsWeek, T.ReportsMonth, T.ReportsCustom], _kind, SetKind)),
            new IconButton(new Icon(LucideIcons.ChevronRight, 20), T.ReportsNext, onPressed: () => Move(1))
            {
                Disabled = _kind == 2 || !v.CanNext,
            },
        ]));
        if (_kind == 2)
        {
            children.Add(Row(gap: Space.S4, cross: CrossAlign.End, children: [
                Flexible(new DatePicker(Dates.Parse(v.From), date => _ = Show(Load("custom", Dates.Str(date), v.To, 0)), label: T.ReportsFrom)),
                Flexible(new DatePicker(Dates.Parse(v.To), date => _ = Show(Load("custom", v.From, Dates.Str(date), 0)), label: T.ReportsTo)),
            ]));
            children.Add(Row(gap: Space.S2, wrap: true, children: [
                Chip(T.ReportsLast7, ChipKind.Filter, onPressed: () => _ = Show(Shortcut("6"))),
                Chip(T.ReportsLast30, ChipKind.Filter, onPressed: () => _ = Show(Shortcut("29"))),
                Chip(T.ReportsYear, ChipKind.Filter, onPressed: () => _ = Show(Shortcut("year"))),
                Chip(T.ReportsAll, ChipKind.Filter, onPressed: () => _ = Show(Shortcut("all"))),
            ]));
        }

        var done = (int)v.Done.Sum();
        var postponed = (int)v.Postponed.Sum();
        children.Add(Parts.Kpis(theme, [
            (v.Rate < 0 ? "–" : $"{v.Rate}%", T.ReportsRate), (done.ToString(), T.ReportsTasksDone),
            (postponed.ToString(), T.ReportsPostponements),
        ]));

        var categories = CategoryAxis(v.Labels);
        children.Add(BarChart(
            series: [ChartSeries(T.ReportsDone, v.Done), ChartSeries(T.ReportsPostponed, v.Postponed)],
            categories: categories, values: Counts(v.Done, v.Postponed), layout: BarLayout.Stacked,
            title: T.ReportsDoneVsPostponed, subtitle: v.Weekly ? T.ReportsPerWeek : T.ReportsPerDay, plotHeight: 160));
        if (v.Best != "") children.Add(Parts.Note(theme, v.Best));
        if (v.Weekly) children.Add(Parts.Note(theme, T.ReportsWeekly));

        children.Add(BarChart(
            series: [ChartSeries(T.ReportsCompleted, v.Completed, 2), ChartSeries(T.ReportsInterrupted, v.Interrupted, 1)],
            categories: categories, values: Counts(v.Completed, v.Interrupted), layout: BarLayout.Stacked,
            title: T.ReportsPomodoros, subtitle: T.ReportsPomodoroTotals((int)v.Completed.Sum(), (int)v.Interrupted.Sum()),
            plotHeight: 160));

        if (v.ProjectNames.Count == 0)
        {
            children.Add(Parts.Section(theme, T.ReportsByProject, v.FocusTotal));
            children.Add(Parts.Note(theme, T.ReportsNone));
        }
        else
            children.Add(BarChart(
                series: [ChartSeries(T.ReportsFocus, v.ProjectMinutes, 2)], categories: CategoryAxis(v.ProjectNames),
                orientation: ChartOrientation.Horizontal, title: T.ReportsByProject, subtitle: v.FocusTotal,
                plotHeight: Math.Max(160, 44 * v.ProjectNames.Count)));

        children.Add(Parts.Section(theme, T.ReportsActivity, T.ReportsLastWeeks(v.Heat.Count / 7)));
        children.Add(ScrollView(Heat(theme, v.Heat), ScrollAxis.Horizontal));
        children.Add(Row(gap: Space.S2, cross: CrossAlign.Center, children: [
            Text(T.ReportsLess, TypeRole.Caption, theme.TextMuted),
            Row(gap: 3, children: [.. Enumerable.Range(0, 5).Select(level => HeatCell(theme, level))]),
            Text(T.ReportsMorePomodoros, TypeRole.Caption, theme.TextMuted),
        ]));
        children.Add(Parts.Kpis(theme, [
            (v.StreakNow.ToString(), T.ReportsStreakNow), (v.StreakBest.ToString(), T.ReportsStreakBest),
            (v.StreakTotal.ToString(), T.ReportsDaysWithFocus),
        ]));
        children.Add(Parts.Note(theme, T.ReportsStreakRule));

        children.Add(Parts.Section(theme, T.ReportsMostPostponed, T.ReportsInPeriodShort));
        if (v.MostPostponed.Count == 0) children.Add(Parts.Note(theme, T.ReportsNone));
        for (var i = 0; i < v.MostPostponed.Count; i++)
        {
            var p = v.MostPostponed[i];
            children.Add(Parts.Line(theme, $"{i + 1}. {p.Title}", p.Text,
                Text($"{p.Times}×", TypeRole.Title, theme.TextPrimary, maxLines: 1)));
        }
        return AppShell("/relatorios", Column(gap: Space.S4, children: [.. children]));
    }
}
