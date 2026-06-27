using Bunit;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Components.Pages.Diff;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;
using DomainDiffLine = Softwareschmiede.Domain.Entities.DiffLine;

namespace Softwareschmiede.Tests.Components.Pages.Diff;

/// <summary>DiffViewerPageBunitTests.</summary>
public sealed class DiffViewerPageBunitTests : TestContext
{
    /// <summary><summary>DiffViewerPage_ShouldRenderStandaloneDiffViewer_WhenDiffExists.</summary>.</summary>
    [Fact]
    /// <summary>DiffViewerPage_ShouldRenderStandaloneDiffViewer_WhenDiffExists.</summary>
    public async Task DiffViewerPage_ShouldRenderStandaloneDiffViewer_WhenDiffExists()
    {
        await using var harness = await CreateHarnessAsync();

        var cut = RenderComponent<DiffViewerPage>(parameters => parameters
            .Add(page => page.DiffResultId, harness.DiffId));

        cut.WaitForAssertion(() =>
        {
            cut.Find("div.diff-viewer").GetAttribute("role").Should().Be("main");
            cut.Markup.Should().Contain("src/wrapper-route.cs");
        });
    }

    /// <summary><summary>DiffViewerPage_ShouldRenderStandaloneNotFoundState_WhenDiffDoesNotExist.</summary>.</summary>
    [Fact]
    /// <summary>DiffViewerPage_ShouldRenderStandaloneNotFoundState_WhenDiffDoesNotExist.</summary>
    public void DiffViewerPage_ShouldRenderStandaloneNotFoundState_WhenDiffDoesNotExist()
    {
        Services.AddSingleton(CreateDiffServiceWithEmptyDatabase());
        Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<Softwareschmiede.Components.Diff.DiffViewer>>(NullLogger<Softwareschmiede.Components.Diff.DiffViewer>.Instance);
        Services.AddLogging();

        var cut = RenderComponent<DiffViewerPage>(parameters => parameters
            .Add(page => page.DiffResultId, Guid.NewGuid()));

        cut.WaitForAssertion(() =>
        {
            cut.Find("div.diff-viewer").GetAttribute("role").Should().Be("main");
            cut.Markup.Should().Contain("Diff with ID");
            cut.Markup.Should().Contain("Back to Home");
        });
    }

    private async Task<TestHarness> CreateHarnessAsync()
    {
        var db = TestDbContextFactory.Create();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var projekt = new Projekt
        {
            Id = Guid.NewGuid(),
            Name = "DiffViewer Page Test Project",
            Status = ProjektStatus.Aktiv,
            ErstellungsDatum = DateTimeOffset.UtcNow,
        };

        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projekt.Id,
            Titel = "DiffViewer Page Test Task",
            Status = AufgabeStatus.InArbeit,
            ErstellungsDatum = DateTimeOffset.UtcNow,
        };

        var diff = CreateDiffResult(aufgabe.Id, "src/wrapper-route.cs");

        db.Projekte.Add(projekt);
        db.Aufgaben.Add(aufgabe);
        db.DiffResults.Add(diff);
        await db.SaveChangesAsync();

        var algorithmService = new DiffAlgorithmService(NullLogger<DiffAlgorithmService>.Instance);
        var cachingService = new DiffCachingService(db, memoryCache, NullLogger<DiffCachingService>.Instance);
        var diffService = new DiffService(db, algorithmService, cachingService, NullLogger<DiffService>.Instance);

        Services.AddSingleton(diffService);
        Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<Softwareschmiede.Components.Diff.DiffViewer>>(NullLogger<Softwareschmiede.Components.Diff.DiffViewer>.Instance);
        Services.AddLogging();

        return new TestHarness(db, memoryCache, diff.Id);
    }

    private static DiffService CreateDiffServiceWithEmptyDatabase()
    {
        var db = TestDbContextFactory.Create();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var algorithmService = new DiffAlgorithmService(NullLogger<DiffAlgorithmService>.Instance);
        var cachingService = new DiffCachingService(db, memoryCache, NullLogger<DiffCachingService>.Instance);
        return new DiffService(db, algorithmService, cachingService, NullLogger<DiffService>.Instance);
    }

    private static DiffResult CreateDiffResult(Guid aufgabeId, string filePath)
    {
        var diffResultId = Guid.NewGuid();
        var blockId = Guid.NewGuid();

        return new DiffResult
        {
            Id = diffResultId,
            AufgabeId = aufgabeId,
            FilePath = filePath,
            SourceVersion = "HEAD~1",
            TargetVersion = "HEAD",
            Status = DiffResultStatus.Generated,
            DiffType = DiffType.Full,
            GeneratedAt = DateTimeOffset.UtcNow,
            GeneratedBy = nameof(DiffViewerPageBunitTests),
            AddedLines = 1,
            RemovedLines = 0,
            ModifiedLines = 0,
            LineCount = 1,
            DiffBlocks =
            [
                new DiffBlock
                {
                    Id = blockId,
                    DiffResultId = diffResultId,
                    BlockType = DiffBlockType.Added,
                    BlockSequence = 0,
                    SourceStartLine = 1,
                    SourceEndLine = 1,
                    TargetStartLine = 1,
                    TargetEndLine = 1,
                    DiffLines =
                    [
                        new DomainDiffLine
                        {
                            Id = Guid.NewGuid(),
                            DiffBlockId = blockId,
                            LineStatus = DiffLineStatus.Added,
                            Content = "line",
                            SourceLineNumber = null,
                            TargetLineNumber = 1,
                            LineSequence = 0,
                        },
                    ],
                },
            ],
        };
    }

    private sealed class TestHarness(
        Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext db,
        MemoryCache memoryCache,
        Guid diffId) : IAsyncDisposable
    {
        public Guid DiffId { get; } = diffId;

        /// <summary>DisposeAsync.</summary>
        public ValueTask DisposeAsync()
        {
            memoryCache.Dispose();
            db.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
