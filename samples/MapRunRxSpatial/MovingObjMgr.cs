using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Threading;

using Microsoft.SqlServer.Types;

namespace MapRunRxSpatial
{
    class MovingObjMgr
    {
        Dictionary<int, MovingObject> movingObjList = new Dictionary<int, MovingObject>();
        public MapLibreMap baseMap;
        private PolygonMgr polygonMgr;
        private DispatcherTimer dispatcherTimer;

        public MovingObjMgr(MapLibreMap map, PolygonMgr polygonMgr)
        {
            baseMap = map;
            this.polygonMgr = polygonMgr;
            initMovingObjActionList();
        }

        public bool TrafficStarted { get; private set; }

        #region ObjectActionDefinition
        enum Operation { Add, Modify, Delete, NA };
        class ObjAction
        {
            private int _id; private Operation _operation; private SqlGeography _loc;
            public ObjAction(int id, string operation, SqlGeography loc)
            {
                this._id = id;
                this._loc = loc;
                if (operation.StartsWith("n")) this._operation = Operation.Add;
                else if (operation.StartsWith("p")) this._operation = Operation.Modify;
                else if (operation.StartsWith("d")) this._operation = Operation.Delete;
                else this._operation = Operation.NA;
            }
            public int id { get { return this._id; } }
            public SqlGeography loc { get { return this._loc; } }
            public Operation operation { get { return this._operation; } }
        }
        class ObjActionArray
        {
            List<ObjAction> _arr;
            int len;
            public int currIdx;
            public ObjActionArray(List<ObjAction> arr)
            {
                _arr = arr;
                len = _arr.Count;
                currIdx = 0;
            }
            public ObjAction getNext(){
                if(currIdx==len) currIdx=1;
                return _arr[currIdx++];
            }
            public void reset()
            {
                currIdx = 0;
            }
        }
        #endregion
        ObjActionArray[] objActionArrayList = new ObjActionArray[20];
        private void initMovingObjActionList()
        {
            for (int i = 0; i < 20; i++)
            {
                List<ObjAction> arr = new List<ObjAction>();
                using (TextReader tr = new StreamReader(Parameters.dataPath + i.ToString() + ".txt"))
                {
                    tr.ReadLine();
                    tr.ReadLine();
                    for (string line = tr.ReadLine(); !string.IsNullOrEmpty(line); line = tr.ReadLine())
                    {
                        string[] strs = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        if (strs.Length != 5)
                            continue;
                        string status = strs[2];
                        double latitude = Convert.ToDouble(strs[3]);
                        double longitude = Convert.ToDouble(strs[4]);
                        arr.Add(new ObjAction(i, status, SqlGeography.Point(latitude, longitude, 4326)));
                    }
                }
                objActionArrayList[i] = new ObjActionArray(arr);
            }
        }

        public void procTraffic()
        {
            // Stop any running timer first: repeated Start clicks must not stack timers
            // (each stacked timer would multiply the traffic speed).
            stopTraffic();
            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += updateTraffic;
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(ParamMgr.MovementIntervalInMS);
            dispatcherTimer.Start();
            TrafficStarted = true;
        }
        public void stopTraffic()
        {
            if (dispatcherTimer != null)
                dispatcherTimer.Stop();
        }
        public void restartProcTraffic()
        {
            if (TrafficStarted)
                procTraffic();
        }
        int vehicleNumberPrev=-1;
        private void updateTraffic(object sender, EventArgs e)
        {
            if(vehicleNumberPrev==-1){
               vehicleNumberPrev = ParamMgr.VehicleNumber;
            }
            for (int i = 0; i < ParamMgr.VehicleNumber;i++)
            {
                updateNewLoc(objActionArrayList[i].getNext());
            }
            if (vehicleNumberPrev > ParamMgr.VehicleNumber)
            {
                for (int i = ParamMgr.VehicleNumber; i < vehicleNumberPrev;i++ )
                {
                    objActionArrayList[i].reset();
                    updateNewLoc(new ObjAction(i,"disappear",null));
                }
            }
            vehicleNumberPrev = ParamMgr.VehicleNumber;
        }
        private void updateNewLoc(ObjAction action)
        {
            switch (action.operation)
            {
                case Operation.Add:
                    addItem(action.id, action.loc);
                    break;
                case Operation.Modify:
                    updateItem(action.id, action.loc);
                    break;
                case Operation.Delete:
                    delItem(action.id);
                    break;
                default:
                    break;
            }
        }
        private void addItem(int id, SqlGeography loc)
        {
            // A trajectory can replay an add for a vehicle that already exists; treat it
            // as a position update instead of crashing on the duplicate dictionary key.
            if (movingObjList.ContainsKey(id))
            {
                updateItem(id, loc);
                return;
            }

            MovingObject newItem = new MovingObject(id, baseMap, polygonMgr);
            newItem.feedLocation(loc);
            movingObjList.Add(id, newItem);
            foreach(Polygon p in polygonMgr.polygonDict.Values){
                if (ParamMgr.subscribeState == STATE.INTERSCT)
                {
                    newItem.subscribeIntersect(p.name, p.polygon);
                }
                else if (ParamMgr.subscribeState == STATE.DIST)
                    newItem.subscribeDist(p.name, p.polygon);
            }
        }


        private void updateItem(int id, SqlGeography loc)
        {
            if (movingObjList.ContainsKey(id))
            {
                MovingObject currItem = movingObjList[id];
                currItem.feedLocation(loc);
            }
        }

        private void delItem(int id)
        {
            if (movingObjList.ContainsKey(id))
            {
                movingObjList[id].delObj();
                movingObjList.Remove(id);
            }
        }

        public void subscribeIntersectNewPolygon(string name,SqlGeography polygon){
            foreach (MovingObject obj in movingObjList.Values)
            {
                obj.subscribeIntersect(name,polygon);
            }
        }

        public void subscribeDistNewPolygon(string name, SqlGeography polygon)
        {
            foreach (MovingObject obj in movingObjList.Values)
            {
                obj.subscribeDist(name, polygon);
            }
        }

        public bool subscribeRxRxDist(int id1, int id2)
        {
            if ((id1 >= 0) && (id1 <= movingObjList.Count - 1) && (id2 >= 0) && (id2 <= movingObjList.Count - 1))
                return movingObjList[id1].subscribeRxRxDist(id2.ToString(), movingObjList[id2].loc);
            else
                return false;
        }

        public bool subscribeRxRxDistConnection(int id1, int id2)
        {
            if ((id1 >= 0) && (id1 <= movingObjList.Count - 1) && (id2 >= 0) && (id2 <= movingObjList.Count - 1))
                return movingObjList[id1].subscribeRxRxDistConnection(id2.ToString(), movingObjList[id2].loc);
            else
                return false;
        }

        public void unsubscribeAll()
        {
            foreach (MovingObject obj in movingObjList.Values)
            {
                obj.unsubscribeAll();
            }
        }

        /// <summary>
        /// Removes every vehicle so the next traffic tick recreates them - used when the
        /// active tab changes the spatial backend, which is baked into each RxGeography
        /// at construction.
        /// </summary>
        public void clearAllObjects()
        {
            foreach (MovingObject obj in movingObjList.Values)
            {
                obj.delObj();
            }
            movingObjList.Clear();
            vehicleNumberPrev = -1;

            // Rewind every trajectory to its "newpoint" action so the next tick
            // recreates the vehicles; mid-file "point" updates for ids that no
            // longer exist would otherwise be ignored forever.
            foreach (ObjActionArray actions in objActionArrayList)
            {
                actions.reset();
            }
        }

    }
}
