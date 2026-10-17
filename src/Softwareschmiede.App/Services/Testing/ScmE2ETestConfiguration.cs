using System.IO;
using System.Text.Json;

namespace Softwareschmiede.App.Services.Testing;

// Deliberately internal: a file fixture, not an application control API.
internal sealed class ScmE2ETestConfiguration
{
    public const string EnvironmentVariable = "SOFTWARESCHMIEDE_SCM_TEST_CONFIG";
    public string TestRoot { get; init; } = string.Empty;
    public string ScenarioPath => Path.Combine(TestRoot, "scm-scenario.json");
    public string ResponsesPath => Path.Combine(TestRoot, "scm-responses.jsonl");
    public string CallsPath => Path.Combine(TestRoot, "scm-calls.jsonl");
    public string SimulatorDirectory => Path.Combine(TestRoot, "simulator");

    public static ScmE2ETestConfiguration? LoadFromEnvironment(string? databasePath)
    {
        var path = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new InvalidOperationException("SCM-Testkonfiguration erfordert eine isolierte Testdatenbank.");
        var config = JsonSerializer.Deserialize<ScmE2ETestConfiguration>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("Leere SCM-Testkonfiguration.");
        if (string.IsNullOrWhiteSpace(config.TestRoot) || !Directory.Exists(config.TestRoot))
            throw new InvalidOperationException("SCM-Testwurzel fehlt.");
        foreach (var candidate in new[] { path, databasePath, config.ScenarioPath, config.ResponsesPath, config.CallsPath, config.SimulatorDirectory })
        {
            var relative = Path.GetRelativePath(config.TestRoot, candidate);
            if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("SCM-Fixturepfad liegt außerhalb der Testwurzel.");
            for (var current = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(candidate))!); current is not null; current = current.Parent)
                if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new InvalidOperationException("SCM-Fixturepfad darf keine Verzeichnisumleitung verwenden.");
        }
        _ = config.ReadScenario(); // No silent fall back to production on malformed configuration.
        return config;
    }

    public ScmE2EScenario ReadScenario()
    {
        // The fixture publishes snapshots by rename while the monitor reads them.
        // Delete sharing keeps that atomic replacement legal on Windows.
        using var stream = new FileStream(ScenarioPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return JsonSerializer.Deserialize<ScmE2EScenario>(stream)
               ?? throw new InvalidOperationException("Leeres SCM-Testszenario.");
    }
}

internal sealed class ScmE2EScenario
{
    public bool Authenticated { get; set; } = true;
    public bool CanWrite { get; set; } = true;
    public bool RejectAiResponse { get; set; }
    public bool PluginAvailable { get; set; } = true;
    public List<ScmE2EText> Texts { get; set; } = [];
}

internal sealed class ScmE2EText
{
    public int Number { get; set; }
    public string Kind { get; set; } = "IssueComment";
    public bool PullRequest { get; set; }
    public long Id { get; set; }
    public string Body { get; set; } = string.Empty;
    public string Author { get; set; } = "scm-owner";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow.AddMinutes(-1);
    public DateTimeOffset? EditedAt { get; set; }
    public string NodeId => $"SCM_{Kind}_{Id}";
}

internal sealed record ScmE2ERemoteResponse(string Endpoint, long Id, string Body, string? Content);
