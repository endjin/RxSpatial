using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>Continuous intersection geometry between a moving object and a stationary geography.</summary>
internal sealed class RxIntersectionObserver(SqlGeography anchor, IObserver<SqlGeography> observer)
    : RxObserverBase<SqlGeography>(anchor, observer), IObserver<SqlGeography>
{
    public void OnNext(SqlGeography location) => Observer.OnNext(Anchor.STIntersection(location));

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }
}
