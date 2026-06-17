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

    private static readonly Lazy<string> _claudeExecutablePath = new(FindClaudeExecutable);

    /// <summary>Erstellt eine neue Instanz von <see cref="ClaudeCliPlugin"/>.</summary>
    public ClaudeCliPlugin(
        ICredentialStore credentialStore,
        ILogger<ClaudeCliPlugin> logger)
    {
        _credentialStore = credentialStore;
        _logger = logger;
    }

    private static string FindClaudeExecutable()
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;
            foreach (var ext in new[] { ".exe", ".cmd", ".bat", string.Empty })
            {
                var candidate = Path.Combine(dir.Trim(), $"claude{ext}");
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        return "claude";
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
                    FileName = _claudeExecutablePath.Value,
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
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignorieren */ }
            }
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
            FileName = _claudeExecutablePath.Value,
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
