using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlTypes;
using System.Windows.Media;
using System.Windows;
namespace MapRunRxSpatial
{
    class DistanceObserver : GeoObserver,IObserver<SqlDouble>
    {
        public DistanceObserver(ObjMovementHandler objMovementHandler)
            : base(objMovementHandler)
        {
        }

        public virtual void OnNext(SqlDouble distance)
        {
           // MessageBox.Show("dist");
            //MessageBox.Show(distance.ToString());
            if (distance.Value > ParamMgr.ObsvDist)
                _objMovementHandler.updatePushpinColor(Brushes.Blue);
            else
            {
                if (distance.Value == 0)
                    _objMovementHandler.updatePushpinColor(Brushes.Red);
                else if (distance.Value < ParamMgr.ObsvDist*4/7) 
                    _objMovementHandler.updatePushpinColor(Brushes.Orange);
               // else if (distance.Value < ParamMgr.ObsvDist*1/3)
                 //   _objMovementHandler.updatePushpinColor(Brushes.GreenYellow);
                else if (distance.Value < ParamMgr.ObsvDist)
                    _objMovementHandler.updatePushpinColor(Brushes.Green);

            }
        }
        public virtual void OnCompleted()
        {
        }
        public virtual void OnError(Exception e)
        {
        }
    }
}
