using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.IntegrationTests.Infrastructure;
using TestRunResult = Softwareschmiede.Domain.ValueObjects.TestRunResult;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Integrationstests für <see cref="AufgabeService"/> mit echter SQLite-Datenbank.</summary>
public sealed class AufgabeServiceTests
{
    /// <summary>Hilfsmethode: erstellt ein Testprojekt direkt über den DbContext.</summary>
    private static async Task<Guid> CreateTestProjektAsync(DatabaseFixture db)
    {
        var service = new ProjektService(db.Context, NullLogger<ProjektService>.Instance);
        var project = await service.CreateAsync("Integrationstestprojekt", null);
        return project.Id;
    }

    /// <summary>
    /// Testet, dass CreateAsync eine Aufgabe anlegt und mit einem neuen Context wieder geladen werden kann.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldPersistAufgabe_WhenValidDataGiven()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));

        // Act
        var created = await service.CreateAsync(projectId, "Test-Aufgabe", "Anforderungsbeschreibung", ct: TestContext.Current.CancellationToken);

        // Assert – Persistenz via neuem Context prüfen
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.WorkItems.FindAsync(new object?[] { created.Id }, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.Title.Should().Be("Test-Aufgabe");
        loaded.RequirementDescription.Should().Be("Anforderungsbeschreibung");
        loaded.Status.Should().Be(WorkItemStatus.New);
        loaded.ExecutionStatus.Should().Be(WorkItemExecutionStatus.NotStarted);
        loaded.ProjectId.Should().Be(projectId);
    }

    /// <summary>
    /// Testet, dass StartenAsync den Status auf InBearbeitung setzt und Branch/Pfad persistiert.
    /// </summary>
    [Fact]
    public async Task StartenAsync_ShouldSetStatusInBearbeitungAndPersistBranchInfo_WhenAufgabeExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await service.CreateAsync(projectId, "Startbare Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        await service.StartenAsync(workItem.Id, "task/feature-branch", @"C:\klone\aufgabe", ct: TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);

        loaded!.Status.Should().Be(WorkItemStatus.Started);
        loaded.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Active);
        loaded.BranchName.Should().Be("task/feature-branch");
        loaded.LocalClonePath.Should().Be(@"C:\klone\aufgabe");
    }

    /// <summary>
    /// Testet, dass AbschliessenAsync den Status auf Abgeschlossen setzt und das AbschlussDatum persistiert.
    /// </summary>
    [Fact]
    public async Task AbschliessenAsync_ShouldSetStatusAbgeschlossenAndClearBranchInfo_WhenAufgabeInBearbeitung()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await service.CreateAsync(projectId, "Abzuschließende Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await service.StartenAsync(workItem.Id, "task/fertig", @"C:\klone\fertig", ct: TestContext.Current.CancellationToken);

        var vorAbschluss = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        await service.AbschliessenAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);

        loaded!.Status.Should().Be(WorkItemStatus.Completed);
        loaded.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Completed);
        loaded.CompletedAt.Should().NotBeNull();
        // Toleranz von 2 Sekunden, da DateTimeOffset als Unix-Millisekunden gespeichert wird (Präzisionsverlust < 1 ms)
        loaded.CompletedAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        loaded.BranchName.Should().BeNull();
        loaded.LocalClonePath.Should().BeNull();
    }

    /// <summary>
    /// Testet, dass GetDetailAsync Protokolleinträge NICHT mehr per Include lädt (Issue 193: Protokoll wird
    /// separat über ProtokollService.GetByAufgabeAsync geladen, um die Detailansicht schnell anzuzeigen).
    /// </summary>
    [Fact]
    public async Task GetDetailAsync_ShouldNotIncludeProtokolleintraege_WhenDataExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var protokollService = new ProtokollService(db.Context, NullLogger<ProtokollService>.Instance);

        var workItem = await aufgabeService.CreateAsync(projectId, "Aufgabe mit Protokoll", null, ct: TestContext.Current.CancellationToken);
        await protokollService.AddEintragAsync(workItem.Id, LogEntryType.Prompt, "Starte Analyse", "TestAgent", TestContext.Current.CancellationToken);

        var testResult = new TestRunResult(
            true,
            new[]
            {
                new TestErgebnisInfo("Test1", TestStatus.Passed, null, TimeSpan.FromMilliseconds(42))
            });
        await protokollService.AddTestErgebnisseAsync(workItem.Id, testResult, TestContext.Current.CancellationToken);

        // Act
        var detail = await aufgabeService.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);
        var protokolleintraege = await protokollService.GetByAufgabeAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        detail.Should().NotBeNull();
        detail!.LogEntries.Should().BeEmpty();

        protokolleintraege.Should().HaveCount(2);
        var testEintrag = protokolleintraege.Single(p => p.Type == LogEntryType.TestResult);
        testEintrag.TestResults.Should().HaveCount(1);
        testEintrag.TestResults[0].TestName.Should().Be("Test1");
    }

    /// <summary>
    /// Testet, dass DeleteAsync eine Aufgabe und ihre Protokolleinträge kaskadierend löscht.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ShouldCascadeDeleteProtokolleintraege_WhenAufgabeDeleted()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var protokollService = new ProtokollService(db.Context, NullLogger<ProtokollService>.Instance);

        var workItem = await aufgabeService.CreateAsync(projectId, "Zu löschende Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await protokollService.AddEintragAsync(workItem.Id, LogEntryType.Prompt, "Irgendein Prompt", ct: TestContext.Current.CancellationToken);
        await protokollService.AddEintragAsync(workItem.Id, LogEntryType.AiResponse, "Irgendeine Antwort", ct: TestContext.Current.CancellationToken);

        // Act
        await aufgabeService.DeleteAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var geladen = await db2.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        var protokolle = db2.LogEntries.Where(p => p.WorkItemId == workItem.Id).ToList();

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
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await service.CreateAsync(projectId, "Offene Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        await service.VerwerfenAsync(workItem.Id, VerwerfenAktion.Archive, TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);

        loaded!.Status.Should().Be(WorkItemStatus.Archived);
    }

    /// <summary>
    /// Testet, dass VerwerfenAsync eine offene Aufgabe dauerhaft löscht und kaskadierend entfernt.
    /// </summary>
    [Fact]
    public async Task VerwerfenAsync_ShouldCascadeDeleteProtokolleintraege_WhenOffeneAufgabeGeloeschtWird()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var aufgabeService = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var protokollService = new ProtokollService(db.Context, NullLogger<ProtokollService>.Instance);

        var workItem = await aufgabeService.CreateAsync(projectId, "Offene Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await protokollService.AddEintragAsync(workItem.Id, LogEntryType.Prompt, "Ein Prompt", ct: TestContext.Current.CancellationToken);

        // Act
        await aufgabeService.VerwerfenAsync(workItem.Id, VerwerfenAktion.Delete, TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var geladen = await db2.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        var protokolle = db2.LogEntries.Where(p => p.WorkItemId == workItem.Id).ToList();

        geladen.Should().BeNull();
        protokolle.Should().BeEmpty();
    }

    /// <summary>
    /// Testet, dass VerwerfenAsync eine bereits gestartete Aufgabe für beide Verwerfungsaktionen ablehnt.
    /// </summary>
    [Theory]
    [InlineData(VerwerfenAktion.Archive)]
    [InlineData(VerwerfenAktion.Delete)]
    public async Task VerwerfenAsync_ShouldThrowWhenAufgabeIsNotOffen(VerwerfenAktion aktion)
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await service.CreateAsync(projectId, "Gestartete Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await service.StartenAsync(workItem.Id, "task/test", @"C:\klone\test", ct: TestContext.Current.CancellationToken);

        // Act
        var act = () => service.VerwerfenAsync(workItem.Id, aktion);

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
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));

        var issue = new Issue(
            Nummer: 42,
            Title: "Bug: Crash beim Start",
            Body: "Detaillierte Beschreibung",
            Labels: new[] { "bug", "critical" },
            Milestone: "v1.0",
            IssueUrl: "https://github.com/test/repo/issues/42");

        // Act
        var workItem = await service.CreateFromIssueAsync(projectId, issue, ct: TestContext.Current.CancellationToken);

        // Assert – IssueReferenz via Detail-Abfrage laden
        var detail = await service.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);

        detail!.IssueReference.Should().NotBeNull();
        detail.IssueReference!.IssueNumber.Should().Be(42);
        detail.IssueReference.Title.Should().Be("Bug: Crash beim Start");
        detail.IssueReference.Milestone.Should().Be("v1.0");
    }

    /// <summary>
    /// Deckt das Rennen zwischen zweiter ViewModel-Prüfung und Persistenz ab: eine parallel gesetzte
    /// IssueReferenz darf durch die Issue-Anlage nicht überschrieben werden.
    /// </summary>
    [Fact]
    public async Task TryAssignIssueReferenzIfNoneAsync_ShouldNotOverwrite_WhenReferenceIsAssignedAfterCallerCheck()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await service.CreateAsync(projectId, "Issue-Anlage", null, ct: TestContext.Current.CancellationToken);
        var createdIssue = new Issue(23, "Neu", "Body", [], null, "https://example.test/23");
        var parallelIssue = new Issue(99, "Parallel", "Body", [], null, "https://example.test/99");

        var standVorZuordnung = await service.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);
        standVorZuordnung!.IssueReference.Should().BeNull();

        await using (var parallelContext = db.CreateNewContext())
        {
            var parallelService = new AufgabeService(parallelContext, NullLogger<AufgabeService>.Instance, new TodoService(parallelContext, NullLogger<TodoService>.Instance));
            await parallelService.UpdateIssueReferenzAsync(workItem.Id, parallelIssue, TestContext.Current.CancellationToken);
        }

        var assigned = await service.TryAssignIssueReferenzIfNoneAsync(workItem.Id, createdIssue, TestContext.Current.CancellationToken);

        assigned.Should().BeFalse();
        await using var assertContext = db.CreateNewContext();
        var reloaded = await assertContext.WorkItems
            .Include(a => a.IssueReference)
            .SingleAsync(a => a.Id == workItem.Id, cancellationToken: TestContext.Current.CancellationToken);
        reloaded.IssueReference!.IssueNumber.Should().Be(99);
        reloaded.IssueReference.IssueUrl.Should().Be("https://example.test/99");
    }

    /// <summary>
    /// Testet, dass GetByProjektAsync alle Aufgaben eines Projekts absteigend nach Erstellungsdatum zurückgibt.
    /// </summary>
    [Fact]
    public async Task GetByProjektAsync_ShouldReturnAufgabenDescendingByDate_WhenMultipleAufgabenExist()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));

        await service.CreateAsync(projectId, "Erste Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await Task.Delay(5, TestContext.Current.CancellationToken); // sicherstellen, dass ErstellungsDatum unterschiedlich ist
        await service.CreateAsync(projectId, "Zweite Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await Task.Delay(5, TestContext.Current.CancellationToken);
        await service.CreateAsync(projectId, "Dritte Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        var result = await service.GetByProjektAsync(projectId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(3);
        result[0].Title.Should().Be("Dritte Aufgabe");
        result[2].Title.Should().Be("Erste Aufgabe");
    }

    /// <summary>
    /// Testet, dass SetStatusAsync den Status auf Beendet setzt.
    /// </summary>
    [Fact]
    public async Task SetStatusAsync_ShouldSetStatusBeendet_WhenAufgabeIsGestartet()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await service.CreateAsync(projectId, "Aufgabe für Statuswechsel", null, ct: TestContext.Current.CancellationToken);
        await service.StatusSetzenAsync(workItem.Id, WorkItemStatus.Started, TestContext.Current.CancellationToken);

        // Act
        await service.SetStatusAsync(workItem.Id, WorkItemStatus.Completed, TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);

        loaded!.Status.Should().Be(WorkItemStatus.Completed);
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
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));

        var jetzt = DateTimeOffset.UtcNow;

        var mitAltemStart = new Softwareschmiede.Domain.Entities.WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Alter Start",
            Status = WorkItemStatus.Started,
            CreatedAt = jetzt.AddHours(-3),
            LastHeartbeatUtc = jetzt,
            LastCliStartUtc = jetzt.AddMinutes(-10)
        };
        var mitNeuemStart = new Softwareschmiede.Domain.Entities.WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Neuer Start",
            Status = WorkItemStatus.Waiting,
            CreatedAt = jetzt.AddHours(-2),
            LastHeartbeatUtc = jetzt.AddHours(-1),
            LastCliStartUtc = jetzt.AddMinutes(-1)
        };
        var ohneStart = new Softwareschmiede.Domain.Entities.WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Ohne Start",
            Status = WorkItemStatus.Started,
            CreatedAt = jetzt.AddMinutes(-5),
            LastHeartbeatUtc = null,
            LastCliStartUtc = null
        };

        db.Context.WorkItems.AddRange(mitAltemStart, mitNeuemStart, ohneStart);
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await service.GetAktiveAufgabenAsync(TestContext.Current.CancellationToken);

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
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await service.CreateAsync(projectId, "Ursprünglicher Titel", "Alt", ct: TestContext.Current.CancellationToken);

        // Act
        await service.UpdateAsync(workItem.Id, "Neuer Titel", "Neue Anforderung", "copilot", TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);

        loaded!.Title.Should().Be("Neuer Titel");
        loaded.RequirementDescription.Should().Be("Neue Anforderung");
        loaded.AiPluginPrefix.Should().Be("copilot");
    }

    /// <summary>
    /// Testet, dass ein beendeter Ausführungsstatus beim Laden der Detaildaten und erneutem Speichern
    /// der Aufgabe nicht wieder auf aktiv oder nicht gestartet zurückfällt.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldKeepAusfuehrungsStatus_WhenBeendeteAusfuehrungLoadedAndSaved()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjektAsync(db);
        var service = new AufgabeService(db.Context, NullLogger<AufgabeService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await service.CreateAsync(projectId, "Persistierte Ausführung", "Alt", ct: TestContext.Current.CancellationToken);

        await service.StartenAsync(workItem.Id, "task/persistierte-ausfuehrung", @"C:\klone\persistiert", ct: TestContext.Current.CancellationToken);
        await service.AktivenLaufSetzenAsync(workItem.Id, "lauf-alt", TestContext.Current.CancellationToken);
        await service.AktivenLaufBeendenAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Act
        var detail = await service.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);
        detail!.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Completed);

        await service.UpdateAsync(detail.Id, "Persistierte Ausführung aktualisiert", detail.RequirementDescription, "copilot", TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);

        loaded!.Status.Should().Be(WorkItemStatus.Started);
        loaded.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Completed);
        loaded.ActiveRunId.Should().BeNull();
    }
}
