using Microsoft.SqlServer.Types;

namespace RxSpatial.Backends;

/// <summary>A spatial index of observers, queryable by zone.</summary>
public interface ISpatialBackend<T>
    where T : notnull
{
    /// <summary>
    /// Unregisters an observer and ends any location tracking started for it. Returns false
    /// (without throwing) when the observer was not registered.
    /// </summary>
    bool Remove(T observer);

    bool Contains(T observer);

    /// <summary>
    /// Observers whose zone intersects <paramref name="location"/>; empty when none. The list
    /// is for immediate enumeration only — a backend may hand back its own storage.
    /// </summary>
    List<T> GetObserversInZone(SqlGeography location);

    /// <summary>
    /// Registers an observer that follows the moving object <paramref name="source"/>. The
    /// tracking subscription is owned by the backend and ends with <see cref="Remove"/>.
    /// </summary>
    void Subscribe(T observer, RxGeography source);

    /// <summary>Registers an observer anchored at the stationary <paramref name="location"/>.</summary>
    void Subscribe(T observer, SqlGeography location);
}
