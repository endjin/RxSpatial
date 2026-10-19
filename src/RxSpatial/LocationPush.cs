using System.Diagnostics;

namespace RxSpatial;

/// <summary>A queued location update, timestamped for latency measurement.</summary>
internal sealed class LocationPush<TShape>(TShape content)
    where TShape : class
{
    // Stopwatch ticks: monotonic, high-resolution on every platform, and unaffected by
    // wall-clock adjustments, which an elapsed-time measure must not see.
    private readonly long startTimestamp = Stopwatch.GetTimestamp();
    private long endTimestamp;

    public TShape Content { get; } = content;

    /// <summary>
    /// Milliseconds from entering the push buffer to the end of processing — the latency
    /// measure of the ICDE 2017 paper (§VI). Total, fractional milliseconds: the 2016 code
    /// recorded the integer <see cref="TimeSpan.Milliseconds"/> component, which read every
    /// sub-millisecond update as 0 and wrapped latencies over a second.
    /// </summary>
    public double Latency => Stopwatch.GetElapsedTime(startTimestamp, endTimestamp).TotalMilliseconds;

    public void FinishProcessing() => endTimestamp = Stopwatch.GetTimestamp();
}
