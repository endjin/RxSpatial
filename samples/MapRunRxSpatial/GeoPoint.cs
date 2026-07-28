namespace MapRunRxSpatial
{
    /// <summary>A latitude/longitude pair (replaces the Bing Maps Location type).</summary>
    public record struct GeoPoint(double Latitude, double Longitude);
}
