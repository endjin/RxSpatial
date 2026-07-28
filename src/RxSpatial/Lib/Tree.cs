using Microsoft.SqlServer.Types;

namespace RxSpatial.Lib;

/// <summary>
/// Tree backend: observers are indexed by the bounding box of their geography in either the
/// classic R-tree or the RUM-tree (R-tree with Update Memo).
/// </summary>
internal class Tree<T> : IContainer<T>
    where T : notnull
{
    private readonly Entity _tree;

    public Tree(RxGeographyDataStruct treeType) => _tree = new Entity(treeType);

    public string? dump() => _tree.dumpNode();

    public void subscribeRxRx(T observer, RxGeography G)
    {
        // Preserved behaviour: G.loc is null until the moving object's first OnNext push;
        // subscribing before it has reported a location fails, as it always has.
        _tree.UpdateLocation(G.loc!, observer);
        LocationObsvr locationObsvr = new(_tree, observer);
        G.Subscribe(locationObsvr);
    }

    public void subscribeRx(T observer, SqlGeography loc) => _tree.UpdateLocation(loc, observer);

    public List<T>? getObjsInSameZone(SqlGeography loc) => _tree.getIntersect(loc);

    public bool Remove(T observer) => _tree.Remove(observer);

    public bool Contains(T observer) => _tree.Contains(observer);

    // Tracks a moving observer and updates its indexed position on every location push.
    private class LocationObsvr(Entity tree, T item) : IObserver<SqlGeography>
    {
        public void OnNext(SqlGeography g) => tree.UpdateLocation(g, item);

        public void OnCompleted()
        {
        }

        public void OnError(Exception e)
        {
        }
    }

    private class Entity
    {
        private readonly RxGeographyDataStruct datastruct;
        private readonly RTree.RTree<T>? rtree;
        private readonly RUMTree.RUMTree<T>? rumTree;
        private readonly Dictionary<T, RTree.Rectangle> itemRectMapRtree = new();
        private readonly Dictionary<T, RUMTree.Rectangle> itemRectMapRUMtree = new();

        public Entity(RxGeographyDataStruct datastruct)
        {
            this.datastruct = datastruct;
            if (datastruct == RxGeographyDataStruct.rtree)
                rtree = new RTree.RTree<T>();
            if (datastruct == RxGeographyDataStruct.rumtree)
                rumTree = new RUMTree.RUMTree<T>();
        }

        public bool Remove(T item)
        {
            if (datastruct == RxGeographyDataStruct.rumtree && itemRectMapRUMtree.Remove(item, out RUMTree.Rectangle? oldRumRect))
                return rumTree!.Delete(oldRumRect, item);

            if (datastruct == RxGeographyDataStruct.rtree && itemRectMapRtree.Remove(item, out RTree.Rectangle? oldRect))
                return rtree!.Delete(oldRect, item);

            return true;
        }

        public bool Contains(T item)
            => datastruct == RxGeographyDataStruct.rumtree
                ? itemRectMapRUMtree.ContainsKey(item)
                : itemRectMapRtree.ContainsKey(item);

        public void UpdateLocation(SqlGeography newloc, T item)
        {
            (SqlGeometry lowerLeft, SqlGeometry upperRight) = EnvelopeCorners(newloc);
            if (datastruct == RxGeographyDataStruct.rtree)
                UpdateLocationRtree(item, lowerLeft, upperRight);
            if (datastruct == RxGeographyDataStruct.rumtree)
                UpdateLocationRUMtree(item, lowerLeft, upperRight);
        }

        public List<T>? getIntersect(SqlGeography loc)
        {
            (SqlGeometry lowerLeft, SqlGeometry upperRight) = EnvelopeCorners(loc);
            if (datastruct == RxGeographyDataStruct.rumtree)
            {
                RUMTree.Rectangle rect = new(
                    lowerLeft.STX.Value, lowerLeft.STY.Value, upperRight.STX.Value, upperRight.STY.Value);
                return rumTree!.Intersects(rect);
            }

            if (datastruct == RxGeographyDataStruct.rtree)
            {
                RTree.Rectangle rect = new(
                    lowerLeft.STX.Value, lowerLeft.STY.Value, upperRight.STX.Value, upperRight.STY.Value);
                return rtree!.Intersects(rect);
            }

            return null;
        }

        public string? dumpNode()
            => datastruct switch
            {
                RxGeographyDataStruct.rumtree => rumTree!.Count.ToString(),
                RxGeographyDataStruct.rtree => rtree!.Count.ToString(),
                _ => null,
            };

        private static (SqlGeometry LowerLeft, SqlGeometry UpperRight) EnvelopeCorners(SqlGeography loc)
        {
            SqlGeometry box = SqlGeometry.STGeomFromWKB(loc.STAsBinary(), loc.STSrid.Value).STEnvelope();
            return (box.STPointN(1), box.STPointN(3));
        }

        private void UpdateLocationRUMtree(T item, SqlGeometry lowerLeft, SqlGeometry upperRight)
        {
            if (itemRectMapRUMtree.Remove(item, out RUMTree.Rectangle? oldRect))
                rumTree!.Delete(oldRect, item);

            RUMTree.Rectangle rect = new(
                lowerLeft.STX.Value, lowerLeft.STY.Value, upperRight.STX.Value, upperRight.STY.Value);
            itemRectMapRUMtree.Add(item, rect);
            rumTree!.Add(rect, item);
        }

        private void UpdateLocationRtree(T item, SqlGeometry lowerLeft, SqlGeometry upperRight)
        {
            if (itemRectMapRtree.Remove(item, out RTree.Rectangle? oldRect))
                rtree!.Delete(oldRect, item);

            RTree.Rectangle rect = new(
                lowerLeft.STX.Value, lowerLeft.STY.Value, upperRight.STX.Value, upperRight.STY.Value);
            itemRectMapRtree.Add(item, rect);
            rtree!.Add(rect, item);
        }
    }
}
