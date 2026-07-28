using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;


using Microsoft.Maps.MapControl.WPF;
using System.Windows.Media;

namespace MapRunRxSpatial
{
    class IntersctObserver : Observer
    {
        private List<SqlGeography> polygonList;
        private string name;
        ObjMovementHandler handler;
        public IntersctObserver(String name, List<SqlGeography> polygonList, ObjMovementHandler handler)
        {
            setName(name);
            this.handler = handler;
            this.polygonList = polygonList;
            setObserverType(ObserverType.Intersect);
        }

        //intersect-red, no intersect or not observed for intersection- blue
         public override void OnNext(SqlGeography loc)
        {
            
            Boolean isIntersect = checkIntersect(loc);
            if (isIntersect)
                handler.updatePushpinColor(Brushes.Red);
            else
                handler.updatePushpinColor(Brushes.Blue);
        }

        public override void OnCompleted()
        {
            base.OnCompleted();
            handler.updatePushpinColor(Brushes.Blue);
        }

        private Boolean checkIntersect(SqlGeography loc)
        {
            foreach (SqlGeography polygon in polygonList)
                if (polygon.STIntersects(loc).IsTrue)
                    return true;
            return false;
        }
    }
}

