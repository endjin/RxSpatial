// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

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
