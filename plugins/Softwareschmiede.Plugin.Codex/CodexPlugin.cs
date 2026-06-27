using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Codex CLI plugin for AI-assisted development.</summary>
public sealed class CodexPlugin : CliKiPluginBase
{
    private const string ExecutablePathSettingKey = "ExecutablePath";

    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<CodexPlugin> _logger;

    /// <inheritdoc/>
    public override string PluginName => "Codex CLI";

    /// <inheritdoc/>
    public override string ProviderDateiPraefix => "codex";

    /// <inheritdoc/>
    public override string PluginPrefix => "Softwareschmiede.Codex";

    /// <inheritdoc/>
    public override PluginType PluginType => PluginType.DevelopmentAutomation;

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() =>
    [
        new PluginSettingGroup("Ausfuehrung",
        [
            new PluginSettingField(
                Key: ExecutablePathSettingKey,
                Label: "Codex CLI Pfad",
                FieldType: PluginSettingFieldType.Text,
                Placeholder: "C:\\Program Files\\Codex\\codex.exe",
                Description: "Optionaler absoluter Pfad zur codex-Executable.",
                IsRequired: false)
        ])
    ];

    /// <summary>Creates a new <see cref="CodexPlugin"/> instance.</summary>
    public CodexPlugin(
        ICredentialStore credentialStore,
        ILogger<CodexPlugin> logger)
    {
        _credentialStore = credentialStore;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override bool SupportsSessionContinuation() => false;

    /// <inheritdoc/>
    public override async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Pruefe Codex-CLI-Plugin-Health.");
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GetCodexCommand(),
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
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            }

            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Codex-CLI nicht gefunden oder nicht ausfuehrbar.");
            return false;
        }
    }

    /// <inheritdoc/>
    protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters)
    {
        _logger.LogInformation(
            "CodexPlugin BuildProcessStartInfo (Repo: {RepoPath}, Parameters: {Parameters}).",
            localRepoPath,
            parameters);

        var psi = new ProcessStartInfo
        {
            FileName = GetCodexCommand(),
            WorkingDirectory = localRepoPath,
            UseShellExecute = false,
            CreateNoWindow = false,
        };

        if (!string.IsNullOrWhiteSpace(parameters))
        {
            psi.Arguments = parameters;
        }

        return psi;
    }

    private string GetCodexCommand()
    {
        var configuredPath = _credentialStore.GetCredential($"{PluginPrefix}.{ExecutablePathSettingKey}");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath.Trim().Trim('"');
        }

        return "codex";
    }
}
