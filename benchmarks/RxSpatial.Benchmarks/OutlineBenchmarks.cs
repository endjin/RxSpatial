// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using BenchmarkDotNet.Attributes;
using RxSpatial.Geo;

namespace RxSpatial.Benchmarks;

/// <summary>
/// <see cref="GeoOperations.ToVertices(GeoShape, int)"/> and its planar twin: the WPF demo outlines every
/// vehicle's reach circle with 64 segments on every location update, and 128 is the default
/// (SQL Server's <c>STBuffer</c> count). A geodesic circle costs one Vincenty direct solution
/// per vertex; a polygon's outline is a copy of its vertices. The <c>_IntoSpan</c> rows write into a
/// reused buffer instead of allocating.
/// </summary>
[MemoryDiagnoser]
public class OutlineBenchmarks
{
    private static readonly GeoPoint Needle = new(47.6205, -122.3493);

    private readonly GeoCircle reach = new(Needle, 2_000);
    private readonly GeoPolygon lens = (GeoPolygon)GeoOperations.Intersection(
        new GeoCircle(Needle, 2_000), new GeoCircle(Wgs84.Direct(Needle, 63, 1_500), 2_000));

    private readonly PlanarCircle pallet = new(new(500, 500), 50);
    private readonly GeoPoint[] outline = new GeoPoint[128];
    private readonly PlanarPoint[] planarOutline = new PlanarPoint[128];

    [Params(64, 128)]
    public int Segments;

    [Benchmark]
    public GeoPoint[] GeoCircleOutline() => GeoOperations.ToVertices(reach, Segments);

    [Benchmark]
    public GeoPoint[] GeoLensOutline() => GeoOperations.ToVertices(lens, Segments);

    [Benchmark]
    public PlanarPoint[] PlanarCircleOutline() => PlanarOperations.ToVertices(pallet, Segments);

    [Benchmark]
    public int GeoCircleOutline_IntoSpan() => GeoOperations.ToVertices(reach, outline, Segments);

    [Benchmark]
    public int PlanarCircleOutline_IntoSpan() => PlanarOperations.ToVertices(pallet, planarOutline, Segments);
}
