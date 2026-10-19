using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Geo;
using Shouldly;

namespace RxSpatial.Tests;

/// <summary>
/// The <see cref="BackendSubscriptionTests"/> lifetime guarantees for the planar
/// <see cref="RxGeometry"/>, on the raw (non-Rx) API: disposing the handle must end
/// notifications on every backend, for stationary regions and for tracked moving objects.
/// </summary>
[TestClass]
public class PlanarBackendSubscriptionTests
{
    private static readonly PlanarShape Bay =
        PlanarPolygon.FromWkt("POLYGON ((450 450, 550 450, 550 550, 450 550, 450 450))");

    [TestMethod]
    [DataRow(RxGeographyDataStruct.List)]
    [DataRow(RxGeographyDataStruct.HashMap)]
    [DataRow(RxGeographyDataStruct.RTree)]
    [DataRow(RxGeographyDataStruct.RumTree)]
    public void DisposingAStationaryRegionSubscription_StopsNotifications(RxGeographyDataStruct backend)
    {
        RxGeometry forklift = new(VisualizedOptions(backend));
        Recorder<bool> recorder = new();
        IDisposable subscription = forklift.RxIntersect(Bay, recorder);

        forklift.OnNext(Disc(500, 500, 5));
        subscription.Dispose();
        forklift.OnNext(Disc(500, 500, 5));

        recorder.Values.Count.ShouldBe(1);
    }

    [TestMethod]
    [DataRow(RxGeographyDataStruct.List)]
    [DataRow(RxGeographyDataStruct.HashMap)]
    [DataRow(RxGeographyDataStruct.RTree)]
    [DataRow(RxGeographyDataStruct.RumTree)]
    public void DisposingAMovingObserverSubscription_StopsTheLocationTracker(RxGeographyDataStruct backend)
    {
        RxGeometry a = new(VisualizedOptions(backend));
        RxGeometry b = new(VisualizedOptions(backend));
        b.OnNext(Disc(500, 500, 50));

        Recorder<double> recorder = new();
        IDisposable subscription = a.RxRxDistance(b, recorder);

        a.OnNext(Disc(500, 500, 50));
        int afterFirst = recorder.Values.Count;
        afterFirst.ShouldBeGreaterThan(0);

        subscription.Dispose();

        // The tracked object's push drives the backend's location tracker, which must have
        // ended with the subscription rather than re-index the removed observer.
        b.OnNext(Disc(530, 530, 50));
        a.OnNext(Disc(530, 530, 50));

        recorder.Values.Count.ShouldBe(afterFirst);
    }

    [TestMethod]
    public void MovingRegionObserver_DoesNotAccumulateAcrossGridCells()
    {
        RxGeometry a = new(VisualizedOptions(RxGeographyDataStruct.HashMap));
        RxGeometry b = new(VisualizedOptions(RxGeographyDataStruct.HashMap));
        b.OnNext(Disc(150, 150, 5));

        Recorder<double> recorder = new();
        using IDisposable subscription = a.RxRxDistance(b, recorder);

        // Walk the tracked object across several grid cells, then push A as a region spanning
        // all of them: an observer left behind in a visited cell would be notified once per cell.
        b.OnNext(Disc(350, 350, 5));
        b.OnNext(Disc(550, 550, 5));
        a.OnNext(Disc(350, 350, 250));

        recorder.Values.Count.ShouldBe(1);
    }

    private static RxSpatialOptions VisualizedOptions(RxGeographyDataStruct backend)
        => new()
        {
            PreferredDataStruct = backend,
            IsVisualized = true,
            QueueLimit = 64,
        };

    private static PlanarPointShape Point(double x, double y) => new(x, y);

    private static PlanarCircle Disc(double x, double y, double radius) => new(new PlanarPoint(x, y), radius);

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
