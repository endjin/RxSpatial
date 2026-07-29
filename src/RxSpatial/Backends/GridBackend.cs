using System.Text;
using Microsoft.SqlServer.Types;

namespace RxSpatial.Backends;

/// <summary>
/// Spatial-hash backend: a fixed lat/long grid over the configured coverage region. Point
/// observers occupy one cell; region observers occupy every cell their envelope covers.
/// </summary>
internal sealed class GridBackend<T> : ISpatialBackend<T>
    where T : notnull
{
    // Every observer's occupied cell block is recorded, region observers included, so that
    // Remove can evict a region from all of its cells. (The original only tracked the single
    // cell of a point observer, which left regions permanently subscribed and let a moving
    // region accumulate in every cell it had ever visited.)
    private readonly Dictionary<T, CellBlock> placements = [];
    private readonly Dictionary<T, IDisposable> trackers = [];
    private readonly ListBackend<T>[,] cells;
    private readonly int latitudeCells;
    private readonly int longitudeCells;
    private readonly double originLatitude;  // coverage region lower-left corner
    private readonly double originLongitude;
    private readonly double latitudeStep;
    private readonly double longitudeStep;

    public GridBackend(GridOptions options)
    {
        latitudeCells = options.LatitudeCells;
        longitudeCells = options.LongitudeCells;
        originLatitude = options.CoverageRegion.LowerLeftLatitude;
        originLongitude = options.CoverageRegion.LowerLeftLongitude;
        latitudeStep = (options.CoverageRegion.UpperRightLatitude - originLatitude) / latitudeCells;
        longitudeStep = (options.CoverageRegion.UpperRightLongitude - originLongitude) / longitudeCells;

        cells = new ListBackend<T>[latitudeCells, longitudeCells];
        for (int i = 0; i < latitudeCells; i++)
        {
            for (int j = 0; j < longitudeCells; j++)
                cells[i, j] = [];
        }
    }

    public void Subscribe(T observer, RxGeography source)
    {
        // Preserved behaviour: source.Location is null until the moving object's first OnNext
        // push; subscribing before it has reported a location fails, as it always has.
        UpdateLocation(source.Location!, observer);

        // The tracker handle is kept so Remove can end it: otherwise the tracked object's
        // next push would re-register the observer that has just been unsubscribed.
        trackers[observer] = source.Subscribe(new LocationTracker(this, observer));
    }

    public void Subscribe(T observer, SqlGeography location) => UpdateLocation(location, observer);

    public bool Remove(T observer)
    {
        if (trackers.Remove(observer, out IDisposable? tracker))
            tracker.Dispose();

        return Evict(observer);
    }

    public bool Contains(T observer) => placements.ContainsKey(observer);

    public List<T> GetObserversInZone(SqlGeography location)
    {
        if (location.STGeometryType().Value == "Point")
        {
            return cells[LatitudeSlotOf(location), LongitudeSlotOf(location)];
        }

        List<T> result = [];
        CellBlock block = EnvelopeBlockOf(location);
        for (int i = block.LatitudeMin; i <= block.LatitudeMax; i++)
        {
            for (int j = block.LongitudeMin; j <= block.LongitudeMax; j++)
                result.AddRange(cells[i, j]);
        }

        return result;
    }

    /// <summary>Cell occupancy counts, one row per latitude slot — diagnostic snapshot.</summary>
    public override string ToString()
    {
        StringBuilder builder = new();
        for (int i = 0; i < latitudeCells; i++)
        {
            for (int j = 0; j < longitudeCells; j++)
            {
                builder.Append(cells[i, j].Count);
                builder.Append(" | ");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private void UpdateLocation(SqlGeography location, T observer)
    {
        Evict(observer);
        Insert(location, observer);
    }

    private void Insert(SqlGeography location, T observer)
    {
        CellBlock block;
        if (location.STGeometryType().Value == "Point")
        {
            int latitudeSlot = LatitudeSlotOf(location);
            int longitudeSlot = LongitudeSlotOf(location);
            block = new CellBlock(latitudeSlot, latitudeSlot, longitudeSlot, longitudeSlot);
        }
        else
        {
            // Region observers (e.g. geofences) are registered in every cell their envelope covers.
            block = EnvelopeBlockOf(location);
        }

        for (int i = block.LatitudeMin; i <= block.LatitudeMax; i++)
        {
            for (int j = block.LongitudeMin; j <= block.LongitudeMax; j++)
                cells[i, j].Add(observer);
        }

        placements[observer] = block;
    }

    /// <summary>Takes an observer out of every cell it occupies. False when it was not indexed.</summary>
    private bool Evict(T observer)
    {
        if (!placements.Remove(observer, out CellBlock block))
            return false;

        for (int i = block.LatitudeMin; i <= block.LatitudeMax; i++)
        {
            for (int j = block.LongitudeMin; j <= block.LongitudeMax; j++)
                cells[i, j].Remove(observer);
        }

        return true;
    }

    private CellBlock EnvelopeBlockOf(SqlGeography location)
    {
        SqlGeometry envelope = SqlGeometry.STGeomFromWKB(location.STAsBinary(), location.STSrid.Value).STEnvelope();
        SqlGeometry lowerLeft = envelope.STPointN(1);
        SqlGeography corner1 = SqlGeography.STGeomFromText(lowerLeft.MakeValid().STAsText(), lowerLeft.STSrid.Value);
        SqlGeometry upperRight = envelope.STPointN(3);
        SqlGeography corner2 = SqlGeography.STGeomFromText(upperRight.MakeValid().STAsText(), lowerLeft.STSrid.Value);

        int latitudeA = LatitudeSlotOf(corner1);
        int longitudeA = LongitudeSlotOf(corner1);
        int latitudeB = LatitudeSlotOf(corner2);
        int longitudeB = LongitudeSlotOf(corner2);

        return new CellBlock(
            Math.Min(latitudeA, latitudeB), Math.Max(latitudeA, latitudeB),
            Math.Min(longitudeA, longitudeB), Math.Max(longitudeA, longitudeB));
    }

    private int LatitudeSlotOf(SqlGeography location)
        => Math.Clamp((int)((location.Lat.Value - originLatitude) / latitudeStep), 0, latitudeCells - 1);

    private int LongitudeSlotOf(SqlGeography location)
        => Math.Clamp((int)((location.Long.Value - originLongitude) / longitudeStep), 0, longitudeCells - 1);

    /// <summary>The inclusive rectangle of grid slots an observer occupies.</summary>
    private readonly record struct CellBlock(int LatitudeMin, int LatitudeMax, int LongitudeMin, int LongitudeMax);

    // Tracks a moving observer and updates its grid cell on every location push.
    private sealed class LocationTracker(GridBackend<T> grid, T observer) : IObserver<SqlGeography>
    {
        public void OnNext(SqlGeography location) => grid.UpdateLocation(location, observer);

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
