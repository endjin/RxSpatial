using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>Continuous distance query between a moving object and a stationary geography.</summary>
internal sealed class RxDistanceObserver(SqlGeography anchor, IObserver<double> observer)
    : RxObserverBase<double>(anchor, observer)
{
    public override void OnNext(SqlGeography location) => Observer.OnNext(SpatialDistance.Between(Anchor, location));
}
