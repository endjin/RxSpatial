namespace RxSpatial.Geo;

/// <summary>The closed set of shape kinds, for pair dispatch without type tests.</summary>
internal enum ShapeKind : byte
{
    Empty,
    Point,
    Circle,
    Polygon,
}
