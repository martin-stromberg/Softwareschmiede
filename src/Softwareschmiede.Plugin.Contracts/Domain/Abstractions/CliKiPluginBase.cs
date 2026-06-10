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

    /// <summary>Erzeugt den Dateinamen für die erste provider-spezifische Kontextdatei.</summary>
    public string BuildContextFileName(Guid aufgabeId)
        => BuildContextFileName(1);

    /// <summary>Erzeugt den Dateinamen für die provider-spezifische Kontextdatei mit Sequenznummer.</summary>
    public string BuildContextFileName(int index)
        => index <= 1
            ? $"{ProviderDateiPraefix}.context.md"
            : $"{ProviderDateiPraefix}.context.{index}.md";

    /// <summary>Erzeugt den Dateinamen für die nächste freie Kontextdatei im Repository.</summary>
    public string BuildContextFileName(string localRepoPath)
        => BuildContextFileName(GetNextContextFileIndex(localRepoPath));

    /// <summary>Erzeugt den Dateimuster-Filter für alle Kontextdateien eines Providers.</summary>
    protected string BuildContextFileMask() => $"{ProviderDateiPraefix}.context*.md";

    /// <summary>Erzeugt den Dateinamen für den aktuellen Kontextlauf im Repository.</summary>
    public string BuildContextFilePath(string localRepoPath)
        => Path.Combine(localRepoPath, BuildContextFileName(localRepoPath));

    /// <summary>Erzeugt den Pfad für die erste Kontextdatei einer Aufgabe.</summary>
    public string BuildContextFilePath(string localRepoPath, Guid aufgabeId)
        => Path.Combine(localRepoPath, BuildContextFileName(aufgabeId));

    /// <summary>Gibt alle vorhandenen provider-spezifischen Kontextdateien in stabiler Reihenfolge zurück.</summary>
    public IReadOnlyList<string> GetContextFileNames(string localRepoPath)
    {
        if (string.IsNullOrWhiteSpace(localRepoPath) || !Directory.Exists(localRepoPath))
        {
            return [];
        }

        return Directory.GetFiles(localRepoPath, BuildContextFileMask(), SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Select(fileName => fileName!)
            .OrderBy(fileName => GetContextFileIndex(fileName) ?? int.MaxValue)
            .ThenBy(fileName => fileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Gibt die zuletzt vorhandene provider-spezifische Kontextdatei im Repository zurück.</summary>
    public string? GetLatestContextFilePath(string localRepoPath)
    {
        var contextFileName = GetContextFileNames(localRepoPath).LastOrDefault();
        return string.IsNullOrWhiteSpace(contextFileName)
            ? null
            : Path.Combine(localRepoPath, contextFileName);
    }

    /// <summary>Entfernt alle provider-spezifischen Kontextdateien aus dem Repository.</summary>
    public void ClearContextFiles(string localRepoPath)
    {
        if (string.IsNullOrWhiteSpace(localRepoPath) || !Directory.Exists(localRepoPath))
        {
            return;
        }

        foreach (var fileName in GetContextFileNames(localRepoPath))
        {
            var filePath = Path.Combine(localRepoPath, fileName);
            TryDeleteFile(filePath);
        }
    }

    /// <summary>Erzeugt den Dateinamen für eine Prompt-Taskdatei eines KI-Runs.</summary>
    public string BuildTaskFileName(Guid runId)
        => $"{runId}.{ProviderDateiPraefix}.task.md";

    /// <summary>Erzeugt den Dateimaske für eine Prompt-Taskdatei eines KI-Runs.</summary>
    protected string BuildTaskFileMask() => $"*.{ProviderDateiPraefix}.task.md";

    /// <summary>Erzeugt den Pfad für eine Prompt-Taskdatei eines KI-Runs.</summary>
    public string BuildTaskFilePath(string localRepoPath, Guid runId)
        => Path.Combine(localRepoPath, BuildTaskFileName(runId));

    /// <summary>Erzeugt den Prompt-Text für den CLI-Aufruf.</summary>
    protected string BuildCliPrompt(string localRepoPath, string taskFilePath, bool includeContext)
    {
        var lines = new List<string>();

        lines.Add($"Aktuelle Anfrage: {Path.GetFileName(taskFilePath)}");

        if (includeContext)
        {
            var contextFiles = GetContextFileNames(localRepoPath);
            if (contextFiles.Count > 0)
            {
                lines.Add($"Für das Verständnis des bisherigen Chatverlaufs schau in den Kontextdateien nach:");
                lines.Add($"Bisheriger Kontext: {string.Join(", ", contextFiles.Reverse())}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

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
        {
            throw new ArgumentException("Directory path must not be empty.", nameof(directoryPath));
        }

        var gitFolder = Path.Combine(directoryPath, ".git");
        if (!Directory.Exists(gitFolder))
        {
            return;
        }

        var gitignorePath = Path.Combine(directoryPath, ".gitignore");

        var existingLines = File.Exists(gitignorePath)
            ? File.ReadAllLines(gitignorePath).ToList()
            : new List<string>();

        var changed = false;

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
        EnsureGitignoreEntries(directoryPath, BuildTaskFileMask(), BuildContextFileMask());
    }

    public const string IncludeContextMarker = "[[INCLUDE_CONTEXT_FILE_REFERENCE]]";

    public static string MarkPromptToIncludeContextFile(string prompt) =>
        $"{IncludeContextMarker}\n{prompt}";

    public static (string Prompt, bool IncludeContext) UnwrapPromptContextMarker(string prompt)
    {
        if (prompt.StartsWith(IncludeContextMarker, StringComparison.Ordinal))
        {
            return (prompt[IncludeContextMarker.Length..].TrimStart('\r', '\n'), true);
        }

        return (prompt, false);
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

    private int GetNextContextFileIndex(string localRepoPath)
    {
        var highestIndex = GetContextFileNames(localRepoPath)
            .Select(GetContextFileIndex)
            .Where(index => index is not null)
            .Select(index => index!.Value)
            .DefaultIfEmpty(0)
            .Max();

        return highestIndex + 1;
    }

    private static int? GetContextFileIndex(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        const string marker = ".context";
        var markerIndex = fileName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var suffix = fileName[(markerIndex + marker.Length)..^3];
        if (string.IsNullOrEmpty(suffix))
        {
            return 1;
        }

        if (suffix.StartsWith(".", StringComparison.Ordinal) && int.TryParse(suffix[1..], out var index) && index > 0)
        {
            return index;
        }

        return null;
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

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
