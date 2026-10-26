// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Geo;
using Shouldly;

namespace RxSpatial.Tests;

/// <summary>
/// The buffered (non-visualised) processing mode: pushes are queued and drained by a
/// notification thread, and per-push latency is recorded — the load-test path of the papers.
/// </summary>
[TestClass]
public class BufferedModeTests
{
    private static readonly GeoShape Fence =
        Disc(47.6205, -122.3493, 500);

    [TestMethod]
    public void BufferedPushes_AreDrainedByTheNotificationThread_AndLatencyIsRecorded()
    {
        RxGeography vehicle = new(new RxSpatialOptions
        {
            IsVisualized = false,
            QueueLimit = 128,
            PreferredDataStruct = RxGeographyDataStruct.List,
        });

        List<bool> events = [];
        using IDisposable subscription = vehicle.Intersects(Fence).Subscribe(b =>
        {
            lock (events)
            {
                events.Add(b);
            }
        });

        vehicle.OnNext(Point(47.7000, -122.4000));
        vehicle.OnNext(Point(47.6205, -122.3493));

        // The queue drains on a background thread; poll briefly for completion.
        bool drained = SpinWait.SpinUntil(
            () => vehicle.QueuedPushCount == 0 && EventCount() == 2,
            TimeSpan.FromSeconds(10));

        drained.ShouldBeTrue("buffered pushes were not drained in time");
        lock (events)
        {
            events.ShouldBe([false, true]);
        }

        // Total, fractional milliseconds: a sub-millisecond push still records a positive
        // latency, where the 2016 component-milliseconds metric read it as 0.
        vehicle.AverageLatency.ShouldBeGreaterThan(0);
        vehicle.AverageLatency.ShouldBeLessThan(10_000);

        int EventCount()
        {
            lock (events)
            {
                return events.Count;
            }
        }
    }

    [TestMethod]
    public void OverloadException_PreservesMessageAndInnerException()
    {
        InvalidOperationException inner = new("inner");
        new RxSpatialOverloadException("boom").Message.ShouldBe("boom");
        RxSpatialOverloadException wrapped = new("outer", inner);
        wrapped.Message.ShouldBe("outer");
        wrapped.InnerException.ShouldBeSameAs(inner);
    }

    private static GeoPointShape Point(double latitude, double longitude) => new(latitude, longitude);

    private static GeoCircle Disc(double latitude, double longitude, double radiusMetres)
        => new(new GeoPoint(latitude, longitude), radiusMetres);
}
