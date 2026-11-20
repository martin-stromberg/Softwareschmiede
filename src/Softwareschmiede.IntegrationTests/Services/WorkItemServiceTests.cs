using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.IntegrationTests.Infrastructure;
using TestRunResult = Softwareschmiede.Domain.ValueObjects.TestRunResult;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Integrationstests für <see cref="WorkItemService"/> mit echter SQLite-Datenbank.</summary>
public sealed class WorkItemServiceTests
{
    /// <summary>Hilfsmethode: erstellt ein Testprojekt direkt über den DbContext.</summary>
    private static async Task<Guid> CreateTestProjectAsync(DatabaseFixture db)
    {
        var service = new ProjectService(db.Context, NullLogger<ProjectService>.Instance);
        var project = await service.CreateAsync("Integrationstestprojekt", null);
        return project.Id;
    }

    /// <summary>
    /// Testet, dass CreateAsync eine Aufgabe anlegt und mit einem neuen Context wieder geladen werden kann.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldPersistWorkItem_WhenValidDataGiven()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjectAsync(db);
        var service = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));

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
    public async Task StartAsync_ShouldSetStatusInEditingAndPersistBranchInfo_WhenWorkItemExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjectAsync(db);
        var service = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await service.CreateAsync(projectId, "Startbare Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        await service.StartAsync(workItem.Id, "task/feature-branch", @"C:\klone\aufgabe", ct: TestContext.Current.CancellationToken);

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
    public async Task CompleteAsync_ShouldSetStatusCompletedAndClearBranchInfo_WhenWorkItemInEditing()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjectAsync(db);
        var service = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await service.CreateAsync(projectId, "Abzuschließende Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await service.StartAsync(workItem.Id, "task/fertig", @"C:\klone\fertig", ct: TestContext.Current.CancellationToken);

        var beforeCompletion = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        await service.CompleteAsync(workItem.Id, TestContext.Current.CancellationToken);

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
    public async Task GetDetailAsync_ShouldNotIncludeLogentries_WhenDataExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjectAsync(db);
        var workItemService = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var logService = new LogService(db.Context, NullLogger<LogService>.Instance);

        var workItem = await workItemService.CreateAsync(projectId, "Aufgabe mit Protokoll", null, ct: TestContext.Current.CancellationToken);
        await logService.AddEntryAsync(workItem.Id, LogEntryType.Prompt, "Starte Analyse", "TestAgent", TestContext.Current.CancellationToken);

        var testResult = new TestRunResult(
            true,
            new[]
            {
                new TestResultInfo("Test1", TestStatus.Passed, null, TimeSpan.FromMilliseconds(42))
            });
        await logService.AddTestResultsAsync(workItem.Id, testResult, TestContext.Current.CancellationToken);

        // Act
        var detail = await workItemService.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);
        var logentries = await logService.GetByWorkItemAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        detail.Should().NotBeNull();
        detail!.LogEntries.Should().BeEmpty();

        logentries.Should().HaveCount(2);
        var testEntry = logentries.Single(p => p.Type == LogEntryType.TestResult);
        testEntry.TestResults.Should().HaveCount(1);
        testEntry.TestResults[0].TestName.Should().Be("Test1");
    }

    /// <summary>
    /// Testet, dass DeleteAsync eine Aufgabe und ihre Protokolleinträge kaskadierend löscht.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ShouldCascadeDeleteLogentries_WhenWorkItemDeleted()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjectAsync(db);
        var workItemService = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var logService = new LogService(db.Context, NullLogger<LogService>.Instance);

        var workItem = await workItemService.CreateAsync(projectId, "Zu löschende Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await logService.AddEntryAsync(workItem.Id, LogEntryType.Prompt, "Irgendein Prompt", ct: TestContext.Current.CancellationToken);
        await logService.AddEntryAsync(workItem.Id, LogEntryType.AiResponse, "Irgendeine Antwort", ct: TestContext.Current.CancellationToken);

        // Act
        await workItemService.DeleteAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        var log = db2.LogEntries.Where(p => p.WorkItemId == workItem.Id).ToList();

        loaded.Should().BeNull();
        log.Should().BeEmpty();
    }

    /// <summary>
    /// Testet, dass VerwerfenAsync eine offene Aufgabe archiviert und die Änderung persistiert.
    /// </summary>
    [Fact]
    public async Task DiscardAsync_ShouldPersistArchivedStatus_WhenOpenWorkItemArchivedWill()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjectAsync(db);
        var service = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await service.CreateAsync(projectId, "Offene Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        await service.DiscardAsync(workItem.Id, DiscardAction.Archive, TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);

        loaded!.Status.Should().Be(WorkItemStatus.Archived);
    }

    /// <summary>
    /// Testet, dass VerwerfenAsync eine offene Aufgabe dauerhaft löscht und kaskadierend entfernt.
    /// </summary>
    [Fact]
    public async Task DiscardAsync_ShouldCascadeDeleteLogentries_WhenOpenWorkItemDeletedWill()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjectAsync(db);
        var workItemService = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var logService = new LogService(db.Context, NullLogger<LogService>.Instance);

        var workItem = await workItemService.CreateAsync(projectId, "Offene Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await logService.AddEntryAsync(workItem.Id, LogEntryType.Prompt, "Ein Prompt", ct: TestContext.Current.CancellationToken);

        // Act
        await workItemService.DiscardAsync(workItem.Id, DiscardAction.Delete, TestContext.Current.CancellationToken);

        // Assert
        await using var db2 = db.CreateNewContext();
        var loaded = await db2.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        var log = db2.LogEntries.Where(p => p.WorkItemId == workItem.Id).ToList();

        loaded.Should().BeNull();
        log.Should().BeEmpty();
    }

    /// <summary>
    /// Testet, dass VerwerfenAsync eine bereits gestartete Aufgabe für beide Verwerfungsaktionen ablehnt.
    /// </summary>
    [Theory]
    [InlineData(DiscardAction.Archive)]
    [InlineData(DiscardAction.Delete)]
    public async Task DiscardAsync_ShouldThrowWhenWorkItemIsNotOpen(DiscardAction action)
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjectAsync(db);
        var service = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await service.CreateAsync(projectId, "Gestartete Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await service.StartAsync(workItem.Id, "task/test", @"C:\klone\test", ct: TestContext.Current.CancellationToken);

        // Act
        var act = () => service.DiscardAsync(workItem.Id, action);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Nur neue Aufgaben können verworfen werden.");
    }

    /// <summary>
    /// Testet, dass CreateFromIssueAsync eine Aufgabe mit IssueReferenz anlegt.
    /// </summary>
    [Fact]
    public async Task CreateFromIssueAsync_ShouldPersistWorkItemWithIssueReference_WhenIssueGiven()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjectAsync(db);
        var service = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));

        var issue = new Issue(
            Number: 42,
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
    public async Task TryAssignIssueReferenceIfNoneAsync_ShouldNotOverwrite_WhenReferenceIsAssignedAfterCallerCheck()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjectAsync(db);
        var service = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await service.CreateAsync(projectId, "Issue-Anlage", null, ct: TestContext.Current.CancellationToken);
        var createdIssue = new Issue(23, "Neu", "Body", [], null, "https://example.test/23");
        var parallelIssue = new Issue(99, "Parallel", "Body", [], null, "https://example.test/99");

        var stateBeforeAssignment = await service.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);
        stateBeforeAssignment!.IssueReference.Should().BeNull();

        await using (var parallelContext = db.CreateNewContext())
        {
            var parallelService = new WorkItemService(parallelContext, NullLogger<WorkItemService>.Instance, new TodoService(parallelContext, NullLogger<TodoService>.Instance));
            await parallelService.UpdateIssueReferenceAsync(workItem.Id, parallelIssue, TestContext.Current.CancellationToken);
        }

        var assigned = await service.TryAssignIssueReferenceIfNoneAsync(workItem.Id, createdIssue, TestContext.Current.CancellationToken);

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
    public async Task GetByProjectAsync_ShouldReturnWorkItemsDescendingByDate_WhenMultipleWorkItemsExist()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjectAsync(db);
        var service = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));

        await service.CreateAsync(projectId, "Erste Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await Task.Delay(5, TestContext.Current.CancellationToken); // sicherstellen, dass ErstellungsDatum unterschiedlich ist
        await service.CreateAsync(projectId, "Zweite Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await Task.Delay(5, TestContext.Current.CancellationToken);
        await service.CreateAsync(projectId, "Dritte Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        var result = await service.GetByProjectAsync(projectId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(3);
        result[0].Title.Should().Be("Dritte Aufgabe");
        result[2].Title.Should().Be("Erste Aufgabe");
    }

    /// <summary>
    /// Testet, dass SetStatusAsync den Status auf Beendet setzt.
    /// </summary>
    [Fact]
    public async Task SetStatusAsync_ShouldSetStatusCompleted_WhenWorkItemIsStarted()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjectAsync(db);
        var service = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await service.CreateAsync(projectId, "Aufgabe für Statuswechsel", null, ct: TestContext.Current.CancellationToken);
        await service.SetStatusUncheckedAsync(workItem.Id, WorkItemStatus.Started, TestContext.Current.CancellationToken);

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
    public async Task GetActiveWorkItemsAsync_ShouldSortByLastCliStartDescThenByCreationDate_WhenUsingSqlite()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjectAsync(db);
        var service = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));

        var now = DateTimeOffset.UtcNow;

        var withOldStart = new Softwareschmiede.Domain.Entities.WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Alter Start",
            Status = WorkItemStatus.Started,
            CreatedAt = now.AddHours(-3),
            LastHeartbeatUtc = now,
            LastCliStartUtc = now.AddMinutes(-10)
        };
        var withNewStart = new Softwareschmiede.Domain.Entities.WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Neuer Start",
            Status = WorkItemStatus.Waiting,
            CreatedAt = now.AddHours(-2),
            LastHeartbeatUtc = now.AddHours(-1),
            LastCliStartUtc = now.AddMinutes(-1)
        };
        var withoutStart = new Softwareschmiede.Domain.Entities.WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Ohne Start",
            Status = WorkItemStatus.Started,
            CreatedAt = now.AddMinutes(-5),
            LastHeartbeatUtc = null,
            LastCliStartUtc = null
        };

        db.Context.WorkItems.AddRange(withOldStart, withNewStart, withoutStart);
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await service.GetActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Select(a => a.Id).Should().ContainInOrder(withNewStart.Id, withoutStart.Id, withOldStart.Id);
    }

    /// <summary>
    /// Testet, dass UpdateAsync Titel, Beschreibung und Agenteninformationen persistiert.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldPersistAllFields_WhenWorkItemExists()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjectAsync(db);
        var service = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
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
    public async Task UpdateAsync_ShouldKeepExecutionStatus_WhenFinishedExecutionLoadedAndSaved()
    {
        // Arrange
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        var projectId = await CreateTestProjectAsync(db);
        var service = new WorkItemService(db.Context, NullLogger<WorkItemService>.Instance, new TodoService(db.Context, NullLogger<TodoService>.Instance));
        var workItem = await service.CreateAsync(projectId, "Persistierte Ausführung", "Alt", ct: TestContext.Current.CancellationToken);

        await service.StartAsync(workItem.Id, "task/persistierte-ausfuehrung", @"C:\klone\persistiert", ct: TestContext.Current.CancellationToken);
        await service.SetActiveRunAsync(workItem.Id, "lauf-alt", TestContext.Current.CancellationToken);
        await service.EndActiveRunAsync(workItem.Id, TestContext.Current.CancellationToken);

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
