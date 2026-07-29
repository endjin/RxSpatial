using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>Continuous distance query between a moving object and a stationary geography.</summary>
internal sealed class RxDistanceObserver(SqlGeography anchor, IObserver<SqlDouble> observer)
    : RxObserverBase<SqlDouble>(anchor, observer)
{
    public override void OnNext(SqlGeography location) => Observer.OnNext(Anchor.STDistance(location));
}
