using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using PomodoroTasker.Web.Components;
using PomodoroTasker.Web.Server;

namespace PomodoroTasker.Web.Pages;

/// <summary>Configurações: durations, the theme, and where the data lives. Every change is saved at once.</summary>
[Page("/configuracoes", Title = "Configurações · pomodoro-tasker")]
public sealed class SettingsPage : StatefulComponent
{
    private SettingsView? _view;
    private bool _dark;

    [ServerAction]
    public Task<SettingsView> Load() => Task.FromResult(Api.Settings());

    [ServerAction]
    public Task<SettingsView> Save(int focus, int @short, int @long, int every, string theme) =>
        Task.FromResult(Api.SaveSettings(focus, @short, @long, every, theme));

    protected override void OnMount()
    {
        _dark = GetService<IThemeController>()?.Mode == ThemeMode.Dark;
        _ = Show(Load());
    }

    private async Task Show(Task<SettingsView> change)
    {
        var view = await change;
        SetState(() => _view = view);
        Bus.Timer?.Invoke(); // the timer block shows the focus length and the cycle size
    }

    private void SetTheme(bool dark)
    {
        // The framework's controller knows light and dark, and remembers the choice in a cookie.
        GetService<IThemeController>()?.Apply(dark ? ThemeMode.Dark : ThemeMode.Light);
        SetState(() => _dark = dark);
        var v = _view!;
        _ = Show(Save(v.Focus, v.Short, v.Long, v.Every, dark ? "dark" : "light"));
    }

    private VisualNode Duration(IAppTheme theme, string label, string hint, int value, int min, int max, Action<int> set) =>
        Parts.Line(theme, label, hint, new Stepper(value, set) { Min = min, Max = max, Label = label });

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var v = _view;
        var children = new List<VisualNode> { Parts.Heading(theme, T.NavSettings) };
        if (v == null) return AppShell("/configuracoes", Column(gap: Space.S4, children: [.. children]));

        children.Add(Parts.Section(theme, T.SettingsTimer));
        children.Add(Duration(theme, T.TimerFocus, T.SettingsFocusHint, v.Focus, 1, 120,
            n => _ = Show(Save(n, v.Short, v.Long, v.Every, v.Theme))));
        children.Add(Duration(theme, T.TimerShort, T.SettingsMinutes, v.Short, 1, 60,
            n => _ = Show(Save(v.Focus, n, v.Long, v.Every, v.Theme))));
        children.Add(Duration(theme, T.TimerLong, T.SettingsMinutes, v.Long, 1, 120,
            n => _ = Show(Save(v.Focus, v.Short, n, v.Every, v.Theme))));
        children.Add(Duration(theme, T.SettingsEvery, T.SettingsEveryHint, v.Every, 2, 12,
            n => _ = Show(Save(v.Focus, v.Short, v.Long, n, v.Theme))));
        if (v.Running) children.Add(Parts.Note(theme, T.SettingsRunning));

        children.Add(Parts.Section(theme, T.SettingsAppearance));
        children.Add(Parts.Line(theme, T.SettingsTheme, "", Box(new BoxStyle { Width = 200 },
            new SegmentedControl([T.SettingsThemes[1], T.SettingsThemes[2]], _dark ? 1 : 0, index => SetTheme(index == 1)))));

        children.Add(Parts.Section(theme, T.SettingsData));
        children.Add(Parts.Line(theme, T.SettingsDatabase, v.Database));
        children.Add(Parts.Note(theme, v.About));
        return AppShell("/configuracoes", Column(gap: Space.S4, children: [.. children]));
    }
}
