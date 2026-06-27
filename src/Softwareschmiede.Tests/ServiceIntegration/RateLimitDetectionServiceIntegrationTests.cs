using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: Marker erkennen, Vorschlag speichern, Status → Wartend.</summary>
public sealed class RateLimitDetectionServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly ProtokollService _protokollService;
    private readonly AufgabeService _aufgabeService;
    private readonly ProjektService _projektService;

    /// <summary>RateLimitDetectionServiceIntegrationTests.</summary>
    public RateLimitDetectionServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _protokollService = new ProtokollService(_db, NullLogger<ProtokollService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary><summary>MarkerInAusgabe_WirdErkannt_UndRateLimitEintragErstellt.</summary>.</summary>
    [Fact]
    /// <summary>MarkerInAusgabe_WirdErkannt_UndRateLimitEintragErstellt.</summary>
    public async Task MarkerInAusgabe_WirdErkannt_UndRateLimitEintragErstellt()
    {
        var projekt = await _projektService.CreateAsync("RateLimit-Projekt", null);
        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "RateLimit-Aufgabe", null);

        var markerZeile = "[[SOFTWARESCHMIEDE_RATE_LIMIT:2026-06-15T10:00:00Z]]";
        await _protokollService.AddCliOutputAsync(aufgabe.Id, markerZeile);

        var eintraege = await _protokollService.GetByAufgabeAsync(aufgabe.Id);
        eintraege.Should().HaveCount(2);
        eintraege.Should().Contain(e => e.Typ == ProtokollTyp.RateLimit);
    }

    /// <summary><summary>ParseRateLimitMarker_ExtrahiertZeitstempel_Korrekt.</summary>.</summary>
    [Fact]
    /// <summary>ParseRateLimitMarker_ExtrahiertZeitstempel_Korrekt.</summary>
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
    /// <summary>VorschlagPrompt_WirdGespeichert_UndStatusWirdWartend.</summary>
    public async Task VorschlagPrompt_WirdGespeichert_UndStatusWirdWartend()
    {
        var projekt = await _projektService.CreateAsync("Vorschlag-Projekt", null);
        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Vorschlag-Aufgabe", null);

        await _aufgabeService.StartenAsync(aufgabe.Id, "main", "/tmp/repo");

        await _aufgabeService.SavePromptVorschlagAsync(
            aufgabe.Id,
            "Fortsetze mit Analyse der Fehler",
            DateTimeOffset.UtcNow.AddHours(1));

        await _aufgabeService.SetStatusAsync(aufgabe.Id, AufgabeStatus.Wartend);

        var geladen = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        geladen!.Status.Should().Be(AufgabeStatus.Wartend);
        geladen.VorschlagPrompt.Should().Be("Fortsetze mit Analyse der Fehler");
        geladen.VorschlagAusfuehrenAbUtc.Should().NotBeNull();
    }

    /// <summary><summary>ClearPromptVorschlag_EntferntVorschlagUndZeitstempel.</summary>.</summary>
    [Fact]
    /// <summary>ClearPromptVorschlag_EntferntVorschlagUndZeitstempel.</summary>
    public async Task ClearPromptVorschlag_EntferntVorschlagUndZeitstempel()
    {
        var projekt = await _projektService.CreateAsync("Clear-Projekt", null);
        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Clear-Aufgabe", null);

        await _aufgabeService.SavePromptVorschlagAsync(aufgabe.Id, "Ein Vorschlag", DateTimeOffset.UtcNow.AddHours(2));
        await _aufgabeService.ClearPromptVorschlagAsync(aufgabe.Id);

        var geladen = await _aufgabeService.GetByIdAsync(aufgabe.Id);
        geladen!.VorschlagPrompt.Should().BeNull();
        geladen.VorschlagAusfuehrenAbUtc.Should().BeNull();
    }
}
