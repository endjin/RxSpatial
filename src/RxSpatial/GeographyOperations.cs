using Microsoft.SqlServer.Types;
using RxSpatial.Backends;
using RxSpatial.Index;

namespace RxSpatial;

/// <summary>
/// Geodetic operations for <see cref="RxGeography"/>: SQL Server's ellipsoidal
/// <see cref="SqlGeography"/> methods, with rectangles in longitude (X) and latitude (Y).
/// </summary>
internal readonly struct GeographyOperations : ISpatialOperations<SqlGeography>
{
    public static Rect BoundsOf(SqlGeography shape)
    {
        if (shape.STGeometryType().Value == "Point")
        {
            // Fast path for bare position pushes; also avoids the WKB round-trip.
            double x = shape.Long.Value;
            double y = shape.Lat.Value;
            return new Rect(x, y, x, y);
        }

        return SpatialBounds.OfEnvelope(
            SqlGeometry.STGeomFromWKB(shape.STAsBinary(), shape.STSrid.Value).STEnvelope());
    }

    public static bool Intersects(SqlGeography a, SqlGeography b) => a.STIntersects(b).IsTrue;

    public static SqlGeography Intersection(SqlGeography a, SqlGeography b) => a.STIntersection(b);

    /// <summary>
    /// The distance in metres, or <see cref="double.NaN"/> when SQL Server cannot compute one
    /// (different SRIDs, an empty geography).
    /// </summary>
    public static double Distance(SqlGeography a, SqlGeography b) => SpatialDistance.ToDouble(a.STDistance(b));

    public static GridLayout GridLayoutOf(RxSpatialOptions options) => GridLayout.From(options.Grid);
}
