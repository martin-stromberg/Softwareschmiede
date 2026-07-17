using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services.Updates;

/// <summary>Tests für <see cref="CliUpdateSafetyService"/>.</summary>
public sealed class CliUpdateSafetyServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db = TestDbContextFactory.Create();
    private readonly AufgabeService _aufgabeService;
    private readonly Guid _projektId = new("33333333-3333-3333-3333-333333333333");

    /// <summary>Initialisiert die Testdatenbank.</summary>
    public CliUpdateSafetyServiceTests()
    {
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance);
        _db.Projekte.Add(new Softwareschmiede.Domain.Entities.Projekt
        {
            Id = _projektId,
            Name = "Projekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Nur tatsächlich laufende CLI-Aufgaben werden als riskant gezählt; "Bereit" (null) und "Wartet auf Eingabe" blockieren nicht.</summary>
    [Fact]
    public async Task CheckAsync_ShouldTreatOnlyRunningStatusAsRisky()
    {
        var running = await CreateActiveTaskAsync("Laeuft", AufgabeLaufStatus.Laeuft);
        var nullStatus = await CreateActiveTaskAsync("Null", null);
        var waiting = await CreateActiveTaskAsync("Wartet", AufgabeLaufStatus.WartetAufEingabe);
        var sut = new CliUpdateSafetyService(_aufgabeService, NullLogger<CliUpdateSafetyService>.Instance);

        var result = await sut.CheckAsync();

        result.RequiresConfirmation.Should().BeTrue();
        result.RiskyTaskCount.Should().Be(1);
        result.RiskyTasks.Should().Contain(t => t.Contains(running.Titel, StringComparison.Ordinal));
        result.RiskyTasks.Should().NotContain(t => t.Contains(nullStatus.Titel, StringComparison.Ordinal));
        result.RiskyTasks.Should().NotContain(t => t.Contains(waiting.Titel, StringComparison.Ordinal));
    }

    private async Task<Softwareschmiede.Domain.Entities.Aufgabe> CreateActiveTaskAsync(string title, AufgabeLaufStatus? laufStatus)
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, title, null);
        await _aufgabeService.StartenAsync(aufgabe.Id, "feature/test", "C:/repo");
        await _aufgabeService.AktivenLaufSetzenAsync(aufgabe.Id, Guid.NewGuid().ToString("N"));
        var tracked = await _db.Aufgaben.FindAsync(aufgabe.Id);
        tracked!.LaufStatus = laufStatus;
        await _db.SaveChangesAsync();
        return tracked;
    }
}
