namespace RxSpatial;

/// <summary>Continuous intersection geometry between a moving object and a stationary shape.</summary>
internal sealed class RxIntersectionObserver<TShape, TOps>(TShape anchor, IObserver<TShape> observer)
    : RxObserverBase<TShape, TShape>(anchor, observer)
    where TShape : class
    where TOps : struct, ISpatialOperations<TShape>
{
    public override void OnNext(TShape location) => Observer.OnNext(TOps.Intersection(Anchor, location));
}
