// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using BenchmarkDotNet.Attributes;
using RxSpatial.Geo;

namespace RxSpatial.Benchmarks;

/// <summary>
/// The per-operation cost of the clean-room shape maths (RxSpatial.Geo) on Seattle-scale
/// shapes: the 500 m Space Needle fence, 2 km discs, the 3 km × 2 km test zone. Intersects,
/// Distance, Area and Bounds must not allocate; Intersection builds a new shape.
/// </summary>
[MemoryDiagnoser]
public class GeoOperationsBenchmarks
{
    private static readonly GeoPoint Needle = new(47.6205, -122.3493);

    private readonly GeoCircle fence = new(Needle, 500);
    private readonly GeoPointShape inside = new(47.6230, -122.3480);
    private readonly GeoPointShape nearBoundary = new(Wgs84.Direct(Needle, 22.5, 500.1));
    private readonly GeoPointShape outside = new(47.7000, -122.4000);
    private readonly GeoPointShape route = new(47.6300, -122.3550);
    private readonly GeoPointShape inZone = new(47.6090, -122.3500);
    private readonly GeoCircle disc = new(Needle, 2_000);
    private readonly GeoCircle otherDisc = new(Wgs84.Direct(Needle, 63, 1_500), 2_000);
    private readonly GeoPolygon zone = GeoPolygon.Rectangle(47.600, -122.370, 47.618, -122.330);
    private readonly GeoPolygon overlappingZone = GeoPolygon.Rectangle(47.610, -122.350, 47.630, -122.300);
    private readonly PlanarPolygon bay = PlanarPolygon.Rectangle(450, 450, 550, 550);
    private readonly PlanarPointShape forklift = new(300, 500);
    private readonly PlanarCircle pallet = new(new(500, 500), 50);

    [Benchmark]
    public GeoPointShape ConstructPoint() => new(route.Position);

    [Benchmark]
    public GeoCircle ConstructCircle() => new(disc.Center, disc.RadiusMetres);

    [Benchmark]
    public GeoBounds BoundsOfCircle() => GeoOperations.Bounds(fence);

    [Benchmark]
    public GeoBounds BoundsOfPolygon() => GeoOperations.Bounds(zone);

    [Benchmark]
    public GeoBounds BoundsOfPoint() => GeoOperations.Bounds(route);

    [Benchmark]
    public bool IntersectsPointInCircle_Guarded() => GeoOperations.Intersects(fence, inside);

    [Benchmark]
    public bool IntersectsPointInCircle_NearBoundary() => GeoOperations.Intersects(fence, nearBoundary);

    [Benchmark]
    public bool IntersectsPointInCircle_BoundsReject() => GeoOperations.Intersects(fence, outside);

    [Benchmark]
    public bool IntersectsPointInPolygon_Inside() => GeoOperations.Intersects(zone, inZone);

    [Benchmark]
    public bool IntersectsPointInPolygon_BoundsReject() => GeoOperations.Intersects(zone, route);

    [Benchmark]
    public bool IntersectsCircleCircle() => GeoOperations.Intersects(disc, otherDisc);

    [Benchmark]
    public bool IntersectsCirclePolygon() => GeoOperations.Intersects(disc, zone);

    [Benchmark]
    public bool IntersectsPolygonPolygon() => GeoOperations.Intersects(zone, overlappingZone);

    [Benchmark]
    public double DistancePointPoint() => GeoOperations.Distance(inside, route);

    [Benchmark]
    public double DistancePointCircle() => GeoOperations.Distance(fence, outside);

    [Benchmark]
    public double DistancePointPolygon() => GeoOperations.Distance(zone, outside);

    [Benchmark]
    public double DistanceCirclePolygon() => GeoOperations.Distance(fence, zone);

    [Benchmark]
    public GeoShape IntersectionLens() => GeoOperations.Intersection(disc, otherDisc);

    [Benchmark]
    public double AreaOfLens() => GeoOperations.Area(GeoOperations.Intersection(disc, otherDisc));

    [Benchmark]
    public GeoShape IntersectionCirclePolygon() => GeoOperations.Intersection(disc, zone);

    [Benchmark]
    public GeoShape IntersectionPolygonPolygon() => GeoOperations.Intersection(zone, overlappingZone);

    [Benchmark]
    public double PlanarDistancePointPolygon() => PlanarOperations.Distance(bay, forklift);

    [Benchmark]
    public bool PlanarIntersectsCirclePolygon() => PlanarOperations.Intersects(bay, pallet);
}
