using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Devin-CLI Plugin fuer KI-gestuetzte Entwicklung.</summary>
public sealed class DevinPlugin : CliKiPluginBase, IIssueTemplateTextGenerator
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
    public override string PluginSymbol => "🤖";

    /// <inheritdoc/>
    public override TerminalProviderCapabilities TerminalCapabilities =>
        TerminalProviderCapabilities.RequiresPty | TerminalProviderCapabilities.SupportsPty;

    /// <summary>Empirisch ermittelte Verzögerung für den Devin-Submit: Die Devin CLI (v3000.11.x,
    /// crossterm/chisel-TUI) wertet ein <c>\r</c>, das im selben Eingabe-Read wie der Prompttext
    /// ankommt, als Zeilenumbruch (Paste-Semantik) — nicht als Enter-Submit. Erst ein zeitlich
    /// getrennt gesendetes CR wird als Tastatur-Enter erkannt (gemessen an kurzen Prompts: 50 ms
    /// noch im selben Batch, ab ~75 ms separat; 200 ms als Sicherheitsmarge unter Last).
    /// Achtung: Das nötige Abstands-Fenster wächst mit der Eingabegröße — bei ~10 KB Prompt liegt
    /// die Schwelle bei ~300–400 ms, bei ~20–30 KB bei ~0,7–1,1 s (ConPTY-vermessen); ein fester
    /// Delay allein reicht für große SCM-Prompts daher nicht — siehe
    /// <see cref="PromptSubmitRepeatCount"/>.</summary>
    /// <inheritdoc/>
    public override TimeSpan PromptSubmitDelay => TimeSpan.FromMilliseconds(200);

    /// <summary>Wiederholte Submit-Versuche: Weil das für den Devin-Submit nötige Abstands-Fenster
    /// von der Prompt-Größe abhängt und streut, wird der Submit-<c>\r</c> mehrfach wiederholt
    /// (erster Versuch nach <see cref="PromptSubmitDelay"/>, dann alle
    /// <see cref="PromptSubmitRepeatInterval"/>). Empirisch (ConPTY, devin.exe v3000.11.92):
    /// kurzer Prompt → Versuch 1, ~10 KB → Versuch 2, ~20–30 KB → Versuch 3 bei 400-ms-Abstand;
    /// 7 Wiederholungen decken bis ~3 s nach dem letzten Text-Batch ab. Verschluckte Versuche
    /// erzeugen nur einen Zeilenumbruch im Entwurf; Versuche nach einem bereits erfolgten Submit
    /// wirken als Enter auf leerem/bearbeitendem Prompt und werden ignoriert (vermessen).</summary>
    /// <inheritdoc/>
    public override int PromptSubmitRepeatCount => 7;

    /// <inheritdoc/>
    public override TimeSpan PromptSubmitRepeatInterval => TimeSpan.FromMilliseconds(400);

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
    public Task<string> FillIssueTemplateAsync(string templateBody, string? originalRequirement, CancellationToken ct = default)
    {
        var invocation = BuildIssueTemplateFillInvocation(templateBody, originalRequirement);
        return RunOneShotTextGenerationAsync(invocation.ProcessStartInfo, null, ct);
    }

    internal (ProcessStartInfo ProcessStartInfo, string? StandardInput) BuildIssueTemplateFillInvocation(
        string templateBody,
        string? originalRequirement)
    {
        var psi = new ProcessStartInfo
        {
            FileName = GetDevinCommand(),
            WorkingDirectory = Path.GetTempPath(),
        };

        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(BuildIssueTemplateFillPrompt(templateBody, originalRequirement));
        psi.ArgumentList.Add("--respect-workspace-trust");
        psi.ArgumentList.Add("false");

        var commandLineParameters = _credentialStore.GetCredential($"{PluginPrefix}.CommandLineParameters");
        if (!string.IsNullOrWhiteSpace(commandLineParameters))
        {
            foreach (var part in commandLineParameters.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                psi.ArgumentList.Add(part);
            }
        }

        return (psi, null);
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
