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
    private static readonly ScmEventRepository BotRepository = new("test-scm", "github.com", "R_1",
        "owner", "repo", "U_1", "BOT", "scm-bot", true, ScmKontoModus.SeparatesKonto);

    /// <summary>Eine mention-Notification erzeugt Referenz → Read → Resolver-Aufgabe → Inbox-Ereignis;
    /// die repository-weite Enumeration läuft nicht.</summary>
    [Fact]
    public async Task Monitor_SeparatesKonto_MentionNotification_EntersInboxViaResolver()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await SetKontoModusAsync(fixture, ScmKontoModus.SeparatesKonto);
        ConfigureBotResolution(fixture);
        ConfigureMentionObservations(fixture);
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadIssueAsync(It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScmEventReference reference, CancellationToken _) =>
                new Issue(reference.Number, "Titel", "Body", [], null, "https://github.com/owner/repo/issues/" + reference.Number));
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "mention", DateTimeOffset.UtcNow));

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
        var observation = ScmEventStore.Observation(item);
        Assert.Equal(7, observation.Reference.Number);
        Assert.Equal(ScmKontoModus.SeparatesKonto, observation.Reference.Repository.Konto);
        Assert.NotEqual(Guid.Empty, item.AufgabeId);
        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
        var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey == "notifications/test-scm/github.com").ToListAsync());
        Assert.NotNull(checkpoint.WatermarkUtc);
        Assert.StartsWith(ScmEventStore.InboxDiscardDiagnosticPrefix, checkpoint.Diagnostic);
        Assert.Contains("1 verarbeitet, 0 verworfen", checkpoint.Diagnostic);
    }

    /// <summary>Eine Notification eines nicht registrierten Repositorys wird verworfen; die
    /// Verwerfungsdiagnose trägt das Inbox-Präfix und legt keinen Backoff an.</summary>
    [Fact]
    public async Task Monitor_SeparatesKonto_ForeignNotification_DiscardedWithDiagnostic()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await SetKontoModusAsync(fixture, ScmKontoModus.SeparatesKonto);
        ConfigureBotResolution(fixture);
        fixture.AddNotification(new ScmNotification("T_9", "R_FOREIGN", "github.com", "other", "other-repo",
            ScmReferenceKind.Issue, 5, "mention", DateTimeOffset.UtcNow));

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync());
        var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.StartsWith("notifications/")).ToListAsync());
        Assert.StartsWith(ScmEventStore.InboxDiscardDiagnosticPrefix, checkpoint.Diagnostic);
        Assert.Contains("0 verarbeitet, 1 verworfen", checkpoint.Diagnostic);
        Assert.Null(checkpoint.NextAttemptUtc);
        Assert.Null(checkpoint.ErrorCode);
    }

    /// <summary>Nur reason=mention triggert; andere Gründe werden ignoriert.</summary>
    [Fact]
    public async Task Monitor_SeparatesKonto_NonMentionReasons_AreIgnored()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await SetKontoModusAsync(fixture, ScmKontoModus.SeparatesKonto);
        ConfigureBotResolution(fixture);
        ConfigureMentionObservations(fixture);
        fixture.AddNotification(new ScmNotification("T_1", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 7, "review_requested", DateTimeOffset.UtcNow));
        fixture.AddNotification(new ScmNotification("T_2", "R_1", "github.com", "owner", "repo",
            ScmReferenceKind.Issue, 8, "subscribed", DateTimeOffset.UtcNow));

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync());
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Ein Inbox-Fehler geht in den eigenen Backoff-Checkpoint; der Watermark bleibt stehen,
    /// sodass die Notification im nächsten Fenster erneut gelesen wird.</summary>
    [Fact]
    public async Task Monitor_SeparatesKonto_InboxFailure_KeepsWatermarkForRetry()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await SetKontoModusAsync(fixture, ScmKontoModus.SeparatesKonto);
        ConfigureBotResolution(fixture);
        ConfigureMentionObservations(fixture);
        fixture.Git.As<IScmNotificationSource>().Setup(x => x.EnumerateNotificationsAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScmProviderException(ScmProviderFailureKind.Temporary, "Verbindung unterbrochen."));
        var monitor = Monitor(fixture);

        await monitor.RunOnceAsync();

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
                .Where(x => x.SourceKey.StartsWith("notifications/")).ToListAsync());
            Assert.Equal("Temporary", checkpoint.ErrorCode);
            Assert.NotNull(checkpoint.NextAttemptUtc);
            Assert.Null(checkpoint.WatermarkUtc);
        }

        // Nach der Backoff-Zeit (NextAttemptUtc entfernen) wird die Inbox erneut gelesen.
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            var checkpoint = await db.ScmObservationCheckpoints.SingleAsync(x => x.SourceKey.StartsWith("notifications/"));
            checkpoint.NextAttemptUtc = null;
            await db.SaveChangesAsync();
        }
        fixture.Git.As<IScmNotificationSource>().Setup(x => x.EnumerateNotificationsAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScmNotification>());

        await monitor.RunOnceAsync();

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            var checkpoint = Assert.Single(await db.ScmObservationCheckpoints
                .Where(x => x.SourceKey.StartsWith("notifications/")).ToListAsync());
            Assert.NotNull(checkpoint.WatermarkUtc);
            Assert.Null(checkpoint.ErrorCode);
            Assert.StartsWith(ScmEventStore.InboxDiscardDiagnosticPrefix, checkpoint.Diagnostic);
        }
    }

    /// <summary>Existiert der Inbox-Checkpoint bereits mit Default-Werten (z. B. aus einem
    /// früheren Teillauf), wird er beim Watermark-Commit aktualisiert statt erneut eingefügt.</summary>
    [Fact]
    public async Task Monitor_SeparatesKonto_InboxCommit_WithPreexistingCheckpoint_UpdatesWatermark()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        await SetKontoModusAsync(fixture, ScmKontoModus.SeparatesKonto);
        ConfigureBotResolution(fixture);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            db.Add(new ScmObservationCheckpoint { SourceKey = "notifications/test-scm/github.com" });
            await db.SaveChangesAsync();
        }

        await Monitor(fixture).RunOnceAsync();

        using var verify = fixture.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var checkpoint = Assert.Single(await db2.ScmObservationCheckpoints
            .Where(x => x.SourceKey == "notifications/test-scm/github.com").ToListAsync());
        Assert.NotNull(checkpoint.WatermarkUtc);
    }

    /// <summary>Node-IDs sind instanzlokal: Eine Notification von Host B mit einer node_id, die nur
    /// ein Repository auf Host A trägt, wird verworfen statt dem fremden Repository zugeordnet.</summary>
    [Fact]
    public async Task Monitor_SeparatesKonto_NotificationFromForeignHost_DiscardedDespiteMatchingNodeId()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture, "https://ghe-a.example.com/owner/repo-a", "repo-a");
        await AddRepositoryAsync(fixture, "https://ghe-b.example.com/owner/repo-b", "repo-b");
        await SetKontoModusAsync(fixture, ScmKontoModus.SeparatesKonto);
        var repositoryA = new ScmEventRepository("test-scm", "ghe-a.example.com", "R_SAME",
            "owner", "repo-a", "U_1", "BOT", "scm-bot", true, ScmKontoModus.SeparatesKonto);
        var repositoryB = new ScmEventRepository("test-scm", "ghe-b.example.com", "R_B",
            "owner", "repo-b", "U_1", "BOT", "scm-bot", true, ScmKontoModus.SeparatesKonto);
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()))
            .Returns((ScmRepositoryContext context, CancellationToken _) => Task.FromResult(
                (context.RepositoryId.Contains("ghe-b") ? repositoryB : repositoryA) with { Konto = context.Konto }));
        fixture.Git.As<IScmNotificationSource>().Setup(x => x.EnumerateNotificationsAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .Returns((string host, DateTimeOffset? _, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<ScmNotification>>(host == "ghe-b.example.com"
                    ? new[] { new ScmNotification("T_1", "R_SAME", "ghe-b.example.com", "owner", "repo-x",
                        ScmReferenceKind.Issue, 9, "mention", DateTimeOffset.UtcNow) }
                    : Array.Empty<ScmNotification>()));

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync());
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        var checkpoint = await db.ScmObservationCheckpoints
            .SingleAsync(x => x.SourceKey == "notifications/test-scm/ghe-b.example.com");
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
        await SetKontoModusAsync(fixture, ScmKontoModus.SeparatesKonto);
        ConfigureBotResolution(fixture);
        var monitor = Monitor(fixture);

        await monitor.RunOnceAsync();
        fixture.Git.As<IScmNotificationSource>().Verify(x => x.EnumerateNotificationsAsync(
            It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);

        await SetKontoModusAsync(fixture, ScmKontoModus.Benutzerkonto);
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()))
            .Returns((ScmRepositoryContext context, CancellationToken _) =>
                Task.FromResult(fixture.Repository with { Konto = context.Konto }));

        await monitor.RunOnceAsync();

        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Git.As<IScmNotificationSource>().Verify(x => x.EnumerateNotificationsAsync(
            It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ScmEventMonitoringService Monitor(ScmDispatchFixture fixture)
        => fixture.Services.GetRequiredService<ScmEventMonitoringService>();

    private static async Task SetKontoModusAsync(ScmDispatchFixture fixture, ScmKontoModus modus)
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppEinstellungService>().SetScmKontoModusAsync(modus);
    }

    private static void ConfigureBotResolution(ScmDispatchFixture fixture)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()))
            .Returns((ScmRepositoryContext context, CancellationToken _) =>
                Task.FromResult(BotRepository with { Konto = context.Konto }));

    private static void ConfigureMentionObservations(ScmDispatchFixture fixture)
    {
        var time = DateTimeOffset.UtcNow;
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmEventReadRequest request, CancellationToken _) => Task.FromResult(new ScmEventBatch(request,
                request.ContentKind == ScmContentKind.IssueComment ? [new ScmEventObservation(request.Reference, request.ContentKind,
                    "C_" + request.Reference.Number, "42", time, time, null, "U_1", "owner", null, null,
                    "@scm-bot bitte übernehmen", "https://github.com/owner/repo/issues/" + request.Reference.Number, time)] : [])));
    }

    private static async Task AddRepositoryAsync(ScmDispatchFixture fixture,
        string repositoryUrl = "https://github.com/owner/repo", string repositoryName = "repo")
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var project = new Projekt { Id = Guid.NewGuid(), Name = "Inbox" };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Projekt = project,
            RepositoryUrl = repositoryUrl,
            RepositoryName = repositoryName,
            PluginTyp = fixture.Repository.ProviderPrefix,
            Aktiv = true
        };
        db.AddRange(project, repository);
        await db.SaveChangesAsync();
    }
}
