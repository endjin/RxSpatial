using Microsoft.SqlServer.Types;
using RxSpatial.Index;

namespace RxSpatial.Lib;

/// <summary>
/// Tree backend: observers are indexed by the bounding box of their geography in either the
/// R*-tree or the RUM-tree (R-tree with Update Memo) from RxSpatial.Index.
/// </summary>
internal class Tree<T> : IContainer<T>
    where T : notnull
{
    private readonly ISpatialIndex<T> _index;

    public Tree(RxGeographyDataStruct treeType, RxSpatialOptions options)
        => _index = treeType == RxGeographyDataStruct.RumTree
            ? new RumTree<T>(new RumTreeOptions { CleaningInterval = options.RumTreeCleaningInterval })
            : new RStarTree<T>();

    public string? dump() => _index.Count.ToString();

    public void subscribeRxRx(T observer, RxGeography G)
    {
        // Preserved behaviour: G.loc is null until the moving object's first OnNext push;
        // subscribing before it has reported a location fails, as it always has.
        _index.Update(observer, BoundsOf(G.loc!));
        G.Subscribe(new LocationObsvr(_index, observer));
    }

    public void subscribeRx(T observer, SqlGeography loc) => _index.Update(observer, BoundsOf(loc));

    public List<T>? getObjsInSameZone(SqlGeography loc) => _index.Search(BoundsOf(loc));

    public bool Remove(T observer) => _index.Remove(observer);

    public bool Contains(T observer) => _index.Contains(observer);

    private static Rect BoundsOf(SqlGeography loc)
    {
        if (loc.STGeometryType().Value == "Point")
        {
            // Fast path for bare position pushes; also avoids the WKB round-trip.
            double x = loc.Long.Value;
            double y = loc.Lat.Value;
            return new Rect(x, y, x, y);
        }

        // SQL Server's STEnvelope returns degenerate shapes for degenerate extents:
        // a Point for points, a LineString for axis-aligned lines, else a Polygon.
        SqlGeometry envelope = SqlGeometry.STGeomFromWKB(loc.STAsBinary(), loc.STSrid.Value).STEnvelope();
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
    private class LocationObsvr(ISpatialIndex<T> index, T item) : IObserver<SqlGeography>
    {
        public void OnNext(SqlGeography g) => index.Update(item, BoundsOf(g));

        public void OnCompleted()
        {
        }

        public void OnError(Exception e)
        {
        }
    }
}
