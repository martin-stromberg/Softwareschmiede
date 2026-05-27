namespace Softwareschmiede.Components.Pages.Aufgaben;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

public partial class AufgabeDetail : IDisposable
{
    [Parameter] public Guid Id { get; set; }
    [Inject] private IServiceScopeFactory ServiceScopeFactory { get; set; } = null!;
    [Inject] private IPluginManager PluginManager { get; set; } = null!;
    [Inject] private PluginSelectionService PluginSelection { get; set; } = null!;
    [Inject] private AufgabeService AufgabeService { get; set; } = null!;
    [Inject] private AufgabeRecoveryService RecoveryService { get; set; } = null!;
    [Inject] private EntwicklungsprozessService EntwicklungsprozessService { get; set; } = null!;
    [Inject] private KiAusfuehrungsService KiAusfuehrungsService { get; set; } = null!;
    [Inject] private IRunningAutomationStatusSource RunningAutomationStatusSource { get; set; } = null!;
    [Inject] private GitOrchestrationService GitService { get; set; } = null!;
    [Inject] private IGitWorkspaceBrowserService GitWorkspaceBrowserService { get; set; } = null!;
    [Inject] private ProtokollService ProtokollService { get; set; } = null!;
    [Inject] private ProjektService ProjektService { get; set; } = null!;
    [Inject] private IAgentPackageService AgentPackageService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] private ILogger<AufgabeDetail> _logger { get; set; } = null!;
    [SupplyParameterFromQuery(Name = "register")]
    public string? Register { get; set; }
    [SupplyParameterFromQuery(Name = "view")]
    public string? View { get; set; }

    private bool _loading = true;
    private bool _processing;
    private Aufgabe? _aufgabe;
    private List<Protokolleintrag> _protokoll = [];
    private IReadOnlyList<AgentPackageInfo> _agentenpakete = [];
    private List<AgentInfo> _agenten = [];
    private IReadOnlyList<IKiPlugin> _kiPlugins = [];
    private List<string> _streamingLines = [];
    private DateTimeOffset _kiStreamingStartedUtc = DateTimeOffset.UtcNow;
    private WorkspaceSnapshot? _workspaceSnapshot;
    private WorkspaceFileNode? _selectedWorkspaceNode;
    private FilePreview? _selectedWorkspacePreview;
    private string? _selectedWorkspacePath;
    private AufgabeDetailRegister _activeRegister = AufgabeDetailRegister.Aufgabe;
    private bool _useTreeLayout = true;
    private bool _loadingWorkspace;
    private Guid? _latestDiffResultId;
    private Guid? _selectedWorkspaceDiffResultId;
    private long _previewLoadVersion;
    private bool HasSelectedWorkspaceFile => _selectedWorkspaceNode is not null && !_selectedWorkspaceNode.IsDirectory;
    private string ProjektAnzeigeText =>
        string.IsNullOrWhiteSpace(_aufgabe?.Projekt?.Name)
            ? "Projekt: ohne projekt"
            : $"Projekt: {_aufgabe.Projekt.Name}";
    private bool IsAufgabeRegisterAktiv => _activeRegister == AufgabeDetailRegister.Aufgabe;
    private bool IsAusfuehrungRegisterAktiv => _activeRegister == AufgabeDetailRegister.Ausfuehrung;
    private bool IsProjektverzeichnisRegisterAktiv => _activeRegister == AufgabeDetailRegister.Projektverzeichnis;
    private bool KannRepositoryAktionenAusfuehren => _aufgabe?.Status is AufgabeStatus.InBearbeitung or AufgabeStatus.KiAktiv;
    private bool KannAufgabeAbschliessen => _aufgabe?.Status == AufgabeStatus.InBearbeitung;
    private bool KannAufgabeAbbrechen => _aufgabe?.Status == AufgabeStatus.InBearbeitung;
    private string AnlagezeitpunktText => _aufgabe?.ErstellungsDatum.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") ?? "–";
    private string LetzteAusfuehrungText => _protokoll.LastOrDefault()?.Zeitstempel.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") ?? "–";
    private string AusfuehrungsdauerText => BuildAusfuehrungsdauerText();
    private string LetzterAusfuehrungsstatusText => _aufgabe?.Status switch
    {
        AufgabeStatus.KiAktiv => "Läuft",
        AufgabeStatus.Abgeschlossen => "Erfolgreich",
        AufgabeStatus.Fehlgeschlagen => "Fehlgeschlagen",
        AufgabeStatus.Archiviert => "Archiviert",
        AufgabeStatus.TestsLaufen => "Tests laufen",
        AufgabeStatus.InBearbeitung => "Bereit",
        AufgabeStatus.Offen => "Noch nicht gestartet",
        _ => "–"
    };

    // Subscription auf laufende KI-Session (wird beim Dispose freigegeben)
    private IDisposable? _kiSubscription;

    // Forms
    private bool _showCommitForm;
    private bool _showPushForm;
    private bool _showPullForm;
    private bool _showResetForm;
    private bool _showPullRequestForm;
    private bool _showAbbrechenConfirm;
    private bool _showRecoveryConfirm;
    private bool _showStatusResetConfirm;
    private bool _showArchivierenConfirm;
    private bool _showDeleteConfirm;
    private bool _showVerwerfenConfirm;
    private bool _showStartDialog;
    private bool _editAnforderung;
    private bool _loadingBranches;
    private string _anforderungInput = string.Empty;
    private string _selectedPaketName = string.Empty;
    private string _selectedBranchName = string.Empty;
    private List<string> _remoteBranches = [];
    private string _selectedAgentName = string.Empty;
    private string _kiAgentName = string.Empty;
    private string _selectedKiPluginPrefix = string.Empty;
    private FolgeanweisungsKontextmodus _folgeKontextmodus = FolgeanweisungsKontextmodus.KontextMitgeben;
    private bool _folgeKontextNeuBeginnenBestaetigt;
    private string _prompt = string.Empty;
    private string _commitMessage = string.Empty;
    private string _resetType = "mixed";
    private string? _resetRef;
    private string _prTitel = string.Empty;
    private string? _prBody;
    private string? _fehler;
    private string? _erfolg;
    private bool _recoveryAllowed;
    private string? _recoveryDisabledReason;
    private bool _statusResetAllowed;
    private string? _statusResetDisabledReason;
    private GitActionCapabilities _gitActionCapabilities = new(
        RepositoryKind.Unknown,
        IsWorkingDirectoryCopy: false,
        CanPush: true,
        CanPull: true,
        CanCreatePullRequest: true,
        CanMergeToSource: false);
    private (bool ShowPushPullToggle, bool ShowPush, bool ShowPull, bool ShowPullRequest, bool ShowMerge) _gitActionVisibility;
    private const string StreamingContainerSelector = "#streamingOutput";
    private const string HistoryContainerSelector = "#historyProtokoll";
    private const double ScrollEndThresholdPx = 16;
    private bool _streamingInitialScrollPending = true;
    private bool _historyInitialScrollPending = true;
    private bool _streamingShouldScrollAfterAppend = true;
    private bool _historyShouldScrollAfterAppend = true;
    private int _streamingUpdateVersion;
    private int _historyUpdateVersion;
    private int _streamingPendingScrollVersion;
    private int _historyPendingScrollVersion;
    private bool _streamingWasVisibleBeforeUpdate;
    private bool _historyWasVisibleBeforeUpdate;

    // TODO: Liste kann bei Bedarf aus der Plugin-Konfiguration dynamisch befüllt werden
    private static readonly IReadOnlyList<(string Value, string Label)> _verfuegbareModelle =
    [
        ("", "– Automatisch (GitHub wählt) –"),
        ("gpt-4o", "GPT-4o"),
        ("gpt-4.1", "GPT-4.1"),
        ("claude-3-7-sonnet", "Claude 3.7 Sonnet"),
        ("claude-sonnet-4-5", "Claude Sonnet 4.5"),
        ("gemini-2.0-flash", "Gemini 2.0 Flash"),
        ("o3", "o3"),
        ("o4-mini", "o4-mini"),
    ];

    private string _selectedModel = string.Empty;

    private static readonly MarkdownPipeline _protokollMarkdownPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();

    private static readonly Regex _unsafeHtmlEventAttributeRegex =
        new(@"\s(on\w+)\s*=\s*(['""]).*?\2", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex _unsafeHtmlUriRegex =
        new(@"\s(href|src)\s*=\s*(['""])\s*(javascript:|data:|vbscript:)[^'""]*\2", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private CancellationTokenSource _cts = new();

    private enum AufgabeDetailRegister
    {
        Aufgabe,
        Ausfuehrung,
        Projektverzeichnis
    }

    protected override async Task OnInitializedAsync()
    {
        ApplyRegisterFromQuery();
        await LadeAsync();

        // Wenn beim Seitenaufruf ein KI-Lauf im Hintergrund läuft: pufferlosen Stand wiederherstellen
        // und auf neue Zeilen subscriben, damit die Ausgabe live weiterläuft.
        if (KiAusfuehrungsService.IsRunning(Id))
        {
            _processing = true;
            _kiStreamingStartedUtc = DateTimeOffset.UtcNow;
            _streamingLines = [.. KiAusfuehrungsService.GetBufferedLines(Id)];
            KiLiveSubscribieren();
        }

    }

    protected override Task OnParametersSetAsync()
    {
        ApplyRegisterFromQuery();
        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await ApplyPendingScrollAsync();
    }

    private bool IsStreamingContainerVisible
        => _aufgabe?.Status == AufgabeStatus.KiAktiv && _streamingLines.Count > 0;

    private bool IsHistoryContainerVisible => _protokoll.Count > 0;

    private async Task CaptureStreamingScrollStateBeforeUpdateAsync()
    {
        _streamingWasVisibleBeforeUpdate = IsStreamingContainerVisible;
        _streamingShouldScrollAfterAppend = await TryReadAtEndStateAsync(StreamingContainerSelector);
        _streamingPendingScrollVersion = _streamingUpdateVersion + 1;
    }

    private async Task CaptureHistoryScrollStateBeforeUpdateAsync()
    {
        _historyWasVisibleBeforeUpdate = IsHistoryContainerVisible;
        _historyShouldScrollAfterAppend = await TryReadAtEndStateAsync(HistoryContainerSelector);
        _historyPendingScrollVersion = _historyUpdateVersion + 1;
    }

    private void RegisterStreamingContentUpdate()
    {
        _streamingUpdateVersion++;
        if (!_streamingWasVisibleBeforeUpdate && _streamingLines.Count > 0)
        {
            _streamingInitialScrollPending = true;
        }
    }

    private void RegisterHistoryContentUpdate()
    {
        _historyUpdateVersion++;
        if (!_historyWasVisibleBeforeUpdate && _protokoll.Count > 0)
        {
            _historyInitialScrollPending = true;
        }
    }

    private void ResetStreamingScrollState()
    {
        _streamingUpdateVersion = 0;
        _streamingPendingScrollVersion = 0;
        _streamingShouldScrollAfterAppend = true;
        _streamingInitialScrollPending = true;
    }

    private async Task ApplyPendingScrollAsync()
    {
        if (!IsStreamingContainerVisible)
        {
            _streamingInitialScrollPending = true;
            _streamingPendingScrollVersion = 0;
        }
        else if (_streamingInitialScrollPending)
        {
            var scrolled = await TryScrollToEndAsync(StreamingContainerSelector);
            if (scrolled)
            {
                _streamingInitialScrollPending = false;
            }
        }
        else if (_streamingPendingScrollVersion > 0 && _streamingPendingScrollVersion == _streamingUpdateVersion)
        {
            if (_streamingShouldScrollAfterAppend)
            {
                await TryScrollToEndAsync(StreamingContainerSelector);
            }

            _streamingPendingScrollVersion = 0;
        }

        if (!IsHistoryContainerVisible)
        {
            _historyInitialScrollPending = true;
            _historyPendingScrollVersion = 0;
            return;
        }

        if (_historyInitialScrollPending)
        {
            var scrolled = await TryScrollToEndAsync(HistoryContainerSelector);
            if (scrolled)
            {
                _historyInitialScrollPending = false;
            }

            return;
        }

        if (_historyPendingScrollVersion <= 0 || _historyPendingScrollVersion != _historyUpdateVersion)
        {
            return;
        }

        if (_historyShouldScrollAfterAppend)
        {
            await TryScrollToEndAsync(HistoryContainerSelector);
        }

        _historyPendingScrollVersion = 0;
    }

    private async Task<bool> TryReadAtEndStateAsync(string selector)
    {
        if (JsRuntime is null)
        {
            return true;
        }

        try
        {
            var metrics = await JsRuntime.InvokeAsync<double[]>(
                "softwareschmiedeLogScroll.getMetrics",
                selector);

            if (metrics.Length < 4 || metrics[3] < 1)
            {
                return true;
            }

            return IsAtEnd(metrics[0], metrics[1], metrics[2], ScrollEndThresholdPx);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Scroll-Endzustand konnte nicht ermittelt werden ({Selector}).", selector);
            return true;
        }
    }

    private async Task<bool> TryScrollToEndAsync(string selector)
    {
        if (JsRuntime is null)
        {
            return false;
        }

        try
        {
            return await JsRuntime.InvokeAsync<bool>(
                "softwareschmiedeLogScroll.scrollToEnd",
                selector);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ScrollToEnd konnte nicht ausgeführt werden ({Selector}).", selector);
            return false;
        }
    }

    private static bool IsAtEnd(double scrollTop, double scrollHeight, double clientHeight, double thresholdPx)
        => scrollHeight - (scrollTop + clientHeight) <= thresholdPx;

    private async Task LadeAsync()
    {
        _loading = true;
        _selectedWorkspaceDiffResultId = null;
        _aufgabe = await AufgabeService.GetDetailAsync(Id);
        _latestDiffResultId = await AufgabeService.GetLatestDiffResultIdAsync(Id);
        if (_aufgabe is not null)
        {
            await CaptureHistoryScrollStateBeforeUpdateAsync();
            _protokoll = (await ProtokollService.GetByAufgabeAsync(Id)).ToList();
            RegisterHistoryContentUpdate();
            await LadeGitActionCapabilitiesAsync();
            await LadeWorkspaceAsync();
            _prTitel = _aufgabe.Titel;
            // Agenten laden wenn Paket gesetzt
            if (!string.IsNullOrEmpty(_aufgabe.AgentenpaketName))
            {
                _selectedPaketName = _aufgabe.AgentenpaketName;
            }

            // Ausgewählten Agenten wiederherstellen
            if (!string.IsNullOrEmpty(_aufgabe.AgentenName))
            {
                _kiAgentName = _aufgabe.AgentenName;
                _selectedAgentName = _aufgabe.AgentenName;
            }

            _selectedKiPluginPrefix = _aufgabe.KiPluginPrefix ?? string.Empty;
            await LadeKiPluginsAsync();

            // Anforderungsbeschreibung als initialen Prompt vorbelegen, solange noch kein Prompt gesendet wurde
            if (!string.IsNullOrWhiteSpace(_aufgabe.AnforderungsBeschreibung)
                && string.IsNullOrWhiteSpace(_prompt)
                && !_protokoll.Any(p => p.Typ == ProtokollTyp.Prompt))
            {
                _prompt = _aufgabe.AnforderungsBeschreibung;
            }
        }
        AktualisiereRecoveryZustand();
        AktualisiereStatusResetZustand();
        _loading = false;
    }

    /// <summary>Lädt die Aufgabendaten mit neuem Scope (für Background-Tasks).</summary>
    private async Task LadeAsyncWithScope()
    {
        try
        {
            using var scope = ServiceScopeFactory.CreateScope();
            var aufgabeService = scope.ServiceProvider.GetRequiredService<AufgabeService>();
            var protokollService = scope.ServiceProvider.GetRequiredService<ProtokollService>();

            _loading = true;
            _selectedWorkspaceDiffResultId = null;
            _aufgabe = await aufgabeService.GetDetailAsync(Id);
            _latestDiffResultId = await aufgabeService.GetLatestDiffResultIdAsync(Id);
            if (_aufgabe is not null)
            {
                await CaptureHistoryScrollStateBeforeUpdateAsync();
                _protokoll = (await protokollService.GetByAufgabeAsync(Id)).ToList();
                RegisterHistoryContentUpdate();
                await LadeGitActionCapabilitiesAsync();
                await LadeWorkspaceAsync();
                _prTitel = _aufgabe.Titel;
                // Agenten laden wenn Paket gesetzt
                if (!string.IsNullOrEmpty(_aufgabe.AgentenpaketName))
                {
                    _selectedPaketName = _aufgabe.AgentenpaketName;
                }

                // Ausgewählten Agenten wiederherstellen
                if (!string.IsNullOrEmpty(_aufgabe.AgentenName))
                {
                    _kiAgentName = _aufgabe.AgentenName;
                    _selectedAgentName = _aufgabe.AgentenName;
                }

                _selectedKiPluginPrefix = _aufgabe.KiPluginPrefix ?? string.Empty;
                await LadeKiPluginsAsync();

                // Anforderungsbeschreibung als initialen Prompt vorbelegen, solange noch kein Prompt gesendet wurde
                if (!string.IsNullOrWhiteSpace(_aufgabe.AnforderungsBeschreibung)
                    && string.IsNullOrWhiteSpace(_prompt)
                    && !_protokoll.Any(p => p.Typ == ProtokollTyp.Prompt))
                {
                    _prompt = _aufgabe.AnforderungsBeschreibung;
                }
            }
            AktualisiereRecoveryZustand();
            AktualisiereStatusResetZustand();
            _loading = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Aufgabendaten");
            _latestDiffResultId = null;
            _selectedWorkspaceDiffResultId = null;
            _recoveryAllowed = false;
            _recoveryDisabledReason = null;
            _loading = false;
        }
    }

    private void AktualisiereRecoveryZustand()
    {
        _recoveryAllowed = false;
        _recoveryDisabledReason = null;

        if (_aufgabe is null || !AufgabeRecoveryService.IstRecoveryStatus(_aufgabe.Status))
        {
            return;
        }

        try
        {
            var isRunning = RunningAutomationStatusSource.IsRunning(_aufgabe.Id);
            _recoveryAllowed = !isRunning;
            if (isRunning)
            {
                _recoveryDisabledReason = "Wiederherstellung nicht möglich, Verarbeitung läuft noch.";
            }
        }
        catch
        {
            _recoveryAllowed = false;
            _recoveryDisabledReason = "Prüfung der Laufzeit war nicht möglich.";
        }
    }

    private void AktualisiereStatusResetZustand()
    {
        _statusResetAllowed = false;
        _statusResetDisabledReason = null;

        if (_aufgabe is null || _aufgabe.Status != AufgabeStatus.KiAktiv)
        {
            return;
        }

        try
        {
            var isRunning = RunningAutomationStatusSource.IsRunning(_aufgabe.Id);
            _statusResetAllowed = !isRunning;
            if (isRunning)
            {
                _statusResetDisabledReason = "Status kann nicht zurückgesetzt werden, solange die Verarbeitung läuft.";
            }
        }
        catch
        {
            _statusResetAllowed = false;
            _statusResetDisabledReason = "Prüfung der Laufzeit war nicht möglich.";
        }
    }

    private bool IsRecoveryStatus
        => _aufgabe is not null && AufgabeRecoveryService.IstRecoveryStatus(_aufgabe.Status);

    private bool IsAgentenauswahlGueltig
        => _kiPlugins.Count > 0;

    private string? AgentenauswahlHinweis
    {
        get
        {
            if (_kiPlugins.Count == 0)
            {
                return "Kein KI-Plugin verfügbar.";
            }

            if (string.IsNullOrWhiteSpace(_selectedPaketName))
            {
                return "Kein Agentenpaket gewählt – die KI verwendet ihre Standardeinstellungen.";
            }

            if (_agenten.Count == 0)
            {
                return "Für das gewählte Agentenpaket sind keine kompatiblen Agenten verfügbar. Die KI verwendet ihre Standardeinstellungen.";
            }

            if (string.IsNullOrWhiteSpace(_selectedAgentName) && string.IsNullOrWhiteSpace(_kiAgentName))
            {
                return "Kein Agent gewählt – die KI verwendet ihre Standardeinstellungen.";
            }

            return null;
        }
    }

    private Task AgentenLadenAsync()
    {
        if (_agentenpakete.Count == 0 || string.IsNullOrWhiteSpace(_selectedPaketName))
        {
            _agenten = [];
            _selectedAgentName = string.Empty;
            _kiAgentName = string.Empty;
            return Task.CompletedTask;
        }

        var paket = _agentenpakete.FirstOrDefault(p => p.Name == _selectedPaketName);
        if (paket is null)
        {
            _selectedPaketName = _agentenpakete[0].Name;
            paket = _agentenpakete[0];
        }

        _agenten = paket.Agenten.ToList();
        if (_agenten.Count == 0)
        {
            _selectedAgentName = string.Empty;
            _kiAgentName = string.Empty;
            return Task.CompletedTask;
        }

        if (!_agenten.Any(agent => agent.Name == _selectedAgentName))
        {
            _selectedAgentName = _agenten[0].Name;
        }

        if (!_agenten.Any(agent => agent.Name == _kiAgentName))
        {
            _kiAgentName = _selectedAgentName;
        }

        return Task.CompletedTask;
    }

    private async Task LadeKiPluginsAsync()
    {
        _kiPlugins = PluginManager.GetDevelopmentAutomationPlugins();
        if (_kiPlugins.Count == 0)
        {
            _agentenpakete = [];
            _agenten = [];
            _selectedKiPluginPrefix = string.Empty;
            _selectedPaketName = string.Empty;
            _selectedAgentName = string.Empty;
            return;
        }

        var resolved = await PluginSelection.ResolveDevelopmentAutomationPluginAsync(
            string.IsNullOrWhiteSpace(_selectedKiPluginPrefix) ? _aufgabe?.KiPluginPrefix : _selectedKiPluginPrefix,
            _cts.Token);
        _selectedKiPluginPrefix = resolved.PluginPrefix;

        await LadeAgentenpaketeAsync(resolved);
    }

    private async Task LadeAgentenpaketeAsync(IKiPlugin? kiPlugin = null)
    {
        if (_kiPlugins.Count == 0)
        {
            _agentenpakete = [];
            _agenten = [];
            _selectedPaketName = string.Empty;
            _selectedAgentName = string.Empty;
            return;
        }

        var resolvedPlugin = kiPlugin ?? await PluginSelection.ResolveDevelopmentAutomationPluginAsync(_selectedKiPluginPrefix, _cts.Token);
        var packages = await AgentPackageService.GetPackagesAsync(_cts.Token);
        var compatiblePackages = new List<AgentPackageInfo>();

        foreach (var package in packages.OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase))
        {
            var agents = (await resolvedPlugin.GetAvailableAgentsAsync(package.Pfad, _cts.Token)).ToList();
            if (agents.Count == 0)
            {
                continue;
            }

            compatiblePackages.Add(package with { Agenten = agents });
        }

        _agentenpakete = compatiblePackages;
        if (_agentenpakete.Count == 0)
        {
            _agenten = [];
            _selectedPaketName = string.Empty;
            _selectedAgentName = string.Empty;
            return;
        }

        if (_agentenpakete.All(paket => paket.Name != _selectedPaketName))
        {
            _selectedPaketName = _agentenpakete[0].Name;
        }

        await AgentenLadenAsync();
    }

    private async Task KiPluginGeaendertAsync()
    {
        _selectedPaketName = string.Empty;
        _selectedAgentName = string.Empty;
        _kiAgentName = string.Empty;
        await LadeAgentenpaketeAsync();
        StateHasChanged();
    }

    private async Task PaketGeaendertAsync()
    {
        _selectedAgentName = string.Empty;
        _kiAgentName = string.Empty;
        await AgentenLadenAsync();
        StateHasChanged();
    }

    private void AnforderungBearbeitenStarten()
    {
        _anforderungInput = _aufgabe?.AnforderungsBeschreibung ?? string.Empty;
        _editAnforderung = true;
    }

    private void AnforderungBearbeitenAbbrechen()
    {
        _editAnforderung = false;
        _anforderungInput = string.Empty;
    }

    private async Task AnforderungSpeichernAsync()
    {
        if (_aufgabe is null) { return; }

        _processing = true;
        _fehler = null;
        try
        {
            await AufgabeService.UpdateAsync(
                Id,
                _aufgabe.Titel,
                _anforderungInput,
                _aufgabe.AgentenpaketName,
                _aufgabe.AgentenName,
                _aufgabe.KiPluginPrefix);
            _editAnforderung = false;
            _erfolg = "Anforderungsbeschreibung gespeichert.";
            await LadeAsync();
            await ClearErfolgAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async void StartDialogOeffnen()
    {
        _fehler = null;
        _selectedBranchName = string.Empty;
        _remoteBranches = [];
        _showStartDialog = true;

        // Remote-Branches im Hintergrund laden
        await LadeRemoteBranchesAsync();
    }

    private async Task LadeRemoteBranchesAsync()
    {
        _loadingBranches = true;
        StateHasChanged();

        try
        {
            var projekt = await ProjektService.GetDetailAsync(_aufgabe!.ProjektId);
            var repo = _aufgabe.GitRepositoryId is not null
                ? projekt?.Repositories.FirstOrDefault(r => r.Id == _aufgabe.GitRepositoryId)
                : projekt?.Repositories.FirstOrDefault(r => r.Aktiv);

            if (repo is not null)
            {
                _remoteBranches = (await EntwicklungsprozessService.GetRemoteBranchesAsync(repo.RepositoryUrl, repo.PluginTyp, _cts.Token)).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Remote-Branches konnten nicht geladen werden.");
            // Kein Fehler zeigen – Branch-Auswahl einfach weglassen
        }
        finally
        {
            _loadingBranches = false;
            StateHasChanged();
        }
    }

    private void StartDialogSchliessen()
    {
        if (_processing) { return; }
        _showStartDialog = false;
    }

    private async Task ProzessStartenAsync()
    {
        if (!IsAgentenauswahlGueltig)
        {
            _fehler = AgentenauswahlHinweis ?? "Kein KI-Plugin verfügbar.";
            return;
        }

        _processing = true;
        _fehler = null;
        try
        {
            // Agentenpaket, Agent und KI-Plugin speichern
            await AufgabeService.UpdateAsync(
                Id,
                _aufgabe!.Titel,
                _aufgabe.AnforderungsBeschreibung,
                string.IsNullOrWhiteSpace(_selectedPaketName) ? null : _selectedPaketName,
                string.IsNullOrWhiteSpace(_selectedAgentName) ? null : _selectedAgentName,
                string.IsNullOrWhiteSpace(_selectedKiPluginPrefix) ? null : _selectedKiPluginPrefix);

            // Repository-URL ermitteln: erst verknüpftes Repo, dann erstes aktives des Projekts
            var projekt = await ProjektService.GetDetailAsync(_aufgabe!.ProjektId);
            var repo = _aufgabe.GitRepositoryId is not null
                ? projekt?.Repositories.FirstOrDefault(r => r.Id == _aufgabe.GitRepositoryId)
                : projekt?.Repositories.FirstOrDefault(r => r.Aktiv);

            if (repo is null)
            {
                _fehler = "Kein aktives Repository im Projekt vorhanden. Bitte zuerst ein Repository anlegen.";
                return;
            }

            await EntwicklungsprozessService.ProzessStartenAsync(
                Id,
                repo.RepositoryUrl,
                string.IsNullOrEmpty(_selectedBranchName) ? null : _selectedBranchName,
                repo.PluginTyp,
                string.IsNullOrWhiteSpace(_selectedKiPluginPrefix) ? null : _selectedKiPluginPrefix,
                _cts.Token);
            _showStartDialog = false;
            _erfolg = "Entwicklungsumgebung erfolgreich gestartet.";
            await LadeAsync();
            await ClearErfolgAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task KiStartenAsync()
    {
        if (!IsAgentenauswahlGueltig)
        {
            _fehler = AgentenauswahlHinweis ?? "Kein KI-Plugin verfügbar.";
            return;
        }

        var hasKiAntwort = _protokoll.Any(p => p.Typ == ProtokollTyp.KiAntwort);
        if (hasKiAntwort
            && _folgeKontextmodus == FolgeanweisungsKontextmodus.KontextNeuBeginnen
            && !_folgeKontextNeuBeginnenBestaetigt)
        {
            _fehler = "Bitte bestätigen Sie zuerst, dass der bisherige Kontext zurückgesetzt werden soll.";
            return;
        }

        FolgeanweisungsKontextmodus? kontextmodus = _folgeKontextmodus;
        await KiMitPromptStartenAsync(_prompt, _kiAgentName, kontextmodus);
        _prompt = string.Empty;
        _folgeKontextNeuBeginnenBestaetigt = false;
    }

    private async Task KiMitPromptStartenAsync(string prompt, string selectedAgentName, FolgeanweisungsKontextmodus? kontextmodus)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return;
        if (_kiPlugins.Count == 0)
        {
            _fehler = "Kein KI-Plugin verfügbar. Bitte Plugin-Konfiguration prüfen.";
            return;
        }

        // Keinen Agenten-Namen übergeben wenn kein passender Agent gefunden wurde –
        // AgentInfo mit leerem Name führt dazu, dass --agent weggelassen wird.
        var agent = _agenten.FirstOrDefault(a => a.Name == selectedAgentName)
            ?? new AgentInfo(string.Empty, null, string.Empty);

        _processing = true;
        _fehler = null;
        _kiStreamingStartedUtc = DateTimeOffset.UtcNow;
        _streamingLines = [];
        ResetStreamingScrollState();

        // Bisherige Subscription aufräumen
        _kiSubscription?.Dispose();
        _kiSubscription = null;

        // Bereinigt eine ggf. bereits abgeschlossene Session, damit eine neue angelegt wird
        KiAusfuehrungsService.SessionBereinigen(Id);

        // Hintergrundlauf starten – kehrt sofort zurück
        StartKiLauf(
            prompt,
            agent,
            string.IsNullOrEmpty(_selectedModel) ? null : _selectedModel,
            kontextmodus,
            string.IsNullOrWhiteSpace(_selectedKiPluginPrefix) ? null : _selectedKiPluginPrefix);

        // Auf Live-Ausgabe subscriben – neue Zeilen werden direkt in _streamingLines eingefügt
        KiLiveSubscribieren();

        NotifyStateChanged();
        await Task.CompletedTask;
    }

    /// <summary>Subscribed auf neue Ausgabezeilen des laufenden KI-Hintergrundlaufs.</summary>
    private void KiLiveSubscribieren()
    {
        _kiSubscription?.Dispose();
        _kiSubscription = KiAusfuehrungsService.Subscribe(Id, line =>
        {
            // UI in den Blazor-Circuit-Thread marshalieren
            InvokeAsync(async () =>
            {
                await CaptureStreamingScrollStateBeforeUpdateAsync();
                _streamingLines.Add(line);
                RegisterStreamingContentUpdate();
                StateHasChanged();
            });
        });
    }

    private void FolgeKontextmodusGeaendert(ChangeEventArgs e)
    {
        if (e.Value is string value
            && Enum.TryParse<FolgeanweisungsKontextmodus>(value, ignoreCase: false, out var parsed))
        {
            _folgeKontextmodus = parsed;
        }

        if (_folgeKontextmodus != FolgeanweisungsKontextmodus.KontextNeuBeginnen)
        {
            _folgeKontextNeuBeginnenBestaetigt = false;
        }
    }

    protected virtual void StartKiLauf(
        string prompt,
        AgentInfo agent,
        string? model,
        FolgeanweisungsKontextmodus? kontextmodus,
        string? selectedKiPluginPrefix)
    {
        KiAusfuehrungsService.StartKiLauf(
            Id,
            prompt,
            agent,
            selectedKiPluginPrefix,
            model: model,
            kontextmodus: kontextmodus,
            onStarted: () => InvokeAsync(async () =>
            {
                await LadeAsync();
                StateHasChanged();
            }),
            onCompleted: fehler => InvokeAsync(async () =>
            {
                _processing = false;
                _kiSubscription?.Dispose();
                _kiSubscription = null;
                if (fehler)
                    _fehler = "KI-Ausführung fehlgeschlagen. Siehe Protokoll für Details.";
                await LadeAsyncWithScope(); // Protokoll neu laden nach Abschluss
                StateHasChanged();
            }));
    }

    protected virtual void NotifyStateChanged() => StateHasChanged();

    private async Task LadeGitActionCapabilitiesAsync()
    {
        if (_aufgabe is null)
        {
            return;
        }

        try
        {
            var capabilities = await GitService.GetGitActionCapabilitiesAsync(Id, _cts.Token);
            _gitActionCapabilities = capabilities ?? new GitActionCapabilities(
                RepositoryKind.Unknown,
                IsWorkingDirectoryCopy: false,
                CanPush: true,
                CanPull: true,
                CanCreatePullRequest: true,
                CanMergeToSource: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitActionCapabilities konnten nicht geladen werden. Fallback auf Standard-Sichtbarkeit.");
            _gitActionCapabilities = new GitActionCapabilities(
                RepositoryKind.Unknown,
                IsWorkingDirectoryCopy: false,
                CanPush: true,
                CanPull: true,
                CanCreatePullRequest: true,
                CanMergeToSource: false);
        }

        _gitActionVisibility = EvaluateGitActionVisibility(_gitActionCapabilities);

        if (!_gitActionVisibility.ShowPush)
        {
            _showPushForm = false;
        }

        if (!_gitActionVisibility.ShowPull)
        {
            _showPullForm = false;
        }

        if (!_gitActionVisibility.ShowPullRequest)
        {
            _showPullRequestForm = false;
        }
    }

    private void ApplyRegisterFromQuery()
    {
        if (string.Equals(Register, "ausfuehrung", StringComparison.OrdinalIgnoreCase))
        {
            SetActiveRegister(AufgabeDetailRegister.Ausfuehrung);
            return;
        }

        if (string.Equals(Register, "projektverzeichnis", StringComparison.OrdinalIgnoreCase)
            || string.Equals(View, "tree", StringComparison.OrdinalIgnoreCase))
        {
            SetActiveRegister(AufgabeDetailRegister.Projektverzeichnis);
            return;
        }

        SetActiveRegister(AufgabeDetailRegister.Aufgabe);
    }

    private void SetActiveRegister(AufgabeDetailRegister register)
    {
        if (_activeRegister == register)
        {
            return;
        }

        _activeRegister = register;
        if (_activeRegister != AufgabeDetailRegister.Projektverzeichnis)
        {
            CloseProjectDirectoryDialogs();
        }
    }

    private void ActivateAufgabeRegister() => SetActiveRegister(AufgabeDetailRegister.Aufgabe);

    private void ActivateAusfuehrungRegister() => SetActiveRegister(AufgabeDetailRegister.Ausfuehrung);

    private void ActivateProjektverzeichnisRegister() => SetActiveRegister(AufgabeDetailRegister.Projektverzeichnis);

    private void CloseProjectDirectoryDialogs()
    {
        _showCommitForm = false;
        _showPushForm = false;
        _showPullForm = false;
        _showPullRequestForm = false;
        _showResetForm = false;
    }

    private async Task LadeWorkspaceAsync()
    {
        if (_aufgabe?.LokalerKlonPfad is null or "")
        {
            _workspaceSnapshot = WorkspaceSnapshot.FromError("Kein lokaler Klonpfad vorhanden.");
            _selectedWorkspaceNode = null;
            _selectedWorkspacePreview = null;
            _selectedWorkspaceDiffResultId = null;
            return;
        }

        _loadingWorkspace = true;
        try
        {
            _workspaceSnapshot = await GitWorkspaceBrowserService.LoadSnapshotAsync(_aufgabe.LokalerKlonPfad, _cts.Token);
            if (_selectedWorkspacePath is not null)
            {
                ExpandPath(_workspaceSnapshot.RootNodes, _selectedWorkspacePath);
            }

            _selectedWorkspaceNode = _selectedWorkspacePath is null
                ? null
                : FindNode(_workspaceSnapshot.RootNodes, _selectedWorkspacePath);

            if (_selectedWorkspaceNode?.IsDirectory == true)
            {
                _selectedWorkspaceNode.IsExpanded = true;
            }

            if (_selectedWorkspaceNode is not null && !_selectedWorkspaceNode.IsDirectory)
            {
                await LadeWorkspacePreviewAsync(_selectedWorkspaceNode);
            }
            else
            {
                _selectedWorkspacePreview = null;
                _selectedWorkspaceDiffResultId = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workspace snapshot konnte nicht geladen werden.");
            _workspaceSnapshot = WorkspaceSnapshot.FromError(ex.Message);
            _selectedWorkspaceNode = null;
            _selectedWorkspacePreview = null;
            _selectedWorkspaceDiffResultId = null;
        }
        finally
        {
            _loadingWorkspace = false;
        }
    }

    private async Task LadeWorkspacePreviewAsync(WorkspaceFileNode node)
    {
        if (_aufgabe?.LokalerKlonPfad is null or "")
        {
            return;
        }

        var requestVersion = Interlocked.Increment(ref _previewLoadVersion);

        try
        {
            _selectedWorkspacePath = node.RelativePath;
            _selectedWorkspaceNode = node;
            _selectedWorkspacePreview = null;
            _selectedWorkspaceDiffResultId = null;
            var preview = await GitWorkspaceBrowserService.LoadPreviewAsync(_aufgabe.LokalerKlonPfad, node, _cts.Token);
            if (requestVersion != _previewLoadVersion || !string.Equals(_selectedWorkspacePath, node.RelativePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedWorkspacePreview = preview;
            _selectedWorkspaceDiffResultId = await ResolveSelectedWorkspaceDiffResultIdAsync(preview);
        }
        catch (Exception ex)
        {
            if (requestVersion != _previewLoadVersion)
            {
                return;
            }

            _logger.LogError(ex, "Workspace preview konnte nicht geladen werden.");
            _selectedWorkspacePreview = new FilePreview(node.RelativePath, node.SourceRelativePath, node.IsDeleted, false, false, null, null, ex.Message);
            _selectedWorkspaceDiffResultId = null;
        }
    }

    private async Task<Guid?> ResolveSelectedWorkspaceDiffResultIdAsync(FilePreview preview)
    {
        var relativePath = preview.RelativePath;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var diffResultId = await AufgabeService.GetLatestDiffResultIdForFileAsync(Id, relativePath, _cts.Token);
        if (diffResultId.HasValue
            || string.IsNullOrWhiteSpace(preview.SourceRelativePath)
            || string.Equals(preview.SourceRelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
        {
            return diffResultId;
        }

        return await AufgabeService.GetLatestDiffResultIdForFileAsync(Id, preview.SourceRelativePath, _cts.Token);
    }

    private static WorkspaceFileNode? FindNode(IEnumerable<WorkspaceFileNode> nodes, string relativePath)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            var child = FindNode(node.Children, relativePath);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private static void ExpandPath(IEnumerable<WorkspaceFileNode> nodes, string relativePath)
    {
        var segments = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return;
        }

        var currentNodes = nodes.ToList();
        var currentPath = string.Empty;
        for (var index = 0; index < Math.Max(0, segments.Length - 1); index++)
        {
            currentPath = string.IsNullOrEmpty(currentPath)
                ? segments[index]
                : Path.Combine(currentPath, segments[index]);

            var directory = currentNodes.FirstOrDefault(node => node.IsDirectory && string.Equals(node.RelativePath, currentPath, StringComparison.OrdinalIgnoreCase));
            if (directory is null)
            {
                return;
            }

            directory.IsExpanded = true;
            currentNodes = directory.Children;
        }
    }

    private bool IsWorkspaceNodeSelected(WorkspaceFileNode node)
        => string.Equals(_selectedWorkspacePath, node.RelativePath, StringComparison.OrdinalIgnoreCase);

    private ICollection<WorkspaceNodeRow> VisibleWorkspaceNodes
    {
        get
        {
            if (_workspaceSnapshot is null)
            {
                return [];
            }

            if (_useTreeLayout)
            {
                return _workspaceSnapshot.RootNodes
                    .SelectMany(node => FlattenWorkspaceNode(node, 0))
                    .ToList();
            }

            return _workspaceSnapshot.FlatFiles.Select(node => new WorkspaceNodeRow(node, 0)).ToList();
        }
    }

    private static IEnumerable<WorkspaceNodeRow> FlattenWorkspaceNode(WorkspaceFileNode node, int depth)
    {
        yield return new WorkspaceNodeRow(node, depth);

        if (node.IsDirectory && node.IsExpanded)
        {
            foreach (var child in node.Children)
            {
                foreach (var item in FlattenWorkspaceNode(child, depth + 1))
                {
                    yield return item;
                }
            }
        }
    }

    private async Task WorkspaceNodeClickedAsync(WorkspaceFileNode node)
    {
        Interlocked.Increment(ref _previewLoadVersion);

        if (node.IsDirectory)
        {
            node.IsExpanded = !node.IsExpanded;
            _selectedWorkspaceNode = node;
            _selectedWorkspacePreview = null;
            _selectedWorkspacePath = node.RelativePath;
            _selectedWorkspaceDiffResultId = null;
            return;
        }

        await LadeWorkspacePreviewAsync(node);
    }

    private async Task ExplorerRefreshAsync()
    {
        await LadeWorkspaceAsync();
    }

    private static (bool ShowPushPullToggle, bool ShowPush, bool ShowPull, bool ShowPullRequest, bool ShowMerge) EvaluateGitActionVisibility(GitActionCapabilities capabilities)
    {
        var isLocalCopy = capabilities.RepositoryKind == RepositoryKind.LocalDirectory && capabilities.IsWorkingDirectoryCopy;
        if (isLocalCopy)
        {
            return (
                ShowPushPullToggle: false,
                ShowPush: false,
                ShowPull: false,
                ShowPullRequest: false,
                ShowMerge: capabilities.CanMergeToSource);
        }

        var showPush = capabilities.CanPush;
        var showPull = capabilities.CanPull;
        return (
            ShowPushPullToggle: showPush || showPull,
            ShowPush: showPush,
            ShowPull: showPull,
            ShowPullRequest: capabilities.CanCreatePullRequest,
            ShowMerge: capabilities.CanMergeToSource);
    }

    private async Task CommitAsync()
    {
        if (string.IsNullOrWhiteSpace(_commitMessage)) { _fehler = "Commit-Nachricht ist Pflichtfeld."; return; }
        _processing = true;
        try
        {
            await GitService.CommitAsync(Id, _commitMessage, _cts.Token);
            _commitMessage = string.Empty;
            _showCommitForm = false;
            _erfolg = "Commit erfolgreich.";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task PushAsync()
    {
        _processing = true;
        try
        {
            await GitService.PushAsync(Id, _cts.Token);
            _erfolg = "Push erfolgreich.";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task PullAsync()
    {
        _processing = true;
        try
        {
            await GitService.PullAsync(Id, _cts.Token);
            _erfolg = "Pull erfolgreich.";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task MergeToSourceAsync()
    {
        _processing = true;
        try
        {
            await GitService.MergeToSourceAsync(Id, _cts.Token);
            _erfolg = "Merge ins Quellverzeichnis erfolgreich.";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task RepositoryStartskriptAusfuehrenAsync()
    {
        _processing = true;
        _fehler = null;
        _erfolg = null;
        try
        {
            var ergebnis = await EntwicklungsprozessService.RepositoryStartskriptAusfuehrenAsync(Id, _cts.Token);
            _erfolg = ergebnis.Message;
            await LadeAsync();
        }
        catch (Exception ex)
        {
            _fehler = ex.Message;
        }
        finally
        {
            _processing = false;
        }
    }

    private async Task ResetAsync()
    {
        _processing = true;
        try
        {
            await GitService.ResetAsync(Id, _resetType, _resetRef, _cts.Token);
            _showResetForm = false;
            _erfolg = $"git reset --{_resetType} erfolgreich.";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task StatusZuruecksetzenAsync()
    {
        _processing = true;
        _showStatusResetConfirm = false;
        _fehler = null;

        try
        {
            if (_aufgabe is null || _aufgabe.Status != AufgabeStatus.KiAktiv)
            {
                throw new InvalidOperationException("Status zurücksetzen ist nur für Aufgaben im Status 'KI Aktiv' verfügbar.");
            }

            var isRunning = RunningAutomationStatusSource.IsRunning(_aufgabe.Id);
            if (isRunning)
            {
                throw new InvalidOperationException("Status kann nicht zurückgesetzt werden, solange die Verarbeitung läuft.");
            }

            await AufgabeService.KiAbgeschlossenAsync(Id, _cts.Token);
            _erfolg = "Status wurde zurückgesetzt. Eine neue Anfrage kann jetzt gesendet werden.";
            await LadeAsync();
        }
        catch (Exception ex)
        {
            _fehler = ex.Message;
            await LadeAsync();
        }
        finally
        {
            _processing = false;
        }
    }

    private async Task PullRequestErstellenAsync()
    {
        if (string.IsNullOrWhiteSpace(_prTitel))
        {
            _fehler = "Titel ist ein Pflichtfeld.";
            return;
        }
        _processing = true;
        try
        {
            var pr = await GitService.PullRequestErstellenAsync(
                Id,
                title: _prTitel,
                body: _prBody ?? string.Empty,
                ct: _cts.Token);
            _showPullRequestForm = false;
            _prTitel = string.Empty;
            _prBody = null;
            _erfolg = $"Pull Request #{pr.Nummer} erstellt: {pr.Url}";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task AbschliessenAsync()
    {
        _processing = true;
        try
        {
            await EntwicklungsprozessService.AbschliessenAsync(Id, _cts.Token);
            _erfolg = "Aufgabe abgeschlossen.";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task ArchivierenAsync()
    {
        _processing = true;
        _showArchivierenConfirm = false;
        try
        {
            await AufgabeService.ArchivierenAsync(Id, _cts.Token);
            _erfolg = "Aufgabe archiviert.";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task VerwerfenAsync(VerwerfenAktion aktion)
    {
        _processing = true;
        _showVerwerfenConfirm = false;
        _fehler = null;
        try
        {
            await AufgabeService.VerwerfenAsync(Id, aktion, _cts.Token);
            if (_aufgabe is not null)
            {
                NavigationManager.NavigateTo($"projekte/{_aufgabe.ProjektId}");
            }
        }
        catch (Exception ex)
        {
            _fehler = ex.Message;
            _processing = false;
        }
    }

    private async Task AufgabeLoeschenAsync()
    {
        _processing = true;
        _showDeleteConfirm = false;
        try
        {
            await AufgabeService.DeleteAsync(Id, _cts.Token);
            if (_aufgabe is not null)
                NavigationManager.NavigateTo($"projekte/{_aufgabe.ProjektId}");
        }
        catch (Exception ex) { _fehler = ex.Message; _processing = false; }
    }

    private async Task AbbrechenAsync()
    {
        _processing = true;
        _showAbbrechenConfirm = false;
        try
        {
            await EntwicklungsprozessService.AbbrechenAsync(Id, _cts.Token);
            _erfolg = "Aufgabe abgebrochen.";
            await LadeAsync();
        }
        catch (Exception ex) { _fehler = ex.Message; }
        finally { _processing = false; }
    }

    private async Task WiederherstellenAsync()
    {
        _processing = true;
        _showRecoveryConfirm = false;
        _fehler = null;
        try
        {
            await RecoveryService.RecoverManuellAsync(Id, _cts.Token);
            _erfolg = "Aufgabe wurde erfolgreich wiederhergestellt.";
            await LadeAsync();
        }
        catch (InvalidOperationException ex)
        {
            _fehler = ex.Message;
            await LadeAsync();
        }
        finally
        {
            _processing = false;
        }
    }

    private void Zurueck()
    {
        if (_aufgabe is not null)
            NavigationManager.NavigateTo($"projekte/{_aufgabe.ProjektId}");
        else
            NavigationManager.NavigateTo("projekte");
    }

    private void DiffAnzeigen()
    {
        if (_latestDiffResultId is Guid diffResultId)
        {
            NavigationManager.NavigateTo($"/diff/{diffResultId}");
        }
    }

    private string BuildAusfuehrungsdauerText()
    {
        var letzterPrompt = _protokoll.LastOrDefault(eintrag => eintrag.Typ == ProtokollTyp.Prompt)?.Zeitstempel;
        if (letzterPrompt is null)
        {
            return "–";
        }

        var letztesEreignis = _protokoll.LastOrDefault()?.Zeitstempel ?? letzterPrompt.Value;
        var dauer = letztesEreignis - letzterPrompt.Value;
        if (dauer < TimeSpan.Zero)
        {
            dauer = TimeSpan.Zero;
        }

        if (dauer.TotalHours >= 1)
        {
            return $"{(int)dauer.TotalHours:00}:{dauer.Minutes:00}:{dauer.Seconds:00}";
        }

        return $"{dauer.Minutes:00}:{dauer.Seconds:00}";
    }

    private static string GetProtokollCssClass(ProtokollTyp typ) => typ switch
    {
        ProtokollTyp.Prompt => "protokoll-prompt",
        ProtokollTyp.KiAntwort => "protokoll-antwort",
        ProtokollTyp.StatusUebergang => "protokoll-status",
        ProtokollTyp.GitAktion => "protokoll-git",
        ProtokollTyp.TestErgebnis => "protokoll-test",
        _ => ""
    };

    private static string GetProtokollLabel(ProtokollTyp typ) => typ switch
    {
        ProtokollTyp.Prompt => "Prompt",
        ProtokollTyp.KiAntwort => "KI",
        ProtokollTyp.StatusUebergang => "Status",
        ProtokollTyp.GitAktion => "Git",
        ProtokollTyp.TestErgebnis => "Test",
        _ => typ.ToString()
    };

    private static MarkupString RenderProtokollInhalt(string inhalt)
    {
        if (string.IsNullOrWhiteSpace(inhalt))
        {
            return new MarkupString($"<pre>{HtmlEncoder.Default.Encode("–")}</pre>");
        }

        try
        {
            var html = Markdown.ToHtml(inhalt, _protokollMarkdownPipeline);
            var sanitized = SanitizeMarkdownHtml(html);
            return new MarkupString(string.IsNullOrWhiteSpace(sanitized) ? BuildFallbackHtml(inhalt) : sanitized);
        }
        catch
        {
            return new MarkupString(BuildFallbackHtml(inhalt));
        }
    }

    private static string SanitizeMarkdownHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var sanitized = _unsafeHtmlEventAttributeRegex.Replace(html, string.Empty);
        sanitized = _unsafeHtmlUriRegex.Replace(sanitized, " $1=\"#\"");
        return sanitized;
    }

    private static string BuildFallbackHtml(string inhalt)
        => $"<pre>{HtmlEncoder.Default.Encode(inhalt)}</pre>";

    private string BuildStreamingArbeitsprotokollMarkdown()
    {
        var zeilen = _streamingLines
            .Where(zeile => !string.IsNullOrWhiteSpace(zeile))
            .Select(zeile => zeile.TrimEnd())
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine($"# {_kiStreamingStartedUtc:yyyy-MM-dd}");
        builder.AppendLine();

        if (zeilen.Length == 0)
        {
            builder.AppendLine("## Schritt 1");
            builder.AppendLine("Warte auf Ausgabe...");
            return builder.ToString().TrimEnd();
        }

        for (var i = 0; i < zeilen.Length; i++)
        {
            builder.AppendLine($"## Schritt {i + 1}");
            builder.AppendLine(zeilen[i]);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private async Task ClearErfolgAsync()
    {
        await Task.Delay(4000);
        _erfolg = null;
        StateHasChanged();
    }

    public void Dispose()
    {
        // Subscription freigeben: Background-Task läuft weiter, nur der Live-Feed wird getrennt
        _kiSubscription?.Dispose();
        // _cts wird nur für sonstige UI-Operationen (nicht für KI-Hintergrundlauf) verwendet
        _cts.Cancel();
        _cts.Dispose();
    }
}
