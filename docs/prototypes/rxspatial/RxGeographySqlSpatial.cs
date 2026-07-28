using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;
using RxSpatial.Lib;

namespace RxSpatial
{
    class RxGeographySqlSpatial : RxGeography
    {
        private GeoList<IObserver<SqlGeography>> observers = new GeoList<IObserver<SqlGeography>>();
        public IDisposable RxRxIntersect(RxGeography G, IObserver<bool> o)
        {
            IObserver<SqlGeography> observer = new RxRxGeoIntersectObserver(G, o);
            observers.Add(observer);
            return new Unsubscriber<SqlGeography>(observers, observer);
        }

        public IDisposable RxRxIntersection(RxGeography G, IObserver<SqlGeography> o)
        {
            IObserver<SqlGeography> observer = new RxRxGeoIntersectionObserver(G, o);
            observers.Add(observer);
            return new Unsubscriber<SqlGeography>(observers, observer);
        }

        public IDisposable RxRxDistance(RxGeography G, IObserver<SqlDouble> o)
        {
            IObserver<SqlGeography> observer = new RxRxGeoDistObserver(G, o);
            observers.Add(observer);
            return new Unsubscriber<SqlGeography>(observers, observer);
        }

        public IDisposable RxIntersect(SqlGeography G, IObserver<bool> o)
        {
            IObserver<SqlGeography> observer = new RxGeoIntersectObserver(G, o);
            observers.Add(observer);
            return new Unsubscriber<SqlGeography>(observers, observer);
        }

        public IDisposable RxIntersection(SqlGeography G, IObserver<SqlGeography> o)
        {
            IObserver<SqlGeography> observer = new RxGeoIntersectionObserver(G, o);
            observers.Add(observer);
            return new Unsubscriber<SqlGeography>(observers, observer);
        }

        public IDisposable RxDistance(SqlGeography G, IObserver<SqlDouble> o)
        {
            IObserver<SqlGeography> observer = new RxGeoDistObserver(G, o);
            observers.Add(observer);
            return new Unsubscriber<SqlGeography>(observers, observer);
        }

        protected void NotifyRxObservers(SqlGeography loc)
        {
            NotifyLocationObserversInList(loc, observers);
        }
    }
}
