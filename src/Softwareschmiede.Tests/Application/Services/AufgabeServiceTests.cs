using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Tests.Helpers;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den AufgabeService.</summary>
public sealed class AufgabeServiceTests : IDisposable
{
    private readonly Softwareschmiede.Infrastructure.Data.SoftwareschmiedeDbContext _db;
    private readonly Mock<ILogger<WorkItemService>> _loggerMock;
    private readonly TodoService _todoService;
    private readonly WorkItemService _sut;
    private readonly Guid _projektId = new Guid("11111111-1111-1111-1111-111111111111");

    /// <summary>AufgabeServiceTests.</summary>
    public AufgabeServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _loggerMock = new Mock<ILogger<WorkItemService>>();
        _todoService = new TodoService(_db, NullLogger<TodoService>.Instance);
        _sut = new WorkItemService(_db, _loggerMock.Object, _todoService);

        // Seed a project for FK constraints
        _db.Projects.Add(new Softwareschmiede.Domain.Entities.Project
        {
            Id = _projektId,
            Name = "Testprojekt",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = ProjectStatus.Active
        });
        _db.SaveChanges();
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>CreateAsync erstellt eine Aufgabe mit Status Offen.</summary>
    [Fact]
    public async Task CreateAsync_ShouldCreateAufgabeWithStatusOffen_WhenCalledWithValidData()
    {
        // Arrange & Act
        var result = await _sut.CreateAsync(_projektId, "Test Aufgabe", "Beschreibung", ct: TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Title.Should().Be("Test Aufgabe");
        result.Status.Should().Be(WorkItemStatus.New);
        result.ExecutionStatus.Should().Be(WorkItemExecutionStatus.NotStarted);
        result.ProjectId.Should().Be(_projektId);
    }

    /// <summary>CreateFromIssueAsync erstellt eine Aufgabe aus einem Issue mit IssueReferenz.</summary>
    [Fact]
    public async Task CreateFromIssueAsync_ShouldCreateAufgabeWithIssueReferenz_WhenCalledWithValidIssue()
    {
        // Arrange
        var issue = new Issue(
            Number: 42,
            Title: "Fix login bug",
            Body: "Der Login funktioniert nicht.",
            Labels: new[] { "bug", "priority-high" },
            Milestone: "v1.0",
            IssueUrl: "https://github.com/test/repo/issues/42");

        // Act
        var result = await _sut.CreateFromIssueAsync(_projektId, issue, ct: TestContext.Current.CancellationToken);

        // Assert
        result.Title.Should().Be("Fix login bug");
        result.IssueReference.Should().NotBeNull();
        result.IssueReference!.IssueNumber.Should().Be(42);
        result.IssueReference.Milestone.Should().Be("v1.0");
    }

    /// <summary>CreateFromPullRequestAsync erstellt eine Aufgabe mit ReviewSource-Referenz.</summary>
    [Fact]
    public async Task CreateFromPullRequestAsync_ShouldCreateAufgabeWithReviewSourceReference()
    {
        // Arrange
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = _projektId,
            RepositoryName = "owner/repo",
            RepositoryUrl = "https://github.com/owner/repo.git",
            PluginType = "Softwareschmiede.GitHub",
            Active = true
        };
        _db.GitRepositories.Add(repository);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var pullRequest = new PullRequest(
            Number: 17,
            Title: "Review external change",
            Url: "https://github.com/owner/repo/pull/17",
            BranchName: "feature/external",
            Provider: PullRequestProvider.GitHub,
            RepositoryId: "OWNER/Repo",
            ProviderPullRequestId: "PR_kwDO",
            TargetBranch: "main",
            HeadSha: "abc123",
            SourceRepositoryId: "fork/repo",
            SourceRepositoryUrl: "https://github.com/fork/repo.git",
            SourceRef: "refs/heads/feature/external",
            Body: """
                Bitte diese Dependency-Aktualisierung reviewen.
                <details>
                <summary>Release notes</summary>
                <p><em>Sourced from <a href="https://github.com/example/releases">example releases</a>.</em></p>
                <ul><li>fix <code>parser</code> output</li></ul>
                <!-- raw HTML omitted -->
                </details>
                """);
        var context = new ScmRepositoryContext(repository.Id, repository.PluginType, "owner/repo");

        // Act
        var result = await _sut.CreateFromPullRequestAsync(_projektId, pullRequest, context, TestContext.Current.CancellationToken);

        // Assert
        result.Title.Should().Be("Review external change");
        result.RequirementDescription.Should().Contain("Bitte diese Dependency-Aktualisierung reviewen.");
        result.RequirementDescription.Should().Contain("Release notes");
        result.RequirementDescription.Should().Contain("- fix `parser` output");
        result.RequirementDescription.Should().Contain("https://github.com/owner/repo/pull/17");
        result.RequirementDescription.Should().NotContain("<details>");
        result.RequirementDescription.Should().NotContain("<summary>");
        result.RequirementDescription.Should().NotContain("<a ");
        result.GitRepositoryId.Should().Be(repository.Id);
        result.PullRequests.Should().ContainSingle();
        var reference = result.PullRequests.Single();
        reference.Role.Should().Be(PullRequestReferenzRolle.ReviewSource);
        reference.RepositoryId.Should().Be("owner/repo");
        reference.SourceRepositoryId.Should().Be("fork/repo");
        reference.SourceRepositoryUrl.Should().Be("https://github.com/fork/repo.git");
        reference.SourceRef.Should().Be("refs/heads/feature/external");
        reference.MonitoringPhase.Should().Be(PullRequestMonitoringPhase.Created);
        reference.NextCheckUtc.Should().NotBeNull();
    }

    /// <summary>CreateFromAlertAsync erstellt Aufgabe, IssueReferenz und AlertReferenz.</summary>
    [Fact]
    public async Task CreateFromAlertAsync_ShouldCreateAufgabeWithIssueAndAlertReference()
    {
        var alert = new ScmAlert(
            7,
            "github:code-scanning:owner/repo:7",
            ScmAlertType.CodeScanning,
            "SQL injection",
            "Benutzereingabe wird unsicher verwendet.",
            "https://github.com/owner/repo/security/code-scanning/7",
            "high",
            "open",
            "CodeQL",
            "cs/sql-injection",
            "SQL injection",
            "src/Login.cs",
            42);
        var issueBody = string.Join(Environment.NewLine, new[]
        {
            "Automatisch aus einem GitHub-Code-Scanning-Alert erstellt.",
            string.Empty,
            "Alert-Typ: CodeScanning",
            "Severity: high",
            "Status: open",
            "Tool: CodeQL",
            "Rule: SQL injection",
            "Betroffener Ort: src/Login.cs:42",
            "Alert-URL: https://github.com/owner/repo/security/code-scanning/7",
            string.Empty,
            "Benutzereingabe wird unsicher verwendet."
        });
        var issue = new Issue(123, "Code scanning alert: SQL injection", issueBody, [], null, "https://github.com/owner/repo/issues/123");

        var result = await _sut.CreateFromAlertAsync(_projektId, alert, issue, provider: "Softwareschmiede.GitHub", repositoryId: "owner/repo", ct: TestContext.Current.CancellationToken);

        result.Title.Should().Be("SQL injection");
        result.RequirementDescription.Should().Be(issueBody);
        result.IssueReference.Should().NotBeNull();
        result.IssueReference!.IssueNumber.Should().Be(123);
        result.AlertReference.Should().NotBeNull();
        result.AlertReference!.SourceKey.Should().Be("github:code-scanning:owner/repo:7");
        result.AlertReference.RuleId.Should().Be("cs/sql-injection");
    }

    /// <summary>CreateFromAlertAsync erzeugt bei gleicher SourceKey keine zweite Aufgabe.</summary>
    [Fact]
    public async Task CreateFromAlertAsync_ShouldReturnExistingTask_WhenSourceKeyAlreadyExists()
    {
        var alert = new ScmAlert(7, "github:code-scanning:owner/repo:7", ScmAlertType.CodeScanning, "Alert", "Body", null, "medium", "open", "CodeQL", "rule", "Rule", null, null);
        var firstIssue = new Issue(123, "Issue 1", "Body", [], null, "https://github.com/owner/repo/issues/123");
        var secondIssue = new Issue(124, "Issue 2", "Body", [], null, "https://github.com/owner/repo/issues/124");

        var first = await _sut.CreateFromAlertAsync(_projektId, alert, firstIssue, ct: TestContext.Current.CancellationToken);
        var second = await _sut.CreateFromAlertAsync(_projektId, alert, secondIssue, ct: TestContext.Current.CancellationToken);

        second.Id.Should().Be(first.Id);
        var workItems = await _sut.GetByProjectAsync(_projektId, TestContext.Current.CancellationToken);
        workItems.Count(a => a.AlertReference != null && a.AlertReference.SourceKey == alert.SourceKey).Should().Be(1);
    }

    /// <summary>DeleteAsync entfernt die AlertReferenz kaskadierend mit der Aufgabe.</summary>
    [Fact]
    public async Task DeleteAsync_ShouldCascadeDeleteAlertReferenz_WhenTaskHasAlertReference()
    {
        var alert = new ScmAlert(7, "github:code-scanning:owner/repo:7", ScmAlertType.CodeScanning, "Alert", "Body", null, "medium", "open", "CodeQL", "rule", "Rule", null, null);
        var issue = new Issue(123, "Issue", "Body", [], null, "https://github.com/owner/repo/issues/123");
        var workItem = await _sut.CreateFromAlertAsync(_projektId, alert, issue, ct: TestContext.Current.CancellationToken);

        await _sut.DeleteAsync(workItem.Id, TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();

        _db.WorkItems.Any(a => a.Id == workItem.Id).Should().BeFalse();
        _db.AlertReferences.Any(a => a.SourceKey == alert.SourceKey).Should().BeFalse();
    }

    /// <summary>TryAssignIssueReferenzIfNoneAsync speichert eine IssueReferenz, wenn noch keine existiert.</summary>
    [Fact]
    public async Task TryAssignIssueReferenzIfNoneAsync_ShouldPersistIssueReference_WhenNoneExists()
    {
        var workItem = await _sut.CreateAsync(_projektId, "Issue-Anlage", null, ct: TestContext.Current.CancellationToken);
        var issue = new Issue(23, "Neu", "Body", ["feature"], "v1", "https://example.test/23");

        var assigned = await _sut.TryAssignIssueReferenceIfNoneAsync(workItem.Id, issue, TestContext.Current.CancellationToken);

        assigned.Should().BeTrue();
        var reloaded = await _sut.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);
        reloaded!.IssueReference.Should().NotBeNull();
        reloaded.IssueReference!.IssueNumber.Should().Be(23);
        reloaded.IssueReference.IssueUrl.Should().Be("https://example.test/23");
    }

    /// <summary>Kombinierte Issue-Zuweisung speichert Referenz und Beschreibung gemeinsam.</summary>
    [Fact]
    public async Task TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync_ShouldPersistReferenceAndDescription_WhenNoneExists()
    {
        var workItem = await _sut.CreateAsync(_projektId, "Issue-Anlage", "Alte Beschreibung", ct: TestContext.Current.CancellationToken);
        var issue = new Issue(23, "Neu", "Issue Body", ["feature"], "v1", "https://example.test/23");

        var assigned = await _sut.TryAssignIssueReferenceAndUpdateDescriptionIfNoneAsync(workItem.Id, issue, true, "Neue Beschreibung", TestContext.Current.CancellationToken);

        assigned.Should().BeTrue();
        var reloaded = await _sut.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);
        reloaded!.IssueReference!.IssueNumber.Should().Be(23);
        reloaded.RequirementDescription.Should().Be("Neue Beschreibung");
    }

    /// <summary>Kombinierte Issue-Zuweisung behält die Beschreibung bei deaktivierter Option unverändert.</summary>
    [Fact]
    public async Task TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync_ShouldKeepDescription_WhenUpdateFlagIsFalse()
    {
        var workItem = await _sut.CreateAsync(_projektId, "Issue-Anlage", "Alte Beschreibung", ct: TestContext.Current.CancellationToken);
        var issue = new Issue(23, "Neu", "Issue Body", [], null, "https://example.test/23");

        var assigned = await _sut.TryAssignIssueReferenceAndUpdateDescriptionIfNoneAsync(workItem.Id, issue, false, "Neue Beschreibung", TestContext.Current.CancellationToken);

        assigned.Should().BeTrue();
        var reloaded = await _sut.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);
        reloaded!.IssueReference!.IssueNumber.Should().Be(23);
        reloaded.RequirementDescription.Should().Be("Alte Beschreibung");
    }

    /// <summary>Kombinierte Issue-Zuweisung überschreibt bei vorhandener Referenz weder Referenz noch Beschreibung.</summary>
    [Fact]
    public async Task TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync_ShouldReturnFalseAndKeepDescription_WhenReferenceExists()
    {
        var workItem = await _sut.CreateAsync(_projektId, "Issue-Anlage", "Alte Beschreibung", ct: TestContext.Current.CancellationToken);
        var existingIssue = new Issue(99, "Vorhanden", "Body", [], null, "https://example.test/99");
        var createdIssue = new Issue(23, "Neu", "Body", [], null, "https://example.test/23");
        await _sut.UpdateIssueReferenceAsync(workItem.Id, existingIssue, TestContext.Current.CancellationToken);

        var assigned = await _sut.TryAssignIssueReferenceAndUpdateDescriptionIfNoneAsync(workItem.Id, createdIssue, true, "Neue Beschreibung", TestContext.Current.CancellationToken);

        assigned.Should().BeFalse();
        var reloaded = await _sut.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);
        reloaded!.IssueReference!.IssueNumber.Should().Be(99);
        reloaded.RequirementDescription.Should().Be("Alte Beschreibung");
    }

    /// <summary>TryAssignIssueReferenzIfNoneAsync überschreibt keine vorhandene IssueReferenz.</summary>
    [Fact]
    public async Task TryAssignIssueReferenzIfNoneAsync_ShouldReturnFalseAndKeepExistingReference_WhenReferenceExists()
    {
        var workItem = await _sut.CreateAsync(_projektId, "Issue-Anlage", null, ct: TestContext.Current.CancellationToken);
        var existingIssue = new Issue(99, "Vorhanden", "Body", [], null, "https://example.test/99");
        var createdIssue = new Issue(23, "Neu", "Body", [], null, "https://example.test/23");
        await _sut.UpdateIssueReferenceAsync(workItem.Id, existingIssue, TestContext.Current.CancellationToken);

        var assigned = await _sut.TryAssignIssueReferenceIfNoneAsync(workItem.Id, createdIssue, TestContext.Current.CancellationToken);

        assigned.Should().BeFalse();
        var reloaded = await _sut.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);
        reloaded!.IssueReference!.IssueNumber.Should().Be(99);
        reloaded.IssueReference.IssueUrl.Should().Be("https://example.test/99");
    }

    /// <summary>UpdateIssueReferenzAsync behält die bestehende Issue-zuweisen-Semantik bei und überschreibt vorhandene Referenzen.</summary>
    [Fact]
    public async Task UpdateIssueReferenzAsync_ShouldOverwriteExistingReference_WhenReferenceExists()
    {
        var workItem = await _sut.CreateAsync(_projektId, "Issue-Zuweisung", null, ct: TestContext.Current.CancellationToken);
        var existingIssue = new Issue(99, "Vorhanden", "Body", [], null, "https://example.test/99");
        var selectedIssue = new Issue(42, "Auswahl", "Body", [], null, "https://example.test/42");
        await _sut.UpdateIssueReferenceAsync(workItem.Id, existingIssue, TestContext.Current.CancellationToken);

        await _sut.UpdateIssueReferenceAsync(workItem.Id, selectedIssue, TestContext.Current.CancellationToken);

        var reloaded = await _sut.GetDetailAsync(workItem.Id, TestContext.Current.CancellationToken);
        reloaded!.IssueReference!.IssueNumber.Should().Be(42);
        reloaded.IssueReference.IssueUrl.Should().Be("https://example.test/42");
    }

    /// <summary>GetByProjektAsync gibt alle Aufgaben eines Projekts zurück.</summary>
    [Fact]
    public async Task GetByProjektAsync_ShouldReturnAufgabenForProjekt_WhenAufgabenExist()
    {
        // Arrange
        await _sut.CreateAsync(_projektId, "Aufgabe 1", null, ct: TestContext.Current.CancellationToken);
        await _sut.CreateAsync(_projektId, "Aufgabe 2", null, ct: TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetByProjectAsync(_projektId, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
    }

    /// <summary>StartenAsync setzt Status auf Gestartet und setzt Branch und KlonPfad.</summary>
    [Fact]
    public async Task StartenAsync_ShouldSetStatusGestartetAndBranchName_WhenAufgabeExists()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Zu startende Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        await _sut.StartAsync(workItem.Id, "feature/test-branch", "/tmp/klon", ct: TestContext.Current.CancellationToken);

        // Assert
        var result = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        result!.Status.Should().Be(WorkItemStatus.Started);
        result.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Active);
        result.BranchName.Should().Be("feature/test-branch");
        result.LocalClonePath.Should().Be("/tmp/klon");
    }

    /// <summary>StartenAsync sperrt terminale Gesamtstatus unabhängig vom Ausführungsstatus.</summary>
    [Fact]
    public async Task StartenAsync_ShouldThrow_WhenAufgabeBeendetIst()
    {
        var workItem = await _sut.CreateAsync(_projektId, "Beendete Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _sut.CompleteAsync(workItem.Id, TestContext.Current.CancellationToken);

        var act = () => _sut.StartAsync(workItem.Id, "feature/test", "/tmp/klon");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Beendete oder archivierte Aufgaben können nicht gestartet werden.");
    }

    /// <summary>GetLatestDiffResultIdForFileAsync liefert den neuesten dateispezifischen Diff auch bei unterschiedlicher Pfadnotation.</summary>
    [Fact]
    public async Task GetLatestDiffResultIdForFileAsync_ShouldReturnNewestMatchingDiff_WhenPathUsesDifferentSeparators()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Diff-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        var older = CreateDiffResult(workItem.Id, @"src\module\alpha.cs", DateTimeOffset.UtcNow.AddMinutes(-5));
        var newer = CreateDiffResult(workItem.Id, "src/module/alpha.cs", DateTimeOffset.UtcNow);
        var otherFile = CreateDiffResult(workItem.Id, "src/module/beta.cs", DateTimeOffset.UtcNow.AddMinutes(1));

        _db.DiffResults.AddRange(older, newer, otherFile);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetLatestDiffResultIdForFileAsync(workItem.Id, "./src/module/alpha.cs", TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(newer.Id);
    }

    /// <summary>GetLatestDiffResultIdForFileAsync liefert null, wenn kein Diff für die Datei vorhanden ist.</summary>
    [Fact]
    public async Task GetLatestDiffResultIdForFileAsync_ShouldReturnNull_WhenNoDiffForFileExists()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Diff-Aufgabe ohne Treffer", null, ct: TestContext.Current.CancellationToken);
        _db.DiffResults.Add(CreateDiffResult(workItem.Id, "src/module/existing.cs", DateTimeOffset.UtcNow));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetLatestDiffResultIdForFileAsync(workItem.Id, "src/module/missing.cs", TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>StatusSetzenAsync setzt Status auf Gestartet via generische Methode.</summary>
    [Fact]
    public async Task StatusSetzenAsync_ShouldSetStatusGestartet_WhenAufgabeExists()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "KI-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        await _sut.SetStatusUncheckedAsync(workItem.Id, WorkItemStatus.Started, TestContext.Current.CancellationToken);

        // Assert
        var result = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        result!.Status.Should().Be(WorkItemStatus.Started);
    }

    /// <summary>AbschliessenAsync setzt Status auf Beendet und setzt AbschlussDatum.</summary>
    [Fact]
    public async Task AbschliessenAsync_ShouldSetStatusBeendetAndSetAbschlussDatum_WhenAufgabeExists()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Abzuschließende Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        await _sut.CompleteAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        var result = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        result!.Status.Should().Be(WorkItemStatus.Completed);
        result.ExecutionStatus.Should().Be(WorkItemExecutionStatus.Completed);
        result.CompletedAt.Should().NotBeNull();
    }

    /// <summary>UpdateAsync aktualisiert Titel, Agenteninfos und KI-Plugin.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldUpdateTitelAndAgentenInfos_WhenAufgabeExists()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Alter Titel", "Alte Beschreibung", ct: TestContext.Current.CancellationToken);

        // Act
        await _sut.UpdateAsync(workItem.Id, "Neuer Titel", "Neue Beschreibung", "Softwareschmiede.Ki", TestContext.Current.CancellationToken);

        // Assert
        var result = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        result!.Title.Should().Be("Neuer Titel");
        result.RequirementDescription.Should().Be("Neue Beschreibung");
        result.AiPluginPrefix.Should().Be("Softwareschmiede.Ki");
    }

    /// <summary>DeleteAsync entfernt die Aufgabe aus der Datenbank.</summary>
    [Fact]
    public async Task DeleteAsync_ShouldRemoveAufgabe_WhenAufgabeExists()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Zu löschende Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        await _sut.DeleteAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        var result = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }

    /// <summary>SavePromptVorschlagAsync speichert Prompt und Ausführungszeitpunkt.</summary>
    [Fact]
    public async Task SavePromptVorschlagAsync_ShouldPersistPromptAndSchedule_WhenValuesAreProvided()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Prompt-Vorschlag", null, ct: TestContext.Current.CancellationToken);
        var executeAfterUtc = new DateTimeOffset(2026, 06, 01, 11, 50, 0, TimeSpan.Zero);

        // Act
        await _sut.SavePromptSuggestionAsync(workItem.Id, "Mach nun bitte weiter.", executeAfterUtc, TestContext.Current.CancellationToken);

        // Assert
        var result = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        result!.SuggestedPrompt.Should().Be("Mach nun bitte weiter.");
        result.SuggestedExecuteAfterUtc.Should().Be(executeAfterUtc);
    }

    /// <summary>SavePromptVorschlagAsync entfernt Zeitstempel, wenn der Prompt leer ist.</summary>
    [Fact]
    public async Task SavePromptVorschlagAsync_ShouldClearSchedule_WhenPromptIsWhitespace()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Prompt-Vorschlag löschen", null, ct: TestContext.Current.CancellationToken);
        await _sut.SavePromptSuggestionAsync(workItem.Id, "Mach nun bitte weiter.", new DateTimeOffset(2026, 06, 01, 11, 50, 0, TimeSpan.Zero), TestContext.Current.CancellationToken);

        // Act
        await _sut.SavePromptSuggestionAsync(workItem.Id, "  ", new DateTimeOffset(2026, 06, 01, 12, 0, 0, TimeSpan.Zero), TestContext.Current.CancellationToken);

        // Assert
        var result = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        result!.SuggestedPrompt.Should().BeNull();
        result.SuggestedExecuteAfterUtc.Should().BeNull();
    }

    /// <summary>ClearPromptVorschlagAsync entfernt Prompt und Ausführungszeitpunkt vollständig.</summary>
    [Fact]
    public async Task ClearPromptVorschlagAsync_ShouldRemovePromptAndSchedule_WhenValuesExist()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Prompt-Vorschlag entfernen", null, ct: TestContext.Current.CancellationToken);
        await _sut.SavePromptSuggestionAsync(workItem.Id, "Mach nun bitte weiter.", new DateTimeOffset(2026, 06, 01, 11, 50, 0, TimeSpan.Zero), TestContext.Current.CancellationToken);

        // Act
        await _sut.ClearPromptSuggestionAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        var result = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        result!.SuggestedPrompt.Should().BeNull();
        result.SuggestedExecuteAfterUtc.Should().BeNull();
    }

    /// <summary>VerwerfenAsync archiviert eine neue Aufgabe.</summary>
    [Fact]
    public async Task VerwerfenAsync_ShouldSetStatusArchiviert_WhenNeueAufgabeArchiviertWird()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Zu verwerfende Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        await _sut.DiscardAsync(workItem.Id, DiscardAction.Archive, TestContext.Current.CancellationToken);

        // Assert
        var result = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        result!.Status.Should().Be(WorkItemStatus.Archived);
    }

    /// <summary>VerwerfenAsync löscht eine neue Aufgabe dauerhaft.</summary>
    [Fact]
    public async Task VerwerfenAsync_ShouldRemoveAufgabe_WhenNeueAufgabeGeloeschtWird()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Zu verwerfende Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        await _sut.DiscardAsync(workItem.Id, DiscardAction.Delete, TestContext.Current.CancellationToken);

        // Assert
        var result = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }

    /// <summary>VerwerfenAsync wirft eine Ausnahme, wenn die Aufgabe nicht neu ist.</summary>
    [Fact]
    public async Task VerwerfenAsync_ShouldThrowInvalidOperationException_WhenAufgabeIsNotNeu()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Bereits gestartete Aufgabe", null, ct: TestContext.Current.CancellationToken);
        await _sut.StartAsync(workItem.Id, "feature/test", @"C:\klon", ct: TestContext.Current.CancellationToken);

        // Act
        var act = () => _sut.DiscardAsync(workItem.Id, DiscardAction.Archive);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Nur neue Aufgaben können verworfen werden.");
    }

    /// <summary>StartenAsync wirft InvalidOperationException wenn Aufgabe nicht gefunden.</summary>
    [Fact]
    public async Task StartenAsync_ShouldThrowInvalidOperationException_WhenAufgabeDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = () => _sut.StartAsync(nonExistentId, "branch", "/path");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>StatusSetzenAsync setzt den Status auf den angegebenen Wert.</summary>
    [Fact]
    public async Task StatusSetzenAsync_ShouldSetGivenStatus_WhenAufgabeExists()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Status-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        await _sut.SetStatusUncheckedAsync(workItem.Id, WorkItemStatus.Completed, TestContext.Current.CancellationToken);

        // Assert
        var result = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        result!.Status.Should().Be(WorkItemStatus.Completed);
    }

    /// <summary>GetHeartbeatAgeMinutesAsync berechnet das Alter bei gesetztem Heartbeat korrekt.
    /// Der Heartbeat wird direkt an der Entity gesetzt (UpdateHeartbeatAsync wurde als toter Code
    /// entfernt — produktiv wird LastHeartbeatUtc ausschließlich über die LaufGates-serialisierten
    /// Laufpfade AktivenLaufSetzen-/Sicherstellen-/BeendenAsync geschrieben, Issue 342).</summary>
    [Fact]
    public async Task GetHeartbeatAgeMinutesAsync_BerechnetAlter_BeiGesetztemHeartbeat()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Heartbeat-Aufgabe", null, ct: TestContext.Current.CancellationToken);
        workItem.LastHeartbeatUtc.Should().BeNull();

        // Act – Heartbeat direkt an der Entity setzen
        var vorUpdate = DateTimeOffset.UtcNow;
        var tracked = await _db.WorkItems.FindAsync(new object?[] { workItem.Id }, TestContext.Current.CancellationToken);
        tracked!.LastHeartbeatUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var nachUpdate = DateTimeOffset.UtcNow;

        // Assert – Heartbeat wurde gesetzt
        var geladen = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        geladen!.LastHeartbeatUtc.Should().NotBeNull();
        geladen.LastHeartbeatUtc!.Value.Should().BeOnOrAfter(vorUpdate.AddSeconds(-1));
        geladen.LastHeartbeatUtc!.Value.Should().BeOnOrBefore(nachUpdate.AddSeconds(1));
        geladen.LastCliStartUtc.Should().BeNull("ein Heartbeat ist kein echter CLI-Start");

        // Alter ist 0 Minuten (frischer Heartbeat)
        var ageMinutes = await _sut.GetHeartbeatAgeMinutesAsync(workItem.Id, TestContext.Current.CancellationToken);
        ageMinutes.Should().NotBeNull();
        ageMinutes!.Value.Should().BeGreaterThanOrEqualTo(0);
        ageMinutes!.Value.Should().BeLessThan(2);
    }

    /// <summary>GetHeartbeatAgeMinutesAsync gibt null zurück wenn kein Heartbeat gesetzt.</summary>
    [Fact]
    public async Task GetHeartbeatAgeMinutesAsync_ShouldReturnNull_WhenNoHeartbeatSet()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "No-Heartbeat-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetHeartbeatAgeMinutesAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>TestStatusValidation: SetStatusAsync wirft InvalidStatusTransitionException bei ungültigem Übergang.</summary>
    [Fact]
    public async Task TestStatusValidation()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Validierungs-Aufgabe", null, ct: TestContext.Current.CancellationToken);

        // Act – ungültiger Übergang Neu → Beendet
        var act = () => _sut.SetStatusAsync(workItem.Id, WorkItemStatus.Completed);
        // Assert
        await act.Should().ThrowAsync<InvalidStatusTransitionException>()
            .WithMessage("*Neu*Beendet*");
    }

    /// <summary>SetStatusAsync erlaubt gültige Übergänge ohne Exception.</summary>
    [Fact]
    public async Task SetStatusAsync_ShouldSetStatus_WhenTransitionIsAllowed()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Gültiger-Übergang", null, ct: TestContext.Current.CancellationToken);

        // Act
        await _sut.SetStatusAsync(workItem.Id, WorkItemStatus.Started, TestContext.Current.CancellationToken);

        // Assert
        var result = await _sut.GetByIdAsync(workItem.Id, TestContext.Current.CancellationToken);
        result!.Status.Should().Be(WorkItemStatus.Started);
    }

    /// <summary>GetAktiveAufgabenAsync gibt nur Aufgaben mit Status Gestartet oder Wartend zurück.</summary>
    [Fact]
    public async Task GetAktiveAufgabenAsync_ShouldReturnAufgabenWithStatusGestartetOrWartend_WhenCalled()
    {
        // Arrange
        _db.WorkItems.AddRange(
            CreateAufgabeDirekt("Neu-Aufgabe", WorkItemStatus.New),
            CreateAufgabeDirekt("Gestartet-Aufgabe", WorkItemStatus.Started),
            CreateAufgabeDirekt("Wartend-Aufgabe", WorkItemStatus.Waiting),
            CreateAufgabeDirekt("Beendet-Aufgabe", WorkItemStatus.Completed),
            CreateAufgabeDirekt("Archiviert-Aufgabe", WorkItemStatus.Archived));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.Status == WorkItemStatus.Started || a.Status == WorkItemStatus.Waiting);
    }

    /// <summary>GetAktiveAufgabenAsync sortiert nach LetzterCliStartUtc absteigend, mit Fallback auf ErstellungsDatum.</summary>
    [Fact]
    public async Task GetAktiveAufgabenAsync_ShouldSortByLetzterCliStartDescThenByErstellungsDatum_WhenCalled()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var alterStart = CreateAufgabeDirekt("Alter Start", WorkItemStatus.Started, erstellungsDatum: now.AddHours(-3), letzterCliStartUtc: now.AddMinutes(-10));
        var neuerStart = CreateAufgabeDirekt("Neuer Start", WorkItemStatus.Waiting, erstellungsDatum: now.AddHours(-2), letzterCliStartUtc: now.AddMinutes(-1));
        var ohneStart = CreateAufgabeDirekt("Ohne Start", WorkItemStatus.Started, erstellungsDatum: now.AddMinutes(-5), letzterCliStartUtc: null);

        _db.WorkItems.AddRange(alterStart, neuerStart, ohneStart);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Select(a => a.Id).Should().ContainInOrder(neuerStart.Id, ohneStart.Id, alterStart.Id);
    }

    /// <summary>GetAktiveAufgabenAsync bleibt stabil, wenn nur Heartbeats unterschiedlich sind.</summary>
    [Fact]
    public async Task GetAktiveAufgabenAsync_ShouldIgnoreLastHeartbeatForSorting_WhenLetzterCliStartExists()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var a = CreateAufgabeDirekt(
            "A alter Heartbeat",
            WorkItemStatus.Started,
            erstellungsDatum: now.AddHours(-2),
            lastHeartbeatUtc: now,
            letzterCliStartUtc: now.AddMinutes(-20));
        var b = CreateAufgabeDirekt(
            "B neuerer Start",
            WorkItemStatus.Started,
            erstellungsDatum: now.AddHours(-1),
            lastHeartbeatUtc: now.AddHours(-1),
            letzterCliStartUtc: now.AddMinutes(-5));

        _db.WorkItems.AddRange(a, b);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Select(x => x.Id).Should().ContainInOrder(b.Id, a.Id);
    }

    /// <summary>GetAktiveAufgabenAsync begrenzt die Ergebnisse auf maximal 20 Einträge.</summary>
    [Fact]
    public async Task GetAktiveAufgabenAsync_ShouldLimitTo20Results_WhenMoreThan20Exist()
    {
        // Arrange
        for (var i = 0; i < 25; i++)
            _db.WorkItems.Add(CreateAufgabeDirekt($"Aufgabe {i}", WorkItemStatus.Started));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(20);
    }

    /// <summary>GetAktiveAufgabenAsync lädt das zugehörige Projekt mit, damit der Projektname ohne Nachladen angezeigt werden kann.</summary>
    [Fact]
    public async Task GetAktiveAufgabenAsync_ShouldIncludeProjekt_WhenCalled()
    {
        // Arrange
        _db.WorkItems.Add(CreateAufgabeDirekt("Aufgabe mit Projekt", WorkItemStatus.Started));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle();
        result[0].Project.Should().NotBeNull();
        result[0].Project.Name.Should().Be("Testprojekt");
    }

    /// <summary>GetAktiveAufgabenAsync lädt das zugehörige GitRepository für die SCM-Plugin-Anzeige mit.</summary>
    [Fact]
    public async Task GetAktiveAufgabenAsync_ShouldIncludeGitRepository_WhenCalled()
    {
        // Arrange
        var repositoryId = Guid.NewGuid();
        _db.GitRepositories.Add(new GitRepository
        {
            Id = repositoryId,
            ProjectId = _projektId,
            RepositoryName = "repo",
            RepositoryUrl = "https://example.invalid/repo.git",
            PluginType = "Softwareschmiede.GitHub",
            Active = true
        });
        _db.WorkItems.Add(CreateAufgabeDirekt("Aufgabe mit Repository", WorkItemStatus.Started, gitRepositoryId: repositoryId));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetActiveWorkItemsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle();
        result[0].GitRepository.Should().NotBeNull();
        result[0].GitRepository!.PluginType.Should().Be("Softwareschmiede.GitHub");
    }

    /// <summary>CanCompleteTaskAsync gibt false zurück, wenn offene Todos existieren.</summary>
    [Fact]
    public async Task CanCompleteTaskAsync_WithOpenTodos_ReturnsFalse()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Aufgabe mit offenen Todos", null, ct: TestContext.Current.CancellationToken);
        _db.Todos.Add(new Todo
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItem.Id,
            Description = "Offenes Todo",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.CanCompleteTaskAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>CanCompleteTaskAsync gibt true zurück, wenn keine Todos existieren.</summary>
    [Fact]
    public async Task CanCompleteTaskAsync_WithoutTodos_ReturnsTrue()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Aufgabe ohne Todos", null, ct: TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.CanCompleteTaskAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>CanCompleteTaskAsync gibt true zurück, wenn nur erledigte Todos existieren.</summary>
    [Fact]
    public async Task CanCompleteTaskAsync_WithOnlyCompletedTodos_ReturnsTrue()
    {
        // Arrange
        var workItem = await _sut.CreateAsync(_projektId, "Aufgabe mit erledigten Todos", null, ct: TestContext.Current.CancellationToken);
        _db.Todos.Add(new Todo
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItem.Id,
            Description = "Erledigtes Todo",
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.CanCompleteTaskAsync(workItem.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    private WorkItem CreateAufgabeDirekt(
        string title,
        WorkItemStatus status,
        DateTimeOffset? erstellungsDatum = null,
        DateTimeOffset? lastHeartbeatUtc = null,
        DateTimeOffset? letzterCliStartUtc = null,
        Guid? gitRepositoryId = null) => new()
        {
            Id = Guid.NewGuid(),
            ProjectId = _projektId,
            GitRepositoryId = gitRepositoryId,
            Title = title,
            Status = status,
            CreatedAt = erstellungsDatum ?? DateTimeOffset.UtcNow,
            LastHeartbeatUtc = lastHeartbeatUtc,
            LastCliStartUtc = letzterCliStartUtc
        };

    private static DiffResult CreateDiffResult(Guid workItemId, string filePath, DateTimeOffset generatedAt)
    {
        var diffResultId = Guid.NewGuid();
        var blockId = Guid.NewGuid();

        return new DiffResult
        {
            Id = diffResultId,
            WorkItemId = workItemId,
            FilePath = filePath,
            SourceVersion = "HEAD~1",
            TargetVersion = "HEAD",
            Status = DiffResultStatus.Generated,
            DiffType = DiffType.Full,
            GeneratedAt = generatedAt,
            GeneratedBy = nameof(AufgabeServiceTests),
            AddedLines = 1,
            RemovedLines = 0,
            ModifiedLines = 0,
            LineCount = 1,
            DiffBlocks =
            [
                new DiffBlock
                {
                    Id = blockId,
                    DiffResultId = diffResultId,
                    BlockType = DiffBlockType.Added,
                    BlockSequence = 0,
                    SourceStartLine = 1,
                    SourceEndLine = 1,
                    TargetStartLine = 1,
                    TargetEndLine = 1,
                    DiffLines =
                    [
                        new DiffLine
                        {
                            Id = Guid.NewGuid(),
                            DiffBlockId = blockId,
                            LineStatus = DiffLineStatus.Added,
                            Content = "added line",
                            TargetLineNumber = 1,
                            LineSequence = 0,
                        },
                    ],
                },
            ],
        };
    }
}
