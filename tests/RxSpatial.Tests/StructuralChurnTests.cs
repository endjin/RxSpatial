// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Index;
using Shouldly;

namespace RxSpatial.Tests;

/// <summary>
/// Deep-collapse churn: draining a multi-level tree in spatial order empties whole subtrees,
/// which drives internal-node underflow, orphan-subtree reinsertion, root shrink/regrowth and
/// (for the RUM-tree) cleaning-token hand-off while the token's own leaf is eliminated. Every
/// scenario is verified against a naive oracle and ends by rebuilding on the collapsed tree.
/// </summary>
[TestClass]
public class StructuralChurnTests
{
    private static RTreeOptions TinyOptions => new() { MaxEntries = 4, MinEntries = 2, ReinsertCount = 1 };

    private static RumTreeOptions TinyRumOptions(int cleaningInterval)
        => new() { MaxEntries = 4, MinEntries = 2, ReinsertCount = 1, CleaningInterval = cleaningInterval };

    /// <summary>A side×side grid of unit squares, giving a deterministic multi-level tree.</summary>
    private static Dictionary<int, Rect> GridItems(int side)
    {
        Dictionary<int, Rect> items = [];
        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
                items.Add((y * side) + x, new Rect(x * 3, y * 3, (x * 3) + 1, (y * 3) + 1));
        }

        return items;
    }

    private static IEnumerable<int> Drain(int side, DrainOrder order)
        => order switch
        {
            DrainOrder.RowMajor => Enumerable.Range(0, side * side),
            DrainOrder.Reverse => Enumerable.Range(0, side * side).Reverse(),
            DrainOrder.ColumnMajor => from x in Enumerable.Range(0, side)
                                      from y in Enumerable.Range(0, side)
                                      select (y * side) + x,
            DrainOrder.InsideOut => Enumerable.Range(0, side * side)
                .OrderBy(id => Math.Abs((id % side) - (side / 2)) + Math.Abs((id / side) - (side / 2))),
            _ => throw new ArgumentOutOfRangeException(nameof(order)),
        };

    [TestMethod]
    [DataRow(TreeKind.RStar, DrainOrder.RowMajor)]
    [DataRow(TreeKind.RStar, DrainOrder.Reverse)]
    [DataRow(TreeKind.RStar, DrainOrder.ColumnMajor)]
    [DataRow(TreeKind.RStar, DrainOrder.InsideOut)]
    [DataRow(TreeKind.Rum, DrainOrder.RowMajor)]
    [DataRow(TreeKind.Rum, DrainOrder.ColumnMajor)]
    [DataRow(TreeKind.Rum, DrainOrder.InsideOut)]
    public void SpatialOrderDrain_CollapsesThenRebuilds(TreeKind kind, DrainOrder order)
    {
        const int Side = 12;
        ISpatialIndex<int> index = kind == TreeKind.RStar
            ? new RStarTree<int>(TinyOptions)
            : new RumTree<int>(TinyRumOptions(cleaningInterval: 1));
        Dictionary<int, Rect> oracle = GridItems(Side);

        foreach ((int id, Rect bounds) in oracle)
            index.Insert(id, bounds);

        index.Count.ShouldBe(oracle.Count);

        // Removing spatially-contiguous blocks empties whole subtrees: parents underflow,
        // orphaned subtrees are reinserted, and the root shrinks (and regrows) repeatedly.
        Dictionary<int, Rect> live = new(oracle);
        int step = 0;
        foreach (int id in Drain(Side, order))
        {
            index.Remove(id).ShouldBeTrue($"item {id} missing at drain step {step}");
            live.Remove(id);
            index.Count.ShouldBe(live.Count, $"count diverged at drain step {step}");

            if (step % 7 == 0)
                VerifyAgainstOracle(index, live);

            step++;
        }

        index.Count.ShouldBe(0);
        index.Search(EverythingWindow).ShouldBeEmpty();

        // The collapsed tree must still be structurally sound: rebuild and query everything.
        foreach ((int id, Rect bounds) in oracle)
            index.Insert(id, bounds);

        VerifyAgainstOracle(index, oracle);
    }

    [TestMethod]
    [DataRow(TreeKind.RStar)]
    [DataRow(TreeKind.Rum)]
    public void SplitAfterClear_RestoresInternalStructure(TreeKind kind)
    {
        ISpatialIndex<int> index = kind == TreeKind.RStar
            ? new RStarTree<int>(TinyOptions)
            : new RumTree<int>(TinyRumOptions(cleaningInterval: 1));
        Dictionary<int, Rect> items = GridItems(6);

        foreach ((int id, Rect bounds) in items)
            index.Insert(id, bounds);

        index.Clear();
        index.Count.ShouldBe(0);
        index.Search(EverythingWindow).ShouldBeEmpty();

        // Growing well past one node forces splits on the post-Clear tree (for the RUM-tree
        // this re-links the cleaning ring from empty); then drain half to prove cleaning and
        // condensing still work on the rebuilt structure.
        foreach ((int id, Rect bounds) in items)
            index.Insert(id, bounds);

        VerifyAgainstOracle(index, items);

        Dictionary<int, Rect> live = new(items);
        foreach (int id in items.Keys.Where(id => id % 2 == 0))
        {
            index.Remove(id).ShouldBeTrue();
            live.Remove(id);
        }

        VerifyAgainstOracle(index, live);
    }

    [TestMethod]
    public void RumTree_DrainAndRefill_Cycles_KeepGarbageBounded()
    {
        RumTree<int> index = new(TinyRumOptions(cleaningInterval: 1));
        Dictionary<int, Rect> items = GridItems(8);

        // Repeated full drain/refill cycles walk the cleaning token over every leaf the
        // condense pass eliminates, including the leaf the token currently rests on.
        for (int cycle = 0; cycle < 3; cycle++)
        {
            foreach ((int id, Rect bounds) in items)
                index.Insert(id, bounds);

            VerifyAgainstOracle(index, items);

            foreach (int id in items.Keys)
                index.Remove(id).ShouldBeTrue();

            index.Count.ShouldBe(0);
            index.Search(EverythingWindow).ShouldBeEmpty();
            index.PendingObsoleteCount.ShouldBeLessThan(items.Count, "cleaning stalled during drain");
        }
    }

    private static Rect EverythingWindow => new(-1000, -1000, 1000, 1000);

    private static void VerifyAgainstOracle(ISpatialIndex<int> index, Dictionary<int, Rect> live)
    {
        List<int> actual = index.Search(EverythingWindow);
        actual.Count.ShouldBe(actual.Distinct().Count(), "search returned duplicates");
        actual.Sort();

        List<int> expected = [.. live.Keys];
        expected.Sort();
        actual.ShouldBe(expected);
    }
}
