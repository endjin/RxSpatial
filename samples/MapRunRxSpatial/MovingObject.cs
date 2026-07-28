using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maps.MapControl.WPF;
using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;

using System.Windows.Media;

using System.Windows;

using RxSpatial;

namespace MapRunRxSpatial
{
    class MovingObject
    {
        int id;
        PolygonMgr polygonMgr;
        ObjMovementHandler objMovementHandler; //visualize movement by pushpin
        Dictionary<string, IDisposable> observerIDisposableDict = new Dictionary<string, IDisposable>();

        private ObjMovementObserver objMovementObserver; //receive location feed to trigger visualized movement in objMovementHandler
         Map _baseMap; 
        RxGeography currLoc;
        public RxGeography loc 
        { get { return this.currLoc; } }

        public MovingObject(int id, Map baseMap, PolygonMgr polygonMgr)
        {
            _baseMap = baseMap;
            this.id = id;
            this.polygonMgr = polygonMgr;
            currLoc=new RxGeography();
            this.objMovementHandler = new ObjMovementHandler(id, baseMap, polygonMgr);
            this.objMovementObserver = new ObjMovementObserver(objMovementHandler);
            this.objMovementObserver.Subscribe(currLoc);
            //subscribeFromSettings();
            //subscribeAll();
        }


        public void feedLocation(SqlGeography location)
        {

               // Polygon p = polygonMgr.genPolygon(new Location(location.Lat.Value+0.01,location.Long.Value+0.01),
                 //   new Location(location.Lat.Value-0.01,location.Long.Value-0.01));
                currLoc.OnNext(location);
            
        }
       /* private void subscribeAll()
        {
            int i = 0;
            foreach (string key in SubscribeParams.polygonList.Keys)
            {
                i++;
                if (i <= LoadParams.observerNumber)
                    subscribeIntersect(key, SubscribeParams.polygonList[key]);
                else
                    break;
            }
        }
        private void subscribeFromSettings()
        {
            if (!SubscribeParams.subscriptionList.ContainsKey(id.ToString())
                || (SubscribeParams.subscriptionList[id.ToString()].Count == 0))
            {
                return;
            }
            List<Subscription> subscriptionlist = SubscribeParams.subscriptionList[id.ToString()];
            foreach (Subscription subscription in subscriptionlist)
            {
                if (!SubscribeParams.polygonList.ContainsKey(subscription.Name))
                {
                   // MessageBox.Show("Polygon " + subscription.Name + " does not exist");
                    continue;
                }
                if (subscription.ObserverType.Equals("Intersection"))
                {
                    subscribeIntersection(subscription.Name, SubscribeParams.polygonList[subscription.Name]);
                }
                else if (subscription.ObserverType.Equals("Intersect"))
                {
                    subscribeIntersect(subscription.Name, SubscribeParams.polygonList[subscription.Name]);
                }
                else if (subscription.ObserverType.Equals("Dist"))
                {
                    subscribeDist(subscription.Name, SubscribeParams.polygonList[subscription.Name]);
                }
            }
        }*/

        public void subscribeIntersect(string name, SqlGeography polygon)
        {
            if (!observerIDisposableDict.ContainsKey(name))
            {
               observerIDisposableDict.Add(name, 
                   currLoc.RxIntersect(polygon, new IntersectObserver(objMovementHandler)));
            }
            else
            {
                //Trace err
            }   
        }


        public void subscribeIntersection(string name, SqlGeography polygon)
        {
            if (!observerIDisposableDict.ContainsKey(name))
            {
                observerIDisposableDict.Add(name,
                    currLoc.RxIntersection(polygon, new IntersectionObserver(objMovementHandler)));
            }
            else
            {
                //Trace err
            } 
        }

        public void subscribeDist(string name, SqlGeography polygon)
        {
            if (!observerIDisposableDict.ContainsKey(name))
            {
                observerIDisposableDict.Add(name,
                    currLoc.RxDistance(polygon, new DistanceObserver(objMovementHandler)));
            }
            else
            {
                //Trace err
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
                //Trace err
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
                //Trace err
            }
        }

        public void subscribeRxRxIntersection(string name, RxGeography pt)
        {
            if (!observerIDisposableDict.ContainsKey(name))
            {
                observerIDisposableDict.Add(name,
                    currLoc.RxRxIntersection(pt, new RxRxIntersectionObserver(objMovementHandler)));
               // observerNotifiedFlagDict.Add(name, false);
            }
            else
            {
                //Trace err
            }
        }
        public void unsubscribe(string name)
        {
            if (observerIDisposableDict.ContainsKey(name))
            {
                observerIDisposableDict[name].Dispose();
            }
        }
/*#region ObserverListMgmtDomain
        public IDisposable subscribeIntersect(SqlGeography polygon)
        {
            return newLocation.RxIntersect(polygon, new IntersectObserver(objMovementHandler));
        }
        public void addIntersectObserver(string name, List<SqlGeography> polygonList)
        {
            IntersectObserver intersectObserver = new IntersectObserver(name,polygonList,objMovementHandler);
            intersectObserver.Subscribe(newLocation);
            //Put the locObserver to the end so that every observer could update pushpin info before actually moved;
            objMovementObserver.OnCompleted();
            objMovementObserver.Subscribe(newLocation);

            observers.Add(intersectObserver);
        }

        public void delIntersectObserver(string name)
        {
            foreach (Observer observer in observers)
            {
                if (observer.Type.Equals(Observer.ObserverType.Intersect) && observer.Name.Equals(name))
                {
                    observer.OnCompleted();
                    observers.Remove(observer);
                    return;
                }
            }
        }
#endregion*/
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
