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
/// Tests für die Verwaltung eines aktiven CLI-Laufs auf <see cref="WorkItemService"/>
/// (<see cref="WorkItemService.SetActiveRunAsync"/>, <see cref="WorkItemService.EndActiveRunAsync"/>,
/// <see cref="WorkItemService.EnsureActiveRunAsync"/>). Deckt den vollständigen Substatus-Zyklus
/// Bereit → Läuft → Wartet (während der Prozess noch lebt) → Bereit (nach Stopp) aus Sicht des Services ab
/// (Issue 108, Folgefehler des Rückwegs Läuft → Wartet: <see cref="Softwareschmiede.Domain.Entities.WorkItem.RunStatus"/>
/// bildet den bis dahin nirgends persistierten Laufzeit-Substatus der CLI ab).
/// </summary>
public sealed class AufgabeServiceTests_AktiverLauf : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly Mock<ILogger<WorkItemService>> _loggerMock;
    private readonly WorkItemService _sut;
    private readonly Guid _projectId = new Guid("22222222-2222-2222-2222-222222222222");

    /// <summary>AufgabeServiceTests_AktiverLauf.</summary>
    public AufgabeServiceTests_AktiverLauf()
    {
        _db = TestDbContextFactory.Create();
        _loggerMock = new Mock<ILogger<WorkItemService>>();
        _sut = new WorkItemService(_db, _loggerMock.Object, new TodoService(_db, new Mock<ILogger<TodoService>>().Object));

        _db.Projects.Add(new Softwareschmiede.Domain.Entities.Project
        {
            Id = _projectId,
            Name = "Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
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
        var workItem = await _sut.CreateAsync(_projectId, "Aktiver-Lauf-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        workItem.ActiveRunId.Should().BeNull();

        // Act
        var vorUpdate = DateTimeOffset.UtcNow;
        await _sut.SetActiveRunAsync(workItem.Id, "lauf-123", TestContext.Current.CancellationToken);
        var nachUpdate = DateTimeOffset.UtcNow;

        // Assert
        var geladen = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.ActiveRunId.Should().Be("lauf-123");
        geladen.LastHeartbeatUtc.Should().NotBeNull();
        geladen.LastHeartbeatUtc!.Value.Should().BeOnOrAfter(vorUpdate.AddSeconds(-1));
        geladen.LastHeartbeatUtc!.Value.Should().BeOnOrBefore(nachUpdate.AddSeconds(1));
        geladen.LastCliStartUtc.Should().NotBeNull();
        geladen.LastCliStartUtc!.Value.Should().BeOnOrAfter(vorUpdate.AddSeconds(-1));
        geladen.LastCliStartUtc!.Value.Should().BeOnOrBefore(nachUpdate.AddSeconds(1));
        geladen.RunStatus.Should().Be(WorkItemRunStatus.Running);
        geladen.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Active);
    }

    /// <summary>AktivenLaufBeendenAsync entfernt eine zuvor gesetzte AktiveRunId und den LaufStatus.</summary>
    [Fact]
    public async Task AktivenLaufBeendenAsync_ShouldClearAktiveRunId_WhenPreviouslySet()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projectId, "Aktiver-Lauf-Beenden-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _sut.SetActiveRunAsync(workItem.Id, "lauf-456", TestContext.Current.CancellationToken);
        (await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken))!.ActiveRunId.Should().Be("lauf-456");

        // Act
        await _sut.EndActiveRunAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        var geladen = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.ActiveRunId.Should().BeNull();
        geladen.LastHeartbeatUtc.Should().BeNull();
        geladen.LastCliStartUtc.Should().BeNull();
        geladen.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Completed);
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
        var workItem = await _sut.CreateAsync(_projectId, "Aktiver-Lauf-Beenden-LaufStatus-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _sut.SetActiveRunAsync(workItem.Id, "lauf-789", TestContext.Current.CancellationToken);
        await _sut.EnsureActiveRunAsync(workItem.Id, () => WorkItemRunStatus.WaitingForInput, TestContext.Current.CancellationToken);
        (await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken))!.RunStatus.Should().Be(WorkItemRunStatus.WaitingForInput);

        // Act
        await _sut.EndActiveRunAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        var geladen = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.RunStatus.Should().BeNull();
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
        var workItem = await _sut.CreateAsync(_projectId, "Guard-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        await _sut.EnsureActiveRunAsync(workItem.Id, runningGuard: () => false, ct: TestContext.Current.CancellationToken);

        // Assert
        var geladen = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.ActiveRunId.Should().BeNull();
        geladen.RunStatus.Should().BeNull();
        geladen.ExecutionStatus.Should().Be(WorkItemExecutionStatus.NotStarted);
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
        var workItem = await _sut.CreateAsync(_projectId, "Serialisierungs-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _sut.SetActiveRunAsync(workItem.Id, "lauf-ser", TestContext.Current.CancellationToken);

        var gates = (ConcurrentDictionary<Guid, SemaphoreSlim>)typeof(WorkItemService)
            .GetField("RunGates", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var gate = gates[workItem.Id];
        await gate.WaitAsync(TestContext.Current.CancellationToken);

        Task beendenTask;
        try
        {
            // Act
            beendenTask = _sut.EndActiveRunAsync(workItem.Id, TestContext.Current.CancellationToken);
            await Task.Delay(200, TestContext.Current.CancellationToken);

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
        (await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken))!.ActiveRunId.Should().BeNull();
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
        var workItem = await _sut.CreateAsync(_projectId, "Provider-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _sut.SetActiveRunAsync(workItem.Id, "lauf-prov", TestContext.Current.CancellationToken);

        var gates = (ConcurrentDictionary<Guid, SemaphoreSlim>)typeof(WorkItemService)
            .GetField("RunGates", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var gate = gates[workItem.Id];
        await gate.WaitAsync(TestContext.Current.CancellationToken);

        var providerAusgewertet = false;
        Task sicherstellenTask;
        try
        {
            // Act
            sicherstellenTask = _sut.EnsureActiveRunAsync(workItem.Id, runStatusProvider: () =>
                {
                    providerAusgewertet = true;
                    return WorkItemRunStatus.WaitingForInput;
                }, ct: TestContext.Current.CancellationToken);
            await Task.Delay(200, TestContext.Current.CancellationToken);

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
        (await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken))!.RunStatus.Should().Be(WorkItemRunStatus.WaitingForInput);
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
        var workItem = await _sut.CreateAsync(_projectId, "Voller-Zyklus-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        workItem.ActiveRunId.Should().BeNull();
        workItem.RunStatus.Should().BeNull();

        // Läuft: CLI-Prozess gestartet.
        await _sut.SetActiveRunAsync(workItem.Id, "lauf-zyklus-1", TestContext.Current.CancellationToken);
        var nachStart = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        nachStart!.ActiveRunId.Should().NotBeNull();
        nachStart.RunStatus.Should().Be(WorkItemRunStatus.Running);

        // Wartet: CLI erzeugt seit längerer Zeit keine Ausgabe mehr (Prozess lebt weiterhin).
        await _sut.EnsureActiveRunAsync(workItem.Id, () => WorkItemRunStatus.WaitingForInput, TestContext.Current.CancellationToken);
        var waehrendWarten = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        waehrendWarten!.ActiveRunId.Should().NotBeNull("der Prozess lebt während des Wartens weiterhin");
        waehrendWarten.RunStatus.Should().Be(WorkItemRunStatus.WaitingForInput);

        // Bereit: CLI-Prozess beendet.
        await _sut.EndActiveRunAsync(workItem.Id, TestContext.Current.CancellationToken);
        var nachStopp = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        nachStopp!.ActiveRunId.Should().BeNull();
        nachStopp.RunStatus.Should().BeNull();
        nachStopp.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Completed);
    }

    /// <summary>
    /// AktivenLaufSicherstellenAsync stellt bei fehlender AktiveRunId den vollständigen Lauf-Zustand
    /// her (AusfuehrungsStatus Aktiv, neue AktiveRunId, LastHeartbeatUtc, LaufStatus) — die zentrale
    /// Selbstheilung für Läufe ohne Gestartet-Persistierung (Issue 342).
    /// </summary>
    [Fact]
    public async Task AktivenLaufSicherstellenAsync_SetztVolleLaufdaten_BeiFehlenderAktiveRunId()
    {
        var workItem = await _sut.CreateAsync(_projectId, "Sicherstellen-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        workItem.ActiveRunId.Should().BeNull();

        var vorUpdate = DateTimeOffset.UtcNow;
        await _sut.EnsureActiveRunAsync(workItem.Id, ct: TestContext.Current.CancellationToken);
        var nachUpdate = DateTimeOffset.UtcNow;

        var geladen = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Active);
        geladen.ActiveRunId.Should().NotBeNullOrEmpty();
        geladen.LastHeartbeatUtc.Should().NotBeNull();
        geladen.LastHeartbeatUtc!.Value.Should().BeOnOrAfter(vorUpdate.AddSeconds(-1));
        geladen.LastHeartbeatUtc!.Value.Should().BeOnOrBefore(nachUpdate.AddSeconds(1));
        geladen.LastCliStartUtc.Should().NotBeNull();
        geladen.RunStatus.Should().Be(WorkItemRunStatus.Running,
            "ohne übergebenen Substatus startet ein neuer Lauf mit Laeuft");
    }

    /// <summary>Ein explizit übergebener Lauf-Substatus wird beim Anlegen der Laufdaten übernommen.</summary>
    [Fact]
    public async Task AktivenLaufSicherstellenAsync_UebernimmtLaufStatus_BeiFehlenderAktiveRunId()
    {
        var workItem = await _sut.CreateAsync(_projectId, "Sicherstellen-Substatus-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        await _sut.EnsureActiveRunAsync(workItem.Id, () => WorkItemRunStatus.WaitingForInput, TestContext.Current.CancellationToken);

        var geladen = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.ActiveRunId.Should().NotBeNullOrEmpty();
        geladen.RunStatus.Should().Be(WorkItemRunStatus.WaitingForInput);
    }

    /// <summary>
    /// Idempotenz: Bei bereits vorhandener AktiveRunId bleiben AktiveRunId und LetzterCliStartUtc
    /// erhalten; nur der Heartbeat wird aufgefrischt und der Substatus nur bei übergebenem Parameter
    /// geändert.
    /// </summary>
    [Fact]
    public async Task AktivenLaufSicherstellenAsync_BehaeltAktiveRunId_UndFrischtHeartbeat_BeiBestehendemLauf()
    {
        var workItem = await _sut.CreateAsync(_projectId, "Idempotenz-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _sut.SetActiveRunAsync(workItem.Id, "lauf-bestehend", TestContext.Current.CancellationToken);

        var tracked = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        var bekannterStart = DateTimeOffset.UtcNow.AddHours(-2);
        tracked!.LastCliStartUtc = bekannterStart;
        tracked.LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-3);
        tracked.RunStatus = WorkItemRunStatus.WaitingForInput;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();

        var vorUpdate = DateTimeOffset.UtcNow;
        await _sut.EnsureActiveRunAsync(workItem.Id, ct: TestContext.Current.CancellationToken);

        var geladen = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.ActiveRunId.Should().Be("lauf-bestehend");
        geladen.LastCliStartUtc.Should().BeCloseTo(bekannterStart, TimeSpan.FromMilliseconds(2),
            "ein bekannter echter Startzeitpunkt darf nicht durch die Heilung überschrieben werden");
        geladen.LastHeartbeatUtc!.Value.Should().BeOnOrAfter(vorUpdate.AddSeconds(-1));
        geladen.RunStatus.Should().Be(WorkItemRunStatus.WaitingForInput,
            "ohne Parameter bleibt der bestehende Substatus erhalten");

        await _sut.EnsureActiveRunAsync(workItem.Id, () => WorkItemRunStatus.Running, TestContext.Current.CancellationToken);
        (await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken))!.RunStatus.Should().Be(WorkItemRunStatus.Running,
            "mit Parameter wird der Substatus aktualisiert");
    }

    /// <summary>Terminale Aufgaben (Beendet/Archiviert) werden ohne Änderung und ohne Wurf übersprungen.</summary>
    [Fact]
    public async Task AktivenLaufSicherstellenAsync_SkipptOhneWurf_BeiTerminalemStatus()
    {
        var workItem = await _sut.CreateAsync(_projectId, "Terminale-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var tracked = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        tracked!.Status = WorkItemStatus.Completed;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();

        var act = () => _sut.EnsureActiveRunAsync(workItem.Id);

        await act.Should().NotThrowAsync();
        var geladen = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.ActiveRunId.Should().BeNull();
        geladen.ExecutionStatus.Should().Be(WorkItemExecutionStatus.NotStarted);
    }

    /// <summary>Konsistent zu den Geschwistermethoden wirft die Sicherstellung bei unbekannter Aufgabe.</summary>
    [Fact]
    public async Task AktivenLaufSicherstellenAsync_Wirft_BeiUnbekannterAufgabe()
    {
        var act = () => _sut.EnsureActiveRunAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
