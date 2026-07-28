using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;

namespace RxSpatial
{
    public class RxSpatialOverLoadException:System.Exception
    {
        public RxSpatialOverLoadException() : base() { }
        public RxSpatialOverLoadException(string message) : base(message) { }
        public RxSpatialOverLoadException(string message, System.Exception inner)
            : base(message, inner) { }

        protected RxSpatialOverLoadException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) { }
    }
}
