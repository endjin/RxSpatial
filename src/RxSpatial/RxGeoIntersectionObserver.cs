using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>Continuous intersection geometry between a moving object and a stationary geography.</summary>
internal class RxGeoIntersectionObserver : RxGeoObserverContainer<SqlGeography>, IObserver<SqlGeography>
{
    private readonly SqlGeography _localG;

    public RxGeoIntersectionObserver(SqlGeography G, IObserver<SqlGeography> o)
        : base(o)
    {
        _localG = G;
    }

    public object G => _localG;

    public virtual void OnNext(SqlGeography g) => _observer.OnNext(_localG.STIntersection(g));

    public virtual void OnCompleted()
    {
    }

    public virtual void OnError(Exception e)
    {
    }
}
