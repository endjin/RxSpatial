namespace RxSpatial.Backends;

/// <summary>The trivial backend: every observer is notified on every update.</summary>
internal sealed class ListBackend<TShape, T> : List<T>, ISpatialBackend<TShape, T>
    where TShape : class
    where T : notnull
{
    public void Subscribe(T observer, IMovingObject<TShape> source) => Add(observer);

    public void Subscribe(T observer, TShape location) => Add(observer);

    public List<T> GetObserversInZone(TShape location) => this;
}
