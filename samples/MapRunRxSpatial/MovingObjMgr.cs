using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Maps.MapControl.WPF;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;

using Microsoft.SqlServer.Types;

using RxSpatial;

namespace MapRunRxSpatial
{
    class MovingObjMgr
    {
        Dictionary<int, MovingObject> movingObjList = new Dictionary<int, MovingObject>();
        public Map baseMap;
        private PolygonMgr polygonMgr;
       
        public static DispatcherTimer dispatcherTimer;
        public MovingObjMgr(Map map, PolygonMgr polygonMgr)
        {
            baseMap = map;
            this.polygonMgr = polygonMgr;
            initMovingObjActionList();
        }
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
                List<ObjAction> arr= new List<ObjAction>();
                TextReader tr = new StreamReader(Parameters.dataPath + i.ToString()+".txt");
                tr.ReadLine();
                tr.ReadLine();
                string line=tr.ReadLine();
                while(line!=null && line.Length!=0)
                {
                    string[] strs = line.Split();
                    if (strs.Length != 5)
                        continue;
                    string status = strs[2];
                    Double latitude = Convert.ToDouble(strs[3]);
                    Double longitude = Convert.ToDouble(strs[4]);
                    SqlGeography location = SqlGeography.Point(latitude, longitude, 4326);
                    arr.Add(new ObjAction(i, status, location));
                    line = tr.ReadLine();
                }
                objActionArrayList[i] = new ObjActionArray(arr);
            }
        }
        private Boolean traffic_started = false;
        public void procTraffic()
        {
            //  DispatcherTimer setup
            dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(updateTraffic);
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(ParamMgr.MovementIntervalInMS);
            dispatcherTimer.Start();
            traffic_started = true;
        }
        public void stopTraffic()
        {
            if (traffic_started == true)
            dispatcherTimer.Stop();
        }
        public void restartProcTraffic()
        {
            if (traffic_started == true)
            {
                dispatcherTimer.Stop();
                procTraffic();
            }
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
            if (movingObjList.ContainsKey(id))
            {
           //     throw new Exception("key" + id.ToString() + "already added");
            }

            MovingObject newItem = new MovingObject(id, baseMap, polygonMgr);
            newItem.feedLocation(loc);
            //  Console.WriteLine("add"+id.ToString());
            movingObjList.Add(id, newItem);
            foreach(Polygon p in PolygonMgr.polygonDict.Values){
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

    }
}
