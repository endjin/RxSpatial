using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RxSpatial.Lib;
using Microsoft.SqlServer.Types;

namespace RxSpatial
{
    class RxGeographyRTree : RxGeographyTree
    {
        public RxGeographyRTree()
        {
            tree = new Tree<IObserver<SqlGeography>>(RxGeographyDataStruct.rtree);
        }
    }
}
