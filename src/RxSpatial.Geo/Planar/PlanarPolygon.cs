using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace RxSpatial.Geo;

/// <summary>
/// A convex polygon with straight edges. Vertices run counter-clockwise and the ring is not
/// closed (the last vertex connects back to the first). Concave and self-intersecting rings
/// are rejected; clockwise rings are reversed.
/// </summary>
public sealed class PlanarPolygon : PlanarShape
{
    private readonly PlanarPoint[] ring;

    private PlanarPolygon(PlanarPoint[] ring, double exactArea)
        : base(ShapeKind.Polygon)
    {
        this.ring = ring;
        Vertices = Array.AsReadOnly(ring);
        Area = double.IsNaN(exactArea) ? ConvexMath.SignedArea(ring) : exactArea;

        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        foreach (PlanarPoint vertex in ring)
        {
            minX = Math.Min(minX, vertex.X);
            minY = Math.Min(minY, vertex.Y);
            maxX = Math.Max(maxX, vertex.X);
            maxY = Math.Max(maxY, vertex.Y);
        }

        Bounds = new(minX, minY, maxX, maxY);
    }

    /// <summary>The vertices, counter-clockwise, without a closing repeat of the first.</summary>
    public IReadOnlyList<PlanarPoint> Vertices { get; }

    internal ReadOnlySpan<PlanarPoint> Ring => ring;

    /// <summary>
    /// The area: the ring's own, or the exact curved region's for a polygon produced by
    /// <see cref="PlanarOperations.Intersection"/> with a circle.
    /// </summary>
    internal double Area { get; }

    internal PlanarBounds Bounds { get; }

    /// <summary>Creates a convex polygon from its vertices, in either winding order.</summary>
    /// <param name="vertices">At least three distinct vertices; a closing repeat of the first is ignored.</param>
    /// <exception cref="ArgumentException">
    /// Fewer than three distinct vertices, a non-finite coordinate, or a ring that has no area,
    /// is concave or intersects itself.
    /// </exception>
    public static PlanarPolygon FromVertices(params ReadOnlySpan<PlanarPoint> vertices)
    {
        List<PlanarPoint> distinct = new(vertices.Length);
        foreach (PlanarPoint vertex in vertices)
        {
            if (!vertex.IsFinite)
                throw new ArgumentException("Coordinates must be finite.", nameof(vertices));
            if (distinct.Count == 0 || distinct[^1] != vertex)
                distinct.Add(vertex);
        }

        while (distinct.Count > 1 && distinct[^1] == distinct[0])
            distinct.RemoveAt(distinct.Count - 1);

        if (distinct.Count < 3)
            throw new ArgumentException("A polygon needs at least three distinct vertices.", nameof(vertices));

        PlanarPoint[] ring = [.. distinct];
        ConvexMath.OrientCounterClockwise(ring, nameof(vertices));
        return new(ring, double.NaN);
    }

    /// <summary>Creates a convex polygon from its vertices, in either winding order.</summary>
    /// <inheritdoc cref="FromVertices(ReadOnlySpan{PlanarPoint})" path="/param|/exception"/>
    public static PlanarPolygon FromVertices(IEnumerable<PlanarPoint> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        return FromVertices([.. vertices]);
    }

    /// <summary>Creates an axis-aligned rectangle; the corners may be given in either order.</summary>
    /// <exception cref="ArgumentException">The rectangle has no area or a coordinate is not finite.</exception>
    public static PlanarPolygon Rectangle(double minX, double minY, double maxX, double maxY)
        => FromVertices([new(minX, minY), new(maxX, minY), new(maxX, maxY), new(minX, maxY)]);

    /// <summary>
    /// Parses a single-ring WKT polygon, <c>POLYGON ((x y, x y, ...))</c>, in the invariant
    /// culture. Either winding order is accepted.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The text is not a single-ring two-dimensional WKT polygon, or the ring is not a valid
    /// convex polygon (see <see cref="FromVertices(ReadOnlySpan{PlanarPoint})"/>).
    /// </exception>
    public static PlanarPolygon FromWkt(string wkt)
    {
        List<(double X, double Y)> pairs = WktPolygon.Parse(wkt, nameof(wkt));
        PlanarPoint[] vertices = [.. pairs.Select(p => new PlanarPoint(p.X, p.Y))];
        return FromVertices(vertices);
    }

    /// <summary>Wraps a ring the kernel has already made convex and counter-clockwise.</summary>
    internal static PlanarPolygon FromTrustedRing(PlanarPoint[] ring, double exactArea) => new(ring, exactArea);

    /// <summary>The polygon as closed-ring WKT in the invariant culture.</summary>
    public override string ToString()
    {
        StringBuilder text = new("POLYGON ((");
        foreach (PlanarPoint vertex in ring)
            text.Append(CultureInfo.InvariantCulture, $"{vertex.X} {vertex.Y}, ");

        return text.Append(CultureInfo.InvariantCulture, $"{ring[0].X} {ring[0].Y}))").ToString();
    }
}
