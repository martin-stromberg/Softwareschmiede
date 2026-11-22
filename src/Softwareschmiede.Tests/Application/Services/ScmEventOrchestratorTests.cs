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

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Dispatch guards and run-bound confirmations against persisted events and real session adapters.</summary>
public sealed class ScmEventOrchestratorTests
{
    /// <summary>Ein Token ohne Repository-Schreibzugriff hinterlässt einen umsetzbaren Einstellungspfad, ohne die CLI zu starten.</summary>
    [Fact]
    public async Task MissingWriteAccess_ExplainsTokenSettingsBeforeCliStart()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        fixture.Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fixture.Repository with { CanWrite = false });
        await fixture.DispatchAsync(eventId);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("Einstellungen → Plugins → GitHub → Authentifizierung", item.Reason);
        Assert.Contains("Personal Access Token", item.Reason);
        Assert.Contains("owner/repo", item.Reason);
        Assert.Contains("Lese- und Schreibrechte", item.Reason);
        Assert.Contains("Erneut prüfen", item.Reason);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
    }

    /// <summary>Eine gespeicherte Beobachtung mit Viewer-Reaktion (Altdaten oder manuelle Inserts) wird niemals zugestellt: die Dispatch-Revalidierung parkt sie mit eigenem Grund.</summary>
    [Fact]
    public async Task Dispatch_ReactedStoredObservation_Waits()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        Guid eventId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            var project = new Project { Id = Guid.NewGuid(), Name = "SCM" };
            var repository = new GitRepository { Id = Guid.NewGuid(), ProjectId = project.Id, RepositoryUrl = "https://github.com/owner/repo", PluginType = fixture.Repository.ProviderPrefix, RepositoryName = "repo" };
            var task = new WorkItem { Id = Guid.NewGuid(), Title = "SCM task", ProjectId = project.Id, GitRepositoryId = repository.Id, Status = WorkItemStatus.Started, AiPluginPrefix = ScmDispatchFixture.AiPrefix };
            var time = DateTimeOffset.UtcNow;
            var observation = new ScmEventObservation(new(fixture.Repository, ScmReferenceKind.Issue, 7), ScmContentKind.IssueComment,
                Guid.NewGuid().ToString(), "42", time, time, null, "U_1", "owner", null, null,
                "$Softwareschmiede test", "https://github.com/owner/repo/issues/7", time, ["EYES"]);
            var incomingEvent = new ScmIncomingEvent
            {
                WorkItemId = task.Id,
                EventKey = observation.EventKey,
                ObservationJson = JsonSerializer.Serialize(observation, ScmEventStore.JsonOptions),
                ReceivedUtc = time.ToUnixTimeMilliseconds()
            };
            db.AddRange(project, repository, task, incomingEvent);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            eventId = incomingEvent.Id;
        }
        await fixture.DispatchAsync(eventId);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("bereits mit dem Ereignis-Konto reagiert", item.Reason);
        Assert.Contains("zweite Installation", item.Reason);
        Assert.Null(item.DeliveryAttemptId);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
    }

    /// <summary>Ein gültiger Empfang aktiviert eine neue Aufgabe, auch solange noch eine CLI-Auswahl nötig ist.</summary>
    [Fact]
    public async Task NoDefault_ActivatesTaskBeforeWaitingForSelection()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync();
        await fixture.DispatchAsync(eventId);
        using var scope = fixture.Services.CreateScope();
        var task = await scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>().WorkItems.FindAsync(new object?[] { taskId }, TestContext.Current.CancellationToken);
        Assert.Equal(WorkItemStatus.Started, task!.Status);
        Assert.Contains("Keine Standard-CLI", (await fixture.EventAsync(eventId)).Reason);
        Assert.Empty(fixture.Sessions);
        Assert.Equal(2, fixture.Responses.Count);
    }

    /// <summary>Eine Aufgabe ohne Plugin nutzt die in den Einstellungen konfigurierte Standard-CLI (ki.plugin.default), und der Start persistiert das aufgelöste Präfix an der Aufgabe.</summary>
    [Fact]
    public async Task SettingsDefaultCli_StartsTaskAndDelivers()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync();
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppSettingService>()
                .SetSettingAsync(AppSettingService.DefaultAiPluginKey, "Simulator", TestContext.Current.CancellationToken);
        await fixture.DispatchAsync(eventId);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", item.Status);
        Assert.NotNull(item.DeliveryAttemptId);
        Assert.Single(fixture.Sessions);
        Assert.Single(fixture.Prompts);
        using var verification = fixture.Services.CreateScope();
        var task = await verification.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>().WorkItems.FindAsync(new object?[] { taskId }, TestContext.Current.CancellationToken);
        Assert.Equal(ScmDispatchFixture.AiPrefix, task!.AiPluginPrefix);
    }

    /// <summary>Ein Einstellungs-Standard, der kein installiertes Plugin nennt, wird nicht umrissen: das Ereignis behält einen umsetzbaren Wartezustand, der die ignorierte Zuordnung nennt.</summary>
    [Fact]
    public async Task UnresolvableSettingsDefaultCli_KeepsWaitingState()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync();
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppSettingService>()
                .SetSettingAsync(AppSettingService.DefaultAiPluginKey, "Nicht installiert", TestContext.Current.CancellationToken);
        await fixture.DispatchAsync(eventId);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("nicht mehr installiert oder aktiviert", item.Reason);
        Assert.Contains("Nicht installiert", item.Reason);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
    }

    /// <summary>Eine deaktivierte Aufgaben-Plugin-Zuordnung wird ignoriert: der Dispatch fällt auf das aktivierte globale Standard-Plugin zurück, stellt das Ereignis zu und persistiert das genutzte Präfix.</summary>
    [Fact]
    public async Task DisabledTaskPlugin_IfBackToGlobalDefault_Delivers()
    {
        var second = new ScmAiProfile("Softwareschmiede.OtherKi", string.Empty, "OTHER", "OTHER>",
            new Dictionary<string, string?>());
        await using var fixture = new ScmDispatchFixture(ki2: second);
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            (await db.WorkItems.FindAsync(new object?[] { taskId }, TestContext.Current.CancellationToken))!.AiPluginPrefix = second.Prefix;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<PluginActivationService>()
                .SetPluginEnabledAsync(second.Prefix, false, TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<PluginDefaultSettingsService>()
                .SaveDefaultPluginPrefixAsync(PluginType.DevelopmentAutomation, ScmDispatchFixture.AiPrefix, TestContext.Current.CancellationToken);
        }
        await fixture.DispatchAsync(eventId);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", item.Status);
        Assert.NotNull(item.DeliveryAttemptId);
        Assert.Single(fixture.Sessions);
        Assert.Single(fixture.Prompts);
        using var verification = fixture.Services.CreateScope();
        var task = await verification.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>().WorkItems.FindAsync(new object?[] { taskId }, TestContext.Current.CancellationToken);
        Assert.Equal(ScmDispatchFixture.AiPrefix, task!.AiPluginPrefix);
    }

    /// <summary>Ohne konfiguriertes ausführbares Plugin wartet das Ereignis ehrlich: ein verständlicher Grund und ein Protokolleintrag werden aufgezeichnet, kein harter NextAttemptUtc-Lock wird gesetzt, und die Zustellung geht weiter, sobald das Plugin wieder aktiviert wird.</summary>
    [Fact]
    public async Task UnavailablePlugins_EventWaitsWithoutHardLock_ResumesAfterFix()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PluginActivationService>()
                .SetPluginEnabledAsync(ScmDispatchFixture.AiPrefix, false, TestContext.Current.CancellationToken);
        await fixture.DispatchAsync(eventId);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Null(item.NextAttemptUtc);
        Assert.Contains("nicht mehr installiert oder aktiviert", item.Reason);
        Assert.Contains(ScmDispatchFixture.AiPrefix, item.Reason);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            var entries = await db.LogEntries.Where(x => x.WorkItemId == taskId).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Contains(entries, x => x.Type == LogEntryType.SystemMessage && x.Content.Contains(ScmDispatchFixture.AiPrefix));
        }
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PluginActivationService>()
                .SetPluginEnabledAsync(ScmDispatchFixture.AiPrefix, true, TestContext.Current.CancellationToken);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().RetryAsync(eventId, TestContext.Current.CancellationToken);
        item = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", item.Status);
        Assert.NotNull(item.DeliveryAttemptId);
        Assert.Single(fixture.Sessions);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>Eine explizite Benutzerauswahl bleibt strikt: ein deaktiviertes explizites Präfix behält den bestehenden Fehlerpfad mit hartem NextAttemptUtc-Lock statt eines stillen Fallbacks.</summary>
    [Fact]
    public async Task ExplicitPluginPrefix_RemainsStrict_LocksOnUnavailable()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PluginActivationService>()
                .SetPluginEnabledAsync(ScmDispatchFixture.AiPrefix, false, TestContext.Current.CancellationToken);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().DispatchAsync(eventId, ScmDispatchFixture.AiPrefix, TestContext.Current.CancellationToken);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Equal(long.MaxValue, item.NextAttemptUtc);
        Assert.Contains("nicht mehr installiert oder aktiviert", item.Reason);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
    }

    /// <summary>Terminal absence does not override completion, archive or pause guards.</summary>
    [Theory]
    [InlineData(WorkItemStatus.Completed, false)]
    [InlineData(WorkItemStatus.Archived, false)]
    [InlineData(WorkItemStatus.New, true)]
    public async Task BlockedTask_IsNeverActivated(WorkItemStatus status, bool paused)
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(status, configured: true);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            if (paused) { (await db.WorkItems.FindAsync(new object?[] { taskId }, TestContext.Current.CancellationToken))!.PausedUntilUtc = DateTimeOffset.UtcNow.AddHours(1); await db.SaveChangesAsync(TestContext.Current.CancellationToken); }
        }
        await fixture.DispatchAsync(eventId);
        using var verification = fixture.Services.CreateScope();
        Assert.Equal(status, (await verification.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>().WorkItems.FindAsync(new object?[] { taskId }, TestContext.Current.CancellationToken))!.Status);
        Assert.Empty(fixture.Sessions);
        Assert.Equal("Wartend", (await fixture.EventAsync(eventId)).Status);
    }

    /// <summary>Eine fehlgeschlagene Kanalprüfung kann nach einer weiteren Eingabe im selben Lauf keine alte Benutzerbestätigung wiederverwenden.</summary>
    [Fact]
    public async Task Confirmation_ChannelFailureThenManualInput_DoesNotAuthorizeLaterUnknownPrompt()
    {
        await using var fixture = new ScmDispatchFixture { Prompt = "SCM_UNKNOWN_V1" };
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var cli = fixture.Services.GetRequiredService<AiExecutionService>();
        var adapter = cli.GetScmPromptSession(taskId)!;
        var channel = Path.Combine(adapter.WorkingDirectory, ScmEventExchangeProtocol.DirectoryName);
        Directory.CreateDirectory(channel);
        var foreign = Path.Combine(channel, "foreign.txt");
        await File.WriteAllTextAsync(foreign, "retain", TestContext.Current.CancellationToken);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().ConfirmReadyAsync(eventId, adapter.CliRunId.ToString("D"), TestContext.Current.CancellationToken);
        Assert.Null((await fixture.EventAsync(eventId)).DeliveryAttemptId);
        Assert.Equal("retain", await File.ReadAllTextAsync(foreign, TestContext.Current.CancellationToken));
        await cli.GetTerminalSession(taskId)!.WriteInputAsync(new byte[] { 65 }, TestContext.Current.CancellationToken);
        fixture.SetPrompt(taskId, "SCM_UNKNOWN_V1");
        Assert.Equal(ScmPromptReadyStatus.Unknown, adapter.ReadyStatus);
        File.Delete(foreign);
        Directory.Delete(channel);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().RetryAsync(eventId, TestContext.Current.CancellationToken);
        var item = await fixture.EventAsync(eventId);
        Assert.Null(item.DeliveryAttemptId);
        Assert.Null(item.ReadyConfirmedRunId);
        Assert.Contains("Eingabebereitschaft unbekannt", item.Reason);
        Assert.Empty(fixture.Prompts);
        Assert.Equal(adapter.CliRunId.ToString("D"), item.CliRunId);
    }

    /// <summary>Ein Workspace-Vertrauensdialog ist ein eigener Wartezustand: es wird kein Prompt gesendet, die Bestätigungsaktion bleibt unverfügbar, und die Zustellung läuft automatisch weiter, sobald der Benutzer im Terminal antwortet.</summary>
    [Fact]
    public async Task TrustDialog_BlocksUntilAnswered_ThenDeliversAutomatically()
    {
        await using var fixture = new ScmDispatchFixture { Prompt = "Do you trust the contents of this directory?" };
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var cli = fixture.Services.GetRequiredService<AiExecutionService>();
        var adapter = cli.GetScmPromptSession(taskId)!;
        Assert.Equal(ScmPromptReadyStatus.TrustPrompt, adapter.ReadyStatus);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("Vertrauenswürdigkeit", item.Reason);
        Assert.DoesNotContain("Eingabebereitschaft unbekannt", item.Reason);
        Assert.Null(item.DeliveryAttemptId);
        Assert.Empty(fixture.Prompts);
        using (var scope = fixture.Services.CreateScope())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().ConfirmReadyAsync(eventId, adapter.CliRunId.ToString("D"), TestContext.Current.CancellationToken));
        Assert.Empty(fixture.Prompts);
        fixture.SetPrompt(taskId, "SCM_READY_V1");
        await fixture.DispatchAsync(eventId);
        item = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", item.Status);
        Assert.NotNull(item.DeliveryAttemptId);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>Eine bewusste Bereit-Bestätigung ist eine explizite Wiederholung und hebt einen früheren harten Stopp auf.</summary>
    [Fact]
    public async Task ConfirmReady_LiftsNextAttemptLock()
    {
        await using var fixture = new ScmDispatchFixture { Prompt = "SCM_UNKNOWN_V1" };
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var cli = fixture.Services.GetRequiredService<AiExecutionService>();
        var adapter = cli.GetScmPromptSession(taskId)!;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
            await db.ScmIncomingEvents.ExecuteUpdateAsync(x => x.SetProperty(p => p.NextAttemptUtc, (long?)long.MaxValue), cancellationToken: TestContext.Current.CancellationToken);
        }
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().ConfirmReadyAsync(eventId, adapter.CliRunId.ToString("D"), TestContext.Current.CancellationToken);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", item.Status);
        Assert.NotNull(item.DeliveryAttemptId);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>Eine CLI ohne Prompt-Format-Profil wartet ehrlich, ohne eine Bestätigung anzubieten.</summary>
    [Fact]
    public async Task UnknownCliFormat_WaitsWithoutConfirmOption()
    {
        await using var fixture = new ScmDispatchFixture(ai: new ScmAiProfile("Softwareschmiede.GitHubCopilot", string.Empty,
            "GitHub Copilot CLI", ">", new Dictionary<string, string?>()));
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("Promptformat", item.Reason);
        Assert.Contains("unbekannt", item.Reason);
        Assert.False(ScmCliWaitReason.IsConfirmable(item.Reason));
        var adapter = fixture.Services.GetRequiredService<AiExecutionService>().GetScmPromptSession(taskId)!;
        Assert.Equal(ScmCliRecognitionState.Unknown, adapter.CliRecognition.State);
        using var scope = fixture.Services.CreateScope();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().ConfirmReadyAsync(eventId, adapter.CliRunId.ToString("D"), TestContext.Current.CancellationToken));
        Assert.Contains("unbekannt", error.Message);
        Assert.Empty(fixture.Prompts);
    }

    /// <summary>Eine explizite Bestätigung einer ausserhalb des Bereichs liegenden Version lässt die Datei-Tool-Kanal-Probe den Zugriff beweisen.</summary>
    [Fact]
    public async Task ConfirmReady_UnsupportedVersion_DeliversAfterChannelProbe()
    {
        var prompts = new List<string>();
        var fixture = default(ScmDispatchFixture);
        fixture = new ScmDispatchFixture(ai: ScmAiProfile.Devin(banner: "Devin CLI\nv3001.0.0",
            onWritePrompt: async prompt =>
            {
                if (prompt.Contains("challengePath", StringComparison.Ordinal)) await ScmDispatchFixture.CompleteProbeAsync(prompt);
                else prompts.Add(prompt);
                fixture!.Sessions.Values.Single().Raise(x => x.BufferChanged += null, EventArgs.Empty);
            }));
        try
        {
            await fixture.InitializeAsync();
            var (taskId, eventId) = await fixture.AddAsync(configured: true);
            await fixture.DispatchAsync(eventId);
            var waiting = await fixture.EventAsync(eventId);
            Assert.Equal("Wartend", waiting.Status);
            Assert.Contains("Devin CLI", waiting.Reason);
            Assert.Contains("v3001.0.0", waiting.Reason);
            Assert.Contains("nicht unterstützt", waiting.Reason);
            var adapter = fixture.Services.GetRequiredService<AiExecutionService>().GetScmPromptSession(taskId)!;
            Assert.Equal(ScmCliRecognitionState.Unsupported, adapter.CliRecognition.State);
            using (var scope = fixture.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().ConfirmReadyAsync(eventId, adapter.CliRunId.ToString("D"), TestContext.Current.CancellationToken);
            ScmIncomingEvent item;
            var attempts = 0;
            do
            {
                await fixture.DispatchAsync(eventId);
                item = await fixture.EventAsync(eventId);
            } while (item.Status == "Wartend" && ++attempts < 5);
            Assert.Equal("Zugestellt", item.Status);
            Assert.Single(prompts);
        }
        finally
        {
            if (fixture is not null) await fixture.DisposeAsync();
        }
    }

    /// <summary>Erreicht ein Ereignis im Dispatch seinen Endstatus, fordert der Lauf die
    /// Startprüfung an — so wird der Platz einer bereits am Prompt wartenden Sitzung
    /// ohne Poll frei.</summary>
    [Fact]
    public async Task DispatchCoreAsync_EventReachesEndStatus_RequestsStartCheck()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", delivered.Status);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"),
            JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), json),
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"),
            JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [], false, Report: "Fertig."), json),
            TestContext.Current.CancellationToken);

        var angefordert = 0;
        fixture.Trigger.Requested += () => angefordert++;
        // Die Kanal-Stabilitätsprüfung übernimmt eine Nachricht erst beim zweiten identischen Lesen.
        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        Assert.Equal("Verarbeitet", (await fixture.EventAsync(eventId)).Status);
        Assert.True(angefordert >= 1, "Der Endstatus-Übergang des Ereignisses hat keine Startprüfung angefordert.");
    }
}
