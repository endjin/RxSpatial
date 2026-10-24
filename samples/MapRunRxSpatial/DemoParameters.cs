// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

using System.IO;

namespace MapRunRxSpatial;

/// <summary>The continuous-query scenario driven by the active tab.</summary>
internal enum Scenario
{
    Intersect,
    Distance,
    RxRxDistance,
    RxRxDistanceConnection,
    RxRxIntersection,
}

/// <summary>Mutable demo state shared between the control panel and the traffic engine.</summary>
internal static class DemoParameters
{
    /// <summary>Folder holding the recorded vehicle trajectories (0.txt .. 19.txt).</summary>
    public static readonly string TrajectoryPath = Path.Combine(AppContext.BaseDirectory, "trafficData");

    /// <summary>Every figure in the papers replays the whole fleet of 20 vehicles.</summary>
    public const int DefaultVehicleCount = 20;

    /// <summary>
    /// Updates per second on every tab. Not taken from the papers: their screenshots show
    /// speeds of 1, 48 and 56 on a differently scaled slider, so this is a readable middle value.
    /// </summary>
    public const int DefaultReplayRate = 25;

    public static int MovementIntervalMilliseconds { get; set; } = ReplayIntervalMilliseconds(DefaultReplayRate);

    public static int VehicleCount { get; set; } = DefaultVehicleCount;

    public static double ObserveDistance { get; set; } = DefaultObserveDistance(Scenario.Intersect);

    /// <summary>
    /// The Overlap tab's reach, in metres: there each vehicle pushes the disc of this radius
    /// around its position instead of the bare point. On the recorded trajectories, 2,000 m
    /// makes the pre-set pairs overlap for different shares of the replay: 15-16 always, 10-12
    /// 90% of the time, 7-9 24% and 17-1 16%, with about 11 of the 190 pairs overlapping at once.
    /// </summary>
    public const double DefaultReach = 2000;

    public static double Reach { get; set; } = DefaultReach;

    public static Scenario ActiveScenario { get; set; } = Scenario.Intersect;

    public static int ReplayIntervalMilliseconds(int updatesPerSecond) => 1000 / updatesPerSecond;

    /// <summary>
    /// The observe distance a tab starts with, in metres. The Geofence and Overlap tabs have
    /// no distance; they return the Near area value so the shared state is never meaningless.
    /// </summary>
    public static double DefaultObserveDistance(Scenario scenario) => scenario switch
    {
        // DEBS 2016 Fig. 4 (family locator): 4,610.93 m, rounded.
        Scenario.Intersect or Scenario.Distance or Scenario.RxRxIntersection => 4600,

        // SIGMOD 2016 poster, RxRxDist tab: 5,065.43 m, rounded.
        Scenario.RxRxDistance => 5000,

        // ICDE Fig. 5 / DEBS Fig. 3 show 9,935 m, but on this data pair 4-18 is gray only for
        // the first ticks at that distance. 8,000 m keeps 4-18 mostly gray, 10-12 mostly
        // yellow and 7-9 mostly green, as the figure describes.
        Scenario.RxRxDistanceConnection => 8000,
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown scenario."),
    };

    /// <summary>The vehicle pairs a tab watches from Start, as in the papers' figures.</summary>
    public static IReadOnlyList<VehiclePair> DefaultPairs(Scenario scenario) => scenario switch
    {
        // ICDE Fig. 4c: offenders 10-12 and 15-16 are close enough to meet.
        Scenario.RxRxDistance => [VehiclePair.Of(10, 12), VehiclePair.Of(15, 16)],

        // ICDE Fig. 5 / DEBS Fig. 3: 4-18 beyond the limit, 10-12 stable, 7-9 near the limit.
        Scenario.RxRxDistanceConnection => [VehiclePair.Of(4, 18), VehiclePair.Of(10, 12), VehiclePair.Of(7, 9)],

        // DEBS 2016 section 3.3 (car pooling, Fig. 3): 15-16 could car pool, and 7-9, 10-12
        // and 17-1 are tracked. The figure's fifth pair, 4-18, is left out: those vehicles stay
        // more than 6 km apart, so their reaches never overlap at a reach the slider offers.
        Scenario.RxRxIntersection => [VehiclePair.Of(15, 16), VehiclePair.Of(10, 12), VehiclePair.Of(7, 9), VehiclePair.Of(17, 1)],
        _ => [],
    };
}
