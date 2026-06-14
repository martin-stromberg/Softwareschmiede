using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>
/// GitHub Copilot Plugin – nutzt das <c>copilot</c>-CLI für KI-gestützte Entwicklung.
/// </summary>
public sealed class GitHubCopilotPlugin : CliKiPluginBase
{
    private const string ExecutablePathSettingKey = "ExecutablePath";

    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<GitHubCopilotPlugin> _logger;

    /// <inheritdoc/>
    public override string PluginName => "GitHub Copilot";

    /// <inheritdoc/>
    public override string ProviderDateiPraefix => "copilot";

    /// <inheritdoc/>
    public override string PluginPrefix => "Softwareschmiede.GitHubCopilot";

    /// <inheritdoc/>
    public override PluginType PluginType => PluginType.DevelopmentAutomation;

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() =>
    [
        new PluginSettingGroup("Authentifizierung",
        [
            new PluginSettingField(
                Key: "Token",
                Label: "GitHub Token",
                FieldType: PluginSettingFieldType.Secret,
                Placeholder: "ghp_...",
                Description: "GitHub Personal Access Token. Wird als GH_TOKEN-Umgebungsvariable an das copilot-CLI übergeben.",
                IsRequired: false)
        ]),
        new PluginSettingGroup("Ausführung",
        [
            new PluginSettingField(
                Key: ExecutablePathSettingKey,
                Label: "Copilot CLI Pfad",
                FieldType: PluginSettingFieldType.Text,
                Placeholder: "C:\\Program Files\\GitHub Copilot\\copilot.exe",
                Description: "Optionaler absoluter Pfad zur copilot-Executable.",
                IsRequired: false)
        ])
    ];

    /// <summary>Erstellt eine neue Instanz des <see cref="GitHubCopilotPlugin"/>.</summary>
    public GitHubCopilotPlugin(
        ICredentialStore credentialStore,
        ILogger<GitHubCopilotPlugin> logger)
    {
        _credentialStore = credentialStore;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override bool SupportsSessionContinuation() => false;

    /// <inheritdoc/>
    public override async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Pruefe GitHub-Copilot-Plugin-Health.");
        try
        {
            var copilotCommand = GetCopilotCommand();
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = copilotCommand,
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
        catch (Win32Exception)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Copilot-CLI nicht gefunden oder nicht ausführbar.");
            return false;
        }
    }

    /// <inheritdoc/>
    protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters)
    {
        _logger.LogInformation(
            "GitHubCopilotPlugin BuildProcessStartInfo (Repo: {RepoPath}, Parameters: {Parameters}).",
            localRepoPath,
            parameters);

        var copilotCommand = GetCopilotCommand();

        var psi = new ProcessStartInfo
        {
            FileName = copilotCommand,
            WorkingDirectory = localRepoPath,
            UseShellExecute = false,
            CreateNoWindow = false,
        };

        if (!string.IsNullOrWhiteSpace(parameters))
        {
            psi.Arguments = parameters;
        }

        // Umgebungsisolation (USERPROFILE, HOME, APPDATA etc.) wurde bewusst entfernt.
        // Das copilot-CLI benötigt die Standard-Umgebungsvariablen des Benutzerprofils,
        // um seine eigene Konfiguration (Token, OAuth-Cache, Proxy-Einstellungen) zu laden.
        // Eine vollständige Isolation würde dazu führen, dass das CLI keine Authentifizierung findet.
        var token = _credentialStore.GetCredential("Softwareschmiede.GitHub.Token");
        if (!string.IsNullOrEmpty(token))
        {
            psi.EnvironmentVariables["GH_TOKEN"] = token;
        }

        return psi;
    }

    private string GetCopilotCommand()
    {
        var configuredPath = _credentialStore.GetCredential($"{PluginPrefix}.{ExecutablePathSettingKey}");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath.Trim().Trim('"');
        }

        return "copilot";
    }
}
