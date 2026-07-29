namespace RxSpatial;

/// <summary>
/// Thrown when a moving object's location-push buffer exceeds the configured
/// <see cref="RxSpatialOptions.QueueLimit"/> in buffered (non-visualised) mode.
/// </summary>
public sealed class RxSpatialOverloadException : Exception
{
    public RxSpatialOverloadException()
    {
    }

    public RxSpatialOverloadException(string message)
        : base(message)
    {
    }

    public RxSpatialOverloadException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
