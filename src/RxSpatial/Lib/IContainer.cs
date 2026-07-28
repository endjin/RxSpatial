using Microsoft.SqlServer.Types;

namespace RxSpatial.Lib;

/// <summary>A spatial container of observers, queryable by zone.</summary>
public interface IContainer<T>
    where T : notnull
{
    bool Remove(T item);

    bool Contains(T item);

    /// <summary>Observers whose zone intersects <paramref name="loc"/>, or null when none.</summary>
    List<T>? getObjsInSameZone(SqlGeography loc);

    /// <summary>Subscribes an observer that follows the moving object <paramref name="G"/>.</summary>
    void subscribeRxRx(T observer, RxGeography G);

    /// <summary>Subscribes an observer anchored at the stationary geography <paramref name="loc"/>.</summary>
    void subscribeRx(T observer, SqlGeography loc);
}
