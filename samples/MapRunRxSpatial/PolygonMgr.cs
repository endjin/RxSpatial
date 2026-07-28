using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;
using Microsoft.Maps.MapControl.WPF;
namespace MapRunRxSpatial
{
    class Polygon
    {
        public string name;
        public MapPolyline polyline; //visual display polyline
        public MapPolygon mapPolygon; //visual display polygon
        public SqlGeography polygon; //backend process
        public Polygon(string name, MapPolyline pl, SqlGeography pg, MapPolygon mpg)
        {
            this.name = name; this.polyline = pl; this.polygon = pg; this.mapPolygon = mpg;
        }
    }
    class PolygonMgr
    {
        public PolygonMgr(Map map)
        {
            _map = map;
        }
        Map _map;
        int addCnter = 0;
        int addLineCnter = 0;
       public static Dictionary<string,Polygon> polygonDict = new Dictionary<string,Polygon>();
       public static Dictionary<string, Polygon> lineDict = new Dictionary<string, Polygon>();

       public Polygon genLine(Location pt1, Location pt2, bool isDash, int strokeThickness )
       {
           if (pt1.Latitude == pt2.Latitude || pt1.Longitude == pt2.Longitude)
               return null;

           SqlGeography polygon;
           string line = String.Format("LINESTRING({0} {1},{2} {3})",
                pt1.Longitude, pt1.Latitude,
                pt2.Longitude, pt2.Latitude);
           polygon = SqlGeography.STLineFromText(new SqlChars(new SqlString(line)), 4326);
          
           MapPolyline polyLine = new MapPolyline();
           polyLine.Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
           if(isDash)
           {
               List<double> dashes = new List<double>();
               dashes.Add(2.0); dashes.Add(8.0);
               polyLine.StrokeDashArray = new System.Windows.Media.DoubleCollection(dashes);
           }
           polyLine.StrokeThickness = strokeThickness;
           polyLine.Opacity = 0.7;
           polyLine.Locations = new LocationCollection() { 
            new Location(pt1.Latitude, pt1.Longitude), 
            new Location(pt2.Latitude,pt2.Longitude)};
           MapPolygon mapPolygon = new MapPolygon();
          // mapPolygon.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
           // polyLine.StrokeThickness = 2;
           mapPolygon.Opacity = 0.3;
           mapPolygon.Locations = new LocationCollection() { 
            new Location(pt1.Latitude, pt1.Longitude), 
           // new Location(pt1.Latitude,pt2.Longitude), 
            new Location(pt2.Latitude,pt2.Longitude),
            //new Location(pt2.Latitude,pt1.Longitude),
            new Location(pt1.Latitude, pt1.Longitude)};
           Polygon p = new Polygon(addCnter.ToString(), polyLine, polygon, mapPolygon);
           return p;
       }

       public Polygon genPolygon(Location pt1, Location pt2)
       {
           if(pt1.Latitude==pt2.Latitude || pt1.Longitude == pt2.Longitude)
               return null;

           SqlGeography polygon;
          //  string msg = String.Format("{0} {1},{2} {3}",
             //   pt1.Latitude, pt1.Longitude, pt2.Latitude, pt2.Longitude);
           // this.textBlock1.Text = msg;
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
            MapPolyline polyLine = new MapPolyline();
            polyLine.Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
            polyLine.StrokeThickness = 2;
            polyLine.Opacity = 0.8;
            polyLine.Locations = new LocationCollection() { 
            new Location(pt1.Latitude, pt1.Longitude), 
            new Location(pt1.Latitude,pt2.Longitude), 
            new Location(pt2.Latitude,pt2.Longitude),
            new Location(pt2.Latitude,pt1.Longitude),
            new Location(pt1.Latitude, pt1.Longitude)};
            MapPolygon mapPolygon = new MapPolygon();
            mapPolygon.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
           // polyLine.StrokeThickness = 2;
            mapPolygon.Opacity = 0.6;
            mapPolygon.Locations = new LocationCollection() { 
            new Location(pt1.Latitude, pt1.Longitude), 
            new Location(pt1.Latitude,pt2.Longitude), 
            new Location(pt2.Latitude,pt2.Longitude),
            new Location(pt2.Latitude,pt1.Longitude),
            new Location(pt1.Latitude, pt1.Longitude)};
            Polygon p = new Polygon(addCnter.ToString(), polyLine, polygon, mapPolygon);

            return p;
           // string msg = String.Format("{0} {1},{2} {3}",
           //   pt1.Latitude, pt1.Longitude, pt2.Latitude, pt2.Longitude);
           // this.textBlock1.Text = msg;
            //long -122.2 lat 47.5,pt2 
            //-122.2 47.6, 
            //-122.3 47.6, pt1            
            //-122.3 47.5, 
            //-122.2 47.5
            //addPolygon()
       }

      /* public int addLine(Location pt1, Location pt2, bool isDash, int strokeThickness )

       {
           Polygon p = genLine(pt1, pt2, isDash, strokeThickness);
           if (p != null)
           {
               addLineCnter++;
               lineDict.Add(addLineCnter.ToString(), p);
               _map.Children.Add(p.polyline);
               _map.Children.Add(p.mapPolygon);
           }
           return addLineCnter;
       }*/

       public int updateLine(int oldLineCnter, Location pt1, Location pt2, bool isDash, int strokeThickness)
       {
           if (lineDict.ContainsKey(oldLineCnter.ToString()))
           {
               Polygon line = lineDict[oldLineCnter.ToString()];
               lineDict.Remove(oldLineCnter.ToString());
               _map.Children.Remove(line.polyline);
           }
           Polygon p = genLine(pt1, pt2, isDash, strokeThickness);
           if (p != null)
           {
               addLineCnter++;
               lineDict.Add(addLineCnter.ToString(), p);
               _map.Children.Add(p.polyline);
           }
           return addLineCnter;
       }

       public Polygon addPolygon(Location pt1, Location pt2)
       {
           Polygon p=genPolygon(pt1,pt2);
           if (p != null)
           {
               polygonDict.Add(addCnter.ToString(), p);
               addCnter++;
               _map.Children.Add(p.polyline);
               _map.Children.Add(p.mapPolygon);
           }
           return p;
       }
       public void clearAllPolygons()
       {
           foreach (Polygon p in polygonDict.Values)
           {
               _map.Children.Remove(p.polyline);
               _map.Children.Remove(p.mapPolygon); 
           }
           //addCnter = 0;
           polygonDict.Clear();

           foreach (Polygon p in lineDict.Values)
           {
               _map.Children.Remove(p.polyline);
           }
           //addLineCnter = 0;
           lineDict.Clear();
       }
    }
}
