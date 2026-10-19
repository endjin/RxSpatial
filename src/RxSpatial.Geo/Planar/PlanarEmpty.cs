namespace RxSpatial.Geo;

/// <summary>
/// The planar shape with no points: what <see cref="PlanarOperations.Intersection"/> returns
/// for disjoint shapes. Distances to it are <see cref="double.NaN"/>.
/// </summary>
public sealed class PlanarEmpty : PlanarShape
{
    private PlanarEmpty()
        : base(ShapeKind.Empty)
    {
    }

    /// <summary>The single instance.</summary>
    public static PlanarEmpty Instance { get; } = new();

    /// <inheritdoc/>
    public override string ToString() => "POLYGON EMPTY";
}
