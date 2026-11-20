using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: Marker erkennen, Vorschlag speichern, Status → Wartend.</summary>
public sealed class RateLimitDetectionServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly LogService _logService;
    private readonly WorkItemService _workItemService;
    private readonly ProjectService _projectService;

    /// <summary>RateLimitDetectionServiceIntegrationTests.</summary>
    public RateLimitDetectionServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _logService = new LogService(_db, NullLogger<LogService>.Instance);
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _projectService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>MarkerInAusgabe_WirdErkannt_UndRateLimitEintragErstellt.</summary>
    [Fact]
    public async Task MarkerInOutput_WillDetected_CreatesAndRateLimitEntry()
    {
        var project = await _projectService.CreateAsync("RateLimit-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(project.Id, "RateLimit-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        var markerLine = "[[SOFTWARESCHMIEDE_RATE_LIMIT:2026-06-15T10:00:00Z]]";
        await _logService.AddCliOutputAsync(workItem.Id, markerLine, TestContext.Current.CancellationToken);

        var entries = await _logService.GetByWorkItemAsync(workItem.Id, TestContext.Current.CancellationToken);
        entries.Should().HaveCount(2);
        entries.Should().Contain(e => e.Type == LogEntryType.RateLimit);
    }

    /// <summary>ParseRateLimitMarker_ExtrahiertZeitstempel_Korrekt.</summary>
    [Fact]
    public void ParseRateLimitMarker_ExtractedTimestamp_Correct()
    {
        var (found, _, resetUtc) = LogService.ParseRateLimitMarker("[[SOFTWARESCHMIEDE_RATE_LIMIT:2026-06-15T10:00:00Z]]");

        found.Should().BeTrue();
        resetUtc.Should().NotBeNull();
        resetUtc!.Value.Year.Should().Be(2026);
        resetUtc.Value.Month.Should().Be(6);
        resetUtc.Value.Day.Should().Be(15);
    }

    /// <summary>VorschlagPrompt_WirdGespeichert_UndStatusWirdWartend.</summary>
    [Fact]
    public async Task SuggestionPrompt_WillSaved_AndStatusWillWaiting()
    {
        var project = await _projectService.CreateAsync("Vorschlag-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(project.Id, "Vorschlag-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        await _workItemService.StartAsync(workItem.Id, "main", "/tmp/repo", ct: TestContext.Current.CancellationToken);

        await _workItemService.SavePromptSuggestionAsync(workItem.Id, "Fortsetze mit Analyse der Fehler", DateTimeOffset.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        await _workItemService.SetStatusAsync(workItem.Id, WorkItemStatus.Waiting, TestContext.Current.CancellationToken);

        var loaded = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        loaded!.Status.Should().Be(WorkItemStatus.Waiting);
        loaded.SuggestedPrompt.Should().Be("Fortsetze mit Analyse der Fehler");
        loaded.SuggestedExecuteAfterUtc.Should().NotBeNull();
    }

    /// <summary>ClearPromptVorschlag_EntferntVorschlagUndZeitstempel.</summary>
    [Fact]
    public async Task ClearPromptSuggestion_RemovesSuggestionAndTimestamp()
    {
        var project = await _projectService.CreateAsync("Clear-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(project.Id, "Clear-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        await _workItemService.SavePromptSuggestionAsync(workItem.Id, "Ein Vorschlag", DateTimeOffset.UtcNow.AddHours(2), TestContext.Current.CancellationToken);
        await _workItemService.ClearPromptSuggestionAsync(workItem.Id, TestContext.Current.CancellationToken);

        var loaded = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        loaded!.SuggestedPrompt.Should().BeNull();
        loaded.SuggestedExecuteAfterUtc.Should().BeNull();
    }
}
