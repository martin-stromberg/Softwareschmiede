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
    private readonly ProtokollService _protokollService;
    private readonly AufgabeService _aufgabeService;
    private readonly ProjektService _projektService;

    /// <summary>RateLimitDetectionServiceIntegrationTests.</summary>
    public RateLimitDetectionServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _protokollService = new ProtokollService(_db, NullLogger<ProtokollService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary><summary>MarkerInAusgabe_WirdErkannt_UndRateLimitEintragErstellt.</summary>.</summary>
    [Fact]
    public async Task MarkerInAusgabe_WirdErkannt_UndRateLimitEintragErstellt()
    {
        var project = await _projektService.CreateAsync("RateLimit-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _aufgabeService.CreateAsync(project.Id, "RateLimit-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        var markerZeile = "[[SOFTWARESCHMIEDE_RATE_LIMIT:2026-06-15T10:00:00Z]]";
        await _protokollService.AddCliOutputAsync(workItem.Id, markerZeile, TestContext.Current.CancellationToken);

        var eintraege = await _protokollService.GetByAufgabeAsync(workItem.Id, TestContext.Current.CancellationToken);
        eintraege.Should().HaveCount(2);
        eintraege.Should().Contain(e => e.Type == LogEntryType.RateLimit);
    }

    /// <summary><summary>ParseRateLimitMarker_ExtrahiertZeitstempel_Korrekt.</summary>.</summary>
    [Fact]
    public void ParseRateLimitMarker_ExtrahiertZeitstempel_Korrekt()
    {
        var (found, _, resetUtc) = ProtokollService.ParseRateLimitMarker("[[SOFTWARESCHMIEDE_RATE_LIMIT:2026-06-15T10:00:00Z]]");

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
        var workItem = await _aufgabeService.CreateAsync(project.Id, "Vorschlag-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        await _aufgabeService.StartenAsync(workItem.Id, "main", "/tmp/repo", ct: TestContext.Current.CancellationToken);

        await _aufgabeService.SavePromptVorschlagAsync(workItem.Id, "Fortsetze mit Analyse der Fehler", DateTimeOffset.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        await _aufgabeService.SetStatusAsync(workItem.Id, WorkItemStatus.Waiting, TestContext.Current.CancellationToken);

        var geladen = await _aufgabeService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.Status.Should().Be(WorkItemStatus.Waiting);
        geladen.SuggestedPrompt.Should().Be("Fortsetze mit Analyse der Fehler");
        geladen.SuggestedExecuteAfterUtc.Should().NotBeNull();
    }

    /// <summary><summary>ClearPromptVorschlag_EntferntVorschlagUndZeitstempel.</summary>.</summary>
    [Fact]
    public async Task ClearPromptVorschlag_EntferntVorschlagUndZeitstempel()
    {
        var project = await _projektService.CreateAsync("Clear-Projekt", null, TestContext.Current.CancellationToken);
        var workItem = await _aufgabeService.CreateAsync(project.Id, "Clear-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        await _aufgabeService.SavePromptVorschlagAsync(workItem.Id, "Ein Vorschlag", DateTimeOffset.UtcNow.AddHours(2), TestContext.Current.CancellationToken);
        await _aufgabeService.ClearPromptVorschlagAsync(workItem.Id, TestContext.Current.CancellationToken);

        var geladen = await _aufgabeService.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.SuggestedPrompt.Should().BeNull();
        geladen.SuggestedExecuteAfterUtc.Should().BeNull();
    }
}
