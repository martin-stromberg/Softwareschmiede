using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Service für 2-Tier Caching von Diff-Ergebnissen (Memory + Persistent).
/// </summary>
public sealed class DiffCachingService
{
    private readonly SoftwareschmiededDbContext _db;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<DiffCachingService> _logger;

    // Cache-Konfiguration
    private const string CacheKeyPrefix = "diff_";
    private const int MemoryCacheTtlMinutes = 60; // 1 Stunde
    private const int PersistentCacheTtlHours = 24; // 24 Stunden

    /// <inheritdoc cref="DiffCachingService"/>
    public DiffCachingService(
        SoftwareschmiededDbContext db,
        IMemoryCache memoryCache,
        ILogger<DiffCachingService> logger)
    {
        _db = db;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// Versucht, einen Diff aus dem Cache (Memory oder Persistent) abzurufen.
    /// </summary>
    public async Task<DiffResult?> GetFromCacheAsync(
        Guid diffResultId,
        Guid aufgabeId,
        string filePath,
        string sourceVersion,
        string targetVersion,
        CancellationToken ct = default)
    {
        var cacheKey = GenerateCacheKey(aufgabeId, filePath, sourceVersion, targetVersion);

        // Versuche zuerst aus Memory Cache zu laden
        if (_memoryCache.TryGetValue($"{CacheKeyPrefix}mem_{cacheKey}", out DiffResult? cachedResult))
        {
            _logger.LogInformation("Diff aus Memory Cache geladen. CacheKey: {CacheKey}", cacheKey);
            return cachedResult;
        }

        // Versuche aus persistentem Cache zu laden
        try
        {
            var persistedCache = await _db.DiffCaches
                .AsNoTracking()
                .FirstOrDefaultAsync(dc => dc.DiffResultId == diffResultId && dc.IsValid, ct);

            if (persistedCache != null && persistedCache.ExpiresAt > DateTimeOffset.UtcNow)
            {
                try
                {
                    var deserializedData = JsonSerializer.Deserialize<DiffResult>(persistedCache.CachedData);
                    if (deserializedData != null)
                    {
                        _logger.LogInformation("Diff aus persistentem Cache geladen. CacheKey: {CacheKey}", cacheKey);

                        // Setze in Memory Cache für schnellere Zugriffe
                        _memoryCache.Set($"{CacheKeyPrefix}mem_{cacheKey}", deserializedData,
                            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(MemoryCacheTtlMinutes) });

                        return deserializedData;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Fehler beim Deserialisieren des Caches. Markiere als ungültig.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden des persistenten Caches.");
        }

        return null;
    }

    /// <summary>
    /// Speichert einen Diff im Cache (Memory + Persistent).
    /// </summary>
    public async Task SetInCacheAsync(
        DiffResult diffResult,
        Guid aufgabeId,
        string filePath,
        string sourceVersion,
        string targetVersion,
        CancellationToken ct = default)
    {
        var cacheKey = GenerateCacheKey(aufgabeId, filePath, sourceVersion, targetVersion);

        try
        {
            // Speichere in Memory Cache
            _memoryCache.Set($"{CacheKeyPrefix}mem_{cacheKey}", diffResult,
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(MemoryCacheTtlMinutes) });

            // Speichere in persistentem Cache
            var serializedData = JsonSerializer.Serialize(diffResult);
            var now = DateTimeOffset.UtcNow;
            var expiresAt = now.AddHours(PersistentCacheTtlHours);

            var diffCache = new DiffCache
            {
                Id = Guid.NewGuid(),
                DiffResultId = diffResult.Id,
                CacheKey = cacheKey,
                CachedData = serializedData,
                CachedAt = now,
                ExpiresAt = expiresAt,
                CachingStrategy = DiffCachingStrategy.TTL,
                IsValid = true
            };

            _db.DiffCaches.Add(diffCache);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Diff im Cache gespeichert. CacheKey: {CacheKey}, Expires: {ExpiresAt}",
                cacheKey, expiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern im Cache für CacheKey: {CacheKey}", cacheKey);
            // Cache-Fehler sollten nicht den Hauptprozess unterbrechen
        }
    }

    /// <summary>
    /// Invalidiert einen Cache-Eintrag (z.B. wenn der Quellcode geändert wurde).
    /// </summary>
    public async Task InvalidateCacheAsync(
        Guid diffResultId,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Invalidiere Cache für DiffResult {DiffResultId}", diffResultId);

        var cacheEntry = await _db.DiffCaches
            .FirstOrDefaultAsync(dc => dc.DiffResultId == diffResultId, ct);

        if (cacheEntry != null)
        {
            cacheEntry.IsValid = false;
            _db.DiffCaches.Update(cacheEntry);
            await _db.SaveChangesAsync(ct);

            // Entferne auch aus Memory Cache (best effort)
            _memoryCache.Remove($"{CacheKeyPrefix}mem_{cacheEntry.CacheKey}");
        }
    }

    /// <summary>
    /// Löscht abgelaufene Cache-Einträge (sollte regelmäßig aufgerufen werden).
    /// </summary>
    public async Task CleanupExpiredCachesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starte Cleanup von abgelaufenen Caches.");

        var expiredCaches = await _db.DiffCaches
            .Where(dc => dc.ExpiresAt <= DateTimeOffset.UtcNow)
            .ToListAsync(ct);

        if (expiredCaches.Count > 0)
        {
            foreach (var cache in expiredCaches)
            {
                _db.DiffCaches.Remove(cache);
            }
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Cleanup abgeschlossen. {Count} abgelaufene Caches gelöscht.", expiredCaches.Count);
        }
    }

    /// <summary>
    /// Generiert einen eindeutigen Cache-Schlüssel auf Basis der Eingabeparameter.
    /// </summary>
    private static string GenerateCacheKey(
        Guid aufgabeId,
        string filePath,
        string sourceVersion,
        string targetVersion)
    {
        var input = $"{aufgabeId}_{filePath}_{sourceVersion}_{targetVersion}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes);
    }
}
