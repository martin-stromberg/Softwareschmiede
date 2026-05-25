using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

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
    /// <summary>Erzeugt den Dateimaske für die Kontextdatei einer Aufgabe.</summary>
    protected string BuildContextFileMask() => $"*.{ProviderDateiPraefix}.context.md";

    /// <summary>Erzeugt den Pfad für die Kontextdatei einer Aufgabe.</summary>
    public string BuildContextFilePath(string localRepoPath, Guid aufgabeId)
        => Path.Combine(localRepoPath, BuildContextFileName(aufgabeId));

    /// <summary>Erzeugt den Dateinamen für eine Prompt-Taskdatei eines KI-Runs.</summary>
    public string BuildTaskFileName(Guid runId)
        => $"{runId}.{ProviderDateiPraefix}-task.md";
    /// <summary>Erzeugt den Dateimaske für eine Prompt-Taskdatei eines KI-Runs.</summary>
    protected string BuildTaskFileMask() => $"*.{ProviderDateiPraefix}-task.md";
    /// <summary>Erzeugt den Dateimaske für eine Backupdatei der Prompt-Taskdatei eines KI-Runs.</summary>
    protected string BuildTaskBackupFileMask() => $"{BuildTaskFileMask()}.bak";

    /// <summary>Erzeugt den Pfad für eine Prompt-Taskdatei eines KI-Runs.</summary>
    public string BuildTaskFilePath(string localRepoPath, Guid runId)
        => Path.Combine(localRepoPath, BuildTaskFileName(runId));

    /// <summary>
    /// Liest alle Agenten aus dem angegebenen Unterverzeichnis eines Agentenpakets.
    /// </summary>
    /// <param name="agentPackagePath">Pfad zum Agentenpaket.</param>
    /// <param name="relativeAgentDirectory">Relativer Pfad zum Agentenverzeichnis innerhalb des Pakets.</param>
    /// <returns>Gefundene Agenten in stabiler alphabetischer Reihenfolge.</returns>
    protected static IReadOnlyList<AgentInfo> DiscoverAgents(string agentPackagePath, string relativeAgentDirectory)
    {
        if (string.IsNullOrWhiteSpace(agentPackagePath))
        {
            return [];
        }

        var agentDirectory = Path.Combine(agentPackagePath, relativeAgentDirectory);
        if (!Directory.Exists(agentDirectory))
        {
            return [];
        }

        return Directory.GetFiles(agentDirectory, "*.md", SearchOption.AllDirectories)
            .Select(filePath => new AgentInfo(
                Path.GetFileNameWithoutExtension(filePath).Replace(".agent", string.Empty, StringComparison.OrdinalIgnoreCase),
                ReadAgentDescription(filePath),
                filePath))
            .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    /// <summary>
    /// Stellt sicher, dass die .gitignore-Datei im angegebenen Verzeichnis
    /// die erforderlichen Ignore-Muster enthält. Die Datei wird nur geändert,
    /// wenn das Verzeichnis ein Git-Repository ist (erkennbar am .git-Ordner).
    /// Bereits vorhandene Einträge werden nicht dupliziert.
    /// </summary>
    /// <param name="directoryPath">
    /// Das Zielverzeichnis, in dem die .gitignore-Datei geprüft bzw. erstellt wird.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Wird ausgelöst, wenn der Pfad null oder leer ist.
    /// </exception>
    protected void EnsureGitignoreEntries(string directoryPath, params string[] requiredPatterns)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Directory path must not be empty.", nameof(directoryPath));

        // Prüfen, ob es ein Git-Repository ist
        var gitFolder = Path.Combine(directoryPath, ".git");
        if (!Directory.Exists(gitFolder))
        {
            // Kein Git-Repo → keine Änderungen vornehmen
            return;
        }

        var gitignorePath = Path.Combine(directoryPath, ".gitignore");

        var existingLines = File.Exists(gitignorePath)
            ? File.ReadAllLines(gitignorePath).ToList()
            : new List<string>();

        bool changed = false;

        foreach (var pattern in requiredPatterns)
        {
            if (!existingLines.Any(line => line.Trim() == pattern))
            {
                existingLines.Add(pattern);
                changed = true;
            }
        }

        if (changed)
        {
            File.WriteAllLines(gitignorePath, existingLines);
        }
    }
    /// <summary>
    /// Stellt sicher, dass die .gitignore-Datei im angegebenen Verzeichnis
    /// die erforderlichen Ignore-Muster enthält. Die Datei wird nur geändert,
    /// wenn das Verzeichnis ein Git-Repository ist (erkennbar am .git-Ordner).
    /// Bereits vorhandene Einträge werden nicht dupliziert.
    /// </summary>
    /// <param name="directoryPath">
    /// Das Zielverzeichnis, in dem die .gitignore-Datei geprüft bzw. erstellt wird.
    /// </param>
    protected void EnsureGitignoreEntries(string directoryPath)
    {
        EnsureGitignoreEntries(directoryPath, BuildTaskFileMask(), BuildTaskBackupFileMask(), BuildContextFileMask());
    }

    public abstract string PluginName { get; }
    public abstract string PluginPrefix { get; }
    public abstract PluginType PluginType { get; }
    public abstract IReadOnlyList<PluginSettingGroup> GetSettingGroups();
    public abstract Task<IEnumerable<AgentInfo>> GetAvailableAgentsAsync(string agentPackagePath, CancellationToken ct = default);
    public abstract Task<bool> IsAgentPackageCompatibleAsync(string agentPackagePath, CancellationToken ct = default);
    public abstract Task DeployAgentPackageAsync(string agentPackagePath, string localRepoPath, CancellationToken ct = default);
    public abstract IAsyncEnumerable<string> StartDevelopmentAsync(string prompt, AgentInfo agent, string localRepoPath, string? model = null, CancellationToken ct = default);
    public abstract Task<TestResult> RunTestsAsync(string localRepoPath, CancellationToken ct = default);
    public abstract Task<bool> CheckHealthAsync(CancellationToken ct = default);

    private static string? ReadAgentDescription(string agentFilePath)
    {
        try
        {
            var lines = File.ReadLines(agentFilePath).Take(10).ToList();
            var descLine = lines.FirstOrDefault(line => line.TrimStart().StartsWith("description:", StringComparison.OrdinalIgnoreCase));
            if (descLine is not null)
            {
                return descLine.Split(':', 2).ElementAtOrDefault(1)?.Trim().Trim('"');
            }

            return lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("---", StringComparison.Ordinal));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
