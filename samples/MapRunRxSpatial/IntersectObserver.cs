using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

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
                _objMovementHandler.updatePushpinColor(Brushes.Blue);
            else
                _objMovementHandler.updatePushpinColor(Brushes.Red);
        }
        public virtual void OnCompleted()
        {
        }
        public virtual void OnError(Exception e)
        {
        }
    }
}
