using eQuantic.UI.Primitives;

[assembly: GoogleFont("Silkscreen", FontWeight.Regular, FontWeight.Bold)]
[assembly: GoogleFont("Schibsted Grotesk", FontWeight.Regular, FontWeight.SemiBold, FontWeight.Bold)]

namespace PomodoroTasker.Web.Theme;

/// <summary>Paper, ink and one magenta accent; square corners. Tokens from the desktop app's app.css.</summary>
public sealed class TaskerTheme : IAppTheme
{
    private static readonly IAppTheme Inner = PhotonTheme.Instance;

    private static Color Hex(uint rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    private static ColorToken Pair(uint light, uint dark) => new(Hex(light), Hex(dark));

    public static readonly ColorToken Paper = Pair(0xF2F0EA, 0x161614);
    public static readonly ColorToken Ink = Pair(0x0D0D0D, 0xF2F0EA);
    public static readonly ColorToken Ink2 = Pair(0x3A3935, 0xCFCCC4);
    public static readonly ColorToken Mute = Pair(0x5A5853, 0xA8A59E);
    public static readonly ColorToken Rule = Pair(0xC9C6BD, 0x3F3D39);
    public static readonly ColorToken Field = Pair(0xFFFFFF, 0x0D0D0D);
    public static readonly ColorToken Accent = Pair(0xFF2E88, 0xFF2E88);
    public static readonly ColorToken OnAccent = Pair(0x0D0D0D, 0x0D0D0D);
    public static readonly ColorToken Leaf = Pair(0x1F7A2E, 0x5FD06E);
    public static readonly ColorToken Block = Pair(0x0D0D0D, 0x000000);
    public static readonly ColorToken BlockInk = Pair(0xF2F0EA, 0xF2F0EA);
    public static readonly ColorToken BlockMute = Pair(0xD5D2CA, 0xD5D2CA);

    public ColorToken Background => Paper;
    public ColorToken Surface => Field;
    public ColorToken SurfaceSubtle => Paper;
    public ColorToken SurfaceHighlight => Rule;
    public ColorToken Border => Rule;
    public ColorToken BorderStrong => Ink;
    public ColorToken TextPrimary => Ink;
    public ColorToken TextSecondary => Ink2;
    public ColorToken TextMuted => Mute;
    public ColorToken TextInverse => Paper;
    public ColorToken FocusRing => Accent;
    public ColorToken LinkColor => Ink;
    public ColorToken Scrim => Inner.Scrim;
    public ColorToken Selection => Accent;
    public ColorToken? OnSelection => OnAccent;

    public VariantColors Colors(Variant variant) => variant switch
    {
        Variant.Primary => new(Accent, OnAccent, Accent, Rule, Ink),
        Variant.Secondary => new(Ink, Paper, Ink2, Rule, Ink),
        Variant.Success => new(Leaf, Paper, Leaf, Rule, Leaf),
        _ => Inner.Colors(variant),
    };

    public FontFamily? TextFamily => Fonts.SchibstedGrotesk;

    // The bitmap face only at whole multiples of 8px.
    public TypeStyle Type(TypeRole role) => role switch
    {
        TypeRole.Display => Inner.Type(role) with { Family = Fonts.Silkscreen, Size = 64, LineHeight = 64, Tracking = -8 },
        TypeRole.Heading => Inner.Type(role) with { Family = Fonts.Silkscreen, Size = 24, LineHeight = 32, Tracking = -3 },
        _ => Inner.Type(role),
    };

    public ShadowSpec Elevation(int level) => Inner.Elevation(level);
    public float Shape(ShapeScale scale) => 0;
    public float DisabledOpacity => Inner.DisabledOpacity;
}
