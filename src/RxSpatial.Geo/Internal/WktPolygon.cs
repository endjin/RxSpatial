using System.Globalization;

namespace RxSpatial.Geo;

/// <summary>A minimal reader for single-ring WKT polygons: <c>POLYGON ((a b, a b, ...))</c>.</summary>
internal static class WktPolygon
{
    /// <summary>
    /// The ring's coordinate pairs in file order (the first and second number of each vertex).
    /// The closing vertex, when present, is kept; callers drop repeated vertices.
    /// </summary>
    /// <exception cref="ArgumentException">The text is not a single-ring two-dimensional WKT polygon.</exception>
    public static List<(double First, double Second)> Parse(string wkt, string paramName)
    {
        ArgumentNullException.ThrowIfNull(wkt, paramName);
        ReadOnlySpan<char> text = wkt.AsSpan().Trim();
        const string Keyword = "POLYGON";
        if (!text.StartsWith(Keyword, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Expected WKT of the form POLYGON ((x y, x y, ...)).", paramName);

        text = text[Keyword.Length..].TrimStart();
        if (!text.StartsWith('(') || !text.EndsWith(')'))
            throw new ArgumentException("Expected WKT of the form POLYGON ((x y, x y, ...)).", paramName);

        text = text[1..^1].Trim();
        if (!text.StartsWith('(') || !text.EndsWith(')'))
            throw new ArgumentException("Expected WKT of the form POLYGON ((x y, x y, ...)).", paramName);

        text = text[1..^1];
        if (text.ContainsAny('(', ')'))
            throw new ArgumentException("Polygons with holes or more than one ring are not supported.", paramName);

        List<(double, double)> pairs = [];
        Span<Range> parts = stackalloc Range[3];
        foreach (Range vertexRange in text.Split(','))
        {
            ReadOnlySpan<char> vertex = text[vertexRange].Trim();
            int count = vertex.Split(parts, ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (count != 2
                || !double.TryParse(vertex[parts[0]], NumberStyles.Float, CultureInfo.InvariantCulture, out double first)
                || !double.TryParse(vertex[parts[1]], NumberStyles.Float, CultureInfo.InvariantCulture, out double second))
            {
                throw new ArgumentException($"'{vertex}' is not a two-dimensional WKT coordinate pair.", paramName);
            }

            pairs.Add((first, second));
        }

        return pairs;
    }
}
