using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;

namespace RxSpatial
{
    class RxGeoObserverContainer<T>
    {
        protected IObserver<T> _observer;
        public RxGeoObserverContainer(IObserver<T> o)
        {
            _observer = o;
        }
    }
}
