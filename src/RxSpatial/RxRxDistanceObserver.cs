using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>Continuous distance query between two moving objects.</summary>
internal sealed class RxRxDistanceObserver(RxGeography source, IObserver<double> observer)
    : RxRxObserverBase<double>(source, observer)
{
    // Source.Location is null until the tracked object's first push (preserved behaviour).
    public override void OnNext(SqlGeography location)
        => Observer.OnNext(SpatialDistance.Between(Source.Location!, location));
}
