using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;


using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;

namespace MapRunRxSpatial
{
     class Subscription{
                private string _name;
                private string _clientId;
                private string _observerType;
                public Subscription(string name,string clientId,string observerType){
                    _name=name;_clientId=clientId;_observerType=observerType;
                }
                public string Name{ get { return this._name; } }
                public string ClientId{ get { return this._clientId; } }
                public string ObserverType{ get { return this._observerType; } }
            }
    class SubscribeParams
    {
        //PolygonList to test intersection observer only
        public static Dictionary<string, SqlGeography> polygonList = new Dictionary<string, SqlGeography>();
        //subscriptionList to subscribe observers, in different clientIds oriented
        public static Dictionary<string, List<Subscription>> subscriptionList = new Dictionary<string,List<Subscription>>();

        public static void init()
        {
            initPolygon();
            initSubscription();
        }

        private static void initPolygon()
        {
            TextReader tr = new StreamReader(Parameters.dataPath + Parameters.polygonFilename);
            string line = null;
            for (line = tr.ReadLine(); line != null; line = tr.ReadLine())
            {
                string[] strs = line.Split(' ');
                int idx = line.IndexOf(" ");
                addPolygon(strs[0], line.Substring(idx));
            }
        }

        private static void initSubscription()
        {
            TextReader tr = new StreamReader(Parameters.dataPath + Parameters.subscriptionFilename);
            string line = tr.ReadLine();
            while (line != null)
            {
                string[] strs = line.Split(' ');
                addSubscripton(strs[0], strs[1], strs[2]);
                line = tr.ReadLine();
            }
        }
        private static void addPolygon(string name, string line){
            SqlGeography polygon = SqlGeography.STPolyFromText(new SqlChars(new SqlString(line)), 4326);
            polygonList.Add(name,polygon);
        }
        private static void addSubscripton(string name, string clientId, string observerType)
        {
            if(!subscriptionList.ContainsKey(clientId)){
                subscriptionList.Add(clientId,new List<Subscription>());
            }
            Subscription subscription = new Subscription(name,clientId,observerType);
            subscriptionList[clientId].Add(subscription);
        }
    }
    class LoadParams
    {
        public static int observerNumber=-1;
        public static int vehicleNumber=-1;
        public static void init()
        {
            TextReader tr = new StreamReader(Parameters.dataPath + Parameters.loadTestParamsFilename);
            string line = tr.ReadLine();
            while (line != null)
            {
                string[] strs = line.Split(' ');
                if (strs[0].Equals("ObserverNumber:")) observerNumber = Convert.ToInt32(strs[1]);
                else if (strs[0].Equals("VehicleNumber:")) vehicleNumber = Convert.ToInt32(strs[1]);
                line = tr.ReadLine();
            }
        }
    }
}
