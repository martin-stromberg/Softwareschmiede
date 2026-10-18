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

/// <summary>Automatischer Start und Zustellung für aus qualifizierten Ereignissen angelegte oder
/// wiederverwendete Aufgaben inklusive Wartend-Verhalten ohne Standard-CLI.</summary>
public sealed class ScmEventMonitoringServiceTests_AutoStartDelivery
{
    /// <summary>Mit auflösbarer Standard-CLI wird die angelegte Aufgabe im selben Lauf gestartet
    /// (Klon, Branch, Sitzung) und das Ereignis zugestellt.</summary>
    [Fact]
    public async Task Monitor_CreatedTaskWithDefaultCli_StartsAndDelivers()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var repositoryId = await AddRepositoryAsync(fixture);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureObservations(fixture);
        ConfigureIssue(fixture, 9);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PluginDefaultSettingsService>()
                .SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, ScmDispatchFixture.KiPrefix);

        await Monitor(fixture).RunOnceAsync();

        using var verification = fixture.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var task = Assert.Single(await db.Aufgaben.Include(x => x.IssueReferenz).ToListAsync());
        Assert.Equal(AufgabeStatus.Gestartet, task.Status);
        Assert.Equal(AufgabeAusfuehrungsStatus.Aktiv, task.AusfuehrungsStatus);
        Assert.Equal(repositoryId, task.GitRepositoryId);
        Assert.Equal(ScmDispatchFixture.KiPrefix, task.KiPluginPrefix);
        Assert.NotNull(task.LokalerKlonPfad);
        Assert.StartsWith(fixture.Root, task.LokalerKlonPfad);
        Assert.NotNull(task.BranchName);
        Assert.StartsWith("task/issue-9-", task.BranchName);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
        Assert.Equal(task.Id, item.AufgabeId);
        Assert.Equal("Zugestellt", item.Status);
        Assert.NotNull(item.DeliveryAttemptId);
        Assert.NotNull(item.CliRunId);
        Assert.Single(fixture.Sessions);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>Ohne Aufgaben- und Projekt-Plugin nutzt die automatisch angelegte Aufgabe die in den
    /// Einstellungen konfigurierte Standard-CLI (ki.plugin.default, gespeicherter PluginName) und das
    /// Ereignis wird im selben Lauf zugestellt.</summary>
    [Fact]
    public async Task Monitor_CreatedTaskWithSettingsDefaultCli_StartsAndDelivers()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var repositoryId = await AddRepositoryAsync(fixture);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureObservations(fixture);
        ConfigureIssue(fixture, 9);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetSettingAsync(AppEinstellungService.DefaultKiPluginKey, "Simulator");

        await Monitor(fixture).RunOnceAsync();

        using var verification = fixture.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var task = Assert.Single(await db.Aufgaben.Include(x => x.IssueReferenz).ToListAsync());
        Assert.Equal(AufgabeStatus.Gestartet, task.Status);
        Assert.Equal(AufgabeAusfuehrungsStatus.Aktiv, task.AusfuehrungsStatus);
        Assert.Equal(repositoryId, task.GitRepositoryId);
        Assert.Equal(ScmDispatchFixture.KiPrefix, task.KiPluginPrefix);
        Assert.NotNull(task.LokalerKlonPfad);
        Assert.StartsWith(fixture.Root, task.LokalerKlonPfad);
        Assert.NotNull(task.BranchName);
        Assert.StartsWith("task/issue-9-", task.BranchName);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
        Assert.Equal(task.Id, item.AufgabeId);
        Assert.Equal("Zugestellt", item.Status);
        Assert.NotNull(item.DeliveryAttemptId);
        Assert.NotNull(item.CliRunId);
        Assert.Single(fixture.Sessions);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>Ohne Standard-CLI wird die Aufgabe dennoch angelegt (Entscheidung) und das Ereignis
    /// bleibt mit bedienbarer Aufforderung wartend — ohne Duplikat bei Folgeläufen. Nach der
    /// CLI-Konfiguration führt „Erneut prüfen" zur Zustellung.</summary>
    [Fact]
    public async Task Monitor_CreatedTaskWithoutCli_WaitsThenConfiguresAndDelivers()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureObservations(fixture);
        ConfigureIssue(fixture, 9);
        var monitor = Monitor(fixture);

        await monitor.RunOnceAsync();

        Guid eventId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            var task = Assert.Single(await db.Aufgaben.ToListAsync());
            Assert.Equal(AufgabeStatus.Gestartet, task.Status);
            Assert.Null(task.LokalerKlonPfad);
            var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
            eventId = item.Id;
            Assert.Equal("Wartend", item.Status);
            Assert.Equal("Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen.", item.Reason);
        }
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
        Assert.Contains(fixture.Responses, x => x.Kind == ScmResponseKind.Reaction);
        Assert.Contains(fixture.Responses, x => x.Kind == ScmResponseKind.Comment);

        // Ein Folgelauf ohne CLI erzeugt weder eine zweite Aufgabe noch ein verlorenes Ereignis.
        await monitor.RunOnceAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            Assert.Single(await db.Aufgaben.ToListAsync());
            var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
            Assert.Equal("Wartend", item.Status);
        }

        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PluginDefaultSettingsService>()
                .SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, ScmDispatchFixture.KiPrefix);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().RetryAsync(eventId);

        using var verification = fixture.Services.CreateScope();
        var verifiedDb = verification.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var verified = await verifiedDb.ScmIncomingEvents.SingleAsync(x => x.Id == eventId);
        Assert.Equal("Zugestellt", verified.Status);
        Assert.NotNull(verified.DeliveryAttemptId);
        var startedTask = await verifiedDb.Aufgaben.SingleAsync();
        Assert.NotNull(startedTask.LokalerKlonPfad);
        Assert.Equal(ScmDispatchFixture.KiPrefix, startedTask.KiPluginPrefix);
        Assert.Single(fixture.Sessions);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>Die Queue-Aktion „Öffnen / CLI auswählen" liefert ein wartendes Ereignis einer
    /// angelegten Aufgabe mit dem gewählten Plugin aus.</summary>
    [Fact]
    public async Task Monitor_CreatedTaskWithoutCli_QueueSelectionDelivers()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await AddRepositoryAsync(fixture);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureObservations(fixture);
        ConfigureIssue(fixture, 9);
        await Monitor(fixture).RunOnceAsync();
        Guid eventId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
            eventId = item.Id;
            Assert.Equal("Wartend", item.Status);
        }

        Aufgabe? selectedTask = null;
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().OpenFromQueueAsync(
                eventId, (task, _, _) =>
                {
                    selectedTask = task;
                    return Task.FromResult<string?>(ScmDispatchFixture.KiPrefix);
                });

        Assert.NotNull(selectedTask);
        using var verification = fixture.Services.CreateScope();
        var verifiedDb = verification.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var verified = await verifiedDb.ScmIncomingEvents.SingleAsync(x => x.Id == eventId);
        Assert.Equal("Zugestellt", verified.Status);
        Assert.NotNull(verified.DeliveryAttemptId);
        var task = await verifiedDb.Aufgaben.SingleAsync();
        Assert.Equal(selectedTask!.Id, task.Id);
        Assert.Equal(AufgabeStatus.Gestartet, task.Status);
        Assert.NotNull(task.LokalerKlonPfad);
        Assert.Single(fixture.Sessions);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>Eine wiederverwendete, blockierte Aufgabe wird auch im neuen Pfad nicht gestartet;
    /// ihr Ereignis bleibt mit Grund wartend.</summary>
    [Theory]
    [InlineData(AufgabeStatus.Beendet, false)]
    [InlineData(AufgabeStatus.Archiviert, false)]
    [InlineData(AufgabeStatus.Neu, true)]
    public async Task Monitor_ReusedBlockedTask_StaysWaitingWithoutStart(AufgabeStatus status, bool paused)
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var taskId = await AddLinkedTaskAsync(fixture, status, paused);
        ConfigureEnumeration(fixture, new ScmEventReference(fixture.Repository, ScmReferenceKind.Issue, 9));
        ConfigureObservations(fixture);

        await Monitor(fixture).RunOnceAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var task = Assert.Single(await db.Aufgaben.ToListAsync());
        Assert.Equal(taskId, task.Id);
        Assert.Equal(status, task.Status);
        Assert.Null(task.LokalerKlonPfad);
        var item = Assert.Single(await db.ScmIncomingEvents.ToListAsync());
        Assert.Equal(taskId, item.AufgabeId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains(paused ? "pausiert" : "beendet/archiviert", item.Reason);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
    }

    private static ScmEventMonitoringService Monitor(ScmDispatchFixture fixture)
        => fixture.Services.GetRequiredService<ScmEventMonitoringService>();

    private static async Task<Guid> AddRepositoryAsync(ScmDispatchFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var project = new Projekt { Id = Guid.NewGuid(), Name = "AutoStartDelivery" };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Projekt = project,
            RepositoryUrl = "https://github.com/owner/repo",
            RepositoryName = "repo",
            PluginTyp = fixture.Repository.ProviderPrefix
        };
        db.AddRange(project, repository);
        await db.SaveChangesAsync();
        return repository.Id;
    }

    private static async Task<Guid> AddLinkedTaskAsync(ScmDispatchFixture fixture, AufgabeStatus status, bool paused)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var project = new Projekt { Id = Guid.NewGuid(), Name = "Blocked" };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            Projekt = project,
            RepositoryUrl = "https://github.com/owner/repo",
            RepositoryName = "repo",
            PluginTyp = fixture.Repository.ProviderPrefix
        };
        var task = new Aufgabe
        {
            Id = Guid.NewGuid(),
            Projekt = project,
            GitRepository = repository,
            Titel = "Verknüpfte Aufgabe",
            Status = status,
            PausiertBisUtc = paused ? DateTimeOffset.UtcNow.AddHours(1) : null,
            IssueReferenz = new IssueReferenz
            {
                Id = Guid.NewGuid(),
                IssueNummer = 9,
                Titel = "Issue 9",
                IssueUrl = "https://github.com/owner/repo/issues/9"
            }
        };
        db.Add(task);
        await db.SaveChangesAsync();
        return task.Id;
    }

    private static void ConfigureEnumeration(ScmDispatchFixture fixture, params ScmEventReference[] references)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.EnumerateReferencesAsync(
                It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(references);

    private static void ConfigureObservations(ScmDispatchFixture fixture)
    {
        var time = DateTimeOffset.UtcNow;
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmEventReadRequest request, CancellationToken _) => Task.FromResult(new ScmEventBatch(request,
                request.ContentKind == ScmContentKind.IssueComment
                    ? [new ScmEventObservation(request.Reference, request.ContentKind, "C_" + request.Reference.Number, "42",
                        time, time, null, "U_1", "owner", null, null, "$Softwareschmiede test",
                        "https://github.com/owner/repo/issues/" + request.Reference.Number, time)]
                    : Array.Empty<ScmEventObservation>())));
    }

    private static void ConfigureIssue(ScmDispatchFixture fixture, int number)
        => fixture.Git.As<IScmEventSource>().Setup(x => x.ReadIssueAsync(
                It.Is<ScmEventReference>(r => r.Kind == ScmReferenceKind.Issue && r.Number == number), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScmEventReference reference, CancellationToken _) => new Issue(reference.Number,
                "Issue Titel " + reference.Number, "Beschreibungstext", Array.Empty<string>(), null,
                "https://github.com/owner/repo/issues/" + reference.Number));
}
