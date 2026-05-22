namespace Softwareschmiede.Domain.Enums;

/// <summary>Caching-Strategie für Diff-Ergebnisse.</summary>
public enum DiffCachingStrategy
{
    /// <summary>TTL-basiertes Caching (Time-To-Live).</summary>
    TTL,

    /// <summary>LRU-Caching (Least Recently Used).</summary>
    LRU,

    /// <summary>Manuelles Invalidieren des Cache.</summary>
    Manual
}
