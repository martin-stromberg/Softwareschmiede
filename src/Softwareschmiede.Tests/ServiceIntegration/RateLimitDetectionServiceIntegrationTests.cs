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
    private readonly ProjectService _projektService;

    /// <summary>RateLimitDetectionServiceIntegrationTests.</summary>
    public RateLimitDetectionServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _logService = new LogService(_db, NullLogger<LogService>.Instance);
        _workItemService = new WorkItemService(_db, NullLogger<WorkItemService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _projektService = new ProjectService(_db, NullLogger<ProjectService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary><summary>MarkerInAusgabe_WirdErkannt_UndRateLimitEintragErstellt.</summary>.</summary>
    [Fact]
    public async Task MarkerInAusgabe_WirdErkannt_UndRateLimitEintragErstellt()
    {
        var project = await _projektService.CreateAsync("RateLimit-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(project.Id, "RateLimit-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        var markerZeile = "[[SOFTWARESCHMIEDE_RATE_LIMIT:2026-06-15T10:00:00Z]]";
        await _logService.AddCliOutputAsync(workItem.Id, markerZeile, TestContext.Current.CancellationToken);

        var entries = await _logService.GetByWorkItemAsync(workItem.Id, TestContext.Current.CancellationToken);
        entries.Should().HaveCount(2);
        entries.Should().Contain(e => e.Type == LogEntryType.RateLimit);
    }

    /// <summary><summary>ParseRateLimitMarker_ExtrahiertZeitstempel_Korrekt.</summary>.</summary>
    [Fact]
    public void ParseRateLimitMarker_ExtrahiertZeitstempel_Korrekt()
    {
        var (found, _, resetUtc) = LogService.ParseRateLimitMarker("[[SOFTWARESCHMIEDE_RATE_LIMIT:2026-06-15T10:00:00Z]]");

        found.Should().BeTrue();
        resetUtc.Should().NotBeNull();
        resetUtc!.Value.Year.Should().Be(2026);
        resetUtc.Value.Month.Should().Be(6);
        resetUtc.Value.Day.Should().Be(15);
    }

    /// <summary><summary>VorschlagPrompt_WirdGespeichert_UndStatusWirdWartend.</summary>.</summary>
    [Fact]
    public async Task VorschlagPrompt_WirdGespeichert_UndStatusWirdWartend()
    {
        var project = await _projektService.CreateAsync("Vorschlag-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(project.Id, "Vorschlag-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        await _workItemService.StartAsync(workItem.Id, "main", "/tmp/repo", ct: TestContext.Current.CancellationToken);

        await _workItemService.SavePromptVorschlagAsync(workItem.Id, "Fortsetze mit Analyse der Fehler", DateTimeOffset.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        await _workItemService.SetStatusAsync(workItem.Id, WorkItemStatus.Waiting, TestContext.Current.CancellationToken);

        var geladen = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.Status.Should().Be(WorkItemStatus.Waiting);
        geladen.SuggestedPrompt.Should().Be("Fortsetze mit Analyse der Fehler");
        geladen.SuggestedExecuteAfterUtc.Should().NotBeNull();
    }

    /// <summary><summary>ClearPromptVorschlag_EntferntVorschlagUndZeitstempel.</summary>.</summary>
    [Fact]
    public async Task ClearPromptVorschlag_EntferntVorschlagUndZeitstempel()
    {
        var project = await _projektService.CreateAsync("Clear-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _workItemService.CreateAsync(project.Id, "Clear-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        await _workItemService.SavePromptVorschlagAsync(workItem.Id, "Ein Vorschlag", DateTimeOffset.UtcNow.AddHours(2), TestContext.Current.CancellationToken);
        await _workItemService.ClearPromptVorschlagAsync(workItem.Id, TestContext.Current.CancellationToken);

        var geladen = await _workItemService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.SuggestedPrompt.Should().BeNull();
        geladen.SuggestedExecuteAfterUtc.Should().BeNull();
    }
}
