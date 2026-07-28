using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlTypes;
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
                _objMovementHandler.updatePushpinColor("blue");
            else
            {
                if (distance.Value == 0)
                    _objMovementHandler.updatePushpinColor("red");
                else if (distance.Value < ParamMgr.ObsvDist*4/7) 
                    _objMovementHandler.updatePushpinColor("orange");
               // else if (distance.Value < ParamMgr.ObsvDist*1/3)
                 //   _objMovementHandler.updatePushpinColor("greenyellow");
                else if (distance.Value < ParamMgr.ObsvDist)
                    _objMovementHandler.updatePushpinColor("green");

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
