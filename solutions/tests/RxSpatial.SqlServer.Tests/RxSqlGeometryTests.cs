// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using System.Reactive.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace RxSpatial.SqlServer.Tests;

/// <summary>
/// Behavioural tests for <see cref="RxSqlGeometry"/>, the planar moving object, and its Rx.NET v7
/// bridge: a forklift on a warehouse floor (SRID 0, metres) approaching a loading bay.
/// </summary>
[TestClass]
[TestCategory("SqlServer")]
[OSCondition(OperatingSystems.Windows)]
public class RxSqlGeometryTests
{
    // A rectangle rather than a buffered point, so every distance to it is exact.
    private static readonly SqlGeometry Bay =
        SqlGeometry.Parse("POLYGON ((450 450, 550 450, 550 550, 450 550, 450 450))");

    [TestMethod]
    public void DistanceStream_DecreasesToZero_WhileApproachingBay()
    {
        RxSqlGeometry forklift = new(VisualizedOptions());
        List<double> distances = [];
        using IDisposable subscription = forklift.DistanceTo(Bay).Subscribe(distances.Add);

        foreach (SqlGeometry position in Route())
            forklift.OnNext(position);

        distances.ShouldBe([350, 150, 50, 0]);
    }

    [TestMethod]
    public void IntersectionStream_FlipsFalseToTrue_WhenEnteringBay()
    {
        RxSqlGeometry forklift = new(VisualizedOptions());
        List<bool> events = [];
        using IDisposable subscription = forklift.Intersects(Bay)
                                                 .DistinctUntilChanged()
                                                 .Subscribe(events.Add);

        foreach (SqlGeometry position in Route())
            forklift.OnNext(position);

        events.ShouldBe([false, true]);
    }

    [TestMethod]
    public void Locations_ForwardsEveryUpdate_InVisualizedMode()
    {
        RxSqlGeometry forklift = new(VisualizedOptions());
        List<SqlGeometry> positions = [];
        using IDisposable subscription = forklift.Locations.Subscribe(positions.Add);

        foreach (SqlGeometry position in Route())
            forklift.OnNext(position);

        positions.Count.ShouldBe(4);
    }

    [TestMethod]
    [DataRow(RxGeographyDataStruct.List, 2)]
    [DataRow(RxGeographyDataStruct.HashMap, 1)]
    [DataRow(RxGeographyDataStruct.RTree, 1)]
    [DataRow(RxGeographyDataStruct.RumTree, 1)]
    public void SpatialBackends_PruneUpdatesOutsideTheObserverZone(
        RxGeographyDataStruct backend, int expectedEvents)
    {
        RxSqlGeometry forklift = new(VisualizedOptions(backend));
        List<bool> events = [];
        using IDisposable subscription = forklift.Intersects(Bay).Subscribe(events.Add);

        // Far from the bay (a different grid cell, outside the bay's bounding box), then at its
        // centre, as small buffered regions like the geographic tests push.
        forklift.OnNext(Point(50, 50).STBuffer(5));
        forklift.OnNext(Point(500, 500).STBuffer(5));

        events.Count.ShouldBe(expectedEvents);
        events[^1].ShouldBeTrue();
    }

    [TestMethod]
    [DataRow(RxGeographyDataStruct.HashMap)]
    [DataRow(RxGeographyDataStruct.RTree)]
    [DataRow(RxGeographyDataStruct.RumTree)]
    public void SpatialBackends_HandleBarePointPushes(RxGeographyDataStruct backend)
    {
        RxSqlGeometry forklift = new(VisualizedOptions(backend));
        List<bool> events = [];
        using IDisposable subscription = forklift.Intersects(Bay).Subscribe(events.Add);

        forklift.OnNext(Point(50, 50));
        forklift.OnNext(Point(500, 500));

        events.ShouldBe([true]);
    }

    [TestMethod]
    public void RxRxDistanceStream_TracksTwoMovingObjects()
    {
        RxSqlGeometry a = new(VisualizedOptions());
        RxSqlGeometry b = new(VisualizedOptions());
        b.OnNext(Point(0, 0));

        List<double> distances = [];
        using IDisposable subscription = a.DistanceTo(b).Subscribe(distances.Add);

        a.OnNext(Point(300, 400));
        a.OnNext(Point(0, 0));

        distances.ShouldBe([500, 0]);
    }

    [TestMethod]
    public void ParameterlessConstructor_UsesAmbientDefaultOptions()
    {
        RxSpatialOptions saved = RxSpatialOptions.Default;
        try
        {
            RxSpatialOptions.Default = VisualizedOptions();
            RxSqlGeometry forklift = new();
            List<bool> events = [];
            using IDisposable subscription = forklift.Intersects(Bay).Subscribe(events.Add);
            forklift.OnNext(Point(500, 500));
            events.ShouldBe([true]);
        }
        finally
        {
            RxSpatialOptions.Default = saved;
        }
    }

    [TestMethod]
    public void IntersectionWithStationaryGeometry_StreamsIntersectionGeometry()
    {
        RxSqlGeometry pallet = new(VisualizedOptions());
        List<SqlGeometry> intersections = [];
        using IDisposable subscription = pallet.IntersectionWith(Bay).Subscribe(intersections.Add);

        pallet.OnNext(Point(50, 50).STBuffer(5));
        pallet.OnNext(Rectangle(400, 450, 500, 550));

        intersections.Count.ShouldBe(2);
        intersections[0].STIsEmpty().Value.ShouldBeTrue();
        intersections[1].STArea().Value.ShouldBe(5000);
    }

    [TestMethod]
    public void IntersectionWithMovingObject_StreamsIntersectionGeometry()
    {
        RxSqlGeometry a = new(VisualizedOptions());
        RxSqlGeometry b = new(VisualizedOptions());
        b.OnNext(Point(500, 500).STBuffer(20));

        List<SqlGeometry> intersections = [];
        using IDisposable subscription = a.IntersectionWith(b).Subscribe(intersections.Add);

        a.OnNext(Point(100, 100).STBuffer(20));
        a.OnNext(Point(510, 500).STBuffer(20));

        intersections.Count.ShouldBe(2);
        intersections[0].STIsEmpty().Value.ShouldBeTrue();
        intersections[1].STIsEmpty().Value.ShouldBeFalse();
    }

    [TestMethod]
    public void IntersectsMovingObject_StreamsBooleanTests()
    {
        RxSqlGeometry a = new(VisualizedOptions());
        RxSqlGeometry b = new(VisualizedOptions());
        b.OnNext(Point(500, 500).STBuffer(20));

        List<bool> events = [];
        using IDisposable subscription = a.Intersects(b).Subscribe(events.Add);

        a.OnNext(Point(100, 100).STBuffer(20));
        a.OnNext(Point(510, 500).STBuffer(20));

        events.ShouldBe([false, true]);
    }

    [TestMethod]
    [DataRow(RxGeographyDataStruct.List)]
    [DataRow(RxGeographyDataStruct.HashMap)]
    [DataRow(RxGeographyDataStruct.RTree)]
    [DataRow(RxGeographyDataStruct.RumTree)]
    public void RxRxDistance_WorksOnEveryBackend_TrackingTheMovingObserver(RxGeographyDataStruct backend)
    {
        // The moving observer is registered in A's index at B's location and follows B's
        // location stream through the backend's location tracker.
        RxSqlGeometry a = new(VisualizedOptions(backend));
        RxSqlGeometry b = new(VisualizedOptions(backend));
        b.OnNext(Point(500, 500).STBuffer(50));

        List<double> distances = [];
        using IDisposable subscription = a.DistanceTo(b).Subscribe(distances.Add);

        // Move B (exercises the location tracker), then push A inside B's zone.
        b.OnNext(Point(650, 650).STBuffer(50));
        a.OnNext(Point(650, 650).STBuffer(50));

        distances.Count.ShouldBeGreaterThanOrEqualTo(1);
        distances[^1].ShouldBe(0);
    }

    [TestMethod]
    [DataRow(RxGeographyDataStruct.HashMap)]
    [DataRow(RxGeographyDataStruct.RTree)]
    [DataRow(RxGeographyDataStruct.RumTree)]
    public void Unsubscribe_StopsNotifications_OnSpatialBackends(RxGeographyDataStruct backend)
    {
        RxSqlGeometry forklift = new(VisualizedOptions(backend));
        List<bool> events = [];
        IDisposable subscription = forklift.Intersects(Bay).Subscribe(events.Add);

        forklift.OnNext(Point(500, 500).STBuffer(5));
        subscription.Dispose();
        forklift.OnNext(Point(500, 500).STBuffer(5));

        events.Count.ShouldBe(1);
    }

    [TestMethod]
    public void BufferedMode_ThrowsOverloadException_WhenQueueLimitExceeded()
    {
        RxSqlGeometry forklift = new(new RxSpatialOptions { IsVisualized = false, QueueLimit = 0 });

        Should.Throw<RxSpatialOverloadException>(() => forklift.OnNext(Point(500, 500)));
    }

    [TestMethod]
    public void Unsubscribe_StopsNotifications()
    {
        RxSqlGeometry forklift = new(VisualizedOptions());
        List<bool> events = [];
        IDisposable subscription = forklift.Intersects(Bay).Subscribe(events.Add);

        forklift.OnNext(Point(500, 500).STBuffer(5));
        subscription.Dispose();
        forklift.OnNext(Point(500, 500).STBuffer(5));

        events.Count.ShouldBe(1);
    }

    [TestMethod]
    public void PlanarGridOptions_BindFromConfigurationSection()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RxSpatial:PlanarGrid:XCells"] = "20",
                ["RxSpatial:PlanarGrid:CoverageRegion:MaxX"] = "2000",
            })
            .Build();

        RxSpatialOptions options =
            configuration.GetSection(RxSpatialOptions.SectionName).Get<RxSpatialOptions>()!;

        options.PlanarGrid.XCells.ShouldBe(20);
        options.PlanarGrid.YCells.ShouldBe(10);
        options.PlanarGrid.CoverageRegion.MaxX.ShouldBe(2000);
        options.PlanarGrid.CoverageRegion.MinX.ShouldBe(0);
    }

    [TestMethod]
    public void PlanarGrid_UsesItsOwnCoverageRegion_NotTheGeographicOne()
    {
        // A 10 m planar grid. Measured on the default lat/long Grid (46..49, -124..-120) both
        // pushes would clamp into its top-right cell, beside the bay, and notify twice.
        RxSpatialOptions options = VisualizedOptions(RxGeographyDataStruct.HashMap);
        options.PlanarGrid = new PlanarGridOptions
        {
            XCells = 10,
            YCells = 10,
            CoverageRegion = new PlanarCoverageRegionOptions { MinX = 0, MinY = 0, MaxX = 100, MaxY = 100 },
        };
        RxSqlGeometry forklift = new(options);
        List<bool> events = [];
        using IDisposable subscription =
            forklift.Intersects(Rectangle(95, 95, 100, 100)).Subscribe(events.Add);

        forklift.OnNext(Point(5, 95));
        forklift.OnNext(Point(97, 97));

        events.ShouldBe([true]);
    }

    private static RxSpatialOptions VisualizedOptions(
        RxGeographyDataStruct backend = RxGeographyDataStruct.List)
        => new()
        {
            PreferredDataStruct = backend,
            IsVisualized = true,
            QueueLimit = 64,
        };

    private static SqlGeometry Point(double x, double y) => SqlGeometry.Point(x, y, 0);

    private static SqlGeometry Rectangle(double minX, double minY, double maxX, double maxY)
        => SqlGeometry.Parse(FormattableString.Invariant(
            $"POLYGON (({minX} {minY}, {maxX} {minY}, {maxX} {maxY}, {minX} {maxY}, {minX} {minY}))"));

    private static IEnumerable<SqlGeometry> Route()
    {
        yield return Point(100, 500);
        yield return Point(300, 500);
        yield return Point(400, 500);
        yield return Point(500, 500); // inside the bay
    }
}
