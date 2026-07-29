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
        SqlGeometry box = SqlGeometry.STGeomFromWKB(loc.STAsBinary(), loc.STSrid.Value).STEnvelope();
        SqlGeometry lowerLeft = box.STPointN(1);
        SqlGeometry upperRight = box.STPointN(3);
        return new Rect(lowerLeft.STX.Value, lowerLeft.STY.Value, upperRight.STX.Value, upperRight.STY.Value);
    }

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
