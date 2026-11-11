# RxSpatial.Index

The spatial indexes that [RxSpatial](https://github.com/endjin/RxSpatial) uses: an R*-tree and a RUM-tree. You can also use them without RxSpatial. Each maps items to bounding rectangles and finds the items whose rectangle intersects a search rectangle. The package has no dependencies.

```csharp
using RxSpatial.Index;

var index = new RStarTree<string>();
index.Insert("depot", new Rect(0, 0, 10, 10));
index.Insert("van-7", new Rect(4, 4, 4, 4));                  // a point is a zero-extent rectangle

List<string> hits = index.Search(new Rect(0, 0, 12, 12));     // depot, van-7

index.Update("van-7", new Rect(20, 20, 20, 20));              // the van moves away
hits = index.Search(new Rect(0, 0, 12, 12));                  // depot

index.Remove("depot");
```

## Two indexes, one interface

Both classes implement `ISpatialIndex<T>`: `Insert`, `Update`, `Remove`, `Search`, `Contains`, `Count` and `Clear`. An index holds one entry per item.

- `RStarTree<T>` is an R*-tree (Beckmann et al., 1990). Use it when searches outnumber updates.
- `RumTree<T>` is an R-tree with Update Memo (Xiong and Aref). An update only inserts the new entry. The old entry stays in the tree, is filtered out of searches, and is purged later by a cleaning pass, so an item that moves often does not pay for a deletion on every move.

```csharp
var index = new RumTree<string>(new RumTreeOptions { CleaningInterval = 5 });
```

`RTreeOptions` and `RumTreeOptions` set the node sizes and, for the RUM-tree, how often obsolete entries are purged.

## Things to know

- The indexes are not thread-safe. Synchronise access yourself.
- `Rect` accepts any two opposite corners, and rejects NaN coordinates.
- `Insert` throws `ArgumentException` if the item is already in the index.
- These are clean-room .NET implementations, not ports of other libraries.

## More

- [Benchmarks](https://github.com/endjin/RxSpatial/blob/main/docs/benchmarks.md): the R*-tree against the RUM-tree for bulk insertion, updates and queries.
- [The repository](https://github.com/endjin/RxSpatial).

Released under the MIT licence.
