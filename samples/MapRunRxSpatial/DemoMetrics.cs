// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MapRunRxSpatial;

/// <summary>
/// The demo's own performance instruments, on the <c>MapRunRxSpatial</c> meter: traffic tick
/// time, location pushes and the web messages sent to the map. Instruments cost next to
/// nothing until a listener enables them, so they stay compiled in. Read them live with
/// <c>dotnet-counters monitor -n MapRunRxSpatial --counters System.Runtime,MapRunRxSpatial</c>
/// (or <c>collect --format csv</c> for a run to compare).
/// </summary>
internal static class DemoMetrics
{
    private static readonly Meter Meter = new("MapRunRxSpatial");

    private static readonly Histogram<double> TickDuration = Meter.CreateHistogram<double>(
        "maprun.tick.duration", "ms", "Time the UI thread spends on one traffic tick: pushes, queries and map commands.");

    private static readonly Counter<double> TickBusy = Meter.CreateCounter<double>(
        "maprun.tick.busy", "ms", "UI-thread time spent on traffic ticks; its rate per second is the ticks' share of the UI thread.");

    private static readonly Counter<long> Ticks = Meter.CreateCounter<long>(
        "maprun.ticks", "{tick}", "Traffic ticks run.");

    private static readonly Counter<long> Pushes = Meter.CreateCounter<long>(
        "maprun.pushes", "{push}", "Vehicle location pushes (updates) replayed.");

    private static readonly Counter<long> WebMessages = Meter.CreateCounter<long>(
        "maprun.web.messages", "{message}", "Web messages posted to the map.");

    private static readonly Counter<long> WebMessageChars = Meter.CreateCounter<long>(
        "maprun.web.chars", "{char}", "Characters of JSON posted to the map (bytes, for the ASCII the demo sends).");

    private static readonly Histogram<double> SendDuration = Meter.CreateHistogram<double>(
        "maprun.web.send.duration", "ms", "Time the UI thread spends writing and posting one batch of map commands.");

    private static readonly Counter<double> SendBusy = Meter.CreateCounter<double>(
        "maprun.web.send.busy", "ms", "UI-thread time spent writing and posting map commands after the ticks.");

    /// <summary>A timestamp to pass to <see cref="EndTick"/>; 0 (and no clock read) while nobody listens.</summary>
    public static long BeginTick() => TickDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    public static void EndTick(long start)
    {
        Ticks.Add(1);
        if (start != 0)
        {
            double milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            TickDuration.Record(milliseconds);
            TickBusy.Add(milliseconds);
        }
    }

    public static void Push() => Pushes.Add(1);

    /// <summary>A timestamp to pass to <see cref="EndSend"/>; 0 (and no clock read) while nobody listens.</summary>
    public static long BeginSend() => SendDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    /// <summary>Counts a posted batch; <paramref name="start"/> is from <see cref="BeginSend"/>.</summary>
    public static void EndSend(long start, string json)
    {
        WebMessages.Add(1);
        WebMessageChars.Add(json.Length);
        if (start != 0)
        {
            double milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            SendDuration.Record(milliseconds);
            SendBusy.Add(milliseconds);
        }
    }
}
