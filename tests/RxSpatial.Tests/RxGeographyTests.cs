using System.Reactive.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace RxSpatial.Tests;

/// <summary>
/// Behavioural tests for <see cref="RxGeography"/> and its Rx.NET v7 bridge. Settings are
/// passed explicitly as <see cref="RxSpatialOptions"/>.
/// </summary>
[TestClass]
public class RxGeographyTests
{
    // 500 m geofence around the Seattle Space Needle.
    private static readonly SqlGeography Fence =
        SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(500);

    [TestMethod]
    public void DistanceStream_DecreasesToZero_WhileApproachingFence()
    {
        RxGeography vehicle = new(VisualizedOptions());
        List<double> distances = [];
        using IDisposable subscription = vehicle.DistanceTo(Fence)
                                                .Select(d => (double)d)
                                                .Subscribe(distances.Add);

        foreach (SqlGeography position in Route())
            vehicle.OnNext(position);

        distances.Count.ShouldBe(4);
        distances.ShouldBe([.. distances.OrderByDescending(d => d)]);
        distances[^1].ShouldBe(0);
    }

    [TestMethod]
    public void IntersectionStream_FlipsFalseToTrue_WhenEnteringFence()
    {
        RxGeography vehicle = new(VisualizedOptions());
        List<bool> events = [];
        using IDisposable subscription = vehicle.Intersects(Fence)
                                                .DistinctUntilChanged()
                                                .Subscribe(events.Add);

        foreach (SqlGeography position in Route())
            vehicle.OnNext(position);

        events.ShouldBe([false, true]);
    }

    [TestMethod]
    public void Locations_ForwardsEveryUpdate_InVisualizedMode()
    {
        RxGeography vehicle = new(VisualizedOptions());
        List<SqlGeography> positions = [];
        using IDisposable subscription = vehicle.Locations.Subscribe(positions.Add);

        foreach (SqlGeography position in Route())
            vehicle.OnNext(position);

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
        RxGeography vehicle = new(VisualizedOptions(backend));
        List<bool> events = [];
        using IDisposable subscription = vehicle.Intersects(Fence).Subscribe(events.Add);

        // Far away from the fence (a different grid zone, outside the fence's bounding box),
        // then right at its centre. The tree backends index bounding boxes, so the vehicle
        // reports a small buffered region rather than a bare point.
        vehicle.OnNext(SqlGeography.Point(46.2000, -123.5000, 4326).STBuffer(50));
        vehicle.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(50));

        events.Count.ShouldBe(expectedEvents);
        events[^1].ShouldBeTrue();
    }

    [TestMethod]
    [DataRow(RxGeographyDataStruct.HashMap)]
    [DataRow(RxGeographyDataStruct.RTree)]
    [DataRow(RxGeographyDataStruct.RumTree)]
    public void SpatialBackends_HandleBarePointPushes(RxGeographyDataStruct backend)
    {
        // The map demo pushes raw points; the old tree ports crashed on point envelopes.
        RxGeography vehicle = new(VisualizedOptions(backend));
        List<bool> events = [];
        using IDisposable subscription = vehicle.Intersects(Fence).Subscribe(events.Add);

        vehicle.OnNext(SqlGeography.Point(46.2000, -123.5000, 4326));
        vehicle.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326));

        events.ShouldBe([true]);
    }

    [TestMethod]
    public void RxRxDistanceStream_TracksTwoMovingObjects()
    {
        RxGeography a = new(VisualizedOptions());
        RxGeography b = new(VisualizedOptions());
        b.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326));

        List<double> distances = [];
        using IDisposable subscription = a.DistanceTo(b)
                                          .Select(d => (double)d)
                                          .Subscribe(distances.Add);

        a.OnNext(SqlGeography.Point(47.7000, -122.4000, 4326));
        a.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326));

        distances.Count.ShouldBe(2);
        distances[0].ShouldBeGreaterThan(0);
        distances[^1].ShouldBe(0);
    }

    [TestMethod]
    public void ParameterlessConstructor_UsesAmbientDefaultOptions()
    {
        RxSpatialOptions saved = RxSpatialOptions.Default;
        try
        {
            RxSpatialOptions.Default = VisualizedOptions();
            RxGeography vehicle = new();
            List<bool> events = [];
            using IDisposable subscription = vehicle.Intersects(Fence).Subscribe(events.Add);
            vehicle.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326));
            events.ShouldBe([true]);
        }
        finally
        {
            RxSpatialOptions.Default = saved;
        }
    }

    [TestMethod]
    public void IntersectionWithStationaryGeography_StreamsIntersectionGeometry()
    {
        RxGeography vehicle = new(VisualizedOptions());
        List<SqlGeography> intersections = [];
        using IDisposable subscription = vehicle.IntersectionWith(Fence).Subscribe(intersections.Add);

        vehicle.OnNext(SqlGeography.Point(47.7000, -122.4000, 4326).STBuffer(100));
        vehicle.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(100));

        intersections.Count.ShouldBe(2);
        intersections[0].STIsEmpty().Value.ShouldBeTrue();
        intersections[1].STIsEmpty().Value.ShouldBeFalse();
    }

    [TestMethod]
    public void IntersectionWithMovingObject_StreamsIntersectionGeometry()
    {
        RxGeography a = new(VisualizedOptions());
        RxGeography b = new(VisualizedOptions());
        b.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(200));

        List<SqlGeography> intersections = [];
        using IDisposable subscription = a.IntersectionWith(b).Subscribe(intersections.Add);

        a.OnNext(SqlGeography.Point(47.7000, -122.4000, 4326).STBuffer(200));
        a.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(200));

        intersections.Count.ShouldBe(2);
        intersections[0].STIsEmpty().Value.ShouldBeTrue();
        intersections[1].STIsEmpty().Value.ShouldBeFalse();
    }

    [TestMethod]
    public void IntersectsMovingObject_StreamsBooleanTests()
    {
        RxGeography a = new(VisualizedOptions());
        RxGeography b = new(VisualizedOptions());
        b.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(200));

        List<bool> events = [];
        using IDisposable subscription = a.Intersects(b).Subscribe(events.Add);

        a.OnNext(SqlGeography.Point(47.7000, -122.4000, 4326).STBuffer(200));
        a.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(200));

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
        // location stream (the Tree/Grid LocationObsvr trackers) — with buffered regions so
        // the tree backends can index the observer's zone.
        RxGeography a = new(VisualizedOptions(backend));
        RxGeography b = new(VisualizedOptions(backend));
        b.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(500));

        List<double> distances = [];
        using IDisposable subscription = a.DistanceTo(b)
                                          .Select(d => (double)d)
                                          .Subscribe(distances.Add);

        // Move B (exercises the location-tracking observer), then push A inside B's zone.
        b.OnNext(SqlGeography.Point(47.6300, -122.3550, 4326).STBuffer(500));
        a.OnNext(SqlGeography.Point(47.6300, -122.3550, 4326).STBuffer(500));

        distances.Count.ShouldBeGreaterThanOrEqualTo(1);
        distances[^1].ShouldBe(0);
    }

    [TestMethod]
    [DataRow(RxGeographyDataStruct.HashMap)]
    [DataRow(RxGeographyDataStruct.RTree)]
    [DataRow(RxGeographyDataStruct.RumTree)]
    public void Unsubscribe_StopsNotifications_OnSpatialBackends(RxGeographyDataStruct backend)
    {
        RxGeography vehicle = new(VisualizedOptions(backend));
        List<bool> events = [];
        IDisposable subscription = vehicle.Intersects(Fence).Subscribe(events.Add);

        vehicle.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(50));
        subscription.Dispose();
        vehicle.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(50));

        events.Count.ShouldBe(1);
    }

    [TestMethod]
    public void BufferedMode_ThrowsOverloadException_WhenQueueLimitExceeded()
    {
        RxGeography vehicle = new(new RxSpatialOptions { IsVisualized = false, QueueLimit = 0 });

        Should.Throw<RxSpatialOverLoadException>(
            () => vehicle.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326)));
    }

    [TestMethod]
    public void Unsubscribe_StopsNotifications()
    {
        RxGeography vehicle = new(VisualizedOptions());
        List<bool> events = [];
        IDisposable subscription = vehicle.Intersects(Fence).Subscribe(events.Add);

        vehicle.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(50));
        subscription.Dispose();
        vehicle.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(50));

        events.Count.ShouldBe(1);
    }

    [TestMethod]
    public void RxSpatialOptions_BindFromConfigurationSection()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Historic (differently-cased) setting values bind case-insensitively.
                ["RxSpatial:PreferredDataStruct"] = "RUMTREE",
                ["RxSpatial:QueueLimit"] = "7",
                ["RxSpatial:IsVisualized"] = "true",
                ["RxSpatial:Grid:LatitudeCells"] = "20",
                ["RxSpatial:Grid:CoverageRegion:LowerLeftLatitude"] = "45.5",
            })
            .Build();

        RxSpatialOptions options =
            configuration.GetSection(RxSpatialOptions.SectionName).Get<RxSpatialOptions>()!;

        options.PreferredDataStruct.ShouldBe(RxGeographyDataStruct.RumTree);
        options.QueueLimit.ShouldBe(7);
        options.IsVisualized.ShouldBeTrue();
        options.Grid.LatitudeCells.ShouldBe(20);
        options.Grid.LongitudeCells.ShouldBe(10);
        options.Grid.CoverageRegion.LowerLeftLatitude.ShouldBe(45.5);
    }

    private static RxSpatialOptions VisualizedOptions(
        RxGeographyDataStruct backend = RxGeographyDataStruct.List)
        => new()
        {
            PreferredDataStruct = backend,
            IsVisualized = true,
            QueueLimit = 64,
        };

    private static IEnumerable<SqlGeography> Route()
    {
        yield return SqlGeography.Point(47.7000, -122.4000, 4326);
        yield return SqlGeography.Point(47.6600, -122.3700, 4326);
        yield return SqlGeography.Point(47.6300, -122.3550, 4326);
        yield return SqlGeography.Point(47.6205, -122.3493, 4326); // inside the fence
    }
}
