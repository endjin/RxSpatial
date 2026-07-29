using Microsoft.SqlServer.Types;

namespace RxSpatial.Backends;

/// <summary>A spatial index of observers, queryable by zone.</summary>
public interface ISpatialBackend<T>
    where T : notnull
{
    bool Remove(T observer);

    bool Contains(T observer);

    /// <summary>Observers whose zone intersects <paramref name="location"/>, or null when none.</summary>
    List<T>? GetObserversInZone(SqlGeography location);

    /// <summary>Registers an observer that follows the moving object <paramref name="source"/>.</summary>
    void Subscribe(T observer, RxGeography source);

    /// <summary>Registers an observer anchored at the stationary <paramref name="location"/>.</summary>
    void Subscribe(T observer, SqlGeography location);
}
