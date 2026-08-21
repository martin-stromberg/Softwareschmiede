using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.ViewModels;

/// <summary>ViewModel für die Detail-Ansicht einer Autonomen Aufgabe: Konfiguration, plan.md/progress.md/governance.md, Start/Stop/Resume-Kontrollen.</summary>
public sealed class AutonomAufgabeDetailViewModel : ViewModelBase
{
    private readonly ProjektleiterAgentService _projektleiterAgentService;
    private readonly SessionManagementService _sessionManagementService;
    private readonly ILogger<AutonomAufgabeDetailViewModel> _logger;
    private Aufgabe? _aufgabe;

    private AutonomAufgabeKonfiguration _konfiguration = null!;
    private string _planContent = string.Empty;
    private string _progressContent = string.Empty;
    private string _governanceContent = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;

    /// <summary>Konfiguration der angezeigten Autonomen Aufgabe.</summary>
    public AutonomAufgabeKonfiguration Konfiguration
    {
        get => _konfiguration;
        private set => SetProperty(ref _konfiguration, value);
    }

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
        ProjektleiterAgentService projektleiterAgentService,
        SessionManagementService sessionManagementService,
        ILogger<AutonomAufgabeDetailViewModel> logger)
    {
        _projektleiterAgentService = projektleiterAgentService;
        _sessionManagementService = sessionManagementService;
        _logger = logger;

        StartCommand = new AsyncRelayCommand(ct => StarteAgentAsync(ct), () => !IsBusy);
        StopCommand = new AsyncRelayCommand(ct => StoppeAgentAsync(ct), () => !IsBusy);
        ResumeCommand = new AsyncRelayCommand(ct => ResumeAgentAsync(ct), () => !IsBusy);
        SavePlanCommand = new AsyncRelayCommand(ct => AktualisierePlanAsync(PlanContent, ct), () => !IsBusy);
    }

    /// <summary>Initialisiert die Detail-Ansicht mit Aufgabe, Konfiguration, Unteragenten und Skills.</summary>
    public void Initialize(
        Aufgabe aufgabe,
        AutonomAufgabeKonfiguration konfiguration,
        IReadOnlyList<UnteragentSpezifikation>? unteragenten = null,
        IReadOnlyList<SkillDefinition>? skills = null)
    {
        ArgumentNullException.ThrowIfNull(aufgabe);
        ArgumentNullException.ThrowIfNull(konfiguration);

        _aufgabe = aufgabe;
        Konfiguration = konfiguration;

        Unteragenten.Clear();
        foreach (var unteragent in unteragenten ?? [])
        {
            Unteragenten.Add(unteragent);
        }

        Skills.Clear();
        foreach (var skill in skills ?? [])
        {
            Skills.Add(skill);
        }

        ErrorMessage = null;
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
            token => _projektleiterAgentService.StarteAgentAsync(Konfiguration, token),
            "Projektleiter-Agent konnte nicht gestartet werden",
            ct);

    /// <summary>Stoppt (pausiert) den Projektleiter-Agenten.</summary>
    public Task StoppeAgentAsync(CancellationToken ct = default)
    {
        if (_aufgabe is null)
        {
            return Task.CompletedTask;
        }

        return FuehreAgentOperationAsync(
            token => _sessionManagementService.PauseAufgabeBeiBudgetLimitAsync(_aufgabe, token),
            "Projektleiter-Agent konnte nicht gestoppt werden",
            ct);
    }

    /// <summary>Setzt den Projektleiter-Agenten nach einer Session-Pause fort.</summary>
    public Task ResumeAgentAsync(CancellationToken ct = default)
    {
        if (_aufgabe is null)
        {
            return Task.CompletedTask;
        }

        return FuehreAgentOperationAsync(
            token => _sessionManagementService.SetzeFortAsync(_aufgabe, token),
            "Projektleiter-Agent konnte nicht fortgesetzt werden",
            ct);
    }

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
}
