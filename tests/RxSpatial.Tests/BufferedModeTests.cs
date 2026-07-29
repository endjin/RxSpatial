using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace RxSpatial.Tests;

/// <summary>
/// The buffered (non-visualised) processing mode: pushes are queued and drained by a
/// notification thread, and per-push latency is recorded — the load-test path of the papers.
/// </summary>
[TestClass]
public class BufferedModeTests
{
    private static readonly SqlGeography Fence =
        SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(500);

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

        vehicle.OnNext(SqlGeography.Point(47.7000, -122.4000, 4326));
        vehicle.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326));

        // The queue drains on a background thread; poll briefly for completion.
        bool drained = SpinWait.SpinUntil(
            () => vehicle.checkQueuedPushNum() == 0 && EventCount() == 2,
            TimeSpan.FromSeconds(10));

        drained.ShouldBeTrue("buffered pushes were not drained in time");
        lock (events)
        {
            events.ShouldBe([false, true]);
        }

        vehicle.latency.ShouldBeGreaterThanOrEqualTo(0);

        int EventCount()
        {
            lock (events)
            {
                return events.Count;
            }
        }
    }

    [TestMethod]
    public void HighResolutionClock_IsAvailableOnWindows_AndTracksUtcNow()
    {
        HighResolutionDateTime.IsAvailable.ShouldBeTrue();
        DateTime now = HighResolutionDateTime.UtcNow;
        (DateTime.UtcNow - now).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void OverloadException_PreservesMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");
        new RxSpatialOverLoadException("boom").Message.ShouldBe("boom");
        var wrapped = new RxSpatialOverLoadException("outer", inner);
        wrapped.Message.ShouldBe("outer");
        wrapped.InnerException.ShouldBeSameAs(inner);
    }
}
