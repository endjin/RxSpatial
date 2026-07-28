using System.Text;
using Microsoft.SqlServer.Types;

namespace RxSpatial.Lib;

/// <summary>
/// Spatial-hash backend: a fixed lat/long grid over the coverage region configured by
/// <c>HashMapGridNum</c> and <c>HashMapCoverageRegion</c>. Point observers occupy one cell;
/// polygon observers occupy every cell their envelope covers.
/// </summary>
internal class Grid<T> : IContainer<T>
    where T : notnull
{
    private readonly Dictionary<T, int> itemGridMap = new();
    private readonly GeoList<T>[,] grid;
    private readonly int latNum;
    private readonly int longNum;
    private readonly double lat1;  // coverage region lower-left latitude
    private readonly double long1; // coverage region lower-left longitude
    private readonly double lat2;  // coverage region upper-right latitude
    private readonly double long2; // coverage region upper-right longitude
    private readonly double latStep;
    private readonly double longStep;

    public Grid(GridOptions options)
    {
        latNum = options.LatitudeCells;
        longNum = options.LongitudeCells;
        lat1 = options.CoverageRegion.LowerLeftLatitude;
        long1 = options.CoverageRegion.LowerLeftLongitude;
        lat2 = options.CoverageRegion.UpperRightLatitude;
        long2 = options.CoverageRegion.UpperRightLongitude;

        grid = new GeoList<T>[latNum, longNum];
        latStep = (lat2 - lat1) / latNum;
        longStep = (long2 - long1) / longNum;
        for (int i = 0; i < latNum; i++)
        {
            for (int j = 0; j < longNum; j++)
                grid[i, j] = new GeoList<T>();
        }
    }

    public string dump()
    {
        StringBuilder sb = new();
        for (int i = 0; i < latNum; i++)
        {
            for (int j = 0; j < longNum; j++)
            {
                sb.Append(grid[i, j].Count);
                sb.Append(" | ");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    public void subscribeRxRx(T observer, RxGeography G)
    {
        // Preserved behaviour: G.loc is null until the moving object's first OnNext push;
        // subscribing before it has reported a location fails, as it always has.
        UpdateLocation(G.loc!, observer);
        LocationObsvr locationObsvr = new(this, observer);
        G.Subscribe(locationObsvr);
    }

    public void subscribeRx(T observer, SqlGeography loc) => UpdateLocation(loc, observer);

    public bool Remove(T o)
    {
        if (itemGridMap.Remove(o, out int cell))
        {
            int lat = cell / longNum;
            int lon = cell % longNum;
            return grid[lat, lon].Remove(o);
        }

        return true;
    }

    public bool Contains(T o) => itemGridMap.ContainsKey(o);

    public List<T> getObjsInSameZone(SqlGeography g)
    {
        if (g.STGeometryType().Value == "Point")
        {
            return grid[findLatSlot(g), findLongSlot(g)];
        }

        GeoList<T> result = new();
        (int latSlotMin, int latSlotMax, int longSlotMin, int longSlotMax) = FindEnvelopeSlots(g);
        for (int i = latSlotMin; i <= latSlotMax; i++)
        {
            for (int j = longSlotMin; j <= longSlotMax; j++)
                result.AddRange(grid[i, j]);
        }

        return result;
    }

    private void UpdateLocation(SqlGeography g, T o)
    {
        Remove(o);
        add(g, o);
    }

    private void add(SqlGeography g, T o)
    {
        if (g.STGeometryType().Value == "Point")
        {
            int latSlot = findLatSlot(g);
            int longSlot = findLongSlot(g);
            grid[latSlot, longSlot].Add(o);
            itemGridMap[o] = (latSlot * longNum) + longSlot;
        }
        else
        {
            // Region observers (e.g. geofences) are registered in every cell their
            // envelope covers; they are not tracked in itemGridMap (original behaviour).
            (int latSlotMin, int latSlotMax, int longSlotMin, int longSlotMax) = FindEnvelopeSlots(g);
            for (int i = latSlotMin; i <= latSlotMax; i++)
            {
                for (int j = longSlotMin; j <= longSlotMax; j++)
                    grid[i, j].Add(o);
            }
        }
    }

    private (int LatMin, int LatMax, int LongMin, int LongMax) FindEnvelopeSlots(SqlGeography g)
    {
        SqlGeometry box = SqlGeometry.STGeomFromWKB(g.STAsBinary(), g.STSrid.Value).STEnvelope();
        SqlGeometry lowerLeft = box.STPointN(1);
        SqlGeography pt1 = SqlGeography.STGeomFromText(lowerLeft.MakeValid().STAsText(), lowerLeft.STSrid.Value);
        SqlGeometry upperRight = box.STPointN(3);
        SqlGeography pt2 = SqlGeography.STGeomFromText(upperRight.MakeValid().STAsText(), lowerLeft.STSrid.Value);

        int latA = findLatSlot(pt1);
        int longA = findLongSlot(pt1);
        int latB = findLatSlot(pt2);
        int longB = findLongSlot(pt2);

        return (Math.Min(latA, latB), Math.Max(latA, latB), Math.Min(longA, longB), Math.Max(longA, longB));
    }

    private int findLatSlot(SqlGeography loc)
    {
        int latSlot = (int)((loc.Lat.Value - lat1) / latStep);
        return Math.Clamp(latSlot, 0, latNum - 1);
    }

    private int findLongSlot(SqlGeography loc)
    {
        int longSlot = (int)((loc.Long.Value - long1) / longStep);
        return Math.Clamp(longSlot, 0, longNum - 1);
    }

    // Tracks a moving observer and updates its grid cell on every location push.
    private class LocationObsvr(Grid<T> grid, T item) : IObserver<SqlGeography>
    {
        public void OnNext(SqlGeography g) => grid.UpdateLocation(g, item);

        public void OnCompleted()
        {
        }

        public void OnError(Exception e)
        {
        }
    }
}
