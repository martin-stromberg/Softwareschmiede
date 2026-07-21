using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Claude-CLI Plugin für KI-gestützte Entwicklung.</summary>
public sealed class ClaudeCliPlugin : CliKiPluginBase, IIssueTemplateTextGenerator
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
        ]),
        new PluginSettingGroup("CLI-Konfiguration",
        [
            new PluginSettingField(
                Key: "CommandLineParameters",
                Label: "Kommandozeilenparameter",
                FieldType: PluginSettingFieldType.CommandLineParameters,
                Description: "Zusätzliche Parameter für den CLI-Aufruf. Beispiel: --model claude-3-sonnet-20250514",
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
    public override async Task<string?> GetCliHelpTextAsync(CancellationToken ct = default)
    {
        var path = await Task.Run(() => _claudeExecutablePath.Value, ct);
        return await RunHelpCommandAsync(path, ct);
    }

    /// <inheritdoc/>
    public override bool SupportsSessionContinuation() => true;

    /// <inheritdoc/>
    public override async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Pruefe Claude-CLI-Plugin-Health.");
        return await CheckHealthWithVersionCommandAsync(_claudeExecutablePath.Value, ct);
    }

    /// <inheritdoc/>
    public Task<string> FillIssueTemplateAsync(string templateBody, string? originalRequirement, CancellationToken ct = default)
    {
        var invocation = BuildIssueTemplateFillInvocation(templateBody, originalRequirement);
        return RunOneShotTextGenerationAsync(invocation.ProcessStartInfo, invocation.StandardInput, ct);
    }

    internal (ProcessStartInfo ProcessStartInfo, string StandardInput) BuildIssueTemplateFillInvocation(
        string templateBody,
        string? originalRequirement)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _claudeExecutablePath.Value,
            WorkingDirectory = Path.GetTempPath(),
        };
        psi.ArgumentList.Add("-p");

        var token = _credentialStore.GetCredential($"{PluginPrefix}.Token");
        if (!string.IsNullOrEmpty(token))
        {
            psi.EnvironmentVariables["ANTHROPIC_API_KEY"] = token;
        }

        return (psi, BuildIssueTemplateFillPrompt(templateBody, originalRequirement));
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

        AppendCommandLineParameters(psi, _credentialStore, PluginPrefix);

        var token = _credentialStore.GetCredential($"{PluginPrefix}.Token");
        if (!string.IsNullOrEmpty(token))
        {
            psi.EnvironmentVariables["ANTHROPIC_API_KEY"] = token;
        }

        return psi;
    }
}
