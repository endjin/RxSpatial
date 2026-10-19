using RxSpatial.Backends;
using RxSpatial.Geo;
using RxSpatial.Index;

namespace RxSpatial;

/// <summary>
/// Planar operations for <see cref="RxGeometry"/>: the exact Euclidean shape maths of
/// <see cref="PlanarOperations"/>, with rectangles in the shapes' own X and Y.
/// </summary>
internal readonly struct GeometryOperations : ISpatialOperations<PlanarShape>
{
    /// <summary>
    /// The shape's bounds. Throws <see cref="InvalidOperationException"/> for
    /// <see cref="PlanarEmpty"/>, which has none, so an empty location cannot be pushed on the
    /// grid and tree backends.
    /// </summary>
    public static Rect BoundsOf(PlanarShape shape)
    {
        PlanarBounds bounds = PlanarOperations.Bounds(shape);
        return new Rect(bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MaxY);
    }

    public static bool Intersects(PlanarShape a, PlanarShape b) => PlanarOperations.Intersects(a, b);

    public static PlanarShape Intersection(PlanarShape a, PlanarShape b) => PlanarOperations.Intersection(a, b);

    /// <summary>
    /// The Euclidean distance in the shapes' coordinate units, or <see cref="double.NaN"/>
    /// when either shape is empty.
    /// </summary>
    public static double Distance(PlanarShape a, PlanarShape b) => PlanarOperations.Distance(a, b);

    public static GridLayout GridLayoutOf(RxSpatialOptions options) => GridLayout.From(options.PlanarGrid);
}
