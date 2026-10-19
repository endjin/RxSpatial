using RxSpatial.Backends;

namespace RxSpatial;

/// <summary>Disposable subscription handle that removes an observer from its spatial backend.</summary>
public sealed class Unsubscriber<TShape>(ISpatialBackend<TShape, IObserver<TShape>> observers, IObserver<TShape> observer)
    : IDisposable
    where TShape : class
{
    public void Dispose() => observers.Remove(observer);
}
