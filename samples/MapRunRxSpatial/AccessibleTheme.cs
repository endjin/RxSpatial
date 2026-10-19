using System.Collections;
using System.Windows;
using System.Windows.Media;

namespace MapRunRxSpatial;

/// <summary>
/// Keeps the Fluent light theme at WCAG 2.2 AA contrast: 4.5:1 for text, 3:1 for the accent
/// state indicators (pressed toggles, selection pills, slider thumbs).
/// </summary>
/// <remarks>
/// <para>
/// Fluent's secondary text brush is translucent black (#9E000000). Composited over the card it
/// is 6.1:1 on paper, but WPF renders translucent text much lighter than its composited
/// colour: the darkest glyph pixels measure 3.2:1. It is replaced with the opaque grey it
/// composites to over the window background, whose glyphs render at that exact colour
/// (6.0:1 on the window, 6.5:1 on the card).
/// </para>
/// <para>
/// The accent is the user's Windows accent colour, which can be light (a yellow accent gives
/// white text 2.5:1). If white text on the faintest accent fill Fluent draws (pressed, at 80%
/// opacity) would fall below 4.5:1, every theme brush in the accent colour is pinned to a dark
/// blue instead; otherwise the user's accent is kept.
/// </para>
/// <para>
/// Neither override applies while a high-contrast theme is on: its system colours must win.
/// </para>
/// </remarks>
internal static class AccessibleTheme
{
    private const string SecondaryTextKey = "TextFillColorSecondaryBrush";
    private const string AccentFillKey = "AccentFillColorDefaultBrush";
    private const double MinimumTextContrast = 4.5;
    private const double PressedAccentOpacity = 0.8;

    private static readonly Color SecondaryText = Color.FromRgb(0x5C, 0x5C, 0x5C);

    // White text on it at 80% opacity over the card is still 4.6:1.
    private static readonly Color PinnedAccent = Color.FromRgb(0x00, 0x5A, 0x9E);

    // The card fill (#B3FFFFFF) composited over the light Mica / solid window background.
    private static readonly Color CardBackground = Color.FromRgb(0xFB, 0xFB, 0xFB);

    private static readonly List<object> OverriddenKeys = [];

    /// <summary>Applies the overrides to the application resources and follows high-contrast changes.</summary>
    public static void Apply(Application application)
    {
        Update(application.Resources);
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast))
                Update(application.Resources);
        };
    }

    private static void Update(ResourceDictionary resources)
    {
        foreach (object key in OverriddenKeys)
            resources.Remove(key);
        OverriddenKeys.Clear();

        if (SystemParameters.HighContrast)
            return;

        Override(resources, SecondaryTextKey, SecondaryText, opacity: 1);

        // Looked up after the removal above, so this is always the theme's own accent.
        if (resources[AccentFillKey] is SolidColorBrush accent
            && ContrastRatio(Colors.White, Composite(accent.Color, PressedAccentOpacity, CardBackground)) < MinimumTextContrast)
        {
            foreach ((object key, double opacity) in ThemeBrushesInColour(resources, accent.Color))
                Override(resources, key, PinnedAccent, opacity);
        }
    }

    private static void Override(ResourceDictionary resources, object key, Color color, double opacity)
    {
        var brush = new SolidColorBrush(color) { Opacity = opacity };
        brush.Freeze();
        resources[key] = brush;
        OverriddenKeys.Add(key);
    }

    // The theme dictionaries (merged into the application resources by ThemeMode) and their
    // solid brushes in the given colour; hover and pressed variants keep their opacity.
    private static List<(object Key, double Opacity)> ThemeBrushesInColour(ResourceDictionary resources, Color color)
    {
        List<(object, double)> found = [];
        foreach (ResourceDictionary theme in resources.MergedDictionaries)
        {
            foreach (DictionaryEntry entry in theme)
            {
                if (entry.Value is SolidColorBrush brush && brush.Color == color)
                    found.Add((entry.Key, brush.Opacity));
            }
        }

        return found;
    }

    private static Color Composite(Color foreground, double opacity, Color background)
    {
        double alpha = foreground.A / 255.0 * opacity;
        byte Mix(byte f, byte b) => (byte)Math.Round((f * alpha) + (b * (1 - alpha)));
        return Color.FromRgb(Mix(foreground.R, background.R), Mix(foreground.G, background.G), Mix(foreground.B, background.B));
    }

    /// <summary>The WCAG 2.2 contrast ratio between two opaque colours.</summary>
    private static double ContrastRatio(Color a, Color b)
    {
        double la = RelativeLuminance(a);
        double lb = RelativeLuminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double RelativeLuminance(Color c)
    {
        static double Linear(byte channel)
        {
            double s = channel / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linear(c.R)) + (0.7152 * Linear(c.G)) + (0.0722 * Linear(c.B));
    }
}
