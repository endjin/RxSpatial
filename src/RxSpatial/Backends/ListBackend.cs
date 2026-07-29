using Microsoft.SqlServer.Types;

namespace RxSpatial.Backends;

/// <summary>The trivial backend: every observer is notified on every update.</summary>
internal sealed class ListBackend<T> : List<T>, ISpatialBackend<T>
    where T : notnull
{
    public void Subscribe(T observer, RxGeography source) => Add(observer);

    public void Subscribe(T observer, SqlGeography location) => Add(observer);

    public List<T> GetObserversInZone(SqlGeography location) => this;
}
