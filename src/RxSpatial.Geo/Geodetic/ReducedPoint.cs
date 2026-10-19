using System.Runtime.CompilerServices;

namespace RxSpatial.Geo;

/// <summary>
/// A position with its reduced (parametric) latitude β worked out, tan β = (1 − f) tan φ: the
/// per-point start of Vincenty's inverse and direct solutions. Building it once lets a loop
/// that solves many geodesics from one point (projecting a ring into a plane, outlining a
/// circle) skip a tangent, a square root and a division per geodesic, with bit-identical
/// results.
/// </summary>
internal readonly struct ReducedPoint
{
    private const double RadiansPerDegree = Math.PI / 180;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReducedPoint(GeoPoint point)
    {
        Longitude = point.Longitude;
        TanU = (1 - Wgs84.Flattening) * Math.Tan(point.Latitude * RadiansPerDegree);
        CosU = 1 / Math.Sqrt(1 + (TanU * TanU));
        SinU = TanU * CosU;
    }

    /// <summary>The longitude, in degrees.</summary>
    public double Longitude { get; }

    /// <summary>tan β.</summary>
    public double TanU { get; }

    /// <summary>sin β.</summary>
    public double SinU { get; }

    /// <summary>cos β.</summary>
    public double CosU { get; }
}
