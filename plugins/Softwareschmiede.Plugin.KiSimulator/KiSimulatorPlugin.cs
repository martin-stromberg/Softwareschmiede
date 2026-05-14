using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Deterministisches KI-Simulator-Plugin mit vier festen Antwortschritten.</summary>
public sealed class KiSimulatorPlugin : CliKiPluginBase
{
    private const string Delay12MsKey = "Delay12Ms";
    private const string Delay23MsKey = "Delay23Ms";
    private const string Delay34MsKey = "Delay34Ms";
    private const int DefaultDelayMs = 2000;
    private const int MinimumDelayMs = 0;
    private const int MaximumDelayMs = 10000;

    private const string Antwort1Text = "Ich kümmere mich darum, die Anforderung zu verstehen.";
    private const string Antwort2Text = "Ich habe die Anforderung verstanden. ich mache mir nun einen Plan.";
    private const string Antwort3Text = "Der Plan ist fertig. Ich begebe mich nun an die Umsetzung.";
    private const string Antwort4Text = """
        Fertig. Hier ist das Ergebnis:
        Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet. Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet. Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet.

        Duis autem vel eum iriure dolor in hendrerit in vulputate velit esse molestie consequat, vel illum dolore eu feugiat nulla facilisis at vero eros et accumsan et iusto odio dignissim qui blandit praesent luptatum zzril delenit augue duis dolore te feugait nulla facilisi. Lorem ipsum dolor sit amet, consectetuer adipiscing elit, sed diam nonummy nibh euismod tincidunt ut laoreet dolore magna aliquam erat volutpat.
        """;

    private readonly ICredentialStore _credentialStore;
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
    public KiSimulatorPlugin(
        ICredentialStore credentialStore,
        ILogger<KiSimulatorPlugin> logger)
    {
        _credentialStore = credentialStore;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<PluginSettingGroup> GetSettingGroups() =>
    [
        new PluginSettingGroup("Simulation",
        [
            new PluginSettingField(
                Key: Delay12MsKey,
                Label: "Delay 1→2 (ms)",
                FieldType: PluginSettingFieldType.Integer,
                Description: "Wartezeit zwischen Antwort 1 und 2 (0..10000). Standard/Fallback: 2000.",
                IsRequired: false),
            new PluginSettingField(
                Key: Delay23MsKey,
                Label: "Delay 2→3 (ms)",
                FieldType: PluginSettingFieldType.Integer,
                Description: "Wartezeit zwischen Antwort 2 und 3 (0..10000). Standard/Fallback: 2000.",
                IsRequired: false),
            new PluginSettingField(
                Key: Delay34MsKey,
                Label: "Delay 3→4 (ms)",
                FieldType: PluginSettingFieldType.Integer,
                Description: "Wartezeit zwischen Antwort 3 und 4 (0..10000). Standard/Fallback: 2000.",
                IsRequired: false)
        ])
    ];

    /// <inheritdoc/>
    public override Task<IEnumerable<AgentInfo>> GetAvailableAgentsAsync(string agentPackagePath, CancellationToken ct = default)
        => Task.FromResult<IEnumerable<AgentInfo>>([new AgentInfo("ki-simulator", "Deterministischer KI-Simulator-Agent.", "builtin://ki-simulator")]);

    /// <inheritdoc/>
    public override Task<bool> IsAgentPackageCompatibleAsync(string agentPackagePath, CancellationToken ct = default)
    {
        _logger.LogInformation("Agentenpaket-Kompatibilität für Simulator immer erfüllt (Pfad: {Path}).", agentPackagePath);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public override Task DeployAgentPackageAsync(string agentPackagePath, string localRepoPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Simulator benötigt kein Agentenpaket-Deployment (Paket: {PackagePath}, Repo: {RepoPath}).", agentPackagePath, localRepoPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<string> StartDevelopmentAsync(
        string prompt,
        AgentInfo agent,
        string localRepoPath,
        string? model = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Simulatorlauf gestartet (Agent: {AgentName}, Repo: {RepoPath}, PromptLaenge: {PromptLength}).",
            agent.Name,
            localRepoPath,
            prompt?.Length ?? 0);

        ct.ThrowIfCancellationRequested();
        yield return Antwort1Text;
        await DelayAsync(Delay12MsKey, ct);
        yield return Antwort2Text;
        await DelayAsync(Delay23MsKey, ct);
        yield return Antwort3Text;
        await DelayAsync(Delay34MsKey, ct);
        yield return Antwort4Text;
    }

    /// <inheritdoc/>
    public override Task<TestResult> RunTestsAsync(string localRepoPath, CancellationToken ct = default)
        => Task.FromResult(new TestResult(true,
        [
            new TestErgebnisInfo("KI-Simulator Selbsttest", TestStatus.Bestanden, null, TimeSpan.Zero)
        ]));

    /// <inheritdoc/>
    public override Task<bool> CheckHealthAsync(CancellationToken ct = default)
        => Task.FromResult(true);

    private async Task DelayAsync(string settingKey, CancellationToken ct)
    {
        var delayMs = ResolveDelayMilliseconds(settingKey);
        if (delayMs <= 0)
        {
            return;
        }

        await Task.Delay(delayMs, ct);
    }

    private int ResolveDelayMilliseconds(string settingKey)
    {
        var rawValue = _credentialStore.GetCredential($"{PluginPrefix}.{settingKey}");
        if (int.TryParse(rawValue, out var parsed)
            && parsed >= MinimumDelayMs
            && parsed <= MaximumDelayMs)
        {
            return parsed;
        }

        _logger.LogWarning(
            "Ungültiger Delay-Wert für {SettingKey}: '{RawValue}'. Fallback auf {FallbackDelayMs}ms.",
            settingKey,
            rawValue,
            DefaultDelayMs);
        return DefaultDelayMs;
    }
}
