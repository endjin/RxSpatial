using System.Diagnostics;

namespace RxSpatial.Experiments;

/// <summary>
/// Waits for <see cref="Stopwatch"/> deadlines more precisely than a timer: Windows timers and
/// <see cref="Thread.Sleep(int)"/> tick at ~15.6 ms, too coarse for the paper's 5 ms
/// inter-arrival times. Long waits sleep until ~16 ms before the deadline, then spin.
/// </summary>
internal static class Pacer
{
    private static readonly long SleepMargin = Stopwatch.Frequency / 1000 * 16;

    public static long Ticks(double milliseconds) => (long)(milliseconds * Stopwatch.Frequency / 1000);

    public static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    public static void WaitUntil(long deadline)
    {
        long remaining = deadline - Stopwatch.GetTimestamp();
        if (remaining > SleepMargin)
            Thread.Sleep(TimeSpan.FromMilliseconds(Milliseconds(remaining - SleepMargin)));

        SpinWait spinner = default;
        while (Stopwatch.GetTimestamp() < deadline)
        {
            // Yields are fine, Sleep(1) is not: it alone would cost a timer tick.
            spinner.SpinOnce(sleep1Threshold: -1);
        }
    }
}
