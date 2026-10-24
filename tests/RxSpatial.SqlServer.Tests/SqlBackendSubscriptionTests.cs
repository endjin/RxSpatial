// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace RxSpatial.SqlServer.Tests;

/// <summary>
/// Subscription lifetime on the raw (non-Rx) API, where nothing detaches an observer for the
/// caller: disposing the handle must end notifications on every backend, for stationary region
/// observers as well as observers that track another moving object.
/// </summary>
[TestClass]
[TestCategory("SqlServer")]
[OSCondition(OperatingSystems.Windows)]
public class SqlBackendSubscriptionTests
{
    private static readonly SqlGeography Fence =
        SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(500);

    [TestMethod]
    [DataRow(RxGeographyDataStruct.List)]
    [DataRow(RxGeographyDataStruct.HashMap)]
    [DataRow(RxGeographyDataStruct.RTree)]
    [DataRow(RxGeographyDataStruct.RumTree)]
    public void DisposingAStationaryRegionSubscription_StopsNotifications(RxGeographyDataStruct backend)
    {
        // A geofence is a region, not a point: the grid backend used to leave regions out of
        // its observer map entirely, so unsubscribing one silently did nothing.
        RxSqlGeography vehicle = new(VisualizedOptions(backend));
        Recorder<bool> recorder = new();
        IDisposable subscription = vehicle.RxIntersect(Fence, recorder);

        vehicle.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(50));
        subscription.Dispose();
        vehicle.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(50));

        recorder.Values.Count.ShouldBe(1);
    }

    [TestMethod]
    [DataRow(RxGeographyDataStruct.List)]
    [DataRow(RxGeographyDataStruct.HashMap)]
    [DataRow(RxGeographyDataStruct.RTree)]
    [DataRow(RxGeographyDataStruct.RumTree)]
    public void DisposingAMovingObserverSubscription_StopsTheLocationTracker(RxGeographyDataStruct backend)
    {
        RxSqlGeography a = new(VisualizedOptions(backend));
        RxSqlGeography b = new(VisualizedOptions(backend));
        b.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(500));

        Recorder<double> recorder = new();
        IDisposable subscription = a.RxRxDistance(b, recorder);

        a.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(500));
        int afterFirst = recorder.Values.Count;
        afterFirst.ShouldBeGreaterThan(0);

        subscription.Dispose();

        // The tracked object's push drives the backend's location tracker. While the tracker
        // outlived the subscription it re-indexed (Update is an upsert) the removed observer.
        b.OnNext(SqlGeography.Point(47.6300, -122.3550, 4326).STBuffer(500));
        a.OnNext(SqlGeography.Point(47.6300, -122.3550, 4326).STBuffer(500));

        recorder.Values.Count.ShouldBe(afterFirst);
    }

    [TestMethod]
    public void MovingRegionObserver_DoesNotAccumulateAcrossGridCells()
    {
        // The grid re-indexes a tracked observer on every push. Without eviction of its old
        // cells a moving region stacked up in every cell it had ever visited, so one push
        // could notify it several times.
        RxSqlGeography a = new(VisualizedOptions(RxGeographyDataStruct.HashMap));
        RxSqlGeography b = new(VisualizedOptions(RxGeographyDataStruct.HashMap));
        b.OnNext(SqlGeography.Point(46.2000, -123.5000, 4326).STBuffer(500));

        Recorder<double> recorder = new();
        using IDisposable subscription = a.RxRxDistance(b, recorder);

        // Walk the tracked object across several grid cells, then push A into the last one.
        b.OnNext(SqlGeography.Point(47.0000, -123.0000, 4326).STBuffer(500));
        b.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(500));
        a.OnNext(SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(500));

        recorder.Values.Count.ShouldBe(1);
    }

    private static RxSpatialOptions VisualizedOptions(RxGeographyDataStruct backend)
        => new()
        {
            PreferredDataStruct = backend,
            IsVisualized = true,
            QueueLimit = 64,
        };

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
