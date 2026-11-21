using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>DiffCachingServiceTests.</summary>
public sealed class DiffCachingServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly IMemoryCache _memoryCache;
    private readonly DiffCachingService _sut;

    /// <summary>DiffCachingServiceTests.</summary>
    public DiffCachingServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _sut = new DiffCachingService(_db, _memoryCache, NullLogger<DiffCachingService>.Instance);
    }

    /// <summary>SetInCacheAsync_AndGetFromCacheAsync_ShouldReturnResultFromCache.</summary>
    [Fact]
    public async Task SetInCacheAsync_AndGetFromCacheAsync_ShouldReturnResultFromCache()
    {
        // Arrange
        var seeded = await SeedDiffResultAsync();

        // Act
        await _sut.SetInCacheAsync(seeded.CachePayload, seeded.WorkItemId, seeded.FilePath, "v1", "v2", TestContext.Current.CancellationToken);
        var loaded = await _sut.GetFromCacheAsync(seeded.CachePayload.Id, seeded.WorkItemId, seeded.FilePath, "v1", "v2", TestContext.Current.CancellationToken);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(seeded.CachePayload.Id);
        loaded.FilePath.Should().Be(seeded.FilePath);
    }

    /// <summary>GetFromCacheAsync_ShouldReturnNull_WhenCacheExpired.</summary>
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
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var loaded = await _sut.GetFromCacheAsync(seeded.CachePayload.Id, seeded.WorkItemId, seeded.FilePath, "v1", "v2", TestContext.Current.CancellationToken);

        // Assert
        loaded.Should().BeNull();
    }

    /// <summary>InvalidateCacheAsync_ShouldMarkPersistentEntryInvalid_AndRemoveMemoryCache.</summary>
    [Fact]
    public async Task InvalidateCacheAsync_ShouldMarkPersistentEntryInvalid_AndRemoveMemoryCache()
    {
        // Arrange
        var seeded = await SeedDiffResultAsync();
        await _sut.SetInCacheAsync(seeded.CachePayload, seeded.WorkItemId, seeded.FilePath, "v1", "v2", TestContext.Current.CancellationToken);

        // Act
        await _sut.InvalidateCacheAsync(seeded.CachePayload.Id, TestContext.Current.CancellationToken);

        // Assert
        var cacheEntry = _db.DiffCaches.Single(dc => dc.DiffResultId == seeded.CachePayload.Id);
        cacheEntry.IsValid.Should().BeFalse();
        var loaded = await _sut.GetFromCacheAsync(seeded.CachePayload.Id, seeded.WorkItemId, seeded.FilePath, "v1", "v2", TestContext.Current.CancellationToken);
        loaded.Should().BeNull();
    }

    /// <summary>CleanupExpiredCachesAsync_ShouldDeleteOnlyExpiredEntries.</summary>
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
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _sut.CleanupExpiredCachesAsync(TestContext.Current.CancellationToken);

        // Assert
        _db.DiffCaches.Should().ContainSingle(dc => dc.CacheKey == "active-cleanup");
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _memoryCache.Dispose();
        _db.Dispose();
    }

    private async Task<(Guid WorkItemId, string FilePath, DiffResult CachePayload)> SeedDiffResultAsync()
    {
        var projectId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var diffId = Guid.NewGuid();
        _db.Projects.Add(new Project
        {
            Id = projectId,
            Name = $"Projekt-{projectId:N}",
            Status = ProjectStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        });
        _db.WorkItems.Add(new WorkItem
        {
            Id = workItemId,
            ProjectId = projectId,
            Title = "Aufgabe",
            Status = WorkItemStatus.New,
            CreatedAt = DateTimeOffset.UtcNow
        });
        var persisted = new DiffResult
        {
            Id = diffId,
            WorkItemId = workItemId,
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
            WorkItemId = workItemId,
            FilePath = "src/file.cs",
            SourceVersion = "v1",
            TargetVersion = "v2",
            DiffType = DiffType.Full,
            Status = DiffResultStatus.Generated,
            GeneratedAt = DateTimeOffset.UtcNow,
            GeneratedBy = "test"
        };

        return (workItemId, "src/file.cs", cachePayload);
    }
}
