using eQuantic.UI.Primitives;

namespace PomodoroTasker.Web.Components;

/// <summary>Pieces several screens draw the same way.</summary>
public static class Parts
{
    /// <summary>
    /// A modal layer for content the framework's Dialog cannot hold (it takes a title, a text and
    /// actions): a scrim that dismisses, the card centred over it, Escape to close.
    /// </summary>
    public static VisualNode Modal(IAppTheme theme, string title, VisualNode content, Action onDismiss)
    {
        var scrim = new Pressable(new Box(new BoxStyle
        {
            Width = SizeValue.Fill, Height = SizeValue.Fill, Background = theme.Scrim,
        }), onDismiss) { Label = T.Close };

        var card = new Box(new BoxStyle
        {
            Width = SizeValue.Fill, MaxWidth = 480, Background = theme.Background,
            BorderWidth = 2, BorderColor = theme.BorderStrong, Padding = EdgeInsets.All(Space.S6),
        }, content);

        var centering = new Column(gap: 0)
        {
            Width = SizeValue.Fill, Height = SizeValue.Fill, Main = MainAlign.Center, Cross = CrossAlign.Center,
            Padding = EdgeInsets.Symmetric(Space.S6, 0),
        };
        centering.Add(card);

        var layers = new Stack { Width = SizeValue.Fill, Height = SizeValue.Fill };
        layers.Add(scrim);
        layers.Add(centering);
        return new Shortcut(new Overlay(new Presence(layers)) { Label = title }, KeyChord.Escape, onDismiss);
    }

    public static VisualNode Heading(IAppTheme theme, string title, string? aside = null)
    {
        var row = new Row(gap: Space.S4, cross: CrossAlign.End);
        row.Add(new Text(title, TypeRole.Heading, theme.TextPrimary, maxLines: 1, headingLevel: 1));
        row.Add(new Text(aside ?? "", TypeRole.BodyM, theme.TextMuted, maxLines: 1));
        return row;
    }
}
