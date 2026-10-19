namespace RxSpatial;

/// <summary>
/// A moving object as the spatial backends see it: a stream of location updates plus the
/// last location reported. Implemented by <see cref="RxGeography"/> and
/// <see cref="RxGeometry"/>; the backends follow a moving observer's tracked object through it.
/// </summary>
/// <typeparam name="TShape">The SQL Server spatial type of a location.</typeparam>
public interface IMovingObject<TShape> : IObservable<TShape>
    where TShape : class
{
    /// <summary>The last location pushed to this moving object, or null before the first push.</summary>
    TShape? Location { get; }
}
