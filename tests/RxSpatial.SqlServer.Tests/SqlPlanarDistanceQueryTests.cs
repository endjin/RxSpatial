using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace RxSpatial.SqlServer.Tests;

/// <summary>
/// Planar distance queries report Euclidean distances in the geometries' coordinate units as
/// <see cref="double"/>. Where SQL Server's <c>STDistance</c> returns <c>SqlDouble.Null</c>
/// the query emits <see cref="double.NaN"/>, so every location update still produces one
/// notification — the same rule as the geographic <see cref="SqlDistanceQueryTests"/>.
/// </summary>
[TestClass]
[TestCategory("SqlServer")]
[OSCondition(OperatingSystems.Windows)]
public class SqlPlanarDistanceQueryTests
{
    private static readonly SqlGeometry Needle = SqlGeometry.Point(0, 0, 0);
    private static readonly SqlGeometry Nearby = SqlGeometry.Point(3, 4, 0);

    // The same coordinates in Web Mercator (SRID 3857): SQL Server will not measure across SRIDs.
    private static readonly SqlGeometry NeedleInOtherSrid = SqlGeometry.Point(0, 0, 3857);
    private static readonly SqlGeometry EmptyPoint = SqlGeometry.Parse("POINT EMPTY"); // SRID 0

    public static IEnumerable<object[]> NullDistanceInputs =>
    [
        ["different SRIDs", NeedleInOtherSrid],
        ["empty geometry", EmptyPoint],
    ];

    [TestMethod]
    public void StDistance_ReturnsNull_ForTheInputsTheseTestsRelyOn()
    {
        // Guards the premise: if SQL Server ever computes these, the NaN tests below test nothing.
        Needle.STDistance(NeedleInOtherSrid).IsNull.ShouldBeTrue();
        Needle.STDistance(EmptyPoint).IsNull.ShouldBeTrue();
    }

    [TestMethod]
    public void RxDistance_ReportsEuclideanUnitsAsDouble()
    {
        RxSqlGeometry forklift = new(VisualizedOptions());
        Recorder recorder = new();
        using IDisposable subscription = forklift.RxDistance(Needle, recorder);

        forklift.OnNext(Nearby);

        recorder.Values.ShouldBe([5]);
    }

    [TestMethod]
    [DynamicData(nameof(NullDistanceInputs))]
    public void RxDistance_ReportsNaN_WhenSqlServerCannotComputeADistance(string reason, SqlGeometry location)
    {
        RxSqlGeometry forklift = new(VisualizedOptions());
        Recorder recorder = new();
        using IDisposable subscription = forklift.RxDistance(Needle, recorder);

        forklift.OnNext(location);
        forklift.OnNext(Nearby);

        recorder.Values.Count.ShouldBe(2, reason);
        double.IsNaN(recorder.Values[0]).ShouldBeTrue(reason);
        recorder.Values[1].ShouldBe(5, reason);
    }

    [TestMethod]
    public void RxRxDistance_ReportsEuclideanUnitsAsDouble()
    {
        RxSqlGeometry a = new(VisualizedOptions());
        RxSqlGeometry b = new(VisualizedOptions());
        b.OnNext(Needle);
        Recorder recorder = new();
        using IDisposable subscription = a.RxRxDistance(b, recorder);

        a.OnNext(Nearby);

        recorder.Values.ShouldBe([5]);
    }

    [TestMethod]
    [DynamicData(nameof(NullDistanceInputs))]
    public void RxRxDistance_ReportsNaN_WhenSqlServerCannotComputeADistance(string reason, SqlGeometry location)
    {
        RxSqlGeometry a = new(VisualizedOptions());
        RxSqlGeometry b = new(VisualizedOptions());
        b.OnNext(Needle);
        Recorder recorder = new();
        using IDisposable subscription = a.RxRxDistance(b, recorder);

        a.OnNext(location);

        recorder.Values.Count.ShouldBe(1, reason);
        double.IsNaN(recorder.Values[0]).ShouldBeTrue(reason);
    }

    [TestMethod]
    [DynamicData(nameof(NullDistanceInputs))]
    public void DistanceTo_StreamsNaN_WhenSqlServerCannotComputeADistance(string reason, SqlGeometry location)
    {
        RxSqlGeometry a = new(VisualizedOptions());
        RxSqlGeometry b = new(VisualizedOptions());
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
            distances[1].ShouldBe(5, reason);
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
