// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

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
/// map.html: "circle", "dashed" (a circle with a dashed ring), "square" (rounded), "triangle"
/// (pointing up, like a warning sign) or "diamond".
/// </summary>
internal readonly record struct MarkerStyle(string Fill, string Border, string Text, string Shape);

/// <summary>Maps a <see cref="VehicleColor"/> to the marker styling sent to the map.</summary>
/// <remarks>
/// Colour is never the only cue (WCAG 1.4.1): every state has its own shape, so a viewer who
/// cannot tell the fills apart (blue and green are close under tritanopia, for example) can
/// still read the state. The legends in MainWindow.xaml draw and name the same shapes.
/// </remarks>
internal static class VehicleColors
{
    extension(VehicleColor color)
    {
        public MarkerStyle Style => color switch
        {
            VehicleColor.Blue => new MarkerStyle("#1565C0", "#FFFFFF", "#FFFFFF", "circle"),
            VehicleColor.Gray => new MarkerStyle("#757575", "#FFFFFF", "#FFFFFF", "dashed"),
            VehicleColor.Green => new MarkerStyle("#2E7D32", "#FFFFFF", "#FFFFFF", "square"),

            // Dark border and label: white on amber is unreadable over OSM tiles.
            VehicleColor.Yellow => new MarkerStyle("#FFD54F", "#333333", "#222222", "triangle"),
            VehicleColor.Red => new MarkerStyle("#D32F2F", "#FFFFFF", "#FFFFFF", "diamond"),
            _ => throw new ArgumentOutOfRangeException(nameof(color), color, "Unknown vehicle colour."),
        };
    }
}
