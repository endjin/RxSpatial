namespace RxSpatial.Util;

/// <summary>Incremental (running) average of recorded latencies.</summary>
internal class AvgCalc
{
    private int count;

    public double Avg { get; private set; }

    public double recordLatency(double latency)
    {
        Avg += (latency - Avg) / (count + 1);
        count++;
        return Avg;
    }
}
