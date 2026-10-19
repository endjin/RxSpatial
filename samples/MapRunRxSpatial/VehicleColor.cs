namespace MapRunRxSpatial;

/// <summary>
/// Marker colours in ascending urgency. Every location push resets a marker to Blue (location
/// observers are notified before query observers), then each continuous query may only raise
/// the colour, so the most urgent query wins regardless of notification order.
/// </summary>
internal enum VehicleColor
{
    Blue,
    Gray,
    Green,
    Yellow,
    Red,
}

/// <summary>
/// CSS colours for a marker (fill, border and label text) and its shape, a class name in
/// map.html: "circle", "dashed" (a circle with a dashed ring) or "diamond".
/// </summary>
internal readonly record struct MarkerStyle(string Fill, string Border, string Text, string Shape);

/// <summary>Maps a <see cref="VehicleColor"/> to the marker styling sent to the map.</summary>
/// <remarks>
/// Colour is not the only cue (WCAG 1.4.1): an alert is a diamond and an out-of-range pair a
/// dashed ring, so red and gray stay distinct from green and blue without colour vision;
/// yellow differs by its dark border and label. The legends in MainWindow.xaml match.
/// </remarks>
internal static class VehicleColors
{
    extension(VehicleColor color)
    {
        public MarkerStyle Style => color switch
        {
            VehicleColor.Blue => new MarkerStyle("#1565C0", "#FFFFFF", "#FFFFFF", "circle"),
            VehicleColor.Gray => new MarkerStyle("#757575", "#FFFFFF", "#FFFFFF", "dashed"),
            VehicleColor.Green => new MarkerStyle("#2E7D32", "#FFFFFF", "#FFFFFF", "circle"),

            // Dark border and label: white on amber is unreadable over OSM tiles.
            VehicleColor.Yellow => new MarkerStyle("#FFD54F", "#333333", "#222222", "circle"),
            VehicleColor.Red => new MarkerStyle("#D32F2F", "#FFFFFF", "#FFFFFF", "diamond"),
            _ => throw new ArgumentOutOfRangeException(nameof(color), color, "Unknown vehicle colour."),
        };
    }
}
