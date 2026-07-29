using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>Continuous intersection geometry between two moving objects.</summary>
internal sealed class RxRxIntersectionObserver(RxGeography source, IObserver<SqlGeography> observer)
    : RxRxObserverBase<SqlGeography>(source, observer)
{
    // Source.Location is null until the tracked object's first push (preserved behaviour).
    public override void OnNext(SqlGeography location)
        => Observer.OnNext(Source.Location!.STIntersection(location));
}
