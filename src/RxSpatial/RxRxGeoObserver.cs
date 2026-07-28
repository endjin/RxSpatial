namespace RxSpatial;

/// <summary>Base for observers that relate two moving objects.</summary>
internal class RxRxGeoObserver<T>
{
    protected readonly RxGeography _localG;
    protected readonly IObserver<T> _observer;

    public RxRxGeoObserver(RxGeography G, IObserver<T> o)
    {
        _localG = G;
        _observer = o;
    }

    public RxGeography G => _localG;
}
