using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

public sealed class DiffServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly IMemoryCache _memoryCache;
    private readonly DiffService _sut;

    public DiffServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        var algorithm = new DiffAlgorithmService(NullLogger<DiffAlgorithmService>.Instance);
        var caching = new DiffCachingService(_db, _memoryCache, NullLogger<DiffCachingService>.Instance);
        _sut = new DiffService(_db, algorithm, caching, NullLogger<DiffService>.Instance);
    }

    [Fact]
    public async Task GenerateDiffAsync_ShouldThrowInvalidOperationException_WhenAufgabeDoesNotExist()
    {
        // Act
        var act = () => _sut.GenerateDiffAsync(Guid.NewGuid(), "src/file.cs", "a", "b");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateDiffAsync_ShouldPersistGeneratedDiff_WhenAufgabeExists()
    {
        // Arrange
        var aufgabeId = await SeedAufgabeAsync();

        // Act
        var result = await _sut.GenerateDiffAsync(aufgabeId, "src/file.cs", "a\nb\nc", "a\nx\nc");

        // Assert
        result.Status.Should().Be(DiffResultStatus.Generated);
        result.AddedLines.Should().Be(1);
        result.RemovedLines.Should().Be(1);
        result.SourceContent.Should().Be("a\nb\nc");
        result.TargetContent.Should().Be("a\nx\nc");
        _db.DiffResults.Should().ContainSingle(dr => dr.Id == result.Id && dr.Status == DiffResultStatus.Generated);
    }

    [Fact]
    public async Task GenerateDiffAsync_ShouldNotStoreInlineContents_WhenPayloadExceedsLimit()
    {
        // Arrange
        var aufgabeId = await SeedAufgabeAsync();
        var largeSource = new string('a', 102401);
        var largeTarget = new string('b', 102401);

        // Act
        var result = await _sut.GenerateDiffAsync(aufgabeId, "src/huge.txt", largeSource, largeTarget);

        // Assert
        result.SourceContent.Should().BeNull();
        result.TargetContent.Should().BeNull();
    }

    [Fact]
    public async Task GenerateDiffAsync_ShouldPersistErrorStatus_WhenAlgorithmThrows()
    {
        // Arrange
        var aufgabeId = await SeedAufgabeAsync();

        // Act
        var act = () => _sut.GenerateDiffAsync(aufgabeId, "src/file.cs", " ", "target");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
        _db.DiffResults.Should().ContainSingle(dr => dr.AufgabeId == aufgabeId && dr.Status == DiffResultStatus.Error);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnAggregatesPerAufgabe()
    {
        // Arrange
        var aufgabeId = await SeedAufgabeAsync();
        _db.DiffResults.AddRange(
            new DiffResult
            {
                Id = Guid.NewGuid(),
                AufgabeId = aufgabeId,
                FilePath = "a.cs",
                SourceVersion = "v1",
                TargetVersion = "v2",
                AddedLines = 3,
                RemovedLines = 1,
                ModifiedLines = 0,
                Status = DiffResultStatus.Generated,
                GeneratedBy = "test",
                GeneratedAt = DateTimeOffset.UtcNow
            },
            new DiffResult
            {
                Id = Guid.NewGuid(),
                AufgabeId = aufgabeId,
                FilePath = "b.cs",
                SourceVersion = "v2",
                TargetVersion = "v3",
                AddedLines = 1,
                RemovedLines = 2,
                ModifiedLines = 1,
                Status = DiffResultStatus.Error,
                GeneratedBy = "test",
                GeneratedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            });
        await _db.SaveChangesAsync();

        // Act
        var stats = await _sut.GetStatisticsAsync(aufgabeId);

        // Assert
        stats.TotalDiffCount.Should().Be(2);
        stats.TotalAddedLines.Should().Be(4);
        stats.TotalRemovedLines.Should().Be(3);
        stats.TotalModifiedLines.Should().Be(1);
        stats.StatusBreakdown[DiffResultStatus.Generated].Should().Be(1);
        stats.StatusBreakdown[DiffResultStatus.Error].Should().Be(1);
    }

    [Fact]
    public async Task DeleteDiffAsync_ShouldDeleteDiff_WhenExisting()
    {
        // Arrange
        var aufgabeId = await SeedAufgabeAsync();
        var diff = await _sut.GenerateDiffAsync(aufgabeId, "src/file.cs", "a", "b");

        // Act
        var deleted = await _sut.DeleteDiffAsync(diff.Id);

        // Assert
        deleted.Should().BeTrue();
        _db.DiffResults.Should().NotContain(dr => dr.Id == diff.Id);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
        _db.Dispose();
    }

    private async Task<Guid> SeedAufgabeAsync()
    {
        var projektId = Guid.NewGuid();
        var aufgabeId = Guid.NewGuid();
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
            Titel = "Diff-Aufgabe",
            Status = AufgabeStatus.Offen,
            ErstellungsDatum = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();
        return aufgabeId;
    }
}
