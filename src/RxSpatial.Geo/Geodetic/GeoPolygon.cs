using System.Globalization;
using System.Text;
using static System.FormattableString;

namespace RxSpatial.Geo;

/// <summary>
/// A convex polygon whose edges are geodesics, as in SQL Server's <c>geography</c> type.
/// Vertices run counter-clockwise seen from above (the interior is on the left, SQL
/// Server's left-hand rule) and the ring is not closed. Concave and self-intersecting rings
/// are rejected; clockwise rings are reversed. A polygon may be at most
/// <see cref="GeoShape.MaxExtent"/> across.
/// </summary>
public sealed class GeoPolygon : GeoShape
{
    private const double DegreesPerRadian = 180 / Math.PI;

    /// <summary>How far, in metres, <see cref="ContainsGuarded"/> may use the flat-earth offset.</summary>
    private const double FastGuardReach = 50_000;

    /// <summary>The highest latitude, in degrees, at which <see cref="ContainsGuarded"/> may use the flat-earth offset.</summary>
    private const double FastGuardLatitude = 75;

    private readonly GeoBounds bounds;

    // tan φ bounding the latitudes ContainsGuarded may see, or −1 where it may not decide.
    private readonly double guardTangent;

    /// <summary>The smallest radius of curvature of the ellipsoid (meridional, at the equator), in metres.</summary>
    private const double MinRadiusOfCurvature = Wgs84.SemiMajorAxis * (1 - (Wgs84.Flattening * (2 - Wgs84.Flattening)));

    private readonly GeoPoint[] vertices;
    private readonly PlanarPoint[] ring;
    private readonly double[] edgeLengths;

    // Each edge's initial azimuth (radians), computed on first use by the exact side test.
    private double[]? edgeAzimuths;

    private GeoPolygon(GeoPoint[] vertices, LocalPlane plane, PlanarPoint[] ring, double exactArea)
        : base(ShapeKind.Polygon)
    {
        this.vertices = vertices;
        this.ring = ring;
        Plane = plane;
        Area = double.IsNaN(exactArea) ? ConvexMath.SignedArea(ring) : exactArea;
        bounds = ComputeBounds(vertices, plane, ring);

        edgeLengths = new double[ring.Length];
        double reach = 0;
        double longest = 0;
        for (int i = 0; i < ring.Length; i++)
        {
            edgeLengths[i] = ring[i].DistanceTo(ring[(i + 1) % ring.Length]);
            reach = Math.Max(reach, ring[i].Length);
            longest = Math.Max(longest, edgeLengths[i]);
        }

        Reach = reach;

        // The guard needs tan φ at any latitude within FastGuardReach of the origin (0.46°).
        double guardLatitude = Math.Abs(plane.Origin.Latitude);
        guardTangent = guardLatitude <= FastGuardLatitude ? Math.Tan((guardLatitude + 0.46) / DegreesPerRadian) : -1;
        LongestEdge = longest;

        // A geodesic edge that does not pass through the plane's origin bows in the plane by
        // about ρ s² / 2R² (ρ: distance from the origin, s: half the edge). Twice ρ L² / R²
        // bounds that comfortably: 0.8 mm for a 3 km zone, a few kilometres at 500 km.
        EdgeBow = (2 * reach * longest * longest / (MinRadiusOfCurvature * MinRadiusOfCurvature)) + GeoOperations.BoundaryTolerance;
    }

    /// <summary>The vertices, counter-clockwise, without a closing repeat of the first.</summary>
    /// <remarks>The read-only wrapper is made on first use: polygons the kernel builds (intersection results) rarely need it.</remarks>
    public IReadOnlyList<GeoPoint> Vertices => field ??= Array.AsReadOnly(vertices);

    /// <summary>The local plane the polygon's maths runs in, centred inside the polygon.</summary>
    internal LocalPlane Plane { get; }

    /// <summary>The vertices projected into <see cref="Plane"/>: a convex counter-clockwise ring.</summary>
    internal ReadOnlySpan<PlanarPoint> Ring => ring;

    internal ReadOnlySpan<GeoPoint> VertexSpan => vertices;

    /// <summary>
    /// The area in square metres: the ring's area in the local plane, or the exact curved
    /// region's for a polygon produced by <see cref="GeoOperations.Intersection"/> with a circle.
    /// </summary>
    internal double Area { get; }

    /// <summary>
    /// The degree bounds, by reference: the hot tests read them in place. Copying the 32-byte
    /// struct lets the JIT use a 256-bit register, and a dirty upper half then costs an AVX–SSE
    /// transition penalty in the native maths that follows (Math.SinCos in FastDistance):
    /// measured at ~270 ns on a 20 ns point-in-circle test.
    /// </summary>
    internal ref readonly GeoBounds Bounds => ref bounds;

    /// <summary>
    /// How far, in metres, an edge may stray from its straight image in <see cref="Plane"/>:
    /// within this band of an edge, <see cref="Contains(GeoPoint)"/> decides with the exact geodesic.
    /// </summary>
    internal double EdgeBow { get; }

    /// <summary>The largest distance, in metres, of a vertex from the plane's origin.</summary>
    internal double Reach { get; }

    /// <summary>The longest edge in the plane, in metres.</summary>
    internal double LongestEdge { get; }

    /// <summary>
    /// Whether the polygon contains the position (boundary included, within
    /// <see cref="GeoOperations.BoundaryTolerance"/>). The plane decides where the position is
    /// clear of every edge by <see cref="EdgeBow"/>; nearer an edge, the side of the true
    /// geodesic decides: a position is inside an edge when its azimuth from the edge's start
    /// turns left (counter-clockwise) of the edge's own azimuth.
    /// </summary>
    internal bool Contains(GeoPoint point) => Contains(point, out _);

    /// <summary>
    /// <see cref="Contains(GeoPoint)"/>, deciding first with the cheap flat-earth offset from
    /// the plane's origin (<see cref="Wgs84.FastOffset"/>) instead of a Vincenty projection.
    /// Up to <see cref="FastGuardReach"/> from the origin and <see cref="FastGuardLatitude"/>,
    /// that offset is within <c>s² (tan φ + 0.1) / R + 1 mm</c> of the exact projection:
    /// twice its worst measured error (2 million random cases; s² tan φ / 2N, the meridians'
    /// convergence). A position that clears every edge by that much more than
    /// <see cref="EdgeBow"/>, or lies beyond one edge by as much, gets the answer the exact
    /// test would give; only positions near an edge pay for the projection.
    /// </summary>
    internal bool ContainsGuarded(GeoPoint point)
    {
        if (guardTangent >= 0 && Math.Abs(point.Latitude) <= FastGuardLatitude)
        {
            PlanarPoint p = Wgs84.FastOffset(Plane.Origin, point);
            double s = p.Length;
            if (s <= FastGuardReach)
            {
                double band = EdgeBow + (s * s * (guardTangent + 0.1) / MinRadiusOfCurvature) + (1e-6 * s) + 1e-3;
                bool clear = true;
                for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
                {
                    double offset = ConvexMath.Cross(ring[j], ring[i], p) / edgeLengths[j];
                    if (offset < -band)
                        return false;

                    clear &= offset > band;
                }

                if (clear)
                    return true;
            }
        }

        return Contains(point);
    }

    /// <summary><see cref="Contains(GeoPoint)"/>, also giving the position projected into <see cref="Plane"/>.</summary>
    internal bool Contains(GeoPoint point, out PlanarPoint projected)
    {
        PlanarPoint p = projected = Plane.Project(point);
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            double offset = ConvexMath.Cross(ring[j], ring[i], p) / edgeLengths[j];
            if (!(offset >= -EdgeBow))
                return false; // clearly outside, or NaN (an unconverged projection)

            if (offset <= EdgeBow && !IsLeftOfEdge(j, point))
                return false;
        }

        return true;
    }

    private bool IsLeftOfEdge(int edge, GeoPoint point)
    {
        double[] azimuths = edgeAzimuths ??= ComputeEdgeAzimuths();
        double distance = Wgs84.DistanceAndAzimuth(vertices[edge], point, out double azimuth);
        if (!(distance > GeoOperations.BoundaryTolerance))
            return !double.IsNaN(distance); // at the vertex itself

        // Clockwise turn from the edge's direction, in (−π, π]: positive means to the right.
        double turn = Math.IEEERemainder(azimuth - azimuths[edge], 2 * Math.PI);
        return turn * distance <= GeoOperations.BoundaryTolerance;
    }

    private double[] ComputeEdgeAzimuths()
    {
        double[] azimuths = new double[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            Wgs84.DistanceAndAzimuth(vertices[i], vertices[(i + 1) % vertices.Length], out azimuths[i]);

        return azimuths;
    }

    /// <summary>Creates a convex polygon from its vertices, in either winding order.</summary>
    /// <param name="vertices">At least three distinct vertices; a closing repeat of the first is ignored.</param>
    /// <exception cref="ArgumentException">
    /// Fewer than three distinct vertices, or a ring that has no area, is concave, intersects
    /// itself, or is more than <see cref="GeoShape.MaxExtent"/> across.
    /// </exception>
    public static GeoPolygon FromVertices(params ReadOnlySpan<GeoPoint> vertices)
    {
        List<GeoPoint> distinct = new(vertices.Length);
        foreach (GeoPoint vertex in vertices)
        {
            if (distinct.Count == 0 || distinct[^1] != vertex)
                distinct.Add(vertex);
        }

        while (distinct.Count > 1 && distinct[^1] == distinct[0])
            distinct.RemoveAt(distinct.Count - 1);

        if (distinct.Count < 3)
            throw new ArgumentException("A polygon needs at least three distinct vertices.", nameof(vertices));

        GeoPoint[] geoRing = [.. distinct];
        LocalPlane plane = new(Centroid(geoRing));
        PlanarPoint[] ring = new PlanarPoint[geoRing.Length];
        plane.Project(geoRing, ring);
        foreach (PlanarPoint point in ring)
        {
            if (!point.IsFinite)
                throw TooLarge(nameof(vertices));
        }

        if (ConvexMath.OrientCounterClockwise(ring, nameof(vertices)))
            Array.Reverse(geoRing);

        for (int i = 0; i < ring.Length; i++)
        {
            for (int j = i + 1; j < ring.Length; j++)
            {
                // The square root only where the squared distance already exceeds the limit (a
                // necessary condition, as the root is correctly rounded): same decision, O(n²) cheaper.
                if (ConvexMath.DistanceSquared(ring[i], ring[j]) > MaxExtent * MaxExtent && ring[i].DistanceTo(ring[j]) > MaxExtent)
                    throw TooLarge(nameof(vertices));
            }
        }

        return new(geoRing, plane, ring, double.NaN);
    }

    /// <summary>Creates a convex polygon from its vertices, in either winding order.</summary>
    /// <inheritdoc cref="FromVertices(ReadOnlySpan{GeoPoint})" path="/param|/exception"/>
    public static GeoPolygon FromVertices(IEnumerable<GeoPoint> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        return FromVertices([.. vertices]);
    }

    /// <summary>
    /// Creates the four-sided polygon with corners at the given latitudes and longitudes.
    /// Like SQL Server's, its edges are geodesics, so the north and south edges bow slightly
    /// poleward of their parallels (about 2 cm over 1 km at 47°N).
    /// </summary>
    /// <param name="south">The southern latitude, less than <paramref name="north"/>.</param>
    /// <param name="west">The western longitude, less than <paramref name="east"/> (the rectangle may not cross the antimeridian).</param>
    /// <param name="north">The northern latitude.</param>
    /// <param name="east">The eastern longitude.</param>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is out of range.</exception>
    /// <exception cref="ArgumentException">The corners are out of order or the rectangle is too large.</exception>
    public static GeoPolygon Rectangle(double south, double west, double north, double east)
    {
        if (!(south < north))
            throw new ArgumentException("South must be less than north.", nameof(south));
        if (!(west < east))
            throw new ArgumentException("West must be less than east.", nameof(west));

        return FromVertices([new(south, west), new(south, east), new(north, east), new(north, west)]);
    }

    /// <summary>
    /// Parses a single-ring WKT polygon in SQL Server's coordinate order, longitude first:
    /// <c>POLYGON ((lon lat, lon lat, ...))</c>, in the invariant culture. Either winding
    /// order is accepted.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The text is not a single-ring two-dimensional WKT polygon, a coordinate is out of range,
    /// or the ring is not a valid convex polygon (see <see cref="FromVertices(ReadOnlySpan{GeoPoint})"/>).
    /// </exception>
    public static GeoPolygon FromWkt(string wkt)
    {
        List<(double Longitude, double Latitude)> pairs = WktPolygon.Parse(wkt, nameof(wkt));
        GeoPoint[] points = new GeoPoint[pairs.Count];
        for (int i = 0; i < pairs.Count; i++)
        {
            (double longitude, double latitude) = pairs[i];
            if (!(latitude is >= -90 and <= 90) || !(longitude is >= -180 and <= 180))
                throw new ArgumentException(Invariant($"'{longitude} {latitude}' is not a valid longitude and latitude."), nameof(wkt));

            points[i] = new(latitude, longitude);
        }

        return FromVertices(points);
    }

    /// <summary>The polygon as closed-ring WKT (longitude first) in the invariant culture.</summary>
    public override string ToString()
    {
        StringBuilder text = new("POLYGON ((");
        foreach (GeoPoint vertex in vertices)
            text.Append(CultureInfo.InvariantCulture, $"{vertex.Longitude} {vertex.Latitude}, ");

        return text.Append(CultureInfo.InvariantCulture, $"{vertices[0].Longitude} {vertices[0].Latitude}))").ToString();
    }

    /// <summary>Wraps a ring the kernel computed in <paramref name="plane"/>: convex, counter-clockwise and small.</summary>
    internal static GeoPolygon FromTrustedRing(LocalPlane plane, PlanarPoint[] ring, double exactArea)
    {
        GeoPoint[] geoRing = new GeoPoint[ring.Length];
        plane.Unproject(ring, geoRing);

        return new(geoRing, plane, ring, exactArea);
    }

    /// <summary>The mean of the vertices, with longitudes unwrapped across the antimeridian.</summary>
    private static GeoPoint Centroid(GeoPoint[] points)
    {
        double latitude = 0;
        double longitudeOffset = 0;
        foreach (GeoPoint point in points)
        {
            latitude += point.Latitude;
            longitudeOffset += Wgs84.LongitudeDifference(points[0].Longitude, point.Longitude);
        }

        double longitude = Wgs84.WrapLongitude(points[0].Longitude + (longitudeOffset / points.Length));
        return new(latitude / points.Length, Math.Clamp(longitude, -180, 180));
    }

    /// <summary>
    /// Conservative degree bounds. Longitude is monotonic along a geodesic edge, so the
    /// vertices bound it; latitude is not, because a geodesic bows poleward of the chord
    /// between its ends by about L² tan φ / 8R, so the latitude range is widened by half
    /// again that bow for the longest edge. A polygon around a pole reaches the pole and
    /// spans all longitudes.
    /// </summary>
    private static GeoBounds ComputeBounds(GeoPoint[] vertices, LocalPlane plane, PlanarPoint[] ring)
    {
        double minLatitude = double.PositiveInfinity, maxLatitude = double.NegativeInfinity;
        double minLongitude = double.PositiveInfinity, maxLongitude = double.NegativeInfinity;
        double bow = 0;
        double reference = vertices[0].Longitude;
        for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
        {
            GeoPoint vertex = vertices[i];
            double longitude = reference + Wgs84.LongitudeDifference(reference, vertex.Longitude);
            minLatitude = Math.Min(minLatitude, vertex.Latitude);
            maxLatitude = Math.Max(maxLatitude, vertex.Latitude);
            minLongitude = Math.Min(minLongitude, longitude);
            maxLongitude = Math.Max(maxLongitude, longitude);

            double latitude = Math.Max(Math.Abs(vertex.Latitude), Math.Abs(vertices[j].Latitude));
            double length = ring[i].DistanceTo(ring[j]);
            bow = Math.Max(bow, length * length * Math.Tan(Math.Min(latitude, 89.999) / DegreesPerRadian) / (8 * Wgs84.SemiMinorAxis));
        }

        double widening = ((1.5 * bow) + 1e-3) / Wgs84.MeridionalRadius(0) * DegreesPerRadian;
        minLatitude = Math.Max(-90, minLatitude - widening);
        maxLatitude = Math.Min(90, maxLatitude + widening);

        if (maxLatitude > 80 && ConvexMath.Contains(ring, plane.Project(new(90, 0))))
            return new(-180, minLatitude, 180, 90);
        if (minLatitude < -80 && ConvexMath.Contains(ring, plane.Project(new(-90, 0))))
            return new(-180, -90, 180, maxLatitude);

        // A hair (~0.1 mm) of longitude so that rounding can never put a vertex outside.
        return new(minLongitude - 1e-9, minLatitude, maxLongitude + 1e-9, maxLatitude);
    }

    private static ArgumentException TooLarge(string paramName)
        => new(Invariant($"The polygon is more than {MaxExtent} metres across."), paramName);
}
