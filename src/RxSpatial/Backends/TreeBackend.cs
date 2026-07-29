using Microsoft.SqlServer.Types;
using RxSpatial.Index;

namespace RxSpatial.Backends;

/// <summary>
/// Tree backend: observers are indexed by the bounding box of their geography in either the
/// R*-tree or the RUM-tree (R-tree with Update Memo) from RxSpatial.Index.
/// </summary>
internal sealed class TreeBackend<T> : ISpatialBackend<T>
    where T : notnull
{
    private readonly ISpatialIndex<T> index;

    public TreeBackend(RxGeographyDataStruct treeType, RxSpatialOptions options)
        => index = treeType == RxGeographyDataStruct.RumTree
            ? new RumTree<T>(new RumTreeOptions { CleaningInterval = options.RumTreeCleaningInterval })
            : new RStarTree<T>();

    public void Subscribe(T observer, RxGeography source)
    {
        // Preserved behaviour: source.Location is null until the moving object's first OnNext
        // push; subscribing before it has reported a location fails, as it always has.
        index.Update(observer, BoundsOf(source.Location!));
        source.Subscribe(new LocationTracker(index, observer));
    }

    public void Subscribe(T observer, SqlGeography location) => index.Update(observer, BoundsOf(location));

    public List<T>? GetObserversInZone(SqlGeography location) => index.Search(BoundsOf(location));

    public bool Remove(T observer) => index.Remove(observer);

    public bool Contains(T observer) => index.Contains(observer);

    /// <summary>Number of indexed observers — diagnostic snapshot.</summary>
    public override string ToString() => index.Count.ToString();

    private static Rect BoundsOf(SqlGeography location)
    {
        if (location.STGeometryType().Value == "Point")
        {
            // Fast path for bare position pushes; also avoids the WKB round-trip.
            double x = location.Long.Value;
            double y = location.Lat.Value;
            return new Rect(x, y, x, y);
        }

        // SQL Server's STEnvelope returns degenerate shapes for degenerate extents:
        // a Point for points, a LineString for axis-aligned lines, else a Polygon.
        SqlGeometry envelope = SqlGeometry.STGeomFromWKB(location.STAsBinary(), location.STSrid.Value).STEnvelope();
        return envelope.STGeometryType().Value switch
        {
            "Point" => CornersToRect(envelope, envelope),
            "LineString" => CornersToRect(envelope.STPointN(1), envelope.STPointN(2)),
            _ => CornersToRect(envelope.STPointN(1), envelope.STPointN(3)),
        };
    }

    private static Rect CornersToRect(SqlGeometry a, SqlGeometry b)
        => new(a.STX.Value, a.STY.Value, b.STX.Value, b.STY.Value);

    // Tracks a moving observer and updates its indexed position on every location push.
    private sealed class LocationTracker(ISpatialIndex<T> index, T observer) : IObserver<SqlGeography>
    {
        public void OnNext(SqlGeography location) => index.Update(observer, BoundsOf(location));

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
