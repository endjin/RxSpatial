using Microsoft.SqlServer.Types;

namespace RxSpatial.Experiments;

/// <summary>
/// Deterministic Seattle-area workload. The paper's §VI-A scenario is "a moving vehicle's
/// location against 2000 geofences that represent the polygons of shopping malls and points of
/// interest"; it used MNTG-generated trajectories and does not publish the fences. Here the
/// fences are <c>STBuffer</c> circles (100–400 m radius), three quarters of them clustered
/// around 25 hot spots (malls and POIs bunch together) and the rest uniform, and every moving
/// object follows its own random walk. Everything derives from one seed, so every variant sees
/// identical fences and routes.
/// </summary>
internal sealed class SeattleWorkload
{
    public const double MinLatitude = 47.50;
    public const double MaxLatitude = 47.75;
    public const double MinLongitude = -122.45;
    public const double MaxLongitude = -122.20;

    private const int Srid = 4326;
    private const double MetresPerDegreeLatitude = 111_320;
    private const int HotSpots = 25;

    private static readonly double MetresPerDegreeLongitude =
        MetresPerDegreeLatitude * Math.Cos((MinLatitude + MaxLatitude) / 2 * Math.PI / 180);

    private readonly int seed;
    private readonly SqlGeography[] fences;

    public SeattleWorkload(int seed, int fenceCount)
    {
        this.seed = seed;
        Random random = new(seed);
        (double Latitude, double Longitude)[] hotSpots = new (double, double)[HotSpots];
        for (int i = 0; i < hotSpots.Length; i++)
            hotSpots[i] = UniformPoint(random);

        fences = new SqlGeography[fenceCount];
        for (int i = 0; i < fenceCount; i++)
        {
            (double latitude, double longitude) = random.NextDouble() < 0.75
                ? NearHotSpot(random, hotSpots[random.Next(HotSpots)])
                : UniformPoint(random);
            double radius = 100 + (random.NextDouble() * 300);
            fences[i] = SqlGeography.Point(latitude, longitude, Srid).STBuffer(radius);
        }
    }

    /// <summary>The geofence pool; experiments subscribe to a prefix of it.</summary>
    public IReadOnlyList<SqlGeography> Fences => fences;

    /// <summary>
    /// The route of moving object <paramref name="objectIndex"/>: a random walk of 100–500 m
    /// steps that turns gradually and reflects off the edges of the area. Bare points, as GPS
    /// updates are.
    /// </summary>
    public SqlGeography[] Route(int objectIndex, int length)
    {
        // Not HashCode.Combine: it is randomised per process, and routes must repeat across runs.
        Random random = new(unchecked((seed * 1_000_003) + objectIndex));
        (double latitude, double longitude) = UniformPoint(random);
        double heading = random.NextDouble() * 2 * Math.PI;
        SqlGeography[] route = new SqlGeography[length];
        for (int i = 0; i < length; i++)
        {
            route[i] = SqlGeography.Point(latitude, longitude, Srid);
            heading += (random.NextDouble() - 0.5) * Math.PI / 3;
            double step = 100 + (random.NextDouble() * 400);
            latitude = Reflect(latitude + (step * Math.Cos(heading) / MetresPerDegreeLatitude), MinLatitude, MaxLatitude);
            longitude = Reflect(longitude + (step * Math.Sin(heading) / MetresPerDegreeLongitude), MinLongitude, MaxLongitude);
        }

        return route;
    }

    private static (double Latitude, double Longitude) UniformPoint(Random random)
        => (MinLatitude + (random.NextDouble() * (MaxLatitude - MinLatitude)),
            MinLongitude + (random.NextDouble() * (MaxLongitude - MinLongitude)));

    // A Gaussian offset (sigma ~1 km) from a hot spot, kept inside the area.
    private static (double Latitude, double Longitude) NearHotSpot(Random random, (double Latitude, double Longitude) hotSpot)
    {
        double sigmaMetres = 1000;
        double latitude = hotSpot.Latitude + (Gaussian(random) * sigmaMetres / MetresPerDegreeLatitude);
        double longitude = hotSpot.Longitude + (Gaussian(random) * sigmaMetres / MetresPerDegreeLongitude);
        return (Math.Clamp(latitude, MinLatitude, MaxLatitude), Math.Clamp(longitude, MinLongitude, MaxLongitude));
    }

    // Box–Muller standard normal.
    private static double Gaussian(Random random)
        => Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());

    private static double Reflect(double value, double min, double max)
        => value < min ? min + (min - value) : value > max ? max - (value - max) : value;
}
