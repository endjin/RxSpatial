namespace RxSpatial;

/// <summary>Continuous intersection test between two moving objects.</summary>
internal sealed class RxRxIntersectObserver<TShape, TOps>(IMovingObject<TShape> source, IObserver<bool> observer)
    : RxRxObserverBase<TShape, bool>(source, observer)
    where TShape : class
    where TOps : struct, ISpatialOperations<TShape>
{
    // Source.Location is null until the tracked object's first push (preserved behaviour).
    public override void OnNext(TShape location)
        => Observer.OnNext(TOps.Intersects(Source.Location!, location));
}
