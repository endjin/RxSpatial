namespace MapRunRxSpatial;

/// <summary>How a convoy connection line is drawn in each distance band.</summary>
internal readonly record struct ConnectionLineStyle(string Color, double Width, bool Dotted, double Opacity)
{
    public static ConnectionLineStyle For(DistanceBand band) => band switch
    {
        DistanceBand.OutOfRange => new ConnectionLineStyle("#757575", 1, Dotted: true, 0.8),
        DistanceBand.NearLimit => new ConnectionLineStyle("#2E7D32", 2, Dotted: false, 0.8),
        DistanceBand.Stable => new ConnectionLineStyle("#F9A825", 4, Dotted: false, 0.9),
        DistanceBand.TooClose => new ConnectionLineStyle("#D32F2F", 5, Dotted: false, 1.0),
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown distance band."),
    };
}

/// <summary>
/// One map line per watched pair; both vehicles' observers update the same shape. The map's
/// setLine is an upsert, so an update simply redraws the line in place.
/// </summary>
internal sealed class ConnectionLines(MapLibreMap map)
{
    private readonly HashSet<VehiclePair> drawn = [];

    public void Update(VehiclePair pair, GeoPoint a, GeoPoint b, DistanceBand band)
    {
        ConnectionLineStyle style = ConnectionLineStyle.For(band);
        drawn.Add(pair);
        map.SetLine(pair.ShapeId, [a, b], style.Color, style.Width, style.Dotted, style.Opacity);
    }

    public void Remove(VehiclePair pair)
    {
        if (drawn.Remove(pair))
            map.RemoveShape(pair.ShapeId);
    }

    public void Clear()
    {
        foreach (VehiclePair pair in drawn)
            map.RemoveShape(pair.ShapeId);

        drawn.Clear();
    }
}
