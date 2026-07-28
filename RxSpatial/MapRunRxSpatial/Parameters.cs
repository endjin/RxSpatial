using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;

namespace MapRunRxSpatial
{
    class Parameters
    {
        //public static   string dataPath=@"C:/trafficData/";
        public static string dataPath = System.IO.Path.Combine(AppContext.BaseDirectory, "trafficData") + System.IO.Path.DirectorySeparatorChar;
        public static   string trafficFilename="trafficData.txt"; 
        public static string polygonFilename = "polygons.txt";
        public static string subscriptionFilename = "subscribeSettings.txt";
        public static string loadTestParamsFilename = "loadTestParams.txt";
        public static int objectsSpeedOnScreenInMS = 100;
    }


}
