using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace RxSpatial.SqlServer.Tests;

/// <summary>
/// The <see cref="SqlBackendSubscriptionTests"/> lifetime guarantees for the planar
/// <see cref="RxSqlGeometry"/>, on the raw (non-Rx) API: disposing the handle must end
/// notifications on every backend, for stationary regions and for tracked moving objects.
/// </summary>
[TestClass]
[TestCategory("SqlServer")]
[OSCondition(OperatingSystems.Windows)]
public class SqlPlanarBackendSubscriptionTests
{
    private static readonly SqlGeometry Bay =
        SqlGeometry.Parse("POLYGON ((450 450, 550 450, 550 550, 450 550, 450 450))");

    [TestMethod]
    [DataRow(RxGeographyDataStruct.List)]
    [DataRow(RxGeographyDataStruct.HashMap)]
    [DataRow(RxGeographyDataStruct.RTree)]
    [DataRow(RxGeographyDataStruct.RumTree)]
    public void DisposingAStationaryRegionSubscription_StopsNotifications(RxGeographyDataStruct backend)
    {
        RxSqlGeometry forklift = new(VisualizedOptions(backend));
        Recorder<bool> recorder = new();
        IDisposable subscription = forklift.RxIntersect(Bay, recorder);

        forklift.OnNext(Point(500, 500).STBuffer(5));
        subscription.Dispose();
        forklift.OnNext(Point(500, 500).STBuffer(5));

        recorder.Values.Count.ShouldBe(1);
    }

    [TestMethod]
    [DataRow(RxGeographyDataStruct.List)]
    [DataRow(RxGeographyDataStruct.HashMap)]
    [DataRow(RxGeographyDataStruct.RTree)]
    [DataRow(RxGeographyDataStruct.RumTree)]
    public void DisposingAMovingObserverSubscription_StopsTheLocationTracker(RxGeographyDataStruct backend)
    {
        RxSqlGeometry a = new(VisualizedOptions(backend));
        RxSqlGeometry b = new(VisualizedOptions(backend));
        b.OnNext(Point(500, 500).STBuffer(50));

        Recorder<double> recorder = new();
        IDisposable subscription = a.RxRxDistance(b, recorder);

        a.OnNext(Point(500, 500).STBuffer(50));
        int afterFirst = recorder.Values.Count;
        afterFirst.ShouldBeGreaterThan(0);

        subscription.Dispose();

        // The tracked object's push drives the backend's location tracker, which must have
        // ended with the subscription rather than re-index the removed observer.
        b.OnNext(Point(530, 530).STBuffer(50));
        a.OnNext(Point(530, 530).STBuffer(50));

        recorder.Values.Count.ShouldBe(afterFirst);
    }

    [TestMethod]
    public void MovingRegionObserver_DoesNotAccumulateAcrossGridCells()
    {
        RxSqlGeometry a = new(VisualizedOptions(RxGeographyDataStruct.HashMap));
        RxSqlGeometry b = new(VisualizedOptions(RxGeographyDataStruct.HashMap));
        b.OnNext(Point(150, 150).STBuffer(5));

        Recorder<double> recorder = new();
        using IDisposable subscription = a.RxRxDistance(b, recorder);

        // Walk the tracked object across several grid cells, then push A as a region spanning
        // all of them: an observer left behind in a visited cell would be notified once per cell.
        b.OnNext(Point(350, 350).STBuffer(5));
        b.OnNext(Point(550, 550).STBuffer(5));
        a.OnNext(Point(350, 350).STBuffer(250));

        recorder.Values.Count.ShouldBe(1);
    }

    private static RxSpatialOptions VisualizedOptions(RxGeographyDataStruct backend)
        => new()
        {
            PreferredDataStruct = backend,
            IsVisualized = true,
            QueueLimit = 64,
        };

    private static SqlGeometry Point(double x, double y) => SqlGeometry.Point(x, y, 0);

    private sealed class Recorder<T> : IObserver<T>
    {
        public List<T> Values { get; } = [];

        public void OnNext(T value) => Values.Add(value);

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
