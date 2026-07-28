using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlTypes;
using System.Windows.Media;
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
                _objMovementHandler.updatePushpinColor(Brushes.Blue);
            else
            {
                if (distance.Value == 0)
                    _objMovementHandler.updatePushpinColor(Brushes.Red);
                else if (distance.Value < ParamMgr.ObsvDist * 4 / 7)
                    _objMovementHandler.updatePushpinColor(Brushes.Orange);
               // else if (distance.Value < ParamMgr.ObsvDist * 1 / 3)
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
                _objMovementHandler.updatePushpinColor(Brushes.Gray);
                _objMovementHandler.updateLine(_rxGeoObj, true, 1);
            }
            else
            {
                if (distance.Value == 0)
                    _objMovementHandler.updatePushpinColor(Brushes.Red);
                else if (distance.Value < ParamMgr.ObsvDist * 4 / 7)
                {
                    _objMovementHandler.updatePushpinColor(Brushes.Orange);
                    _objMovementHandler.updateLine(_rxGeoObj, false, 3);
                }
                // else if (distance.Value < ParamMgr.ObsvDist * 1 / 3)
                //   _objMovementHandler.updatePushpinColor(Brushes.GreenYellow);
                else if (distance.Value < ParamMgr.ObsvDist)
                {
                    _objMovementHandler.updatePushpinColor(Brushes.Green);
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
