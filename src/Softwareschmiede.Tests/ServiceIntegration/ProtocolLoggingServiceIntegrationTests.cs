using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.ServiceIntegration;

/// <summary>E2E-Test: Stdout-Streaming und Protokoll-Eintrag in UI.</summary>
public sealed class ProtocolLoggingServiceIntegrationTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly ProtokollService _protokollService;
    private readonly AufgabeService _aufgabeService;
    private readonly ProjektService _projektService;
    private Guid _aufgabeId;

    public ProtocolLoggingServiceIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
        _protokollService = new ProtokollService(_db, NullLogger<ProtokollService>.Instance);
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
        _projektService = new ProjektService(_db, NullLogger<ProjektService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedAufgabeAsync()
    {
        var projekt = await _projektService.CreateAsync("Log-Projekt", null);
        var aufgabe = await _aufgabeService.CreateAsync(projekt.Id, "Log-Aufgabe", null);
        _aufgabeId = aufgabe.Id;
    }

    [Fact]
    public async Task AddCliOutputAsync_StreichtAusgabeInProtokoll()
    {
        await SeedAufgabeAsync();

        await _protokollService.AddCliOutputAsync(_aufgabeId, "Zeile 1 der Ausgabe");
        await _protokollService.AddCliOutputAsync(_aufgabeId, "Zeile 2 der Ausgabe");

        var eintraege = await _protokollService.GetByAufgabeAsync(_aufgabeId);
        eintraege.Should().HaveCount(2);
        eintraege.All(e => e.Typ == ProtokollTyp.CliOutput).Should().BeTrue();
    }

    [Fact]
    public async Task AddCliOutputAsync_MehrereZeilen_SindInReihenfolgeGespeichert()
    {
        await SeedAufgabeAsync();
        var zeilen = Enumerable.Range(1, 5).Select(i => $"Ausgabezeile {i}").ToList();

        foreach (var zeile in zeilen)
            await _protokollService.AddCliOutputAsync(_aufgabeId, zeile);

        var eintraege = await _protokollService.GetByAufgabeAsync(_aufgabeId);
        eintraege.Should().HaveCount(5);
        for (var i = 0; i < zeilen.Count; i++)
            eintraege[i].Inhalt.Should().Be(zeilen[i]);
    }

    [Fact]
    public async Task AddStatusUebergangAsync_ErstelltEintragImProtokoll()
    {
        await SeedAufgabeAsync();

        await _protokollService.AddStatusUebergangAsync(_aufgabeId, AufgabeStatus.Neu, AufgabeStatus.Gestartet);

        var eintraege = await _protokollService.GetByAufgabeAsync(_aufgabeId);
        eintraege.Should().HaveCount(1);
        eintraege[0].Typ.Should().Be(ProtokollTyp.StatusUebergang);
        eintraege[0].Inhalt.Should().Contain("Gestartet");
    }
}
