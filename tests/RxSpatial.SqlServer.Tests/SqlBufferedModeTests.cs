using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace RxSpatial.SqlServer.Tests;

/// <summary>
/// The buffered (non-visualised) processing mode of <see cref="RxSqlGeography"/>: pushes are
/// queued and drained by a notification thread, and per-push latency is recorded — the
/// load-test path of the papers.
/// </summary>
[TestClass]
[TestCategory("SqlServer")]
[OSCondition(OperatingSystems.Windows)]
public class SqlBufferedModeTests
{
    private static readonly SqlGeography Fence =
        SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(500);

    [TestMethod]
    public void BufferedPushes_AreDrainedByTheNotificationThread_AndLatencyIsRecorded()
    {
        RxSqlGeography vehicle = new(new RxSpatialOptions
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
}
