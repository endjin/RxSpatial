// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using static System.FormattableString;

namespace RxSpatial.Geo;

/// <summary>
/// A geodesic disc: every point within <see cref="RadiusMetres"/> metres of <see cref="Center"/>
/// along the ellipsoid. It is an exact shape, not a polygon (SQL Server's <c>STBuffer</c>
/// gives a 128-vertex polygon instead); <see cref="GeoOperations.ToVertices(GeoShape, int)"/> polygonises
/// it for drawing.
/// </summary>
public sealed class GeoCircle : GeoShape
{
    /// <summary>The smallest meridional radius of curvature (at the equator), in metres.</summary>
    private const double MinMeridionalRadius = Wgs84.SemiMajorAxis * (1 - (Wgs84.Flattening * (2 - Wgs84.Flattening)));
    private const double DegreesPerRadian = 180 / Math.PI;

    private readonly GeoBounds bounds;

    /// <summary>Creates a geodesic disc.</summary>
    /// <param name="center">The centre.</param>
    /// <param name="radiusMetres">The radius in metres: greater than zero and at most half of <see cref="GeoShape.MaxExtent"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The radius is not finite, not positive, or too large.</exception>
    public GeoCircle(GeoPoint center, double radiusMetres)
        : base(ShapeKind.Circle)
    {
        if (!(radiusMetres is > 0 and <= MaxExtent / 2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(radiusMetres),
                radiusMetres,
                Invariant($"The radius must be greater than zero and at most {MaxExtent / 2} metres."));
        }

        Center = center;
        RadiusMetres = radiusMetres;
        bounds = ComputeBounds(center, radiusMetres);
    }

    /// <summary>The centre.</summary>
    public GeoPoint Center { get; }

    /// <summary>The radius in metres.</summary>
    public double RadiusMetres { get; }

    /// <summary>
    /// The degree bounds, by reference: the hot tests read them in place. Copying the 32-byte
    /// struct lets the JIT use a 256-bit register, and a dirty upper half then costs an AVX–SSE
    /// transition penalty in the native maths that follows (Math.SinCos in FastDistance):
    /// measured at ~270 ns on a 20 ns point-in-circle test.
    /// </summary>
    internal ref readonly GeoBounds Bounds => ref bounds;

    /// <summary>The circle as <c>CIRCLE (longitude latitude, radius)</c> in the invariant culture.</summary>
    public override string ToString() => Invariant($"CIRCLE ({Center.Longitude} {Center.Latitude}, {RadiusMetres})");

    /// <summary>
    /// Conservative degree bounds, plus 1e-9° (~0.1 mm) against rounding. Moving s metres changes latitude by at most s/M and
    /// longitude by at most s/(N cos φ), so the bounds use the smallest M and N cos φ the
    /// disc can reach: M at its most equatorward latitude, N cos φ at its most poleward.
    /// </summary>
    private static GeoBounds ComputeBounds(GeoPoint center, double radius)
    {
        double roughLatitudeReach = radius / MinMeridionalRadius * DegreesPerRadian;
        double equatorward = Math.Max(0, Math.Abs(center.Latitude) - roughLatitudeReach);
        double latitudeReach = (radius / Wgs84.MeridionalRadius(equatorward) * DegreesPerRadian * (1 + 1e-9)) + 1e-9;
        double south = center.Latitude - latitudeReach;
        double north = center.Latitude + latitudeReach;
        if (north >= 90 || south <= -90)
            return new(-180, Math.Max(south, -90), 180, Math.Min(north, 90));

        double poleward = Math.Max(Math.Abs(south), Math.Abs(north));
        double parallelRadius = Wgs84.PrimeVerticalRadius(poleward) * Math.Cos(poleward / DegreesPerRadian);
        double longitudeReach = (radius / parallelRadius * DegreesPerRadian * (1 + 1e-9)) + 1e-9;
        return longitudeReach >= 180
            ? new(-180, south, 180, north)
            : new(center.Longitude - longitudeReach, south, center.Longitude + longitudeReach, north);
    }
}
