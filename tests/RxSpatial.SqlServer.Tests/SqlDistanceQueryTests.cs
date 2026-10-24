// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace RxSpatial.SqlServer.Tests;

/// <summary>
/// Distance queries report <see cref="double"/> metres, as the RxSpatial papers declare them.
/// Where SQL Server's <c>STDistance</c> returns <c>SqlDouble.Null</c> the query emits
/// <see cref="double.NaN"/>, so every location update still produces one notification.
/// </summary>
[TestClass]
[TestCategory("SqlServer")]
[OSCondition(OperatingSystems.Windows)]
public class SqlDistanceQueryTests
{
    private static readonly SqlGeography Needle = SqlGeography.Point(47.6205, -122.3493, 4326);
    private static readonly SqlGeography Nearby = SqlGeography.Point(47.6300, -122.3550, 4326);

    // The same place in NAD83 (SRID 4269): SQL Server will not measure across SRIDs.
    private static readonly SqlGeography NeedleInOtherSrid = SqlGeography.Point(47.6205, -122.3493, 4269);
    private static readonly SqlGeography EmptyPoint = SqlGeography.Parse("POINT EMPTY"); // SRID 4326

    public static IEnumerable<object[]> NullDistanceInputs =>
    [
        ["different SRIDs", NeedleInOtherSrid],
        ["empty geography", EmptyPoint],
    ];

    [TestMethod]
    public void StDistance_ReturnsNull_ForTheInputsTheseTestsRelyOn()
    {
        // Guards the premise: if SQL Server ever computes these, the NaN tests below test nothing.
        Needle.STDistance(NeedleInOtherSrid).IsNull.ShouldBeTrue();
        Needle.STDistance(EmptyPoint).IsNull.ShouldBeTrue();
    }

    [TestMethod]
    public void RxDistance_ReportsMetresAsDouble()
    {
        RxSqlGeography vehicle = new(VisualizedOptions());
        Recorder recorder = new();
        using IDisposable subscription = vehicle.RxDistance(Needle, recorder);

        vehicle.OnNext(Nearby);

        recorder.Values.Count.ShouldBe(1);
        recorder.Values[0].ShouldBe(Needle.STDistance(Nearby).Value);
        recorder.Values[0].ShouldBeInRange(1_000, 1_300);
    }

    [TestMethod]
    [DynamicData(nameof(NullDistanceInputs))]
    public void RxDistance_ReportsNaN_WhenSqlServerCannotComputeADistance(string reason, SqlGeography location)
    {
        RxSqlGeography vehicle = new(VisualizedOptions());
        Recorder recorder = new();
        using IDisposable subscription = vehicle.RxDistance(Needle, recorder);

        vehicle.OnNext(location);
        vehicle.OnNext(Nearby);

        recorder.Values.Count.ShouldBe(2, reason);
        double.IsNaN(recorder.Values[0]).ShouldBeTrue(reason);
        double.IsNaN(recorder.Values[1]).ShouldBeFalse(reason);
    }

    [TestMethod]
    public void RxRxDistance_ReportsMetresAsDouble()
    {
        RxSqlGeography a = new(VisualizedOptions());
        RxSqlGeography b = new(VisualizedOptions());
        b.OnNext(Needle);
        Recorder recorder = new();
        using IDisposable subscription = a.RxRxDistance(b, recorder);

        a.OnNext(Nearby);

        recorder.Values.ShouldBe([Needle.STDistance(Nearby).Value]);
    }

    [TestMethod]
    [DynamicData(nameof(NullDistanceInputs))]
    public void RxRxDistance_ReportsNaN_WhenSqlServerCannotComputeADistance(string reason, SqlGeography location)
    {
        RxSqlGeography a = new(VisualizedOptions());
        RxSqlGeography b = new(VisualizedOptions());
        b.OnNext(Needle);
        Recorder recorder = new();
        using IDisposable subscription = a.RxRxDistance(b, recorder);

        a.OnNext(location);

        recorder.Values.Count.ShouldBe(1, reason);
        double.IsNaN(recorder.Values[0]).ShouldBeTrue(reason);
    }

    [TestMethod]
    [DynamicData(nameof(NullDistanceInputs))]
    public void DistanceTo_StreamsNaN_WhenSqlServerCannotComputeADistance(string reason, SqlGeography location)
    {
        RxSqlGeography a = new(VisualizedOptions());
        RxSqlGeography b = new(VisualizedOptions());
        b.OnNext(Needle);
        List<double> stationary = [];
        List<double> moving = [];
        using IDisposable stationarySub = a.DistanceTo(Needle).Subscribe(stationary.Add);
        using IDisposable movingSub = a.DistanceTo(b).Subscribe(moving.Add);

        a.OnNext(location);
        a.OnNext(Nearby);

        foreach (List<double> distances in new[] { stationary, moving })
        {
            distances.Count.ShouldBe(2, reason);
            double.IsNaN(distances[0]).ShouldBeTrue(reason);
            distances[1].ShouldBe(Needle.STDistance(Nearby).Value);
        }
    }

    private static RxSpatialOptions VisualizedOptions()
        => new()
        {
            PreferredDataStruct = RxGeographyDataStruct.List,
            IsVisualized = true,
            QueueLimit = 64,
        };

    private sealed class Recorder : IObserver<double>
    {
        public List<double> Values { get; } = [];

        public void OnNext(double value) => Values.Add(value);

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
