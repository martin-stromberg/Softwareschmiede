using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.App.Services;

/// <summary>Unit-Tests für AutonomAufgabeStartService, insbesondere den Fehlerpfad von StarteAsync.</summary>
public sealed class AutonomAufgabeStartServiceTests : IDisposable
{
    private readonly SoftwareschmiededDbContext _db;
    private readonly AufgabeService _aufgabeService;
    private readonly Guid _projektId = Guid.NewGuid();

    /// <summary>AutonomAufgabeStartServiceTests.</summary>
    public AutonomAufgabeStartServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _aufgabeService = new AufgabeService(_db, NullLogger<AufgabeService>.Instance, new TodoService(_db, NullLogger<TodoService>.Instance));

        _db.Projekte.Add(new Projekt
        {
            Id = _projektId,
            Name = "Testprojekt",
            ErstellungsDatum = DateTimeOffset.UtcNow,
            Status = ProjektStatus.Aktiv
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>
    /// StarteAsync fängt Fehler ab, die beim Auflösen des Initialisierungsdialogs auftreten (hier: fehlende
    /// Registrierung von AutonomAufgabeInitialisierungsDialogViewModel im IServiceProvider-Mock), und gibt dabei
    /// weiterhin die bereits geladene Aufgabe zurück statt null, damit die aufrufende Detail-Ansicht nicht einen
    /// veralteten Stand anzeigt.
    /// </summary>
    [Fact]
    public async Task StarteAsync_GibtBereitsGeladeneAufgabeZurueck_BeiFehlerWaehrendInitialisierung()
    {
        var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Testaufgabe", "Beschreibung", null);

        var dialogServiceMock = new Mock<IDialogService>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        // Bewusst keine Registrierung von AutonomAufgabeInitialisierungsDialogViewModel:
        // GetRequiredService wirft eine InvalidOperationException, die StarteAsync abfangen muss.

        var sut = new AutonomAufgabeStartService(
            serviceProviderMock.Object,
            dialogServiceMock.Object,
            _aufgabeService,
            NullLogger<AutonomAufgabeStartService>.Instance);

        var ergebnis = await sut.StarteAsync(aufgabe, CancellationToken.None);

        ergebnis.Should().NotBeNull();
        ergebnis!.FehlerMeldung.Should().NotBeNullOrEmpty();
        ergebnis.AktualisierteAufgabe.Should().NotBeNull();
        ergebnis.AktualisierteAufgabe!.Id.Should().Be(aufgabe.Id);
    }
}
