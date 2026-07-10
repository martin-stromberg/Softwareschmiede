using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>
/// Tests für die Verwaltung eines aktiven CLI-Laufs auf <see cref="AufgabeService"/>
/// (<see cref="AufgabeService.AktivenLaufSetzenAsync"/>, <see cref="AufgabeService.AktivenLaufBeendenAsync"/>,
/// <see cref="AufgabeService.AktualisiereLaufStatusAsync"/>). Deckt den vollständigen Substatus-Zyklus
/// Bereit → Läuft → Wartet (während der Prozess noch lebt) → Bereit (nach Stopp) aus Sicht des Services ab
/// (Issue 108, Folgefehler des Rückwegs Läuft → Wartet: <see cref="Domain.Entities.Aufgabe.LaufStatus"/>
/// bildet den bis dahin nirgends persistierten Laufzeit-Substatus der CLI ab).
/// </summary>
public sealed class AufgabeServiceTests_AktiverLauf : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiededDbContext _db;
    private readonly Mock<ILogger<AufgabeService>> _loggerMock;
    private readonly AufgabeService _sut;
    private readonly Guid _projektId = new Guid("22222222-2222-2222-2222-222222222222");

    /// <summary>AufgabeServiceTests_AktiverLauf.</summary>
    public AufgabeServiceTests_AktiverLauf()
    {
        _db = TestDbContextFactory.Create();
        _loggerMock = new Mock<ILogger<AufgabeService>>();
        _sut = new AufgabeService(_db, _loggerMock.Object);

        _db.Projekte.Add(new Softwareschmiede.Domain.Entities.Projekt
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
    /// AktivenLaufSetzenAsync setzt AktiveRunId, LastHeartbeatUtc und LetzterCliStartUtc sofort (Issue 108:
    /// die Seitenleisten-Kachel darf nicht bis zu 30s auf den ersten periodischen Heartbeat warten müssen).
    /// Setzt außerdem LaufStatus auf Laeuft (Standardwert von PseudoConsoleSession.RuntimeStatus beim Start).
    /// </summary>
    [Fact]
    public async Task AktivenLaufSetzenAsync_ShouldSetAktiveRunIdAndHeartbeat_WhenCalled()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Aktiver-Lauf-Aufgabe", null);
        aufgabe.AktiveRunId.Should().BeNull();

        // Act
        var vorUpdate = DateTimeOffset.UtcNow;
        await _sut.AktivenLaufSetzenAsync(aufgabe.Id, "lauf-123");
        var nachUpdate = DateTimeOffset.UtcNow;

        // Assert
        var geladen = await _sut.GetByIdAsync(aufgabe.Id);
        geladen!.AktiveRunId.Should().Be("lauf-123");
        geladen.LastHeartbeatUtc.Should().NotBeNull();
        geladen.LastHeartbeatUtc!.Value.Should().BeOnOrAfter(vorUpdate.AddSeconds(-1));
        geladen.LastHeartbeatUtc!.Value.Should().BeOnOrBefore(nachUpdate.AddSeconds(1));
        geladen.LetzterCliStartUtc.Should().NotBeNull();
        geladen.LetzterCliStartUtc!.Value.Should().BeOnOrAfter(vorUpdate.AddSeconds(-1));
        geladen.LetzterCliStartUtc!.Value.Should().BeOnOrBefore(nachUpdate.AddSeconds(1));
        geladen.LaufStatus.Should().Be(AufgabeLaufStatus.Laeuft);
    }

    /// <summary>AktivenLaufBeendenAsync entfernt eine zuvor gesetzte AktiveRunId und den LaufStatus.</summary>
    [Fact]
    public async Task AktivenLaufBeendenAsync_ShouldClearAktiveRunId_WhenPreviouslySet()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Aktiver-Lauf-Beenden-Aufgabe", null);
        await _sut.AktivenLaufSetzenAsync(aufgabe.Id, "lauf-456");
        (await _sut.GetByIdAsync(aufgabe.Id))!.AktiveRunId.Should().Be("lauf-456");

        // Act
        await _sut.AktivenLaufBeendenAsync(aufgabe.Id);

        // Assert
        var geladen = await _sut.GetByIdAsync(aufgabe.Id);
        geladen!.AktiveRunId.Should().BeNull();
    }

    /// <summary>
    /// Regressionstest (Issue 108, Folgefehler des Rückwegs Läuft → Wartet): Nach dem Beenden eines Laufs
    /// muss auch ein zuvor gesetzter Laufzeit-Substatus (LaufStatus) entfernt werden — sonst könnte ein
    /// veralteter "WartetAufEingabe"-Wert eines beendeten Laufs fälschlich für den nächsten Lauf derselben
    /// Aufgabe sichtbar bleiben, bevor der erste RuntimeStatusChanged-Event des neuen Laufs eintrifft.
    /// </summary>
    [Fact]
    public async Task AktivenLaufBeendenAsync_ShouldClearLaufStatus_WhenPreviouslySetToWartetAufEingabe()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Aktiver-Lauf-Beenden-LaufStatus-Aufgabe", null);
        await _sut.AktivenLaufSetzenAsync(aufgabe.Id, "lauf-789");
        await _sut.AktualisiereLaufStatusAsync(aufgabe.Id, AufgabeLaufStatus.WartetAufEingabe);
        (await _sut.GetByIdAsync(aufgabe.Id))!.LaufStatus.Should().Be(AufgabeLaufStatus.WartetAufEingabe);

        // Act
        await _sut.AktivenLaufBeendenAsync(aufgabe.Id);

        // Assert
        var geladen = await _sut.GetByIdAsync(aufgabe.Id);
        geladen!.LaufStatus.Should().BeNull();
    }

    /// <summary>
    /// AktualisiereLaufStatusAsync muss den Laufzeit-Substatus einer Aufgabe mit aktivem Lauf auf
    /// WartetAufEingabe umschalten können, damit KiAusfuehrungsStatusConverter die Kachel korrekt von
    /// "▶ Läuft" auf "⏸ Wartet" umschalten kann, während der CLI-Prozess noch lebt.
    /// </summary>
    [Fact]
    public async Task AktualisiereLaufStatusAsync_ShouldSetWartetAufEingabe_WhenAktiveRunIdIsSet()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "LaufStatus-Wartet-Aufgabe", null);
        await _sut.AktivenLaufSetzenAsync(aufgabe.Id, "lauf-999");

        // Act
        await _sut.AktualisiereLaufStatusAsync(aufgabe.Id, AufgabeLaufStatus.WartetAufEingabe);

        // Assert
        var geladen = await _sut.GetByIdAsync(aufgabe.Id);
        geladen!.LaufStatus.Should().Be(AufgabeLaufStatus.WartetAufEingabe);
    }

    /// <summary>
    /// AktualisiereLaufStatusAsync darf keinen Substatus mehr setzen, wenn AktiveRunId bereits entfernt
    /// wurde (Prozess ist schon beendet, ein verspätetes RuntimeStatusChanged-Event trifft danach noch ein) —
    /// sonst könnte ein veralteter Substatus fälschlich wieder auftauchen, nachdem AktivenLaufBeendenAsync
    /// ihn bereits korrekt entfernt hat.
    /// </summary>
    [Fact]
    public async Task AktualisiereLaufStatusAsync_ShouldNotSetLaufStatus_WhenAktiveRunIdIsNull()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "LaufStatus-Ohne-AktivenLauf-Aufgabe", null);
        aufgabe.AktiveRunId.Should().BeNull();

        // Act
        await _sut.AktualisiereLaufStatusAsync(aufgabe.Id, AufgabeLaufStatus.WartetAufEingabe);

        // Assert
        var geladen = await _sut.GetByIdAsync(aufgabe.Id);
        geladen!.LaufStatus.Should().BeNull();
    }

    /// <summary>
    /// Voller Zyklus (Issue 108): Bereit → Läuft → Wartet (Prozess lebt noch) → Bereit (nach Stopp).
    /// Deckt aus Service-Sicht denselben Ablauf ab, den CliProcessManagerTests_LaufStatus über die
    /// tatsächliche Produktionsverdrahtung (PseudoConsoleSession.RuntimeStatusChanged) prüft.
    /// </summary>
    [Fact]
    public async Task VollerZyklus_BereitLaeuftWartetBereit_PersistiertJedenSchrittKorrekt()
    {
        // Bereit: neue Aufgabe hat weder aktiven Lauf noch Substatus.
        var aufgabe = await _sut.CreateAsync(_projektId, "Voller-Zyklus-Aufgabe", null);
        aufgabe.AktiveRunId.Should().BeNull();
        aufgabe.LaufStatus.Should().BeNull();

        // Läuft: CLI-Prozess gestartet.
        await _sut.AktivenLaufSetzenAsync(aufgabe.Id, "lauf-zyklus-1");
        var nachStart = await _sut.GetByIdAsync(aufgabe.Id);
        nachStart!.AktiveRunId.Should().NotBeNull();
        nachStart.LaufStatus.Should().Be(AufgabeLaufStatus.Laeuft);

        // Wartet: CLI erzeugt seit längerer Zeit keine Ausgabe mehr (Prozess lebt weiterhin).
        await _sut.AktualisiereLaufStatusAsync(aufgabe.Id, AufgabeLaufStatus.WartetAufEingabe);
        var waehrendWarten = await _sut.GetByIdAsync(aufgabe.Id);
        waehrendWarten!.AktiveRunId.Should().NotBeNull("der Prozess lebt während des Wartens weiterhin");
        waehrendWarten.LaufStatus.Should().Be(AufgabeLaufStatus.WartetAufEingabe);

        // Bereit: CLI-Prozess beendet.
        await _sut.AktivenLaufBeendenAsync(aufgabe.Id);
        var nachStopp = await _sut.GetByIdAsync(aufgabe.Id);
        nachStopp!.AktiveRunId.Should().BeNull();
        nachStopp.LaufStatus.Should().BeNull();
    }
}
