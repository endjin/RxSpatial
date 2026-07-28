using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;
using System.Reactive;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections;
using System.Configuration;
using RxSpatial.Util;
using RxSpatial.Lib;

namespace RxSpatial
{
    public class RxGeography : IObserver<SqlGeography>, IObservable<SqlGeography>
    {
        IContainer<IObserver<SqlGeography>> container;

        public SqlGeography loc
        { get { return this.currentLoc; } }
        private Boolean isVisualized;
        //code for perf test
        AvgCalc avgLatencyRecorder = new AvgCalc();
        public double latency
        {
            get { return avgLatencyRecorder.Avg; }
        }


        public RxGeography()
        {
            init();
        }


        #region IObserver
        public virtual void OnNext(SqlGeography g)
        {


            this.currentLoc = g;
            if (this.isVisualized == true)
            {
                queuedPushs.Enqueue(new LocationPush(g));
                NotifyObservers();
            }
            else
            {
                lock (queuedPushs)
                {
                    if (queuedPushs.Count < bufferLimit)
                        queuedPushs.Enqueue(new LocationPush(g));
                    else
                        throw new RxSpatialOverLoadException();
                }
                if (lockTaken == false)
                {
                    t = new Thread(this.NotifyObservers);
                    t.Start();
                }
            }

        }

        public int checkQueuedPushNum()
        {
            return queuedPushs.Count;
        }

        public virtual void OnCompleted()
        {

        }

        public virtual void OnError(Exception e)
        {

        }
        #endregion

        #region IObservable
        public IDisposable Subscribe(IObserver<SqlGeography> observer)
        {
            if (!observersForLocation.Contains(observer))
                observersForLocation.Add(observer);

            return new Unsubscriber<SqlGeography>(observersForLocation, observer);
        }

        public IDisposable RxRxIntersect(RxGeography G, IObserver<bool> o)
        {
            IObserver<SqlGeography> observer = new RxRxGeoIntersectObserver(G, o);
            container.subscribeRxRx(observer, G);
            return new Unsubscriber<SqlGeography>(container, observer);
        }


        public IDisposable RxRxIntersection(RxGeography G, IObserver<SqlGeography> o)
        {
            IObserver<SqlGeography> observer = new RxRxGeoIntersectionObserver(G, o);
            container.subscribeRxRx(observer, G);
            return new Unsubscriber<SqlGeography>(container, observer);
        }

        public IDisposable RxRxDistance(RxGeography G, IObserver<SqlDouble> o)
        {
            IObserver<SqlGeography> observer = new RxRxGeoDistObserver(G, o);
            container.subscribeRxRx(observer, G);
            return new Unsubscriber<SqlGeography>(container, observer);
        }

        public IDisposable RxIntersect(SqlGeography G, IObserver<bool> o)
        {
            IObserver<SqlGeography> observer = new RxGeoIntersectObserver(G, o);
            container.subscribeRx(observer, G);
            return new Unsubscriber<SqlGeography>(container, observer);
        }

        public IDisposable RxIntersection(SqlGeography G, IObserver<SqlGeography> o)
        {
            IObserver<SqlGeography> observer = new RxGeoIntersectionObserver(G, o);
            container.subscribeRx(observer, G);
            return new Unsubscriber<SqlGeography>(container, observer);
        }

        public IDisposable RxDistance(SqlGeography G, IObserver<SqlDouble> o)
        {
            IObserver<SqlGeography> observer = new RxGeoDistObserver(G, o);
            container.subscribeRx(observer, G);
            return new Unsubscriber<SqlGeography>(container, observer);
        }

        private void init()
        {
            LoadAppConfig();
        }
        #endregion

        private SqlGeography currentLoc;
        private Thread t;
        private bool lockTaken = false;
        private Queue<LocationPush> queuedPushs = new Queue<LocationPush>();
        private int bufferLimit;
       /* private RxGeographyDataStruct preferredDataStruct;
        private List<IObserver<SqlGeography>> observers = new List<IObserver<SqlGeography>>();
        private int gridNumHorizontal;
        private int gridNumVertical;
        private Double gridRegionLeftBottomLat;
        private Double gridRegionLeftBottomLong;
        private Double gridRegionUpperRightLat;
        private Double gridRegionUpperRightLong;
        private Grid<IObserver<SqlGeography>> observerMapGrid;*/
        private GeoList<IObserver<SqlGeography>> observersForLocation = new GeoList<IObserver<SqlGeography>>();
        //private Tree<IObserver<SqlGeography>> tree;

        private void NotifyObservers()
        {
            if (this.isVisualized == true)
            {
                LocationPush push = queuedPushs.Dequeue();

                NotifyLocation((SqlGeography)push.content);

                NotifyObserversInSameZone((SqlGeography)push.content);
            }
            else
            {
                lockTaken = true;
                while (queuedPushs.Count > 0)
                {
                    LocationPush push = queuedPushs.Dequeue();
                    NotifyObserversInSameZone((SqlGeography)push.content);
                    push.finishProc();
                    avgLatencyRecorder.recordLatency(push.latency);
                }
                lockTaken = false;
            }
        }



        private static string GetAppConfig(string strKey)
        {
            foreach (string key in ConfigurationManager.AppSettings)
            {
                if (key.Equals(strKey)) return ConfigurationManager.AppSettings[strKey];
            }
            return null;
        }

        private void LoadAppConfig()
        {

            string bufferLimitStr = GetAppConfig("RxSpatialQueueLimit");
            if (bufferLimitStr == null)
                bufferLimit = 1;
            else
                bufferLimit = Convert.ToInt32(bufferLimitStr);

            string isVisualizedStr = GetAppConfig("isVisualized");
            if (isVisualizedStr != null && isVisualizedStr.Equals("true"))
                isVisualized = true;
            else
                isVisualized = false;

            string configDataStruct = GetAppConfig("preferredDataStruct");
            if (configDataStruct != null)
            {
                if (configDataStruct.ToUpper().Equals("HASHMAP"))
                {
                    container = new Grid<IObserver<SqlGeography>>();
                    /*
                    this.preferredDataStruct = RxGeographyDataStruct.hashMap;
                    string hashMapGridNum = GetAppConfig("HashMapGridNum");
                    if (hashMapGridNum != null)
                    {
                        string[] gridNum = hashMapGridNum.Split(',');
                        gridNumHorizontal = Convert.ToInt32(gridNum[0].Trim());
                        gridNumVertical = Convert.ToInt32(gridNum[1].Trim());
                    }
                    else
                    {
                        gridNumHorizontal = 10;
                        gridNumVertical = 10;
                    }
                    string hashMapCoverageRegion = GetAppConfig("HashMapCoverageRegion");
                    if (hashMapCoverageRegion != null)
                    {
                        string[] rect = hashMapCoverageRegion.Split(',');
                        gridRegionLeftBottomLat = Convert.ToDouble(rect[0].Trim());
                        gridRegionLeftBottomLong = Convert.ToDouble(rect[1].Trim());
                        gridRegionUpperRightLat = Convert.ToDouble(rect[2].Trim());
                        gridRegionUpperRightLong = Convert.ToDouble(rect[3].Trim());
                    }
                    else
                    {
                        gridRegionLeftBottomLat = 46;
                        gridRegionLeftBottomLong = -124;
                        gridRegionUpperRightLat = 49;
                        gridRegionUpperRightLong = -120;
                    }*/

                }
                else if (configDataStruct.ToUpper().Equals("RTREE"))
                {
                   /* this.preferredDataStruct = RxGeographyDataStruct.rtree;
                    tree = new Tree<IObserver<SqlGeography>>(RxGeographyDataStruct.rtree);
                    */
                    container = new Tree<IObserver<SqlGeography>>(RxGeographyDataStruct.rtree);
                }
                else if (configDataStruct.ToUpper().Equals("RUMTREE"))
                {
                    /*this.preferredDataStruct = RxGeographyDataStruct.rumtree;
                    tree = new Tree<IObserver<SqlGeography>>(RxGeographyDataStruct.rumtree);
                     */
                    container = new Tree<IObserver<SqlGeography>>(RxGeographyDataStruct.rumtree);
                }
                else
                {
                    container = new GeoList<IObserver<SqlGeography>>();

                   // this.preferredDataStruct = RxGeographyDataStruct.list;
                }
            }
            else
            {
                container = new GeoList<IObserver<SqlGeography>>();
            }
        }

        private void NotifyLocation(SqlGeography loc)
        {

            foreach (var observer in observersForLocation)
                observer.OnNext(loc);
        }


        private void NotifyObserversInSameZone(SqlGeography loc)
        {

           List<IObserver<SqlGeography>> obsvrList = container.getObjsInSameZone(loc);
           if (obsvrList != null)
           {

               foreach (IObserver<SqlGeography> o in obsvrList)
               {
                   o.OnNext(loc);
               }
           }

           /* if (preferredDataStruct == RxGeographyDataStruct.rtree || preferredDataStruct == RxGeographyDataStruct.rumtree)
            {
                List<IObserver<SqlGeography>> obsvrList = tree.getIntersect(loc);
                NotifyLocationObserversInList(loc, obsvrList);
            }
            else if (preferredDataStruct == RxGeographyDataStruct.hashMap)
            {
                List<IObserver<SqlGeography>> observersToProc = observerMapGrid.obsvrListToProc(loc);
                NotifyLocationObserversInList(loc, observersToProc);
            }
            else
            {
                NotifyLocationObserversInList(loc, observers);
            }*/
        }

        /*protected static void NotifyLocationObserversInList(SqlGeography loc, List<IObserver<SqlGeography>> obsvrList)
        {

            if (obsvrList != null)
            {

                foreach (IObserver<SqlGeography> o in obsvrList)
                {
                    o.OnNext(loc);
                }
            }
        }*/


    }

    enum RxGeographyDataStruct
    {
        list, hashMap, rtree, rumtree
    }
}