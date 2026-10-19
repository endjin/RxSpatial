using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>
/// The single place where SQL Server's <see cref="SqlDouble"/> distance becomes the
/// <see cref="double"/> the RxSpatial papers declare for the distance queries.
/// </summary>
internal static class SpatialDistance
{
    /// <summary>
    /// The distance in metres between two geographies, or <see cref="double.NaN"/> when
    /// <see cref="SqlGeography.STDistance"/> returns <see cref="SqlDouble.Null"/> (e.g.
    /// different SRIDs or an empty geography). Mapping Null to NaN, rather than dropping
    /// it, keeps one notification per location update.
    /// </summary>
    public static double Between(SqlGeography from, SqlGeography to)
    {
        SqlDouble distance = from.STDistance(to);
        return distance.IsNull ? double.NaN : distance.Value;
    }
}
