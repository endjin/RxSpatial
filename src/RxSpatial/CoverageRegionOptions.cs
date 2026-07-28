namespace RxSpatial;

/// <summary>A geographic rectangle, expressed by its lower-left and upper-right corners.</summary>
public sealed class CoverageRegionOptions
{
    public double LowerLeftLatitude { get; set; } = 46;

    public double LowerLeftLongitude { get; set; } = -124;

    public double UpperRightLatitude { get; set; } = 49;

    public double UpperRightLongitude { get; set; } = -120;
}
