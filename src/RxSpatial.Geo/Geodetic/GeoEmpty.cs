namespace RxSpatial.Geo;

/// <summary>
/// The shape with no points: what <see cref="GeoOperations.Intersection"/> returns for
/// disjoint shapes. Distances to it are <see cref="double.NaN"/>.
/// </summary>
public sealed class GeoEmpty : GeoShape
{
    private GeoEmpty()
        : base(ShapeKind.Empty)
    {
    }

    /// <summary>The single instance.</summary>
    public static GeoEmpty Instance { get; } = new();

    /// <inheritdoc/>
    public override string ToString() => "POLYGON EMPTY";
}
