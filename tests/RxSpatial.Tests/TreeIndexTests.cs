using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Index;
using Shouldly;

namespace RxSpatial.Tests;

/// <summary>Shared behavioural contract of both spatial indexes, exercised per implementation.</summary>
[TestClass]
public class TreeIndexTests
{
    private static ISpatialIndex<int> Create(string kind)
        => kind switch
        {
            "rstar" => new RStarTree<int>(),
            "rum" => new RumTree<int>(new RumTreeOptions { CleaningInterval = 5 }),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    [TestMethod]
    [DataRow("rstar")]
    [DataRow("rum")]
    public void InsertSearchRemove_Roundtrip(string kind)
    {
        ISpatialIndex<int> index = Create(kind);
        index.Insert(1, new Rect(0, 0, 1, 1));
        index.Insert(2, new Rect(10, 10, 11, 11));

        index.Count.ShouldBe(2);
        index.Contains(1).ShouldBeTrue();
        index.Search(new Rect(0.5, 0.5, 2, 2)).ShouldBe([1]);
        index.Search(new Rect(9, 9, 12, 12)).ShouldBe([2]);
        index.Search(new Rect(5, 5, 6, 6)).ShouldBeEmpty();

        index.Remove(1).ShouldBeTrue();
        index.Count.ShouldBe(1);
        index.Contains(1).ShouldBeFalse();
        index.Search(new Rect(0.5, 0.5, 2, 2)).ShouldBeEmpty();
        index.Search(new Rect(9, 9, 12, 12)).ShouldBe([2]);
    }

    [TestMethod]
    [DataRow("rstar")]
    [DataRow("rum")]
    public void Search_OnEmptyIndex_ReturnsEmptyListNotNull(string kind)
    {
        ISpatialIndex<int> index = Create(kind);
        List<int> results = index.Search(new Rect(0, 0, 100, 100));
        results.ShouldNotBeNull();
        results.ShouldBeEmpty();
    }

    [TestMethod]
    [DataRow("rstar")]
    [DataRow("rum")]
    public void Remove_UnknownItem_ReturnsFalseWithoutThrowing(string kind)
    {
        ISpatialIndex<int> index = Create(kind);
        index.Remove(12345).ShouldBeFalse();
    }

    [TestMethod]
    [DataRow("rstar")]
    [DataRow("rum")]
    public void Insert_DuplicateLiveItem_Throws(string kind)
    {
        ISpatialIndex<int> index = Create(kind);
        index.Insert(7, new Rect(0, 0, 1, 1));
        Should.Throw<ArgumentException>(() => index.Insert(7, new Rect(2, 2, 3, 3)));
    }

    [TestMethod]
    [DataRow("rstar")]
    [DataRow("rum")]
    public void Update_ActsAsUpsert(string kind)
    {
        ISpatialIndex<int> index = Create(kind);
        index.Update(9, new Rect(0, 0, 1, 1));
        index.Count.ShouldBe(1);
        index.Search(new Rect(0, 0, 1, 1)).ShouldBe([9]);
    }

    [TestMethod]
    [DataRow("rstar")]
    [DataRow("rum")]
    public void Update_Churn_KeepsExactlyOneVisibleEntry(string kind)
    {
        ISpatialIndex<int> index = Create(kind);
        Rect current = new(0, 0, 1, 1);
        index.Insert(42, current);
        for (int i = 1; i <= 50; i++)
        {
            current = new Rect(i, i, i + 1, i + 1);
            index.Update(42, current);
        }

        index.Count.ShouldBe(1);
        index.Search(new Rect(49.5, 49.5, 51, 51)).ShouldBe([42]);
        index.Search(new Rect(0, 0, 5, 5)).ShouldBeEmpty();
        index.Search(new Rect(-100, -100, 100, 100)).ShouldBe([42]);
    }

    [TestMethod]
    [DataRow("rstar")]
    [DataRow("rum")]
    public void Count_StaysLive_ThroughChurnAndRemovals(string kind)
    {
        ISpatialIndex<int> index = Create(kind);
        for (int i = 0; i < 20; i++)
            index.Insert(i, new Rect(i, i, i + 1, i + 1));
        for (int round = 0; round < 5; round++)
        {
            for (int i = 0; i < 20; i++)
                index.Update(i, new Rect(i + round, i, i + round + 1, i + 1));
        }

        index.Count.ShouldBe(20);

        for (int i = 0; i < 10; i++)
            index.Remove(i).ShouldBeTrue();
        index.Count.ShouldBe(10);
    }

    [TestMethod]
    [DataRow("rstar")]
    [DataRow("rum")]
    public void PointRectangles_AreValidEntriesAndQueries(string kind)
    {
        ISpatialIndex<int> index = Create(kind);
        index.Insert(1, new Rect(5, 5, 5, 5));
        index.Search(new Rect(5, 5, 5, 5)).ShouldBe([1]);
        index.Search(new Rect(4, 4, 6, 6)).ShouldBe([1]);
        index.Search(new Rect(5.1, 5.1, 6, 6)).ShouldBeEmpty();
        index.Remove(1).ShouldBeTrue();
        index.Count.ShouldBe(0);
    }

    [TestMethod]
    [DataRow("rstar")]
    [DataRow("rum")]
    public void LargeWorkload_SurvivesSplitsReinsertionAndCondensation(string kind)
    {
        ISpatialIndex<int> index = Create(kind);
        Random rng = new(7);
        Rect[] rects = new Rect[2000];
        for (int i = 0; i < rects.Length; i++)
        {
            double x = rng.NextDouble() * 1000;
            double y = rng.NextDouble() * 1000;
            rects[i] = new Rect(x, y, x + 1, y + 1);
            index.Insert(i, rects[i]);
        }

        index.Count.ShouldBe(2000);

        Rect window = new(200, 200, 320, 320);
        List<int> expected = [];
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].Intersects(window))
                expected.Add(i);
        }

        List<int> actual = index.Search(window);
        actual.Sort();
        actual.ShouldBe(expected);

        for (int i = 0; i < rects.Length; i += 2)
            index.Remove(i).ShouldBeTrue();
        index.Count.ShouldBe(1000);

        List<int> afterRemoval = index.Search(window);
        afterRemoval.Sort();
        afterRemoval.ShouldBe(expected.Where(i => i % 2 == 1).ToList());
    }

    [TestMethod]
    [DataRow("rstar")]
    [DataRow("rum")]
    public void Clear_ResetsTheIndex(string kind)
    {
        ISpatialIndex<int> index = Create(kind);
        for (int i = 0; i < 100; i++)
            index.Insert(i, new Rect(i, i, i + 1, i + 1));

        index.Clear();
        index.Count.ShouldBe(0);
        index.Search(new Rect(-1000, -1000, 1000, 1000)).ShouldBeEmpty();

        index.Insert(1, new Rect(0, 0, 1, 1));
        index.Search(new Rect(0, 0, 1, 1)).ShouldBe([1]);
    }
}
