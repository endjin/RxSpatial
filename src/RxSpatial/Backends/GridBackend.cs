// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

using System.Text;
using RxSpatial.Index;

namespace RxSpatial.Backends;

/// <summary>
/// Spatial-hash backend: a fixed grid over the configured coverage region. Every observer
/// occupies the block of cells its bounding rectangle covers — one cell for a point, every
/// covered cell for a region.
/// </summary>
internal sealed class GridBackend<TShape, TOps, T> : ISpatialBackend<TShape, T>
    where TShape : class
    where TOps : struct, ISpatialOperations<TShape>
    where T : notnull
{
    // Every observer's occupied cell block is recorded, region observers included, so that
    // Remove can evict a region from all of its cells. (The original only tracked the single
    // cell of a point observer, which left regions permanently subscribed and let a moving
    // region accumulate in every cell it had ever visited.)
    private readonly Dictionary<T, CellBlock> placements = [];
    private readonly Dictionary<T, IDisposable> trackers = [];
    private readonly ListBackend<TShape, T>[,] cells;
    private readonly GridLayout layout;
    private readonly double xStep;
    private readonly double yStep;

    public GridBackend(GridLayout layout)
    {
        this.layout = layout;
        xStep = (layout.Extent.MaxX - layout.Extent.MinX) / layout.XCells;
        yStep = (layout.Extent.MaxY - layout.Extent.MinY) / layout.YCells;

        cells = new ListBackend<TShape, T>[layout.YCells, layout.XCells];
        for (int row = 0; row < layout.YCells; row++)
        {
            for (int column = 0; column < layout.XCells; column++)
                cells[row, column] = [];
        }
    }

    public void Subscribe(T observer, IMovingObject<TShape> source)
    {
        // Preserved behaviour: source.Location is null until the moving object's first OnNext
        // push; subscribing before it has reported a location fails, as it always has.
        UpdateLocation(source.Location!, observer);

        // The tracker handle is kept so Remove can end it: otherwise the tracked object's
        // next push would re-register the observer that has just been unsubscribed.
        trackers[observer] = source.Subscribe(new LocationTracker(this, observer));
    }

    public void Subscribe(T observer, TShape location) => UpdateLocation(location, observer);

    public bool Remove(T observer)
    {
        if (trackers.Remove(observer, out IDisposable? tracker))
            tracker.Dispose();

        return Evict(observer);
    }

    public bool Contains(T observer) => placements.ContainsKey(observer);

    public List<T> GetObserversInZone(TShape location)
    {
        CellBlock block = CellBlockOf(TOps.BoundsOf(location));
        if (block.IsSingleCell)
            return cells[block.RowMin, block.ColumnMin];

        // A region observer sits in every cell of its own block, so a region push that shares
        // several of those cells would meet it once per shared cell. Report each observer only
        // from the first (lowest row, then column) cell the two blocks share.
        List<T> result = [];
        for (int row = block.RowMin; row <= block.RowMax; row++)
        {
            for (int column = block.ColumnMin; column <= block.ColumnMax; column++)
            {
                foreach (T observer in cells[row, column])
                {
                    CellBlock placed = placements[observer];
                    if (row == Math.Max(placed.RowMin, block.RowMin)
                        && column == Math.Max(placed.ColumnMin, block.ColumnMin))
                    {
                        result.Add(observer);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>Cell occupancy counts, one row per Y (latitude) slot — diagnostic snapshot.</summary>
    public override string ToString()
    {
        StringBuilder builder = new();
        for (int row = 0; row < layout.YCells; row++)
        {
            for (int column = 0; column < layout.XCells; column++)
            {
                builder.Append(cells[row, column].Count);
                builder.Append(" | ");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private void UpdateLocation(TShape location, T observer)
    {
        Evict(observer);
        Insert(location, observer);
    }

    private void Insert(TShape location, T observer)
    {
        // A point occupies one cell; a region (e.g. a geofence) every cell its rectangle covers.
        CellBlock block = CellBlockOf(TOps.BoundsOf(location));
        for (int row = block.RowMin; row <= block.RowMax; row++)
        {
            for (int column = block.ColumnMin; column <= block.ColumnMax; column++)
                cells[row, column].Add(observer);
        }

        placements[observer] = block;
    }

    /// <summary>Takes an observer out of every cell it occupies. False when it was not indexed.</summary>
    private bool Evict(T observer)
    {
        if (!placements.Remove(observer, out CellBlock block))
            return false;

        for (int row = block.RowMin; row <= block.RowMax; row++)
        {
            for (int column = block.ColumnMin; column <= block.ColumnMax; column++)
                cells[row, column].Remove(observer);
        }

        return true;
    }

    private CellBlock CellBlockOf(Rect bounds)
        => new(RowOf(bounds.MinY), RowOf(bounds.MaxY), ColumnOf(bounds.MinX), ColumnOf(bounds.MaxX));

    private int RowOf(double y)
        => Math.Clamp((int)((y - layout.Extent.MinY) / yStep), 0, layout.YCells - 1);

    private int ColumnOf(double x)
        => Math.Clamp((int)((x - layout.Extent.MinX) / xStep), 0, layout.XCells - 1);

    /// <summary>The inclusive rectangle of grid slots an observer occupies.</summary>
    private readonly record struct CellBlock(int RowMin, int RowMax, int ColumnMin, int ColumnMax)
    {
        public bool IsSingleCell => RowMin == RowMax && ColumnMin == ColumnMax;
    }

    // Tracks a moving observer and updates its grid cell on every location push.
    private sealed class LocationTracker(GridBackend<TShape, TOps, T> grid, T observer) : IObserver<TShape>
    {
        public void OnNext(TShape location) => grid.UpdateLocation(location, observer);

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
