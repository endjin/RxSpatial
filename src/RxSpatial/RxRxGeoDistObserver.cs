using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>Continuous distance query between two moving objects.</summary>
internal class RxRxGeoDistObserver : RxRxGeoObserver<SqlDouble>, IObserver<SqlGeography>
{
    public RxRxGeoDistObserver(RxGeography G, IObserver<SqlDouble> o)
        : base(G, o)
    {
    }

    // _localG.loc is null until the tracked object's first push (preserved behaviour).
    public virtual void OnNext(SqlGeography g) => _observer.OnNext(_localG.loc!.STDistance(g));

    public virtual void OnCompleted()
    {
    }

    public virtual void OnError(Exception e)
    {
    }
}
