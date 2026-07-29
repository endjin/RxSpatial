using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Index;
using Shouldly;

namespace RxSpatial.Tests;

/// <summary>Edge behaviour of the spatial indexes surfaced by coverage analysis.</summary>
[TestClass]
public class SpatialIndexEdgeTests
{
    [TestMethod]
    public void ParameterlessConstructors_UseTunedDefaults()
    {
        RStarTree<int> rstar = new();
        RumTree<int> rum = new();

        rstar.Insert(1, new Rect(0, 0, 1, 1));
        rum.Insert(1, new Rect(0, 0, 1, 1));

        rstar.Search(new Rect(0, 0, 1, 1)).ShouldBe([1]);
        rum.Search(new Rect(0, 0, 1, 1)).ShouldBe([1]);
    }

    [TestMethod]
    public void Rect_RejectsNaN_AndNormalisesCorners()
    {
        Should.Throw<ArgumentException>(() => new Rect(double.NaN, 0, 1, 1));
        Should.Throw<ArgumentException>(() => new Rect(0, 0, 1, double.NaN));

        Rect normalised = new(5, 6, 1, 2); // opposite corners in "wrong" order
        normalised.MinX.ShouldBe(1);
        normalised.MinY.ShouldBe(2);
        normalised.MaxX.ShouldBe(5);
        normalised.MaxY.ShouldBe(6);
        normalised.Area.ShouldBe(16);
        normalised.Margin.ShouldBe(8);
        normalised.Enlargement(new Rect(0, 0, 1, 2)).ShouldBe((5 * 6) - 16);
    }

    [TestMethod]
    public void Search_BufferOverload_AppendsWithoutAllocatingResults()
    {
        RStarTree<int> tree = new();
        for (int i = 0; i < 100; i++)
            tree.Insert(i, new Rect(i, 0, i + 0.5, 0.5));

        List<int> buffer = [999];
        tree.Search(new Rect(10, 0, 12, 1), buffer);

        buffer.ShouldContain(999); // appending contract: the buffer is not cleared
        buffer.Where(i => i != 999).OrderBy(i => i).ShouldBe([10, 11, 12]);
    }

    [TestMethod]
    [DataRow("rstar")]
    [DataRow("rum")]
    public void DrainingTheEntireIndex_ThenReusingIt_Works(string kind)
    {
        ISpatialIndex<int> index = kind == "rstar"
            ? new RStarTree<int>(new RTreeOptions { MaxEntries = 4, MinEntries = 2, ReinsertCount = 1 })
            : new RumTree<int>(new RumTreeOptions { MaxEntries = 4, MinEntries = 2, ReinsertCount = 1, CleaningInterval = 2 });

        // Clustered inserts build a multi-level tree with tiny nodes; removing everything in a
        // shuffled order forces deep condensation cascades (the tree-collapse machinery).
        Random rng = new(1234);
        List<int> ids = [];
        for (int i = 0; i < 300; i++)
        {
            double cx = (i % 3) * 100;
            double x = cx + rng.NextDouble();
            double y = rng.NextDouble();
            index.Insert(i, new Rect(x, y, x + 0.1, y + 0.1));
            ids.Add(i);
        }

        // Shuffle removal order deterministically.
        for (int i = ids.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }

        foreach (int id in ids)
            index.Remove(id).ShouldBeTrue();

        index.Count.ShouldBe(0);
        index.Search(new Rect(-1000, -1000, 1000, 1000)).ShouldBeEmpty();

        // The drained index must remain fully usable.
        index.Insert(9999, new Rect(50, 50, 51, 51));
        index.Search(new Rect(49, 49, 52, 52)).ShouldBe([9999]);
        index.Count.ShouldBe(1);
    }

    [TestMethod]
    public void Clear_OnRumTree_ResetsMemoAndPendingGarbage()
    {
        RumTree<int> tree = new(new RumTreeOptions { CleaningInterval = int.MaxValue });
        for (int i = 0; i < 20; i++)
            tree.Insert(i, new Rect(i, 0, i + 1, 1));
        for (int i = 0; i < 20; i++)
            tree.Update(i, new Rect(i, 10, i + 1, 11));

        tree.PendingObsoleteCount.ShouldBe(20);
        tree.Clear();
        tree.Count.ShouldBe(0);
        tree.PendingObsoleteCount.ShouldBe(0);
        tree.Search(new Rect(-100, -100, 100, 100)).ShouldBeEmpty();

        tree.Insert(1, new Rect(0, 0, 1, 1));
        tree.Search(new Rect(0, 0, 1, 1)).ShouldBe([1]);
    }
}
