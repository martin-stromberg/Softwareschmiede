using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

/// <summary>Real SQLite and services with isolated SCM transport and deterministic terminal boundary.</summary>
internal sealed class ScmDispatchFixture : IAsyncDisposable
{
    internal const string KiPrefix = "Softwareschmiede.KiSimulator";
    internal readonly string Root;
    internal readonly Mock<IGitPlugin> Git = new();
    internal readonly Mock<IKiPlugin> Ki = new();
    internal readonly Dictionary<Guid, Mock<ITerminalSession>> Sessions = new();
    internal readonly List<string> Prompts = [];
    internal readonly List<ScmResponseRequest> Responses = [];
    internal readonly ScmEventRepository Repository = new("test-scm", "github.com", "R_1", "owner", "repo", "U_1", "U_1", "owner", true);
    internal ServiceProvider Services { get; }
    internal Func<Task>? CloneGate { get; set; }
    internal string Prompt { get; set; } = "SCM_READY_V1";
    private readonly List<Process> _processes = [];
    private readonly bool _ownsRoot;

    internal ScmDispatchFixture(string? root = null, TimeProvider? time = null)
    {
        Root = root ?? Path.Combine(Path.GetTempPath(), "scm-dispatch-" + Guid.NewGuid().ToString("N"));
        _ownsRoot = root is null;
        Directory.CreateDirectory(Root);
        Git.SetupGet(x => x.PluginPrefix).Returns(Repository.ProviderPrefix);
        Git.SetupGet(x => x.PluginName).Returns("Test SCM");
        Git.As<IScmEventSource>().Setup(x => x.GetEventRepositoryAsync(It.IsAny<ScmRepositoryContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(Repository);
        Git.As<IScmEventSource>().Setup(x => x.ReadEventsAsync(It.IsAny<ScmEventReadRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmEventReadRequest request, CancellationToken _) => Task.FromResult(new ScmEventBatch(request, [])));
        Git.As<IScmEventResponder>().Setup(x => x.ExecuteResponseAsync(It.IsAny<ScmResponseRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ScmResponseRequest request, CancellationToken _) => { Responses.Add(request); return Task.FromResult(new ScmResponseResult("remote", null, false)); });
        Git.Setup(x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string directory, CancellationToken _) =>
            {
                if (CloneGate is { } gate) await gate();
                InitGit(directory);
                await File.WriteAllTextAsync(Path.Combine(directory, "owned.txt"), "preserve workspace");
            });
        Ki.SetupGet(x => x.PluginPrefix).Returns(KiPrefix);
        Ki.SetupGet(x => x.PluginName).Returns("Simulator");
        Ki.Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Ki.Setup(x => x.GetTerminalStartSpecAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns((string directory, string? _, CancellationToken _) => Task.FromResult(new TerminalSessionStartSpec
            { FileName = "fixture", WorkingDirectory = directory, EnvironmentVariables = new Dictionary<string, string?> { ["SOFTWARESCHMIEDE_SCM_SIMULATOR"] = "1" } }));
        var plugins = new Mock<IPluginManager>();
        plugins.Setup(x => x.GetSourceCodeManagementPlugins()).Returns([Git.Object]);
        plugins.Setup(x => x.GetDefaultSourceCodeManagementPlugin()).Returns(Git.Object);
        plugins.Setup(x => x.GetDevelopmentAutomationPlugins()).Returns([Ki.Object]);
        var directories = new Mock<IArbeitsverzeichnisResolver>();
        directories.Setup(x => x.ResolveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ArbeitsverzeichnisResolutionResult(Root, false, "test", Root));
        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(x => x.StartAsync(It.IsAny<Guid>(), It.IsAny<TerminalSessionStartSpec>(), It.IsAny<ITerminalOutputSink?>(), It.IsAny<Func<CancellationToken, Task<bool>>?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid taskId, TerminalSessionStartSpec _, ITerminalOutputSink? _, Func<CancellationToken, Task<bool>>? _, CancellationToken _) =>
            {
                var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 300 127.0.0.1 > nul") { UseShellExecute = false, CreateNoWindow = true })!;
                _processes.Add(process);
                var session = new Mock<ITerminalSession>();
                session.SetupGet(x => x.Process).Returns(process);
                session.SetupGet(x => x.Buffer).Returns(Screen(Prompt));
                session.Setup(x => x.WritePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns((string prompt, CancellationToken _) => { Prompts.Add(prompt); return Task.CompletedTask; });
                Sessions.Add(taskId, session);
                return Task.FromResult(new TerminalSessionStartResult(process, session.Object, false));
            });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<SoftwareschmiededDbContext>(options => options.UseSqlite($"Data Source={Path.Combine(Root, "events.db")};Pooling=False"));
        services.AddSingleton(plugins.Object);
        services.AddSingleton(Git.Object);
        services.AddSingleton(directories.Object);
        services.AddSingleton(factory.Object);
        services.AddSingleton(time ?? TimeProvider.System);
        services.AddSingleton<IOptions<TerminalSessionOptions>>(Options.Create(new TerminalSessionOptions()));
        services.AddSingleton<IOptions<ScmEventOptions>>(Options.Create(new ScmEventOptions()));
        services.AddSingleton<KiAusfuehrungsService>();
        services.AddSingleton<ScmEventStartupGate>();
        services.AddSingleton<ScmEventChangeNotifier>();
        services.AddSingleton<ScmEventExchangeProtocol>();
        services.AddSingleton<ScmEventMonitoringService>();
        services.AddScoped<TodoService>();
        services.AddScoped<AufgabeService>();
        services.AddScoped<ProtokollService>();
        services.AddScoped<AppEinstellungService>();
        services.AddScoped<PluginDefaultSettingsService>();
        services.AddScoped<PluginActivationService>();
        services.AddScoped<PluginSelectionService>();
        services.AddScoped<ScmEventStore>();
        services.AddScoped<ScmEventOutbox>();
        services.AddScoped<ScmEventOrchestrator>();
        services.AddScoped(sp => new EntwicklungsprozessService(sp.GetRequiredService<AufgabeService>(), sp.GetRequiredService<ProtokollService>(),
            Git.Object, sp.GetRequiredService<PluginSelectionService>(), directories.Object,
            new EntwicklungsprozessServiceOptions(KiAusfuehrungsService: sp.GetRequiredService<KiAusfuehrungsService>()), sp.GetRequiredService<ILogger<EntwicklungsprozessService>>()));
        Services = services.BuildServiceProvider();
    }

    internal async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>().Database.EnsureCreatedAsync();
        Services.GetRequiredService<ScmEventStartupGate>().Ready();
    }

    internal async Task<(Guid TaskId, Guid EventId)> AddAsync(AufgabeStatus status = AufgabeStatus.Neu, bool configured = false, bool cloned = true)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var project = new Projekt { Id = Guid.NewGuid(), Name = "SCM" };
        var repository = new GitRepository { Id = Guid.NewGuid(), ProjektId = project.Id, RepositoryUrl = "https://github.com/owner/repo", PluginTyp = Repository.ProviderPrefix, RepositoryName = "repo" };
        var task = new Aufgabe { Id = Guid.NewGuid(), Titel = "SCM task", ProjektId = project.Id, GitRepositoryId = repository.Id, Status = status, KiPluginPrefix = configured ? KiPrefix : null };
        if (cloned) { task.LokalerKlonPfad = Path.Combine(Root, task.Id.ToString("N")); InitGit(task.LokalerKlonPfad); }
        db.AddRange(project, repository, task);
        await db.SaveChangesAsync();
        var time = DateTimeOffset.UtcNow;
        var observation = new ScmEventObservation(new(Repository, ScmReferenceKind.Issue, 7), ScmContentKind.IssueComment, Guid.NewGuid().ToString(), "42",
            time, time, null, "U_1", "owner", null, null, "$Softwareschmiede test", "https://github.com/owner/repo/issues/7", time);
        await scope.ServiceProvider.GetRequiredService<ScmEventStore>().AcceptBatchAsync(task.Id, task.Id.ToString(), new(new(observation.Reference, observation.ContentKind, null, time), [observation]));
        return (task.Id, await db.ScmIncomingEvents.Where(x => x.AufgabeId == task.Id).Select(x => x.Id).SingleAsync());
    }

    internal async Task DispatchAsync(Guid id)
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().DispatchAsync(id);
    }

    internal async Task<ScmIncomingEvent> EventAsync(Guid id)
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>().ScmIncomingEvents.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    internal void SetPrompt(Guid task, string prompt)
    {
        Sessions[task].SetupGet(x => x.Buffer).Returns(Screen(prompt));
        Sessions[task].Raise(x => x.BufferChanged += null, EventArgs.Empty);
    }

    private static TerminalBuffer Screen(string prompt)
    {
        var buffer = new TerminalBuffer(120, 10);
        buffer.Apply(new TextWrittenEvent("SCM_SIMULATOR_PROTOCOL_V1"));
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
        if (_ownsRoot) Directory.Delete(Root, true);
    }
}
