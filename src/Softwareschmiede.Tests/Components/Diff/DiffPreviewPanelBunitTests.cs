using Bunit;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Components.Diff;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Components.Diff;

public sealed class DiffPreviewPanelBunitTests : TestContext
{
    /// <summary>
    /// Verifiziert den Fallback ohne Dateiauswahl.
    /// </summary>
    [Fact]
    public void DiffPreviewPanel_ShouldShowSelectionHint_WhenNoFileIsSelected()
    {
        var cut = RenderComponent<DiffPreviewPanel>(parameters => parameters
            .Add(panel => panel.HasSelectedFile, false)
            .Add(panel => panel.Preview, null)
            .Add(panel => panel.DiffResultId, null));

        cut.Markup.Should().Contain("Wählen Sie links eine Datei aus.");
    }

    /// <summary>
    /// Verifiziert die zentrale Hint-Darstellung für Sonderfälle.
    /// </summary>
    [Fact]
    public void DiffPreviewPanel_ShouldShowHintFallback_WhenPreviewContainsHint()
    {
        var preview = new FilePreview("a.txt", null, false, true, false, "binary", null, "Binärdatei – Vorschau nicht verfügbar.");

        var cut = RenderComponent<DiffPreviewPanel>(parameters => parameters
            .Add(panel => panel.HasSelectedFile, true)
            .Add(panel => panel.Preview, preview)
            .Add(panel => panel.DiffResultId, null));

        cut.Markup.Should().Contain("Binärdatei – Vorschau nicht verfügbar.");
        cut.Markup.Should().Contain("binary");
    }

    /// <summary>
    /// Verifiziert den No-Diff-Fallback für gelöschte Dateien.
    /// </summary>
    [Fact]
    public void DiffPreviewPanel_ShouldShowDeletedFallback_WhenDeletedFileHasNoDiff()
    {
        var preview = new FilePreview("deleted.txt", null, true, false, false, null, "old", null);

        var cut = RenderComponent<DiffPreviewPanel>(parameters => parameters
            .Add(panel => panel.HasSelectedFile, true)
            .Add(panel => panel.Preview, preview)
            .Add(panel => panel.DiffResultId, null));

        cut.Markup.Should().Contain("Datei gelöscht. Für diese Datei ist kein Diff verfügbar.");
    }

    [Fact]
    public void DiffPreviewPanel_ShouldRenderDiffViewer_WhenDiffResultIdIsPresent()
    {
        Services.AddSingleton(CreateDiffServiceWithEmptyDatabase());
        Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<DiffViewer>>(NullLogger<DiffViewer>.Instance);
        Services.AddLogging();

        var preview = new FilePreview("edited.txt", null, false, false, false, "new", "old", null);
        var diffId = Guid.NewGuid();

        var cut = RenderComponent<DiffPreviewPanel>(parameters => parameters
            .Add(panel => panel.HasSelectedFile, true)
            .Add(panel => panel.Preview, preview)
            .Add(panel => panel.DiffResultId, diffId));

        cut.FindComponent<DiffViewer>();
    }

    [Fact]
    public void DiffPreviewPanel_ShouldShowInfoFallback_WhenNoDiffExistsForSelectedFile()
    {
        var preview = new FilePreview("plain.txt", null, false, false, false, "content", "content", null);

        var cut = RenderComponent<DiffPreviewPanel>(parameters => parameters
            .Add(panel => panel.HasSelectedFile, true)
            .Add(panel => panel.Preview, preview)
            .Add(panel => panel.DiffResultId, null));

        cut.Markup.Should().Contain("Für diese Datei ist kein DiffResult vorhanden.");
    }

    private static DiffService CreateDiffServiceWithEmptyDatabase()
    {
        var db = TestDbContextFactory.Create();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var algorithmService = new DiffAlgorithmService(NullLogger<DiffAlgorithmService>.Instance);
        var cachingService = new DiffCachingService(db, memoryCache, NullLogger<DiffCachingService>.Instance);
        return new DiffService(db, algorithmService, cachingService, NullLogger<DiffService>.Instance);
    }
}
