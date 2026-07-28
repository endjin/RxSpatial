using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>A queued location update, timestamped for latency measurement.</summary>
internal sealed class LocationPush(SqlGeography content)
{
    private readonly DateTime startTime = HighResolutionDateTime.UtcNow;
    private DateTime endTime;

    public SqlGeography Content { get; } = content;

    /// <summary>
    /// Component (not total) milliseconds of the processing time — preserved exactly as the
    /// latency metric used for the papers' measurements.
    /// </summary>
    public double Latency => (endTime - startTime).Milliseconds;

    public void FinishProcessing() => endTime = HighResolutionDateTime.UtcNow;
}
