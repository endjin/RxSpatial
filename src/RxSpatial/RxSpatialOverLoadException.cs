namespace RxSpatial;

/// <summary>
/// Thrown when a moving object's location-push buffer exceeds the configured
/// <c>RxSpatialQueueLimit</c> in buffered (non-visualised) mode.
/// </summary>
public class RxSpatialOverLoadException : Exception
{
    public RxSpatialOverLoadException()
    {
    }

    public RxSpatialOverLoadException(string message)
        : base(message)
    {
    }

    public RxSpatialOverLoadException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
