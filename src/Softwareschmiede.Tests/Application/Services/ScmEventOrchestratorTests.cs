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
    /// <summary>A token lacking repository write access leaves an actionable settings path without starting the CLI.</summary>
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

    /// <summary>A valid reception activates a new task even while CLI selection is still required.</summary>
    [Fact]
    public async Task NoDefault_ActivatesTaskBeforeWaitingForSelection()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync();
        await fixture.DispatchAsync(eventId);
        using var scope = fixture.Services.CreateScope();
        var task = await scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>().Aufgaben.FindAsync(taskId);
        Assert.Equal(AufgabeStatus.Gestartet, task!.Status);
        Assert.Contains("Keine Standard-CLI", (await fixture.EventAsync(eventId)).Reason);
        Assert.Empty(fixture.Sessions);
        Assert.Equal(2, fixture.Responses.Count);
    }

    /// <summary>A task without a plugin uses the settings-configured default CLI (ki.plugin.default)
    /// and the start persists the resolved prefix on the task.</summary>
    [Fact]
    public async Task SettingsDefaultCli_StartsTaskAndDelivers()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync();
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetSettingAsync(AppEinstellungService.DefaultKiPluginKey, "Simulator");
        await fixture.DispatchAsync(eventId);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", item.Status);
        Assert.NotNull(item.DeliveryAttemptId);
        Assert.Single(fixture.Sessions);
        Assert.Single(fixture.Prompts);
        using var verification = fixture.Services.CreateScope();
        var task = await verification.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>().Aufgaben.FindAsync(taskId);
        Assert.Equal(ScmDispatchFixture.KiPrefix, task!.KiPluginPrefix);
    }

    /// <summary>A settings default that names no installed plugin is not guessed around:
    /// the event keeps the actionable waiting state.</summary>
    [Fact]
    public async Task UnresolvableSettingsDefaultCli_KeepsWaitingState()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync();
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppEinstellungService>()
                .SetSettingAsync(AppEinstellungService.DefaultKiPluginKey, "Nicht installiert");
        await fixture.DispatchAsync(eventId);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("Keine Standard-CLI", item.Reason);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
    }

    /// <summary>Terminal absence does not override completion, archive or pause guards.</summary>
    [Theory]
    [InlineData(AufgabeStatus.Beendet, false)]
    [InlineData(AufgabeStatus.Archiviert, false)]
    [InlineData(AufgabeStatus.Neu, true)]
    public async Task BlockedTask_IsNeverActivated(AufgabeStatus status, bool paused)
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(status, configured: true);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            if (paused) { (await db.Aufgaben.FindAsync(taskId))!.PausiertBisUtc = DateTimeOffset.UtcNow.AddHours(1); await db.SaveChangesAsync(); }
        }
        await fixture.DispatchAsync(eventId);
        using var verification = fixture.Services.CreateScope();
        Assert.Equal(status, (await verification.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>().Aufgaben.FindAsync(taskId))!.Status);
        Assert.Empty(fixture.Sessions);
        Assert.Equal("Wartend", (await fixture.EventAsync(eventId)).Status);
    }

    /// <summary>A failed channel check cannot reuse an old user confirmation after another input in the same run.</summary>
    [Fact]
    public async Task Confirmation_ChannelFailureThenManualInput_DoesNotAuthorizeLaterUnknownPrompt()
    {
        await using var fixture = new ScmDispatchFixture { Prompt = "SCM_UNKNOWN_V1" };
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var cli = fixture.Services.GetRequiredService<KiAusfuehrungsService>();
        var adapter = cli.GetScmPromptSession(taskId)!;
        var channel = Path.Combine(adapter.WorkingDirectory, ScmEventExchangeProtocol.DirectoryName);
        Directory.CreateDirectory(channel);
        var foreign = Path.Combine(channel, "foreign.txt");
        await File.WriteAllTextAsync(foreign, "retain");
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().ConfirmReadyAsync(eventId, adapter.CliRunId.ToString("D"));
        Assert.Null((await fixture.EventAsync(eventId)).DeliveryAttemptId);
        Assert.Equal("retain", await File.ReadAllTextAsync(foreign));
        await cli.GetTerminalSession(taskId)!.WriteInputAsync(new byte[] { 65 });
        fixture.SetPrompt(taskId, "SCM_UNKNOWN_V1");
        Assert.Equal(ScmPromptReadyStatus.Unknown, adapter.ReadyStatus);
        File.Delete(foreign);
        Directory.Delete(channel);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().RetryAsync(eventId);
        var item = await fixture.EventAsync(eventId);
        Assert.Null(item.DeliveryAttemptId);
        Assert.Null(item.ReadyConfirmedRunId);
        Assert.Contains("Eingabebereitschaft unbekannt", item.Reason);
        Assert.Empty(fixture.Prompts);
        Assert.Equal(adapter.CliRunId.ToString("D"), item.CliRunId);
    }

    /// <summary>A workspace trust dialog is its own wait state: no prompt is sent, the confirm action stays
    /// unavailable, and delivery resumes automatically once the user answers in the terminal.</summary>
    [Fact]
    public async Task TrustDialog_BlocksUntilAnswered_ThenDeliversAutomatically()
    {
        await using var fixture = new ScmDispatchFixture { Prompt = "Do you trust the contents of this directory?" };
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var cli = fixture.Services.GetRequiredService<KiAusfuehrungsService>();
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
                scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().ConfirmReadyAsync(eventId, adapter.CliRunId.ToString("D")));
        Assert.Empty(fixture.Prompts);
        fixture.SetPrompt(taskId, "SCM_READY_V1");
        await fixture.DispatchAsync(eventId);
        item = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", item.Status);
        Assert.NotNull(item.DeliveryAttemptId);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>A deliberate ready confirmation is an explicit retry and lifts an earlier hard stop.</summary>
    [Fact]
    public async Task ConfirmReady_LiftsNextAttemptLock()
    {
        await using var fixture = new ScmDispatchFixture { Prompt = "SCM_UNKNOWN_V1" };
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var cli = fixture.Services.GetRequiredService<KiAusfuehrungsService>();
        var adapter = cli.GetScmPromptSession(taskId)!;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            await db.ScmIncomingEvents.ExecuteUpdateAsync(x => x.SetProperty(p => p.NextAttemptUtc, (long?)long.MaxValue));
        }
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().ConfirmReadyAsync(eventId, adapter.CliRunId.ToString("D"));
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", item.Status);
        Assert.NotNull(item.DeliveryAttemptId);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>A CLI without a prompt-format profile waits honestly without offering a confirmation.</summary>
    [Fact]
    public async Task UnknownCliFormat_WaitsWithoutConfirmOption()
    {
        await using var fixture = new ScmDispatchFixture(ki: new ScmKiProfile("Softwareschmiede.GitHubCopilot", string.Empty,
            "GitHub Copilot CLI", ">", new Dictionary<string, string?>()));
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("Promptformat", item.Reason);
        Assert.Contains("unbekannt", item.Reason);
        Assert.False(ScmCliWaitReason.IsConfirmable(item.Reason));
        var adapter = fixture.Services.GetRequiredService<KiAusfuehrungsService>().GetScmPromptSession(taskId)!;
        Assert.Equal(ScmCliRecognitionState.Unknown, adapter.CliRecognition.State);
        using var scope = fixture.Services.CreateScope();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().ConfirmReadyAsync(eventId, adapter.CliRunId.ToString("D")));
        Assert.Contains("unbekannt", error.Message);
        Assert.Empty(fixture.Prompts);
    }

    /// <summary>An explicit confirmation of an out-of-range version lets the file-tool channel probe prove access.</summary>
    [Fact]
    public async Task ConfirmReady_UnsupportedVersion_DeliversAfterChannelProbe()
    {
        var prompts = new List<string>();
        var fixture = default(ScmDispatchFixture);
        fixture = new ScmDispatchFixture(ki: ScmKiProfile.Devin(banner: "Devin CLI\nv3000.12.7",
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
            Assert.Contains("v3000.12.7", waiting.Reason);
            Assert.Contains("nicht unterstützt", waiting.Reason);
            var adapter = fixture.Services.GetRequiredService<KiAusfuehrungsService>().GetScmPromptSession(taskId)!;
            Assert.Equal(ScmCliRecognitionState.Unsupported, adapter.CliRecognition.State);
            using (var scope = fixture.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().ConfirmReadyAsync(eventId, adapter.CliRunId.ToString("D"));
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
}
