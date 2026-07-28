using System.Configuration;
using System.Reactive.Linq;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace RxSpatial.Tests;

/// <summary>
/// Behavioural tests for <see cref="RxGeography"/> and its Rx.NET v7 bridge. RxSpatial reads
/// its configuration from the process-global AppSettings, so these tests rely on MSTest's
/// default sequential execution — do not add <c>[assembly: Parallelize]</c>.
/// </summary>
[TestClass]
public class RxGeographyTests
{
    // 500 m geofence around the Seattle Space Needle.
    private static readonly SqlGeography Fence =
        SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(500);

    [TestInitialize]
    public void Initialize()
    {
        ConfigurationManager.AppSettings["isVisualized"] = "true";
        ConfigurationManager.AppSettings["preferredDataStruct"] = "list";
        ConfigurationManager.AppSettings["HashMapGridNum"] = "10,10";
        ConfigurationManager.AppSettings["HashMapCoverageRegion"] = "46,-124,49,-120";
        ConfigurationManager.AppSettings["RUMTreeGCInspectionInterval"] = "5";
    }

    [TestMethod]
    public void DistanceStream_DecreasesToZero_WhileApproachingFence()
    {
        RxGeography vehicle = new();
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
        RxGeography vehicle = new();
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
        RxGeography vehicle = new();
        List<SqlGeography> positions = [];
        using IDisposable subscription = vehicle.Locations.Subscribe(positions.Add);

        foreach (SqlGeography position in Route())
            vehicle.OnNext(position);

        positions.Count.ShouldBe(4);
    }

    [TestMethod]
    [DataRow("list", 2)]
    [DataRow("hashmap", 1)]
    [DataRow("RTREE", 1)]
    [DataRow("RUMTREE", 1)]
    public void SpatialBackends_PruneUpdatesOutsideTheObserverZone(string backend, int expectedEvents)
    {
        ConfigurationManager.AppSettings["preferredDataStruct"] = backend;

        RxGeography vehicle = new();
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
    public void RxRxDistanceStream_TracksTwoMovingObjects()
    {
        RxGeography a = new();
        RxGeography b = new();
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
    public void Unsubscribe_StopsNotifications()
    {
        RxGeography vehicle = new();
        List<bool> events = [];
        IDisposable subscription = vehicle.Intersects(Fence).Subscribe(events.Add);

        vehicle.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(50));
        subscription.Dispose();
        vehicle.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(50));

        events.Count.ShouldBe(1);
    }

    private static IEnumerable<SqlGeography> Route()
    {
        yield return SqlGeography.Point(47.7000, -122.4000, 4326);
        yield return SqlGeography.Point(47.6600, -122.3700, 4326);
        yield return SqlGeography.Point(47.6300, -122.3550, 4326);
        yield return SqlGeography.Point(47.6205, -122.3493, 4326); // inside the fence
    }
}
