using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.IntegrationTests.Infrastructure;
using TestRunResult = Softwareschmiede.Domain.ValueObjects.TestRunResult;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Integrationstests für <see cref="LogService"/> mit echter SQLite-Datenbank.</summary>
public sealed class ProtokollServiceTests
{
    /// <summary>Hilfsmethode: Erstellt Testprojekt und Testaufgabe, gibt Aufgabe-ID zurück.</summary>
    private static async Task<Guid> CreateTestAufgabeAsync(DatabaseFixture db)
    {
        var projektService = new ProjectService(db.Context, NullLogger<ProjectService>.Instance);
        var workItemService = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));

        var project = await projektService.CreateAsync("Testprojekt Protokoll", null);
        var workItem = await workItemService.CreateAsync(project.Id, "Testaufgabe Protokoll", null);
        return workItem.Id;
    }

    /// <summary>
    /// Testet, dass AddEintragAsync einen Protokolleintrag speichert und mit neuem Context geladen werden kann.
    /// </summary>
    [Fact]
    public async Task AddEintragAsync_ShouldPersistProtokollEintrag_WhenValidDataGiven()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var workItemId = await CreateTestAufgabeAsync(db);
        var service = new LogService(db.Context, NullLogger<LogService>.Instance);

        // Act
        var entry = await service.AddEntryAsync(workItemId, LogEntryType.Prompt, "Analysiere den Code und erstelle Tests.", "TestAgent", TestContext.Current.CancellationToken);

        // Assert – Persistenz via neuem Context prüfen
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.LogEntries.FindAsync(new object?[] { entry.Id }, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.Content.Should().Be("Analysiere den Code und erstelle Tests.");
        loaded.Type.Should().Be(LogEntryType.Prompt);
        loaded.AgentName.Should().Be("TestAgent");
        loaded.WorkItemId.Should().Be(workItemId);
    }

    /// <summary>
    /// Testet, dass GetByAufgabeAsync Einträge chronologisch aufsteigend zurückgibt.
    /// </summary>
    [Fact]
    public async Task GetByAufgabeAsync_ShouldReturnEntriesChronologicallyAscending_WhenMultipleEntriesExist()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var workItemId = await CreateTestAufgabeAsync(db);
        var service = new LogService(db.Context, NullLogger<LogService>.Instance);

        await service.AddEntryAsync(workItemId, LogEntryType.Prompt, "Erster Eintrag", ct: TestContext.Current.CancellationToken);
        await Task.Delay(5, TestContext.Current.CancellationToken);
        await service.AddEntryAsync(workItemId, LogEntryType.AiResponse, "Zweiter Eintrag", ct: TestContext.Current.CancellationToken);
        await Task.Delay(5, TestContext.Current.CancellationToken);
        await service.AddEntryAsync(workItemId, LogEntryType.StatusTransition, "Dritter Eintrag", ct: TestContext.Current.CancellationToken);

        // Act
        var result = await service.GetByWorkItemAsync(workItemId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(3);
        result[0].Content.Should().Be("Erster Eintrag");
        result[1].Content.Should().Be("Zweiter Eintrag");
        result[2].Content.Should().Be("Dritter Eintrag");
    }

    /// <summary>
    /// Testet, dass SuchenAsync Einträge findet, die den Suchbegriff im Inhalt enthalten.
    /// </summary>
    [Fact]
    public async Task SuchenAsync_ShouldFindEntriesContainingSuchbegriff_WhenInhaltMatches()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var workItemId = await CreateTestAufgabeAsync(db);
        var service = new LogService(db.Context, NullLogger<LogService>.Instance);

        await service.AddEntryAsync(workItemId, LogEntryType.Prompt, "Suche nach Fehler im Modul", ct: TestContext.Current.CancellationToken);
        await service.AddEntryAsync(workItemId, LogEntryType.AiResponse, "Nichts Relevantes hier", ct: TestContext.Current.CancellationToken);
        await service.AddEntryAsync(workItemId, LogEntryType.Prompt, "Fehler gefunden: NullReference", ct: TestContext.Current.CancellationToken);

        // Act
        var result = await service.SearchAsync(workItemId, "Fehler", TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(e => e.Content.Should().Contain("Fehler"));
    }

    /// <summary>
    /// Testet, dass SuchenAsync Einträge findet, die den Suchbegriff im AgentName enthalten.
    /// </summary>
    [Fact]
    public async Task SuchenAsync_ShouldFindEntriesByAgentName_WhenAgentNameMatches()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var workItemId = await CreateTestAufgabeAsync(db);
        var service = new LogService(db.Context, NullLogger<LogService>.Instance);

        await service.AddEntryAsync(workItemId, LogEntryType.Prompt, "Prompt von Agent A", "AgentA", TestContext.Current.CancellationToken);
        await service.AddEntryAsync(workItemId, LogEntryType.Prompt, "Prompt von Agent B", "AgentB", TestContext.Current.CancellationToken);
        await service.AddEntryAsync(workItemId, LogEntryType.AiResponse, "Antwort von Agent A", "AgentA", TestContext.Current.CancellationToken);

        // Act
        var result = await service.SearchAsync(workItemId, "AgentA", TestContext.Current.CancellationToken);

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
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var workItemId = await CreateTestAufgabeAsync(db);
        var service = new LogService(db.Context, NullLogger<LogService>.Instance);

        var testResult = new TestRunResult(
            Passed: false,
            Results: new[]
            {
                new TestErgebnisInfo("IntegrationTest.Test1", TestStatus.Passed, null, TimeSpan.FromMilliseconds(100)),
                new TestErgebnisInfo("IntegrationTest.Test2", TestStatus.Failed, "Expected true but was false", TimeSpan.FromMilliseconds(50)),
                new TestErgebnisInfo("IntegrationTest.Test3", TestStatus.Skipped, null, TimeSpan.Zero)
            });

        // Act
        var entry = await service.AddTestResultsAsync(workItemId, testResult, TestContext.Current.CancellationToken);

        // Assert – Persistenz via neuem Context prüfen
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.LogEntries
            .Include(p => p.TestResults)
            .FirstOrDefaultAsync(p => p.Id == entry.Id, cancellationToken: TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.Type.Should().Be(LogEntryType.TestResult);
        loaded.TestResults.Should().HaveCount(3);

        var fehlgeschlagen = loaded.TestResults.Single(t => t.Status == TestStatus.Failed);
        fehlgeschlagen.TestName.Should().Be("IntegrationTest.Test2");
        fehlgeschlagen.ErrorMessage.Should().Be("Expected true but was false");
    }

    /// <summary>
    /// Testet, dass AddTestErgebnisseAsync die Dauer der Tests korrekt speichert (TimeSpan-Konvertierung).
    /// </summary>
    [Fact]
    public async Task AddTestErgebnisseAsync_ShouldPersistTestDuration_WhenTimeSpanGiven()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var workItemId = await CreateTestAufgabeAsync(db);
        var service = new LogService(db.Context, NullLogger<LogService>.Instance);

        var erwarteteDauer = TimeSpan.FromSeconds(1.5);
        var testResult = new TestRunResult(
            Passed: true,
            Results: new[]
            {
                new TestErgebnisInfo("DauerTest", TestStatus.Passed, null, erwarteteDauer)
            });

        // Act
        var entry = await service.AddTestResultsAsync(workItemId, testResult, TestContext.Current.CancellationToken);

        // Assert – Überprüfen der TimeSpan-Konvertierung (Ticks → TimeSpan)
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.LogEntries
            .Include(p => p.TestResults)
            .FirstAsync(p => p.Id == entry.Id, cancellationToken: TestContext.Current.CancellationToken);

        loaded.TestResults.Should().HaveCount(1);
        loaded.TestResults[0].Duration.Should().Be(erwarteteDauer);
    }

    /// <summary>
    /// Testet, dass AddStatusUebergangAsync einen Statusübergang korrekt als Protokolleintrag speichert.
    /// </summary>
    [Fact]
    public async Task AddStatusUebergangAsync_ShouldPersistStatusTransitionEntry_WhenCalledWithValidStatuses()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var workItemId = await CreateTestAufgabeAsync(db);
        var service = new LogService(db.Context, NullLogger<LogService>.Instance);

        // Act
        var entry = await service.AddStatusTransitionAsync(workItemId, WorkItemStatus.New, WorkItemStatus.Started, TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.LogEntries.FindAsync(new object?[] { entry.Id }, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.Type.Should().Be(LogEntryType.StatusTransition);
        loaded.Content.Should().Contain("Neu");
        loaded.Content.Should().Contain("Gestartet");
    }

    /// <summary>
    /// Testet, dass GetByAufgabeAsync TestErgebnisse via Include mitlädt.
    /// </summary>
    [Fact]
    public async Task GetByAufgabeAsync_ShouldIncludeTestErgebnisse_WhenProtokollEintragHasTestResults()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var workItemId = await CreateTestAufgabeAsync(db);
        var service = new LogService(db.Context, NullLogger<LogService>.Instance);

        var testResult = new TestRunResult(
            Passed: true,
            Results: new[]
            {
                new TestErgebnisInfo("Test.A", TestStatus.Passed, null, TimeSpan.FromMilliseconds(10)),
                new TestErgebnisInfo("Test.B", TestStatus.Passed, null, TimeSpan.FromMilliseconds(20))
            });

        await service.AddTestResultsAsync(workItemId, testResult, TestContext.Current.CancellationToken);

        // Act
        var result = await service.GetByWorkItemAsync(workItemId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(1);
        result[0].TestResults.Should().HaveCount(2);
    }
}
