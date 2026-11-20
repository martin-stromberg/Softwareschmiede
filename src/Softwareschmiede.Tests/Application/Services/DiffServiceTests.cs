using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>DiffServiceTests.</summary>
public sealed class DiffServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly IMemoryCache _memoryCache;
    private readonly DiffService _sut;

    /// <summary>DiffServiceTests.</summary>
    public DiffServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        var algorithm = new DiffAlgorithmService(NullLogger<DiffAlgorithmService>.Instance);
        var caching = new DiffCachingService(_db, _memoryCache, NullLogger<DiffCachingService>.Instance);
        _sut = new DiffService(_db, algorithm, caching, NullLogger<DiffService>.Instance);
    }

    /// <summary><summary>GenerateDiffAsync_ShouldThrowInvalidOperationException_WhenAufgabeDoesNotExist.</summary>.</summary>
    [Fact]
    public async Task GenerateDiffAsync_ShouldThrowInvalidOperationException_WhenAufgabeDoesNotExist()
    {
        // Act
        var act = () => _sut.GenerateDiffAsync(Guid.NewGuid(), "src/file.cs", "a", "b");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary><summary>GenerateDiffAsync_ShouldPersistGeneratedDiff_WhenAufgabeExists.</summary>.</summary>
    [Fact]
    public async Task GenerateDiffAsync_ShouldPersistGeneratedDiff_WhenAufgabeExists()
    {
        // Arrange
        var workItemId = await SeedAufgabeAsync();

        // Act
        var result = await _sut.GenerateDiffAsync(workItemId, "src/file.cs", "a\nb\nc", "a\nx\nc", ct: TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(DiffResultStatus.Generated);
        result.AddedLines.Should().Be(1);
        result.RemovedLines.Should().Be(1);
        result.SourceContent.Should().Be("a\nb\nc");
        result.TargetContent.Should().Be("a\nx\nc");
        _db.DiffResults.Should().ContainSingle(dr => dr.Id == result.Id && dr.Status == DiffResultStatus.Generated);
    }

    /// <summary><summary>GenerateDiffAsync_ShouldNotStoreInlineContents_WhenPayloadExceedsLimit.</summary>.</summary>
    [Fact]
    public async Task GenerateDiffAsync_ShouldNotStoreInlineContents_WhenPayloadExceedsLimit()
    {
        // Arrange
        var workItemId = await SeedAufgabeAsync();
        var largeSource = new string('a', 102401);
        var largeTarget = new string('b', 102401);

        // Act
        var result = await _sut.GenerateDiffAsync(workItemId, "src/huge.txt", largeSource, largeTarget, ct: TestContext.Current.CancellationToken);

        // Assert
        result.SourceContent.Should().BeNull();
        result.TargetContent.Should().BeNull();
    }

    /// <summary><summary>GenerateDiffAsync_ShouldPersistErrorStatus_WhenAlgorithmThrows.</summary>.</summary>
    [Fact]
    public async Task GenerateDiffAsync_ShouldPersistErrorStatus_WhenAlgorithmThrows()
    {
        // Arrange
        var workItemId = await SeedAufgabeAsync();

        // Act
        var act = () => _sut.GenerateDiffAsync(workItemId, "src/file.cs", " ", "target");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
        _db.DiffResults.Should().ContainSingle(dr => dr.WorkItemId == workItemId && dr.Status == DiffResultStatus.Error);
    }

    /// <summary><summary>GetStatisticsAsync_ShouldReturnAggregatesPerAufgabe.</summary>.</summary>
    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnAggregatesPerAufgabe()
    {
        // Arrange
        var workItemId = await SeedAufgabeAsync();
        _db.DiffResults.AddRange(
            new DiffResult
            {
                Id = Guid.NewGuid(),
                WorkItemId = workItemId,
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
                WorkItemId = workItemId,
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
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var stats = await _sut.GetStatisticsAsync(workItemId, TestContext.Current.CancellationToken);

        // Assert
        stats.TotalDiffCount.Should().Be(2);
        stats.TotalAddedLines.Should().Be(4);
        stats.TotalRemovedLines.Should().Be(3);
        stats.TotalModifiedLines.Should().Be(1);
        stats.StatusBreakdown[DiffResultStatus.Generated].Should().Be(1);
        stats.StatusBreakdown[DiffResultStatus.Error].Should().Be(1);
    }

    /// <summary><summary>DeleteDiffAsync_ShouldDeleteDiff_WhenExisting.</summary>.</summary>
    [Fact]
    public async Task DeleteDiffAsync_ShouldDeleteDiff_WhenExisting()
    {
        // Arrange
        var workItemId = await SeedAufgabeAsync();
        var diff = await _sut.GenerateDiffAsync(workItemId, "src/file.cs", "a", "b", ct: TestContext.Current.CancellationToken);

        // Act
        var deleted = await _sut.DeleteDiffAsync(diff.Id, TestContext.Current.CancellationToken);

        // Assert
        deleted.Should().BeTrue();
        _db.DiffResults.Should().NotContain(dr => dr.Id == diff.Id);
    }

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        _memoryCache.Dispose();
        _db.Dispose();
    }

    private async Task<Guid> SeedAufgabeAsync()
    {
        var projectId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
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
            Title = "Diff-Aufgabe",
            Status = WorkItemStatus.New,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();
        return workItemId;
    }
}
