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

/// <summary>Modusunabhängiger Entdeckungszyklus der Issue-Aufbereitung
/// (<see cref="ScmIssuePreparationMonitoringService"/>): freigeschaltete Repositories werden
/// kanonisch unter dem Benutzerkonto enumeriert; neu angelegte Issues werden über den
/// Auswertungs-Checkpoint je Issue genau einmal eingelesen und als Zweck-Aufgabe samt internem,
/// trigger-freiem Ereignis eingereiht. Der Dienst läuft unabhängig von der Ereignisüberwachung
/// und stößt die Zustellung fälliger Ereignisse am Zyklusende selbst an.</summary>
public sealed class ScmIssuePreparationMonitoringServiceTests
{
    /// <summary>Happy Path (Akzeptanzkriterium a): Im Modus „Separates Konto“ wird ein nach dem
    /// Beobachtungsbeginn angelegtes Issue einer freigeschalteten Repository als Zweck-Aufgabe mit
    /// eigenem SourceKey-Namensraum und internem, trigger-freiem Ereignis eingereiht.</summary>
    [Fact]
    public async Task PreparationMonitor_SeparatesAccount_NewIssue_CreatesPreparationTaskAndInternalEvent()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        await fixture.SetSettingAsync(AppSettingService.ScmIssuePreparationPluginKey, "custom.auf");
        var repositoryId = await AddRepositoryAsync(fixture, preparation: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        var createdAt = DateTimeOffset.UtcNow.AddSeconds(1);
        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, createdAt)
            : null);

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var task = Assert.Single(await db.WorkItems.Include(x => x.IssueReference).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmIssuePreparationService.Purpose, task.Purpose);
        Assert.Equal(repositoryId, task.GitRepositoryId);
        Assert.Equal("custom.auf", task.AiPluginPrefix);
        Assert.Equal(9, task.IssueReference!.IssueNumber);
        Assert.EndsWith("/IssueAufbereitung/9", task.IssueReference.SourceKey);
        Assert.DoesNotContain("/Issue/9", task.IssueReference.SourceKey);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(task.Id, item.WorkItemId);
        Assert.Equal(ScmIssuePreparationService.Purpose, item.Purpose);
        Assert.EndsWith("/IssueAufbereitung", item.EventKey);
        Assert.Contains(await db.LogEntries.ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.WorkItemId == task.Id && x.Type == LogEntryType.SystemMessage && x.Content.Contains("Issue-Aufbereitung"));
    }

    /// <summary>Erstlauf: Der Baseline-Checkpoint wird angelegt und alle enumerierten
    /// Issue-Referenzen erhalten Endzustand-Checkpoints — kein Retro-Scan, keine
    /// Beschreibungs-Reads, keine Aufgaben oder Ereignisse.</summary>
    [Fact]
    public async Task PreparationMonitor_FirstRun_SetsBaselineAndMarksAllReferencesEvaluated()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, preparation: true);
        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => DescriptionObservation(request, DateTimeOffset.UtcNow));
        fixture.ConfigureIssue();

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var baseline = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.StartsWith("issueaufbereitung/")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("issueaufbereitung/test-scm/github.com/R_1", baseline.SourceKey);
        Assert.NotNull(baseline.WatermarkUtc);
        var marks = await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.StartsWith("issueaufbereitung-issue/")).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, marks.Count);
        Assert.All(marks, x =>
        {
            Assert.Equal(long.MaxValue, x.NextAttemptUtc);
            Assert.Null(x.ErrorCode);
        });
        Assert.Empty(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Folgezyklen lesen ausgewertete Issues nicht erneut: Der Beschreibungs-Read
    /// beschränkt sich strikt auf Referenzen ohne Auswertungs-Checkpoint.</summary>
    [Fact]
    public async Task PreparationMonitor_RepeatedCycle_NoDescriptionReread()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, preparation: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);
        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, DateTimeOffset.UtcNow)
            : null);

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.Is<ScmEventReadRequest>(r => r.Reference.Number == 9 && r.ContentKind == ScmContentKind.Description),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.Is<ScmEventReadRequest>(r => r.Reference.Number == 8),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Deaktivierter Funktionsschalter: Der Zyklus lässt jede Provider-Arbeit aus —
    /// weder Repository-Auflösung noch Enumeration oder Beschreibungs-Read.</summary>
    [Fact]
    public async Task PreparationMonitor_Disabled_NoProviderWork()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture, preparation: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.GetEventRepositoryAsync(
            It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Ohne Repository-Freischaltung (<see cref="GitRepository.IssuePreparationEnabled"/>)
    /// wird die Repository nicht einmal aufgelöst.</summary>
    [Fact]
    public async Task PreparationMonitor_RepositoryFlagDisabled_NoProviderWork()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, preparation: false);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.GetEventRepositoryAsync(
            It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Deaktivierte Ereignisüberwachung hindert den Aufbereitungs-Entdeckungszyklus
    /// nicht: beide Laufzeit-Schalter sind unabhängig (Backmerge-Präzedenz).</summary>
    [Fact]
    public async Task PreparationMonitor_EventMonitoringDisabled_PreparationStillRuns()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, preparation: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppSettingService>()
                .SetBoolSettingAsync(AppSettingService.ScmMonitoringEnabledKey, false, TestContext.Current.CancellationToken);

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.GetEventRepositoryAsync(
            It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
        using var verification = fixture.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.True(await db.ScmObservationCheckpoints.AnyAsync(
            x => x.SourceKey == "issueaufbereitung/test-scm/github.com/R_1", cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Die Repository-Auflösung des Entdeckungszyklus läuft stets unter dem
    /// Benutzerkonto — auch wenn die Ereignisüberwachung auf „Separates Konto“ konfiguriert ist.</summary>
    [Fact]
    public async Task PreparationMonitor_RepositoryResolution_UsesUseraccountContext()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, preparation: true);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        var contexts = new List<ScmRepositoryContext>();
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()))
            .Returns((ScmRepositoryContext context, CancellationToken _) =>
            {
                contexts.Add(context);
                return Task.FromResult(fixture.Repository);
            });

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        var context = Assert.Single(contexts);
        Assert.Equal(ScmAccountMode.UserAccount, context.Account);
    }

    /// <summary>Die persistierte Referenz des internen Aufbereitungsereignisses trägt den
    /// konfigurierten Überwachungsmodus — der Ausführungspfad wählt sein Credential über sie.</summary>
    [Fact]
    public async Task PreparationMonitor_SeparatesAccount_EventCarriesMonitoringAccount()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        await AddRepositoryAsync(fixture, preparation: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, DateTimeOffset.UtcNow)
            : null);

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmAccountMode.SeparateAccount, ScmEventStore.Observation(item).Reference.Repository.Account);
    }

    /// <summary>Im SeparatesKonto trägt der persistierte Snapshot die <em>vollständige</em>
    /// Identität des Ausführungskontos: ViewerId, ViewerLogin und CanWrite stammen aus der
    /// Neuauflösung unter <see cref="ScmAccountMode.SeparateAccount"/>, nicht aus der
    /// Benutzerkonto-Enumeration — nur so passiert die spätere Identitätsprüfung der
    /// Antwortausführung (<c>ExecuteResponseAsync</c>) den Umleitungsschutz.</summary>
    [Fact]
    public async Task PreparationMonitor_SeparatesAccount_EventCarriesExecutionViewer()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        await AddRepositoryAsync(fixture, preparation: true);
        var contexts = new List<ScmRepositoryContext>();
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()))
            .Returns((ScmRepositoryContext context, CancellationToken _) =>
            {
                contexts.Add(context);
                return Task.FromResult(context.Account == ScmAccountMode.SeparateAccount
                    ? ScmDispatchFixture.BotRepository : fixture.Repository);
            });
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, DateTimeOffset.UtcNow)
            : null);

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        var repository = ScmEventStore.Observation(item).Reference.Repository;
        Assert.Equal(ScmDispatchFixture.BotRepository.ViewerId, repository.ViewerId);
        Assert.Equal(ScmDispatchFixture.BotRepository.ViewerLogin, repository.ViewerLogin);
        Assert.Equal(ScmDispatchFixture.BotRepository.CanWrite, repository.CanWrite);
        Assert.Equal(ScmAccountMode.SeparateAccount, repository.Account);
        // Entdeckung unter Benutzerkonto, Neuauflösung unter dem Ausführungskonto.
        Assert.Contains(contexts, x => x.Account == ScmAccountMode.UserAccount);
        Assert.Contains(contexts, x => x.Account == ScmAccountMode.SeparateAccount);
    }

    /// <summary>Im Benutzerkonto-Modus findet keine Neuauflösung statt: Der Ausführungskontext
    /// wird nie angesprochen, der bisherige Snapshot bleibt unverändert.</summary>
    [Fact]
    public async Task PreparationMonitor_UserAccount_NoSecondResolution()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, preparation: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, DateTimeOffset.UtcNow)
            : null);

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.GetEventRepositoryAsync(
            It.Is<ScmRepositoryContext>(c => c.Account != ScmAccountMode.UserAccount),
            It.IsAny<CancellationToken>()), Times.Never);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Liefert die Ausführungskonto-Auflösung eine andere Repository-Identität als die
    /// Entdeckung, wird der Fehler ehrlich am Issue-Checkpoint vermerkt — kein Auftrag.</summary>
    [Fact]
    public async Task PreparationMonitor_ResolutionMismatch_RecordsIssueFailure()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        await AddRepositoryAsync(fixture, preparation: true);
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.Is<ScmRepositoryContext>(c => c.Account == ScmAccountMode.SeparateAccount), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScmDispatchFixture.BotRepository with { RepositoryId = "R_2" });
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, DateTimeOffset.UtcNow)
            : null);

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var failed = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey == "issueaufbereitung-issue/test-scm/github.com/R_1/9")
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("Permanent", failed.ErrorCode);
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Scheitert die Neuauflösung unter dem Ausführungskonto (fehlender Zugriff des
    /// separaten Kontos), landet der Fehler am Issue-Checkpoint statt als späterer
    /// „Identität geändert"-Alarm — kein Beschreibungs-Read, kein Auftrag.</summary>
    [Fact]
    public async Task PreparationMonitor_ResolutionFailure_RecordsIssueFailure()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        await AddRepositoryAsync(fixture, preparation: true);
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.Is<ScmRepositoryContext>(c => c.Account == ScmAccountMode.SeparateAccount), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScmProviderException(ScmProviderFailureKind.Permission, "Kein Zugriff."));
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, DateTimeOffset.UtcNow)
            : null);

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var failed = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey == "issueaufbereitung-issue/test-scm/github.com/R_1/9")
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("Permission", failed.ErrorCode);
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Lückenschluss: Wurde die Beschreibung eines neu angelegten Issues zuvor schon
    /// gelesen (Erwähnungs-Vorgriff im SeparatesKonto schreibt einen
    /// <see cref="ScmContentSnapshot"/>), wird das Issue trotzdem aufbereitet.</summary>
    [Fact]
    public async Task PreparationMonitor_MentionPrereadIssue_StillPrepared()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        await AddRepositoryAsync(fixture, preparation: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        await AddSnapshotAsync(fixture, DescriptionObservation(
            new ScmEventReadRequest(new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9),
                ScmContentKind.Description, null, DateTimeOffset.UtcNow), DateTimeOffset.UtcNow));
        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, DateTimeOffset.UtcNow)
            : null);

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var task = Assert.Single(await db.WorkItems.Include(x => x.IssueReference).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmIssuePreparationService.Purpose, task.Purpose);
        Assert.Equal(9, task.IssueReference!.IssueNumber);
        Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Ein Beschreibungs-Lesefehler isoliert nur das betroffene Issue: Es erhält einen
    /// Auswertungs-Checkpoint mit Fehler und Backoff, andere Issues werden weiter ausgewertet und
    /// der Enumerations-Watermark bleibt wirksam.</summary>
    [Fact]
    public async Task PreparationMonitor_ReadFailure_IsolatesSingleIssue()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, preparation: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 10));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 10
            ? DescriptionObservation(request, DateTimeOffset.UtcNow)
            : null);
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(
                It.Is<ScmEventReadRequest>(r => r.Reference.Number == 9), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScmProviderException(ScmProviderFailureKind.Temporary, "Lesefehler."));

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var failed = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey == "issueaufbereitung-issue/test-scm/github.com/R_1/9")
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("Temporary", failed.ErrorCode);
        Assert.True(failed.NextAttemptUtc > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Assert.True(failed.NextAttemptUtc < long.MaxValue);
        var task = Assert.Single(await db.WorkItems.Include(x => x.IssueReference).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(10, task.IssueReference!.IssueNumber);
        var enumeration = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.StartsWith("issueaufbereitung-enumeration/"))
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.NotNull(enumeration.WatermarkUtc);
        Assert.Null(enumeration.ErrorCode);
    }

    /// <summary>Ein Fehler der Repository-Auflösung isoliert nur diesen Alias: Der
    /// Auflösungs-Checkpoint erhält einen Backoff (kein dauerhafter Ausschluss), andere Aliase
    /// werden weiter enumeriert.</summary>
    [Fact]
    public async Task PreparationMonitor_AliasResolutionFailure_IsolatesRepository()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, preparation: true);
        await AddRepositoryAsync(fixture, preparation: true, url: "https://github.com/other/repo2");
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.Is<ScmRepositoryContext>(c => c.RepositoryId.Contains("other/repo2")), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScmProviderException(ScmProviderFailureKind.Permanent, "nicht gefunden"));
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var failed = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey.StartsWith("issueaufbereitung-repository/") && x.SourceKey.Contains("other"))
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("Permanent", failed.ErrorCode);
        Assert.True(failed.NextAttemptUtc > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Assert.True(failed.NextAttemptUtc < long.MaxValue);
        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Pull-Request-Referenzen lösen weder Beschreibungs-Read noch Auswertung aus.</summary>
    [Fact]
    public async Task PreparationMonitor_NonIssueReferences_Ignored()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, preparation: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        fixture.ConfigureIssue();

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);
        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Empty(await db.WorkItems.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Moduswechsel (Akzeptanzkriterium b): Ein über den Monitor-Pfad im Benutzerkonto
    /// aufbereitetes Issue erzeugt im SeparatesKonto keinen zweiten Auftrag — der
    /// Auswertungs-Checkpoint dedupliziert beide Erkennungspfade.</summary>
    [Fact]
    public async Task PreparationMonitor_ModeSwitch_NoDuplicateTasks()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, preparation: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        ConfigureDescriptions(fixture, request => DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));
        fixture.ConfigureIssue();

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        var createdAt = DateTimeOffset.UtcNow.AddSeconds(1);
        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, createdAt)
            : DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        await fixture.SetAccountModeAsync(ScmAccountMode.SeparateAccount);
        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.Is<ScmEventReadRequest>(r => r.Reference.Repository.Account == ScmAccountMode.SeparateAccount),
            It.IsAny<CancellationToken>()), Times.Never);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var task = Assert.Single(await db.WorkItems.Include(x => x.IssueReference)
            .Where(x => x.Purpose == ScmIssuePreparationService.Purpose).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(9, task.IssueReference!.IssueNumber);
        Assert.Single(await db.ScmIncomingEvents
            .Where(x => x.Purpose == ScmIssuePreparationService.Purpose).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Das eingereihte interne Ereignis wird noch im selben Zyklus zugestellt — auch bei
    /// deaktivierter Ereignisüberwachung, weil der Dienst die Zustellung selbst anstößt.</summary>
    [Fact]
    public async Task PreparationMonitor_EnqueuedEvent_IsDispatchedWithinCycle()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await fixture.SetSettingAsync(AppSettingService.ScmIssuePreparationPluginKey, "custom.auf");
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppSettingService>()
                .SetBoolSettingAsync(AppSettingService.ScmMonitoringEnabledKey, false, TestContext.Current.CancellationToken);
        await AddRepositoryAsync(fixture, preparation: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, DateTimeOffset.UtcNow)
            : null);

        await PreparationMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        using var verification = fixture.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmIssuePreparationService.Purpose, item.Purpose);
        // „Aufgenommen" wäre der Zustand ohne den zyklusinternen Dispatch-Anstoß.
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("custom.auf", item.Reason);
    }

    private static ScmIssuePreparationMonitoringService PreparationMonitor(ScmDispatchFixture fixture)
        => fixture.Services.GetRequiredService<ScmIssuePreparationMonitoringService>();

    private static async Task EnableAsync(ScmDispatchFixture fixture)
        => await fixture.SetSettingAsync(AppSettingService.ScmIssuePreparationEnabledKey, "true");

    private static async Task<Guid> AddRepositoryAsync(ScmDispatchFixture fixture, bool preparation,
        string url = "https://github.com/owner/repo", string? pluginType = null)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var project = new Project { Id = Guid.NewGuid(), Name = "Aufbereitung" };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Project = project,
            RepositoryUrl = url,
            RepositoryName = "repo",
            PluginType = pluginType ?? fixture.Repository.ProviderPrefix,
            Active = true,
            IssuePreparationEnabled = preparation
        };
        db.AddRange(project, repository);
        await db.SaveChangesAsync();
        return repository.Id;
    }

    private static void ConfigureEnumeration(ScmDispatchFixture fixture, params ScmEventReference[] references)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.EnumerateReferencesAsync(
                It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(references);

    private static void ConfigureDescriptions(ScmDispatchFixture fixture,
        Func<ScmEventReadRequest, ScmEventObservation?> descriptionFor)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(
                It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmEventReadRequest request, CancellationToken _) => Task.FromResult(new ScmEventBatch(request,
                descriptionFor(request) is { } observation ? new[] { observation } : Array.Empty<ScmEventObservation>())));

    private static ScmEventObservation DescriptionObservation(ScmEventReadRequest request,
        DateTimeOffset createdAt, string body = "Issue-Beschreibung")
        => new(request.Reference, ScmContentKind.Description, "I_" + request.Reference.Number, null,
            createdAt, createdAt, null, "U_1", "owner", null, null, body,
            "https://github.com/owner/repo/issues/" + request.Reference.Number, DateTimeOffset.UtcNow);

    /// <summary>Simuliert den Erwähnungs-Vorgriff des SeparatesKonto-Inbox-Pfads: Die Beschreibung
    /// wurde bereits beobachtet und besitzt einen <see cref="ScmContentSnapshot"/>.</summary>
    private static async Task AddSnapshotAsync(ScmDispatchFixture fixture, ScmEventObservation observation)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        db.ScmContentSnapshots.Add(new ScmContentSnapshot
        {
            ContentKey = observation.ContentKey,
            BodyHash = observation.BodyHash,
            Revision = observation.Revision.ToString("O"),
            IdentityResolved = true
        });
        await db.SaveChangesAsync();
    }
}
