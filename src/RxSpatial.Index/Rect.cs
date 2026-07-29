using System.Runtime.CompilerServices;

namespace RxSpatial.Index;

/// <summary>
/// An axis-aligned 2D bounding rectangle with value semantics. Corners passed to the
/// constructor are normalised, so any two opposite corners are accepted. Zero-extent
/// (point) rectangles are valid; NaN coordinates are rejected.
/// </summary>
public readonly record struct Rect
{
    public Rect(double x1, double y1, double x2, double y2)
    {
        if (double.IsNaN(x1) || double.IsNaN(y1) || double.IsNaN(x2) || double.IsNaN(y2))
            throw new ArgumentException("Rectangle coordinates must not be NaN.");

        (MinX, MaxX) = x1 <= x2 ? (x1, x2) : (x2, x1);
        (MinY, MaxY) = y1 <= y2 ? (y1, y2) : (y2, y1);
    }

    public double MinX { get; }

    public double MinY { get; }

    public double MaxX { get; }

    public double MaxY { get; }

    public double Area
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (MaxX - MinX) * (MaxY - MinY);
    }

    /// <summary>The R*-tree margin: the half-perimeter of the rectangle.</summary>
    public double Margin
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (MaxX - MinX) + (MaxY - MinY);
    }

    internal double CenterX
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (MinX + MaxX) * 0.5;
    }

    internal double CenterY
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (MinY + MaxY) * 0.5;
    }

    /// <summary>Closed-interval overlap test: rectangles that merely touch do intersect.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(in Rect other)
        => other.MinX <= MaxX && other.MaxX >= MinX && other.MinY <= MaxY && other.MaxY >= MinY;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(in Rect other)
        => MinX <= other.MinX && MaxX >= other.MaxX && MinY <= other.MinY && MaxY >= other.MaxY;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rect Union(in Rect other)
        => new(
            Math.Min(MinX, other.MinX),
            Math.Min(MinY, other.MinY),
            Math.Max(MaxX, other.MaxX),
            Math.Max(MaxY, other.MaxY));

    /// <summary>The area growth this rectangle would incur to also cover <paramref name="other"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Enlargement(in Rect other) => Union(other).Area - Area;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal double IntersectionArea(in Rect other)
    {
        double width = Math.Min(MaxX, other.MaxX) - Math.Max(MinX, other.MinX);
        if (width <= 0)
            return 0;

        double height = Math.Min(MaxY, other.MaxY) - Math.Max(MinY, other.MinY);
        return height <= 0 ? 0 : width * height;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal double CenterDistanceSquaredTo(in Rect other)
    {
        double dx = CenterX - other.CenterX;
        double dy = CenterY - other.CenterY;
        return (dx * dx) + (dy * dy);
    }
}
