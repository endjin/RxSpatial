namespace RxSpatial;

/// <summary>
/// Base for continuous-query observers that relate two moving objects: the tracked
/// <paramref name="source"/> and the object whose pushes drive <c>OnNext</c>.
/// </summary>
internal abstract class RxRxObserverBase<TResult>(RxGeography source, IObserver<TResult> observer)
{
    protected RxGeography Source { get; } = source;

    protected IObserver<TResult> Observer { get; } = observer;
}
