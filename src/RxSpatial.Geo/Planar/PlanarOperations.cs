namespace RxSpatial.Geo;

/// <summary>
/// Exact Euclidean operations on <see cref="PlanarShape"/>s: the planar twin of
/// <see cref="GeoOperations"/>. Boundaries are closed, so shapes that touch intersect.
/// <see cref="Intersects"/>, <see cref="Distance"/>, <see cref="Area"/> and
/// <see cref="Bounds"/> do not allocate. All methods are thread-safe.
/// </summary>
public static class PlanarOperations
{
    /// <summary>
    /// The default number of segments for a full circle, in <see cref="ToVertices"/> and in
    /// the arcs of intersection results: the number SQL Server's <c>STBuffer</c> uses.
    /// </summary>
    public const int DefaultSegments = 128;

    /// <summary>Whether the shapes share at least one point. Always false for <see cref="PlanarEmpty"/>.</summary>
    /// <exception cref="ArgumentNullException">A shape is null.</exception>
    public static bool Intersects(PlanarShape a, PlanarShape b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        Order(ref a, ref b);
        return (a, b) switch
        {
            ({ Kind: ShapeKind.Empty }, _) => false,
            (PlanarPointShape p, PlanarPointShape q) => p.Position == q.Position,
            (PlanarPointShape p, PlanarCircle c) => ConvexMath.DistanceSquared(p.Position, c.Center) <= c.Radius * c.Radius,
            (PlanarPointShape p, PlanarPolygon polygon) => ConvexMath.Contains(polygon.Ring, p.Position),
            (PlanarCircle c1, PlanarCircle c2) => CirclesIntersect(c1, c2),
            (PlanarCircle c, PlanarPolygon polygon) => CircleBounds(c).Overlaps(polygon.Bounds)
                && ConvexMath.DistanceTo(polygon.Ring, c.Center, out _) <= c.Radius,
            (PlanarPolygon p1, PlanarPolygon p2) => p1.Bounds.Overlaps(p2.Bounds) && ConvexMath.Overlaps(p1.Ring, p2.Ring),
            _ => throw Unreachable(a, b),
        };
    }

    /// <summary>
    /// The shortest Euclidean distance between the shapes: 0 when they intersect, and
    /// <see cref="double.NaN"/> when either is <see cref="PlanarEmpty"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">A shape is null.</exception>
    public static double Distance(PlanarShape a, PlanarShape b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        Order(ref a, ref b);
        return (a, b) switch
        {
            ({ Kind: ShapeKind.Empty }, _) => double.NaN,
            (PlanarPointShape p, PlanarPointShape q) => p.Position.DistanceTo(q.Position),
            (PlanarPointShape p, PlanarCircle c) => Math.Max(0, p.Position.DistanceTo(c.Center) - c.Radius),
            (PlanarPointShape p, PlanarPolygon polygon) => ConvexMath.DistanceTo(polygon.Ring, p.Position, out _),
            (PlanarCircle c1, PlanarCircle c2) => Math.Max(0, c1.Center.DistanceTo(c2.Center) - c1.Radius - c2.Radius),
            (PlanarCircle c, PlanarPolygon polygon) => Math.Max(0, ConvexMath.DistanceTo(polygon.Ring, c.Center, out _) - c.Radius),
            (PlanarPolygon p1, PlanarPolygon p2) => ConvexMath.DistanceBetween(p1.Ring, p2.Ring, out _, out _),
            _ => throw Unreachable(a, b),
        };
    }

    /// <summary>
    /// The shapes' common region. Every input is convex, so the result is too: a point, a
    /// circle (when one disc lies inside the other shape), a convex polygon, or
    /// <see cref="PlanarEmpty"/> when the shapes are disjoint. Shapes that only touch give the
    /// touching point. Where a circle bounds the result, the polygon's vertices lie on the
    /// circle no more than 2π/<see cref="DefaultSegments"/> apart, and its
    /// <see cref="Area"/> is the exact area of the curved region.
    /// </summary>
    /// <exception cref="ArgumentNullException">A shape is null.</exception>
    public static PlanarShape Intersection(PlanarShape a, PlanarShape b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (!Intersects(a, b))
            return PlanarEmpty.Instance;

        Order(ref a, ref b);
        return (a, b) switch
        {
            (PlanarPointShape p, _) => p,
            (PlanarCircle c1, PlanarCircle c2) => CircleCircle(c1, c2),
            (PlanarCircle c, PlanarPolygon polygon) => CirclePolygon(c, polygon),
            (PlanarPolygon p1, PlanarPolygon p2) => PolygonPolygon(p1, p2),
            _ => throw Unreachable(a, b),
        };
    }

    /// <summary>The area: πr² for a circle, the polygon's area, and 0 for points and <see cref="PlanarEmpty"/>.</summary>
    /// <exception cref="ArgumentNullException">The shape is null.</exception>
    public static double Area(PlanarShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape switch
        {
            PlanarCircle c => Math.PI * c.Radius * c.Radius,
            PlanarPolygon polygon => polygon.Area,
            _ => 0,
        };
    }

    /// <summary>The axis-aligned bounding rectangle.</summary>
    /// <exception cref="ArgumentNullException">The shape is null.</exception>
    /// <exception cref="InvalidOperationException">The shape is <see cref="PlanarEmpty"/>, which has no bounds.</exception>
    public static PlanarBounds Bounds(PlanarShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape switch
        {
            PlanarPointShape p => new(p.Position.X, p.Position.Y, p.Position.X, p.Position.Y),
            PlanarCircle c => CircleBounds(c),
            PlanarPolygon polygon => polygon.Bounds,
            _ => throw new InvalidOperationException("An empty shape has no bounds."),
        };
    }

    /// <summary>
    /// The shape's outline for drawing or export, counter-clockwise and not closed: no
    /// vertices for <see cref="PlanarEmpty"/>, one for a point, the polygon's vertices, or
    /// <paramref name="segments"/> vertices on a circle, starting due east of its centre.
    /// </summary>
    /// <exception cref="ArgumentNullException">The shape is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="segments"/> is less than 3.</exception>
    public static PlanarPoint[] ToVertices(PlanarShape shape, int segments = DefaultSegments)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentOutOfRangeException.ThrowIfLessThan(segments, 3);
        return shape switch
        {
            PlanarPointShape p => [p.Position],
            PlanarCircle c => ConvexMath.CircleVertices(c.Center, c.Radius, segments),
            PlanarPolygon polygon => polygon.Ring.ToArray(),
            _ => [],
        };
    }

    /// <summary>Swaps the pair so that the first has the lower <see cref="ShapeKind"/>.</summary>
    private static void Order(ref PlanarShape a, ref PlanarShape b)
    {
        if (a.Kind > b.Kind)
            (a, b) = (b, a);
    }

    private static bool CirclesIntersect(PlanarCircle c1, PlanarCircle c2)
    {
        double reach = c1.Radius + c2.Radius;
        return ConvexMath.DistanceSquared(c1.Center, c2.Center) <= reach * reach;
    }

    private static PlanarBounds CircleBounds(PlanarCircle c)
        => new(c.Center.X - c.Radius, c.Center.Y - c.Radius, c.Center.X + c.Radius, c.Center.Y + c.Radius);

    private static PlanarShape CircleCircle(PlanarCircle c1, PlanarCircle c2)
    {
        double d = c1.Center.DistanceTo(c2.Center);
        if (d + Math.Min(c1.Radius, c2.Radius) <= Math.Max(c1.Radius, c2.Radius))
            return c1.Radius <= c2.Radius ? c1 : c2;

        if (d >= c1.Radius + c2.Radius)
            return new PlanarPointShape(Toward(c1.Center, c2.Center, c1.Radius));

        List<PlanarPoint> ring = ConvexMath.LensRing(c1.Center, c1.Radius, c2.Center, c2.Radius, DefaultSegments);
        double area = ConvexMath.LensArea(d, c1.Radius, c2.Radius);
        return Finish(ring, area, Tolerance(Math.Max(c1.Radius, c2.Radius)), Toward(c1.Center, c2.Center, c1.Radius));
    }

    private static PlanarShape CirclePolygon(PlanarCircle c, PlanarPolygon polygon)
    {
        ReadOnlySpan<PlanarPoint> ring = polygon.Ring;
        List<PlanarPoint>? boundary = ConvexMath.CircleRingBoundary(c.Center, c.Radius, ring, DefaultSegments);
        if (boundary is null)
        {
            double radiusSquared = c.Radius * c.Radius;
            bool polygonInside = true;
            foreach (PlanarPoint vertex in ring)
                polygonInside &= ConvexMath.DistanceSquared(vertex, c.Center) <= radiusSquared;

            if (polygonInside)
                return polygon;

            if (ConvexMath.Contains(ring, c.Center))
                return c;

            ConvexMath.DistanceTo(ring, c.Center, out PlanarPoint touch);
            return new PlanarPointShape(touch);
        }

        double area = ConvexMath.CircleRingArea(c.Center, c.Radius, ring);
        ConvexMath.DistanceTo(ring, c.Center, out PlanarPoint fallback);
        return Finish(boundary, area, Tolerance(c.Radius), fallback);
    }

    private static PlanarShape PolygonPolygon(PlanarPolygon p1, PlanarPolygon p2)
    {
        List<PlanarPoint> ring = ConvexMath.Clip(p2.Ring, p1.Ring);
        PlanarBounds b = p1.Bounds;
        double scale = Math.Max(b.MaxX - b.MinX, b.MaxY - b.MinY);
        PlanarPoint fallback = ring.Count > 0 ? ring[0] : p1.Ring[0];
        return Finish(ring, double.NaN, Tolerance(scale), fallback);
    }

    /// <summary>A polygon from a computed ring, or the touching point when the ring has collapsed.</summary>
    private static PlanarShape Finish(List<PlanarPoint> ring, double exactArea, double tolerance, PlanarPoint fallback)
    {
        ConvexMath.RemoveNearDuplicates(ring, tolerance);
        if (ring.Count < 3 || ConvexMath.SignedArea(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(ring)) <= 0)
            return new PlanarPointShape(ring.Count > 0 ? ring[0] : fallback);

        return PlanarPolygon.FromTrustedRing([.. ring], exactArea);
    }

    private static double Tolerance(double scale) => 1e-10 * Math.Max(scale, 1e-300);

    private static PlanarPoint Toward(PlanarPoint from, PlanarPoint to, double distance)
    {
        double d = from.DistanceTo(to);
        return d == 0 ? from : new(from.X + ((to.X - from.X) * distance / d), from.Y + ((to.Y - from.Y) * distance / d));
    }

    private static InvalidOperationException Unreachable(PlanarShape a, PlanarShape b)
        => new($"Unsupported shape pair {a.GetType().Name} and {b.GetType().Name}.");
}
