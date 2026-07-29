using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using RxSpatial.Index;

namespace RxSpatial.Benchmarks;

/// <summary>
/// Compares the R*-tree (the baseline of each category) against the RUM-tree (R-tree with
/// Update Memo) for bulk insertion, the frequent-update workload the RUM-tree was designed
/// for, and intersection queries. Both trees share the same R* core, so the measured delta
/// is purely the update-memo strategy.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TreeIndexBenchmarks
{
    [Params(1000)]
    public int N;

    private Rect[] insertRects = null!;
    private Rect[] updatePool = null!;
    private Rect[] queryPool = null!;

    private RStarTree<int> rstar = null!;
    private RumTree<int> rum = null!;
    private Rect[] rstarPositions = null!;
    private Rect[] rumPositions = null!;
    private readonly List<int> searchBuffer = new(256);
    private int cursor;

    [GlobalSetup]
    public void Setup()
    {
        Random random = new(42);
        insertRects = new Rect[N];
        for (int i = 0; i < N; i++)
            insertRects[i] = RandomRect(random, 1);

        updatePool = new Rect[1024];
        for (int i = 0; i < updatePool.Length; i++)
            updatePool[i] = RandomRect(random, 1);

        queryPool = new Rect[64];
        for (int i = 0; i < queryPool.Length; i++)
            queryPool[i] = RandomRect(random, 20);

        // Pre-built trees for the update and query benchmarks.
        rstar = new RStarTree<int>();
        rum = new RumTree<int>(new RumTreeOptions { CleaningInterval = 5 });
        for (int i = 0; i < N; i++)
        {
            rstar.Insert(i, insertRects[i]);
            rum.Insert(i, insertRects[i]);
        }

        rstarPositions = (Rect[])insertRects.Clone();
        rumPositions = (Rect[])insertRects.Clone();
    }

    /// <summary>Bounded random walk: shifts the unit rect by ±0.5 per axis, staying in [0, 1000].</summary>
    private static Rect Nudge(in Rect current, int step)
    {
        double dx = (((step * 2654435761u) >> 8 & 1023) / 1023.0) - 0.5;
        double dy = (((step * 2246822519u) >> 8 & 1023) / 1023.0) - 0.5;
        double x = Math.Clamp(current.MinX + dx, 0, 999);
        double y = Math.Clamp(current.MinY + dy, 0, 999);
        return new Rect(x, y, x + 1, y + 1);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("BulkInsert")]
    public RStarTree<int> RStarTree_BulkInsert()
    {
        RStarTree<int> tree = new();
        for (int i = 0; i < N; i++)
        {
            tree.Insert(i, insertRects[i]);
        }

        return tree;
    }

    [Benchmark]
    [BenchmarkCategory("BulkInsert")]
    public RumTree<int> RumTree_BulkInsert()
    {
        RumTree<int> tree = new(new RumTreeOptions { CleaningInterval = 5 });
        for (int i = 0; i < N; i++)
        {
            tree.Insert(i, insertRects[i]);
        }

        return tree;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Update")]
    public void RStarTree_Update()
    {
        rstar.Update(cursor % N, updatePool[cursor & 1023]);
        cursor++;
    }

    [Benchmark]
    [BenchmarkCategory("Update")]
    public void RumTree_Update()
    {
        rum.Update(cursor % N, updatePool[cursor & 1023]);
        cursor++;
    }

    /// <summary>
    /// Small-delta moves (a fraction of the leaf cover) modeling object tracking, the workload
    /// both trees exist for — unlike <c>Update</c>, which teleports items across the whole space.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("UpdateLocal")]
    public void RStarTree_UpdateLocal()
    {
        int id = cursor % N;
        rstarPositions[id] = Nudge(rstarPositions[id], cursor);
        rstar.Update(id, rstarPositions[id]);
        cursor++;
    }

    [Benchmark]
    [BenchmarkCategory("UpdateLocal")]
    public void RumTree_UpdateLocal()
    {
        int id = cursor % N;
        rumPositions[id] = Nudge(rumPositions[id], cursor);
        rum.Update(id, rumPositions[id]);
        cursor++;
    }

    /// <summary>Full delete + reinsert cycles: the only category exercising the remove/condense path.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Churn")]
    public void RStarTree_Churn()
    {
        int id = cursor % N;
        rstar.Remove(id);
        rstar.Insert(id, updatePool[cursor & 1023]);
        cursor++;
    }

    [Benchmark]
    [BenchmarkCategory("Churn")]
    public void RumTree_Churn()
    {
        int id = cursor % N;
        rum.Remove(id);
        rum.Insert(id, updatePool[cursor & 1023]);
        cursor++;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Query")]
    public List<int> RStarTree_Query()
    {
        return rstar.Search(queryPool[cursor++ & 63]);
    }

    [Benchmark]
    [BenchmarkCategory("Query")]
    public List<int> RumTree_Query()
    {
        return rum.Search(queryPool[cursor++ & 63]);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("QueryBuffered")]
    public int RStarTree_QueryBuffered()
    {
        searchBuffer.Clear();
        rstar.Search(queryPool[cursor++ & 63], searchBuffer);
        return searchBuffer.Count;
    }

    [Benchmark]
    [BenchmarkCategory("QueryBuffered")]
    public int RumTree_QueryBuffered()
    {
        searchBuffer.Clear();
        rum.Search(queryPool[cursor++ & 63], searchBuffer);
        return searchBuffer.Count;
    }

    private static Rect RandomRect(Random random, double size)
    {
        double x = random.NextDouble() * 1000;
        double y = random.NextDouble() * 1000;
        return new Rect(x, y, x + size, y + size);
    }
}
