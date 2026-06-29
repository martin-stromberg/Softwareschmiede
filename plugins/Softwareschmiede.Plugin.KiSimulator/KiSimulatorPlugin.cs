using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Deterministisches KI-Simulator-Plugin für Tests und lokale Entwicklung.</summary>
public sealed class KiSimulatorPlugin : CliKiPluginBase
{
    private readonly ILogger<KiSimulatorPlugin> _logger;

    /// <inheritdoc/>
    public override string PluginName => "KI Simulator";

    /// <inheritdoc/>
    public override string ProviderDateiPraefix => "simulator";

    /// <inheritdoc/>
    public override string PluginPrefix => "Softwareschmiede.KiSimulator";

    /// <inheritdoc/>
    public override PluginType PluginType => PluginType.DevelopmentAutomation;

    /// <summary>Erstellt eine neue Instanz von <see cref="KiSimulatorPlugin"/>.</summary>
    public KiSimulatorPlugin(ILogger<KiSimulatorPlugin> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];

    /// <inheritdoc/>
    public override bool SupportsSessionContinuation() => false;

    /// <inheritdoc/>
    public override Task<bool> CheckHealthAsync(CancellationToken ct = default)
        => Task.FromResult(true);

    /// <inheritdoc/>
    protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters)
    {
        _logger.LogInformation(
            "KiSimulator BuildProcessStartInfo (Repo: {RepoPath}, Parameters: {Parameters}).",
            localRepoPath,
            parameters);

        // ping statt timeout.exe verwenden: timeout.exe bricht mit ExitCode 125 ab, wenn kein
        // Konsolen-Handle verfügbar ist.
        return new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c echo KI-Simulator läuft... && ping -n 31 127.0.0.1 > nul",
            WorkingDirectory = localRepoPath,
            UseShellExecute = false,
            CreateNoWindow = false,
        };
    }
}
