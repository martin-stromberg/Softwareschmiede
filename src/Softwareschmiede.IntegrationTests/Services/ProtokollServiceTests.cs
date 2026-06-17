using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.IntegrationTests.Infrastructure;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Integrationstests für <see cref="ProtokollService"/> mit echter SQLite-Datenbank.</summary>
public sealed class ProtokollServiceTests
{
    /// <summary>Hilfsmethode: Erstellt Testprojekt und Testaufgabe, gibt Aufgabe-ID zurück.</summary>
    private static async Task<Guid> CreateTestAufgabeAsync(DatabaseFixture db)
    {
        var projektService = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance);

        var projekt = await projektService.CreateAsync("Testprojekt Protokoll", null);
        var aufgabe = await aufgabeService.CreateAsync(projekt.Id, "Testaufgabe Protokoll", null);
        return aufgabe.Id;
    }

    /// <summary>
    /// Testet, dass AddEintragAsync einen Protokolleintrag speichert und mit neuem Context geladen werden kann.
    /// </summary>
    [Fact]
    public async Task AddEintragAsync_ShouldPersistProtokollEintrag_WhenValidDataGiven()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var aufgabeId = await CreateTestAufgabeAsync(db);
        var service = new ProtokollService(db.Context, NullLogger<ProtokollService>.Instance);

        // Act
        var eintrag = await service.AddEintragAsync(
            aufgabeId,
            ProtokollTyp.Prompt,
            "Analysiere den Code und erstelle Tests.",
            "TestAgent");

        // Assert – Persistenz via neuem Context prüfen
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Protokolleintraege.FindAsync(eintrag.Id);

        loaded.Should().NotBeNull();
        loaded!.Inhalt.Should().Be("Analysiere den Code und erstelle Tests.");
        loaded.Typ.Should().Be(ProtokollTyp.Prompt);
        loaded.AgentName.Should().Be("TestAgent");
        loaded.AufgabeId.Should().Be(aufgabeId);
    }

    /// <summary>
    /// Testet, dass GetByAufgabeAsync Einträge chronologisch aufsteigend zurückgibt.
    /// </summary>
    [Fact]
    public async Task GetByAufgabeAsync_ShouldReturnEntriesChronologicallyAscending_WhenMultipleEntriesExist()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var aufgabeId = await CreateTestAufgabeAsync(db);
        var service = new ProtokollService(db.Context, NullLogger<ProtokollService>.Instance);

        await service.AddEintragAsync(aufgabeId, ProtokollTyp.Prompt, "Erster Eintrag");
        await Task.Delay(5);
        await service.AddEintragAsync(aufgabeId, ProtokollTyp.KiAntwort, "Zweiter Eintrag");
        await Task.Delay(5);
        await service.AddEintragAsync(aufgabeId, ProtokollTyp.StatusUebergang, "Dritter Eintrag");

        // Act
        var result = await service.GetByAufgabeAsync(aufgabeId);

        // Assert
        result.Should().HaveCount(3);
        result[0].Inhalt.Should().Be("Erster Eintrag");
        result[1].Inhalt.Should().Be("Zweiter Eintrag");
        result[2].Inhalt.Should().Be("Dritter Eintrag");
    }

    /// <summary>
    /// Testet, dass SuchenAsync Einträge findet, die den Suchbegriff im Inhalt enthalten.
    /// </summary>
    [Fact]
    public async Task SuchenAsync_ShouldFindEntriesContainingSuchbegriff_WhenInhaltMatches()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var aufgabeId = await CreateTestAufgabeAsync(db);
        var service = new ProtokollService(db.Context, NullLogger<ProtokollService>.Instance);

        await service.AddEintragAsync(aufgabeId, ProtokollTyp.Prompt, "Suche nach Fehler im Modul");
        await service.AddEintragAsync(aufgabeId, ProtokollTyp.KiAntwort, "Nichts Relevantes hier");
        await service.AddEintragAsync(aufgabeId, ProtokollTyp.Prompt, "Fehler gefunden: NullReference");

        // Act
        var result = await service.SuchenAsync(aufgabeId, "Fehler");

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(e => e.Inhalt.Should().Contain("Fehler"));
    }

    /// <summary>
    /// Testet, dass SuchenAsync Einträge findet, die den Suchbegriff im AgentName enthalten.
    /// </summary>
    [Fact]
    public async Task SuchenAsync_ShouldFindEntriesByAgentName_WhenAgentNameMatches()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var aufgabeId = await CreateTestAufgabeAsync(db);
        var service = new ProtokollService(db.Context, NullLogger<ProtokollService>.Instance);

        await service.AddEintragAsync(aufgabeId, ProtokollTyp.Prompt, "Prompt von Agent A", "AgentA");
        await service.AddEintragAsync(aufgabeId, ProtokollTyp.Prompt, "Prompt von Agent B", "AgentB");
        await service.AddEintragAsync(aufgabeId, ProtokollTyp.KiAntwort, "Antwort von Agent A", "AgentA");

        // Act
        var result = await service.SuchenAsync(aufgabeId, "AgentA");

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(e => e.AgentName.Should().Be("AgentA"));
    }

    /// <summary>
    /// Testet, dass AddTestErgebnisseAsync einen Protokolleintrag mit TestErgebnissen speichert.
    /// </summary>
    [Fact]
    public async Task AddTestErgebnisseAsync_ShouldPersistTestErgebnisseWithProtokollEintrag_WhenTestResultGiven()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var aufgabeId = await CreateTestAufgabeAsync(db);
        var service = new ProtokollService(db.Context, NullLogger<ProtokollService>.Instance);

        var testResult = new TestResult(
            Bestanden: false,
            Ergebnisse: new[]
            {
                new TestErgebnisInfo("IntegrationTest.Test1", TestStatus.Bestanden, null, TimeSpan.FromMilliseconds(100)),
                new TestErgebnisInfo("IntegrationTest.Test2", TestStatus.Fehlgeschlagen, "Expected true but was false", TimeSpan.FromMilliseconds(50)),
                new TestErgebnisInfo("IntegrationTest.Test3", TestStatus.Uebersprungen, null, TimeSpan.Zero)
            });

        // Act
        var eintrag = await service.AddTestErgebnisseAsync(aufgabeId, testResult);

        // Assert – Persistenz via neuem Context prüfen
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Protokolleintraege
            .Include(p => p.TestErgebnisse)
            .FirstOrDefaultAsync(p => p.Id == eintrag.Id);

        loaded.Should().NotBeNull();
        loaded!.Typ.Should().Be(ProtokollTyp.TestErgebnis);
        loaded.TestErgebnisse.Should().HaveCount(3);

        var fehlgeschlagen = loaded.TestErgebnisse.Single(t => t.Status == TestStatus.Fehlgeschlagen);
        fehlgeschlagen.TestName.Should().Be("IntegrationTest.Test2");
        fehlgeschlagen.Fehlermeldung.Should().Be("Expected true but was false");
    }

    /// <summary>
    /// Testet, dass AddTestErgebnisseAsync die Dauer der Tests korrekt speichert (TimeSpan-Konvertierung).
    /// </summary>
    [Fact]
    public async Task AddTestErgebnisseAsync_ShouldPersistTestDuration_WhenTimeSpanGiven()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var aufgabeId = await CreateTestAufgabeAsync(db);
        var service = new ProtokollService(db.Context, NullLogger<ProtokollService>.Instance);

        var erwarteteDauer = TimeSpan.FromSeconds(1.5);
        var testResult = new TestResult(
            Bestanden: true,
            Ergebnisse: new[]
            {
                new TestErgebnisInfo("DauerTest", TestStatus.Bestanden, null, erwarteteDauer)
            });

        // Act
        var eintrag = await service.AddTestErgebnisseAsync(aufgabeId, testResult);

        // Assert – Überprüfen der TimeSpan-Konvertierung (Ticks → TimeSpan)
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Protokolleintraege
            .Include(p => p.TestErgebnisse)
            .FirstAsync(p => p.Id == eintrag.Id);

        loaded.TestErgebnisse.Should().HaveCount(1);
        loaded.TestErgebnisse[0].Dauer.Should().Be(erwarteteDauer);
    }

    /// <summary>
    /// Testet, dass AddStatusUebergangAsync einen Statusübergang korrekt als Protokolleintrag speichert.
    /// </summary>
    [Fact]
    public async Task AddStatusUebergangAsync_ShouldPersistStatusTransitionEntry_WhenCalledWithValidStatuses()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var aufgabeId = await CreateTestAufgabeAsync(db);
        var service = new ProtokollService(db.Context, NullLogger<ProtokollService>.Instance);

        // Act
        var eintrag = await service.AddStatusUebergangAsync(
            aufgabeId,
            AufgabeStatus.Neu,
            AufgabeStatus.Gestartet);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.Protokolleintraege.FindAsync(eintrag.Id);

        loaded.Should().NotBeNull();
        loaded!.Typ.Should().Be(ProtokollTyp.StatusUebergang);
        loaded.Inhalt.Should().Contain("Neu");
        loaded.Inhalt.Should().Contain("Gestartet");
    }

    /// <summary>
    /// Testet, dass GetByAufgabeAsync TestErgebnisse via Include mitlädt.
    /// </summary>
    [Fact]
    public async Task GetByAufgabeAsync_ShouldIncludeTestErgebnisse_WhenProtokollEintragHasTestResults()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync();
        var aufgabeId = await CreateTestAufgabeAsync(db);
        var service = new ProtokollService(db.Context, NullLogger<ProtokollService>.Instance);

        var testResult = new TestResult(
            Bestanden: true,
            Ergebnisse: new[]
            {
                new TestErgebnisInfo("Test.A", TestStatus.Bestanden, null, TimeSpan.FromMilliseconds(10)),
                new TestErgebnisInfo("Test.B", TestStatus.Bestanden, null, TimeSpan.FromMilliseconds(20))
            });

        await service.AddTestErgebnisseAsync(aufgabeId, testResult);

        // Act
        var result = await service.GetByAufgabeAsync(aufgabeId);

        // Assert
        result.Should().HaveCount(1);
        result[0].TestErgebnisse.Should().HaveCount(2);
    }
}
