using RxSpatial.Lib;

namespace RxSpatial;

/// <summary>Disposable subscription handle that removes an observer from its spatial container.</summary>
public class Unsubscriber<T>(IContainer<IObserver<T>> observers, IObserver<T> observer) : IDisposable
{
    public void Dispose()
    {
        if (observer is not null && observers.Contains(observer))
            observers.Remove(observer);
    }
}
