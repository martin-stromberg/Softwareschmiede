using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.IntegrationTests.Infrastructure;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Integrationstests für <see cref="AufgabeService"/> mit echter SQLite-Datenbank.</summary>
public sealed class AufgabeServiceTests
{
    /// <summary>Hilfsmethode: erstellt ein Testprojekt direkt über den DbContext.</summary>
    private static async Task<Guid> CreateTestProjektAsync(DatabaseFixture db)
    {
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var projekt = await service.CreateAsync("Integrationstestprojekt", null);
        return projekt.Id;
    }

    /// <summary>
    /// Testet, dass CreateAsync eine Aufgabe anlegt und mit einem neuen Context wieder geladen werden kann.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldPersistAufgabe_WhenValidDataGiven()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);

        // Act
        var created = await service.CreateAsync(projektId, "Test-Aufgabe", "Anforderungsbeschreibung");

        // Assert – Persistenz via neuem Context prüfen
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Aufgaben.FindAsync(created.Id);

        loaded.Should().NotBeNull();
        loaded!.Titel.Should().Be("Test-Aufgabe");
        loaded.AnforderungsBeschreibung.Should().Be("Anforderungsbeschreibung");
        loaded.Status.Should().Be(AufgabeStatus.Neu);
        loaded.ProjektId.Should().Be(projektId);
    }

    /// <summary>
    /// Testet, dass StartenAsync den Status auf InBearbeitung setzt und Branch/Pfad persistiert.
    /// </summary>
    [Fact]
    public async Task StartenAsync_ShouldSetStatusInBearbeitungAndPersistBranchInfo_WhenAufgabeExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);
        var aufgabe = await service.CreateAsync(projektId, "Startbare Aufgabe", null);

        // Act
        await service.StartenAsync(aufgabe.Id, "task/feature-branch", @"C:\klone\aufgabe");

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Aufgaben.FindAsync(aufgabe.Id);

        loaded!.Status.Should().Be(AufgabeStatus.Gestartet);
        loaded.BranchName.Should().Be("task/feature-branch");
        loaded.LokalerKlonPfad.Should().Be(@"C:\klone\aufgabe");
    }

    /// <summary>
    /// Testet, dass AbschliessenAsync den Status auf Abgeschlossen setzt und das AbschlussDatum persistiert.
    /// </summary>
    [Fact]
    public async Task AbschliessenAsync_ShouldSetStatusAbgeschlossenAndClearBranchInfo_WhenAufgabeInBearbeitung()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);
        var aufgabe = await service.CreateAsync(projektId, "Abzuschließende Aufgabe", null);
        await service.StartenAsync(aufgabe.Id, "task/fertig", @"C:\klone\fertig");

        var vorAbschluss = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        await service.AbschliessenAsync(aufgabe.Id);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Aufgaben.FindAsync(aufgabe.Id);

        loaded!.Status.Should().Be(AufgabeStatus.Beendet);
        loaded.AbschlussDatum.Should().NotBeNull();
        // Toleranz von 2 Sekunden, da DateTimeOffset als Unix-Millisekunden gespeichert wird (Präzisionsverlust < 1 ms)
        loaded.AbschlussDatum!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        loaded.BranchName.Should().BeNull();
        loaded.LokalerKlonPfad.Should().BeNull();
    }

    /// <summary>
    /// Testet, dass GetDetailAsync Protokolleinträge und TestErgebnisse per Include lädt.
    /// </summary>
    [Fact]
    public async Task GetDetailAsync_ShouldIncludeProtokolleintraegeWithTestErgebnisse_WhenDataExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);
        var protokollService = new ProtokollService(db.Context, NullLogger<ProtokollService>.Instance);

        var aufgabe = await aufgabeService.CreateAsync(projektId, "Aufgabe mit Protokoll", null);
        await protokollService.AddEintragAsync(aufgabe.Id, ProtokollTyp.Prompt, "Starte Analyse", "TestAgent");

        var testResult = new TestResult(
            true,
            new[]
            {
                new TestErgebnisInfo("Test1", TestStatus.Bestanden, null, TimeSpan.FromMilliseconds(42))
            });
        await protokollService.AddTestErgebnisseAsync(aufgabe.Id, testResult);

        // Act
        var detail = await aufgabeService.GetDetailAsync(aufgabe.Id);

        // Assert
        detail.Should().NotBeNull();
        detail!.Protokolleintraege.Should().HaveCount(2);

        var testEintrag = detail.Protokolleintraege.Single(p => p.Typ == ProtokollTyp.TestErgebnis);
        testEintrag.TestErgebnisse.Should().HaveCount(1);
        testEintrag.TestErgebnisse[0].TestName.Should().Be("Test1");
    }

    /// <summary>
    /// Testet, dass DeleteAsync eine Aufgabe und ihre Protokolleinträge kaskadierend löscht.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ShouldCascadeDeleteProtokolleintraege_WhenAufgabeDeleted()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);
        var protokollService = new ProtokollService(db.Context, NullLogger<ProtokollService>.Instance);

        var aufgabe = await aufgabeService.CreateAsync(projektId, "Zu löschende Aufgabe", null);
        await protokollService.AddEintragAsync(aufgabe.Id, ProtokollTyp.Prompt, "Irgendein Prompt");
        await protokollService.AddEintragAsync(aufgabe.Id, ProtokollTyp.KiAntwort, "Irgendeine Antwort");

        // Act
        await aufgabeService.DeleteAsync(aufgabe.Id);

        // Assert
        await using var db2 = db.CreateNewContext();
        var geladen = await db2.Aufgaben.FindAsync(aufgabe.Id);
        var protokolle = db2.Protokolleintraege.Where(p => p.AufgabeId == aufgabe.Id).ToList();

        geladen.Should().BeNull();
        protokolle.Should().BeEmpty();
    }

    /// <summary>
    /// Testet, dass VerwerfenAsync eine offene Aufgabe archiviert und die Änderung persistiert.
    /// </summary>
    [Fact]
    public async Task VerwerfenAsync_ShouldPersistArchivedStatus_WhenOffeneAufgabeArchiviertWird()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);
        var aufgabe = await service.CreateAsync(projektId, "Offene Aufgabe", null);

        // Act
        await service.VerwerfenAsync(aufgabe.Id, VerwerfenAktion.Archivieren);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Aufgaben.FindAsync(aufgabe.Id);

        loaded!.Status.Should().Be(AufgabeStatus.Archiviert);
    }

    /// <summary>
    /// Testet, dass VerwerfenAsync eine offene Aufgabe dauerhaft löscht und kaskadierend entfernt.
    /// </summary>
    [Fact]
    public async Task VerwerfenAsync_ShouldCascadeDeleteProtokolleintraege_WhenOffeneAufgabeGeloeschtWird()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);
        var protokollService = new ProtokollService(db.Context, NullLogger<ProtokollService>.Instance);

        var aufgabe = await aufgabeService.CreateAsync(projektId, "Offene Aufgabe", null);
        await protokollService.AddEintragAsync(aufgabe.Id, ProtokollTyp.Prompt, "Ein Prompt");

        // Act
        await aufgabeService.VerwerfenAsync(aufgabe.Id, VerwerfenAktion.Loeschen);

        // Assert
        await using var db2 = db.CreateNewContext();
        var geladen = await db2.Aufgaben.FindAsync(aufgabe.Id);
        var protokolle = db2.Protokolleintraege.Where(p => p.AufgabeId == aufgabe.Id).ToList();

        geladen.Should().BeNull();
        protokolle.Should().BeEmpty();
    }

    /// <summary>
    /// Testet, dass VerwerfenAsync eine bereits gestartete Aufgabe für beide Verwerfungsaktionen ablehnt.
    /// </summary>
    [Theory]
    [InlineData(VerwerfenAktion.Archivieren)]
    [InlineData(VerwerfenAktion.Loeschen)]
    public async Task VerwerfenAsync_ShouldThrowWhenAufgabeIsNotOffen(VerwerfenAktion aktion)
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);
        var aufgabe = await service.CreateAsync(projektId, "Gestartete Aufgabe", null);
        await service.StartenAsync(aufgabe.Id, "task/test", @"C:\klone\test");

        // Act
        var act = () => service.VerwerfenAsync(aufgabe.Id, aktion);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Nur neue Aufgaben können verworfen werden.");
    }

    /// <summary>
    /// Testet, dass CreateFromIssueAsync eine Aufgabe mit IssueReferenz anlegt.
    /// </summary>
    [Fact]
    public async Task CreateFromIssueAsync_ShouldPersistAufgabeWithIssueReferenz_WhenIssueGiven()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);

        var issue = new Issue(
            Nummer: 42,
            Titel: "Bug: Crash beim Start",
            Body: "Detaillierte Beschreibung",
            Labels: new[] { "bug", "critical" },
            Milestone: "v1.0",
            IssueUrl: "https://github.com/test/repo/issues/42");

        // Act
        var aufgabe = await service.CreateFromIssueAsync(projektId, issue);

        // Assert – IssueReferenz via Detail-Abfrage laden
        var detail = await service.GetDetailAsync(aufgabe.Id);

        detail!.IssueReferenz.Should().NotBeNull();
        detail.IssueReferenz!.IssueNummer.Should().Be(42);
        detail.IssueReferenz.Titel.Should().Be("Bug: Crash beim Start");
        detail.IssueReferenz.Milestone.Should().Be("v1.0");
    }

    /// <summary>
    /// Testet, dass GetByProjektAsync alle Aufgaben eines Projekts absteigend nach Erstellungsdatum zurückgibt.
    /// </summary>
    [Fact]
    public async Task GetByProjektAsync_ShouldReturnAufgabenDescendingByDate_WhenMultipleAufgabenExist()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);

        await service.CreateAsync(projektId, "Erste Aufgabe", null);
        await Task.Delay(5); // sicherstellen, dass ErstellungsDatum unterschiedlich ist
        await service.CreateAsync(projektId, "Zweite Aufgabe", null);
        await Task.Delay(5);
        await service.CreateAsync(projektId, "Dritte Aufgabe", null);

        // Act
        var result = await service.GetByProjektAsync(projektId);

        // Assert
        result.Should().HaveCount(3);
        result[0].Titel.Should().Be("Dritte Aufgabe");
        result[2].Titel.Should().Be("Erste Aufgabe");
    }

    /// <summary>
    /// Testet, dass SetStatusAsync den Status auf Beendet setzt.
    /// </summary>
    [Fact]
    public async Task SetStatusAsync_ShouldSetStatusBeendet_WhenAufgabeIsGestartet()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);
        var aufgabe = await service.CreateAsync(projektId, "Aufgabe für Statuswechsel", null);
        await service.StatusSetzenAsync(aufgabe.Id, AufgabeStatus.Gestartet);

        // Act
        await service.SetStatusAsync(aufgabe.Id, AufgabeStatus.Beendet);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Aufgaben.FindAsync(aufgabe.Id);

        loaded!.Status.Should().Be(AufgabeStatus.Beendet);
    }

    /// <summary>
    /// Testet, dass GetAktiveAufgabenAsync gegen eine echte SQLite-Datenbank absteigend nach
    /// LetzterCliStartUtc (Fallback ErstellungsDatum) sortiert. Regressionstest für die
    /// COALESCE-basierte Sortierung, die bei der reinen InMemory-Provider-Testabdeckung
    /// nicht gegen die tatsächliche SQL-Übersetzung geprüft wird.
    /// </summary>
    [Fact]
    public async Task GetAktiveAufgabenAsync_ShouldSortByLetzterCliStartDescThenByErstellungsDatum_WhenUsingSqlite()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);

        var jetzt = DateTimeOffset.UtcNow;

        var mitAltemStart = new Softwareschmiede.Domain.Entities.Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projektId,
            Titel = "Alter Start",
            Status = AufgabeStatus.Gestartet,
            ErstellungsDatum = jetzt.AddHours(-3),
            LastHeartbeatUtc = jetzt,
            LetzterCliStartUtc = jetzt.AddMinutes(-10)
        };
        var mitNeuemStart = new Softwareschmiede.Domain.Entities.Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projektId,
            Titel = "Neuer Start",
            Status = AufgabeStatus.Wartend,
            ErstellungsDatum = jetzt.AddHours(-2),
            LastHeartbeatUtc = jetzt.AddHours(-1),
            LetzterCliStartUtc = jetzt.AddMinutes(-1)
        };
        var ohneStart = new Softwareschmiede.Domain.Entities.Aufgabe
        {
            Id = Guid.NewGuid(),
            ProjektId = projektId,
            Titel = "Ohne Start",
            Status = AufgabeStatus.Gestartet,
            ErstellungsDatum = jetzt.AddMinutes(-5),
            LastHeartbeatUtc = null,
            LetzterCliStartUtc = null
        };

        db.Context.Aufgaben.AddRange(mitAltemStart, mitNeuemStart, ohneStart);
        await db.Context.SaveChangesAsync();

        // Act
        var result = await service.GetAktiveAufgabenAsync();

        // Assert
        result.Select(a => a.Id).Should().ContainInOrder(mitNeuemStart.Id, ohneStart.Id, mitAltemStart.Id);
    }

    /// <summary>
    /// Testet, dass UpdateAsync Titel, Beschreibung und Agenteninformationen persistiert.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldPersistAllFields_WhenAufgabeExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var projektId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);
        var aufgabe = await service.CreateAsync(projektId, "Ursprünglicher Titel", "Alt");

        // Act
        await service.UpdateAsync(aufgabe.Id, "Neuer Titel", "Neue Anforderung", "copilot");

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Aufgaben.FindAsync(aufgabe.Id);

        loaded!.Titel.Should().Be("Neuer Titel");
        loaded.AnforderungsBeschreibung.Should().Be("Neue Anforderung");
        loaded.KiPluginPrefix.Should().Be("copilot");
    }
}
