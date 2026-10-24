// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using Microsoft.SqlServer.Types;
using RxSpatial.Backends;
using RxSpatial.Index;

namespace RxSpatial.SqlServer;

/// <summary>
/// Geodetic operations for <see cref="RxSqlGeography"/>: SQL Server's ellipsoidal
/// <see cref="SqlGeography"/> methods, with rectangles in longitude (X) and latitude (Y).
/// </summary>
internal readonly struct SqlGeographyOperations : ISpatialOperations<SqlGeography>
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

    /// <summary><c>a.STIntersects(b)</c>; false when SQL Server returns Null.</summary>
    public static bool Intersects(SqlGeography a, SqlGeography b) => a.STIntersects(b).IsTrue;

    /// <summary><c>a.STIntersection(b)</c>, forwarded as-is.</summary>
    public static SqlGeography Intersection(SqlGeography a, SqlGeography b) => a.STIntersection(b);

    /// <summary>
    /// The distance in metres, or <see cref="double.NaN"/> when SQL Server cannot compute one
    /// (different SRIDs, an empty geography).
    /// </summary>
    public static double Distance(SqlGeography a, SqlGeography b) => SpatialDistance.ToDouble(a.STDistance(b));

    public static GridLayout GridLayoutOf(RxSpatialOptions options) => GridLayout.From(options.Grid);
}
