namespace RxSpatial.Geo;

/// <summary>
/// The solution of the inverse geodesic problem between two points (see
/// <see cref="Wgs84.Inverse"/>). All three values are <see cref="double.NaN"/> when the
/// iteration does not converge, which happens only for nearly antipodal points.
/// </summary>
/// <param name="Distance">The geodesic distance in metres.</param>
/// <param name="InitialAzimuth">The bearing at the start, in degrees clockwise from north, from -180 to 180.</param>
/// <param name="FinalAzimuth">The bearing on arrival, in degrees clockwise from north, from -180 to 180.</param>
public readonly record struct GeodesicInverse(double Distance, double InitialAzimuth, double FinalAzimuth)
{
    /// <summary>Whether the solution converged (the values are not NaN).</summary>
    public bool Converged => !double.IsNaN(Distance);
}
