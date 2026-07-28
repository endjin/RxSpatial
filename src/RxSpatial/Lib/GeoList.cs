using Microsoft.SqlServer.Types;

namespace RxSpatial.Lib;

/// <summary>The trivial backend: every observer is notified on every update.</summary>
internal class GeoList<T> : List<T>, IContainer<T>
    where T : notnull
{
    public void subscribeRxRx(T observer, RxGeography G) => Add(observer);

    public void subscribeRx(T observer, SqlGeography loc) => Add(observer);

    public List<T> getObjsInSameZone(SqlGeography loc) => this;
}
