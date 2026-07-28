using System.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace RxSpatial.Tests;

/// <summary>Sanity tests for the vendored R-tree and RUM-tree index structures.</summary>
[TestClass]
public class TreeIndexTests
{
    [TestMethod]
    public void RTree_AddDeleteIntersects_Roundtrip()
    {
        RTree.RTree<int> tree = new();
        RTree.Rectangle a = new(0, 0, 1, 1);
        RTree.Rectangle b = new(10, 10, 11, 11);
        tree.Add(a, 1);
        tree.Add(b, 2);

        tree.Count.ShouldBe(2);
        tree.Intersects(new RTree.Rectangle(0.5, 0.5, 2, 2)).ShouldBe([1]);

        tree.Delete(a, 1).ShouldBeTrue();
        tree.Count.ShouldBe(1);
        tree.Intersects(new RTree.Rectangle(0.5, 0.5, 2, 2)).ShouldBeEmpty();
        tree.Intersects(new RTree.Rectangle(9, 9, 12, 12)).ShouldBe([2]);
    }

    [TestMethod]
    public void RUMTree_AddIntersects_FindsLiveEntries()
    {
        ConfigurationManager.AppSettings["RUMTreeGCInspectionInterval"] = "5";

        RUMTree.RUMTree<int> tree = new();
        tree.Add(new RUMTree.Rectangle(0, 0, 1, 1), 1);
        tree.Add(new RUMTree.Rectangle(10, 10, 11, 11), 2);

        tree.Intersects(new RUMTree.Rectangle(0.5, 0.5, 2, 2)).ShouldBe([1]);
        tree.Intersects(new RUMTree.Rectangle(9, 9, 12, 12)).ShouldBe([2]);
        tree.Intersects(new RUMTree.Rectangle(5, 5, 6, 6)).ShouldBeEmpty();
    }

    [TestMethod]
    public void RUMTree_FrequentUpdates_QueriesReturnCurrentPositionOnly()
    {
        ConfigurationManager.AppSettings["RUMTreeGCInspectionInterval"] = "5";

        RUMTree.RUMTree<int> tree = new();
        RUMTree.Rectangle current = new(0, 0, 1, 1);
        tree.Add(current, 42);
        for (int i = 1; i <= 50; i++)
        {
            RUMTree.Rectangle next = new(i, i, i + 1, i + 1);
            tree.Delete(current, 42);
            tree.Add(next, 42);
            current = next;
        }

        tree.Intersects(new RUMTree.Rectangle(49.5, 49.5, 51, 51)).ShouldBe([42]);
        tree.Intersects(new RUMTree.Rectangle(0, 0, 5, 5)).ShouldBeEmpty();
    }
}
