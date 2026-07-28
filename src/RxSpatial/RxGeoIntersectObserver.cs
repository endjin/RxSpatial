using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;

namespace RxSpatial
{
    class RxGeoIntersectObserver : RxGeoObserverContainer<Boolean>, IObserver<SqlGeography>
    {
        public object G
        { get { return this._localG; } }

        public RxGeoIntersectObserver(SqlGeography G, IObserver<Boolean> o):base(o)
        {
            _localG = G;
        }
        public virtual void OnNext(SqlGeography g)
        {
            if (_localG.STIntersects(g).IsTrue)
            {
                _observer.OnNext(true);
            }
            else
                _observer.OnNext(false);
        }
        public virtual void OnCompleted()
        {
        }
        public virtual void OnError(Exception e)
        {
        }
        private SqlGeography _localG;
    }
}
