using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.SqlServer.Types;
using System.Windows.Media;

namespace MapRunRxSpatial
{
    class RxRxIntersectionObserver : GeoObserver, IObserver<SqlGeography>
    {
        public RxRxIntersectionObserver(ObjMovementHandler objMovementHandler)
            : base(objMovementHandler)
        {
        }

        public virtual void OnNext(SqlGeography intersection)
        {

            /**this is for demo use only,
             * if we have different polygons, 
             * the color will be overridden by the color of the most recently added polygon*/
   //         if (intersection.STIsEmpty())
     //           _objMovementHandler.updatePushpinColor(Brushes.Blue);
       //     else
         //       _objMovementHandler.updatePushpinColor(Brushes.Wheat);
        }
        public virtual void OnCompleted()
        {
        }
        public virtual void OnError(Exception e)
        {
        }
    }
}
