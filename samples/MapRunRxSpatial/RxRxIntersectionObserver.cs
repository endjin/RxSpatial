using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.SqlServer.Types;

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
     //           _objMovementHandler.updatePushpinColor("blue");
       //     else
         //       _objMovementHandler.updatePushpinColor("wheat");
        }
        public virtual void OnCompleted()
        {
        }
        public virtual void OnError(Exception e)
        {
        }
    }
}
