using System;
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

    class SimulationFromText
    {
       
        Dictionary<int, MovingObject> movingObjList = new Dictionary<int, MovingObject>();
        public Map baseMap;
        TextReader tr = new StreamReader(Parameters.dataPath + Parameters.trafficFilename);
        string line;
        int timestamp;
        PolygonMgr polygonMgr;
        public static DispatcherTimer dispatcherTimer;
        public SimulationFromText(Map map, PolygonMgr polygonMgr)
        {
            baseMap = map;
            this.polygonMgr = polygonMgr;
            tr.ReadLine();
            tr.ReadLine();
            line = tr.ReadLine();
        }

        public void procTraffic()
        {
            //  DispatcherTimer setup
            dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(updateTraffic);
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(Parameters.objectsSpeedOnScreenInMS);
            dispatcherTimer.Start();
        }

        private void updateTraffic(object sender, EventArgs e)
        {
         
            if (line == null) //EOF
            {
                tr.Close();
                dispatcherTimer.Stop();
               // MessageBox.Show("End of moving objects file");
                return;
            }

            if (line != null)
            {
                string[] strs = line.Split();
                int id = Convert.ToInt32(strs[0]);
                timestamp = Convert.ToInt32(strs[1]);
                int currTimestamp = timestamp;
                string status = strs[2];
                double latitude = Convert.ToDouble(strs[3]);
                double longitude = Convert.ToDouble(strs[4]);
                SqlGeography location = SqlGeography.Point(latitude, longitude, 4326);

                while (currTimestamp == timestamp && line != null)
                {
                   try
                    {
                        updateNewLoc(id, status, location); 
                        line = tr.ReadLine();
                        if (line == null)
                            continue;
                        strs = line.Split();
                        if (strs.Length != 5)
                            continue;
                        id = Convert.ToInt32(strs[0]);
                        timestamp = Convert.ToInt32(strs[1]);
                        status = strs[2];
                        latitude = Convert.ToDouble(strs[3]);
                        longitude = Convert.ToDouble(strs[4]);
                        location = SqlGeography.Point(latitude, longitude, 4326);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }

            }
        }

        private void updateNewLoc(int id, string status, SqlGeography loc)
        {
            if (status.StartsWith("n"))
            {
                addItem(id, loc);
            }
            else if (status.StartsWith("p"))
            {
                updateItem(id, loc);
            }
            else if (status.StartsWith("d"))
            {
                delItem(id);
            }
        }

        private void addItem(int id, SqlGeography loc)
        {
            if (movingObjList.ContainsKey(id))
            {
                throw new Exception("key"+id.ToString()+"already added");
            }
            MovingObject newItem = new MovingObject(id, baseMap, polygonMgr);
            movingObjList.Add(id, newItem);
          //  subscribe(id);
           /// newItem.feedLocation(loc);
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
    }
}
