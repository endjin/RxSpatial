using System.Runtime.CompilerServices;

namespace RxSpatial.Geo;

/// <summary>
/// Exact Euclidean maths over points, circles and convex counter-clockwise rings: the kernel
/// that <see cref="PlanarOperations"/> uses directly and <see cref="GeoOperations"/> uses in
/// a <see cref="LocalPlane"/>. Rings are spans of vertices, not closed (the last vertex
/// connects back to the first). Boundaries are closed: touching counts as inside.
/// </summary>
/// <remarks>
/// Comparisons are written so that NaN coordinates (an unconverged geodesic) make a point
/// count as outside, and distances come out as NaN, rather than reporting a false hit.
/// </remarks>
internal static class ConvexMath
{
    /// <summary>The rounding allowance of a sign test, relative to the size of its terms.</summary>
    private const double RoundingTolerance = 1e-12;

    /// <summary>The smallest turn, in radians, that a user-supplied ring may make the wrong way.</summary>
    private const double ConcavityTolerance = 1e-9;

    /// <summary>Twice the signed area of the triangle (a, b, p): positive when p is left of a→b.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Cross(PlanarPoint a, PlanarPoint b, PlanarPoint p)
        => ((b.X - a.X) * (p.Y - a.Y)) - ((b.Y - a.Y) * (p.X - a.X));

    /// <summary>
    /// Whether p is left of, or on, the directed line a→b, or right of it by no more than
    /// <paramref name="slack"/> (a distance). NaN gives false.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLeftOrOn(PlanarPoint a, PlanarPoint b, PlanarPoint p, double slack = 0)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double left = dx * (p.Y - a.Y);
        double right = dy * (p.X - a.X);
        double allowance = RoundingTolerance * (Math.Abs(left) + Math.Abs(right));
        if (slack > 0)
            allowance += slack * Math.Sqrt((dx * dx) + (dy * dy));

        return left - right >= -allowance;
    }

    /// <summary>
    /// Whether the convex counter-clockwise ring contains p (boundary included, widened by
    /// <paramref name="slack"/>).
    /// </summary>
    public static bool Contains(ReadOnlySpan<PlanarPoint> ring, PlanarPoint p, double slack = 0)
    {
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            if (!IsLeftOrOn(ring[j], ring[i], p, slack))
                return false;
        }

        return true;
    }

    /// <summary>The point of segment a–b closest to p.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PlanarPoint ClosestOnSegment(PlanarPoint a, PlanarPoint b, PlanarPoint p)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared == 0)
            return a;

        double t = Math.Clamp((((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / lengthSquared, 0, 1);
        return new(a.X + (t * dx), a.Y + (t * dy));
    }

    /// <summary>
    /// The distance from p to the convex ring, 0 when the ring contains it, with the closest
    /// point of the ring (p itself when inside). NaN when any coordinate is NaN.
    /// </summary>
    public static double DistanceTo(ReadOnlySpan<PlanarPoint> ring, PlanarPoint p, out PlanarPoint closest, double slack = 0)
    {
        closest = p;
        if (Contains(ring, p, slack))
            return 0;

        double best = double.PositiveInfinity;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            PlanarPoint candidate = ClosestOnSegment(ring[j], ring[i], p);
            double distance = candidate.DistanceTo(p);
            if (double.IsNaN(distance))
                return double.NaN;

            if (distance < best)
            {
                best = distance;
                closest = candidate;
            }
        }

        return best;
    }

    /// <summary>Whether two convex rings share any point (separating axis test over both rings' edges).</summary>
    public static bool Overlaps(ReadOnlySpan<PlanarPoint> a, ReadOnlySpan<PlanarPoint> b, double slack = 0)
        => !HasSeparatingEdge(a, b, slack) && !HasSeparatingEdge(b, a, slack);

    /// <summary>
    /// The distance between two convex rings, 0 when they overlap, with a closest pair of
    /// points (one on each ring).
    /// </summary>
    public static double DistanceBetween(
        ReadOnlySpan<PlanarPoint> a,
        ReadOnlySpan<PlanarPoint> b,
        out PlanarPoint closestOnA,
        out PlanarPoint closestOnB,
        double slack = 0)
    {
        closestOnA = a[0];
        closestOnB = a[0];
        if (Overlaps(a, b, slack))
            return 0;

        double best = double.PositiveInfinity;
        foreach (PlanarPoint vertex in a)
        {
            double distance = DistanceTo(b, vertex, out PlanarPoint onB, slack);
            if (double.IsNaN(distance))
                return double.NaN;

            if (distance < best)
            {
                best = distance;
                closestOnA = vertex;
                closestOnB = onB;
            }
        }

        foreach (PlanarPoint vertex in b)
        {
            double distance = DistanceTo(a, vertex, out PlanarPoint onA, slack);
            if (double.IsNaN(distance))
                return double.NaN;

            if (distance < best)
            {
                best = distance;
                closestOnA = onA;
                closestOnB = vertex;
            }
        }

        return best;
    }

    /// <summary>The signed area of a ring: positive when it runs counter-clockwise.</summary>
    public static double SignedArea(ReadOnlySpan<PlanarPoint> ring)
    {
        double sum = 0;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
            sum += (ring[j].X * ring[i].Y) - (ring[i].X * ring[j].Y);

        return sum / 2;
    }

    /// <summary>
    /// Sutherland–Hodgman: the part of the convex ring <paramref name="subject"/> inside the
    /// convex counter-clockwise ring <paramref name="clip"/>. The result runs counter-clockwise
    /// and may have fewer than three vertices when the rings only touch or are disjoint.
    /// </summary>
    public static List<PlanarPoint> Clip(ReadOnlySpan<PlanarPoint> subject, ReadOnlySpan<PlanarPoint> clip)
    {
        List<PlanarPoint> output = new(subject.Length + clip.Length);
        output.AddRange(subject);
        List<PlanarPoint> input = new(output.Count);
        for (int i = 0, j = clip.Length - 1; i < clip.Length && output.Count > 0; j = i++)
        {
            PlanarPoint a = clip[j];
            PlanarPoint b = clip[i];
            (input, output) = (output, input);
            output.Clear();
            PlanarPoint previous = input[^1];
            bool previousInside = IsLeftOrOn(a, b, previous);
            foreach (PlanarPoint current in input)
            {
                bool currentInside = IsLeftOrOn(a, b, current);
                if (currentInside != previousInside)
                    output.Add(LineCrossing(previous, current, a, b));
                if (currentInside)
                    output.Add(current);

                previous = current;
                previousInside = currentInside;
            }
        }

        return output;
    }

    /// <summary>The area of the intersection of two discs whose centres are <paramref name="d"/> apart.</summary>
    public static double LensArea(double d, double r1, double r2)
    {
        if (d >= r1 + r2)
            return 0;

        if (d <= Math.Abs(r1 - r2))
        {
            double r = Math.Min(r1, r2);
            return Math.PI * r * r;
        }

        double alpha1 = Math.Acos(Math.Clamp(((d * d) + (r1 * r1) - (r2 * r2)) / (2 * d * r1), -1, 1));
        double alpha2 = Math.Acos(Math.Clamp(((d * d) + (r2 * r2) - (r1 * r1)) / (2 * d * r2), -1, 1));
        double kite = Math.Sqrt(Math.Max(0, (-d + r1 + r2) * (d + r1 - r2) * (d - r1 + r2) * (d + r1 + r2)));
        return (r1 * r1 * alpha1) + (r2 * r2 * alpha2) - (0.5 * kite);
    }

    /// <summary>
    /// The vertices of the lens where two overlapping discs intersect (neither containing the
    /// other), counter-clockwise: the arc of disc 1 inside disc 2, then the arc of disc 2
    /// inside disc 1. Arc vertices lie on the circles, no more than 2π/<paramref name="segments"/> apart.
    /// </summary>
    public static List<PlanarPoint> LensRing(PlanarPoint c1, double r1, PlanarPoint c2, double r2, int segments)
    {
        double d = c1.DistanceTo(c2);
        double ux = (c2.X - c1.X) / d;
        double uy = (c2.Y - c1.Y) / d;
        double along = ((d * d) + (r1 * r1) - (r2 * r2)) / (2 * d);
        double axis = Math.Atan2(uy, ux);
        double alpha1 = Math.Acos(Math.Clamp(along / r1, -1, 1));
        double alpha2 = Math.Acos(Math.Clamp((d - along) / r2, -1, 1));

        List<PlanarPoint> ring = new(segments + 2);
        AddArc(ring, c1, r1, axis - alpha1, 2 * alpha1, segments, includeEnds: true);
        AddArc(ring, c2, r2, axis + Math.PI - alpha2, 2 * alpha2, segments, includeEnds: false);
        return ring;
    }

    /// <summary>
    /// The area of the intersection of a disc with a convex counter-clockwise ring: the sum,
    /// over the ring's edges, of the signed area the disc shares with the triangle from its
    /// centre to that edge. Exact (no polygonisation of the circle).
    /// </summary>
    public static double CircleRingArea(PlanarPoint center, double radius, ReadOnlySpan<PlanarPoint> ring)
    {
        double sum = 0;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            PlanarPoint p = new(ring[j].X - center.X, ring[j].Y - center.Y);
            PlanarPoint q = new(ring[i].X - center.X, ring[i].Y - center.Y);
            sum += CircleTriangleArea(p, q, radius);
        }

        return Math.Abs(sum);
    }

    /// <summary>
    /// The boundary of a disc's intersection with a convex counter-clockwise ring that it
    /// crosses: the ring's vertices inside the disc, the crossing points, and arc vertices no
    /// more than 2π/<paramref name="segments"/> apart between each exit and the next entry.
    /// Returns null when the ring's boundary never crosses the circle; then the ring lies
    /// inside the disc, the disc inside the ring, or they are disjoint or only touch.
    /// </summary>
    public static List<PlanarPoint>? CircleRingBoundary(PlanarPoint center, double radius, ReadOnlySpan<PlanarPoint> ring, int segments)
    {
        double radiusSquared = radius * radius;
        List<PlanarPoint> output = new(ring.Length + segments + 2);
        double pendingExit = double.NaN;
        int firstEntryIndex = -1;
        bool crossed = false;

        for (int i = 0; i < ring.Length; i++)
        {
            PlanarPoint p = ring[i];
            PlanarPoint q = ring[(i + 1) % ring.Length];
            bool pInside = DistanceSquared(p, center) <= radiusSquared;
            bool qInside = DistanceSquared(q, center) <= radiusSquared;
            if (pInside)
                output.Add(p);

            // |p + t(q - p) - c|² = r²  →  a t² + 2 b t + c = 0
            double dx = q.X - p.X;
            double dy = q.Y - p.Y;
            double a = (dx * dx) + (dy * dy);
            if (a == 0)
                continue;

            double b = ((p.X - center.X) * dx) + ((p.Y - center.Y) * dy);
            double c = DistanceSquared(p, center) - radiusSquared;
            double discriminant = (b * b) - (a * c);
            if (pInside == qInside && (pInside || discriminant <= 0))
                continue;

            double root = Math.Sqrt(Math.Max(0, discriminant));
            double tEntry = Math.Clamp((-b - root) / a, 0, 1);
            double tExit = Math.Clamp((-b + root) / a, 0, 1);
            if (!pInside && !qInside && !(tEntry > 0 && tExit < 1 && tEntry < tExit))
                continue;

            crossed = true;
            if (!pInside)
            {
                PlanarPoint entry = new(p.X + (tEntry * dx), p.Y + (tEntry * dy));
                if (double.IsNaN(pendingExit))
                {
                    firstEntryIndex = output.Count;
                }
                else
                {
                    AddArcBetween(output, center, radius, pendingExit, AngleOf(entry, center), segments);
                    pendingExit = double.NaN;
                }

                output.Add(entry);
            }

            if (!qInside)
            {
                PlanarPoint exit = new(p.X + (tExit * dx), p.Y + (tExit * dy));
                output.Add(exit);
                pendingExit = AngleOf(exit, center);
            }
        }

        if (!crossed)
            return null;

        if (!double.IsNaN(pendingExit) && firstEntryIndex >= 0)
            AddArcBetween(output, center, radius, pendingExit, AngleOf(output[firstEntryIndex], center), segments);

        return output;
    }

    /// <summary>
    /// Validates a user-supplied ring in place: it must have area, be convex and wind once.
    /// A clockwise ring is reversed. Returns whether it was reversed.
    /// </summary>
    /// <exception cref="ArgumentException">The ring is degenerate, concave or self-intersecting.</exception>
    public static bool OrientCounterClockwise(Span<PlanarPoint> ring, string? paramName)
    {
        // "No area" is relative to the ring's size, so rounding in collinear input cannot pass.
        double area = SignedArea(ring);
        double size = 0;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
            size = Math.Max(size, ring[i].DistanceTo(ring[j]));

        if (!double.IsFinite(area) || Math.Abs(area) <= 1e-9 * size * size)
            throw new ArgumentException("The polygon has no area: its vertices are collinear or coincident.", paramName);

        bool reversed = area < 0;
        if (reversed)
            ring.Reverse();

        double turning = 0;
        int n = ring.Length;
        for (int i = 0; i < n; i++)
        {
            PlanarPoint previous = ring[(i + n - 1) % n];
            PlanarPoint current = ring[i];
            PlanarPoint next = ring[(i + 1) % n];
            double e1x = current.X - previous.X;
            double e1y = current.Y - previous.Y;
            double e2x = next.X - current.X;
            double e2y = next.Y - current.Y;
            double cross = (e1x * e2y) - (e1y * e2x);
            double dot = (e1x * e2x) + (e1y * e2y);
            double lengths = Math.Sqrt(((e1x * e1x) + (e1y * e1y)) * ((e2x * e2x) + (e2y * e2y)));
            if (cross < -ConcavityTolerance * lengths)
                throw new ArgumentException("The polygon is concave: only convex polygons are supported.", paramName);

            turning += Math.Atan2(cross, dot);
        }

        // A convex ring turns through exactly one full circle; a star-shaped self-intersecting
        // ring turns left at every vertex too, but winds more than once.
        if (Math.Abs(turning - (2 * Math.PI)) > 1e-6)
            throw new ArgumentException("The polygon is self-intersecting.", paramName);

        return reversed;
    }

    /// <summary>
    /// Drops consecutive vertices closer than <paramref name="tolerance"/> (including the last
    /// against the first) from a computed ring.
    /// </summary>
    public static void RemoveNearDuplicates(List<PlanarPoint> ring, double tolerance)
    {
        double toleranceSquared = tolerance * tolerance;
        int kept = 0;
        for (int i = 0; i < ring.Count; i++)
        {
            if (kept == 0 || DistanceSquared(ring[i], ring[kept - 1]) > toleranceSquared)
                ring[kept++] = ring[i];
        }

        ring.RemoveRange(kept, ring.Count - kept);
        while (ring.Count > 1 && DistanceSquared(ring[^1], ring[0]) <= toleranceSquared)
            ring.RemoveAt(ring.Count - 1);
    }

    /// <summary>The vertices of a circle, counter-clockwise from due east.</summary>
    public static PlanarPoint[] CircleVertices(PlanarPoint center, double radius, int segments)
    {
        PlanarPoint[] vertices = new PlanarPoint[segments];
        for (int i = 0; i < segments; i++)
        {
            (double sin, double cos) = Math.SinCos(2 * Math.PI * i / segments);
            vertices[i] = new(center.X + (radius * cos), center.Y + (radius * sin));
        }

        return vertices;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DistanceSquared(PlanarPoint a, PlanarPoint b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        return (dx * dx) + (dy * dy);
    }

    private static bool HasSeparatingEdge(ReadOnlySpan<PlanarPoint> edges, ReadOnlySpan<PlanarPoint> other, double slack)
    {
        for (int i = 0, j = edges.Length - 1; i < edges.Length; j = i++)
        {
            bool allOutside = true;
            foreach (PlanarPoint p in other)
            {
                if (IsLeftOrOn(edges[j], edges[i], p, slack))
                {
                    allOutside = false;
                    break;
                }
            }

            if (allOutside)
                return true;
        }

        return false;
    }

    /// <summary>Where segment p–q crosses the infinite line through a and b.</summary>
    private static PlanarPoint LineCrossing(PlanarPoint p, PlanarPoint q, PlanarPoint a, PlanarPoint b)
    {
        double sideP = Cross(a, b, p);
        double sideQ = Cross(a, b, q);
        double denominator = sideP - sideQ;
        if (denominator == 0)
            return p;

        double t = Math.Clamp(sideP / denominator, 0, 1);
        return new(p.X + (t * (q.X - p.X)), p.Y + (t * (q.Y - p.Y)));
    }

    /// <summary>
    /// The signed area shared by a disc centred on the origin and the triangle (origin, p, q):
    /// pieces of p–q inside the circle contribute triangles, pieces outside contribute sectors.
    /// </summary>
    private static double CircleTriangleArea(PlanarPoint p, PlanarPoint q, double radius)
    {
        double dx = q.X - p.X;
        double dy = q.Y - p.Y;
        double a = (dx * dx) + (dy * dy);
        if (a == 0)
            return 0;

        double b = (p.X * dx) + (p.Y * dy);
        double c = (p.X * p.X) + (p.Y * p.Y) - (radius * radius);
        double discriminant = (b * b) - (a * c);
        double t1 = 1;
        double t2 = 1;
        if (discriminant > 0)
        {
            double root = Math.Sqrt(discriminant);
            t1 = Math.Clamp((-b - root) / a, 0, 1);
            t2 = Math.Clamp((-b + root) / a, 0, 1);
        }

        PlanarPoint m1 = new(p.X + (t1 * dx), p.Y + (t1 * dy));
        PlanarPoint m2 = new(p.X + (t2 * dx), p.Y + (t2 * dy));
        return Piece(p, m1, radius) + Piece(m1, m2, radius) + Piece(m2, q, radius);

        static double Piece(PlanarPoint u, PlanarPoint v, double radius)
        {
            double cross = (u.X * v.Y) - (u.Y * v.X);
            if (cross == 0)
                return 0;

            double midX = (u.X + v.X) / 2;
            double midY = (u.Y + v.Y) / 2;
            if ((midX * midX) + (midY * midY) <= radius * radius)
                return cross / 2;

            double dot = (u.X * v.X) + (u.Y * v.Y);
            return radius * radius * Math.Atan2(cross, dot) / 2;
        }
    }

    private static double AngleOf(PlanarPoint p, PlanarPoint center) => Math.Atan2(p.Y - center.Y, p.X - center.X);

    /// <summary>Adds the arc vertices strictly between two angles, sweeping counter-clockwise.</summary>
    private static void AddArcBetween(List<PlanarPoint> output, PlanarPoint center, double radius, double from, double to, int segments)
    {
        double sweep = to - from;
        while (sweep < 0)
            sweep += 2 * Math.PI;
        while (sweep >= 2 * Math.PI)
            sweep -= 2 * Math.PI;

        // An exit and entry that coincide up to rounding sweep either ~0 or ~2π: no arc.
        if (sweep < 1e-12 || sweep > (2 * Math.PI) - 1e-9)
            return;

        AddArc(output, center, radius, from, sweep, segments, includeEnds: false);
    }

    private static void AddArc(List<PlanarPoint> output, PlanarPoint center, double radius, double start, double sweep, int segments, bool includeEnds)
    {
        int steps = Math.Max(1, (int)Math.Ceiling(sweep * segments / (2 * Math.PI)));
        int first = includeEnds ? 0 : 1;
        int last = includeEnds ? steps : steps - 1;
        for (int k = first; k <= last; k++)
        {
            (double sin, double cos) = Math.SinCos(start + (sweep * k / steps));
            output.Add(new(center.X + (radius * cos), center.Y + (radius * sin)));
        }
    }
}
