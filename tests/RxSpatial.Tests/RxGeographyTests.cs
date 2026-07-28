using System.Reactive.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace RxSpatial.Tests;

/// <summary>
/// Behavioural tests for <see cref="RxGeography"/> and its Rx.NET v7 bridge. Settings are
/// passed explicitly as <see cref="RxSpatialOptions"/>; only the vendored RUM-tree garbage
/// cleaner still reads process-global AppSettings, so the suite stays on MSTest's default
/// sequential execution.
/// </summary>
[TestClass]
public class RxGeographyTests
{
    // 500 m geofence around the Seattle Space Needle.
    private static readonly SqlGeography Fence =
        SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(500);

    [TestInitialize]
    public void Initialize()
        => System.Configuration.ConfigurationManager.AppSettings["RUMTreeGCInspectionInterval"] = "5";

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
