using Microsoft.SqlServer.Types;
using RxSpatial.Backends;
using RxSpatial.Index;

namespace RxSpatial.SqlServer;

/// <summary>
/// Planar operations for <see cref="RxSqlGeometry"/>: SQL Server's flat-earth
/// <see cref="SqlGeometry"/> methods, with rectangles in the geometry's own X and Y.
/// </summary>
internal readonly struct SqlGeometryOperations : ISpatialOperations<SqlGeometry>
{
    public static Rect BoundsOf(SqlGeometry shape)
    {
        if (shape.STGeometryType().Value == "Point")
        {
            // Fast path for bare position pushes.
            double x = shape.STX.Value;
            double y = shape.STY.Value;
            return new Rect(x, y, x, y);
        }

        return SpatialBounds.OfEnvelope(shape.STEnvelope());
    }

    /// <summary><c>a.STIntersects(b)</c>; false when SQL Server returns Null.</summary>
    public static bool Intersects(SqlGeometry a, SqlGeometry b) => a.STIntersects(b).IsTrue;

    /// <summary><c>a.STIntersection(b)</c>, forwarded as-is.</summary>
    public static SqlGeometry Intersection(SqlGeometry a, SqlGeometry b) => a.STIntersection(b);

    /// <summary>
    /// The Euclidean distance in the geometries' coordinate units, or <see cref="double.NaN"/>
    /// when SQL Server cannot compute one (different SRIDs, an empty geometry).
    /// </summary>
    public static double Distance(SqlGeometry a, SqlGeometry b) => SpatialDistance.ToDouble(a.STDistance(b));

    public static GridLayout GridLayoutOf(RxSpatialOptions options) => GridLayout.From(options.PlanarGrid);
}
