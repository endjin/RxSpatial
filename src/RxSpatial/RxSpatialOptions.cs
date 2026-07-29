namespace RxSpatial;

/// <summary>
/// Settings for RxSpatial moving objects. Bind from the <c>RxSpatial</c> configuration
/// section (<see cref="SectionName"/>) with Microsoft.Extensions.Configuration, or construct
/// directly in code. Enum values bind case-insensitively, so historic setting values such as
/// <c>"RTREE"</c> or <c>"hashmap"</c> remain valid.
/// </summary>
public sealed class RxSpatialOptions
{
    /// <summary>The configuration section name RxSpatial settings are conventionally bound from.</summary>
    public const string SectionName = "RxSpatial";

    /// <summary>
    /// The ambient options used by the parameterless <see cref="RxGeography"/> constructor.
    /// Set once at application start-up (typically from bound configuration).
    /// </summary>
    public static RxSpatialOptions Default { get; set; } = new();

    /// <summary>Spatial index used to hold observers.</summary>
    public RxGeographyDataStruct PreferredDataStruct { get; set; } = RxGeographyDataStruct.List;

    /// <summary>
    /// Location-push buffer size before <see cref="RxSpatialOverLoadException"/> is thrown
    /// (buffered mode only).
    /// </summary>
    public int QueueLimit { get; set; } = 1;

    /// <summary>
    /// <see langword="true"/> processes pushes synchronously and forwards raw locations to
    /// direct subscribers; <see langword="false"/> is the buffered throughput mode.
    /// </summary>
    public bool IsVisualized { get; set; }

    /// <summary>Settings for the grid (<see cref="RxGeographyDataStruct.HashMap"/>) backend.</summary>
    public GridOptions Grid { get; set; } = new();

    /// <summary>
    /// Cleaning-token interval of the <see cref="RxGeographyDataStruct.RumTree"/> backend:
    /// the number of garbage-creating operations between purges of one leaf. Successor to
    /// the legacy <c>RUMTreeGCInspectionInterval</c> application setting.
    /// </summary>
    public int RumTreeCleaningInterval { get; set; } = 5;
}
