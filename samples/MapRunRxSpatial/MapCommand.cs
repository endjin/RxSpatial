using System.Text.Json;
using RxSpatial.Geo;

namespace MapRunRxSpatial;

/// <summary>
/// One command in a <see cref="MapLibreMap"/> batch, written straight to the batch's UTF-8 JSON
/// (handled by the switch at the end of map.html). The commands sent on every traffic tick —
/// markers, lines and overlays — have their own types, so they allocate no intermediate
/// objects; the rest are serialized once as they are sent (<see cref="SerializedCommand"/>).
/// </summary>
internal abstract class MapCommand
{
    public abstract void Write(Utf8JsonWriter writer, OutlineWriter outlines);
}

/// <summary>A command serialized when it was sent: the rare ones (areas, drawing mode, view, removals).</summary>
internal sealed class SerializedCommand(object command) : MapCommand
{
    private readonly byte[] json = JsonSerializer.SerializeToUtf8Bytes(command);

    public override void Write(Utf8JsonWriter writer, OutlineWriter outlines) => writer.WriteRawValue(json, skipInputValidation: true);
}

/// <summary>
/// A vehicle marker's state, one instance per marker for as long as it is on the map. A batch
/// holds it at most once, so the marker's moves and colour changes during a tick reach the map
/// as one setMarker with the final state; and it writes only what the map does not already
/// have: the label once, the style when it changes, the position and reach on every move.
/// </summary>
internal sealed class MarkerCommand(string id, string label) : MapCommand
{
    /// <summary>
    /// Vertices of a drawn reach circle: as many as map.html drew itself before, which keeps
    /// the twenty markers' messages small at 25 updates a second. The chord sag at the 3 km
    /// maximum reach is under 4 m, finer than a pixel at the Overlap tab's zoom.
    /// </summary>
    private const int ReachSegments = 64;

    private GeoPoint position;
    private GeoCircle? reach;
    private bool moved;
    private bool created;
    private bool reachShown;
    private MarkerStyle? sentStyle;

    public MarkerStyle Style { get; set; }

    public void Move(GeoPoint position, MarkerStyle style, GeoCircle? reach)
    {
        this.position = position;
        this.reach = reach;
        moved = true;
        Style = style;
    }

    public override void Write(Utf8JsonWriter writer, OutlineWriter outlines)
    {
        writer.WriteStartObject();
        writer.WriteString("cmd", "setMarker");
        writer.WriteString("id", id);
        if (!created)
            writer.WriteString("label", label);

        if (moved)
        {
            writer.WriteNumber("lat", position.Latitude);
            writer.WriteNumber("lon", position.Longitude);
        }

        if (Style != sentStyle)
        {
            writer.WriteString("fill", Style.Fill);
            writer.WriteString("border", Style.Border);
            writer.WriteString("text", Style.Text);
            writer.WriteString("shape", Style.Shape);
            sentStyle = Style;
        }

        // The reach moves with the marker; null removes a ring the map still shows.
        if (moved && reach is not null)
            outlines.WriteRing(writer, "reach", reach, ReachSegments);
        else if (moved && reachShown)
            writer.WriteNull("reach");

        writer.WriteEndObject();
        reachShown = moved ? reach is not null : reachShown;
        created = true;
        moved = false;
    }
}

/// <summary>A straight line between two points (a convoy connection line).</summary>
internal sealed class LineCommand(string id, GeoPoint from, GeoPoint to, string color, double width, bool dashed, double opacity) : MapCommand
{
    public override void Write(Utf8JsonWriter writer, OutlineWriter outlines)
    {
        writer.WriteStartObject();
        writer.WriteString("cmd", "setLine");
        writer.WriteString("id", id);

        writer.WriteStartArray("points");
        WritePoint(writer, from);
        WritePoint(writer, to);
        writer.WriteEndArray();
        writer.WriteString("color", color);
        writer.WriteNumber("width", width);
        writer.WriteBoolean("dashed", dashed);
        writer.WriteNumber("opacity", opacity);
        writer.WriteEndObject();
    }

    // GeoJSON uses [longitude, latitude] ordering.
    private static void WritePoint(Utf8JsonWriter writer, GeoPoint point)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(point.Longitude);
        writer.WriteNumberValue(point.Latitude);
        writer.WriteEndArray();
    }
}

/// <summary>A query-result region (a lens or a zone overlap) drawn from its outline, with an optional label.</summary>
internal sealed class OverlayCommand(string kind, string id, GeoShape region, string? label) : MapCommand
{
    public override void Write(Utf8JsonWriter writer, OutlineWriter outlines)
    {
        writer.WriteStartObject();
        writer.WriteString("cmd", "setOverlay");
        writer.WriteString("kind", kind);
        writer.WriteString("id", id);
        outlines.WriteRing(writer, "ring", region, GeoOperations.DefaultSegments);
        writer.WriteString("label", label);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Writes shape outlines into the batch from one reused vertex buffer
/// (<see cref="GeoOperations.ToVertices(GeoShape, Span{GeoPoint}, int)"/>), so redrawing twenty
/// reach circles every tick allocates nothing.
/// </summary>
internal sealed class OutlineWriter
{
    private GeoPoint[] vertices = new GeoPoint[GeoOperations.DefaultSegments];

    /// <summary>
    /// Writes the outline of a circle or polygon as one flat array of integer micro-degrees,
    /// the first vertex absolute and each later one as the step from the one before:
    /// [lon0, lat0, dLon1, dLat1, ...]. decodeRing in map.html closes and rebuilds the ring.
    /// Neighbouring vertices are at most a few hundred metres apart, so a step takes 1-4
    /// digits where a coordinate rounded to 6 decimals took 9-11 plus brackets: an outline
    /// takes about a third of the characters. The coordinates the map rebuilds
    /// (integer / 1e6) are exactly those the rounded decimals parsed to, because
    /// Math.Round(x, 6) is itself Math.Round(x * 1e6) / 1e6.
    /// </summary>
    public void WriteRing(Utf8JsonWriter writer, string propertyName, GeoShape shape, int segments)
    {
        int capacity = shape is GeoPolygon polygon ? polygon.Vertices.Count : segments;
        if (vertices.Length < capacity)
            vertices = new GeoPoint[Math.Max(capacity, vertices.Length * 2)];

        int count = GeoOperations.ToVertices(shape, vertices, segments);
        writer.WriteStartArray(propertyName);
        long longitude = 0, latitude = 0;
        foreach (GeoPoint vertex in vertices.AsSpan(0, count))
        {
            long x = (long)Math.Round(vertex.Longitude * 1e6);
            long y = (long)Math.Round(vertex.Latitude * 1e6);
            writer.WriteNumberValue(x - longitude);
            writer.WriteNumberValue(y - latitude);
            longitude = x;
            latitude = y;
        }

        writer.WriteEndArray();
    }
}
