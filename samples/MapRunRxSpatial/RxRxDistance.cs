using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlTypes;
using System.Windows;

using RxSpatial;

namespace MapRunRxSpatial
{
    class RxRxDistanceObserver : GeoObserver, IObserver<SqlDouble>
    {
        public RxRxDistanceObserver(ObjMovementHandler objMovementHandler)
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
                else if (distance.Value < ParamMgr.ObsvDist * 4 / 7)
                    _objMovementHandler.updatePushpinColor("orange");
               // else if (distance.Value < ParamMgr.ObsvDist * 1 / 3)
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


    class RxRxDistanceConnectionObserver : GeoObserver, IObserver<SqlDouble>
    {
        private RxGeography _rxGeoObj;
        public RxRxDistanceConnectionObserver(RxGeography rxGeoObj, ObjMovementHandler objMovementHandler)
            : base(objMovementHandler)
        {
            _rxGeoObj = rxGeoObj;
        }

        public virtual void OnNext(SqlDouble distance)
        {
            // MessageBox.Show("dist");
            //MessageBox.Show(distance.ToString());
            if (distance.Value > ParamMgr.ObsvDist)
            {
                _objMovementHandler.updatePushpinColor("gray");
                _objMovementHandler.updateLine(_rxGeoObj, true, 1);
            }
            else
            {
                if (distance.Value == 0)
                    _objMovementHandler.updatePushpinColor("red");
                else if (distance.Value < ParamMgr.ObsvDist * 4 / 7)
                {
                    _objMovementHandler.updatePushpinColor("orange");
                    _objMovementHandler.updateLine(_rxGeoObj, false, 3);
                }
                // else if (distance.Value < ParamMgr.ObsvDist * 1 / 3)
                //   _objMovementHandler.updatePushpinColor("greenyellow");
                else if (distance.Value < ParamMgr.ObsvDist)
                {
                    _objMovementHandler.updatePushpinColor("green");
                    _objMovementHandler.updateLine(_rxGeoObj, false, 2);
                }

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
