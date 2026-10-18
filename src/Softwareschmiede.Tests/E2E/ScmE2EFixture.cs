using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.App.Services.Testing;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.E2E;

internal sealed class ScmE2EFixture
{
    public ScmE2ETestConfiguration Configuration { get; } = new()
    {
        TestRoot = Path.Combine(Path.GetTempPath(), "softwareschmiede_scm_e2e_" + Guid.NewGuid().ToString("N"))
    };
    public ScmE2EScenario Scenario { get; } = new();
    public string DatabasePath => Path.Combine(Configuration.TestRoot, "test.db");
    public string ConfigurationPath => Path.Combine(Configuration.TestRoot, "scm-config.json");
    public Dictionary<int, Guid> TaskIds { get; } = [];

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Configuration.TestRoot);
        Directory.CreateDirectory(Configuration.SimulatorDirectory);
        File.WriteAllText(ConfigurationPath, JsonSerializer.Serialize(Configuration));
        SaveScenario();
        Hold("accepted", true);
        Hold("complete", true);
        await using var db = OpenDatabase();
        await db.Database.MigrateAsync();
        var settings = new AppEinstellungService(db, NullLogger<AppEinstellungService>.Instance);
        await settings.SetUpdateSettingsAsync(new UpdateSettings(UpdateMode.Aus, false));
        await settings.SetSettingAsync(AppEinstellungService.DefaultKiPluginKey, null);
        await settings.SetSettingAsync(ArbeitsverzeichnisSettingsService.RepositoriesWorkdirKey, Configuration.TestRoot);
        var origin = Path.Combine(Configuration.TestRoot, "origin");
        Directory.CreateDirectory(origin);
        await GitAsync(origin, "init", "--initial-branch=main");
        await GitAsync(origin, "-c", "user.name=SCM E2E", "-c", "user.email=scm-e2e@example.invalid", "commit", "--allow-empty", "-m", "fixture origin");
        var project = new Projekt { Id = Guid.NewGuid(), Name = "SCM E2E", Status = ProjektStatus.Aktiv, ErstellungsDatum = DateTimeOffset.UtcNow };
        var repository = new GitRepository { Id = Guid.NewGuid(), Projekt = project, PluginTyp = "Softwareschmiede.GitHub", RepositoryName = "scm-e2e", RepositoryUrl = "https://github.com/scm-owner/scm-e2e" };
        db.Add(repository);
        for (var number = 1; number <= 17; number++)
        {
            var directory = Path.Combine(Configuration.TestRoot, "workspace-" + number);
            Directory.CreateDirectory(directory);
            await GitAsync(directory, "init", "--initial-branch=main");
            await GitAsync(directory, "-c", "user.name=SCM E2E", "-c", "user.email=scm-e2e@example.invalid", "commit", "--allow-empty", "-m", "fixture");
            var task = new Aufgabe
            {
                Id = Guid.NewGuid(),
                Projekt = project,
                GitRepository = repository,
                Titel = "SCM Aufgabe " + number,
                Status = number switch { 3 => AufgabeStatus.Beendet, 4 => AufgabeStatus.Archiviert, 6 => AufgabeStatus.Gestartet, _ => AufgabeStatus.Neu },
                LokalerKlonPfad = number == 13 ? null : directory,
                BranchName = number == 13 ? null : "main",
                BasisBranchName = "main",
                ErstellungsDatum = DateTimeOffset.UtcNow,
                PausiertBisUtc = number == 5 ? DateTimeOffset.UtcNow.AddHours(2) : null
            };
            TaskIds[number] = task.Id;
            if (number is 7 or 9)
                task.PullRequests.Add(new PullRequestReferenz
                {
                    Id = Guid.NewGuid(),
                    Aufgabe = task,
                    Provider = PullRequestProvider.GitHub,
                    RepositoryId = repository.RepositoryUrl,
                    PullRequestNumber = number,
                    Url = $"https://github.com/scm-owner/scm-e2e/pull/{number}",
                    Titel = task.Titel,
                    CreatedUtc = DateTimeOffset.UtcNow,
                    MonitoringPhase = PullRequestMonitoringPhase.NotMonitored
                });
            else
                task.IssueReferenz = new IssueReferenz
                {
                    Id = Guid.NewGuid(),
                    Aufgabe = task,
                    IssueNummer = number,
                    Titel = task.Titel,
                    IssueUrl = $"https://github.com/scm-owner/scm-e2e/issues/{number}"
                };
            db.Add(task);
        }
        await db.SaveChangesAsync();
    }

    public IReadOnlyDictionary<string, string?> EnvironmentVariables() => new Dictionary<string, string?>
    {
        [ScmE2ETestConfiguration.EnvironmentVariable] = ConfigurationPath,
        ["SOFTWARESCHMIEDE_TEST_DB_PATH"] = DatabasePath
    };

    public void AddText(int number, long id, string body, string kind = "IssueComment")
    {
        Scenario.Texts.Add(new ScmE2EText { Number = number, Id = id, Body = body, Kind = kind, PullRequest = number is 7 or 9 });
        SaveScenario();
    }

    public void SaveScenario()
    {
        var temporary = Configuration.ScenarioPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(Scenario));
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            try { File.Move(temporary, Configuration.ScenarioPath, true); return; }
            catch (IOException) when (DateTime.UtcNow < deadline) { Thread.Sleep(25); }
            catch (UnauthorizedAccessException) when (DateTime.UtcNow < deadline) { Thread.Sleep(25); }
        }
    }

    public void Hold(string gate, bool enabled) => Control("hold-" + gate, enabled);
    public void Control(string name, bool enabled)
    {
        var path = Path.Combine(Configuration.SimulatorDirectory, name);
        if (enabled) File.WriteAllText(path, "fixture");
        else File.Delete(path);
    }

    public SoftwareschmiededDbContext OpenDatabase() => new(new DbContextOptionsBuilder<SoftwareschmiededDbContext>().UseSqlite($"Data Source={DatabasePath}").Options);

    public Task<ScmIncomingEvent> WaitEventAsync(int number, string status, long? remoteId = null)
        => WaitEventCoreAsync(TaskIds[number], status, remoteId, "SCM Aufgabe " + number);

    public Task<ScmIncomingEvent> WaitEventForTaskAsync(Guid taskId, string status, long? remoteId = null)
        => WaitEventCoreAsync(taskId, status, remoteId, "Aufgabe " + taskId);

    private async Task<ScmIncomingEvent> WaitEventCoreAsync(Guid taskId, string status, long? remoteId, string label)
    {
        ScmIncomingEvent? last = null;
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            await using var db = OpenDatabase();
            var events = await db.ScmIncomingEvents.AsNoTracking().Where(x => x.AufgabeId == taskId).OrderBy(x => x.Sequence).ToListAsync();
            last = remoteId is null ? events.LastOrDefault() : events.SingleOrDefault(x => ScmEventStore.Observation(x).RemoteNodeId.EndsWith("_" + remoteId, StringComparison.Ordinal));
            if (last?.Status == status) return last;
            await Task.Delay(100);
        }
        throw new TimeoutException($"{label}: erwartet {status}, tatsächlich {last?.Status}: {last?.Reason}. Fixture: {Configuration.TestRoot}");
    }

    public async Task WaitForPollAsync()
    {
        var before = ReadLines(Configuration.CallsPath).Count;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (ReadLines(Configuration.CallsPath).Count >= before + 8) return;
            await Task.Delay(100);
        }
        throw new TimeoutException("SCM-Fixture: kein weiterer produktiver Monitorlauf.");
    }

    public IReadOnlyList<ScmE2ERemoteResponse> Responses() => ReadLines(Configuration.ResponsesPath).Select(x => JsonSerializer.Deserialize<ScmE2ERemoteResponse>(x)!).ToArray();
    public IReadOnlyList<JsonElement> Prompts() => ReadLines(Path.Combine(Configuration.SimulatorDirectory, "prompts.jsonl")).Select(x => JsonSerializer.Deserialize<JsonElement>(x)).ToArray();

    public string BeginRuntimeCommand(int number, bool replace = false)
    {
        var path = Path.Combine(Configuration.TestRoot, "runtime-" + Guid.NewGuid().ToString("N") + ".command");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new ScmE2ERuntimeCommand(TaskIds[number], replace)));
        File.Move(path + ".tmp", path);
        return path;
    }

    public static async Task WaitFileAsync(string path)
    {
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (!File.Exists(path) && DateTime.UtcNow < deadline) await Task.Delay(50);
        Assert.True(File.Exists(path), "Fixture-Signal fehlt: " + path);
    }

    public static async Task<Guid> RuntimeResultAsync(string path)
    {
        await WaitFileAsync(path + ".result");
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (true)
        {
            try
            {
                using var result = JsonDocument.Parse(await File.ReadAllTextAsync(path + ".result"));
                Assert.False(result.RootElement.TryGetProperty("error", out var error), error.ValueKind == JsonValueKind.String ? error.GetString() : null);
                return result.RootElement.GetProperty("runId").GetGuid();
            }
            catch (IOException) when (DateTime.UtcNow < deadline) { }
            catch (JsonException) when (DateTime.UtcNow < deadline) { }
            await Task.Delay(50);
        }
    }

    private static List<string> ReadLines(string path)
    {
        if (!File.Exists(path)) return [];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        // Writer publishes one JSONL record at a time; a trailing partial record is not yet evidence.
        return text.Split('\n').SkipLast(1).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }

    private static async Task GitAsync(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await error + await output);
    }
}
