using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>Continuous distance query between two moving objects.</summary>
internal sealed class RxRxDistanceObserver(RxGeography source, IObserver<SqlDouble> observer)
    : RxRxObserverBase<SqlDouble>(source, observer), IObserver<SqlGeography>
{
    // Source.Location is null until the tracked object's first push (preserved behaviour).
    public void OnNext(SqlGeography location) => Observer.OnNext(Source.Location!.STDistance(location));

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }
}
