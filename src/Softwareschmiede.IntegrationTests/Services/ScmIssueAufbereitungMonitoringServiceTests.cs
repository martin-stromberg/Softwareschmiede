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
/// (<see cref="ScmIssueAufbereitungMonitoringService"/>): freigeschaltete Repositories werden
/// kanonisch unter dem Benutzerkonto enumeriert; neu angelegte Issues werden über den
/// Auswertungs-Checkpoint je Issue genau einmal eingelesen und als Zweck-Aufgabe samt internem,
/// trigger-freiem Ereignis eingereiht. Der Dienst läuft unabhängig von der Ereignisüberwachung
/// und stößt die Zustellung fälliger Ereignisse am Zyklusende selbst an.</summary>
public sealed class ScmIssueAufbereitungMonitoringServiceTests
{
    /// <summary>Happy Path (Akzeptanzkriterium a): Im Modus „Separates Konto“ wird ein nach dem
    /// Beobachtungsbeginn angelegtes Issue einer freigeschalteten Repository als Zweck-Aufgabe mit
    /// eigenem SourceKey-Namensraum und internem, trigger-freiem Ereignis eingereiht.</summary>
    [Fact]
    public async Task AufbereitungMonitor_SeparatesKonto_NewIssue_CreatesPreparationTaskAndInternalEvent()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await fixture.SetKontoModusAsync(ScmKontoModus.SeparatesKonto);
        await fixture.SetSettingAsync(AppEinstellungService.ScmIssueAufbereitungPluginKey, "custom.auf");
        var repositoryId = await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        var createdAt = DateTimeOffset.UtcNow.AddSeconds(1);
        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, createdAt)
            : null);

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var task = Assert.Single(await db.Aufgaben.Include(x => x.IssueReferenz).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmIssueAufbereitungsService.Zweck, task.Zweck);
        Assert.Equal(repositoryId, task.GitRepositoryId);
        Assert.Equal("custom.auf", task.KiPluginPrefix);
        Assert.Equal(9, task.IssueReferenz!.IssueNummer);
        Assert.EndsWith("/IssueAufbereitung/9", task.IssueReferenz.SourceKey);
        Assert.DoesNotContain("/Issue/9", task.IssueReferenz.SourceKey);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(task.Id, item.AufgabeId);
        Assert.Equal(ScmIssueAufbereitungsService.Zweck, item.Zweck);
        Assert.EndsWith("/IssueAufbereitung", item.EventKey);
        Assert.Contains(await db.Protokolleintraege.ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.AufgabeId == task.Id && x.Typ == ProtokollTyp.SystemMeldung && x.Inhalt.Contains("Issue-Aufbereitung"));
    }

    /// <summary>Erstlauf: Der Baseline-Checkpoint wird angelegt und alle enumerierten
    /// Issue-Referenzen erhalten Endzustand-Checkpoints — kein Retro-Scan, keine
    /// Beschreibungs-Reads, keine Aufgaben oder Ereignisse.</summary>
    [Fact]
    public async Task AufbereitungMonitor_FirstRun_SetsBaselineAndMarksAllReferencesEvaluated()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => DescriptionObservation(request, DateTimeOffset.UtcNow));
        fixture.ConfigureIssue();

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
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
        Assert.Empty(await db.Aufgaben.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Folgezyklen lesen ausgewertete Issues nicht erneut: Der Beschreibungs-Read
    /// beschränkt sich strikt auf Referenzen ohne Auswertungs-Checkpoint.</summary>
    [Fact]
    public async Task AufbereitungMonitor_RepeatedCycle_NoDescriptionReread()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);
        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, DateTimeOffset.UtcNow)
            : null);

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

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
    public async Task AufbereitungMonitor_Disabled_NoProviderWork()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.GetEventRepositoryAsync(
            It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Ohne Repository-Freischaltung (<see cref="GitRepository.IssueAufbereitungAktiv"/>)
    /// wird die Repository nicht einmal aufgelöst.</summary>
    [Fact]
    public async Task AufbereitungMonitor_RepositoryFlagDisabled_NoProviderWork()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: false);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.GetEventRepositoryAsync(
            It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Deaktivierte Ereignisüberwachung hindert den Aufbereitungs-Entdeckungszyklus
    /// nicht: beide Laufzeit-Schalter sind unabhängig (Backmerge-Präzedenz).</summary>
    [Fact]
    public async Task AufbereitungMonitor_EventMonitoringDisabled_AufbereitungStillRuns()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetBoolSettingAsync(AppEinstellungService.ScmMonitoringEnabledKey, false, TestContext.Current.CancellationToken);

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.GetEventRepositoryAsync(
            It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
        using var verification = fixture.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.True(await db.ScmObservationCheckpoints.AnyAsync(
            x => x.SourceKey == "issueaufbereitung/test-scm/github.com/R_1", cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Die Repository-Auflösung des Entdeckungszyklus läuft stets unter dem
    /// Benutzerkonto — auch wenn die Ereignisüberwachung auf „Separates Konto“ konfiguriert ist.</summary>
    [Fact]
    public async Task AufbereitungMonitor_RepositoryResolution_UsesBenutzerkontoContext()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        await fixture.SetKontoModusAsync(ScmKontoModus.SeparatesKonto);
        var contexts = new List<ScmRepositoryContext>();
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()))
            .Returns((ScmRepositoryContext context, CancellationToken _) =>
            {
                contexts.Add(context);
                return Task.FromResult(fixture.Repository);
            });

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        var context = Assert.Single(contexts);
        Assert.Equal(ScmKontoModus.Benutzerkonto, context.Konto);
    }

    /// <summary>Die persistierte Referenz des internen Aufbereitungsereignisses trägt den
    /// konfigurierten Überwachungsmodus — der Ausführungspfad wählt sein Credential über sie.</summary>
    [Fact]
    public async Task AufbereitungMonitor_SeparatesKonto_EventCarriesMonitoringKonto()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await fixture.SetKontoModusAsync(ScmKontoModus.SeparatesKonto);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, DateTimeOffset.UtcNow)
            : null);

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmKontoModus.SeparatesKonto, ScmEventStore.Observation(item).Reference.Repository.Konto);
    }

    /// <summary>Lückenschluss: Wurde die Beschreibung eines neu angelegten Issues zuvor schon
    /// gelesen (Erwähnungs-Vorgriff im SeparatesKonto schreibt einen
    /// <see cref="ScmContentSnapshot"/>), wird das Issue trotzdem aufbereitet.</summary>
    [Fact]
    public async Task AufbereitungMonitor_MentionPrereadIssue_StillPrepared()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await fixture.SetKontoModusAsync(ScmKontoModus.SeparatesKonto);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        await AddSnapshotAsync(fixture, DescriptionObservation(
            new ScmEventReadRequest(new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9),
                ScmContentKind.Description, null, DateTimeOffset.UtcNow), DateTimeOffset.UtcNow));
        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, DateTimeOffset.UtcNow)
            : null);

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var task = Assert.Single(await db.Aufgaben.Include(x => x.IssueReferenz).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmIssueAufbereitungsService.Zweck, task.Zweck);
        Assert.Equal(9, task.IssueReferenz!.IssueNummer);
        Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Ein Beschreibungs-Lesefehler isoliert nur das betroffene Issue: Es erhält einen
    /// Auswertungs-Checkpoint mit Fehler und Backoff, andere Issues werden weiter ausgewertet und
    /// der Enumerations-Watermark bleibt wirksam.</summary>
    [Fact]
    public async Task AufbereitungMonitor_ReadFailure_IsolatesSingleIssue()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

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

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var failed = Assert.Single(await db.ScmObservationCheckpoints
            .Where(x => x.SourceKey == "issueaufbereitung-issue/test-scm/github.com/R_1/9")
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("Temporary", failed.ErrorCode);
        Assert.True(failed.NextAttemptUtc > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Assert.True(failed.NextAttemptUtc < long.MaxValue);
        var task = Assert.Single(await db.Aufgaben.Include(x => x.IssueReferenz).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(10, task.IssueReferenz!.IssueNummer);
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
    public async Task AufbereitungMonitor_AliasResolutionFailure_IsolatesRepository()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        await AddRepositoryAsync(fixture, aufbereitung: true, url: "https://github.com/other/repo2");
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.Is<ScmRepositoryContext>(c => c.RepositoryId.Contains("other/repo2")), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScmProviderException(ScmProviderFailureKind.Permanent, "nicht gefunden"));
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
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
    public async Task AufbereitungMonitor_NonIssueReferences_Ignored()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.PullRequest, 5));
        fixture.ConfigureIssue();

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);
        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Empty(await db.Aufgaben.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Moduswechsel (Akzeptanzkriterium b): Ein über den Monitor-Pfad im Benutzerkonto
    /// aufbereitetes Issue erzeugt im SeparatesKonto keinen zweiten Auftrag — der
    /// Auswertungs-Checkpoint dedupliziert beide Erkennungspfade.</summary>
    [Fact]
    public async Task AufbereitungMonitor_ModeSwitch_NoDuplicateTasks()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        ConfigureDescriptions(fixture, request => DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));
        fixture.ConfigureIssue();

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        var createdAt = DateTimeOffset.UtcNow.AddSeconds(1);
        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, createdAt)
            : DescriptionObservation(request, DateTimeOffset.UtcNow.AddDays(-1)));

        await fixture.Monitor.RunOnceAsync(TestContext.Current.CancellationToken);
        await fixture.SetKontoModusAsync(ScmKontoModus.SeparatesKonto);
        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.Is<ScmEventReadRequest>(r => r.Reference.Repository.Konto == ScmKontoModus.SeparatesKonto),
            It.IsAny<CancellationToken>()), Times.Never);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var task = Assert.Single(await db.Aufgaben.Include(x => x.IssueReferenz)
            .Where(x => x.Zweck == ScmIssueAufbereitungsService.Zweck).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(9, task.IssueReferenz!.IssueNummer);
        Assert.Single(await db.ScmIncomingEvents
            .Where(x => x.Zweck == ScmIssueAufbereitungsService.Zweck).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Das eingereihte interne Ereignis wird noch im selben Zyklus zugestellt — auch bei
    /// deaktivierter Ereignisüberwachung, weil der Dienst die Zustellung selbst anstößt.</summary>
    [Fact]
    public async Task AufbereitungMonitor_EnqueuedEvent_IsDispatchedWithinCycle()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await fixture.SetSettingAsync(AppEinstellungService.ScmIssueAufbereitungPluginKey, "custom.auf");
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetBoolSettingAsync(AppEinstellungService.ScmMonitoringEnabledKey, false, TestContext.Current.CancellationToken);
        await AddRepositoryAsync(fixture, aufbereitung: true);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8));
        fixture.ConfigureIssue();

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        ConfigureEnumeration(fixture,
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 8),
            new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureDescriptions(fixture, request => request.Reference.Number == 9
            ? DescriptionObservation(request, DateTimeOffset.UtcNow)
            : null);

        await AufbereitungMonitor(fixture).RunOnceAsync(TestContext.Current.CancellationToken);

        using var verification = fixture.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ScmIssueAufbereitungsService.Zweck, item.Zweck);
        // „Aufgenommen" wäre der Zustand ohne den zyklusinternen Dispatch-Anstoß.
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("custom.auf", item.Reason);
    }

    private static ScmIssueAufbereitungMonitoringService AufbereitungMonitor(ScmDispatchFixture fixture)
        => fixture.Services.GetRequiredService<ScmIssueAufbereitungMonitoringService>();

    private static async Task EnableAsync(ScmDispatchFixture fixture)
        => await fixture.SetSettingAsync(AppEinstellungService.ScmIssueAufbereitungEnabledKey, "true");

    private static async Task<Guid> AddRepositoryAsync(ScmDispatchFixture fixture, bool aufbereitung,
        string url = "https://github.com/owner/repo", string? pluginTyp = null)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var project = new Projekt { Id = Guid.NewGuid(), Name = "Aufbereitung" };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Projekt = project,
            RepositoryUrl = url,
            RepositoryName = "repo",
            PluginTyp = pluginTyp ?? fixture.Repository.ProviderPrefix,
            Aktiv = true,
            IssueAufbereitungAktiv = aufbereitung
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
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
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
