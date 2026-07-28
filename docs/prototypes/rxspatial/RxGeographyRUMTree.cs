using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RxSpatial.Lib;
using Microsoft.SqlServer.Types;

namespace RxSpatial
{
    class RxGeographyRUMTree : RxGeographyTree
    {
        public RxGeographyRUMTree()
        {
            tree = new Tree<IObserver<SqlGeography>>(RxGeographyDataStruct.rumtree);
        }
    }
}
