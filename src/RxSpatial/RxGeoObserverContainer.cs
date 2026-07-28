namespace RxSpatial;

/// <summary>Base for observers that wrap a downstream result observer.</summary>
internal class RxGeoObserverContainer<T>
{
    protected readonly IObserver<T> _observer;

    public RxGeoObserverContainer(IObserver<T> o) => _observer = o;
}
