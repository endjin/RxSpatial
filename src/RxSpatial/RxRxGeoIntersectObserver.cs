using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>Continuous intersection test between two moving objects.</summary>
internal class RxRxGeoIntersectObserver : RxRxGeoObserver<bool>, IObserver<SqlGeography>
{
    public RxRxGeoIntersectObserver(RxGeography G, IObserver<bool> o)
        : base(G, o)
    {
    }

    // _localG.loc is null until the tracked object's first push (preserved behaviour).
    public virtual void OnNext(SqlGeography g) => _observer.OnNext(_localG.loc!.STIntersects(g).IsTrue);

    public virtual void OnCompleted()
    {
    }

    public virtual void OnError(Exception e)
    {
    }
}
