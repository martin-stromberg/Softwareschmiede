using System.IO;
using System.Text.Json;

namespace Softwareschmiede.App.Services.Testing;

// Bewusst internal: ein Datei-Fixture, keine Anwendungs-Steuerungs-API.
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
            throw new InvalidOperationException("SCM-TestRoot fehlt.");
        foreach (var candidate in new[] { path, databasePath, config.ScenarioPath, config.ResponsesPath, config.CallsPath, config.SimulatorDirectory })
        {
            var relative = Path.GetRelativePath(config.TestRoot, candidate);
            if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("SCM-Fixturepfad liegt außerhalb der TestRoot.");
            for (var current = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(candidate))!); current is not null; current = current.Parent)
                if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new InvalidOperationException("SCM-Fixturepfad darf keine Verzeichnisumleitung verwenden.");
        }
        _ = config.ReadScenario(); // No silent fall back to production on malformed configuration.
        return config;
    }

    public ScmE2EScenario ReadScenario()
    {
        // Die Fixture veröffentlicht Snapshots per Umbenennung, während der Monitor sie liest.
        // Delete-Sharing hält diesen atomaren Austausch unter Windows legal.
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
    // Simuliert ein erschöpftes REST-Primärkontingent: Nicht-GraphQL-Endpunkte liefern 403 mit
    // X-RateLimit-Remaining/Reset; der GraphQL-Transport (eigenes Kontingent) läuft weiter.
    public bool RateLimitRest { get; set; }
    public List<ScmE2EText> Text { get; set; } = [];
    public List<ScmE2ENotification> Notifications { get; set; } = [];
    public List<ScmE2EAssignment> Assignments { get; set; } = [];
    // Reale Head-SHAs im Fixture-Origin angelegter PR-Quellbranches; ohne Eintrag gilt der
    // Platzhalter "sha{n}". Die Head-SHA-Prüfung beim Checkout verlangt den echten Wert.
    public Dictionary<int, string> PullRequestHeadShas { get; set; } = [];
}

// Inbox-Eintrag der Notifications-API; eine abweichende RepositoryNodeId steht für ein
// nicht registriertes Fremd-Repository.
internal sealed record ScmE2ENotification(
    int Number,
    bool PullRequest,
    string RepositoryNodeId,
    string Reason,
    DateTimeOffset UpdatedAt,
    string ThreadId);

internal sealed class ScmE2EText
{
    public int Number { get; set; }
    public string Kind { get; set; } = "IssueComment";
    public bool PullRequest { get; set; }
    public long Id { get; set; }
    public string? Title { get; set; }
    public string Body { get; set; } = string.Empty;
    public string Author { get; set; } = "scm-owner";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow.AddMinutes(-1);
    public DateTimeOffset? EditedAt { get; set; }
    public List<string> ViewerReactions { get; set; } = [];
    public string NodeId => $"SCM_{Kind}_{Id}";
}

// Eintrag des Issue-Events-Endpunkts (issues/{n}/events); EventType ist "assigned" oder
// "unassigned" und gilt für Issue- wie PR-Referenzen gleichermaßen.
internal sealed record ScmE2EAssignment(
    int Number,
    string EventNodeId,
    string EventType,
    string AssignerLogin,
    string AssignerNodeId,
    string AssigneeLogin,
    string AssigneeNodeId,
    DateTimeOffset CreatedAt);

internal sealed record ScmE2ERemoteResponse(string Endpoint, long Id, string Body, string? Content);
