using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>
/// Caching-Management-Entity für TTL-basierte Invalidierung von Diff-Ergebnissen.
/// </summary>
public sealed class DiffCache
{
    /// <summary>Eindeutige ID des Cache-Eintrags.</summary>
    public Guid Id { get; set; }

    /// <summary>ID des zugehörigen Diff-Ergebnisses (1:1-Beziehung).</summary>
    public Guid DiffResultId { get; set; }

    /// <summary>Eindeutiger Cache-Schlüssel (z.B. SHA256-Hash).</summary>
    public string CacheKey { get; set; } = string.Empty;

    /// <summary>Gecachte Diff-Daten (JSON-serialisiert).</summary>
    public string CachedData { get; set; } = string.Empty;

    /// <summary>Zeitstempel der Cache-Erstellung.</summary>
    public DateTimeOffset CachedAt { get; set; }

    /// <summary>Ablaufzeitpunkt des Cache (z.B. CachedAt + 24h).</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Caching-Strategie (TTL, LRU, Manual).</summary>
    public DiffCachingStrategy CachingStrategy { get; set; } = DiffCachingStrategy.TTL;

    /// <summary>Gibt an, ob der Cache noch gültig ist.</summary>
    public bool IsValid { get; set; } = true;

    /// <summary>Navigationseigenschaft zum zugehörigen Diff-Ergebnis.</summary>
    public DiffResult DiffResult { get; set; } = null!;
}
