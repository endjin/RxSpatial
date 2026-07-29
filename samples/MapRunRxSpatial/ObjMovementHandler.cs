using System;

using Microsoft.SqlServer.Types;

using RxSpatial;

namespace MapRunRxSpatial
{
    class ObjMovementHandler
    {
        protected PolygonMgr polygonMgr;
        public Object id;
        public int lineId=-1;
        public MapLibreMap baseMap;
        private readonly string markerId;
        private string currentColor = "blue";
        private GeoPoint position;
        private bool visible;

        public ObjMovementHandler(Object id, MapLibreMap baseMap, PolygonMgr polygonMgr)
        {
            this.baseMap = baseMap;
            this.id = id;
            this.markerId = "obj-" + id;
            this.polygonMgr = polygonMgr;
        }

        public void updateLine(RxGeography rxGeoObj, bool isDash, int strokeThickness)
        {
            GeoPoint loc2 = this.position;
            SqlGeography loc1 = rxGeoObj.loc;
            lineId = polygonMgr.updateLine(lineId, new GeoPoint(loc1.Lat.Value, loc1.Long.Value),
                 loc2, isDash, strokeThickness);
        }

        /// <summary>
        /// Applies a colour with the original demo's precedence ladder: red beats orange beats
        /// green; gray always wins; blue (and other colours) only reset through move().
        /// </summary>
        public void updatePushpinColor(string color)
        {
            switch (color)
            {
                case "gray":
                case "red":
                    setColor(color);
                    break;
                case "orange":
                    if (currentColor != "red")
                        setColor(color);
                    break;
                case "greenyellow":
                    if (currentColor != "red" && currentColor != "orange")
                        setColor(color);
                    break;
                case "green":
                    if (currentColor != "red" && currentColor != "orange" && currentColor != "greenyellow")
                        setColor(color);
                    break;
            }
        }

        private void setColor(string color)
        {
            currentColor = color;
            if (visible)
                baseMap.SetMarkerColor(markerId, color);
        }

        public void move(SqlGeography newLoc)
        {
           if (newLoc != null)
           {
               position = new GeoPoint(newLoc.Lat.Value, newLoc.Long.Value);
               currentColor = "blue";
               visible = true;
               baseMap.SetMarker(markerId, id.ToString(), position.Latitude, position.Longitude, currentColor);
           }
           else
           {
               visible = false;
               baseMap.RemoveMarker(markerId);
           }
        }

        public void del()
        {
            visible = false;
            baseMap.RemoveMarker(markerId);
            if (lineId != -1)
            {
                // Also remove this vehicle's RxRx connection line, or it would linger
                // on the map after the vehicle is gone.
                polygonMgr.removeLine(lineId);
                lineId = -1;
            }
        }
    }
}
