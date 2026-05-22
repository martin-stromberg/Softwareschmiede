using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

public sealed class DiffCachingServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly IMemoryCache _memoryCache;
    private readonly DiffCachingService _sut;

    public DiffCachingServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _sut = new DiffCachingService(_db, _memoryCache, NullLogger<DiffCachingService>.Instance);
    }

    [Fact]
    public async Task SetInCacheAsync_AndGetFromCacheAsync_ShouldReturnResultFromCache()
    {
        // Arrange
        var seeded = await SeedDiffResultAsync();

        // Act
        await _sut.SetInCacheAsync(seeded.CachePayload, seeded.AufgabeId, seeded.FilePath, "v1", "v2");
        var loaded = await _sut.GetFromCacheAsync(seeded.CachePayload.Id, seeded.AufgabeId, seeded.FilePath, "v1", "v2");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(seeded.CachePayload.Id);
        loaded.FilePath.Should().Be(seeded.FilePath);
    }

    [Fact]
    public async Task GetFromCacheAsync_ShouldReturnNull_WhenCacheExpired()
    {
        // Arrange
        var seeded = await SeedDiffResultAsync();
        _db.DiffCaches.Add(new DiffCache
        {
            Id = Guid.NewGuid(),
            DiffResultId = seeded.CachePayload.Id,
            CacheKey = "expired",
            CachedData = "{}",
            CachedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            CachingStrategy = DiffCachingStrategy.TTL,
            IsValid = true
        });
        await _db.SaveChangesAsync();

        // Act
        var loaded = await _sut.GetFromCacheAsync(seeded.CachePayload.Id, seeded.AufgabeId, seeded.FilePath, "v1", "v2");

        // Assert
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task InvalidateCacheAsync_ShouldMarkPersistentEntryInvalid_AndRemoveMemoryCache()
    {
        // Arrange
        var seeded = await SeedDiffResultAsync();
        await _sut.SetInCacheAsync(seeded.CachePayload, seeded.AufgabeId, seeded.FilePath, "v1", "v2");

        // Act
        await _sut.InvalidateCacheAsync(seeded.CachePayload.Id);

        // Assert
        var cacheEntry = _db.DiffCaches.Single(dc => dc.DiffResultId == seeded.CachePayload.Id);
        cacheEntry.IsValid.Should().BeFalse();
        var loaded = await _sut.GetFromCacheAsync(seeded.CachePayload.Id, seeded.AufgabeId, seeded.FilePath, "v1", "v2");
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task CleanupExpiredCachesAsync_ShouldDeleteOnlyExpiredEntries()
    {
        // Arrange
        var seeded = await SeedDiffResultAsync();
        var anotherDiff = await SeedDiffResultAsync();
        _db.DiffCaches.AddRange(
            new DiffCache
            {
                Id = Guid.NewGuid(),
                DiffResultId = seeded.CachePayload.Id,
                CacheKey = "expired-cleanup",
                CachedData = "{}",
                CachedAt = DateTimeOffset.UtcNow.AddHours(-2),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CachingStrategy = DiffCachingStrategy.TTL,
                IsValid = true
            },
            new DiffCache
            {
                Id = Guid.NewGuid(),
                DiffResultId = anotherDiff.CachePayload.Id,
                CacheKey = "active-cleanup",
                CachedData = "{}",
                CachedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                CachingStrategy = DiffCachingStrategy.TTL,
                IsValid = true
            });
        await _db.SaveChangesAsync();

        // Act
        await _sut.CleanupExpiredCachesAsync();

        // Assert
        _db.DiffCaches.Should().ContainSingle(dc => dc.CacheKey == "active-cleanup");
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
        _db.Dispose();
    }

    private async Task<(Guid AufgabeId, string FilePath, DiffResult CachePayload)> SeedDiffResultAsync()
    {
        var projektId = Guid.NewGuid();
        var aufgabeId = Guid.NewGuid();
        var diffId = Guid.NewGuid();
        _db.Projekte.Add(new Projekt
        {
            Id = projektId,
            Name = $"Projekt-{projektId:N}",
            Status = ProjektStatus.Aktiv,
            ErstellungsDatum = DateTimeOffset.UtcNow
        });
        _db.Aufgaben.Add(new Aufgabe
        {
            Id = aufgabeId,
            ProjektId = projektId,
            Titel = "Aufgabe",
            Status = AufgabeStatus.Offen,
            ErstellungsDatum = DateTimeOffset.UtcNow
        });
        var persisted = new DiffResult
        {
            Id = diffId,
            AufgabeId = aufgabeId,
            FilePath = "src/file.cs",
            SourceVersion = "v1",
            TargetVersion = "v2",
            DiffType = DiffType.Full,
            Status = DiffResultStatus.Generated,
            GeneratedAt = DateTimeOffset.UtcNow,
            GeneratedBy = "test"
        };
        _db.DiffResults.Add(persisted);
        await _db.SaveChangesAsync();

        var cachePayload = new DiffResult
        {
            Id = diffId,
            AufgabeId = aufgabeId,
            FilePath = "src/file.cs",
            SourceVersion = "v1",
            TargetVersion = "v2",
            DiffType = DiffType.Full,
            Status = DiffResultStatus.Generated,
            GeneratedAt = DateTimeOffset.UtcNow,
            GeneratedBy = "test"
        };

        return (aufgabeId, "src/file.cs", cachePayload);
    }
}
