using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Laufzeit-Einstellungen der SCM-Ereignisverarbeitung: Überwachungs-Schalter und Trigger-Keyword.</summary>
public sealed class ScmEventMonitoringServiceTests_Settings
{
    /// <summary>Deaktivierte Überwachung liest keine Quellen, enumeriert keine Repositories und stellt
    /// bereits eingegangene Ereignisse nicht automatisch zu. Nach der Reaktivierung läuft Beobachtung
    /// und Zustellung unverändert weiter.</summary>
    [Fact]
    public async Task Monitor_DisabledMonitoring_SkipsObservationAndDispatch_UntilReEnabled()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        var monitor = Monitor(fixture);

        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetBoolSettingAsync(AppEinstellungService.ScmMonitoringEnabledKey, false);

        await monitor.RunOnceAsync();

        fixture.Git.As<IScmEventSource>().Verify(x => x.GetEventRepositoryAsync(
            It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.As<IScmEventSource>().Verify(x => x.ReadEventsAsync(
            It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Git.As<IScmEventSource>().Verify(x => x.EnumerateReferencesAsync(
            It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
        // Das zuvor eingegangene Ereignis bleibt unverändert bestehen und wird nicht zugestellt.
        Assert.Equal("Aufgenommen", (await fixture.EventAsync(eventId)).Status);
        Assert.Empty(fixture.Prompts);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Responses);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            Assert.Single(await db.ScmIncomingEvents.ToListAsync());
            // Nur der Checkpoint der manuell eingespeisten Quelle existiert; der Monitor hat keine
            // eigenen Beobachtungs- oder Enumerations-Checkpoints angelegt.
            Assert.DoesNotContain(await db.ScmObservationCheckpoints.ToListAsync(),
                x => x.SourceKey.StartsWith("enumeration/") || x.SourceKey.Contains('/'));
        }

        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetBoolSettingAsync(AppEinstellungService.ScmMonitoringEnabledKey, true);

        await monitor.RunOnceAsync();

        Assert.Equal("Zugestellt", (await fixture.EventAsync(eventId)).Status);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>Auch der lokale Antwortkanal fährt bei deaktivierter Überwachung keine automatischen
    /// Abgleiche oder Hostaktionen; nach der Reaktivierung wird der Versuch regulär fortgesetzt.</summary>
    [Fact]
    public async Task ExchangeService_DisabledMonitoring_SkipsPendingAttempts()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            var item = await db.ScmIncomingEvents.SingleAsync(x => x.Id == eventId);
            item.DeliveryAttemptId = Guid.NewGuid();
            item.CliRunId = Guid.NewGuid().ToString("D");
            item.DeliveredUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            item.Status = "Zugestellt";
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetBoolSettingAsync(AppEinstellungService.ScmMonitoringEnabledKey, false);
        }
        var exchange = new ScmEventExchangeService(fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System, fixture.Services.GetRequiredService<ScmEventStartupGate>(),
            NullLogger<ScmEventExchangeService>.Instance);

        await exchange.RunOnceAsync();

        Assert.Empty(fixture.Responses);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(eventId)).Status);

        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetBoolSettingAsync(AppEinstellungService.ScmMonitoringEnabledKey, true);

        await exchange.RunOnceAsync();

        Assert.NotEmpty(fixture.Responses);
    }

    /// <summary>Ein konfiguriertes Trigger-Keyword ersetzt den Standard: Nur Texte mit dem neuen
    /// Keyword erzeugen Inbox-Einträge; der alte Trigger wird mit „Kein Trigger" diagnostiziert.</summary>
    [Fact]
    public async Task Monitor_CustomTriggerKeyword_AcceptsNewAndRejectsOld()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var taskId = await AddTaskWithIssueAsync(fixture, 7);
        await AddTaskWithIssueAsync(fixture, 42);
        ConfigureObservations(fixture, number => number == 7 ? "!Bearbeite bitte" : "$Softwareschmiede bitte");
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetSettingAsync(AppEinstellungService.ScmTriggerKeywordKey, "!Bearbeite");

        await Monitor(fixture).RunOnceAsync();

        using var verification = fixture.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
        Assert.Equal(taskId, item.AufgabeId);
        Assert.Equal(7, ScmEventStore.Observation(item).Reference.Number);
        Assert.Contains(await db.ScmContentSnapshots.Where(x => x.ContentKey.Contains("Issue/42/")).ToListAsync(),
            x => x.Diagnostic == "Kein Trigger");
    }

    /// <summary>Explizite Benutzeraktionen bleiben bei deaktivierter Überwachung funktionsfähig:
    /// „Erneut prüfen" liefert ein wartendes Ereignis weiterhin aus.</summary>
    [Fact]
    public async Task ManualRetry_DispatchesWhileMonitoringDisabled()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetBoolSettingAsync(AppEinstellungService.ScmMonitoringEnabledKey, false);

        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().RetryAsync(eventId);

        Assert.Equal("Zugestellt", (await fixture.EventAsync(eventId)).Status);
        Assert.Single(fixture.Prompts);
    }

    private static ScmEventMonitoringService Monitor(ScmDispatchFixture fixture)
        => fixture.Services.GetRequiredService<ScmEventMonitoringService>();

    private static async Task<Guid> AddTaskWithIssueAsync(ScmDispatchFixture fixture, int number)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var project = new Projekt { Id = Guid.NewGuid(), Name = "Repo-wide" };
        var task = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Projekt = project,
            Titel = "Linked task",
            Status = AufgabeStatus.Neu,
            GitRepository = new GitRepository
            {
                Id = Guid.NewGuid(),
                Projekt = project,
                RepositoryUrl = "https://github.com/owner/repo",
                RepositoryName = "repo",
                PluginTyp = fixture.Repository.ProviderPrefix
            },
            IssueReferenz = new IssueReferenz { Id = Guid.NewGuid(), IssueNummer = number, Titel = "Issue" }
        };
        db.Add(task);
        await db.SaveChangesAsync();
        return task.Id;
    }

    private static void ConfigureObservations(ScmDispatchFixture fixture, Func<int, string>? body = null)
    {
        var time = DateTimeOffset.UtcNow;
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmEventReadRequest request, CancellationToken _) => Task.FromResult(new ScmEventBatch(request,
                request.ContentKind == ScmContentKind.IssueComment ? [new ScmEventObservation(request.Reference, request.ContentKind,
                    "C_" + request.Reference.Number, "42", time, time, null, "U_1", "owner", null, null,
                    body?.Invoke(request.Reference.Number) ?? "$Softwareschmiede test",
                    "https://github.com/owner/repo/issues/" + request.Reference.Number, time)] : [])));
    }
}
