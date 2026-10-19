using Microsoft.SqlServer.Types;
using RxSpatial.Index;

namespace RxSpatial;

/// <summary>
/// Bounding rectangles of SQL Server spatial instances, as the <see cref="Rect"/> the grid and
/// tree backends index by. The one place that turns an envelope into a rectangle.
/// </summary>
internal static class SpatialBounds
{
    /// <summary>The bounding rectangle of a geography: X is longitude, Y is latitude.</summary>
    public static Rect OfGeography(SqlGeography location)
    {
        if (location.STGeometryType().Value == "Point")
        {
            // Fast path for bare position pushes; also avoids the WKB round-trip.
            double x = location.Long.Value;
            double y = location.Lat.Value;
            return new Rect(x, y, x, y);
        }

        return OfEnvelope(SqlGeometry.STGeomFromWKB(location.STAsBinary(), location.STSrid.Value).STEnvelope());
    }

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
