using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Index;
using Shouldly;

namespace RxSpatial.Tests;

/// <summary>Update-memo and cleaning-token behaviour specific to the RUM-tree.</summary>
[TestClass]
public class RumTreeCleaningTests
{
    [TestMethod]
    public void MemoFiltering_WithCleaningDisabled_HidesEveryObsoleteEntry()
    {
        var tree = new RumTree<int>(new RumTreeOptions { CleaningInterval = int.MaxValue });
        for (int i = 0; i < 50; i++)
            tree.Insert(i, new Rect(i, 0, i + 0.5, 0.5));

        // Ten update rounds per item; old entries stay physically present but memo-hidden.
        for (int round = 1; round <= 10; round++)
        {
            for (int i = 0; i < 50; i++)
                tree.Update(i, new Rect(i, round * 10, i + 0.5, (round * 10) + 0.5));
        }

        tree.Count.ShouldBe(50);
        tree.PendingObsoleteCount.ShouldBe(500);

        List<int> latestBand = tree.Search(new Rect(-1, 99, 51, 101));
        latestBand.Sort();
        latestBand.ShouldBe(Enumerable.Range(0, 50).ToList());

        tree.Search(new Rect(-1, -1, 51, 95)).ShouldBeEmpty();
    }

    [TestMethod]
    public void CleaningToken_KeepsPendingGarbageBounded()
    {
        var tree = new RumTree<int>(new RumTreeOptions { CleaningInterval = 1 });
        for (int i = 0; i < 50; i++)
            tree.Insert(i, new Rect(i, 0, i + 0.5, 0.5));

        for (int round = 1; round <= 10; round++)
        {
            for (int i = 0; i < 50; i++)
                tree.Update(i, new Rect(i, round * 10, i + 0.5, (round * 10) + 0.5));
        }

        tree.Count.ShouldBe(50);
        tree.PendingObsoleteCount.ShouldBeLessThan(100);

        List<int> latestBand = tree.Search(new Rect(-1, 99, 51, 101));
        latestBand.Sort();
        latestBand.ShouldBe(Enumerable.Range(0, 50).ToList());
        tree.Search(new Rect(-1, -1, 51, 95)).ShouldBeEmpty();
    }

    [TestMethod]
    public void Remove_WritesATombstone_AndReinsertRevivesTheItem()
    {
        var tree = new RumTree<int>(new RumTreeOptions { CleaningInterval = int.MaxValue });
        tree.Insert(42, new Rect(0, 0, 1, 1));

        tree.Remove(42).ShouldBeTrue();
        tree.Contains(42).ShouldBeFalse();
        tree.Count.ShouldBe(0);
        tree.Search(new Rect(-10, -10, 10, 10)).ShouldBeEmpty();
        tree.Remove(42).ShouldBeFalse();

        tree.Insert(42, new Rect(5, 5, 6, 6));
        tree.Contains(42).ShouldBeTrue();
        tree.Count.ShouldBe(1);
        tree.Search(new Rect(4, 4, 7, 7)).ShouldBe([42]);
        tree.Search(new Rect(-1, -1, 2, 2)).ShouldBeEmpty();
    }

    [TestMethod]
    public void CleaningRetiresMemoEntries_ResultsStayIdenticalToEagerTree()
    {
        var lazy = new RumTree<int>(new RumTreeOptions { CleaningInterval = int.MaxValue });
        var eager = new RumTree<int>(new RumTreeOptions { CleaningInterval = 1 });
        var rng = new Random(99);

        for (int i = 0; i < 30; i++)
        {
            Rect r = new(rng.Next(100), rng.Next(100), rng.Next(100, 110), rng.Next(100, 110));
            lazy.Insert(i, r);
            eager.Insert(i, r);
        }

        for (int op = 0; op < 400; op++)
        {
            int id = rng.Next(30);
            Rect r = new(rng.Next(100), rng.Next(100), rng.Next(100, 110), rng.Next(100, 110));
            lazy.Update(id, r);
            eager.Update(id, r);
        }

        Rect window = new(0, 0, 60, 60);
        List<int> fromLazy = lazy.Search(window);
        List<int> fromEager = eager.Search(window);
        fromLazy.Sort();
        fromEager.Sort();
        fromLazy.ShouldBe(fromEager);
        eager.PendingObsoleteCount.ShouldBeLessThan(lazy.PendingObsoleteCount);
    }
}
