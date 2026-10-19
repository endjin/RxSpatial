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

/// <summary>CSS colours for a marker: fill, border and label text.</summary>
internal readonly record struct MarkerStyle(string Fill, string Border, string Text);

/// <summary>Maps a <see cref="VehicleColor"/> to the marker styling sent to the map.</summary>
internal static class VehicleColors
{
    extension(VehicleColor color)
    {
        public MarkerStyle Style => color switch
        {
            VehicleColor.Blue => new MarkerStyle("#1565C0", "#FFFFFF", "#FFFFFF"),
            VehicleColor.Gray => new MarkerStyle("#757575", "#FFFFFF", "#FFFFFF"),
            VehicleColor.Green => new MarkerStyle("#2E7D32", "#FFFFFF", "#FFFFFF"),

            // Dark border and label: white on amber is unreadable over OSM tiles.
            VehicleColor.Yellow => new MarkerStyle("#FFD54F", "#333333", "#222222"),
            VehicleColor.Red => new MarkerStyle("#D32F2F", "#FFFFFF", "#FFFFFF"),
            _ => throw new ArgumentOutOfRangeException(nameof(color), color, "Unknown vehicle colour."),
        };
    }
}
