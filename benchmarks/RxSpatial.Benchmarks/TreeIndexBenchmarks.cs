using System;
using System.Collections.Generic;
using System.Configuration;
using BenchmarkDotNet.Attributes;

namespace RxSpatial.Benchmarks
{
    /// <summary>
    /// Successor to the original memoryTestApplication: compares the classic R-tree against the
    /// RUM-tree (R-tree with Update Memo) for bulk insertion, the frequent-update workload the
    /// RUM-tree was designed for (delete old entry + insert new position), and intersection
    /// queries. MemoryDiagnoser reports the allocation cost that the old harness sampled with
    /// GC.GetTotalMemory.
    /// </summary>
    [MemoryDiagnoser]
    public class TreeIndexBenchmarks
    {
        [Params(1000)]
        public int N;

        private RTree.Rectangle[] insertRectsRTree = null!;
        private RUMTree.Rectangle[] insertRectsRUMTree = null!;
        private RTree.Rectangle[] updatePoolRTree = null!;
        private RUMTree.Rectangle[] updatePoolRUMTree = null!;
        private RTree.Rectangle[] queryPoolRTree = null!;
        private RUMTree.Rectangle[] queryPoolRUMTree = null!;

        private RTree.RTree<int> rtree = null!;
        private RUMTree.RUMTree<int> rumTree = null!;
        private RTree.Rectangle[] currentRTree = null!;
        private RUMTree.Rectangle[] currentRUMTree = null!;
        private int cursor;

        [GlobalSetup]
        public void Setup()
        {
            // The RUM-tree's lazy garbage cleaner reads its inspection interval from AppSettings.
            ConfigurationManager.AppSettings["RUMTreeGCInspectionInterval"] = "5";

            var random = new Random(42);
            insertRectsRTree = new RTree.Rectangle[N];
            insertRectsRUMTree = new RUMTree.Rectangle[N];
            for (int i = 0; i < N; i++)
            {
                (double x, double y) = RandomOrigin(random);
                insertRectsRTree[i] = new RTree.Rectangle(x, y, x + 1, y + 1);
                insertRectsRUMTree[i] = new RUMTree.Rectangle(x, y, x + 1, y + 1);
            }

            updatePoolRTree = new RTree.Rectangle[1024];
            updatePoolRUMTree = new RUMTree.Rectangle[1024];
            for (int i = 0; i < updatePoolRTree.Length; i++)
            {
                (double x, double y) = RandomOrigin(random);
                updatePoolRTree[i] = new RTree.Rectangle(x, y, x + 1, y + 1);
                updatePoolRUMTree[i] = new RUMTree.Rectangle(x, y, x + 1, y + 1);
            }

            queryPoolRTree = new RTree.Rectangle[64];
            queryPoolRUMTree = new RUMTree.Rectangle[64];
            for (int i = 0; i < queryPoolRTree.Length; i++)
            {
                (double x, double y) = RandomOrigin(random);
                queryPoolRTree[i] = new RTree.Rectangle(x, y, x + 20, y + 20);
                queryPoolRUMTree[i] = new RUMTree.Rectangle(x, y, x + 20, y + 20);
            }

            // Pre-built trees for the update and query benchmarks.
            rtree = new RTree.RTree<int>();
            rumTree = new RUMTree.RUMTree<int>();
            currentRTree = new RTree.Rectangle[N];
            currentRUMTree = new RUMTree.Rectangle[N];
            for (int i = 0; i < N; i++)
            {
                rtree.Add(insertRectsRTree[i], i);
                currentRTree[i] = insertRectsRTree[i];
                rumTree.Add(insertRectsRUMTree[i], i);
                currentRUMTree[i] = insertRectsRUMTree[i];
            }
        }

        [Benchmark]
        public RTree.RTree<int> RTree_BulkInsert()
        {
            var tree = new RTree.RTree<int>();
            for (int i = 0; i < N; i++)
            {
                tree.Add(insertRectsRTree[i], i);
            }

            return tree;
        }

        [Benchmark]
        public RUMTree.RUMTree<int> RUMTree_BulkInsert()
        {
            var tree = new RUMTree.RUMTree<int>();
            for (int i = 0; i < N; i++)
            {
                tree.Add(insertRectsRUMTree[i], i);
            }

            return tree;
        }

        [Benchmark]
        public void RTree_Update()
        {
            int id = cursor % N;
            RTree.Rectangle next = updatePoolRTree[cursor & 1023];
            rtree.Delete(currentRTree[id], id);
            rtree.Add(next, id);
            currentRTree[id] = next;
            cursor++;
        }

        [Benchmark]
        public void RUMTree_Update()
        {
            int id = cursor % N;
            RUMTree.Rectangle next = updatePoolRUMTree[cursor & 1023];
            rumTree.Delete(currentRUMTree[id], id);
            rumTree.Add(next, id);
            currentRUMTree[id] = next;
            cursor++;
        }

        [Benchmark]
        public List<int> RTree_IntersectsQuery()
        {
            return rtree.Intersects(queryPoolRTree[cursor++ & 63]);
        }

        [Benchmark]
        public List<int> RUMTree_IntersectsQuery()
        {
            return rumTree.Intersects(queryPoolRUMTree[cursor++ & 63]);
        }

        private static (double X, double Y) RandomOrigin(Random random)
            => (random.NextDouble() * 1000, random.NextDouble() * 1000);
    }
}
