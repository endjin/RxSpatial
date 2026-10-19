using GeographicLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Geo;
using Shouldly;

namespace RxSpatial.Tests.Geo;

/// <summary>
/// Vincenty's solutions and the local plane against GeographicLib (Karney's algorithms, exact
/// to a few nanometres), the test-only geodesic oracle.
/// </summary>
[TestClass]
public class Wgs84Tests
{
    private static readonly GeoPoint Needle = new(47.6205, -122.3493);

    [TestMethod]
    public void Inverse_MatchesGeographicLib_WorldwideToAMillimetre()
    {
        Random random = new(20261019);
        int unconverged = 0;
        for (int i = 0; i < 5_000; i++)
        {
            GeoPoint a = new(random.NextDouble() * 180 - 90, random.NextDouble() * 360 - 180);
            GeoPoint b = new(random.NextDouble() * 180 - 90, random.NextDouble() * 360 - 180);
            Geodesic.WGS84.Inverse(a.Latitude, a.Longitude, b.Latitude, b.Longitude, out double s12, out double azi1, out double azi2);

            GeodesicInverse inverse = Wgs84.Inverse(a, b);
            if (!inverse.Converged)
            {
                // Vincenty fails only for nearly antipodal points.
                s12.ShouldBeGreaterThan(19_000_000, $"{a} {b}");
                unconverged++;
                continue;
            }

            inverse.Distance.ShouldBe(s12, 1e-3, $"{a} {b}");
            if (s12 > 1 && s12 < 19_000_000)
            {
                AngleDifference(inverse.InitialAzimuth, azi1).ShouldBeLessThan(1e-6, $"{a} {b}");
                AngleDifference(inverse.FinalAzimuth, azi2).ShouldBeLessThan(1e-6, $"{a} {b}");
            }
        }

        unconverged.ShouldBeLessThan(25);
    }

    [TestMethod]
    public void Inverse_SeattleScale_MatchesGeographicLibToWithinMicrometres()
    {
        foreach (double s in (double[])[0.01, 1, 10, 100, 1_000, 10_000, 50_000, 250_000])
        {
            for (int azimuth = 0; azimuth < 360; azimuth += 15)
            {
                Geodesic.WGS84.Direct(Needle.Latitude, Needle.Longitude, azimuth, s, out double lat, out double lon);
                // Vincenty's series truncation adds ~5e-12 of the distance.
                Wgs84.Distance(Needle, new(lat, lon)).ShouldBe(s, 1e-7 + (1e-11 * s), $"{s} m at {azimuth}°");
            }
        }
    }

    [TestMethod]
    public void Inverse_NearlyAntipodalPoints_FailDeterministicallyAsNaN()
    {
        GeoPoint origin = new(0, 0);
        GeoPoint antipode = new(0, 180);

        double first = Wgs84.Distance(origin, antipode);
        double.IsNaN(first).ShouldBeTrue();
        double.IsNaN(Wgs84.Distance(origin, antipode)).ShouldBeTrue();
        GeodesicInverse inverse = Wgs84.Inverse(new(0.5, 0), new(-0.5, 179.7));
        inverse.Converged.ShouldBeFalse();
        double.IsNaN(inverse.InitialAzimuth).ShouldBeTrue();
        double.IsNaN(inverse.FinalAzimuth).ShouldBeTrue();
    }

    [TestMethod]
    public void Inverse_CoincidentAndPolarPoints_AreZeroApart()
    {
        Wgs84.Distance(Needle, Needle).ShouldBe(0);
        Wgs84.Distance(new(90, 0), new(90, 135)).ShouldBeLessThan(1e-6);
        Wgs84.Distance(new(10, 180), new(10, -180)).ShouldBe(0);
    }

    [TestMethod]
    public void Inverse_AcrossTheAntimeridian_IsShort()
    {
        Geodesic.WGS84.Inverse(0, 179.999, 0, -179.999, out double expected);

        Wgs84.Distance(new(0, 179.999), new(0, -179.999)).ShouldBe(expected, 1e-6);
        expected.ShouldBeLessThan(250);
    }

    [TestMethod]
    public void Direct_MatchesGeographicLib_WorldwideToAMillimetre()
    {
        Random random = new(7);
        for (int i = 0; i < 2_000; i++)
        {
            GeoPoint from = new(random.NextDouble() * 178 - 89, random.NextDouble() * 360 - 180);
            double azimuth = random.NextDouble() * 360 - 180;
            double distance = random.NextDouble() * 10_000_000;
            Geodesic.WGS84.Direct(from.Latitude, from.Longitude, azimuth, distance, out double lat, out double lon);

            GeoPoint to = Wgs84.Direct(from, azimuth, distance);

            Geodesic.WGS84.Inverse(to.Latitude, to.Longitude, lat, lon, out double miss);
            miss.ShouldBeLessThan(1e-3, $"{from} az {azimuth} s {distance}");
        }
    }

    [TestMethod]
    public void Direct_RejectsNonFiniteInputs()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Wgs84.Direct(Needle, double.NaN, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => Wgs84.Direct(Needle, 0, double.PositiveInfinity));
    }

    [TestMethod]
    public void FastDistance_StaysWellInsideTheGuardBand_WhereTheGuardTrustsIt()
    {
        // The guard trusts FastDistance up to 75° latitude and 350 km; it decides only when
        // clear of the threshold by 0.5 %, so its error there must stay well below that.
        double worst = 0;
        foreach (double lat in (double[])[-75, -60, -30, 0, 30, 47.6, 60, 75])
        {
            foreach (double s in (double[])[10, 1_000, 50_000, 350_000])
            {
                for (int azimuth = 0; azimuth < 360; azimuth += 10)
                {
                    Geodesic.WGS84.Direct(lat, 179.9, azimuth, s, out double lat2, out double lon2);
                    if (Math.Abs(lat2) > 75)
                        continue;

                    double fast = Wgs84.FastDistance(new(lat, 179.9), new(lat2, lon2));
                    worst = Math.Max(worst, Math.Abs(fast - s) / s);
                }
            }
        }

        worst.ShouldBeLessThan(0.0025);
    }

    [TestMethod]
    public void LocalPlane_ProjectsAtTheTrueDistanceAndBearing_AndRoundTrips()
    {
        LocalPlane plane = new(Needle);
        Random random = new(11);
        for (int i = 0; i < 1_000; i++)
        {
            double azimuth = random.NextDouble() * 360;
            double s = random.NextDouble() * 250_000;
            Geodesic.WGS84.Direct(Needle.Latitude, Needle.Longitude, azimuth, s, out double lat, out double lon);
            GeoPoint point = new(lat, lon);

            PlanarPoint projected = plane.Project(point);

            Math.Sqrt((projected.X * projected.X) + (projected.Y * projected.Y)).ShouldBe(s, 1e-7 + (1e-11 * s));
            AngleDifference(Math.Atan2(projected.X, projected.Y) * 180 / Math.PI, azimuth).ShouldBeLessThan(1e-7);
            GeoPoint back = plane.Unproject(projected);
            Geodesic.WGS84.Inverse(back.Latitude, back.Longitude, lat, lon, out double miss);
            miss.ShouldBeLessThan(1e-6);
        }

        plane.Unproject(default).ShouldBe(Needle);
        Should.Throw<ArgumentException>(() => plane.Unproject(new(double.NaN, 0)));
    }

    [TestMethod]
    public void GeoPoint_ValidatesRanges_AndFormatsInvariantly()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new GeoPoint(90.1, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new GeoPoint(-90.1, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new GeoPoint(0, 180.1));
        Should.Throw<ArgumentOutOfRangeException>(() => new GeoPoint(double.NaN, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new GeoPoint(0, double.NaN));
        new GeoPoint(-90, -180).ShouldBe(new GeoPoint(-90, -180));
        new GeoPoint(47.5, -122.25).ToString().ShouldBe("(47.5, -122.25)");
    }

    private static double AngleDifference(double a, double b) => Math.Abs(Math.IEEERemainder(a - b, 360));
}
