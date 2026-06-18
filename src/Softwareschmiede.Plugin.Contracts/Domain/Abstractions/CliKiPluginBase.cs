using System.Diagnostics;
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

    public abstract string PluginName { get; }
    public abstract string PluginPrefix { get; }
    public abstract PluginType PluginType { get; }
    public abstract IReadOnlyList<PluginSettingGroup> GetSettingGroups();

    /// <summary>Konstruiert ProcessStartInfo für den CLI-Aufruf.</summary>
    /// <param name="localRepoPath">Lokales Arbeitsverzeichnis.</param>
    /// <param name="parameters">Optionale Parameter (z.B. Session-ID).</param>
    protected abstract ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters);

    /// <inheritdoc/>
    public Task<ProcessStartInfo> StartCliAsync(string localRepoPath, string? parameters = null, CancellationToken ct = default)
    {
        var psi = BuildProcessStartInfo(localRepoPath, parameters);
        return Task.FromResult(psi);
    }

    /// <inheritdoc/>
    public virtual string GetProcessWindowTitle(Guid aufgabeId) => string.Empty;

    /// <inheritdoc/>
    public abstract bool SupportsSessionContinuation();

    /// <inheritdoc/>
    public abstract Task<bool> CheckHealthAsync(CancellationToken ct = default);

    /// <summary>
    /// Liest alle Agenten aus dem angegebenen Unterverzeichnis eines Agentenpakets.
    /// </summary>
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
