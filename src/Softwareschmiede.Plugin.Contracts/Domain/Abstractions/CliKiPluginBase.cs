using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Domain.Abstractions;

/// <summary>Gemeinsame Basisklasse für CLI-basierte KI-Plugins.</summary>
public abstract class CliKiPluginBase : IKiPlugin
{
    /// <summary>
    /// Provider-spezifischer Bezeichner für Dateinamen.
    /// Beispiele: <c>copilot</c>, <c>claude</c>.
    /// </summary>
    public abstract string ProviderDateiPraefix { get; }

    /// <summary>Erzeugt den Dateinamen für die Kontextdatei einer Aufgabe.</summary>
    public string BuildContextFileName(Guid aufgabeId)
        => $"{aufgabeId}.{ProviderDateiPraefix}.context.md";

    /// <summary>Erzeugt den Pfad für die Kontextdatei einer Aufgabe.</summary>
    public string BuildContextFilePath(string localRepoPath, Guid aufgabeId)
        => Path.Combine(localRepoPath, BuildContextFileName(aufgabeId));

    /// <summary>Erzeugt den Dateinamen für eine Prompt-Taskdatei eines KI-Runs.</summary>
    public string BuildTaskFileName(Guid runId)
        => $"{runId}.{ProviderDateiPraefix}-task.md";

    /// <summary>Erzeugt den Pfad für eine Prompt-Taskdatei eines KI-Runs.</summary>
    public string BuildTaskFilePath(string localRepoPath, Guid runId)
        => Path.Combine(localRepoPath, BuildTaskFileName(runId));

    public abstract string PluginName { get; }
    public abstract string PluginPrefix { get; }
    public abstract Enums.PluginType PluginType { get; }
    public abstract IReadOnlyList<ValueObjects.PluginSettingGroup> GetSettingGroups();
    public abstract Task<IEnumerable<ValueObjects.AgentInfo>> GetAvailableAgentsAsync(string agentPackagePath, CancellationToken ct = default);
    public abstract Task<bool> IsAgentPackageCompatibleAsync(string agentPackagePath, CancellationToken ct = default);
    public abstract Task DeployAgentPackageAsync(string agentPackagePath, string localRepoPath, CancellationToken ct = default);
    public abstract IAsyncEnumerable<string> StartDevelopmentAsync(string prompt, ValueObjects.AgentInfo agent, string localRepoPath, string? model = null, CancellationToken ct = default);
    public abstract Task<ValueObjects.TestResult> RunTestsAsync(string localRepoPath, CancellationToken ct = default);
    public abstract Task<bool> CheckHealthAsync(CancellationToken ct = default);
}
