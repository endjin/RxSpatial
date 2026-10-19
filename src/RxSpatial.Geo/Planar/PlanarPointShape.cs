using static System.FormattableString;

namespace RxSpatial.Geo;

/// <summary>A single position in the plane.</summary>
public sealed class PlanarPointShape : PlanarShape
{
    /// <summary>Creates a point shape.</summary>
    /// <param name="position">The position; both coordinates must be finite.</param>
    /// <exception cref="ArgumentException">A coordinate is NaN or infinite.</exception>
    public PlanarPointShape(PlanarPoint position)
        : base(ShapeKind.Point)
    {
        if (!position.IsFinite)
            throw new ArgumentException("Coordinates must be finite.", nameof(position));

        Position = position;
    }

    /// <summary>Creates a point shape at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public PlanarPointShape(double x, double y)
        : this(new PlanarPoint(x, y))
    {
    }

    /// <summary>The position.</summary>
    public PlanarPoint Position { get; }

    /// <inheritdoc/>
    public override string ToString() => Invariant($"POINT ({Position.X} {Position.Y})");
}
