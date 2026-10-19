using System.Reactive.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.SqlServer.Types;

namespace RxSpatial.Examples;

/// <summary>
/// Demonstrates — and smoke-tests — RxSpatial composed with System.Reactive v7 on .NET 10:
/// an Rx sequence feeds position updates into an <see cref="RxGeography"/> (which is an
/// <c>IObserver&lt;SqlGeography&gt;</c>), and the spatio-temporal query streams coming back
/// out are consumed through ordinary Rx operators. Configuration comes from
/// Microsoft.Extensions.Configuration (appsettings.json + RXSPATIAL_-prefixed environment
/// variables) bound to <see cref="RxSpatialOptions"/>.
/// </summary>
internal static class Program
{
    // 500 m geofence around the Seattle Space Needle.
    private static readonly SqlGeography Fence =
        SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(500);

    private static int failures;

    private static async Task<int> Main()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables(prefix: "RXSPATIAL_")
            .Build();

        RxSpatialOptions.Default =
            configuration.GetSection(RxSpatialOptions.SectionName).Get<RxSpatialOptions>() ?? new();

        Console.WriteLine($"RxSpatial on .NET {Environment.Version}");
        Console.WriteLine($"System.Reactive {typeof(Observable).Assembly.GetName().Version}");
        Console.WriteLine($"Microsoft.SqlServer.Types {typeof(SqlGeography).Assembly.GetName().Version}");
        Console.WriteLine($"Configured backend: {RxSpatialOptions.Default.PreferredDataStruct}");
        Console.WriteLine();

        await DistanceStreamAsync();
        SpatialFiltering(RxGeographyDataStruct.List, expectedEvents: 2);
        SpatialFiltering(RxGeographyDataStruct.HashMap, expectedEvents: 1);
        SpatialFiltering(RxGeographyDataStruct.RTree, expectedEvents: 1);
        SpatialFiltering(RxGeographyDataStruct.RumTree, expectedEvents: 1);

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "All scenarios passed." : $"{failures} scenario(s) FAILED.");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// A vehicle approaches the geofence. The vehicle is configured from bound configuration
    /// (<see cref="RxSpatialOptions.Default"/>), its position feed is an Rx Interval sequence,
    /// and the continuous distance / intersection queries are composed with Rx operators.
    /// </summary>
    private static async Task DistanceStreamAsync()
    {
        Console.WriteLine("-- Continuous distance query (configured backend, Rx Interval position feed) --");

        RxGeography vehicle = new();
        List<SqlGeography> route =
        [
            SqlGeography.Point(47.7000, -122.4000, 4326),
            SqlGeography.Point(47.6600, -122.3700, 4326),
            SqlGeography.Point(47.6300, -122.3550, 4326),
            SqlGeography.Point(47.6205, -122.3493, 4326), // inside the fence
        ];

        List<double> distances = [];
        List<bool> intersections = [];
        List<SqlGeography> positions = [];

        using IDisposable distanceSub = vehicle.DistanceTo(Fence)
                                               .DistinctUntilChanged()
                                               .Subscribe(distances.Add);
        using IDisposable intersectSub = vehicle.Intersects(Fence)
                                                .DistinctUntilChanged()
                                                .Subscribe(intersections.Add);
        using IDisposable locationSub = vehicle.Locations.Subscribe(positions.Add);

        await Observable.Interval(TimeSpan.FromMilliseconds(50))
                        .Take(route.Count)
                        .Do(t => vehicle.OnNext(route[(int)t]))
                        .LastOrDefaultAsync();

        foreach (double metres in distances)
            Console.WriteLine($"   distance to fence: {metres,10:F1} m");
        Console.WriteLine($"   entered fence: {intersections.LastOrDefault()}");

        Check(distances.Count == route.Count, "one distance per position update");
        Check(distances.SequenceEqual(distances.OrderByDescending(d => d)), "distances shrink while approaching");
        Check(distances[^1] == 0, "distance is zero inside the fence");
        Check(intersections.SequenceEqual([false, true]), "intersection stream flips false -> true");
        Check(positions.Count == route.Count, "raw location stream forwards every update");
    }

    /// <summary>
    /// The same intersection query on every index backend, selected through typed
    /// <see cref="RxSpatialOptions"/>. The grid and tree backends prune updates outside the
    /// observer's zone, so the vehicle's first, far-away ping produces no notification at
    /// all — only the plain list backend evaluates every update.
    /// </summary>
    private static void SpatialFiltering(RxGeographyDataStruct backend, int expectedEvents)
    {
        Console.WriteLine($"-- Intersection query, '{backend}' backend --");

        RxGeography vehicle = new(new RxSpatialOptions
        {
            PreferredDataStruct = backend,
            IsVisualized = true,
        });
        List<bool> events = [];
        using IDisposable subscription = vehicle.Intersects(Fence).Subscribe(events.Add);

        // Far away from the fence (a different grid zone, outside the fence's bounding box),
        // then right at its centre. The tree backends index bounding boxes, so the vehicle
        // reports a small buffered region rather than a bare point — exactly like the
        // original load-test traffic generator did.
        vehicle.OnNext(SqlGeography.Point(46.2000, -123.5000, 4326).STBuffer(50));
        vehicle.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(50));

        Console.WriteLine($"   notifications: [{string.Join(", ", events)}]");
        Check(events.Count == expectedEvents, $"{expectedEvents} notification(s) on '{backend}' backend");
        Check(events.LastOrDefault(), "vehicle detected inside the fence");
    }

    private static void Check(bool condition, string description)
    {
        if (condition)
        {
            Console.WriteLine($"   [pass] {description}");
        }
        else
        {
            failures++;
            Console.WriteLine($"   [FAIL] {description}");
        }
    }
}
