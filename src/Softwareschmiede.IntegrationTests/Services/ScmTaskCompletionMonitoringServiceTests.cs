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

/// <summary>Abschlussüberwachung ereignisgesteuerter Aufgaben: im Abschluss gemeldete Pull Requests
/// werden verknüpft, geschlossene/gemergte Referenzen beenden die Aufgabe nach der einstellbaren
/// Wartezeit — nur für Aufgaben mit SCM-Ereigniszuordnung.</summary>
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

    /// <summary>Der Issue-Abschlusspfad reicht den aktiven Kontomodus in den Repository-Kontext
    /// durch: Im SeparatesKonto läuft die Issue-Prüfung unter dem Agent-Konto (der PR-Pfad bleibt
    /// bewusst Benutzer-Token, siehe Token-Grenze).</summary>
    [Fact]
    public async Task CompletionService_PassesActiveKonto_ToRepositoryContext()
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
        Assert.Equal(ScmKontoModus.SeparatesKonto, context.Konto);
        Assert.Equal("https://github.com/owner/repo", context.RepositoryId);
    }

    /// <summary>Deaktivierte Abschlussüberwachung beobachtet nichts; Aufgaben ohne
    /// SCM-Ereigniszuordnung (manuell angelegt) bleiben ebenfalls unberührt.</summary>
    [Fact]
    public async Task Monitor_DisabledAndManualTask_SkipsObservation()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await ProcessEventWithReportAsync(fixture, eventId, pullRequestNumber: 7);
        await AddManualTaskWithPullRequestAsync(fixture);
        SetupPullRequestStatus(fixture, PullRequestStatus.Merged);
        var monitor = fixture.Services.GetRequiredService<ScmTaskCompletionMonitoringService>();

        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetBoolSettingAsync(AppEinstellungService.ScmTaskCompletionEnabledKey, false);

        await monitor.RunOnceAsync();

        fixture.Git.Verify(x => x.GetPullRequestStatusAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null((await TaskAsync(fixture, taskId)).ScmAbschlussErkanntUtc);

        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetBoolSettingAsync(AppEinstellungService.ScmTaskCompletionEnabledKey, true);

        await monitor.RunOnceAsync();

        // Nur die ereignisgesteuerte Aufgabe wurde beobachtet, nicht die manuell angelegte.
        fixture.Git.Verify(x => x.GetPullRequestStatusAsync(
            "owner/repo", 7, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull((await TaskAsync(fixture, taskId)).ScmAbschlussErkanntUtc);
        using var verification = fixture.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var manual = await db.Aufgaben.Include(x => x.PullRequests)
            .SingleAsync(x => x.PullRequests.Any(p => p.PullRequestNumber == 42));
        Assert.Null(manual.ScmAbschlussErkanntUtc);
        Assert.Equal(AufgabeStatus.Gestartet, manual.Status);
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

    private static void SetupPullRequestStatus(ScmDispatchFixture fixture, PullRequestStatus status)
        => fixture.Git.Setup(x => x.GetPullRequestStatusAsync("owner/repo", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestStatusInfo(PullRequestProvider.GitHub, "owner/repo", 7, null,
                "https://github.com/owner/repo/pull/7", "PR", "src", "main", "sha", null,
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

    private static async Task AddIssueReferenceAsync(ScmDispatchFixture fixture, Guid taskId, int number)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        db.IssueReferenzen.Add(new IssueReferenz { Id = Guid.NewGuid(), AufgabeId = taskId, IssueNummer = number, Titel = "Issue" });
        await db.SaveChangesAsync();
    }

    private static async Task AddManualTaskWithPullRequestAsync(ScmDispatchFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var repository = await db.GitRepositories.FirstAsync();
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
