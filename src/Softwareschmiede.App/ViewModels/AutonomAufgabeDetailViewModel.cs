using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für die Detail-Ansicht einer Autonomen Aufgabe: Konfiguration, plan.md/progress.md/governance.md, Start/Stop/Resume-Kontrollen.</summary>
public sealed class AutonomAufgabeDetailViewModel : ViewModelBase, IDisposable
{
    private readonly ProjektleiterAgentService _projektleiterAgentService;
    private readonly SessionManagementService _sessionManagementService;
    private readonly KiAusfuehrungsService _kiAusfuehrungsService;
    private readonly ILogger<AutonomAufgabeDetailViewModel> _logger;
    private readonly Aufgabe _aufgabe;

    private readonly AutonomAufgabeKonfiguration _konfiguration;
    private string _planContent = string.Empty;
    private string _progressContent = string.Empty;
    private string _governanceContent = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;
    private bool _cliIsRunning;
    private bool _disposed;

    /// <summary>Konfiguration der angezeigten Autonomen Aufgabe.</summary>
    public AutonomAufgabeKonfiguration Konfiguration => _konfiguration;

    /// <summary>Unteragenten der Autonomen Aufgabe.</summary>
    public ObservableCollection<UnteragentSpezifikation> Unteragenten { get; } = [];

    /// <summary>Skills der Autonomen Aufgabe.</summary>
    public ObservableCollection<SkillDefinition> Skills { get; } = [];

    /// <summary>Inhalt von plan.md.</summary>
    public string PlanContent
    {
        get => _planContent;
        set => SetProperty(ref _planContent, value);
    }

    /// <summary>Inhalt von progress.md.</summary>
    public string ProgressContent
    {
        get => _progressContent;
        set => SetProperty(ref _progressContent, value);
    }

    /// <summary>Inhalt von governance.md.</summary>
    public string GovernanceContent
    {
        get => _governanceContent;
        set => SetProperty(ref _governanceContent, value);
    }

    /// <summary>Fehlermeldung der Detail-Ansicht.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Gibt an, ob gerade eine Kontroll-Operation läuft.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Gibt an, ob für die angezeigte Aufgabe aktuell ein echter CLI-Prozess läuft.</summary>
    public bool CliIsRunning
    {
        get => _cliIsRunning;
        private set => SetProperty(ref _cliIsRunning, value);
    }

    /// <summary>Startet den Projektleiter-Agenten.</summary>
    public ICommand StartCommand { get; }

    /// <summary>Stoppt (pausiert) den Projektleiter-Agenten.</summary>
    public ICommand StopCommand { get; }

    /// <summary>Setzt den Projektleiter-Agenten nach einer Pause fort.</summary>
    public ICommand ResumeCommand { get; }

    /// <summary>Speichert Änderungen an plan.md.</summary>
    public ICommand SavePlanCommand { get; }

    /// <inheritdoc cref="AutonomAufgabeDetailViewModel"/>
    public AutonomAufgabeDetailViewModel(
        Aufgabe aufgabe,
        AutonomAufgabeKonfiguration konfiguration,
        ProjektleiterAgentService projektleiterAgentService,
        SessionManagementService sessionManagementService,
        KiAusfuehrungsService kiAusfuehrungsService,
        ILogger<AutonomAufgabeDetailViewModel> logger,
        IReadOnlyList<UnteragentSpezifikation>? unteragenten = null,
        IReadOnlyList<SkillDefinition>? skills = null)
    {
        ArgumentNullException.ThrowIfNull(aufgabe);
        ArgumentNullException.ThrowIfNull(konfiguration);

        _aufgabe = aufgabe;
        _konfiguration = konfiguration;
        _projektleiterAgentService = projektleiterAgentService;
        _sessionManagementService = sessionManagementService;
        _kiAusfuehrungsService = kiAusfuehrungsService;
        _logger = logger;

        foreach (var unteragent in unteragenten ?? [])
        {
            Unteragenten.Add(unteragent);
        }

        foreach (var skill in skills ?? [])
        {
            Skills.Add(skill);
        }

        _cliIsRunning = _kiAusfuehrungsService.IsRunning(_aufgabe.Id);
        _kiAusfuehrungsService.CliProcessStatusChanged += OnCliProcessStatusChanged;

        // Zusätzlich an !CliIsRunning gebunden: Start/Resume sind sinnlos (bzw. bei Start sogar schädlich), solange
        // die CLI der Aufgabe bereits läuft. Verhindert insbesondere, dass ein zweiter Klick auf "Start" innerhalb
        // des Verzögerungsfensters von SendeInitialPromptVerzoegertAsync (siehe ProjektleiterAgentService) den
        // Initialprompt doppelt an dieselbe CLI-Session sendet: KiAusfuehrungsService.StartWithPseudoConsoleAsync
        // setzt CliIsRunning bereits synchron auf true, bevor IsBusy im finally-Block zurückgesetzt wird.
        StartCommand = new AsyncRelayCommand(ct => StarteAgentAsync(ct), () => !IsBusy && !CliIsRunning);
        // Bewusst nicht auf CliIsRunning eingeschränkt: "Beenden" muss auch dann verfügbar sein, wenn der
        // CLI-Prozess bereits (z. B. zwischen zwei Turns oder durch Absturz) nicht mehr läuft — StoppeAgenExplizitAsync
        // setzt ExplizitGestoppt best-effort in jedem Fall und verhindert so einen ungewollten Auto-Resume beim
        // nächsten App-Neustart, unabhängig vom aktuellen Prozessstatus.
        StopCommand = new AsyncRelayCommand(ct => StoppeAgentAsync(ct), () => !IsBusy);
        ResumeCommand = new AsyncRelayCommand(ct => ResumeAgentAsync(ct), () => !IsBusy && !CliIsRunning);
        SavePlanCommand = new AsyncRelayCommand(ct => AktualisierePlanAsync(PlanContent, ct), () => !IsBusy);
    }

    /// <summary>Lädt plan.md aus dem Arbeitsverzeichnis.</summary>
    public Task LaedePlanAsync(CancellationToken ct = default)
        => LadeDateiInhaltAsync("plan.md", content => PlanContent = content, ct);

    /// <summary>Lädt progress.md aus dem Arbeitsverzeichnis.</summary>
    public Task LaedeProgressAsync(CancellationToken ct = default)
        => LadeDateiInhaltAsync("progress.md", content => ProgressContent = content, ct);

    /// <summary>Lädt governance.md aus dem Arbeitsverzeichnis.</summary>
    public Task LaedeGovernanceAsync(CancellationToken ct = default)
        => LadeDateiInhaltAsync("governance.md", content => GovernanceContent = content, ct);

    /// <summary>Speichert Änderungen an plan.md.</summary>
    public async Task AktualisierePlanAsync(string content, CancellationToken ct = default)
    {
        var pfad = Path.Combine(Konfiguration.ArbeitsverzeichnisPfad, "plan.md");
        await File.WriteAllTextAsync(pfad, content, ct);
        PlanContent = content;
    }

    /// <summary>Startet den Projektleiter-Agenten für die angezeigte Autonome Aufgabe.</summary>
    public Task StarteAgentAsync(CancellationToken ct = default)
        => FuehreAgentOperationAsync(
            token => _projektleiterAgentService.StarteAgentAsync(Konfiguration, optionalResumePrompt: null, ct: token),
            "Projektleiter-Agent konnte nicht gestartet werden",
            ct);

    /// <summary>Stoppt den Projektleiter-Agenten explizit: setzt <see cref="AutonomAufgabeKonfiguration.ExplizitGestoppt"/>
    /// und beendet den laufenden CLI-Prozess. Verhindert einen automatischen Wiederstart nach dem nächsten App-Neustart.</summary>
    public Task StoppeAgentAsync(CancellationToken ct = default)
        => FuehreAgentOperationAsync(
            token => _projektleiterAgentService.StoppeAgenExplizitAsync(_aufgabe.Id, token),
            "Projektleiter-Agent konnte nicht gestoppt werden",
            ct);

    /// <summary>Setzt den Projektleiter-Agenten nach einer Session-Pause fort.</summary>
    public Task ResumeAgentAsync(CancellationToken ct = default)
        => FuehreAgentOperationAsync(
            token => _sessionManagementService.SetzeFortAsync(_aufgabe, token),
            "Projektleiter-Agent konnte nicht fortgesetzt werden",
            ct);

    private async Task LadeDateiInhaltAsync(string dateiname, Action<string> setter, CancellationToken ct)
    {
        var pfad = Path.Combine(Konfiguration.ArbeitsverzeichnisPfad, dateiname);
        setter(File.Exists(pfad) ? await File.ReadAllTextAsync(pfad, ct) : string.Empty);
    }

    private async Task FuehreAgentOperationAsync(Func<CancellationToken, Task> operation, string fehlerKontext, CancellationToken ct)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await operation(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{FehlerKontext}.", fehlerKontext);
            ErrorMessage = $"{fehlerKontext}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnCliProcessStatusChanged(Guid aufgabeId, CliProcessStatus status)
    {
        if (aufgabeId != _aufgabe.Id)
        {
            return;
        }

        CliIsRunning = status == CliProcessStatus.Gestartet;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _kiAusfuehrungsService.CliProcessStatusChanged -= OnCliProcessStatusChanged;
    }
}
