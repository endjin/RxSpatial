using System;
using System.Collections.Generic;

using Microsoft.SqlServer.Types;

using RxSpatial;

namespace MapRunRxSpatial
{
    class MovingObject
    {
        ObjMovementHandler objMovementHandler; //visualize movement on the map
        Dictionary<string, IDisposable> observerIDisposableDict = new Dictionary<string, IDisposable>();

        private ObjMovementObserver objMovementObserver; //receive location feed to trigger visualized movement in objMovementHandler
        RxGeography currLoc;
        public RxGeography loc
        { get { return this.currLoc; } }

        public MovingObject(int id, MapLibreMap baseMap, PolygonMgr polygonMgr)
        {
            currLoc = new RxGeography();
            this.objMovementHandler = new ObjMovementHandler(id, baseMap, polygonMgr);
            this.objMovementObserver = new ObjMovementObserver(objMovementHandler);
            this.objMovementObserver.Subscribe(currLoc);
        }

        public void feedLocation(SqlGeography location)
        {
            currLoc.OnNext(location);
        }

        public void subscribeIntersect(string name, SqlGeography polygon)
        {
            if (!observerIDisposableDict.ContainsKey(name))
            {
               observerIDisposableDict.Add(name,
                   currLoc.RxIntersect(polygon, new IntersectObserver(objMovementHandler)));
            }
        }

        public void subscribeIntersection(string name, SqlGeography polygon)
        {
            if (!observerIDisposableDict.ContainsKey(name))
            {
                observerIDisposableDict.Add(name,
                    currLoc.RxIntersection(polygon, new IntersectionObserver(objMovementHandler)));
            }
        }

        public void subscribeDist(string name, SqlGeography polygon)
        {
            if (!observerIDisposableDict.ContainsKey(name))
            {
                observerIDisposableDict.Add(name,
                    currLoc.RxDistance(polygon, new DistanceObserver(objMovementHandler)));
            }
        }
        public bool subscribeRxRxDist(string name,RxGeography pt)
        {
            if (!observerIDisposableDict.ContainsKey(name))
            {
                observerIDisposableDict.Add(name,
                    currLoc.RxRxDistance(pt, new RxRxDistanceObserver(objMovementHandler)));
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool subscribeRxRxDistConnection(string name, RxGeography pt)
        {
            if (!observerIDisposableDict.ContainsKey(name))
            {
                observerIDisposableDict.Add(name,
                    currLoc.RxRxDistance(pt, new RxRxDistanceConnectionObserver(pt, objMovementHandler)));
                return true;
            }
            else
            {
                return false;
            }
        }

        public void subscribeRxRxIntersection(string name, RxGeography pt)
        {
            if (!observerIDisposableDict.ContainsKey(name))
            {
                observerIDisposableDict.Add(name,
                    currLoc.RxRxIntersection(pt, new RxRxIntersectionObserver(objMovementHandler)));
            }
        }
        public void unsubscribe(string name)
        {
            // Remove the disposed subscription so the name can be subscribed again later.
            if (observerIDisposableDict.TryGetValue(name, out IDisposable subscription))
            {
                subscription.Dispose();
                observerIDisposableDict.Remove(name);
            }
        }

        public void unsubscribeAll()
        {
            var observerDisposables = observerIDisposableDict.Values;
            foreach (IDisposable observerDisposable in observerDisposables)
            {
                observerDisposable.Dispose();
            }
            observerIDisposableDict.Clear();
        }

        public void delObj()
        {
            unsubscribeAll();
            this.objMovementObserver.OnCompleted();
        }

    }
}
