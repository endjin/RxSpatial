using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>Continuous intersection test between two moving objects.</summary>
internal sealed class RxRxIntersectObserver(RxGeography source, IObserver<bool> observer)
    : RxRxObserverBase<bool>(source, observer), IObserver<SqlGeography>
{
    // Source.Location is null until the tracked object's first push (preserved behaviour).
    public void OnNext(SqlGeography location) => Observer.OnNext(Source.Location!.STIntersects(location).IsTrue);

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }
}
