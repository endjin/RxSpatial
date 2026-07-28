using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>Continuous intersection geometry between two moving objects.</summary>
internal class RxRxGeoIntersectionObserver : RxRxGeoObserver<SqlGeography>, IObserver<SqlGeography>
{
    public RxRxGeoIntersectionObserver(RxGeography G, IObserver<SqlGeography> o)
        : base(G, o)
    {
    }

    // _localG.loc is null until the tracked object's first push (preserved behaviour).
    public virtual void OnNext(SqlGeography g) => _observer.OnNext(_localG.loc!.STIntersection(g));

    public virtual void OnCompleted()
    {
    }

    public virtual void OnError(Exception e)
    {
    }
}
