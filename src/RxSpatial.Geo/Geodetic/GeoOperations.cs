using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RxSpatial.Geo;

/// <summary>
/// Spatial operations on <see cref="GeoShape"/>s over the WGS84 ellipsoid, in metres and
/// square metres. Point and circle relations use exact geodesic distances (Vincenty);
/// polygon work runs in a <see cref="LocalPlane"/> centred on one of the shapes. Boundaries
/// are closed, so shapes that touch intersect. <see cref="Intersects"/>,
/// <see cref="Distance"/>, <see cref="Area"/> and <see cref="Bounds"/> do not allocate.
/// All methods are thread-safe.
/// </summary>
/// <remarks>
/// <para>
/// Exact (to Vincenty's sub-millimetre accuracy) at any size: every distance, whether a
/// point, circle or polygon is inside a circle or polygon, and circle–circle relations. A
/// polygon's geodesic edges bow away from their straight image in a local plane by about
/// ρ s² / 2R², so the polygon tests use the plane only where it cannot be wrong and fall back
/// to the true geodesic edge near it.
/// </para>
/// <para>
/// Plane-bound: polygon–polygon overlap and the clipping inside <see cref="Intersection"/>
/// run in one polygon's plane, so shapes that nearly touch are decided to within that bow
/// (under a millimetre for city-sized shapes, up to ~100 m at <see cref="GeoShape.MaxExtent"/>);
/// polygon areas carry the plane's (s/R)² / 6 area distortion (at most 0.03 %).
/// </para>
/// <para>
/// Against SQL Server's <c>geography</c> (whose circles are 128-vertex <c>STBuffer</c>
/// polygons), the results agree to within 0.5 m for point–polygon and point–circle distances,
/// 0.01 % (or 1 cm) for point-to-point distances, and 0.15 % for areas; the rest of the
/// difference is SQL Server's polygonised circle (see the golden fidelity tests).
/// </para>
/// </remarks>
public static class GeoOperations
{
    /// <summary>
    /// The default number of segments for a full circle, in <see cref="ToVertices"/> and in
    /// the arcs of intersection results: the number SQL Server's <c>STBuffer</c> uses.
    /// </summary>
    public const int DefaultSegments = 128;

    /// <summary>
    /// The guard band of the point-in-circle test, relative to the radius: outside it, the
    /// approximate <see cref="Wgs84.FastDistance"/> decides; inside it, the exact distance does.
    /// </summary>
    internal const double GuardBand = 0.005;

    /// <summary>
    /// The largest approximate distance, in metres, at which <see cref="Wgs84.FastDistance"/>
    /// may decide. Up to 75° latitude its error stays below 0.25 % here, half the guard band.
    /// </summary>
    internal const double GuardMaxDistance = 350_000;

    /// <summary>The highest latitude, in degrees, at which <see cref="Wgs84.FastDistance"/> may decide.</summary>
    internal const double GuardMaxLatitude = 75;

    /// <summary>
    /// How far, in metres, a point may sit outside a polygon edge in the local plane and still
    /// count as on it: projections round at the nanometre level, so a point placed exactly on
    /// an edge must not depend on that rounding.
    /// </summary>
    internal const double BoundaryTolerance = 1e-6;

    private const int StackVertices = 128;

    /// <summary>The square of the ellipsoid's smallest radius of curvature, for conservative bow estimates.</summary>
    private const double EarthRadiusSquared = 6_335_439.0 * 6_335_439.0;

    /// <summary>Rounding tolerance, in metres, for collapsing computed vertices.</summary>
    private const double VertexTolerance = 1e-6;

    /// <summary>Whether the shapes share at least one point. Always false for <see cref="GeoEmpty"/>.</summary>
    /// <exception cref="ArgumentNullException">A shape is null.</exception>
    public static bool Intersects(GeoShape a, GeoShape b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        Order(ref a, ref b);
        return (a, b) switch
        {
            ({ Kind: ShapeKind.Empty }, _) => false,
            (GeoPointShape p, GeoPointShape q) => SamePosition(p.Position, q.Position),
            (GeoPointShape p, GeoCircle c) => c.Bounds.Contains(p.Position) && WithinReach(c.Center, p.Position, c.RadiusMetres),
            (GeoPointShape p, GeoPolygon polygon) => polygon.Bounds.Contains(p.Position) && polygon.Contains(p.Position),
            (GeoCircle c1, GeoCircle c2) => c1.Bounds.Overlaps(c2.Bounds) && WithinReach(c1.Center, c2.Center, c1.RadiusMetres + c2.RadiusMetres),
            (GeoCircle c, GeoPolygon polygon) => c.Bounds.Overlaps(polygon.Bounds)
                && (polygon.Contains(c.Center)
                    || DistanceToPolygon(polygon, c.Center) <= c.RadiusMetres),
            (GeoPolygon p1, GeoPolygon p2) => p1.Bounds.Overlaps(p2.Bounds) && PolygonsOverlap(p1, p2),
            _ => throw Unreachable(a, b),
        };
    }

    /// <summary>
    /// The shortest geodesic distance between the shapes in metres: 0 when they intersect,
    /// and <see cref="double.NaN"/> when either is <see cref="GeoEmpty"/> or the geodesic
    /// does not converge (nearly antipodal shapes).
    /// </summary>
    /// <exception cref="ArgumentNullException">A shape is null.</exception>
    public static double Distance(GeoShape a, GeoShape b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        Order(ref a, ref b);
        return (a, b) switch
        {
            ({ Kind: ShapeKind.Empty }, _) => double.NaN,
            (GeoPointShape p, GeoPointShape q) => Wgs84.Distance(p.Position, q.Position),
            (GeoPointShape p, GeoCircle c) => Math.Max(0, Wgs84.Distance(c.Center, p.Position) - c.RadiusMetres),
            (GeoPointShape p, GeoPolygon polygon) => DistanceToPolygon(polygon, p.Position),
            (GeoCircle c1, GeoCircle c2) => Math.Max(0, Wgs84.Distance(c1.Center, c2.Center) - c1.RadiusMetres - c2.RadiusMetres),
            (GeoCircle c, GeoPolygon polygon) => Math.Max(0, DistanceToPolygon(polygon, c.Center) - c.RadiusMetres),
            (GeoPolygon p1, GeoPolygon p2) => DistanceBetweenPolygons(p1, p2),
            _ => throw Unreachable(a, b),
        };
    }

    /// <summary>
    /// The shapes' common region. Every input is convex, so the result is too: a point, a
    /// circle (when one disc lies inside the other shape), a convex polygon, or
    /// <see cref="GeoEmpty"/> when the shapes are disjoint. Shapes that only touch give the
    /// touching point. Where a circle bounds the result (a lens, or a disc clipped by a
    /// polygon), the polygon's vertices lie on the circle no more than 2π/<see cref="DefaultSegments"/>
    /// apart, and its <see cref="Area"/> is the exact area of the curved region.
    /// </summary>
    /// <exception cref="ArgumentNullException">A shape is null.</exception>
    public static GeoShape Intersection(GeoShape a, GeoShape b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (!Intersects(a, b))
            return GeoEmpty.Instance;

        Order(ref a, ref b);
        return (a, b) switch
        {
            (GeoPointShape p, _) => p,
            (GeoCircle c1, GeoCircle c2) => CircleCircle(c1, c2),
            (GeoCircle c, GeoPolygon polygon) => CirclePolygon(c, polygon),
            (GeoPolygon p1, GeoPolygon p2) => PolygonPolygon(p1, p2),
            _ => throw Unreachable(a, b),
        };
    }

    /// <summary>
    /// The area in square metres: πr² for a circle, the polygon's area, and 0 for points and
    /// <see cref="GeoEmpty"/>. Polygon areas are measured in a local plane, within 0.03 % of
    /// the ellipsoidal area for shapes up to <see cref="GeoShape.MaxExtent"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">The shape is null.</exception>
    public static double Area(GeoShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape switch
        {
            GeoCircle c => Math.PI * c.RadiusMetres * c.RadiusMetres,
            GeoPolygon polygon => polygon.Area,
            _ => 0,
        };
    }

    /// <summary>
    /// The conservative bounding rectangle in degrees, for a spatial index. Not split at the
    /// antimeridian (see <see cref="GeoBounds"/>).
    /// </summary>
    /// <exception cref="ArgumentNullException">The shape is null.</exception>
    /// <exception cref="InvalidOperationException">The shape is <see cref="GeoEmpty"/>, which has no bounds.</exception>
    public static GeoBounds Bounds(GeoShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape switch
        {
            GeoPointShape p => new(p.Position.Longitude, p.Position.Latitude, p.Position.Longitude, p.Position.Latitude),
            GeoCircle c => c.Bounds,
            GeoPolygon polygon => polygon.Bounds,
            _ => throw new InvalidOperationException("An empty shape has no bounds."),
        };
    }

    /// <summary>
    /// The shape's outline for drawing or export, counter-clockwise and not closed: no
    /// vertices for <see cref="GeoEmpty"/>, one for a point, the polygon's vertices, or
    /// <paramref name="segments"/> vertices exactly on a circle (by the direct geodesic
    /// problem), starting due east of its centre.
    /// </summary>
    /// <exception cref="ArgumentNullException">The shape is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="segments"/> is less than 3.</exception>
    public static GeoPoint[] ToVertices(GeoShape shape, int segments = DefaultSegments)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentOutOfRangeException.ThrowIfLessThan(segments, 3);
        switch (shape)
        {
            case GeoPointShape p:
                return [p.Position];
            case GeoCircle c:
                GeoPoint[] ring = new GeoPoint[segments];
                for (int i = 0; i < segments; i++)
                {
                    // Azimuths run clockwise from north; counter-clockwise from east is 90° − θ.
                    double azimuth = (Math.PI / 2) - (2 * Math.PI * i / segments);
                    ring[i] = Wgs84.DirectRadians(c.Center, azimuth, c.RadiusMetres);
                }

                return ring;
            case GeoPolygon polygon:
                return polygon.VertexSpan.ToArray();
            default:
                return [];
        }
    }

    /// <summary>Swaps the pair so that the first has the lower <see cref="ShapeKind"/>.</summary>
    private static void Order(ref GeoShape a, ref GeoShape b)
    {
        if (a.Kind > b.Kind)
            (a, b) = (b, a);
    }

    /// <summary>The same position, including the same pole at any longitude and ±180°.</summary>
    private static bool SamePosition(GeoPoint p, GeoPoint q)
        => p.Latitude == q.Latitude
        && (Math.Abs(p.Latitude) == 90 || Wgs84.LongitudeDifference(p.Longitude, q.Longitude) == 0);

    /// <summary>
    /// Whether two points are at most <paramref name="reach"/> metres apart.
    /// <see cref="Wgs84.FastDistance"/> decides when it is clear of the threshold by the
    /// guard band; otherwise the exact distance decides.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool WithinReach(GeoPoint center, GeoPoint point, double reach)
    {
        if (Math.Abs(center.Latitude) <= GuardMaxLatitude && Math.Abs(point.Latitude) <= GuardMaxLatitude)
        {
            double approximate = Wgs84.FastDistance(center, point);
            if (approximate <= GuardMaxDistance)
            {
                double margin = (reach * GuardBand) + 1e-3;
                if (approximate > reach + margin)
                    return false;
                if (approximate < reach - margin)
                    return true;
            }
        }

        return Wgs84.Distance(center, point) <= reach;
    }

    /// <summary>
    /// The distance from a position to a polygon. Inside (see <see cref="GeoPolygon.Contains(GeoPoint)"/>) it
    /// is 0; outside, the polygon is projected into a plane centred on the position, where
    /// distances from the position are exact.
    /// </summary>
    private static double DistanceToPolygon(GeoPolygon polygon, GeoPoint point)
    {
        if (polygon.Contains(point))
            return 0;

        LocalPlane plane = new(point);
        ReadOnlySpan<GeoPoint> vertices = polygon.VertexSpan;
        PlanarPoint[]? rented = null;
        Span<PlanarPoint> projected = vertices.Length <= StackVertices
            ? stackalloc PlanarPoint[StackVertices]
            : (rented = ArrayPool<PlanarPoint>.Shared.Rent(vertices.Length));
        try
        {
            projected = Project(plane, vertices, projected);
            if (ConvexMath.Contains(projected, default, BoundaryTolerance))
                return 0;

            double best = double.PositiveInfinity;
            int nearestEdge = 0;
            PlanarPoint foot = default;
            for (int i = 0, j = projected.Length - 1; i < projected.Length; j = i++)
            {
                PlanarPoint candidate = ConvexMath.ClosestOnSegment(projected[j], projected[i], default);
                double distance = candidate.Length;
                if (double.IsNaN(distance))
                    return double.NaN;

                if (distance < best)
                {
                    best = distance;
                    nearestEdge = j;
                    foot = candidate;
                }
            }

            // The edge bows in this plane by about ρ s² / 2R² (s: half its length); refine on the
            // true geodesic only where twice that could exceed a millimetre (rare at city scale).
            PlanarPoint start = projected[nearestEdge];
            double length = start.DistanceTo(projected[(nearestEdge + 1) % projected.Length]);
            if (best * length * length / (4 * EarthRadiusSquared) <= 1e-3)
                return best;

            return DistanceToGeodesicEdge(
                vertices[nearestEdge],
                vertices[(nearestEdge + 1) % vertices.Length],
                point,
                length == 0 ? 0 : start.DistanceTo(foot) / length);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<PlanarPoint>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// The distance from a position to the geodesic segment a–b, starting the search at
    /// <paramref name="fraction"/> of the way along it. In the plane centred on the current
    /// foot, the edge is a straight line through the origin (geodesics through an azimuthal
    /// equidistant projection's centre are straight), so the foot moves by the position's
    /// along-track offset until it stops (or reaches an end).
    /// </summary>
    private static double DistanceToGeodesicEdge(GeoPoint a, GeoPoint b, GeoPoint point, double fraction)
    {
        double length = Wgs84.DistanceAndAzimuth(a, b, out double azimuth);
        if (!(length > 0))
            return Wgs84.Distance(a, point);

        Wgs84.DistanceAndAzimuth(b, a, out double backAzimuth);
        double along = Math.Clamp(fraction, 0, 1) * length;
        for (int iteration = 0; iteration < 10; iteration++)
        {
            GeoPoint foot = along <= 0 ? a : Wgs84.DirectRadians(a, azimuth, along);
            double heading = along <= 0 ? azimuth : HeadingTowards(foot, b, backAzimuth, length - along);
            double distance = Wgs84.DistanceAndAzimuth(foot, point, out double bearing);
            if (!(distance > 0))
                return distance;

            double next = Math.Clamp(along + (distance * Math.Cos(bearing - heading)), 0, length);
            if (Math.Abs(next - along) < 1e-6)
                return distance;

            along = next;
        }

        return Wgs84.Distance(along <= 0 ? a : Wgs84.DirectRadians(a, azimuth, along), point);

        // The edge's direction at the foot: towards b, or (at b itself) straight on past it.
        static double HeadingTowards(GeoPoint foot, GeoPoint b, double backAzimuth, double remaining)
        {
            if (remaining > 1e-6)
            {
                Wgs84.DistanceAndAzimuth(foot, b, out double toB);
                return toB;
            }

            return backAzimuth + Math.PI;
        }
    }

    private static bool PolygonsOverlap(GeoPolygon p1, GeoPolygon p2)
    {
        ReadOnlySpan<GeoPoint> vertices = p2.VertexSpan;
        PlanarPoint[]? rented = null;
        Span<PlanarPoint> projected = vertices.Length <= StackVertices
            ? stackalloc PlanarPoint[StackVertices]
            : (rented = ArrayPool<PlanarPoint>.Shared.Rent(vertices.Length));
        try
        {
            projected = Project(p1.Plane, vertices, projected);
            return ConvexMath.Overlaps(p1.Ring, projected, BoundaryTolerance);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<PlanarPoint>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// The distance between two polygons: the closest pair of points is found in the first
    /// polygon's plane, then measured exactly along the geodesic between them.
    /// </summary>
    private static double DistanceBetweenPolygons(GeoPolygon p1, GeoPolygon p2)
    {
        ReadOnlySpan<GeoPoint> vertices = p2.VertexSpan;
        PlanarPoint[]? rented = null;
        Span<PlanarPoint> projected = vertices.Length <= StackVertices
            ? stackalloc PlanarPoint[StackVertices]
            : (rented = ArrayPool<PlanarPoint>.Shared.Rent(vertices.Length));
        try
        {
            projected = Project(p1.Plane, vertices, projected);
            double planar = ConvexMath.DistanceBetween(p1.Ring, projected, out PlanarPoint onP1, out PlanarPoint onP2, BoundaryTolerance);
            if (!(planar > 0))
                return planar;

            // Disjoint convex polygons are nearest at a vertex of one of them: measure from
            // that vertex to the other polygon exactly.
            int vertex = projected.IndexOf(onP2);
            if (vertex >= 0)
                return DistanceToPolygon(p1, vertices[vertex]);

            vertex = p1.Ring.IndexOf(onP1);
            return vertex >= 0
                ? DistanceToPolygon(p2, p1.VertexSpan[vertex])
                : Wgs84.Distance(p1.Plane.Unproject(onP1), p1.Plane.Unproject(onP2));
        }
        finally
        {
            if (rented is not null)
                ArrayPool<PlanarPoint>.Shared.Return(rented);
        }
    }

    private static Span<PlanarPoint> Project(LocalPlane plane, ReadOnlySpan<GeoPoint> vertices, Span<PlanarPoint> buffer)
    {
        Span<PlanarPoint> projected = buffer[..vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            projected[i] = plane.Project(vertices[i]);

        return projected;
    }

    private static GeoShape CircleCircle(GeoCircle c1, GeoCircle c2)
    {
        double d = Wgs84.DistanceAndAzimuth(c1.Center, c2.Center, out double azimuth);
        double r1 = c1.RadiusMetres;
        double r2 = c2.RadiusMetres;
        if (d + Math.Min(r1, r2) <= Math.Max(r1, r2))
            return r1 <= r2 ? c1 : c2;

        if (d >= r1 + r2)
            return new GeoPointShape(Wgs84.DirectRadians(c1.Center, azimuth, r1));

        // In the plane centred on c1, distances and azimuths from c1 are exact: c2 sits at
        // its true distance and bearing, and c1's circle is exactly a circle.
        (double sin, double cos) = Math.SinCos(azimuth);
        LocalPlane plane = new(c1.Center);
        List<PlanarPoint> ring = ConvexMath.LensRing(default, r1, new(d * sin, d * cos), r2, DefaultSegments);
        return Finish(plane, ring, ConvexMath.LensArea(d, r1, r2), new(r1 * sin, r1 * cos));
    }

    private static GeoShape CirclePolygon(GeoCircle c, GeoPolygon polygon)
    {
        LocalPlane plane = new(c.Center);
        PlanarPoint[] ring = new PlanarPoint[polygon.VertexSpan.Length];
        Project(plane, polygon.VertexSpan, ring);
        List<PlanarPoint>? boundary = ConvexMath.CircleRingBoundary(default, c.RadiusMetres, ring, DefaultSegments);
        ConvexMath.DistanceTo(ring, default, out PlanarPoint nearest, BoundaryTolerance);
        if (boundary is null)
        {
            double radiusSquared = c.RadiusMetres * c.RadiusMetres;
            bool polygonInside = true;
            foreach (PlanarPoint vertex in ring)
                polygonInside &= ConvexMath.DistanceSquared(vertex, default) <= radiusSquared;

            if (polygonInside)
                return polygon;

            return ConvexMath.Contains(ring, default, BoundaryTolerance) ? c : new GeoPointShape(plane.Unproject(nearest));
        }

        return Finish(plane, boundary, ConvexMath.CircleRingArea(default, c.RadiusMetres, ring), nearest);
    }

    private static GeoShape PolygonPolygon(GeoPolygon p1, GeoPolygon p2)
    {
        PlanarPoint[] projected = new PlanarPoint[p2.VertexSpan.Length];
        Project(p1.Plane, p2.VertexSpan, projected);
        List<PlanarPoint> ring = ConvexMath.Clip(projected, p1.Ring);
        PlanarPoint fallback = ring.Count > 0 ? ring[0] : p1.Ring[0];
        return Finish(p1.Plane, ring, double.NaN, fallback);
    }

    /// <summary>A polygon from a ring computed in <paramref name="plane"/>, or the touching point when the ring has collapsed.</summary>
    private static GeoShape Finish(LocalPlane plane, List<PlanarPoint> ring, double exactArea, PlanarPoint fallback)
    {
        ConvexMath.RemoveNearDuplicates(ring, VertexTolerance);
        if (ring.Count < 3 || ConvexMath.SignedArea(CollectionsMarshal.AsSpan(ring)) <= 0)
            return new GeoPointShape(plane.Unproject(ring.Count > 0 ? ring[0] : fallback));

        return GeoPolygon.FromTrustedRing(plane, [.. ring], exactArea);
    }

    private static InvalidOperationException Unreachable(GeoShape a, GeoShape b)
        => new($"Unsupported shape pair {a.GetType().Name} and {b.GetType().Name}.");
}
