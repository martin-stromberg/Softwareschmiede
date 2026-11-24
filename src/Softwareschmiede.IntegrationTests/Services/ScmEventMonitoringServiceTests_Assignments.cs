using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Integrationstests des Assignee-Auslösers im Kontomodus „Separates Konto“: eine
/// assign-Notification löst die Aufnahme der Referenzbeschreibung als Auftragsinhalt aus —
/// legitimiert über den Zuweisender-Kreis (Owner, überwachtes Konto, Vertrauensliste) statt
/// über den Texttrigger.</summary>
public sealed class ScmEventMonitoringServiceTests_Assignments
{
    /// <summary>Referenzfall: assign-Notification → Description-Lesung mit Zuweisungsdekoration →
    /// Resolver-Anlage → Zustellung; der ausgelieferte Prompt trägt den Herkunftshinweis der
    /// Assignee-Zuweisung und das Ereignis den persistierten Trigger.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_AssignNotification_CreatesTaskAndDispatches()
    {
        await using var fixture = new ScmDispatchFixture(assignmentSource: true);
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.ConfigureDescriptionObservations("Beschreibung ohne Trigger");
        fixture.ConfigureIssue(7);
        fixture.ConfigureAssignmentSource(new ScmAssignment("EV_1", DateTimeOffset.UtcNow, "U_1", "owner", "BOT", "scm-bot"));
        using (var settingsScope = fixture.Services.CreateScope())
            await settingsScope.ServiceProvider.GetRequiredService<PluginDefaultSettingsService>()
                .SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, ScmDispatchFixture.AiPrefix, TestContext.Current.CancellationToken);
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "assign", DateTimeOffset.UtcNow));

        await fixture.RunMonitorAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var task = Assert.Single(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(task.Id, item.WorkItemId);
        Assert.Equal("Zugestellt", item.Status);
        var observation = ScmEventStore.Observation(item);
        Assert.Equal(ScmContentKind.Description, observation.ContentKind);
        Assert.Equal(ScmNotification.AssignReason, observation.Trigger?.Reason);
        Assert.Equal("owner", observation.Trigger?.ActorLogin);
        Assert.Equal("EV_1", observation.Trigger?.EventNodeId);
        Assert.Contains("Assignee-Zuweisung", Assert.Single(fixture.Prompts));

        // Der Wiederanlauf-Abgleich rekonstruiert den assign-Auftrag bitgleich — der
        // Assignee-Präfix ist Bestandteil des unveränderlichen request.json — und führt
        // Annahme wie Abschluss regulär bis „Verarbeitet“.
        await File.WriteAllTextAsync(Path.Combine(item.ExchangePath!, "accepted.json"),
            JsonSerializer.Serialize(new ScmExchangeAccepted(1, item.Id,
                item.DeliveryAttemptId!.Value, Guid.Parse(item.CliRunId!)), TestJson.Options),
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(item.ExchangePath!, "complete.json"),
            JsonSerializer.Serialize(new ScmExchangeComplete(1, item.Id,
                item.DeliveryAttemptId!.Value, Guid.Parse(item.CliRunId!), [],
                NoRemoteAction: true, Reason: "Keine Remoteaktion erforderlich."), TestJson.Options),
            TestContext.Current.CancellationToken);
        // Erster Abgleich stabilisiert nur die Dateibeobachtung, zweiter verarbeitet sie.
        await fixture.DispatchAsync(item.Id);
        await fixture.DispatchAsync(item.Id);
        Assert.Equal(ScmIncomingEventStatus.Verarbeitet, (await fixture.EventAsync(item.Id)).Status);
    }

    /// <summary>Der assign-Auslöser gilt auch auf Pull Requests: die PR-Beschreibung wird über den
    /// Issue-Events-Pfad legitimiert und legt die Aufgabe mit PR-Verknüpfung an.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_AssignOnPullRequest_CreatesTask()
    {
        await using var fixture = new ScmDispatchFixture(assignmentSource: true);
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.ConfigureDescriptionObservations("PR Beschreibung ohne Trigger");
        fixture.ConfigurePullRequestRead(5);
        fixture.ConfigureAssignmentSource(new ScmAssignment("EV_1", DateTimeOffset.UtcNow, "U_1", "owner", "BOT", "scm-bot"));
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.PullRequest, 5, "assign", DateTimeOffset.UtcNow));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var task = Assert.Single(await db.WorkItems.Include(x => x.PullRequests)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var reference = Assert.Single(task.PullRequests);
        Assert.Equal(5, reference.PullRequestNumber);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(task.Id, item.WorkItemId);
        Assert.Equal(ScmReferenceKind.PullRequest, ScmEventStore.Observation(item).Reference.Kind);
    }

    /// <summary>Eine erneute Zuweisung trägt eine neue Ereignisidentität in den ContentKey —
    /// die unveränderte Beschreibung erzeugt ein zweites Ereignis auf derselben Aufgabe;
    /// ein weiterer Poll ohne neue Zuweisung bleibt dedupliziert.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_Reassignment_DeliversToExistingTask()
    {
        await using var fixture = new ScmDispatchFixture(assignmentSource: true);
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.ConfigureDescriptionObservations("Beschreibung ohne Trigger");
        fixture.ConfigureIssue(7);
        fixture.ConfigureAssignmentSource(new ScmAssignment("EV_1", DateTimeOffset.UtcNow, "U_1", "owner", "BOT", "scm-bot"));
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "assign", DateTimeOffset.UtcNow));
        var monitor = fixture.Monitor;

        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        fixture.ConfigureAssignmentSource(new ScmAssignment("EV_2", DateTimeOffset.UtcNow.AddHours(1), "U_1", "owner", "BOT", "scm-bot"));
        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var task = Assert.Single(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var events = await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, events.Count);
        Assert.All(events, x => Assert.Equal(task.Id, x.WorkItemId));
        Assert.Equal("EV_1", ScmEventStore.Observation(events[0]).Trigger?.EventNodeId);
        Assert.Equal("EV_2", ScmEventStore.Observation(events[1]).Trigger?.EventNodeId);

        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var verify = fixture.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Equal(2, await db2.ScmIncomingEvents.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Eine leere Referenzbeschreibung trägt keinen Auftragsinhalt: keine Aufgabe, kein
    /// Ereignis, kein Resolver-Aufruf — die Ablehnung steht auf dem Snapshot.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_AssignEmptyDescription_CreatesNoTask()
    {
        await using var fixture = new ScmDispatchFixture(assignmentSource: true);
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.ConfigureDescriptionObservations("   ");
        fixture.ConfigureAssignmentSource(new ScmAssignment("EV_1", DateTimeOffset.UtcNow, "U_1", "owner", "BOT", "scm-bot"));
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "assign", DateTimeOffset.UtcNow));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var snapshot = Assert.Single(await db.ScmContentSnapshots.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmEventValidationService.AssignEmptyDescriptionRejection, snapshot.Diagnostic);
        var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.EndsWith("/Issue/7/Description"))
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmEventStore.InboxDiscardDiagnosticPrefix + " " + ScmEventValidationService.AssignEmptyDescriptionRejection,
            checkpoint.Diagnostic);
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadIssueAsync(
            It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Eine Zuweisung durch ein Konto außerhalb von Owner, überwachtem Konto und
    /// Vertrauensliste wird mit Begründung verworfen; es entsteht keine Aufgabe.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_AssignByForeignLogin_Rejected()
    {
        await using var fixture = new ScmDispatchFixture(assignmentSource: true);
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.ConfigureDescriptionObservations("Beschreibung ohne Trigger");
        fixture.ConfigureAssignmentSource(new ScmAssignment("EV_1", DateTimeOffset.UtcNow, "U_9", "fremd", "BOT", "scm-bot"));
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "assign", DateTimeOffset.UtcNow));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var snapshot = Assert.Single(await db.ScmContentSnapshots.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmEventValidationService.AssignerNotAllowedRejection, snapshot.Diagnostic);
    }

    /// <summary>Gleichzeitige assign- und mention-Notification derselben Referenz: Die
    /// Zuweisung gewinnt die Deduplizierung — ihre Ablehnung (fremder Zuweisender)
    /// unterdrückt aber eine gültige Erwähnung in derselben Beschreibung nicht; der
    /// reguläre Texttrigger legitimiert Aufnahme und Aufgabe unabhängig.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_ForeignAssignWithMention_CreatesTaskViaTextTrigger()
    {
        await using var fixture = new ScmDispatchFixture(assignmentSource: true);
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.ConfigureDescriptionObservations("@scm-bot bitte übernehmen");
        fixture.ConfigureIssue(7);
        fixture.ConfigureAssignmentSource(new ScmAssignment("EV_1", DateTimeOffset.UtcNow, "U_9", "fremd", "BOT", "scm-bot"));
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "assign", DateTimeOffset.UtcNow));
        fixture.AddNotification(new ScmNotification("T_2", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "mention", DateTimeOffset.UtcNow));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var task = Assert.Single(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(task.Id, item.WorkItemId);
    }

    /// <summary>Liefert die Zuweisungsabfrage kein wirksames Viewer-Ereignis (z. B. assign →
    /// unassign vor der Verarbeitung), bleibt der Zuweisender unbestimmbar und die Aufnahme
    /// wird abgelehnt.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_AssignUndeterminable_Rejected()
    {
        await using var fixture = new ScmDispatchFixture(assignmentSource: true);
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.ConfigureDescriptionObservations("Beschreibung ohne Trigger");
        fixture.ConfigureAssignmentSource(null);
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "assign", DateTimeOffset.UtcNow));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var snapshot = Assert.Single(await db.ScmContentSnapshots.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmEventValidationService.AssignerUnknownRejection, snapshot.Diagnostic);
    }

    /// <summary>Implementiert das Plugin die optionale Fähigkeit <see cref="IScmAssignmentSource"/>
    /// nicht, bleibt der Zuweisender unbestimmbar: die assign-Notification wird mit
    /// Ablehnungsdiagnose verworfen statt eine Aufgabe auszulösen.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_AssignWithoutCapability_Rejected()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.ConfigureDescriptionObservations("Beschreibung ohne Trigger");
        // Kein ConfigureAssignmentSource: das Plugin-Mock trägt die Fähigkeit nicht.
        Assert.False(fixture.Git.Object is IScmAssignmentSource);
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "assign", DateTimeOffset.UtcNow));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var snapshot = Assert.Single(await db.ScmContentSnapshots.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmEventValidationService.AssignerUnknownRejection, snapshot.Diagnostic);
        var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.EndsWith("/Issue/7/Description"))
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmEventStore.InboxDiscardDiagnosticPrefix + " " + ScmEventValidationService.AssignerUnknownRejection,
            checkpoint.Diagnostic);
    }

    /// <summary>Vertrauenswürdige Automatisierungskonten (beide Schreibweisen von github-actions)
    /// sind berechtigte Zuweisende.</summary>
    [Theory]
    [InlineData("github-actions[bot]")]
    [InlineData("app/github-actions")]
    public async Task Monitor_SeparatesAccount_AssignByAutomation_Accepted(string assignerLogin)
    {
        await using var fixture = new ScmDispatchFixture(assignmentSource: true);
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.ConfigureDescriptionObservations("Beschreibung ohne Trigger");
        fixture.ConfigureIssue(7);
        fixture.ConfigureAssignmentSource(new ScmAssignment("EV_1", DateTimeOffset.UtcNow, null, assignerLogin, "BOT", "scm-bot"));
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "assign", DateTimeOffset.UtcNow));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var task = Assert.Single(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(task.Id, item.WorkItemId);
    }

    /// <summary>Ein Providerfehler der Zuweisungsabfrage geht in den Backoff des Description-
    /// Checkpoints; der Inbox-Watermark bleibt stehen, sodass die Notification im nächsten
    /// Fenster erneut gelesen und verarbeitet wird.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_AssignmentQueryFailure_KeepsInboxWatermarkForRetry()
    {
        await using var fixture = new ScmDispatchFixture(assignmentSource: true);
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.ConfigureDescriptionObservations("Beschreibung ohne Trigger");
        fixture.ConfigureIssue(7);
        fixture.Git.As<IScmAssignmentSource>().Setup(x => x.GetLatestViewerAssignmentAsync(
                It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScmProviderException(ScmProviderFailureKind.Temporary, "Verbindung unterbrochen."));
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "assign", DateTimeOffset.UtcNow));
        var monitor = fixture.Monitor;

        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
                .Where(x => x.SourceKey.EndsWith("/Issue/7/Description"))
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal("Temporary", checkpoint.ErrorCode);
            Assert.NotNull(checkpoint.NextAttemptUtc);
            // Der Inbox-Watermark wird bei Teilfehlern nicht committet — noch kein Inbox-Checkpoint.
            Assert.Empty(await db.ScmObservationCheckpoints
                .Where(x => x.SourceKey.StartsWith("notifications/"))
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
            checkpoint.NextAttemptUtc = null;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        fixture.ConfigureAssignmentSource(new ScmAssignment("EV_1", DateTimeOffset.UtcNow, "U_1", "owner", "BOT", "scm-bot"));

        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var verify = fixture.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var task = Assert.Single(await db2.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var item = Assert.Single(await db2.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(task.Id, item.WorkItemId);
        var inboxCheckpoint = Assert.Single(await db2.ScmObservationCheckpoints
            .Where(x => x.SourceKey.StartsWith("notifications/"))
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.NotNull(inboxCheckpoint.WatermarkUtc);
    }

    /// <summary>Liefert die Description-Lesung zu einer assign-Notification gar keinen Text
    /// (Batch leer — z. B. eine eigene, vom Plugin herausgefilterte Marker-Beschreibung),
    /// landet die generische Leer-Batch-Diagnose als Inbox-Diagnose auf dem Quellen-Checkpoint
    /// statt eines Snapshots.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_AssignEmptyDescriptionBatch_Discarded()
    {
        await using var fixture = new ScmDispatchFixture(assignmentSource: true);
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        // Kein ConfigureDescriptionObservations: die Quelle liefert eine leere Description-Liste.
        fixture.ConfigureAssignmentSource(new ScmAssignment("EV_1", DateTimeOffset.UtcNow, "U_1", "owner", "BOT", "scm-bot"));
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "assign", DateTimeOffset.UtcNow));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.EndsWith("/Issue/7/Description"))
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmEventStore.InboxDiscardDiagnosticPrefix + " " + ScmEventStore.EmptyDescriptionBatchDiagnostic,
            checkpoint.Diagnostic);
    }

    /// <summary>Zwei assign-Referenzen im selben Lauf: die konkrete Zuweisender-Ablehnung (#7)
    /// hebt sich als Inbox-präfixierte Diagnose auf den Quellen-Checkpoint; die Mehrdeutigkeit
    /// (#8) bleibt führend und trägt die kanonische Mehrdeutigkeitsdiagnose statt einer
    /// Assign-Begründung.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_AssignRejection_RaisesInboxCheckpointDiagnostic()
    {
        await using var fixture = new ScmDispatchFixture(assignmentSource: true);
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        // Zweite Aufgabe auf Issue 8 über einen SSH-Alias desselben Provider-Repositorys:
        // beide Aufgaben landen als Kandidaten in derselben Auflösung → Mehrdeutigkeit.
        await AddAmbiguousTaskAsync(fixture, "git@github.com:owner/repo.git");
        await AddAmbiguousTaskAsync(fixture, "https://github.com/owner/repo.git");
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.ConfigureDescriptionObservations("Beschreibung ohne Trigger");
        fixture.ConfigureIssue();
        fixture.Git.As<IScmAssignmentSource>().Setup(x => x.GetLatestViewerAssignmentAsync(
                It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScmEventReference reference, CancellationToken _) => new ScmAssignment("EV_" + reference.Number,
                DateTimeOffset.UtcNow, reference.Number == 7 ? "U_9" : "U_1",
                reference.Number == 7 ? "fremd" : "owner", "BOT", "scm-bot"));
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "assign", DateTimeOffset.UtcNow));
        fixture.AddNotification(new ScmNotification("T_2", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 8, "assign", DateTimeOffset.UtcNow));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var checkpoints = await db.ScmObservationCheckpoints.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        var rejected = Assert.Single(checkpoints, x => x.SourceKey.EndsWith("/Issue/7/Description"));
        Assert.Equal(ScmEventStore.InboxDiscardDiagnosticPrefix + " " + ScmEventValidationService.AssignerNotAllowedRejection,
            rejected.Diagnostic);
        var ambiguous = Assert.Single(checkpoints, x => x.SourceKey.EndsWith("/Issue/8/Description"));
        Assert.Equal(ScmEventStore.AmbiguousAssignmentDiagnostic, ambiguous.Diagnostic);
        var snapshots = await db.ScmContentSnapshots.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(ScmEventValidationService.AssignerNotAllowedRejection,
            Assert.Single(snapshots, x => x.ContentKey.Contains("/Issue/7/")).Diagnostic);
        Assert.Equal(ScmEventStore.AmbiguousAssignmentDiagnostic,
            Assert.Single(snapshots, x => x.ContentKey.Contains("/Issue/8/")).Diagnostic);
    }

    /// <summary>Legt eine zweite Aufgabe mit Issue-Referenz auf Nummer 8 an — über einen eigenen
    /// lokalen Repository-Alias desselben Provider-Repositorys (Auflösungsgruppe wie bei
    /// <see cref="AddRepositoryAsync"/>, aber zweite lokale Repository-Identität).</summary>
    private static async Task AddAmbiguousTaskAsync(ScmDispatchFixture fixture, string repositoryUrl)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var projectId = await db.Projects.Select(x => x.Id).FirstAsync();
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RepositoryUrl = repositoryUrl,
            RepositoryName = "repo",
            PluginType = fixture.Repository.ProviderPrefix,
            Active = true
        };
        var task = new WorkItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Kandidatenaufgabe " + repositoryUrl,
            Status = WorkItemStatus.Started,
            GitRepository = repository,
            IssueReference = new IssueReference { Id = Guid.NewGuid(), IssueNumber = 8, Title = "Issue 8" }
        };
        db.Add(task);
        await db.SaveChangesAsync();
    }

    private static void ConfigureBotResolution(ScmDispatchFixture fixture)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()))
            .Returns((ScmRepositoryContext context, CancellationToken _) =>
                Task.FromResult(ScmDispatchFixture.BotRepository with { Account = context.Account }));

    private static async Task AddRepositoryAsync(ScmDispatchFixture fixture,
        string repositoryUrl = "https://github.com/owner/repo", string repositoryName = "repo")
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var project = new Project { Id = Guid.NewGuid(), Name = "Inbox" };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Project = project,
            RepositoryUrl = repositoryUrl,
            RepositoryName = repositoryName,
            PluginType = fixture.Repository.ProviderPrefix,
            Active = true
        };
        db.AddRange(project, repository);
        await db.SaveChangesAsync();
    }
}
