using Bunit;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Components.Diff;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;
using DomainDiffLine = Softwareschmiede.Domain.Entities.DiffLine;

namespace Softwareschmiede.Tests.Components.Diff;

public sealed class DiffViewerBunitTests : TestContext
{
    /// <summary>
    /// Verifiziert, dass der DiffViewer bei Parameterwechsel den neuen Diff lädt statt stale Inhalte zu behalten.
    /// </summary>
    [Fact]
    public async Task DiffViewer_ShouldReloadDiff_WhenDiffResultIdChanges()
    {
        await using var harness = await CreateHarnessAsync();

        var cut = RenderComponent<DiffViewer>(parameters => parameters
            .Add(viewer => viewer.DiffResultId, harness.FirstDiffId)
            .Add(viewer => viewer.PresentationMode, DiffViewerPresentationMode.Embedded));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("src/file-a.cs"));

        cut.SetParametersAndRender(parameters => parameters
            .Add(viewer => viewer.DiffResultId, harness.SecondDiffId)
            .Add(viewer => viewer.PresentationMode, DiffViewerPresentationMode.Embedded));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("src/file-b.cs");
            cut.Markup.Should().NotContain("src/file-a.cs");
        });
    }

    [Fact]
    public void DiffViewer_ShouldShowValidationError_WhenDiffResultIdIsEmpty()
    {
        Services.AddSingleton(CreateDiffServiceWithEmptyDatabase());
        Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<DiffViewer>>(NullLogger<DiffViewer>.Instance);
        Services.AddLogging();

        var cut = RenderComponent<DiffViewer>(parameters => parameters
            .Add(viewer => viewer.DiffResultId, Guid.Empty)
            .Add(viewer => viewer.PresentationMode, DiffViewerPresentationMode.Embedded));

        cut.Markup.Should().Contain("A valid diff id is required.");
    }

    [Fact]
    public void DiffViewer_ShouldShowNotFoundAndStandaloneBackLink_WhenDiffDoesNotExist()
    {
        Services.AddSingleton(CreateDiffServiceWithEmptyDatabase());
        Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<DiffViewer>>(NullLogger<DiffViewer>.Instance);
        Services.AddLogging();

        var cut = RenderComponent<DiffViewer>(parameters => parameters
            .Add(viewer => viewer.DiffResultId, Guid.NewGuid())
            .Add(viewer => viewer.PresentationMode, DiffViewerPresentationMode.Standalone));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Diff with ID");
            cut.Markup.Should().Contain("Back to Home");
            cut.Find("div.diff-viewer").GetAttribute("role").Should().Be("main");
        });
    }

    [Fact]
    public void DiffViewer_ShouldShowNotFoundWithoutBackLink_WhenEmbedded()
    {
        Services.AddSingleton(CreateDiffServiceWithEmptyDatabase());
        Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<DiffViewer>>(NullLogger<DiffViewer>.Instance);
        Services.AddLogging();

        var cut = RenderComponent<DiffViewer>(parameters => parameters
            .Add(viewer => viewer.DiffResultId, Guid.NewGuid())
            .Add(viewer => viewer.PresentationMode, DiffViewerPresentationMode.Embedded));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Diff with ID");
            cut.Markup.Should().NotContain("Back to Home");
            cut.Find("div.diff-viewer").GetAttribute("role").Should().Be("region");
        });
    }

    private async Task<TestHarness> CreateHarnessAsync()
    {
        var db = TestDbContextFactory.Create();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var projekt = new Projekt
        {
            Id = Guid.NewGuid(),
            Name = "DiffViewer Test Project",
            Status = ProjektStatus.Aktiv,
            ErstellungsDatum = DateTimeOffset.UtcNow,
        };

        var aufgabe = new Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projekt.Id,
            Titel = "DiffViewer Test Task",
            Status = AufgabeStatus.InArbeit,
            ErstellungsDatum = DateTimeOffset.UtcNow,
        };

        var firstDiff = CreateDiffResult(aufgabe.Id, "src/file-a.cs");
        var secondDiff = CreateDiffResult(aufgabe.Id, "src/file-b.cs");

        db.Projekte.Add(projekt);
        db.Aufgaben.Add(aufgabe);
        db.DiffResults.AddRange(firstDiff, secondDiff);
        await db.SaveChangesAsync();

        var algorithmService = new DiffAlgorithmService(NullLogger<DiffAlgorithmService>.Instance);
        var cachingService = new DiffCachingService(db, memoryCache, NullLogger<DiffCachingService>.Instance);
        var diffService = new DiffService(db, algorithmService, cachingService, NullLogger<DiffService>.Instance);

        Services.AddSingleton(diffService);
        Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<DiffViewer>>(NullLogger<DiffViewer>.Instance);
        Services.AddLogging();

        return new TestHarness(db, memoryCache, firstDiff.Id, secondDiff.Id);
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
            GeneratedBy = nameof(DiffViewerBunitTests),
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
        Guid firstDiffId,
        Guid secondDiffId) : IAsyncDisposable
    {
        public Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext Db { get; } = db;
        public Guid FirstDiffId { get; } = firstDiffId;
        public Guid SecondDiffId { get; } = secondDiffId;

        public ValueTask DisposeAsync()
        {
            memoryCache.Dispose();
            Db.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
