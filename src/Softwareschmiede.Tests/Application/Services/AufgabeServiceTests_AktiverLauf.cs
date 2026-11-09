using System.Collections.Concurrent;
using System.Reflection;
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
/// <see cref="AufgabeService.AktivenLaufSicherstellenAsync"/>). Deckt den vollständigen Substatus-Zyklus
/// Bereit → Läuft → Wartet (während der Prozess noch lebt) → Bereit (nach Stopp) aus Sicht des Services ab
/// (Issue 108, Folgefehler des Rückwegs Läuft → Wartet: <see cref="Softwareschmiede.Domain.Entities.Aufgabe.LaufStatus"/>
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
        _sut = new AufgabeService(_db, _loggerMock.Object, new TodoService(_db, new Mock<ILogger<TodoService>>().Object));

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
        geladen.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.Aktiv);
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
        geladen.LastHeartbeatUtc.Should().BeNull();
        geladen.LetzterCliStartUtc.Should().BeNull();
        geladen.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.Beendet);
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
        await _sut.AktivenLaufSicherstellenAsync(aufgabe.Id, () => AufgabeLaufStatus.WartetAufEingabe);
        (await _sut.GetByIdAsync(aufgabe.Id))!.LaufStatus.Should().Be(AufgabeLaufStatus.WartetAufEingabe);

        // Act
        await _sut.AktivenLaufBeendenAsync(aufgabe.Id);

        // Assert
        var geladen = await _sut.GetByIdAsync(aufgabe.Id);
        geladen!.LaufStatus.Should().BeNull();
    }

    /// <summary>
    /// TOCTOU-Guard (Issue 342): Meldet der <c>laeuftGuard</c> beim Eintritt in die serialisierte
    /// Schreibphase, dass der Prozess nicht mehr läuft (z. B. weil er zwischen dem Aufruf-Check und
    /// dem Eintritt in die Aufgabensperre beendet wurde), darf kein Phantom-Lauf persistiert werden.
    /// </summary>
    [Fact]
    public async Task AktivenLaufSicherstellenAsync_SchreibtNichts_WennLaeuftGuardNichtMehrLaeuft()
    {
        // Arrange
        var aufgabe = await _sut.CreateAsync(_projektId, "Guard-Aufgabe", null);

        // Act
        await _sut.AktivenLaufSicherstellenAsync(aufgabe.Id, laeuftGuard: () => false);

        // Assert
        var geladen = await _sut.GetByIdAsync(aufgabe.Id);
        geladen!.AktiveRunId.Should().BeNull();
        geladen.LaufStatus.Should().BeNull();
        geladen.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.NichtGestartet);
    }

    /// <summary>
    /// Die Laufdaten-Schreibpfade sind pro Aufgabe serialisiert (Issue 342, TOCTOU-Fix): Ein Beenden,
    /// das hinter einer noch laufenden Schreibphase eintrifft, muss an der Aufgabensperre warten —
    /// damit steht die Reihenfolge der Schreiboperationen fest und ein zuvor ausgeführter Beenden-Save
    /// kann nicht mehr durch einen nachfolgenden Sicherstellen-Save wiederbelebt werden.
    /// </summary>
    [Fact]
    public async Task LaufdatenSchreibpfade_SerialisierenProAufgabe()
    {
        // Arrange: eine Schreiboperation muss einmal gelaufen sein, damit der Gate-Eintrag existiert.
        var aufgabe = await _sut.CreateAsync(_projektId, "Serialisierungs-Aufgabe", null);
        await _sut.AktivenLaufSetzenAsync(aufgabe.Id, "lauf-ser");

        var gates = (ConcurrentDictionary<Guid, SemaphoreSlim>)typeof(AufgabeService)
            .GetField("LaufGates", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var gate = gates[aufgabe.Id];
        await gate.WaitAsync();

        Task beendenTask;
        try
        {
            // Act
            beendenTask = _sut.AktivenLaufBeendenAsync(aufgabe.Id);
            await Task.Delay(200);

            // Assert
            beendenTask.IsCompleted.Should().BeFalse(
                "der Beenden-Pfad muss hinter der gehaltenen Aufgabensperre warten");
        }
        finally
        {
            gate.Release();
        }

        // Nach Freigabe der Sperre muss der Beenden-Pfad fortschreiten und vollenden.
        await beendenTask;
        (await _sut.GetByIdAsync(aufgabe.Id))!.AktiveRunId.Should().BeNull();
    }

    /// <summary>
    /// Der laufStatusProvider wird — analog zum laeuftGuard — erst innerhalb der serialisierten
    /// Schreibphase ausgewertet (Issue 342, Stale-Write-Fix): Erst beim Eintritt in die
    /// Aufgabensperre darf der Live-Substatus gelesen werden, damit beim Schreiben der frischeste
    /// Session-Stand gilt und nicht ein vor dem Sperreintritt gelesener, veralteter Wert.
    /// </summary>
    [Fact]
    public async Task AktivenLaufSicherstellenAsync_WertetLaufStatusProvider_InDerSperreAus()
    {
        // Arrange: Schreiboperation einmal laufen lassen, damit der Gate-Eintrag existiert.
        var aufgabe = await _sut.CreateAsync(_projektId, "Provider-Aufgabe", null);
        await _sut.AktivenLaufSetzenAsync(aufgabe.Id, "lauf-prov");

        var gates = (ConcurrentDictionary<Guid, SemaphoreSlim>)typeof(AufgabeService)
            .GetField("LaufGates", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var gate = gates[aufgabe.Id];
        await gate.WaitAsync();

        var providerAusgewertet = false;
        Task sicherstellenTask;
        try
        {
            // Act
            sicherstellenTask = _sut.AktivenLaufSicherstellenAsync(
                aufgabe.Id,
                laufStatusProvider: () =>
                {
                    providerAusgewertet = true;
                    return AufgabeLaufStatus.WartetAufEingabe;
                });
            await Task.Delay(200);

            // Assert: Solange die Aufgabensperre gehalten wird, darf der Provider nicht
            // ausgewertet worden sein — ein früher gelesener Wert wäre zum Schreibzeitpunkt
            // möglicherweise veraltet.
            providerAusgewertet.Should().BeFalse(
                "der Substatus darf erst innerhalb der Aufgabensperre gelesen werden");
        }
        finally
        {
            gate.Release();
        }

        await sicherstellenTask;
        providerAusgewertet.Should().BeTrue();
        (await _sut.GetByIdAsync(aufgabe.Id))!.LaufStatus.Should().Be(AufgabeLaufStatus.WartetAufEingabe);
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
        await _sut.AktivenLaufSicherstellenAsync(aufgabe.Id, () => AufgabeLaufStatus.WartetAufEingabe);
        var waehrendWarten = await _sut.GetByIdAsync(aufgabe.Id);
        waehrendWarten!.AktiveRunId.Should().NotBeNull("der Prozess lebt während des Wartens weiterhin");
        waehrendWarten.LaufStatus.Should().Be(AufgabeLaufStatus.WartetAufEingabe);

        // Bereit: CLI-Prozess beendet.
        await _sut.AktivenLaufBeendenAsync(aufgabe.Id);
        var nachStopp = await _sut.GetByIdAsync(aufgabe.Id);
        nachStopp!.AktiveRunId.Should().BeNull();
        nachStopp.LaufStatus.Should().BeNull();
        nachStopp.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.Beendet);
    }

    /// <summary>
    /// AktivenLaufSicherstellenAsync stellt bei fehlender AktiveRunId den vollständigen Lauf-Zustand
    /// her (AusfuehrungsStatus Aktiv, neue AktiveRunId, LastHeartbeatUtc, LaufStatus) — die zentrale
    /// Selbstheilung für Läufe ohne Gestartet-Persistierung (Issue 342).
    /// </summary>
    [Fact]
    public async Task AktivenLaufSicherstellenAsync_SetztVolleLaufdaten_BeiFehlenderAktiveRunId()
    {
        var aufgabe = await _sut.CreateAsync(_projektId, "Sicherstellen-Aufgabe", null);
        aufgabe.AktiveRunId.Should().BeNull();

        var vorUpdate = DateTimeOffset.UtcNow;
        await _sut.AktivenLaufSicherstellenAsync(aufgabe.Id);
        var nachUpdate = DateTimeOffset.UtcNow;

        var geladen = await _sut.GetByIdAsync(aufgabe.Id);
        geladen!.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.Aktiv);
        geladen.AktiveRunId.Should().NotBeNullOrEmpty();
        geladen.LastHeartbeatUtc.Should().NotBeNull();
        geladen.LastHeartbeatUtc!.Value.Should().BeOnOrAfter(vorUpdate.AddSeconds(-1));
        geladen.LastHeartbeatUtc!.Value.Should().BeOnOrBefore(nachUpdate.AddSeconds(1));
        geladen.LetzterCliStartUtc.Should().NotBeNull();
        geladen.LaufStatus.Should().Be(AufgabeLaufStatus.Laeuft,
            "ohne übergebenen Substatus startet ein neuer Lauf mit Laeuft");
    }

    /// <summary>Ein explizit übergebener Lauf-Substatus wird beim Anlegen der Laufdaten übernommen.</summary>
    [Fact]
    public async Task AktivenLaufSicherstellenAsync_UebernimmtLaufStatus_BeiFehlenderAktiveRunId()
    {
        var aufgabe = await _sut.CreateAsync(_projektId, "Sicherstellen-Substatus-Aufgabe", null);

        await _sut.AktivenLaufSicherstellenAsync(aufgabe.Id, () => AufgabeLaufStatus.WartetAufEingabe);

        var geladen = await _sut.GetByIdAsync(aufgabe.Id);
        geladen!.AktiveRunId.Should().NotBeNullOrEmpty();
        geladen.LaufStatus.Should().Be(AufgabeLaufStatus.WartetAufEingabe);
    }

    /// <summary>
    /// Idempotenz: Bei bereits vorhandener AktiveRunId bleiben AktiveRunId und LetzterCliStartUtc
    /// erhalten; nur der Heartbeat wird aufgefrischt und der Substatus nur bei übergebenem Parameter
    /// geändert.
    /// </summary>
    [Fact]
    public async Task AktivenLaufSicherstellenAsync_BehaeltAktiveRunId_UndFrischtHeartbeat_BeiBestehendemLauf()
    {
        var aufgabe = await _sut.CreateAsync(_projektId, "Idempotenz-Aufgabe", null);
        await _sut.AktivenLaufSetzenAsync(aufgabe.Id, "lauf-bestehend");

        var tracked = await _db.Aufgaben.FindAsync(aufgabe.Id);
        var bekannterStart = DateTimeOffset.UtcNow.AddHours(-2);
        tracked!.LetzterCliStartUtc = bekannterStart;
        tracked.LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-3);
        tracked.LaufStatus = AufgabeLaufStatus.WartetAufEingabe;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var vorUpdate = DateTimeOffset.UtcNow;
        await _sut.AktivenLaufSicherstellenAsync(aufgabe.Id);

        var geladen = await _sut.GetByIdAsync(aufgabe.Id);
        geladen!.AktiveRunId.Should().Be("lauf-bestehend");
        geladen.LetzterCliStartUtc.Should().BeCloseTo(bekannterStart, TimeSpan.FromMilliseconds(2),
            "ein bekannter echter Startzeitpunkt darf nicht durch die Heilung überschrieben werden");
        geladen.LastHeartbeatUtc!.Value.Should().BeOnOrAfter(vorUpdate.AddSeconds(-1));
        geladen.LaufStatus.Should().Be(AufgabeLaufStatus.WartetAufEingabe,
            "ohne Parameter bleibt der bestehende Substatus erhalten");

        await _sut.AktivenLaufSicherstellenAsync(aufgabe.Id, () => AufgabeLaufStatus.Laeuft);
        (await _sut.GetByIdAsync(aufgabe.Id))!.LaufStatus.Should().Be(AufgabeLaufStatus.Laeuft,
            "mit Parameter wird der Substatus aktualisiert");
    }

    /// <summary>Terminale Aufgaben (Beendet/Archiviert) werden ohne Änderung und ohne Wurf übersprungen.</summary>
    [Fact]
    public async Task AktivenLaufSicherstellenAsync_SkipptOhneWurf_BeiTerminalemStatus()
    {
        var aufgabe = await _sut.CreateAsync(_projektId, "Terminale-Aufgabe", null);
        var tracked = await _db.Aufgaben.FindAsync(aufgabe.Id);
        tracked!.Status = AufgabeStatus.Beendet;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var act = () => _sut.AktivenLaufSicherstellenAsync(aufgabe.Id);

        await act.Should().NotThrowAsync();
        var geladen = await _sut.GetByIdAsync(aufgabe.Id);
        geladen!.AktiveRunId.Should().BeNull();
        geladen.AusfuehrungsStatus.Should().Be(AufgabeAusfuehrungsStatus.NichtGestartet);
    }

    /// <summary>Konsistent zu den Geschwistermethoden wirft die Sicherstellung bei unbekannter Aufgabe.</summary>
    [Fact]
    public async Task AktivenLaufSicherstellenAsync_Wirft_BeiUnbekannterAufgabe()
    {
        var act = () => _sut.AktivenLaufSicherstellenAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
