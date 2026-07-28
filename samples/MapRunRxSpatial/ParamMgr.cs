using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapRunRxSpatial
{
    enum STATE
    {
        INTERSCT, DIST, RXRXDIST, RXRXINTERSECTION, RXRXDISTCONN
    };
    class ParamMgr
    {
        public static int MovementIntervalInMS = 1000;
        public static int VehicleNumber = 1;
        public static double ObsvDist = 0;
        public static STATE subscribeState = STATE.INTERSCT;
        public void updateParam(){

        }
    }
}
