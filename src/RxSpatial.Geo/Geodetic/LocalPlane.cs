using System.Runtime.CompilerServices;

namespace RxSpatial.Geo;

/// <summary>
/// An azimuthal equidistant projection centred on <see cref="Origin"/>: a point maps to
/// metres east (X) and north (Y), at its true geodesic distance from the origin and along
/// its true initial azimuth. Distances and directions from the origin are exact, so a
/// geodesic circle centred on the origin is exactly a circle; elsewhere the distortion grows
/// as (s/R)², about 0.03 % at 250 km. <see cref="GeoOperations"/> does its polygon work in
/// such planes.
/// </summary>
/// <param name="Origin">The projection centre.</param>
public readonly record struct LocalPlane(GeoPoint Origin)
{
    /// <summary>
    /// Projects a position into the plane. The coordinates are NaN when the geodesic from the
    /// origin does not converge (nearly antipodal points).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PlanarPoint Project(GeoPoint point)
    {
        double distance = Wgs84.DistanceAndAzimuth(Origin, point, out double azimuth);
        (double sin, double cos) = Math.SinCos(azimuth);
        return new(distance * sin, distance * cos);
    }

    /// <summary>Maps a point of the plane back to a position.</summary>
    /// <exception cref="ArgumentException">A coordinate is NaN or infinite.</exception>
    public GeoPoint Unproject(PlanarPoint point)
    {
        if (!point.IsFinite)
            throw new ArgumentException("Coordinates must be finite.", nameof(point));

        double distance = point.Length;
        return distance == 0 ? Origin : Wgs84.DirectRadians(Origin, Math.Atan2(point.X, point.Y), distance);
    }
}
