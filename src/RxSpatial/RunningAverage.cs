namespace RxSpatial;

/// <summary>Incremental (running) average of recorded values.</summary>
internal sealed class RunningAverage
{
    private int count;

    public double Average { get; private set; }

    public double Record(double value)
    {
        Average += (value - Average) / (count + 1);
        count++;
        return Average;
    }
}
