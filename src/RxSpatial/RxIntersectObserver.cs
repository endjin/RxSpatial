using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>Continuous intersection test between a moving object and a stationary geography.</summary>
internal sealed class RxIntersectObserver(SqlGeography anchor, IObserver<bool> observer)
    : RxObserverBase<bool>(anchor, observer), IObserver<SqlGeography>
{
    public void OnNext(SqlGeography location) => Observer.OnNext(Anchor.STIntersects(location).IsTrue);

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }
}
