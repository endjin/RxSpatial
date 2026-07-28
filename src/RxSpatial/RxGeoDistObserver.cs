using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>Continuous distance query between a moving object and a stationary geography.</summary>
internal class RxGeoDistObserver : RxGeoObserverContainer<SqlDouble>, IObserver<SqlGeography>
{
    private readonly SqlGeography _localG;

    public RxGeoDistObserver(SqlGeography G, IObserver<SqlDouble> o)
        : base(o)
    {
        _localG = G;
    }

    public object G => _localG;

    public virtual void OnNext(SqlGeography g) => _observer.OnNext(_localG.STDistance(g));

    public virtual void OnCompleted()
    {
    }

    public virtual void OnError(Exception e)
    {
    }
}
