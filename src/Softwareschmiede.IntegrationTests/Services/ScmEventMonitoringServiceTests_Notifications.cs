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

/// <summary>Integrationstests des Kontomodus „Separates Konto“: die Notifications-Inbox ersetzt die
/// repository-weite Enumeration; Verwerfungen werden sichtbar ohne Fehlalarm.</summary>
public sealed class ScmEventMonitoringServiceTests_Notifications
{
    /// <summary>Eine mention-Notification erzeugt Referenz → Read → Resolver-Aufgabe → Inbox-Ereignis;
    /// die repository-weite Enumeration läuft nicht.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_MentionNotification_EntersInboxViaResolver()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.ConfigureMentionObservations();
        fixture.ConfigureIssue();
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "mention", DateTimeOffset.UtcNow));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var observation = ScmEventStore.Observation(item);
        Assert.Equal(7, observation.Reference.Number);
        Assert.Equal(ScmAccountMode.SeparateAccount, observation.Reference.Repository.Account);
        Assert.NotEqual(Guid.Empty, item.WorkItemId);
        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
        var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey == "notifications/test-scm/github.com").ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.NotNull(checkpoint.WatermarkUtc);
        Assert.StartsWith(ScmEventStore.InboxDiscardDiagnosticPrefix, checkpoint.Diagnostic);
        Assert.Contains("1 verarbeitet, 0 verworfen", checkpoint.Diagnostic);
    }

    /// <summary>Referenzfall SeparatesKonto: Eine mention-Notification auf ein vom Workflow
    /// (github-actions) verfasstes Issue führt über die Vertrauensliste zu Aufgabenanlage
    /// und regulärer Zustellung — nicht zur Owner-Ablehnungsdiagnose.</summary>
    [Fact]
    public async Task Monitor_GitHubActionsAuthoredMention_CreatesTaskAndDispatches()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.ConfigureMentionObservations(authorId: "U_gha", authorLogin: "github-actions[bot]");
        fixture.ConfigureIssue();
        using (var settingsScope = fixture.Services.CreateScope())
            await settingsScope.ServiceProvider.GetRequiredService<PluginDefaultSettingsService>()
                .SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, ScmDispatchFixture.AiPrefix, TestContext.Current.CancellationToken);
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "mention", DateTimeOffset.UtcNow));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var task = Assert.Single(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(task.Id, item.WorkItemId);
        Assert.Equal("github-actions[bot]", ScmEventStore.Observation(item).AuthorLogin);
        Assert.Equal("Zugestellt", item.Status);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>Eine Notification eines nicht registrierten Repositorys wird verworfen; die
    /// Verwerfungsdiagnose trägt das Inbox-Präfix und legt keinen Backoff an.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_ForeignNotification_DiscardedWithDiagnostic()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.AddNotification(new ScmNotification("T_9", "R_FOREIGN", "github.com", "other", "other-repo",
            ScmReferenceKind.Issue, 5, "mention", DateTimeOffset.UtcNow));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.StartsWith("notifications/")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.StartsWith(ScmEventStore.InboxDiscardDiagnosticPrefix, checkpoint.Diagnostic);
        Assert.Contains("0 verarbeitet, 1 verworfen", checkpoint.Diagnostic);
        Assert.Null(checkpoint.NextAttemptUtc);
        Assert.Null(checkpoint.ErrorCode);
    }

    /// <summary>Nur reason=mention triggert; andere Gründe werden ignoriert.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_NonMentionReasons_AreIgnored()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.ConfigureMentionObservations();
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "review_requested", DateTimeOffset.UtcNow));
        fixture.AddNotification(new ScmNotification("T_2", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 8, "subscribed", DateTimeOffset.UtcNow));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Ein Inbox-Fehler geht in den eigenen Backoff-Checkpoint; der Watermark bleibt stehen,
    /// sodass die Notification im nächsten Fenster erneut gelesen wird.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_InboxFailure_KeepsWatermarkForRetry()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        fixture.ConfigureMentionObservations();
        fixture.Git.As<IScmNotificationSource>().Setup(x => x.EnumerateNotificationsAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScmProviderException(ScmProviderFailureKind.Temporary, "Verbindung unterbrochen."));
        var monitor = fixture.Monitor;

        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
                .Where(x => x.SourceKey.StartsWith("notifications/")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal("Temporary", checkpoint.ErrorCode);
            Assert.NotNull(checkpoint.NextAttemptUtc);
            Assert.Null(checkpoint.WatermarkUtc);
        }

        // Nach der Backoff-Zeit (NextAttemptUtc entfernen) wird die Inbox erneut gelesen.
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            var checkpoint = await db.ScmObservationCheckpoints.SingleAsync(x => x.SourceKey.StartsWith("notifications/"), cancellationToken: TestContext.Current.CancellationToken);
            checkpoint.NextAttemptUtc = null;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        fixture.Git.As<IScmNotificationSource>().Setup(x => x.EnumerateNotificationsAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScmNotification>());

        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
                .Where(x => x.SourceKey.StartsWith("notifications/")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.NotNull(checkpoint.WatermarkUtc);
            Assert.Null(checkpoint.ErrorCode);
            Assert.StartsWith(ScmEventStore.InboxDiscardDiagnosticPrefix, checkpoint.Diagnostic);
        }
    }

    /// <summary>Existiert der Inbox-Checkpoint bereits mit Default-Werten (z. B. aus einem
    /// früheren Teillauf), wird er beim Watermark-Commit aktualisiert statt erneut eingefügt.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_InboxCommit_WithPreexistingCheckpoint_UpdatesWatermark()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            db.Add(new ScmObservationCheckpoint { SourceKey = "notifications/test-scm/github.com" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var verify = fixture.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var checkpoint = Assert.Single(await db2.ScmObservationCheckpoints
            .Where(x => x.SourceKey == "notifications/test-scm/github.com").ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.NotNull(checkpoint.WatermarkUtc);
    }

    /// <summary>Node-IDs sind instanzlokal: Eine Notification von Host B mit einer node_id, die nur
    /// ein Repository auf Host A trägt, wird verworfen statt dem fremden Repository zugeordnet.</summary>
    [Fact]
    public async Task Monitor_SeparatesAccount_NotificationFromForeignHost_DiscardedDespiteMatchingNodeId()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture, "https://ghe-a.example.com/owner/repo-a", "repo-a");
        await AddRepositoryAsync(fixture, "https://ghe-b.example.com/owner/repo-b", "repo-b");
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        var repositoryA = new ScmEventRepository("test-scm", "ghe-a.example.com", "R_SAME",
            "owner", "repo-a", "U_1", "BOT", "scm-bot", true, ScmAccountMode.SeparateAccount);
        var repositoryB = new ScmEventRepository("test-scm", "ghe-b.example.com", "R_B",
            "owner", "repo-b", "U_1", "BOT", "scm-bot", true, ScmAccountMode.SeparateAccount);
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()))
            .Returns((ScmRepositoryContext context, CancellationToken _) => Task.FromResult(
                (context.RepositoryId.Contains("ghe-b") ? repositoryB : repositoryA) with { Account = context.Account }));
        fixture.Git.As<IScmNotificationSource>().Setup(x => x.EnumerateNotificationsAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .Returns((string host, DateTimeOffset? _, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<ScmNotification>>(host == "ghe-b.example.com"
                    ? new[] { new ScmNotification("T_1", "R_SAME", "ghe-b.example.com", "owner", "repo-x",
                        ScmReferenceKind.Issue, 9, "mention", DateTimeOffset.UtcNow) }
                    : Array.Empty<ScmNotification>()));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        var checkpoint = await db.ScmObservationCheckpoints
            .SingleAsync(x => x.SourceKey == "notifications/test-scm/ghe-b.example.com", cancellationToken: TestContext.Current.CancellationToken);
        Assert.StartsWith(ScmEventStore.InboxDiscardDiagnosticPrefix, checkpoint.Diagnostic);
        Assert.Contains("0 verarbeitet, 1 verworfen", checkpoint.Diagnostic);
    }

    /// <summary>Nach der Rückschaltung auf Benutzerkonto läuft die repository-weite Enumeration wieder.</summary>
    [Fact]
    public async Task Monitor_ModeSwitch_BackToEnumeration()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        ConfigureBotResolution(fixture);
        var monitor = fixture.Monitor;

        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        fixture.Git.As<IScmNotificationSource>().Verify(x => x.EnumerateNotificationsAsync(
            It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);

        await fixture.SetAccountModeAsync(ScmAccountMode.UserAccount);
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()))
            .Returns((ScmRepositoryContext context, CancellationToken _) =>
                Task.FromResult(fixture.Repository with { Account = context.Account }));

        await monitor.RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Git.As<IScmNotificationSource>().Verify(x => x.EnumerateNotificationsAsync(
            It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
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
