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
    abstract class RxGeographyTree
    {
        protected Tree<IObserver<SqlGeography>> tree;
        public IDisposable RxRxIntersect(RxGeography G, IObserver<bool> o)
        {
            IObserver<SqlGeography> observer = new RxRxGeoIntersectObserver(G, o);
                tree.subscribeRxRx(observer, G);
                return new Unsubscriber<SqlGeography>(tree, observer);
        }

        public IDisposable RxRxIntersection(RxGeography G, IObserver<SqlGeography> o)
        {
            IObserver<SqlGeography> observer = new RxRxGeoIntersectionObserver(G, o);
                tree.subscribeRxRx(observer, G);

                return new Unsubscriber<SqlGeography>(tree, observer);
        }

        public IDisposable RxRxDistance(RxGeography G, IObserver<SqlDouble> o)
        {
            IObserver<SqlGeography> observer = new RxRxGeoDistObserver(G, o);
                tree.subscribeRxRx(observer, G);
            return new Unsubscriber<SqlGeography>(tree, observer);
        }

        public IDisposable RxIntersect(SqlGeography G, IObserver<bool> o)
        {
            IObserver<SqlGeography> observer = new RxGeoIntersectObserver(G, o);
            tree.subscribeRx(observer, G);
            return new Unsubscriber<SqlGeography>(tree, observer);
        }

        public IDisposable RxIntersection(SqlGeography G, IObserver<SqlGeography> o)
        {
            IObserver<SqlGeography> observer = new RxGeoIntersectionObserver(G, o);
            tree.subscribeRx(observer, G);
            return new Unsubscriber<SqlGeography>(tree, observer);
        }

        public IDisposable RxDistance(SqlGeography G, IObserver<SqlDouble> o)
        {
            IObserver<SqlGeography> observer = new RxGeoDistObserver(G, o);
            tree.subscribeRx(observer, G);
            return new Unsubscriber<SqlGeography>(tree, observer);
        }
    }
}
