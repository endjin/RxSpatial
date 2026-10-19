namespace MapRunRxSpatial;

/// <summary>How close a vehicle is to an area or to another vehicle, relative to the observe distance.</summary>
internal enum DistanceBand
{
    OutOfRange,
    NearLimit,
    Stable,
    TooClose,
}

/// <summary>Shared band classifier for the area and vehicle-pair distance scenarios.</summary>
internal static class DistanceBands
{
    /// <summary>Below this fraction of the observe distance a pair is "stable" (the 2016 demo's 4/7).</summary>
    public const double StableFraction = 4.0 / 7;

    /// <summary>Below this fraction two vehicles breach the minimum safe distance.</summary>
    public const double TooCloseFraction = 1.0 / 7;

    /// <summary>Area scenario: red only at distance 0, i.e. inside the area.</summary>
    public static DistanceBand ForArea(double distance, double observeDistance)
        => distance <= 0 ? DistanceBand.TooClose : Classify(distance, observeDistance);

    /// <summary>Vehicle-pair scenario: red below the minimum safe distance.</summary>
    public static DistanceBand ForPair(double distance, double observeDistance)
        => distance < observeDistance * TooCloseFraction ? DistanceBand.TooClose : Classify(distance, observeDistance);

    /// <summary>
    /// The out-of-range colour differs by scenario: Blue (not watched) for areas, Gray
    /// (watched, no connection) for pairs.
    /// </summary>
    public static VehicleColor ToColor(DistanceBand band, VehicleColor outOfRange) => band switch
    {
        DistanceBand.TooClose => VehicleColor.Red,
        DistanceBand.Stable => VehicleColor.Yellow,
        DistanceBand.NearLimit => VehicleColor.Green,
        _ => outOfRange,
    };

    private static DistanceBand Classify(double distance, double observeDistance) => distance switch
    {
        _ when distance >= observeDistance => DistanceBand.OutOfRange,
        _ when distance < observeDistance * StableFraction => DistanceBand.Stable,
        _ => DistanceBand.NearLimit,
    };
}
