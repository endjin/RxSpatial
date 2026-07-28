using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapRunRxSpatial
{
    class IntersectObserver : GeoObserver,IObserver<bool>
    {
        public IntersectObserver(ObjMovementHandler objMovementHandler)
            : base(objMovementHandler)
        {
        }

        public virtual void OnNext(bool b)
        {
            /**this is for demo use only,
             * if we have different polygons, 
             * the color will be overridden by the color of the most recently added polygon*/
            if(b==false)
                _objMovementHandler.updatePushpinColor("blue");
            else
                _objMovementHandler.updatePushpinColor("red");
        }
        public virtual void OnCompleted()
        {
        }
        public virtual void OnError(Exception e)
        {
        }
    }
}
