using RxSpatial.Backends;
using RxSpatial.Index;

namespace RxSpatial;

/// <summary>
/// The shape-specific primitives of a moving object, supplied by a stateless struct so that
/// <see cref="MovingObjectCore{TShape, TOps}"/>, the query observers and the backends call
/// them directly (JIT-specialised static calls), never through virtual dispatch.
/// </summary>
/// <typeparam name="TShape">The SQL Server spatial type the operations work on.</typeparam>
internal interface ISpatialOperations<TShape>
    where TShape : class
{
    /// <summary>The bounding rectangle the grid and tree backends index a shape by.</summary>
    static abstract Rect BoundsOf(TShape shape);

    /// <summary><c>a.STIntersects(b)</c>; false when SQL Server returns Null.</summary>
    static abstract bool Intersects(TShape a, TShape b);

    /// <summary><c>a.STIntersection(b)</c>, forwarded as-is.</summary>
    static abstract TShape Intersection(TShape a, TShape b);

    /// <summary><c>a.STDistance(b)</c>, or <see cref="double.NaN"/> when SQL Server returns Null.</summary>
    static abstract double Distance(TShape a, TShape b);

    /// <summary>The grid backend's layout for this shape type, from its own options section.</summary>
    static abstract GridLayout GridLayoutOf(RxSpatialOptions options);
}
