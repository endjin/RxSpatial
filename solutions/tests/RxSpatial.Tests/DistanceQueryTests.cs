// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Geo;
using Shouldly;

namespace RxSpatial.Tests;

/// <summary>
/// Distance queries report <see cref="double"/> metres, as the RxSpatial papers declare them.
/// Where <see cref="GeoOperations.Distance"/> has no distance — an empty shape, or a nearly
/// antipodal pair whose geodesic does not converge — the query emits <see cref="double.NaN"/>,
/// so every location update still produces one notification.
/// </summary>
[TestClass]
public class DistanceQueryTests
{
    private static readonly GeoShape Needle = Point(47.6205, -122.3493);
    private static readonly GeoShape Nearby = Point(47.6300, -122.3550);

    // The Space Needle's antipode, in the southern Indian Ocean: Vincenty's inverse
    // iteration does not converge there, and the distance is reported as NaN.
    private static readonly GeoShape Antipode = Point(-47.6205, 57.6507);

    public static IEnumerable<object[]> NaNDistanceInputs =>
    [
        ["antipodal point", Antipode],
        ["empty shape", GeoEmpty.Instance],
    ];

    [TestMethod]
    public void Distance_IsNaN_ForTheInputsTheseTestsRelyOn()
    {
        // Guards the premise: if the maths ever computes these, the NaN tests below test nothing.
        double.IsNaN(GeoOperations.Distance(Needle, Antipode)).ShouldBeTrue();
        double.IsNaN(GeoOperations.Distance(Needle, GeoEmpty.Instance)).ShouldBeTrue();
    }

    [TestMethod]
    public void RxDistance_ReportsMetresAsDouble()
    {
        RxGeography vehicle = new(VisualizedOptions());
        Recorder recorder = new();
        using IDisposable subscription = vehicle.RxDistance(Needle, recorder);

        vehicle.OnNext(Nearby);

        recorder.Values.Count.ShouldBe(1);
        recorder.Values[0].ShouldBe(GeoOperations.Distance(Needle, Nearby));
        recorder.Values[0].ShouldBeInRange(1_000, 1_300);
    }

    [TestMethod]
    [DynamicData(nameof(NaNDistanceInputs))]
    public void RxDistance_ReportsNaN_WhenThereIsNoDistance(string reason, GeoShape location)
    {
        RxGeography vehicle = new(VisualizedOptions());
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
        RxGeography a = new(VisualizedOptions());
        RxGeography b = new(VisualizedOptions());
        b.OnNext(Needle);
        Recorder recorder = new();
        using IDisposable subscription = a.RxRxDistance(b, recorder);

        a.OnNext(Nearby);

        recorder.Values.ShouldBe([GeoOperations.Distance(Needle, Nearby)]);
    }

    [TestMethod]
    [DynamicData(nameof(NaNDistanceInputs))]
    public void RxRxDistance_ReportsNaN_WhenThereIsNoDistance(string reason, GeoShape location)
    {
        RxGeography a = new(VisualizedOptions());
        RxGeography b = new(VisualizedOptions());
        b.OnNext(Needle);
        Recorder recorder = new();
        using IDisposable subscription = a.RxRxDistance(b, recorder);

        a.OnNext(location);

        recorder.Values.Count.ShouldBe(1, reason);
        double.IsNaN(recorder.Values[0]).ShouldBeTrue(reason);
    }

    [TestMethod]
    [DynamicData(nameof(NaNDistanceInputs))]
    public void DistanceTo_StreamsNaN_WhenThereIsNoDistance(string reason, GeoShape location)
    {
        RxGeography a = new(VisualizedOptions());
        RxGeography b = new(VisualizedOptions());
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
            distances[1].ShouldBe(GeoOperations.Distance(Needle, Nearby));
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

    private static GeoPointShape Point(double latitude, double longitude) => new(latitude, longitude);

    private static GeoCircle Disc(double latitude, double longitude, double radiusMetres)
        => new(new GeoPoint(latitude, longitude), radiusMetres);
}
