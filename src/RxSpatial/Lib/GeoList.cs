using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;


namespace RxSpatial.Lib
{
    class GeoList<T>:List<T>,IContainer<T>
    {
        public void subscribeRxRx(T observer, RxGeography G)
        {
            Add(observer);
        }
        public void subscribeRx(T observer, SqlGeography loc)
        {
            Add(observer);
        }
        public List<T> getObjsInSameZone(SqlGeography loc)
        {
            return this;
        }
    }
}
