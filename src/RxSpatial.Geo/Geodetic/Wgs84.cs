using System.Runtime.CompilerServices;

namespace RxSpatial.Geo;

/// <summary>
/// Geodesics on the WGS84 ellipsoid (the datum of GPS and of SQL Server's SRID 4326):
/// Vincenty's inverse and direct solutions, accurate to well under a millimetre, and a fast
/// approximate distance for guarding the exact one. All methods are thread-safe and do not
/// allocate.
/// </summary>
/// <remarks>
/// Vincenty's inverse iteration does not converge for nearly antipodal points (about
/// 19,900 km or more apart). It is bounded at <see cref="MaxIterations"/> and then fails
/// deterministically: <see cref="Inverse"/> and <see cref="Distance"/> return
/// <see cref="double.NaN"/>.
/// </remarks>
public static class Wgs84
{
    /// <summary>The equatorial radius, in metres.</summary>
    public const double SemiMajorAxis = 6_378_137.0;

    /// <summary>The flattening, (a − b) / a.</summary>
    public const double Flattening = 1 / 298.257223563;

    /// <summary>The polar radius, in metres.</summary>
    public const double SemiMinorAxis = SemiMajorAxis * (1 - Flattening);

    /// <summary>The iteration bound of the inverse and direct solutions.</summary>
    public const int MaxIterations = 200;

    private const double EccentricitySquared = Flattening * (2 - Flattening);
    private const double SecondEccentricitySquared = ((SemiMajorAxis * SemiMajorAxis) - (SemiMinorAxis * SemiMinorAxis)) / (SemiMinorAxis * SemiMinorAxis);
    // λ and σ in radians: 1e-14 is ~6e-8 m on the ellipsoid (1e-12, the textbook value, leaves
    // micrometres) and still ~45 ulps of π, so the iteration always reaches it.
    private const double Convergence = 1e-14;
    private const double RadiansPerDegree = Math.PI / 180;

    /// <summary>
    /// The inverse problem: the geodesic distance from <paramref name="from"/> to
    /// <paramref name="to"/> and the azimuths at both ends.
    /// </summary>
    public static GeodesicInverse Inverse(GeoPoint from, GeoPoint to)
    {
        double distance = Solve(from, to, finalAzimuth: true, out double initial, out double final);
        return new(distance, initial / RadiansPerDegree, final / RadiansPerDegree);
    }

    /// <summary>The geodesic distance in metres, or <see cref="double.NaN"/> for unconverged, nearly antipodal points.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Distance(GeoPoint from, GeoPoint to) => Solve(from, to, finalAzimuth: false, out _, out _);

    /// <summary>
    /// The direct problem: the point <paramref name="distance"/> metres from
    /// <paramref name="from"/> along the geodesic that starts at <paramref name="azimuth"/>.
    /// </summary>
    /// <param name="from">The start.</param>
    /// <param name="azimuth">The initial bearing, in degrees clockwise from north.</param>
    /// <param name="distance">The distance along the geodesic, in metres.</param>
    /// <exception cref="ArgumentOutOfRangeException">The azimuth or distance is not finite.</exception>
    public static GeoPoint Direct(GeoPoint from, double azimuth, double distance)
    {
        if (!double.IsFinite(azimuth))
            throw new ArgumentOutOfRangeException(nameof(azimuth), azimuth, "The azimuth must be finite.");
        if (!double.IsFinite(distance))
            throw new ArgumentOutOfRangeException(nameof(distance), distance, "The distance must be finite.");

        return DirectRadians(from, azimuth * RadiansPerDegree, distance);
    }

    /// <summary>
    /// An approximate distance in metres: the flat-earth distance with the ellipsoid's radii of
    /// curvature at the mean latitude. It costs a few nanoseconds and is accurate to a few
    /// parts per million over tens of kilometres at mid latitudes, but its error grows with
    /// distance and latitude: use it to rule cases in or out with a margin, never as a result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double FastDistance(GeoPoint from, GeoPoint to)
    {
        PlanarPoint offset = FastOffset(from, to);
        return Math.Sqrt((offset.X * offset.X) + (offset.Y * offset.Y));
    }

    /// <summary>
    /// The flat-earth offset behind <see cref="FastDistance"/>: metres east (X) and north (Y)
    /// from <paramref name="from"/> to <paramref name="to"/>, with the radii of curvature at
    /// the mean latitude. It differs from the exact <see cref="LocalPlane"/> projection about
    /// <paramref name="from"/> mainly by the meridians' convergence, about s² tan φ / 2N.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PlanarPoint FastOffset(GeoPoint from, GeoPoint to)
    {
        double meanLatitude = (from.Latitude + to.Latitude) * 0.5 * RadiansPerDegree;
        (double sin, double cos) = Math.SinCos(meanLatitude);
        double w2 = 1 - (EccentricitySquared * sin * sin);
        double w = Math.Sqrt(w2);
        double dy = (to.Latitude - from.Latitude) * RadiansPerDegree * (SemiMajorAxis * (1 - EccentricitySquared) / (w2 * w));
        double dx = LongitudeDifference(from.Longitude, to.Longitude) * RadiansPerDegree * (SemiMajorAxis / w) * cos;
        return new(dx, dy);
    }

    /// <summary>The meridional radius of curvature at a latitude (degrees), in metres.</summary>
    internal static double MeridionalRadius(double latitude)
    {
        double sin = Math.Sin(latitude * RadiansPerDegree);
        double w2 = 1 - (EccentricitySquared * sin * sin);
        return SemiMajorAxis * (1 - EccentricitySquared) / (w2 * Math.Sqrt(w2));
    }

    /// <summary>The prime-vertical radius of curvature at a latitude (degrees), in metres.</summary>
    internal static double PrimeVerticalRadius(double latitude)
    {
        double sin = Math.Sin(latitude * RadiansPerDegree);
        return SemiMajorAxis / Math.Sqrt(1 - (EccentricitySquared * sin * sin));
    }

    /// <summary><paramref name="to"/> − <paramref name="from"/> in degrees, wrapped into [−180, 180].</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double LongitudeDifference(double from, double to) => WrapLongitude(to - from);

    /// <summary>
    /// <see cref="Math.IEEERemainder"/>(<paramref name="degrees"/>, 360), skipping its
    /// floating-point remainder (a helper call costing several nanoseconds) when the value is
    /// already in [−180, 180]: there the quotient rounds to zero (±180 rounds half to even), so
    /// the remainder is the value itself, signed zero included. Bit-identical everywhere; NaN
    /// and infinities take the slow path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double WrapLongitude(double degrees)
        => Math.Abs(degrees) <= 180 ? degrees : Math.IEEERemainder(degrees, 360);

    /// <summary>The distance and the initial azimuth in radians, for the local plane.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double DistanceAndAzimuth(GeoPoint from, GeoPoint to, out double initialAzimuth)
        => Solve(new ReducedPoint(from), new ReducedPoint(to), finalAzimuth: false, out initialAzimuth, out _);

    /// <summary>
    /// <see cref="DistanceAndAzimuth(GeoPoint, GeoPoint, out double)"/> from a start whose
    /// reduced latitude is already worked out: for many geodesics from one point.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double DistanceAndAzimuth(in ReducedPoint from, GeoPoint to, out double initialAzimuth)
        => Solve(from, new ReducedPoint(to), finalAzimuth: false, out initialAzimuth, out _);

    /// <summary>Vincenty's direct solution with the azimuth in radians.</summary>
    internal static GeoPoint DirectRadians(GeoPoint from, double azimuth, double distance)
        => DirectRadians(new ReducedPoint(from), azimuth, distance);

    /// <summary>Vincenty's direct solution with the azimuth in radians, from a start whose reduced latitude is already worked out.</summary>
    internal static GeoPoint DirectRadians(in ReducedPoint from, double azimuth, double distance)
    {
        (double sinAlpha1, double cosAlpha1) = Math.SinCos(azimuth);
        double tanU1 = from.TanU;
        double cosU1 = from.CosU;
        double sinU1 = from.SinU;
        double sigma1 = Math.Atan2(tanU1, cosAlpha1);
        double sinAlpha = cosU1 * sinAlpha1;
        double cos2Alpha = 1 - (sinAlpha * sinAlpha);
        double uSquared = cos2Alpha * SecondEccentricitySquared;
        double bigA = 1 + (uSquared / 16384 * (4096 + (uSquared * (-768 + (uSquared * (320 - (175 * uSquared)))))));
        double bigB = uSquared / 1024 * (256 + (uSquared * (-128 + (uSquared * (74 - (47 * uSquared))))));

        double sigma0 = distance / (SemiMinorAxis * bigA);
        double sigma = sigma0;
        double sinSigma, cosSigma, cos2SigmaM;
        int iterations = 0;
        while (true)
        {
            cos2SigmaM = Math.Cos((2 * sigma1) + sigma);
            (sinSigma, cosSigma) = Math.SinCos(sigma);
            double deltaSigma = DeltaSigma(bigB, sinSigma, cosSigma, cos2SigmaM);
            double previous = sigma;
            sigma = sigma0 + deltaSigma;
            if (Math.Abs(sigma - previous) <= Convergence || ++iterations >= MaxIterations)
                break;
        }

        cos2SigmaM = Math.Cos((2 * sigma1) + sigma);
        (sinSigma, cosSigma) = Math.SinCos(sigma);
        double tmp = (sinU1 * sinSigma) - (cosU1 * cosSigma * cosAlpha1);
        double latitude = Math.Atan2(
            (sinU1 * cosSigma) + (cosU1 * sinSigma * cosAlpha1),
            (1 - Flattening) * Math.Sqrt((sinAlpha * sinAlpha) + (tmp * tmp)));
        double lambda = Math.Atan2(sinSigma * sinAlpha1, (cosU1 * cosSigma) - (sinU1 * sinSigma * cosAlpha1));
        double c = Flattening / 16 * cos2Alpha * (4 + (Flattening * (4 - (3 * cos2Alpha))));
        double l = lambda - ((1 - c) * Flattening * sinAlpha
            * (sigma + (c * sinSigma * (cos2SigmaM + (c * cosSigma * (-1 + (2 * cos2SigmaM * cos2SigmaM)))))));
        double longitude = WrapLongitude(from.Longitude + (l / RadiansPerDegree));
        return new(Math.Clamp(latitude / RadiansPerDegree, -90, 90), Math.Clamp(longitude, -180, 180));
    }

    /// <summary>Vincenty's inverse solution; azimuths in radians.</summary>
    private static double Solve(GeoPoint from, GeoPoint to, bool finalAzimuth, out double initialAzimuth, out double arrivalAzimuth)
        => Solve(new ReducedPoint(from), new ReducedPoint(to), finalAzimuth, out initialAzimuth, out arrivalAzimuth);

    /// <summary>Vincenty's inverse solution; azimuths in radians.</summary>
    private static double Solve(in ReducedPoint from, in ReducedPoint to, bool finalAzimuth, out double initialAzimuth, out double arrivalAzimuth)
    {
        double l = LongitudeDifference(from.Longitude, to.Longitude) * RadiansPerDegree;
        double sinU1 = from.SinU, cosU1 = from.CosU;
        double sinU2 = to.SinU, cosU2 = to.CosU;

        double lambda = l;
        double sinLambda, cosLambda, sinSigma, cosSigma, sigma, sinAlpha, cos2Alpha, cos2SigmaM;
        int iterations = 0;
        while (true)
        {
            (sinLambda, cosLambda) = Math.SinCos(lambda);
            double t1 = cosU2 * sinLambda;
            double t2 = (cosU1 * sinU2) - (sinU1 * cosU2 * cosLambda);
            sinSigma = Math.Sqrt((t1 * t1) + (t2 * t2));
            if (sinSigma == 0)
            {
                // Coincident points (or both at the same pole).
                initialAzimuth = 0;
                arrivalAzimuth = 0;
                return 0;
            }

            cosSigma = (sinU1 * sinU2) + (cosU1 * cosU2 * cosLambda);
            sigma = Math.Atan2(sinSigma, cosSigma);
            sinAlpha = cosU1 * cosU2 * sinLambda / sinSigma;
            cos2Alpha = 1 - (sinAlpha * sinAlpha);
            cos2SigmaM = cos2Alpha != 0 ? cosSigma - (2 * sinU1 * sinU2 / cos2Alpha) : 0;
            double c = Flattening / 16 * cos2Alpha * (4 + (Flattening * (4 - (3 * cos2Alpha))));
            double previous = lambda;
            lambda = l + ((1 - c) * Flattening * sinAlpha
                * (sigma + (c * sinSigma * (cos2SigmaM + (c * cosSigma * (-1 + (2 * cos2SigmaM * cos2SigmaM)))))));
            if (Math.Abs(lambda - previous) <= Convergence)
                break;

            // λ beyond π, or no convergence within the bound: the nearly antipodal case.
            if (Math.Abs(lambda) > Math.PI || ++iterations >= MaxIterations)
            {
                initialAzimuth = double.NaN;
                arrivalAzimuth = double.NaN;
                return double.NaN;
            }
        }

        double uSquared = cos2Alpha * SecondEccentricitySquared;
        double bigA = 1 + (uSquared / 16384 * (4096 + (uSquared * (-768 + (uSquared * (320 - (175 * uSquared)))))));
        double bigB = uSquared / 1024 * (256 + (uSquared * (-128 + (uSquared * (74 - (47 * uSquared))))));
        double deltaSigma = DeltaSigma(bigB, sinSigma, cosSigma, cos2SigmaM);

        initialAzimuth = Math.Atan2(cosU2 * sinLambda, (cosU1 * sinU2) - (sinU1 * cosU2 * cosLambda));
        arrivalAzimuth = finalAzimuth
            ? Math.Atan2(cosU1 * sinLambda, (-sinU1 * cosU2) + (cosU1 * sinU2 * cosLambda))
            : double.NaN;
        return SemiMinorAxis * bigA * (sigma - deltaSigma);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double DeltaSigma(double bigB, double sinSigma, double cosSigma, double cos2SigmaM)
        => bigB * sinSigma * (cos2SigmaM + (bigB / 4 * ((cosSigma * (-1 + (2 * cos2SigmaM * cos2SigmaM)))
            - (bigB / 6 * cos2SigmaM * (-3 + (4 * sinSigma * sinSigma)) * (-3 + (4 * cos2SigmaM * cos2SigmaM))))));
}
