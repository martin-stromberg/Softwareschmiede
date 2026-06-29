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
        ]),
        new PluginSettingGroup("CLI-Konfiguration",
        [
            new PluginSettingField(
                Key: "CommandLineParameters",
                Label: "Kommandozeilenparameter",
                FieldType: PluginSettingFieldType.CommandLineParameters,
                Description: "Zusätzliche Parameter für den codex-CLI-Aufruf",
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
    public override Task<string?> GetCliHelpTextAsync(CancellationToken ct = default)
        => RunHelpCommandAsync(GetCodexCommand(), ct);

    /// <inheritdoc/>
    public override bool SupportsSessionContinuation() => false;

    /// <inheritdoc/>
    public override async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Pruefe Codex-CLI-Plugin-Health.");
        return await CheckHealthWithVersionCommandAsync(GetCodexCommand(), ct);
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

        AppendCommandLineParameters(psi, _credentialStore, PluginPrefix);

        return psi;
    }

    private string GetCodexCommand()
        => ResolveExecutablePath(_credentialStore, PluginPrefix, "codex");
}
