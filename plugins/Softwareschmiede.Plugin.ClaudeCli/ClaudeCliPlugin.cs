using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Claude-CLI Plugin für KI-gestützte Entwicklung.</summary>
public sealed class ClaudeCliPlugin : CliKiPluginBase
{
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<ClaudeCliPlugin> _logger;

    /// <inheritdoc/>
    public override string PluginName => "Claude CLI";

    /// <inheritdoc/>
    public override string ProviderDateiPraefix => "claude";

    /// <inheritdoc/>
    public override string PluginPrefix => "Softwareschmiede.ClaudeCli";

    /// <inheritdoc/>
    public override PluginType PluginType => PluginType.DevelopmentAutomation;

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() =>
    [
        new PluginSettingGroup("Authentifizierung",
        [
            new PluginSettingField(
                Key: "Token",
                Label: "Anthropic API Key",
                FieldType: PluginSettingFieldType.Secret,
                Placeholder: "sk-ant-...",
                Description: "Anthropic API Key. Wird als ANTHROPIC_API_KEY-Umgebungsvariable an das claude-CLI übergeben.",
                IsRequired: false)
        ])
    ];

    /// <summary>Erstellt eine neue Instanz von <see cref="ClaudeCliPlugin"/>.</summary>
    public ClaudeCliPlugin(
        ICredentialStore credentialStore,
        ILogger<ClaudeCliPlugin> logger)
    {
        _credentialStore = credentialStore;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override bool SupportsSessionContinuation() => true;

    /// <inheritdoc/>
    public override async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Pruefe Claude-CLI-Plugin-Health.");
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "claude",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Claude-CLI nicht gefunden oder nicht ausführbar.");
            return false;
        }
    }

    /// <inheritdoc/>
    protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters)
    {
        _logger.LogInformation(
            "ClaudeCliPlugin BuildProcessStartInfo (Repo: {RepoPath}, Parameters: {Parameters}).",
            localRepoPath,
            parameters);

        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            WorkingDirectory = localRepoPath,
            UseShellExecute = false,
            CreateNoWindow = false,
        };

        if (!string.IsNullOrWhiteSpace(parameters))
        {
            psi.Arguments = parameters;
        }

        var token = _credentialStore.GetCredential("Softwareschmiede.ClaudeCli.Token");
        if (!string.IsNullOrEmpty(token))
        {
            psi.EnvironmentVariables["ANTHROPIC_API_KEY"] = token;
        }

        return psi;
    }
}
