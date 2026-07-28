using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;

namespace RxSpatial
{
    class RxRxGeoDistObserver : RxRxGeoObserver<SqlDouble>, IObserver<SqlGeography>
    {
    

        public RxRxGeoDistObserver(RxGeography G, IObserver<SqlDouble> o)
            : base(G, o)
        {
            _localG = G;
        }

        public virtual void OnNext(SqlGeography g)
        {
            _observer.OnNext(_localG.loc.STDistance(g));
        }
        public virtual void OnCompleted()
        {
        }

        public virtual void OnError(Exception e)
        {
        }

    }
}
