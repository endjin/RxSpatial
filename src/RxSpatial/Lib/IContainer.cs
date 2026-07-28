using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;

namespace RxSpatial.Lib
{
    public interface IContainer<T>
    {
        bool Remove(T item);
        bool Contains(T item);
        List<T> getObjsInSameZone(SqlGeography loc);
        void subscribeRxRx(T observer, RxGeography G);
        void subscribeRx(T observer, SqlGeography loc);
    }
}
