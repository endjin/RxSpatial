using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>Continuous intersection test between a moving object and a stationary geography.</summary>
internal class RxGeoIntersectObserver : RxGeoObserverContainer<bool>, IObserver<SqlGeography>
{
    private readonly SqlGeography _localG;

    public RxGeoIntersectObserver(SqlGeography G, IObserver<bool> o)
        : base(o)
    {
        _localG = G;
    }

    public object G => _localG;

    public virtual void OnNext(SqlGeography g) => _observer.OnNext(_localG.STIntersects(g).IsTrue);

    public virtual void OnCompleted()
    {
    }

    public virtual void OnError(Exception e)
    {
    }
}
