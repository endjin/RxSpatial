using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.SqlServer.Types;
using RxSpatial.Geo;

namespace RxSpatial.SqlServer.Benchmarks;

/// <summary>
/// One operation at a time, SQL Server's spatial types (the baseline of each category) against
/// RxSpatial.Geo on the same Seattle-scale inputs: the 500 m Space Needle fence and two 2 km
/// discs 1.5 km apart. Each pair answers the same question, but not on the same shape: SQL
/// Server's <c>STBuffer</c> disc is a polygon of about 128 edges, while <see cref="GeoCircle"/>
/// is the exact circle. The SQL bounds follow <c>SqlGeographyOperations.BoundsOf</c>, the path
/// <see cref="RxSqlGeography"/>'s tree and grid backends take for a non-point shape.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SqlVsGeoOperationsBenchmarks
{
    private const int Srid = 4326;
    private static readonly GeoPoint Needle = new(47.6205, -122.3493);

    // Geo inputs.
    private readonly GeoCircle fence = new(Needle, 500);
    private readonly GeoPointShape inside = new(47.6230, -122.3480);
    private readonly GeoPointShape outsideNear = new(Wgs84.Direct(Needle, 22.5, 600));
    private readonly GeoPointShape route = new(47.6300, -122.3550);
    private readonly GeoCircle disc = new(Needle, 2_000);
    private readonly GeoCircle otherDisc = new(Wgs84.Direct(Needle, 63, 1_500), 2_000);
    private GeoShape lens = null!;

    // The same inputs as SQL Server geographies.
    private SqlGeography sqlFence = null!;
    private SqlGeography sqlInside = null!;
    private SqlGeography sqlOutsideNear = null!;
    private SqlGeography sqlRoute = null!;
    private SqlGeography sqlDisc = null!;
    private SqlGeography sqlOtherDisc = null!;
    private SqlGeography sqlLens = null!;

    [GlobalSetup]
    public void Setup()
    {
        sqlFence = Point(fence.Center).STBuffer(fence.RadiusMetres);
        sqlInside = Point(inside.Position);
        sqlOutsideNear = Point(outsideNear.Position);
        sqlRoute = Point(route.Position);
        sqlDisc = Point(disc.Center).STBuffer(disc.RadiusMetres);
        sqlOtherDisc = Point(otherDisc.Center).STBuffer(otherDisc.RadiusMetres);
        lens = GeoOperations.Intersection(disc, otherDisc);
        sqlLens = sqlDisc.STIntersection(sqlOtherDisc);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("BoundsOfCircle")]
    public double Sql_BoundsOfCircle()
    {
        SqlGeometry envelope = SqlGeometry.STGeomFromWKB(sqlFence.STAsBinary(), Srid).STEnvelope();
        return envelope.STPointN(1).STX.Value + envelope.STPointN(3).STY.Value;
    }

    [Benchmark, BenchmarkCategory("BoundsOfCircle")]
    public double Geo_BoundsOfCircle()
    {
        GeoBounds bounds = GeoOperations.Bounds(fence);
        return bounds.MinLongitude + bounds.MaxLatitude;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("IntersectsPointInCircle_Inside")]
    public bool Sql_IntersectsPointInCircle_Inside() => sqlFence.STIntersects(sqlInside).IsTrue;

    [Benchmark, BenchmarkCategory("IntersectsPointInCircle_Inside")]
    public bool Geo_IntersectsPointInCircle_Inside() => GeoOperations.Intersects(fence, inside);

    [Benchmark(Baseline = true), BenchmarkCategory("IntersectsPointInCircle_Outside")]
    public bool Sql_IntersectsPointInCircle_Outside() => sqlFence.STIntersects(sqlOutsideNear).IsTrue;

    [Benchmark, BenchmarkCategory("IntersectsPointInCircle_Outside")]
    public bool Geo_IntersectsPointInCircle_Outside() => GeoOperations.Intersects(fence, outsideNear);

    [Benchmark(Baseline = true), BenchmarkCategory("IntersectsDiscDisc")]
    public bool Sql_IntersectsDiscDisc() => sqlDisc.STIntersects(sqlOtherDisc).IsTrue;

    [Benchmark, BenchmarkCategory("IntersectsDiscDisc")]
    public bool Geo_IntersectsDiscDisc() => GeoOperations.Intersects(disc, otherDisc);

    [Benchmark(Baseline = true), BenchmarkCategory("DistancePointPoint")]
    public double Sql_DistancePointPoint() => sqlInside.STDistance(sqlRoute).Value;

    [Benchmark, BenchmarkCategory("DistancePointPoint")]
    public double Geo_DistancePointPoint() => GeoOperations.Distance(inside, route);

    [Benchmark(Baseline = true), BenchmarkCategory("DistancePointCircle")]
    public double Sql_DistancePointCircle() => sqlFence.STDistance(sqlRoute).Value;

    [Benchmark, BenchmarkCategory("DistancePointCircle")]
    public double Geo_DistancePointCircle() => GeoOperations.Distance(fence, route);

    [Benchmark(Baseline = true), BenchmarkCategory("IntersectionLens")]
    public SqlGeography Sql_IntersectionLens() => sqlDisc.STIntersection(sqlOtherDisc);

    [Benchmark, BenchmarkCategory("IntersectionLens")]
    public GeoShape Geo_IntersectionLens() => GeoOperations.Intersection(disc, otherDisc);

    [Benchmark(Baseline = true), BenchmarkCategory("AreaOfLens")]
    public double Sql_AreaOfLens() => sqlLens.STArea().Value;

    [Benchmark, BenchmarkCategory("AreaOfLens")]
    public double Geo_AreaOfLens() => GeoOperations.Area(lens);

    private static SqlGeography Point(GeoPoint point) => SqlGeography.Point(point.Latitude, point.Longitude, Srid);
}
