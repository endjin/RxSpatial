using System.IO;

namespace MapRunRxSpatial;

/// <summary>The continuous-query scenario driven by the active tab.</summary>
internal enum Scenario
{
    Intersect,
    Distance,
    RxRxDistance,
    RxRxDistanceConnection,
}

/// <summary>Mutable demo state shared between the control panel and the traffic engine.</summary>
internal static class DemoParameters
{
    /// <summary>Folder holding the recorded vehicle trajectories (0.txt .. 19.txt).</summary>
    public static readonly string TrajectoryPath = Path.Combine(AppContext.BaseDirectory, "trafficData");

    public static int MovementIntervalMilliseconds { get; set; } = 1000;

    public static int VehicleCount { get; set; } = 1;

    public static double ObserveDistance { get; set; } = 1;

    public static Scenario ActiveScenario { get; set; } = Scenario.Intersect;
}
