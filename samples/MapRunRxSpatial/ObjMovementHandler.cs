using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Maps.MapControl.WPF;
using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;
using System.Windows;
using System.Windows.Media;

using RxSpatial;

namespace MapRunRxSpatial
{
    class ObjMovementHandler 
    {
        protected Pushpin pushpin;
        protected PolygonMgr polygonMgr;
        public Object id;
        public int lineId=-1;
        public Map baseMap;
        public ObjMovementHandler(Object id, Map baseMap, PolygonMgr polygonMgr)
        {
            this.baseMap = baseMap;
            this.id = id;
            this.pushpin = new Pushpin();
            this.pushpin.Content = id;
            this.pushpin.Background = Brushes.Blue;
            this.polygonMgr = polygonMgr;
        }

    

        public void updatePushpinId(Object id)
        {
          //  baseMap.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal,
    //new Action(delegate()
    //{
        this.pushpin.Content = id;
    //}));
            
        }

        public void updateLine(RxGeography rxGeoObj, bool isDash, int strokeThickness)
        {
            Location loc2 = this.pushpin.Location;
            SqlGeography loc1 = rxGeoObj.loc;
            lineId = polygonMgr.updateLine(lineId, new Location(loc1.Lat.Value, loc1.Long.Value),
                 new Location(loc2.Latitude, loc2.Longitude), isDash, strokeThickness);
           // if (polygon!=null)
             //   baseMap.Children.Add(polygon.polyline);
        }


        public void updatePushpinColor(Brush color)
        {
         //   baseMap.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal,
    //new Action(delegate()
      //      {
            if (color == Brushes.Gray)
            {
                this.pushpin.Background = color;
            }
            else if(color == Brushes.Red){
                this.pushpin.Background = color;
                return;
            }
            else if(color == Brushes.Orange){
                if(this.pushpin.Background == Brushes.Red){
                    return;
                }else{
                    this.pushpin.Background = color;
                    return;
                } 
            }
            else if (color == Brushes.GreenYellow)
            {
                if ((this.pushpin.Background == Brushes.Red)
                    || (this.pushpin.Background == Brushes.Orange))
                {
                    return;
                }
                else
                {
                    this.pushpin.Background = color;
                    return;
                }
            }
            else if (color == Brushes.Green)
            {
                if ((this.pushpin.Background == Brushes.Red)
                    || (this.pushpin.Background == Brushes.Orange)
                    || (this.pushpin.Background == Brushes.GreenYellow))
                {
                    return;
                }
                else
                {
                    this.pushpin.Background = color;
                    return;
                }
            }
        }

        public void move(SqlGeography newLoc)
        {
       //     baseMap.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal,
    //new Action(delegate(){

           baseMap.Children.Remove(this.pushpin);

           if (newLoc != null)
           {
               this.pushpin.Location =
                    new Location(newLoc.Lat.Value, newLoc.Long.Value);
               baseMap.Children.Add(this.pushpin);
               this.pushpin.Background = Brushes.Blue;
           }
         /*  if (polygon != null && polygon.polyline != null)
               baseMap.Children.Remove(polygon.polyline);
           polygon = new PolygonMgr(baseMap).genPolygon(new Location(newLoc.Lat.Value + 0.01, newLoc.Long.Value + 0.01),
                new Location(newLoc.Lat.Value - 0.01, newLoc.Long.Value - 0.01));
           baseMap.Children.Add(polygon.polyline);*/
      //  }));

            //baseMap.Children.Remove(this.pushpin);
          //this.pushpin.Location = new Location(newLoc.Lat.Value, newLoc.Long.Value);
            //baseMap.Children.Add(this.pushpin);
        }

        public void del()
        {
            // baseMap.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal,
            //new Action(delegate()
            //{
            baseMap.Children.Remove(this.pushpin);

            //if (polygon != null &&polygon.polyline != null)
            //  baseMap.Children.Remove(polygon.polyline);
            //}));
            //}
        }
        ~ObjMovementHandler()
        {
           // baseMap.Children.Remove(this.pushpin);
        }

    }
}
