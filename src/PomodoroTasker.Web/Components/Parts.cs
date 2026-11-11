using eQuantic.UI.Components;
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

    /// <summary>A section title with a quiet aside.</summary>
    public static VisualNode Section(IAppTheme theme, string title, string aside = "")
    {
        var row = new Row(gap: Space.S3, cross: CrossAlign.End) { Padding = new EdgeInsets(0, Space.S4, 0, 0) };
        row.Add(new Text(title, TypeRole.Title, theme.TextPrimary, maxLines: 1, headingLevel: 2));
        row.Add(new Text(aside, TypeRole.Caption, theme.TextMuted, maxLines: 1));
        return row;
    }

    public static VisualNode Note(IAppTheme theme, string text) => new Text(text, TypeRole.BodyM, theme.TextMuted);

    /// <summary>A text field with its button: Enter submits.</summary>
    public static VisualNode QuickAdd(string value, string placeholder, string button, Action<string> onChanged,
        Action onSubmit, Variant variant = Variant.Primary)
    {
        var row = new Row(gap: Space.S2, cross: CrossAlign.End);
        row.Add(new Flexible(new TextEntry(value, onChanged) { Placeholder = placeholder, Label = placeholder, OnSubmit = onSubmit }));
        row.Add(new Button(button, variant, onPressed: onSubmit));
        return row;
    }

    /// <summary>One line of a list: a title, what is known about it, and something at the end.</summary>
    public static VisualNode Line(IAppTheme theme, string title, string meta, VisualNode? trailing = null,
        bool quiet = false, string note = "")
    {
        var text = new Column(gap: 2);
        text.Add(new Text(title, TypeRole.BodyL, quiet ? theme.TextMuted : theme.TextPrimary));
        if (meta != "") text.Add(new Text(meta, TypeRole.Caption, theme.TextMuted));
        if (note != "") text.Add(new Text(note, TypeRole.Caption, theme.TextSecondary));
        var row = new Row(gap: Space.S3, cross: CrossAlign.Center);
        row.Add(new Flexible(text));
        if (trailing != null) row.Add(trailing);
        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill, Padding = EdgeInsets.Symmetric(0, Space.S3),
            BorderWidth = 1, BorderColor = theme.Border, BorderSides = BorderSides.Bottom,
        }, row);
    }

    /// <summary>Big numbers side by side, each over its label.</summary>
    public static VisualNode Kpis(IAppTheme theme, (string Value, string Label)[] items)
    {
        var row = new Row(gap: Space.S8, cross: CrossAlign.Start) { Wrap = true };
        foreach (var item in items)
        {
            var cell = new Column(gap: 0);
            cell.Add(new Text(item.Value, TypeRole.Heading, theme.TextPrimary, maxLines: 1));
            cell.Add(new Text(item.Label, TypeRole.Caption, theme.TextMuted, maxLines: 1));
            row.Add(cell);
        }
        return row;
    }

    /// <summary>Asks for a name in a modal: rename and create share it.</summary>
    public static VisualNode NameModal(IAppTheme theme, string title, string value, Action<string> onChanged,
        Action onSave, Action onCancel)
    {
        var actions = new Row(gap: Space.S2, main: MainAlign.End);
        actions.Add(new Button(T.Cancel, Variant.Outline, onPressed: onCancel));
        actions.Add(new Button(T.Save, onPressed: onSave));
        var content = new Column(gap: Space.S4);
        content.Add(new Text(title, TypeRole.Title, theme.TextPrimary));
        content.Add(new TextInput(value, onChanged, T.Name) { Autofocus = true });
        content.Add(actions);
        return Modal(theme, title, content, onCancel);
    }
}
