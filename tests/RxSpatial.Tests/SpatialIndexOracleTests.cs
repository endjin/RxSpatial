using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Index;
using Shouldly;

namespace RxSpatial.Tests;

/// <summary>
/// The rewrite-safety harness: seeded randomized insert/update/remove/search workloads whose
/// results are compared, operation by operation, against a naive full-scan oracle.
/// </summary>
[TestClass]
public class SpatialIndexOracleTests
{
    private static ISpatialIndex<int> Create(OracleFlavour flavour)
        => flavour switch
        {
            OracleFlavour.RStar => new RStarTree<int>(),
            OracleFlavour.RStarNoReinsert => new RStarTree<int>(new RTreeOptions { ReinsertCount = 0 }),
            OracleFlavour.RStarTinyNodes =>
                new RStarTree<int>(new RTreeOptions { MaxEntries = 4, MinEntries = 2, ReinsertCount = 1 }),
            OracleFlavour.RumEager => new RumTree<int>(new RumTreeOptions { CleaningInterval = 1 }),
            OracleFlavour.RumLazy => new RumTree<int>(new RumTreeOptions { CleaningInterval = int.MaxValue }),
            OracleFlavour.RumTinyNodes => new RumTree<int>(
                new RumTreeOptions { MaxEntries = 4, MinEntries = 2, ReinsertCount = 1, CleaningInterval = 3 }),
            _ => throw new ArgumentOutOfRangeException(nameof(flavour)),
        };

    [TestMethod]
    [DataRow(OracleFlavour.RStar, 12345)]
    [DataRow(OracleFlavour.RStar, 987)]
    [DataRow(OracleFlavour.RStarNoReinsert, 12345)]
    [DataRow(OracleFlavour.RStarTinyNodes, 4242)]
    [DataRow(OracleFlavour.RumEager, 12345)]
    [DataRow(OracleFlavour.RumEager, 987)]
    [DataRow(OracleFlavour.RumLazy, 12345)]
    [DataRow(OracleFlavour.RumTinyNodes, 4242)]
    public void RandomizedWorkload_MatchesNaiveOracle(OracleFlavour flavour, int seed)
        => RunWorkload(Create(flavour), seed, clustered: false);

    [TestMethod]
    [DataRow(OracleFlavour.RStar, 555)]
    [DataRow(OracleFlavour.RStarTinyNodes, 555)]
    [DataRow(OracleFlavour.RumEager, 555)]
    [DataRow(OracleFlavour.RumLazy, 555)]
    public void ClusteredWorkload_WithDuplicatePositions_MatchesNaiveOracle(OracleFlavour flavour, int seed)
        => RunWorkload(Create(flavour), seed, clustered: true);

    private static void RunWorkload(ISpatialIndex<int> index, int seed, bool clustered)
    {
        Random rng = new(seed);
        Dictionary<int, Rect> oracle = [];
        List<int> liveIds = [];
        int nextId = 0;

        for (int op = 0; op < 800; op++)
        {
            int dice = rng.Next(100);
            if (dice < 35 || liveIds.Count == 0)
            {
                int id = nextId++;
                Rect bounds = RandomRect(rng, clustered);
                index.Insert(id, bounds);
                oracle.Add(id, bounds);
                liveIds.Add(id);
            }
            else if (dice < 65)
            {
                int id = liveIds[rng.Next(liveIds.Count)];
                Rect bounds = RandomRect(rng, clustered);
                index.Update(id, bounds);
                oracle[id] = bounds;
            }
            else if (dice < 80)
            {
                int pick = rng.Next(liveIds.Count);
                int id = liveIds[pick];
                index.Remove(id).ShouldBeTrue();
                oracle.Remove(id);
                liveIds[pick] = liveIds[^1];
                liveIds.RemoveAt(liveIds.Count - 1);
            }
            else if (dice < 85)
            {
                index.Remove(nextId + 1_000_000).ShouldBeFalse();
            }
            else
            {
                VerifySearch(index, oracle, RandomWindow(rng, clustered));
            }

            index.Count.ShouldBe(oracle.Count, $"live count diverged at operation {op}");
        }

        VerifySearch(index, oracle, new Rect(double.MinValue, double.MinValue, double.MaxValue, double.MaxValue));
    }

    private static void VerifySearch(ISpatialIndex<int> index, Dictionary<int, Rect> oracle, Rect window)
    {
        List<int> actual = index.Search(window);
        actual.Count.ShouldBe(actual.Distinct().Count(), "search returned duplicates");

        List<int> expected = [];
        foreach ((int id, Rect bounds) in oracle)
        {
            if (bounds.Intersects(window))
                expected.Add(id);
        }

        actual.Sort();
        expected.Sort();
        actual.ShouldBe(expected);
    }

    private static Rect RandomRect(Random rng, bool clustered)
    {
        if (clustered)
        {
            // Three tight clusters with frequent exact-duplicate positions to stress tie-breaking.
            double cx = rng.Next(3) * 40;
            double cy = rng.Next(3) * 40;
            double x = cx + rng.Next(5);
            double y = cy + rng.Next(5);
            return new Rect(x, y, x + 1, y + 1);
        }

        double px = rng.NextDouble() * 1000;
        double py = rng.NextDouble() * 1000;
        double w = rng.NextDouble() * 8;
        double h = rng.NextDouble() * 8;
        return new Rect(px, py, px + w, py + h);
    }

    private static Rect RandomWindow(Random rng, bool clustered)
    {
        if (clustered)
        {
            double x = rng.Next(-5, 90);
            double y = rng.Next(-5, 90);
            return new Rect(x, y, x + rng.Next(1, 50), y + rng.Next(1, 50));
        }

        double px = rng.NextDouble() * 1000;
        double py = rng.NextDouble() * 1000;
        return new Rect(px, py, px + (rng.NextDouble() * 120), py + (rng.NextDouble() * 120));
    }
}
