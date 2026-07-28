using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;

namespace MapRunRxSpatial
{
    abstract class GeoObserver
    {
        protected ObjMovementHandler _objMovementHandler;
        public GeoObserver(ObjMovementHandler objMovementHandler)
        {
            _objMovementHandler = objMovementHandler;
        }
    }
}
