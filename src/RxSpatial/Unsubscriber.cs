using RxSpatial.Backends;

namespace RxSpatial;

/// <summary>Disposable subscription handle that removes an observer from its spatial backend.</summary>
public sealed class Unsubscriber<T>(ISpatialBackend<IObserver<T>> observers, IObserver<T> observer) : IDisposable
{
    public void Dispose() => observers.Remove(observer);
}
