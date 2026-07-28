using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;
using System.Reactive;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections;
using System.Configuration;
using RTree;
using RUMTree;

namespace RxSpatial.Lib
{
    class Tree<T> : IContainer<T>
    {
        public Tree(RxGeographyDataStruct treeType)
        {
            _tree = new Entity(treeType);
        }
        public string dump()
        {
            return _tree.dumpNode();
        }
        public void subscribeRxRx(T observer, RxGeography G)
        {
            _tree.UpdateLocation(G.loc, observer);
            LocationObsvr locationObsvr = new LocationObsvr(_tree, observer);
            IDisposable unsubscriber= G.Subscribe(locationObsvr);
        }

        public void subscribeRx(T observer, SqlGeography loc)
        {
            _tree.UpdateLocation(loc,observer);
        }

        public List<T> getObjsInSameZone(SqlGeography loc)
        {
            return _tree.getIntersect(loc);
        }

        public bool Remove(T observer)
        {
            return _tree.Remove(observer);
        }

        public bool Contains(T observer)
        {
            return _tree.Contains(observer);
        }

        private List<IObserver<SqlGeography>> observersForLocation = new List<IObserver<SqlGeography>>();
        private Entity _tree;

        //Modify the location of actual observer in rtree/rumtree
        private class LocationObsvr : IObserver<SqlGeography>
        {
            Entity tree;
            T item; //corresponding obsvr which is actually stored in R/RUM tree
            public LocationObsvr(Entity tree, T item)
            {
                this.tree = tree;
                this.item = item;
            }
            public virtual void OnNext(SqlGeography g)
            {
                tree.UpdateLocation(g, item);
            }
            public virtual void OnCompleted()
            {
            }

            public virtual void OnError(Exception e)
            {
            }
        }

        private class Entity
        {
            private RTree<T> rtree = null;
            RUMTree<T> rumTree = null;
            RxGeographyDataStruct datastruct;
            Dictionary<T, RTree.Rectangle> itemRectMapRtree
                = new Dictionary<T, RTree.Rectangle>();
            Dictionary<T, RUMTree.Rectangle> itemRectMapRUMtree
                = new Dictionary<T, RUMTree.Rectangle>();
            public Entity(RxGeographyDataStruct datastruct)
            {
                this.datastruct = datastruct;
                if (datastruct == RxGeographyDataStruct.rtree)
                {
                    rtree = new RTree<T>();
                }
                if (datastruct == RxGeographyDataStruct.rumtree)
                {
                    rumTree = new RUMTree<T>();
                }
            }

            public bool Remove(T item){
                if (itemRectMapRUMtree.ContainsKey(item))
                {
                    RUMTree.Rectangle oldRect = itemRectMapRUMtree[item];
                    itemRectMapRtree.Remove(item);
                   return rumTree.Delete(oldRect, item);
                }
                return true;
            }

            public bool Contains(T item)
            {
                return itemRectMapRUMtree.ContainsKey(item);
            }
            public void UpdateLocation(SqlGeography newloc, T item)
            {
                SqlGeometry box = SqlGeometry.STGeomFromWKB(newloc.STAsBinary(), newloc.STSrid.Value).STEnvelope();
                SqlGeometry lowerLeft = box.STPointN(1);
                SqlGeometry upperRight = box.STPointN(3);
                if (datastruct == RxGeographyDataStruct.rtree)
                {
                    UpdateLocationRtree(item, lowerLeft, upperRight);

                }
                if (datastruct == RxGeographyDataStruct.rumtree)
                {
                    UpdateLocationRUMtree(item, lowerLeft, upperRight);
                }
            }

            private void UpdateLocationRUMtree(T item, SqlGeometry lowerLeft, SqlGeometry upperRight)
            {
                if (itemRectMapRUMtree.ContainsKey(item))
                {
                    RUMTree.Rectangle oldRect = itemRectMapRUMtree[item];
                    itemRectMapRUMtree.Remove(item);

                    rumTree.Delete(oldRect, item);
                }
                RUMTree.Rectangle rect = new RUMTree.Rectangle(lowerLeft.STX.Value, lowerLeft.STY.Value,
                        upperRight.STX.Value, upperRight.STY.Value);
                itemRectMapRUMtree.Add(item, rect);
                rumTree.Add(rect, item);
            }

            private void UpdateLocationRtree(T item, SqlGeometry lowerLeft, SqlGeometry upperRight)
            {
                if (itemRectMapRtree.ContainsKey(item))
                {
                    RTree.Rectangle oldRect = itemRectMapRtree[item];
                    itemRectMapRtree.Remove(item);

                    rtree.Delete(oldRect, item);
                }
                RTree.Rectangle rect = new RTree.Rectangle(lowerLeft.STX.Value, lowerLeft.STY.Value,
                    upperRight.STX.Value, upperRight.STY.Value);
                itemRectMapRtree.Add(item, rect);
                rtree.Add(rect, item);
            }

            public string dumpNode()
            {
                if (datastruct == RxGeographyDataStruct.rumtree)
                {
                    return rumTree.Count.ToString();
                }
                else if (datastruct == RxGeographyDataStruct.rtree)
                {
                    return rtree.Count.ToString();
                }
                else
                    return null;
            }



            public List<T> getIntersect(SqlGeography loc)
            {
                SqlGeometry box = SqlGeometry.STGeomFromWKB(loc.STAsBinary(), loc.STSrid.Value).STEnvelope();
                SqlGeometry lowerLeft = box.STPointN(1);
                SqlGeometry upperRight = box.STPointN(3);
                if (datastruct == RxGeographyDataStruct.rumtree)
                {
                    RUMTree.Rectangle rect = new RUMTree.Rectangle(lowerLeft.STX.Value, lowerLeft.STY.Value,
                            upperRight.STX.Value, upperRight.STY.Value);
                    return rumTree.Intersects(rect);
                }
                if (datastruct == RxGeographyDataStruct.rtree)
                {
                    RTree.Rectangle rect = new RTree.Rectangle(lowerLeft.STX.Value, lowerLeft.STY.Value,
                            upperRight.STX.Value, upperRight.STY.Value);
                    return rtree.Intersects(rect);
                }
                return null;
            }

        }

    }
        

}
