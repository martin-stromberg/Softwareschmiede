using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.Terminal;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.TestSupport;

/// <summary>Terminal-Profil der gemockten KI-CLI: Prefix, Startargumente, Banner und Eingabezeile.</summary>
internal sealed record ScmAiProfile(string Prefix, string Arguments, string Banner, string Prompt,
    IReadOnlyDictionary<string, string?> EnvironmentVariables, Func<string, Task>? OnWritePrompt = null)
{
    /// <summary>Bisheriges Verhalten: deterministischer JSON-Simulator im SCM-Modus.</summary>
    internal static ScmAiProfile Simulator => CreateSimulator();

    private static ScmAiProfile CreateSimulator() => new("Softwareschmiede.KiSimulator", string.Empty,
        "SCM_SIMULATOR_PROTOCOL_V1", "SCM_READY_V1",
        new Dictionary<string, string?> { ["SOFTWARESCHMIEDE_SCM_SIMULATOR"] = "1" });

    /// <summary>Devin-Startvertrag und Promptformat; das Banner ist auf den unterstützten Bereich v3000.x voreingestellt.</summary>
    internal static ScmAiProfile Devin(string arguments = "--permission-mode accept-edits --respect-workspace-trust false",
        Func<string, Task>? onWritePrompt = null, string banner = "Devin CLI\nv3000.11.92") => new("Softwareschmiede.Devin", arguments,
        banner, "❭", new Dictionary<string, string?>(), onWritePrompt);
}

/// <summary>Echtes SQLite und echte Services mit isoliertem SCM-Transport und deterministischer Terminal-Grenze.</summary>
internal sealed class ScmDispatchFixture : IAsyncDisposable
{
    internal const string AiPrefix = "Softwareschmiede.KiSimulator";
    internal readonly ScmAiProfile AiProfile;
    internal readonly string Root;
    internal readonly Mock<IGitPlugin> Git = new();
    internal readonly Mock<IAiPlugin> Ai = new();
    /// <summary>Optionales zweites KI-Plugin (z. B. für Fallback-Tests mit deaktiviertem Prefix).</summary>
    internal readonly Mock<IAiPlugin>? Ki2;
    internal readonly ScmAiProfile? Ki2Profile;
    internal readonly Dictionary<Guid, Mock<ITerminalSession>> Sessions = new();
    internal readonly List<TerminalSessionStartSpec> StartSpecs = [];
    internal readonly List<string> Prompts = [];
    internal readonly List<ScmResponseRequest> Responses = [];
    internal readonly List<ScmNotification> Notifications = [];
    internal readonly ScmEventRepository Repository = new("test-scm", "github.com", "R_1", "owner", "repo", "U_1", "U_1", "owner", true);
    /// <summary>Repository-Darstellung des dedizierten Bot-Kontos, wie die Quelle sie im Modus
    /// <see cref="ScmAccountMode.SeparateAccount"/> nach der Auflösung liefert.</summary>
    internal static readonly ScmEventRepository BotRepository = new("test-scm", "github.com", "R_1",
        "owner", "repo", "U_1", "BOT", "scm-bot", true, ScmAccountMode.SeparateAccount);
    internal ServiceProvider Services { get; }
    internal Func<Task>? CloneGate { get; set; }
    internal string Prompt { get; set; }
    private readonly List<Process> _processes = [];
    private readonly bool _ownsRoot;

    internal ScmDispatchFixture(string? root = null, TimeProvider? time = null, ScmAiProfile? ai = null, ScmAiProfile? ki2 = null)
    {
        AiProfile = ai ?? ScmAiProfile.Simulator;
        Ki2Profile = ki2;
        Prompt = AiProfile.Prompt;
        Root = root ?? Path.Combine(Path.GetTempPath(), "scm-dispatch-" + Guid.NewGuid().ToString("N"));
        _ownsRoot = root is null;
        Directory.CreateDirectory(Root);
        Git.SetupGet(x => x.PluginPrefix).Returns(Repository.ProviderPrefix);
        Git.SetupGet(x => x.PluginName).Returns("Test SCM");
        Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(Repository);
        Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmEventReadRequest request, CancellationToken _) => Task.FromResult(new ScmEventBatch(request, [])));
        Git.As<IScmEventSource>().Setup(x => x.EnumerateReferencesAsync(It.IsAny<ScmEventRepository>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScmEventReference>());
        Git.As<IScmEventSource>().Setup(x => x.ReadIssueAsync(It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Issue?)null);
        Git.As<IScmEventSource>().Setup(x => x.ReadPullRequestAsync(It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PullRequest?)null);
        Git.As<IScmEventResponder>().Setup(x => x.ExecuteResponseAsync(It.IsAny<ScmResponseRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmResponseRequest request, CancellationToken _) => { Responses.Add(request); return Task.FromResult(new ScmResponseResult("remote", null, false)); });
        // As<> muss vor dem ersten Git.Object-Zugriff erfolgen; Tests können die leere
        // Standard-Anbindung mit eigenem Setup überschreiben.
        Git.As<IScmAgentAccess>().Setup(x => x.GetAgentEnvironmentAsync(It.IsAny<ScmAccountMode>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>());
        Git.As<IScmNotificationSource>().Setup(x => x.EnumerateNotificationsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .Returns((string _, DateTimeOffset? _, CancellationToken _) => Task.FromResult<IReadOnlyList<ScmNotification>>(Notifications.ToArray()));
        Git.Setup(x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string directory, CancellationToken _) =>
            {
                if (CloneGate is { } gate) await gate();
                InitGit(directory);
                await File.WriteAllTextAsync(Path.Combine(directory, "owned.txt"), "preserve workspace");
            });
        Ai.SetupGet(x => x.PluginPrefix).Returns(AiProfile.Prefix);
        Ai.SetupGet(x => x.PluginName).Returns("Simulator");
        Ai.Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Ai.Setup(x => x.GetTerminalStartSpecAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns((string directory, string? _, CancellationToken _) => Task.FromResult(new TerminalSessionStartSpec
            {
                FileName = "fixture",
                Arguments = AiProfile.Arguments,
                WorkingDirectory = directory,
                EnvironmentVariables = AiProfile.EnvironmentVariables
            }));
        var aiPlugins = new List<IAiPlugin> { Ai.Object };
        if (ki2 is not null)
        {
            Ki2 = new Mock<IAiPlugin>();
            Ki2.SetupGet(x => x.PluginPrefix).Returns(ki2.Prefix);
            Ki2.SetupGet(x => x.PluginName).Returns(ki2.Prefix);
            Ki2.Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Ki2.Setup(x => x.GetTerminalStartSpecAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns((string directory, string? _, CancellationToken _) => Task.FromResult(new TerminalSessionStartSpec
                {
                    FileName = "fixture",
                    Arguments = ki2.Arguments,
                    WorkingDirectory = directory,
                    EnvironmentVariables = ki2.EnvironmentVariables
                }));
            aiPlugins.Add(Ki2.Object);
        }
        var plugins = new Mock<IPluginManager>();
        plugins.Setup(x => x.GetSourceCodeManagementPlugins()).Returns([Git.Object]);
        plugins.Setup(x => x.GetDefaultSourceCodeManagementPlugin()).Returns(Git.Object);
        plugins.Setup(x => x.GetDevelopmentAutomationPlugins()).Returns(aiPlugins);
        var directories = new Mock<IWorkingDirectoryResolver>();
        directories.Setup(x => x.ResolveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new WorkingDirectoryResolutionResult(Root, false, "test", Root));
        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(x => x.StartAsync(It.IsAny<Guid>(), It.IsAny<TerminalSessionStartSpec>(), It.IsAny<ITerminalOutputSink?>(), It.IsAny<Func<CancellationToken, Task<bool>>?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid taskId, TerminalSessionStartSpec spec, ITerminalOutputSink? _, Func<CancellationToken, Task<bool>>? _, CancellationToken _) =>
            {
                StartSpecs.Add(spec);
                var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 300 127.0.0.1 > nul") { UseShellExecute = false, CreateNoWindow = true })!;
                _processes.Add(process);
                var session = new Mock<ITerminalSession>();
                session.SetupGet(x => x.Process).Returns(process);
                session.SetupGet(x => x.Buffer).Returns(Screen(AiProfile.Banner, Prompt));
                session.Setup(x => x.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(async (string prompt, CancellationToken _) =>
                    {
                        if (AiProfile.OnWritePrompt is { } handler) await handler(prompt);
                        else Prompts.Add(prompt);
                    });
                Sessions.Add(taskId, session);
                return Task.FromResult(new TerminalSessionStartResult(process, session.Object, false));
            });
        var services = new ServiceCollection();
        services.AddScmTestServices(Path.Combine(Root, "events.db"), plugins.Object, Git.Object,
            directories.Object, factory.Object, time ?? TimeProvider.System);
        Services = services.BuildServiceProvider();
    }

    internal async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>().Database.EnsureCreatedAsync();
        Services.GetRequiredService<ScmEventStartupGate>().Ready();
    }

    internal async Task<(Guid TaskId, Guid EventId)> AddAsync(WorkItemStatus status = WorkItemStatus.New, bool configured = false, bool cloned = true,
        ScmReferenceKind referenceKind = ScmReferenceKind.Issue, ScmContentKind contentKind = ScmContentKind.IssueComment, string body = "$Softwareschmiede test",
        ScmEventRepository? eventRepository = null, string? authorId = "U_1", string? authorLogin = "owner", string? aiPluginPrefix = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var project = new Project { Id = Guid.NewGuid(), Name = "SCM" };
        var repository = new GitRepository { Id = Guid.NewGuid(), ProjectId = project.Id, RepositoryUrl = "https://github.com/owner/repo", PluginType = Repository.ProviderPrefix, RepositoryName = "repo" };
        var task = new WorkItem { Id = Guid.NewGuid(), Title = "SCM task", ProjectId = project.Id, GitRepositoryId = repository.Id, Status = status, AiPluginPrefix = configured ? (aiPluginPrefix ?? AiProfile.Prefix) : null };
        if (cloned) { task.LocalClonePath = Path.Combine(Root, task.Id.ToString("N")); InitGit(task.LocalClonePath); }
        db.AddRange(project, repository, task);
        await db.SaveChangesAsync();
        var time = DateTimeOffset.UtcNow;
        var url = referenceKind == ScmReferenceKind.PullRequest
            ? "https://github.com/owner/repo/pull/7"
            : "https://github.com/owner/repo/issues/7";
        var observation = new ScmEventObservation(new(eventRepository ?? Repository, referenceKind, 7), contentKind, Guid.NewGuid().ToString(), "42",
            time, time, null, authorId, authorLogin, null, null, body, url, time);
        await scope.ServiceProvider.GetRequiredService<ScmEventStore>().AcceptBatchAsync(task.Id, task.Id.ToString(), new(new(observation.Reference, observation.ContentKind, null, time), [observation]));
        return (task.Id, await db.ScmIncomingEvents.Where(x => x.WorkItemId == task.Id).Select(x => x.Id).SingleAsync());
    }

    /// <summary>Registriert eine Inbox-Notification, die die gemockte <see cref="IScmNotificationSource"/> liefert.</summary>
    internal void AddNotification(ScmNotification notification) => Notifications.Add(notification);

    /// <summary>Lässt <see cref="IScmEventSource.ReadIssueAsync"/> ein Standard-Issue liefern; mit
    /// <paramref name="number"/> nur für Issue-Referenzen dieser Nummer, sonst für jede Referenz.</summary>
    internal void ConfigureIssue(int? number = null)
        => Git.As<IScmEventSource>().Setup(x => x.ReadIssueAsync(
                It.Is<ScmEventReference>(r => r.Kind == ScmReferenceKind.Issue
                    && (!number.HasValue || r.Number == number.Value)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScmEventReference reference, CancellationToken _) => new Issue(reference.Number,
                "Issue Titel " + reference.Number, "Beschreibungstext", Array.Empty<string>(), null,
                "https://github.com/owner/repo/issues/" + reference.Number));

    /// <summary>Lässt <see cref="IScmEventSource.ReadEventsAsync"/> für Issue-Kommentare eine
    /// Mention-Observation mit Bot-Ansprache liefern; der Autor ist über
    /// <paramref name="authorId"/>/<paramref name="authorLogin"/> steuerbar.</summary>
    internal void ConfigureMentionObservations(string? authorId = "U_1", string? authorLogin = "owner")
    {
        var time = DateTimeOffset.UtcNow;
        Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmEventReadRequest request, CancellationToken _) => Task.FromResult(new ScmEventBatch(request,
                request.ContentKind == ScmContentKind.IssueComment ? [new ScmEventObservation(request.Reference, request.ContentKind,
                    "C_" + request.Reference.Number, "42", time, time, null, authorId, authorLogin, null, null,
                    "@scm-bot bitte übernehmen", "https://github.com/owner/repo/issues/" + request.Reference.Number, time)] : [])));
    }

    /// <summary>Der Monitoring-Dienst aus dem Service-Provider der Fixture.</summary>
    internal ScmEventMonitoringService Monitor => Services.GetRequiredService<ScmEventMonitoringService>();

    /// <summary>Der Auslöser der ereignisgesteuerten Startprüfung aus dem Service-Provider der Fixture.</summary>
    internal ScmStartCheckTrigger Trigger => Services.GetRequiredService<ScmStartCheckTrigger>();

    /// <summary>Der ereignisgesteuerte Startprüfungs-Dienst aus dem Service-Provider der Fixture.</summary>
    internal ScmStartCheckService StartCheck => Services.GetRequiredService<ScmStartCheckService>();

    /// <summary>Setzt den Konto-Modus über den <see cref="AppSettingService"/>.</summary>
    internal async Task SetAccountModeAsync(ScmAccountMode mode)
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppSettingService>().SetScmAccountModeAsync(mode);
    }

    /// <summary>Schreibt eine generische Anwendungseinstellung (z. B. Ausführungslimits).</summary>
    internal async Task SetSettingAsync(string key, string? value)
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppSettingService>().SetSettingAsync(key, value);
    }

    /// <summary>Der Singleton-Prozessverwalter der Fixture (für Start/Stop-Prüfungen).</summary>
    internal AiExecutionService Cli => Services.GetRequiredService<AiExecutionService>();

    internal async Task DispatchAsync(Guid id)
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().DispatchAsync(id);
    }

    internal async Task<ScmIncomingEvent> EventAsync(Guid id)
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>().ScmIncomingEvents.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    /// <summary>Vollzieht die Dateitool-Kanalprobe wie ein echtes CLI-Dateitool: Challenge lesen,
    /// temporären Nachweis schreiben und beide Seiten in die Endnamen verschieben.</summary>
    internal static async Task CompleteProbeAsync(string prompt, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(prompt);
        string PathValue(string name) => document.RootElement.GetProperty(name).GetString()!;
        var content = await File.ReadAllBytesAsync(PathValue("challengePath"), ct);
        await File.WriteAllBytesAsync(PathValue("temporaryProofPath"), content, ct);
        File.Move(PathValue("temporaryProofPath"), PathValue("proofPath"));
        File.Move(PathValue("challengePath"), PathValue("readChallengePath"));
    }

    internal void SetPrompt(Guid task, string prompt)
    {
        Sessions[task].SetupGet(x => x.Buffer).Returns(Screen(AiProfile.Banner, prompt));
        Sessions[task].Raise(x => x.BufferChanged += null, EventArgs.Empty);
    }

    private static TerminalBuffer Screen(string banner, string prompt)
    {
        var buffer = new TerminalBuffer(120, 10);
        buffer.Apply(new TextWrittenEvent(banner));
        buffer.Apply(new CursorMovedEvent(2, 0, true));
        buffer.Apply(new TextWrittenEvent(prompt));
        return buffer;
    }

    private static void InitGit(string directory)
    {
        Directory.CreateDirectory(directory);
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("init"); start.ArgumentList.Add("--quiet");
        using var process = Process.Start(start)!;
        process.WaitForExit();
        if (process.ExitCode != 0) throw new IOException(process.StandardError.ReadToEnd());
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var process in _processes)
        {
            try { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
            catch (InvalidOperationException) { }
        }
        await Services.DisposeAsync();
        foreach (var process in _processes) process.Dispose();
        if (_ownsRoot) TempDirectoryCleanup.DeleteBestEffort(Root);
    }
}
