using System;
using System.Collections.Generic;
using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;

namespace MapRunRxSpatial
{
    class Polygon
    {
        public string name;
        public string shapeId; //visual display shape on the MapLibre map
        public SqlGeography polygon; //backend process
        public Polygon(string name, string shapeId, SqlGeography pg)
        {
            this.name = name; this.shapeId = shapeId; this.polygon = pg;
        }
    }
    class PolygonMgr
    {
        public PolygonMgr(MapLibreMap map)
        {
            _map = map;
        }
        MapLibreMap _map;
        int addCnter = 0;
        int addLineCnter = 0;
       public static Dictionary<string,Polygon> polygonDict = new Dictionary<string,Polygon>();
       public static Dictionary<string, Polygon> lineDict = new Dictionary<string, Polygon>();

       public Polygon genLine(GeoPoint pt1, GeoPoint pt2, out GeoPoint[] points)
       {
           points = null;
           if (pt1.Latitude == pt2.Latitude || pt1.Longitude == pt2.Longitude)
               return null;

           string line = String.Format("LINESTRING({0} {1},{2} {3})",
                pt1.Longitude, pt1.Latitude,
                pt2.Longitude, pt2.Latitude);
           SqlGeography polygon = SqlGeography.STLineFromText(new SqlChars(new SqlString(line)), 4326);

           points = new[] { pt1, pt2 };
           return new Polygon(addCnter.ToString(), null, polygon);
       }

       public static SqlGeography genPolygon(GeoPoint pt1, GeoPoint pt2)
       {
           if(pt1.Latitude==pt2.Latitude || pt1.Longitude == pt2.Longitude)
               return null;

           SqlGeography polygon;
            if (pt1.Latitude > pt2.Latitude && pt1.Longitude > pt2.Longitude)
            {
                string line = String.Format("POLYGON(({0} {1},{2} {3},{4} {5},{6} {7},{8} {9}))",
                pt1.Longitude, pt2.Latitude,
                pt1.Longitude, pt1.Latitude,
                pt2.Longitude, pt1.Latitude,
                pt2.Longitude, pt2.Latitude,
                pt1.Longitude, pt2.Latitude);
                 polygon = SqlGeography.STPolyFromText(new SqlChars(new SqlString(line)), 4326);
            }
            else if (pt1.Latitude < pt2.Latitude && pt1.Longitude > pt2.Longitude)
            {
                string line = String.Format("POLYGON(({0} {1},{2} {3},{4} {5},{6} {7},{8} {9}))",
                pt1.Longitude, pt1.Latitude,
                pt1.Longitude, pt2.Latitude,
                pt2.Longitude, pt2.Latitude,
                pt2.Longitude, pt1.Latitude,
                pt1.Longitude, pt1.Latitude);
                 polygon = SqlGeography.STPolyFromText(new SqlChars(new SqlString(line)), 4326);
            }
            else if (pt1.Latitude > pt2.Latitude && pt1.Longitude < pt2.Longitude)
            {
                string line = String.Format("POLYGON(({0} {1},{2} {3},{4} {5},{6} {7},{8} {9}))",
                pt2.Longitude, pt2.Latitude,
                pt2.Longitude, pt1.Latitude,
                pt1.Longitude, pt1.Latitude,
                pt1.Longitude, pt2.Latitude,
                pt2.Longitude, pt2.Latitude);
                 polygon = SqlGeography.STPolyFromText(new SqlChars(new SqlString(line)), 4326);
            }
            else if (pt1.Latitude < pt2.Latitude && pt1.Longitude < pt2.Longitude)
            {
                string line = String.Format("POLYGON(({0} {1},{2} {3},{4} {5},{6} {7},{8} {9}))",
                pt2.Longitude, pt1.Latitude,
                pt2.Longitude, pt2.Latitude,
                pt1.Longitude, pt2.Latitude,
                pt1.Longitude, pt1.Latitude,
                pt2.Longitude, pt1.Latitude);
                 polygon = SqlGeography.STPolyFromText(new SqlChars(new SqlString(line)), 4326);
            }
            else
                return null;

            return polygon;
       }

       public int updateLine(int oldLineCnter, GeoPoint pt1, GeoPoint pt2, bool isDash, int strokeThickness)
       {
           if (lineDict.ContainsKey(oldLineCnter.ToString()))
           {
               Polygon line = lineDict[oldLineCnter.ToString()];
               lineDict.Remove(oldLineCnter.ToString());
               _map.RemoveShape(line.shapeId);
           }
           Polygon p = genLine(pt1, pt2, out GeoPoint[] points);
           if (p != null)
           {
               addLineCnter++;
               p.shapeId = "line-" + addLineCnter;
               lineDict.Add(addLineCnter.ToString(), p);
               _map.SetLine(p.shapeId, points, "red", strokeThickness, isDash, 0.7);
           }
           return addLineCnter;
       }

       public Polygon addPolygon(GeoPoint pt1, GeoPoint pt2)
       {
           SqlGeography geography = genPolygon(pt1, pt2);
           if (geography == null)
               return null;

           string name = addCnter.ToString();
           string shapeId = "area-" + name;
           Polygon p = new Polygon(name, shapeId, geography);
           polygonDict.Add(name, p);
           addCnter++;

           GeoPoint[] ring = new[]
           {
               new GeoPoint(pt1.Latitude, pt1.Longitude),
               new GeoPoint(pt1.Latitude, pt2.Longitude),
               new GeoPoint(pt2.Latitude, pt2.Longitude),
               new GeoPoint(pt2.Latitude, pt1.Longitude),
               new GeoPoint(pt1.Latitude, pt1.Longitude),
           };
           _map.SetPolygon(shapeId, ring, "orange", 2, 0.8, "orange", 0.4);
           return p;
       }
       public void clearAllPolygons()
       {
           foreach (Polygon p in polygonDict.Values)
           {
               _map.RemoveShape(p.shapeId);
           }
           polygonDict.Clear();

           foreach (Polygon p in lineDict.Values)
           {
               _map.RemoveShape(p.shapeId);
           }
           lineDict.Clear();
       }
    }
}
