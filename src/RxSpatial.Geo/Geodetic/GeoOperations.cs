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
    /// The default number of segments for a full circle, in <see cref="ToVertices(GeoShape, int)"/> and in
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

    /// <summary>
    /// How far, in metres, a polygon's vertices and a position may lie from the polygon's plane
    /// origin for that plane to shortlist edges and decide circle reach: within it, a plane's
    /// distances are within (100 km / R)² ≈ 2.5e-4 of the true ones.
    /// </summary>
    private const double ShortlistReach = 50_000;

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
            (GeoPointShape p, GeoPolygon polygon) => polygon.Bounds.Contains(p.Position) && polygon.ContainsGuarded(p.Position),
            (GeoCircle c1, GeoCircle c2) => c1.Bounds.Overlaps(c2.Bounds) && WithinReach(c1.Center, c2.Center, c1.RadiusMetres + c2.RadiusMetres),
            (GeoCircle c, GeoPolygon polygon) => c.Bounds.Overlaps(polygon.Bounds) && CircleReachesPolygon(c, polygon),
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
        GeoPoint[] outline = new GeoPoint[VertexCount(shape, segments)];
        WriteVertices(shape, segments, outline);
        return outline;
    }

    /// <summary>
    /// Writes the shape's outline (see <see cref="ToVertices(GeoShape, int)"/>) into
    /// <paramref name="destination"/> without allocating, for callers that redraw a shape on
    /// every update.
    /// </summary>
    /// <returns>The number of vertices written: 0 for <see cref="GeoEmpty"/>, 1 for a point, the polygon's vertex count, or <paramref name="segments"/> for a circle.</returns>
    /// <exception cref="ArgumentNullException">The shape is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="segments"/> is less than 3.</exception>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than the outline.</exception>
    public static int ToVertices(GeoShape shape, Span<GeoPoint> destination, int segments = DefaultSegments)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentOutOfRangeException.ThrowIfLessThan(segments, 3);
        int count = VertexCount(shape, segments);
        if (destination.Length < count)
            throw new ArgumentException($"The outline has {count} vertices; the destination holds {destination.Length}.", nameof(destination));

        WriteVertices(shape, segments, destination);
        return count;
    }

    private static int VertexCount(GeoShape shape, int segments) => shape switch
    {
        GeoPointShape => 1,
        GeoCircle => segments,
        GeoPolygon polygon => polygon.VertexSpan.Length,
        _ => 0,
    };

    private static void WriteVertices(GeoShape shape, int segments, Span<GeoPoint> destination)
    {
        switch (shape)
        {
            case GeoPointShape p:
                destination[0] = p.Position;
                break;
            case GeoCircle c:
                // Every vertex starts from the centre: work out its reduced latitude once.
                ReducedPoint center = new(c.Center);
                for (int i = 0; i < segments; i++)
                {
                    // Azimuths run clockwise from north; counter-clockwise from east is 90° − θ.
                    double azimuth = (Math.PI / 2) - (2 * Math.PI * i / segments);
                    destination[i] = Wgs84.DirectRadians(center, azimuth, c.RadiusMetres);
                }

                break;
            case GeoPolygon polygon:
                polygon.VertexSpan.CopyTo(destination);
                break;
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
        => polygon.Contains(point, out PlanarPoint local) ? 0 : DistanceFromOutside(polygon, point, local);

    /// <summary>
    /// Whether a circle reaches a polygon: <c>DistanceToPolygon(polygon, c.Center) &lt;= r</c>,
    /// decided in the polygon's own plane when that is clear of the radius by more than the
    /// plane can be wrong (see <see cref="TryPlaneDistance"/>), which spares projecting every
    /// vertex into the centre's plane. Same answer as the exact test.
    /// </summary>
    private static bool CircleReachesPolygon(GeoCircle c, GeoPolygon polygon)
    {
        if (polygon.Contains(c.Center, out PlanarPoint local))
            return true;

        if (TryPlaneDistance(polygon, local, out double approximate, out double slack))
        {
            if (approximate - slack > c.RadiusMetres)
                return false;
            if (approximate + slack < c.RadiusMetres)
                return true;
        }

        return DistanceFromOutside(polygon, c.Center, local) <= c.RadiusMetres;
    }

    /// <summary>
    /// The plane distance from <paramref name="local"/> (a position projected into the
    /// polygon's plane) to the polygon's nearest edge, and a slack that bounds how far that can
    /// differ from the exact distance (and from the same distance measured in the position's own
    /// plane): the distortion of both planes, at most (s/R)² relative within
    /// <see cref="ShortlistReach"/>, plus the bow of the geodesic edges in both. False where
    /// that bound is not established (a large polygon, a far position, or one within the slack
    /// of the boundary), and then the callers measure every edge.
    /// </summary>
    private static bool TryPlaneDistance(GeoPolygon polygon, PlanarPoint local, out double distance, out double slack)
    {
        distance = double.NaN;
        slack = double.NaN;
        double fromOrigin = local.Length;
        if (!(polygon.Reach <= ShortlistReach && fromOrigin <= ShortlistReach))
            return false; // NaN fails too

        ReadOnlySpan<PlanarPoint> ring = polygon.Ring;
        double nearest = double.PositiveInfinity;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
            nearest = Math.Min(nearest, ConvexMath.ClosestOnSegment(ring[j], ring[i], local).DistanceTo(local));

        // In the position's plane the edges are up to |local| + reach away and bow by at most
        // 2ρL²/R², as in the polygon's own EdgeBow; 1e-3 relative is ~16× the (s/R)² distortion
        // at 100 km, and 1 cm covers the exact path's own millimetre refinement threshold.
        double longest = polygon.LongestEdge;
        double pointBow = 2 * (fromOrigin + polygon.Reach) * longest * longest / EarthRadiusSquared;
        double bound = (2 * (polygon.EdgeBow + pointBow)) + (1e-3 * nearest) + 0.01;
        if (!(nearest > bound + 1))
            return false; // near the boundary: the exact path also decides touching

        distance = nearest;
        slack = bound;
        return true;
    }

    /// <summary>
    /// The distance from a position outside the polygon (see
    /// <see cref="GeoPolygon.Contains(GeoPoint, out PlanarPoint)"/>, which gives
    /// <paramref name="local"/>). The polygon is projected into a plane centred on the
    /// position, where distances from the position are exact, and the nearest edge is refined
    /// on the true geodesic when its bow could matter. Where <see cref="TryPlaneDistance"/>
    /// bounds the polygon plane's error, only the edges it cannot rule out (those within twice
    /// the slack of the nearest) are projected and measured, in the same order: the nearest
    /// edge, and so the result, is the one measuring every edge would give.
    /// </summary>
    private static double DistanceFromOutside(GeoPolygon polygon, GeoPoint point, PlanarPoint local)
    {
        ReadOnlySpan<GeoPoint> vertices = polygon.VertexSpan;
        ReadOnlySpan<PlanarPoint> ring = polygon.Ring;
        bool shortlist = TryPlaneDistance(polygon, local, out double nearest, out double slack);
        double limit = nearest + (2 * slack);
        PlanarPoint[]? rented = null;
        Span<PlanarPoint> projected = vertices.Length <= StackVertices
            ? stackalloc PlanarPoint[StackVertices]
            : (rented = ArrayPool<PlanarPoint>.Shared.Rent(vertices.Length));
        try
        {
            projected = projected[..vertices.Length];
            ReducedPoint origin = shortlist ? new(point) : default;
            if (shortlist)
            {
                // Projected on demand; a projection within ShortlistReach always converges, so NaN marks "not yet".
                projected.Fill(new(double.NaN, double.NaN));
            }
            else
            {
                new LocalPlane(point).Project(vertices, projected);
                if (ConvexMath.Contains(projected, default, BoundaryTolerance))
                    return 0;
            }

            double best = double.PositiveInfinity;
            int nearestEdge = 0;
            PlanarPoint foot = default;
            for (int i = 0, j = projected.Length - 1; i < projected.Length; j = i++)
            {
                if (shortlist)
                {
                    if (ConvexMath.ClosestOnSegment(ring[j], ring[i], local).DistanceTo(local) > limit)
                        continue;

                    ProjectOnDemand(projected, vertices, origin, j);
                    ProjectOnDemand(projected, vertices, origin, i);
                }

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

        static void ProjectOnDemand(Span<PlanarPoint> projected, ReadOnlySpan<GeoPoint> vertices, in ReducedPoint origin, int index)
        {
            if (double.IsNaN(projected[index].X))
                projected[index] = LocalPlane.Project(origin, vertices[index]);
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
        plane.Project(vertices, projected);
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
