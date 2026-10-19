namespace RxSpatial;

/// <summary>Continuous intersection test between a moving object and a stationary shape.</summary>
internal sealed class RxIntersectObserver<TShape, TOps>(TShape anchor, IObserver<bool> observer)
    : RxObserverBase<TShape, bool>(anchor, observer)
    where TShape : class
    where TOps : struct, ISpatialOperations<TShape>
{
    public override void OnNext(TShape location) => Observer.OnNext(TOps.Intersects(Anchor, location));
}
