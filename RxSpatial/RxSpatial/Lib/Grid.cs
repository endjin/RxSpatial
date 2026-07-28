using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;
using RxSpatial.Util;

namespace RxSpatial.Lib
{
    class Grid <T>: IContainer<T>
    {
        private int latNum = 10; 
        private int longNum=10;
        private Double lat1 = 46;//gridRegionLeftBottomLat
        private Double long1 = -124;//gridRegionLeftBottomLong
        private Double lat2 = 49;//gridRegionUpperRightLat
        private Double long2 = -120;//gridRegionUpperRightLong
        private GeoList<T>[,] grid;
        private double latStep; private double longStep;
        Dictionary<T, int> itemGridMap = new Dictionary<T, int>();

        public Grid(){
            loadAppConfig();
            grid = new GeoList<T>[latNum, longNum];
            this.latStep = (lat2 - lat1) / latNum;
            this.longStep = (long2 - long1) / latNum;
            for (int i = 0; i < latNum; i++)
                for (int j = 0; j < longNum; j++)
                {
                    grid[i, j] = new GeoList<T>();
                }
        }

        public String dump()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < latNum; i++)
            {
                for (int j = 0; j < longNum; j++)
                {
                    sb.Append(grid[i, j].Count);
                    sb.Append(" | ");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }


        public void subscribeRxRx(T observer, RxGeography G)
        {
            UpdateLocation(G.loc, observer);
            LocationObsvr locationObsvr = new LocationObsvr(this, observer);
            IDisposable unsubscriber = G.Subscribe(locationObsvr);
        }

        public void subscribeRx(T observer, SqlGeography loc)
        {
            UpdateLocation(loc, observer);
        }

        //Modify the location of actual observer in rtree/rumtree
        private class LocationObsvr : IObserver<SqlGeography>
        {
            Grid<T> grid;
            T item; //corresponding obsvr which is actually stored in R/RUM tree
            public LocationObsvr(Grid<T> grid, T item)
            {
                this.grid = grid;
                this.item = item;
            }
            public virtual void OnNext(SqlGeography g)
            {
                grid.UpdateLocation(g, item);
            }
            public virtual void OnCompleted()
            {
            }

            public virtual void OnError(Exception e)
            {
            }
        }

        public void UpdateLocation(SqlGeography g, T o){
            Remove(o);
            add(g, o);
        }

        public void add(SqlGeography g, T o)
        {
            if (g.STGeometryType().Equals("Point"))
            {
                //for points area
                int latSlot = findLatSlot(g);
                int longSlot = findLongSlot(g);
                grid[latSlot, longSlot].Add(o);
                itemGridMap[o] = latSlot*latNum + longSlot;
            }
            else
            {
                //for malls/schools
                SqlGeometry box = SqlGeometry.STGeomFromWKB(g.STAsBinary(), g.STSrid.Value).STEnvelope();
                SqlGeometry lowerLeft = box.STPointN(1);
                SqlGeography pt1= SqlGeography.STGeomFromText(lowerLeft.MakeValid().STAsText(), lowerLeft.STSrid.Value);
                SqlGeometry upperRight = box.STPointN(3);
               SqlGeography pt2= SqlGeography.STGeomFromText(upperRight.MakeValid().STAsText(), lowerLeft.STSrid.Value);
               int lat1 = findLatSlot(pt1); int long1 = findLongSlot(pt1);
               int lat2 = findLatSlot(pt2); int long2 = findLongSlot(pt2);
               int latSlotMin; int latSlotMax;
                 int longSlotMin; int longSlotMax;
               if (lat1 > lat2) { latSlotMin = lat2; latSlotMax = lat1; }
               else { latSlotMin = lat1; latSlotMax = lat2; }
               if (long1 > long2) { longSlotMin = long2; longSlotMax = long1; }
               else { longSlotMin = long1; longSlotMax = long2; }

                for (int i = latSlotMin; i <= latSlotMax; i++)
                    for (int j = longSlotMin; j <= longSlotMax; j++)
                    {
                        grid[i, j].Add(o);
                    }
            }
        }

        public void remove(T o, SqlGeography g)
        {
                SqlGeometry box = SqlGeometry.STGeomFromWKB(g.STAsBinary(), g.STSrid.Value).STEnvelope();
                SqlGeometry lowerLeft = box.STPointN(1);
                SqlGeography pt1= SqlGeography.STGeomFromText(lowerLeft.MakeValid().STAsText(), lowerLeft.STSrid.Value);
                SqlGeometry upperRight = box.STPointN(3);
               SqlGeography pt2= SqlGeography.STGeomFromText(upperRight.MakeValid().STAsText(), lowerLeft.STSrid.Value);
               int lat1 = findLatSlot(pt1); int long1 = findLongSlot(pt1);
               int lat2 = findLatSlot(pt2); int long2 = findLongSlot(pt2);
               int latSlotMin; int latSlotMax;
                 int longSlotMin; int longSlotMax;
               if (lat1 > lat2) { latSlotMin = lat2; latSlotMax = lat1; }
               else { latSlotMin = lat1; latSlotMax = lat2; }
               if (long1 > long2) { longSlotMin = long2; longSlotMax = long1; }
               else { longSlotMin = long1; longSlotMax = long2; }

                for (int i = latSlotMin; i <= latSlotMax; i++)
                    for (int j = longSlotMin; j <= longSlotMax; j++)
                    {
                        grid[i, j].Remove(o);
                    }
        }

        public bool Remove(T o){
            if(itemGridMap.ContainsKey(o)){
                int gridNum = itemGridMap[o];
                int lat = gridNum/latNum;
                int lon = gridNum%latNum;
                itemGridMap.Remove(o);
                return grid[lat, lon].Remove(o);
            }
            return true;
        }
        public bool Contains(T o)
        {
            return itemGridMap.ContainsKey(o);
        }
        public List<T> getObjsInSameZone(SqlGeography g)
        {
            if (g.STGeometryType().Equals("Point"))
            {
                //for points area
                int latSlot = findLatSlot(g);
                int longSlot = findLongSlot(g);
                //retList.Add(grid[latSlot, longSlot]);
                return grid[latSlot, longSlot];
            }
            else
            {

                GeoList<T> retList = new GeoList<T>();
   
                SqlGeometry box = SqlGeometry.STGeomFromWKB(g.STAsBinary(), g.STSrid.Value).STEnvelope();

                SqlGeometry lowerLeft = box.STPointN(1);
                SqlGeography pt1 = SqlGeography.STGeomFromText(lowerLeft.MakeValid().STAsText(), lowerLeft.STSrid.Value);
                SqlGeometry upperRight = box.STPointN(3);
                SqlGeography pt2 = SqlGeography.STGeomFromText(upperRight.MakeValid().STAsText(), lowerLeft.STSrid.Value);
                int lat1 = findLatSlot(pt1); int long1 = findLongSlot(pt1);
                int lat2 = findLatSlot(pt2); int long2 = findLongSlot(pt2);
                int latSlotMin; int latSlotMax;
                int longSlotMin; int longSlotMax;
                if (lat1 > lat2) { latSlotMin = lat2; latSlotMax = lat1; }
                else { latSlotMin = lat1; latSlotMax = lat2; }
                if (long1 > long2) { longSlotMin = long2; longSlotMax = long1; }
                else { longSlotMin = long1; longSlotMax = long2; }

                for (int i = latSlotMin; i <= latSlotMax; i++)
                    for (int j = longSlotMin; j <= longSlotMax; j++)
                    {
                        retList.AddRange(grid[i, j]);
                    }
                return retList;
            }
        }


        private void loadAppConfig()
        {
            string hashMapGridNum = Config.GetAppConfig("HashMapGridNum");
            if (hashMapGridNum != null)
            {
                string[] gridNum = hashMapGridNum.Split(',');
                latNum = Convert.ToInt32(gridNum[0].Trim());
                longNum = Convert.ToInt32(gridNum[1].Trim());
            }
            else
            {
                latNum = 10;
                longNum = 10;
            }
            string hashMapCoverageRegion = Config.GetAppConfig("HashMapCoverageRegion");
            if (hashMapCoverageRegion != null)
            {
                string[] rect = hashMapCoverageRegion.Split(',');
                lat1 = Convert.ToDouble(rect[0].Trim());
                long1 = Convert.ToDouble(rect[1].Trim());
                lat2 = Convert.ToDouble(rect[2].Trim());
                long2 = Convert.ToDouble(rect[3].Trim());
            }
            else
            {
                lat1 = 46;
                long1 = -124;
                lat2 = 49;
                long2 = -120;
            }
        }

        //for point
        private int findLatSlot(SqlGeography loc){ 
            double x = loc.Lat.Value - lat1;
            int latSlot = (int)(x / this.latStep);
            if (latSlot >= latNum) latSlot = latNum - 1;
            if (latSlot < 0) latSlot = 0;
            return latSlot; 
        }

        private int findLongSlot(SqlGeography loc)
        {
            double y = loc.Long.Value - long1;
            int longSlot = (int)(y / this.longStep);
            if (longSlot >= longNum) longSlot = longNum - 1;
            if (longSlot < 0) longSlot = 0;
            return longSlot;
        }

        private class Index2D
        {
            public int Lat { get { return this.latSlot; } }
            public int Long { get { return this.longSlot; } }
            public Index2D(int latSlot, int longSlot) { this.latSlot = latSlot; this.longSlot = longSlot; }
            private int latSlot; private int longSlot;
        }
    }
}
