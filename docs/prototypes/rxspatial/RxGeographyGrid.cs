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
    class RxGeographyGrid
    {
        private Grid<IObserver<SqlGeography>> grid;
        public IDisposable RxRxIntersect(RxGeography G, IObserver<bool> o)
        {
            IObserver<SqlGeography> observer = new RxRxGeoIntersectObserver(G, o);
            grid.subscribeRxRx(observer, G);
            return new Unsubscriber<SqlGeography>(grid, observer);
        }


        public IDisposable RxRxIntersection(RxGeography G, IObserver<SqlGeography> o)
        {
            IObserver<SqlGeography> observer = new RxRxGeoIntersectionObserver(G, o);
            grid.subscribeRxRx(observer, G);
            return new Unsubscriber<SqlGeography>(grid, observer);
        }

        public IDisposable RxRxDistance(RxGeography G, IObserver<SqlDouble> o)
        {
            IObserver<SqlGeography> observer = new RxRxGeoDistObserver(G, o);
            grid.subscribeRxRx(observer, G);
            return new Unsubscriber<SqlGeography>(grid, observer);
        }

        public IDisposable RxIntersect(SqlGeography G, IObserver<bool> o)
        {
            IObserver<SqlGeography> observer = new RxGeoIntersectObserver(G, o);
            grid.subscribeRx(observer, G);
            return new Unsubscriber<SqlGeography>(grid, observer);
        }

        public IDisposable RxIntersection(SqlGeography G, IObserver<SqlGeography> o)
        {
            IObserver<SqlGeography> observer = new RxGeoIntersectionObserver(G, o);
            grid.subscribeRx(observer, G);
            return new Unsubscriber<SqlGeography>(grid, observer);
        }

        public IDisposable RxDistance(SqlGeography G, IObserver<SqlDouble> o)
        {
            IObserver<SqlGeography> observer = new RxGeoDistObserver(G, o);
            grid.subscribeRx(observer, G);
            return new Unsubscriber<SqlGeography>(grid, observer);
        }
    }
}
