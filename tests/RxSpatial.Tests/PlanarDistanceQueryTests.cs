using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Geo;
using Shouldly;

namespace RxSpatial.Tests;

/// <summary>
/// Planar distance queries report Euclidean distances in the shapes' coordinate units as
/// <see cref="double"/>. Where <see cref="PlanarOperations.Distance"/> has no distance (an
/// empty shape) the query emits <see cref="double.NaN"/>, so every location update still
/// produces one notification — the same rule as the geographic <see cref="DistanceQueryTests"/>.
/// </summary>
[TestClass]
public class PlanarDistanceQueryTests
{
    private static readonly PlanarShape Needle = new PlanarPointShape(0, 0);
    private static readonly PlanarShape Nearby = new PlanarPointShape(3, 4);

    public static IEnumerable<object[]> NaNDistanceInputs =>
    [
        ["empty shape", PlanarEmpty.Instance],
    ];

    [TestMethod]
    public void Distance_IsNaN_ForTheInputsTheseTestsRelyOn()
    {
        // Guards the premise: if the maths ever computes this, the NaN tests below test nothing.
        double.IsNaN(PlanarOperations.Distance(Needle, PlanarEmpty.Instance)).ShouldBeTrue();
    }

    [TestMethod]
    public void RxDistance_ReportsEuclideanUnitsAsDouble()
    {
        RxGeometry forklift = new(VisualizedOptions());
        Recorder recorder = new();
        using IDisposable subscription = forklift.RxDistance(Needle, recorder);

        forklift.OnNext(Nearby);

        recorder.Values.ShouldBe([5]);
    }

    [TestMethod]
    [DynamicData(nameof(NaNDistanceInputs))]
    public void RxDistance_ReportsNaN_WhenThereIsNoDistance(string reason, PlanarShape location)
    {
        RxGeometry forklift = new(VisualizedOptions());
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
        RxGeometry a = new(VisualizedOptions());
        RxGeometry b = new(VisualizedOptions());
        b.OnNext(Needle);
        Recorder recorder = new();
        using IDisposable subscription = a.RxRxDistance(b, recorder);

        a.OnNext(Nearby);

        recorder.Values.ShouldBe([5]);
    }

    [TestMethod]
    [DynamicData(nameof(NaNDistanceInputs))]
    public void RxRxDistance_ReportsNaN_WhenThereIsNoDistance(string reason, PlanarShape location)
    {
        RxGeometry a = new(VisualizedOptions());
        RxGeometry b = new(VisualizedOptions());
        b.OnNext(Needle);
        Recorder recorder = new();
        using IDisposable subscription = a.RxRxDistance(b, recorder);

        a.OnNext(location);

        recorder.Values.Count.ShouldBe(1, reason);
        double.IsNaN(recorder.Values[0]).ShouldBeTrue(reason);
    }

    [TestMethod]
    [DynamicData(nameof(NaNDistanceInputs))]
    public void DistanceTo_StreamsNaN_WhenThereIsNoDistance(string reason, PlanarShape location)
    {
        RxGeometry a = new(VisualizedOptions());
        RxGeometry b = new(VisualizedOptions());
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
