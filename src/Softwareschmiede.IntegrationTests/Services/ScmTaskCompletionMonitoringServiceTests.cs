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

/// <summary>Abschlussüberwachung von Aufgaben mit SCM-Referenz: im Abschluss gemeldete Pull Requests
/// werden verknüpft, geschlossene/gemergte Referenzen beenden die Aufgabe nach der einstellbaren
/// Wartezeit. Der Prüfmodus bestimmt, ob nur ereignisgesteuerte oder alle Aufgaben mit Referenz
/// beobachtet werden; Aufgaben ohne gültige Issue-/Pull-Request-Referenz bleiben stets unberührt.</summary>
public sealed class ScmTaskCompletionMonitoringServiceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Ein im Abschluss gemeldeter Pull Request wird mit der Aufgabe als
    /// <see cref="PullRequestReferenz"/> verknüpft — Grundlage der Abschlussüberwachung.</summary>
    [Fact]
    public async Task Complete_ReportedPullRequestNumber_LinksReferenceToTask()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);

        await ProcessEventWithReportAsync(fixture, eventId, pullRequestNumber: 7);

        Assert.Equal("Verarbeitet", (await fixture.EventAsync(eventId)).Status);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var reference = Assert.Single(await db.PullRequestReferenzen.ToListAsync());
        Assert.Equal(taskId, reference.AufgabeId);
        Assert.Equal(7, reference.PullRequestNumber);
        Assert.Equal("owner/repo", reference.RepositoryId);
        Assert.Equal(PullRequestReferenzRolle.CreatedByTask, reference.Rolle);
        Assert.Equal("https://github.com/owner/repo/pull/7", reference.Url);
        Assert.NotNull((await db.Protokolleintraege.ToListAsync())
            .FirstOrDefault(x => x.Inhalt.Contains("Pull Request #7")));
    }

    /// <summary>Ein gemergter Pull Request startet den persistierten Countdown; nach Ablauf der
    /// Wartezeit werden die laufende CLI beendet und die Aufgabe auf Beendet gesetzt.</summary>
    [Fact]
    public async Task Monitor_MergedPullRequest_StopsCliAndCompletesTaskAfterWait()
    {
        var time = new TestTimeProvider();
        await using var fixture = new ScmDispatchFixture(time: time);
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await ProcessEventWithReportAsync(fixture, eventId, pullRequestNumber: 7);
        await SetWaitAsync(fixture, 5);
        SetupPullRequestStatus(fixture, PullRequestStatus.Merged);
        var monitor = fixture.Services.GetRequiredService<ScmTaskCompletionMonitoringService>();

        await monitor.RunOnceAsync();

        var task = await TaskAsync(fixture, taskId);
        Assert.Equal(AufgabeStatus.Gestartet, task.Status);
        Assert.NotNull(task.ScmAbschlussErkanntUtc);
        Assert.Equal("Pull Request #7", task.ScmAbschlussReferenz);
        Assert.False(fixture.Sessions[taskId].Object.Process.HasExited);

        time.Advance(TimeSpan.FromMinutes(5));
        await monitor.RunOnceAsync();

        task = await TaskAsync(fixture, taskId);
        Assert.Equal(AufgabeStatus.Beendet, task.Status);
        Assert.True(fixture.Sessions[taskId].Object.Process.HasExited);
    }

    /// <summary>Der automatische Abschluss zieht einen für die Aufgabe geplanten
    /// zeitgesteuerten Prompt zurück (Prompt-Abbruch läuft im einheitlichen Pass).</summary>
    [Fact]
    public async Task Monitor_MergedPullRequest_CancelsScheduledPromptOnComplete()
    {
        var time = new TestTimeProvider();
        await using var fixture = new ScmDispatchFixture(time: time);
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await ProcessEventWithReportAsync(fixture, eventId, pullRequestNumber: 7);
        await SetWaitAsync(fixture, 5);
        SetupPullRequestStatus(fixture, PullRequestStatus.Merged);
        var monitor = fixture.Services.GetRequiredService<ScmTaskCompletionMonitoringService>();
        var promptService = fixture.Services.GetRequiredService<PromptZeitVersandService>();

        await monitor.RunOnceAsync();
        Assert.NotNull((await TaskAsync(fixture, taskId)).ScmAbschlussErkanntUtc);

        await promptService.SchedulePromptAsync(taskId, "verspäteter Prompt", time.GetUtcNow().AddHours(1));
        Assert.NotNull(promptService.GetScheduledPromptStatus(taskId));

        time.Advance(TimeSpan.FromMinutes(5));
        await monitor.RunOnceAsync();

        Assert.Equal(AufgabeStatus.Beendet, (await TaskAsync(fixture, taskId)).Status);
        Assert.Null(promptService.GetScheduledPromptStatus(taskId));
    }

    /// <summary>Ein wieder geöffneter Pull Request setzt den laufenden Countdown zurück.</summary>
    [Fact]
    public async Task Monitor_ReopenedPullRequest_ResetsCountdown()
    {
        var time = new TestTimeProvider();
        await using var fixture = new ScmDispatchFixture(time: time);
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await ProcessEventWithReportAsync(fixture, eventId, pullRequestNumber: 7);
        await SetWaitAsync(fixture, 5);
        SetupPullRequestStatus(fixture, PullRequestStatus.Closed);
        var monitor = fixture.Services.GetRequiredService<ScmTaskCompletionMonitoringService>();

        await monitor.RunOnceAsync();
        Assert.NotNull((await TaskAsync(fixture, taskId)).ScmAbschlussErkanntUtc);

        SetupPullRequestStatus(fixture, PullRequestStatus.Open);
        await monitor.RunOnceAsync();

        var task = await TaskAsync(fixture, taskId);
        Assert.Null(task.ScmAbschlussErkanntUtc);
        Assert.Null(task.ScmAbschlussReferenz);
        Assert.Equal(AufgabeStatus.Gestartet, task.Status);
    }

    /// <summary>Ohne Pull-Request-Verknüpfung gilt der Zustand des verknüpften Issues:
    /// ein geschlossenes Issue beendet die Aufgabe nach der Wartezeit.</summary>
    [Fact]
    public async Task Monitor_ClosedIssue_WithoutPullRequest_CompletesTaskAfterWait()
    {
        var time = new TestTimeProvider();
        await using var fixture = new ScmDispatchFixture(time: time);
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await ProcessEventWithReportAsync(fixture, eventId, pullRequestNumber: null);
        await AddIssueReferenceAsync(fixture, taskId, 7);
        await SetWaitAsync(fixture, 5);
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadIssueAsync(
                It.Is<ScmEventReference>(r => r.Number == 7), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Issue(7, "Issue", null, [], null, null) { IstGeschlossen = true });
        var monitor = fixture.Services.GetRequiredService<ScmTaskCompletionMonitoringService>();

        await monitor.RunOnceAsync();

        var task = await TaskAsync(fixture, taskId);
        Assert.Equal(AufgabeStatus.Gestartet, task.Status);
        Assert.Equal("Issue #7", task.ScmAbschlussReferenz);

        time.Advance(TimeSpan.FromMinutes(5));
        await monitor.RunOnceAsync();

        Assert.Equal(AufgabeStatus.Beendet, (await TaskAsync(fixture, taskId)).Status);
    }

    /// <summary>Der Issue-Abschlusspfad läuft stets unter dem Benutzerkonto: Auch bei konfiguriertem
    /// SeparatesKonto der Ereignisüberwachung wird der Repository-Kontext der Abschlussprüfung mit
    /// <see cref="ScmKontoModus.Benutzerkonto"/> aufgebaut.</summary>
    [Fact]
    public async Task CompletionService_IssueCheck_UsesBenutzerkontoContext()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await ProcessEventWithReportAsync(fixture, eventId, pullRequestNumber: null);
        await AddIssueReferenceAsync(fixture, taskId, 7);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetScmKontoModusAsync(ScmKontoModus.SeparatesKonto);
        var contexts = new List<ScmRepositoryContext>();
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(
                It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()))
            .Returns((ScmRepositoryContext context, CancellationToken _) =>
            {
                contexts.Add(context);
                return Task.FromResult(fixture.Repository with { Konto = context.Konto });
            });
        var monitor = fixture.Services.GetRequiredService<ScmTaskCompletionMonitoringService>();

        await monitor.RunOnceAsync();

        var context = Assert.Single(contexts);
        Assert.Equal(ScmKontoModus.Benutzerkonto, context.Konto);
        Assert.Equal("https://github.com/owner/repo", context.RepositoryId);
    }

    /// <summary>Deaktivierte Abschlussüberwachung beobachtet nichts: kein Provider-Aufruf,
    /// kein Abschluss-Countdown. (Der Ereignisgesteuert-Fall manuell angelegter Aufgaben ist
    /// in <see cref="Monitor_Ereignisgesteuert_SkipsManualTaskWithPullRequest"/> abgedeckt.)</summary>
    [Fact]
    public async Task Monitor_Disabled_SkipsObservation()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await ProcessEventWithReportAsync(fixture, eventId, pullRequestNumber: 7);
        SetupPullRequestStatus(fixture, PullRequestStatus.Merged);
        var monitor = fixture.Services.GetRequiredService<ScmTaskCompletionMonitoringService>();

        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetBoolSettingAsync(AppEinstellungService.ScmTaskCompletionEnabledKey, false);

        await monitor.RunOnceAsync();

        fixture.Git.Verify(x => x.GetPullRequestStatusAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null((await TaskAsync(fixture, taskId)).ScmAbschlussErkanntUtc);
    }

    /// <summary>Ein offener Pull Request und ein noch nicht abgeschlossenes Ereignis verhindern
    /// jeweils jeden Abschluss: weder Countdown noch Beendigung.</summary>
    [Fact]
    public async Task Monitor_OpenPullRequestOrPendingEvent_KeepTaskRunning()
    {
        var time = new TestTimeProvider();
        await using var fixture = new ScmDispatchFixture(time: time);
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await ProcessEventWithReportAsync(fixture, eventId, pullRequestNumber: 7);
        await SetWaitAsync(fixture, 5);
        SetupPullRequestStatus(fixture, PullRequestStatus.Open);
        var monitor = fixture.Services.GetRequiredService<ScmTaskCompletionMonitoringService>();

        await monitor.RunOnceAsync();
        Assert.Null((await TaskAsync(fixture, taskId)).ScmAbschlussErkanntUtc);

        // Geschlossener PR, aber ein zweites, noch laufendes Ereignis: kein Countdown-Start.
        var (_, pendingEventId) = await fixture.AddAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            var pending = await db.ScmIncomingEvents.SingleAsync(x => x.Id == pendingEventId);
            pending.AufgabeId = taskId;
            await db.SaveChangesAsync();
        }
        SetupPullRequestStatus(fixture, PullRequestStatus.Merged);
        time.Advance(TimeSpan.FromMinutes(10));
        await monitor.RunOnceAsync();

        var task = await TaskAsync(fixture, taskId);
        Assert.Null(task.ScmAbschlussErkanntUtc);
        Assert.Equal(AufgabeStatus.Gestartet, task.Status);
        Assert.False(fixture.Sessions[taskId].Object.Process.HasExited);
    }

    /// <summary>Im Prüfmodus „AlleAufgaben“ wird auch eine manuell angelegte Aufgabe ohne
    /// SCM-Ereigniszuordnung beobachtet, sofern sie eine Pull-Request-Referenz trägt.</summary>
    [Fact]
    public async Task Monitor_AlleAufgabenMode_ObservesManualTaskWithPullRequest()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddManualTaskWithPullRequestAsync(fixture);
        await SetPruefmodusAsync(fixture, ScmTaskCompletionPruefmodus.AlleAufgaben);
        SetupPullRequestStatus(fixture, PullRequestStatus.Merged, number: 42);
        var monitor = fixture.Services.GetRequiredService<ScmTaskCompletionMonitoringService>();

        await monitor.RunOnceAsync();

        fixture.Git.Verify(x => x.GetPullRequestStatusAsync(
            "owner/repo", 42, It.IsAny<CancellationToken>()), Times.Once);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var manual = await db.Aufgaben.Include(x => x.PullRequests)
            .SingleAsync(x => x.PullRequests.Any(p => p.PullRequestNumber == 42));
        Assert.NotNull(manual.ScmAbschlussErkanntUtc);
        Assert.Equal("Pull Request #42", manual.ScmAbschlussReferenz);
    }

    /// <summary>Im Standard-Prüfmodus „Ereignisgesteuert“ wird dieselbe manuell angelegte Aufgabe
    /// ohne SCM-Ereigniszuordnung nicht beobachtet.</summary>
    [Fact]
    public async Task Monitor_Ereignisgesteuert_SkipsManualTaskWithPullRequest()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddManualTaskWithPullRequestAsync(fixture);
        SetupPullRequestStatus(fixture, PullRequestStatus.Merged, number: 42);
        var monitor = fixture.Services.GetRequiredService<ScmTaskCompletionMonitoringService>();

        await monitor.RunOnceAsync();

        fixture.Git.Verify(x => x.GetPullRequestStatusAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var manual = await db.Aufgaben.Include(x => x.PullRequests)
            .SingleAsync(x => x.PullRequests.Any(p => p.PullRequestNumber == 42));
        Assert.Null(manual.ScmAbschlussErkanntUtc);
    }

    /// <summary>Im Prüfmodus „AlleAufgaben“ wird eine manuell angelegte Aufgabe auch über ihr
    /// verknüpftes Issue beobachtet: ein geschlossenes Issue beendet sie nach der Wartezeit.</summary>
    [Fact]
    public async Task Monitor_PruefmodusAlleAufgaben_ManuellAngelegteAufgabeMitGeschlossenemIssue_WirdBeendet()
    {
        var time = new TestTimeProvider();
        await using var fixture = new ScmDispatchFixture(time: time);
        await fixture.InitializeAsync();
        var taskId = await AddManualTaskWithIssueAsync(fixture, 7);
        await SetPruefmodusAsync(fixture, ScmTaskCompletionPruefmodus.AlleAufgaben);
        await SetWaitAsync(fixture, 5);
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadIssueAsync(
                It.Is<ScmEventReference>(r => r.Number == 7), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Issue(7, "Issue", null, [], null, null) { IstGeschlossen = true });
        var monitor = fixture.Services.GetRequiredService<ScmTaskCompletionMonitoringService>();

        await monitor.RunOnceAsync();

        var task = await TaskAsync(fixture, taskId);
        Assert.Equal("Issue #7", task.ScmAbschlussReferenz);
        Assert.Equal(AufgabeStatus.Gestartet, task.Status);

        time.Advance(TimeSpan.FromMinutes(5));
        await monitor.RunOnceAsync();

        Assert.Equal(AufgabeStatus.Beendet, (await TaskAsync(fixture, taskId)).Status);
    }

    /// <summary>Im Standard-Prüfmodus „Ereignisgesteuert“ wird eine Aufgabe mit ungültiger
    /// Issue-Referenz (keine Nummer) trotz SCM-Ereigniszuordnung nie beobachtet.</summary>
    [Fact]
    public async Task Monitor_Ereignisgesteuert_TaskWithInvalidIssueReference_IsNeverObserved()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await ProcessEventWithReportAsync(fixture, eventId, pullRequestNumber: null);
        await AddIssueReferenceAsync(fixture, taskId, null);
        var monitor = fixture.Services.GetRequiredService<ScmTaskCompletionMonitoringService>();

        await monitor.RunOnceAsync();

        // Referenz vorhanden, aber ohne gültige Issue-/PR-Nummer → kein Provider-Aufruf.
        fixture.Git.Verify(x => x.GetPullRequestStatusAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadIssueAsync(
            It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null((await TaskAsync(fixture, taskId)).ScmAbschlussErkanntUtc);
    }

    /// <summary>Im Prüfmodus „AlleAufgaben“ gilt eine ungültige Issue-Referenz (keine Nummer)
    /// ebenfalls nicht als beobachtbare Referenz — kein Provider-Aufruf.</summary>
    [Fact]
    public async Task Monitor_AlleAufgaben_TaskWithInvalidIssueReference_IsNeverObserved()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, _) = await fixture.AddAsync(configured: true);
        await AddIssueReferenceAsync(fixture, taskId, null);
        await SetPruefmodusAsync(fixture, ScmTaskCompletionPruefmodus.AlleAufgaben);
        var monitor = fixture.Services.GetRequiredService<ScmTaskCompletionMonitoringService>();

        await monitor.RunOnceAsync();

        fixture.Git.Verify(x => x.GetPullRequestStatusAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadIssueAsync(
            It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null((await TaskAsync(fixture, taskId)).ScmAbschlussErkanntUtc);
    }

    /// <summary>Eine Aufgabe ganz ohne SCM-Referenz (weder Issue noch Pull Request) wird in keinem
    /// Prüfmodus beobachtet oder beendet — selbst wenn ihr ein SCM-Ereignis zugeordnet ist.</summary>
    [Theory]
    [InlineData(ScmTaskCompletionPruefmodus.Ereignisgesteuert)]
    [InlineData(ScmTaskCompletionPruefmodus.AlleAufgaben)]
    public async Task Monitor_AufgabeOhneIssueUndOhnePullRequest_WirdNiemalsBeendet(ScmTaskCompletionPruefmodus modus)
    {
        var time = new TestTimeProvider();
        await using var fixture = new ScmDispatchFixture(time: time);
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await ProcessEventWithReportAsync(fixture, eventId, pullRequestNumber: null);
        await SetPruefmodusAsync(fixture, modus);
        await SetWaitAsync(fixture, 0);
        var monitor = fixture.Services.GetRequiredService<ScmTaskCompletionMonitoringService>();

        await monitor.RunOnceAsync();

        // Keine Referenz → der Kandidatenfilter sortiert die Aufgabe vor jedem Provider-Aufruf aus.
        fixture.Git.Verify(x => x.GetPullRequestStatusAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadIssueAsync(
            It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()), Times.Never);
        var task = await TaskAsync(fixture, taskId);
        Assert.Null(task.ScmAbschlussErkanntUtc);
        Assert.Equal(AufgabeStatus.Gestartet, task.Status);

        time.Advance(TimeSpan.FromMinutes(30));
        await monitor.RunOnceAsync();

        Assert.Equal(AufgabeStatus.Gestartet, (await TaskAsync(fixture, taskId)).Status);
    }

    private static void SetupPullRequestStatus(ScmDispatchFixture fixture, PullRequestStatus status, int number = 7)
        => fixture.Git.Setup(x => x.GetPullRequestStatusAsync("owner/repo", number, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestStatusInfo(PullRequestProvider.GitHub, "owner/repo", number, null,
                $"https://github.com/owner/repo/pull/{number}", "PR", "src", "main", "sha", null,
                status, PullRequestMergeStatus.Unknown, DateTimeOffset.UtcNow));

    private static async Task ProcessEventWithReportAsync(ScmDispatchFixture fixture, Guid eventId, int? pullRequestNumber)
    {
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", delivered.Status);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"),
            JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json));
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"),
            JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [], false, Report: "PR angelegt.", PullRequestNumber: pullRequestNumber), Json));
        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);
    }

    private static async Task SetWaitAsync(ScmDispatchFixture fixture, int minutes)
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
            .SetIntSettingAsync(AppEinstellungService.ScmTaskCompletionWaitMinutesKey, minutes);
    }

    private static async Task SetPruefmodusAsync(ScmDispatchFixture fixture, ScmTaskCompletionPruefmodus modus)
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
            .SetScmTaskCompletionPruefmodusAsync(modus);
    }

    private static async Task AddIssueReferenceAsync(ScmDispatchFixture fixture, Guid taskId, int? number)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        db.IssueReferenzen.Add(new IssueReferenz { Id = Guid.NewGuid(), AufgabeId = taskId, IssueNummer = number, Titel = "Issue" });
        await db.SaveChangesAsync();
    }

    private static async Task<GitRepository> GetOrCreateRepositoryAsync(ScmDispatchFixture fixture, SoftwareschmiededDbContext db)
    {
        var repository = await db.GitRepositories.FirstOrDefaultAsync();
        if (repository is not null)
            return repository;
        var project = new Projekt { Id = Guid.NewGuid(), Name = "SCM" };
        repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjektId = project.Id,
            RepositoryUrl = "https://github.com/owner/repo",
            PluginTyp = fixture.Repository.ProviderPrefix,
            RepositoryName = "repo",
            Aktiv = true
        };
        db.AddRange(project, repository);
        await db.SaveChangesAsync();
        return repository;
    }

    private static async Task AddManualTaskWithPullRequestAsync(ScmDispatchFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var repository = await GetOrCreateRepositoryAsync(fixture, db);
        var task = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Manuell angelegt",
            ProjektId = repository.ProjektId,
            GitRepositoryId = repository.Id,
            Status = AufgabeStatus.Gestartet,
            PullRequests =
            {
                new PullRequestReferenz
                {
                    Id = Guid.NewGuid(),
                    Provider = PullRequestProvider.GitHub,
                    Rolle = PullRequestReferenzRolle.CreatedByTask,
                    RepositoryId = "owner/repo",
                    PullRequestNumber = 42,
                    CreatedUtc = DateTimeOffset.UtcNow,
                    MonitoringPhase = PullRequestMonitoringPhase.Created
                }
            }
        };
        db.Aufgaben.Add(task);
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> AddManualTaskWithIssueAsync(ScmDispatchFixture fixture, int issueNumber)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var repository = await GetOrCreateRepositoryAsync(fixture, db);
        var task = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Titel = "Manuell angelegt",
            ProjektId = repository.ProjektId,
            GitRepositoryId = repository.Id,
            Status = AufgabeStatus.Gestartet,
            IssueReferenz = new IssueReferenz { Id = Guid.NewGuid(), IssueNummer = issueNumber, Titel = "Issue" }
        };
        db.Aufgaben.Add(task);
        await db.SaveChangesAsync();
        return task.Id;
    }

    private static async Task<Aufgabe> TaskAsync(ScmDispatchFixture fixture, Guid taskId)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>()
            .Aufgaben.AsNoTracking().SingleAsync(x => x.Id == taskId);
    }

    /// <summary>Deterministische Zeitquelle für die Abschluss-Wartezeit.</summary>
    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan span) => _now += span;
    }
}
