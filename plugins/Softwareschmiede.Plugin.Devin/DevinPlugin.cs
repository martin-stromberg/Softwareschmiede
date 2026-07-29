using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Devin-CLI Plugin fuer KI-gestuetzte Entwicklung.</summary>
public sealed class DevinPlugin : CliKiPluginBase
{
    private const string ExecutablePathSettingKey = "ExecutablePath";

    private static readonly Lazy<string> _devinExecutablePath = new(FindDevinExecutable);

    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<DevinPlugin> _logger;

    /// <inheritdoc/>
    public override string PluginName => "Devin CLI";

    /// <inheritdoc/>
    public override string ProviderDateiPraefix => "devin";

    /// <inheritdoc/>
    public override string PluginPrefix => "Softwareschmiede.Devin";

    /// <inheritdoc/>
    public override PluginType PluginType => PluginType.DevelopmentAutomation;

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() =>
    [
        new PluginSettingGroup("Ausfuehrung",
        [
            new PluginSettingField(
                Key: ExecutablePathSettingKey,
                Label: "Devin CLI Pfad",
                FieldType: PluginSettingFieldType.Text,
                Placeholder: "C:\\Program Files\\Devin\\devin.exe",
                Description: "Optionaler absoluter Pfad zur devin-Executable. Ohne Angabe wird devin ueber PATH gestartet.",
                IsRequired: false)
        ]),
        new PluginSettingGroup("CLI-Konfiguration",
        [
            new PluginSettingField(
                Key: "CommandLineParameters",
                Label: "Kommandozeilenparameter",
                FieldType: PluginSettingFieldType.CommandLineParameters,
                Description: "Zusaetzliche Parameter fuer den devin-CLI-Aufruf, z. B. --continue, --resume, --model oder --permission-mode.",
                IsRequired: false)
        ])
    ];

    /// <summary>Erstellt eine neue Instanz des <see cref="DevinPlugin"/>.</summary>
    public DevinPlugin(
        ICredentialStore credentialStore,
        ILogger<DevinPlugin> logger)
    {
        _credentialStore = credentialStore;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override Task<string?> GetCliHelpTextAsync(CancellationToken ct = default)
        => RunHelpCommandAsync(GetDevinCommand(), ct);

    /// <inheritdoc/>
    public override bool SupportsSessionContinuation() => true;

    /// <inheritdoc/>
    public override async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Pruefe Devin-CLI-Plugin-Health.");
        return await CheckHealthWithVersionCommandAsync(GetDevinCommand(), ct);
    }

    /// <inheritdoc/>
    protected override ProcessStartInfo BuildProcessStartInfo(string localRepoPath, string? parameters)
    {
        _logger.LogInformation(
            "DevinPlugin BuildProcessStartInfo (Repo: {RepoPath}, Parameters: {Parameters}).",
            localRepoPath,
            parameters);

        var psi = new ProcessStartInfo
        {
            FileName = GetDevinCommand(),
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

    private string GetDevinCommand()
        => ResolveExecutablePath(_credentialStore, PluginPrefix, _devinExecutablePath.Value);

    private static string FindDevinExecutable()
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                continue;
            }

            foreach (var ext in new[] { ".exe", ".cmd", ".bat", string.Empty })
            {
                var candidate = Path.Combine(dir.Trim(), $"devin{ext}");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return OperatingSystem.IsWindows() ? "devin.exe" : "devin";
    }
}
