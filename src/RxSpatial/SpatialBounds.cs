using Microsoft.SqlServer.Types;
using RxSpatial.Index;

namespace RxSpatial;

/// <summary>
/// The one place that turns a SQL Server envelope into the <see cref="Rect"/> the grid and
/// tree backends index by, for geographies and geometries alike.
/// </summary>
internal static class SpatialBounds
{
    /// <summary>The rectangle spanned by an <c>STEnvelope()</c> result.</summary>
    public static Rect OfEnvelope(SqlGeometry envelope)
        // SQL Server's STEnvelope returns degenerate shapes for degenerate extents:
        // a Point for points, a LineString for axis-aligned lines, else a Polygon.
        => envelope.STGeometryType().Value switch
        {
            "Point" => CornersToRect(envelope, envelope),
            "LineString" => CornersToRect(envelope.STPointN(1), envelope.STPointN(2)),
            _ => CornersToRect(envelope.STPointN(1), envelope.STPointN(3)),
        };

    private static Rect CornersToRect(SqlGeometry a, SqlGeometry b)
        => new(a.STX.Value, a.STY.Value, b.STX.Value, b.STY.Value);
}
