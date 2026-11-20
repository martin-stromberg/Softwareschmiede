using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Koordiniert den gesamten Update-Ablauf des Hauptfensters: einmalige Startautomatik,
/// manuelle Prüfung, Installation sowie die Reaktion auf gespeicherte Update--Einstellungen.
/// Kapselt Zustand (Angebot, Hinweis, laufende Prüfung/Vorbereitung), Serialisierung über
/// ein Gate und den Generationsschutz gegen veraltete Ergebnisse.
/// </summary>
public sealed class MainWindowUpdateFlow : ViewModelBase
{
    private const string UpdateSettingsNotReadableNotice =
        "Die Update--Einstellungen konnten nicht gelesen werden. Eine Update-Prüfung ist nicht möglich.";
    private const string UpdateSettingsChangedNotice =
        "Die Update--Einstellungen wurden geändert. Der Update-Vorgang wurde abgebrochen.";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly IUpdateService? _updateService;
    private readonly ICliUpdateSafetyService? _cliUpdateSafetyService;
    private readonly IUpdateProgressDialogService? _updateProgressDialogService;
    private readonly IDialogService? _dialogService;
    private readonly IUpdateAttemptLog? _attemptLog;
    private readonly SemaphoreSlim _updateGate = new(1, 1);

    private UpdateSettings? _currentUpdateSettings;
    private int _updateSettingsGeneration;
    private int _startInitializationCompleted;
    private CancellationTokenSource? _updateFlowCts;
    private bool _updateAvailable;
    private UpdateInfo? _availableUpdate;
    private bool _updateCheckRunning;
    private bool _updateWillPrepared;
    private string? _updateNotice;

    /// <inheritdoc cref="MainWindowUpdateFlow"/>
    public MainWindowUpdateFlow(
        IServiceProvider serviceProvider,
        ILogger logger,
        MainWindowUpdateServices? services,
        IDialogService? dialogService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _updateService = services?.UpdateService;
        _cliUpdateSafetyService = services?.CliUpdateSafetyService;
        _updateProgressDialogService = services?.UpdateProgressDialogService;
        _attemptLog = services?.AttemptLog;
        _dialogService = dialogService;
    }

    /// <summary>Gibt an, ob ein neueres Programmupdate verfügbar ist.</summary>
    public bool UpdateAvailable
    {
        get => _updateAvailable;
        private set => SetProperty(ref _updateAvailable, value, RelayCommand.Refresh);
    }

    /// <summary>Informationen zum aktuell verfügbaren Update.</summary>
    public UpdateInfo? AvailableUpdate
    {
        get => _availableUpdate;
        private set => SetProperty(ref _availableUpdate, value);
    }

    /// <summary>Gibt an, ob gerade eine Update-Prüfung läuft.</summary>
    public bool UpdateCheckRunning
    {
        get => _updateCheckRunning;
        private set => SetProperty(ref _updateCheckRunning, value, RelayCommand.Refresh);
    }

    /// <summary>Gibt an, ob gerade ein Update heruntergeladen oder vorbereitet wird.</summary>
    public bool UpdateWillPrepared
    {
        get => _updateWillPrepared;
        private set => SetProperty(ref _updateWillPrepared, value, RelayCommand.Refresh);
    }

    /// <summary>Optionaler Hinweis zur letzten Update-Prüfung.</summary>
    public string? UpdateNotice
    {
        get => _updateNotice;
        private set => SetProperty(ref _updateNotice, value);
    }

    /// <summary>Tooltip der Prüfen-Schaltfläche; begründet die Deaktivierung im Modus „Aus".</summary>
    public string UpdateCheckTooltip
        => _currentUpdateSettings is { Mode: UpdateMode.Off }
            ? "Update-Prüfung ist in den Einstellungen deaktiviert."
            : "Auf Programmupdate prüfen";

    /// <summary>Gibt an, ob eine manuelle Update-Prüfung aktuell gestartet werden kann.</summary>
    public bool CheckCan()
        => _updateService is not null
           && _currentUpdateSettings is { Mode: not UpdateMode.Off }
           && !UpdateCheckRunning
           && !UpdateWillPrepared;

    /// <summary>Gibt an, ob die Installation des angebotenen Updates aktuell gestartet werden kann.</summary>
    public bool StartCan()
        => _updateService is not null
           && _cliUpdateSafetyService is not null
           && _updateProgressDialogService is not null
           && _dialogService is not null
           && _currentUpdateSettings is { Mode: not UpdateMode.Off }
           && UpdateAvailable
           && !UpdateCheckRunning
           && !UpdateWillPrepared;

    /// <summary>
    /// Übernimmt einen per Settings-UI gespeicherten Einstellungs-Snapshot: erhöht die
    /// Generation (laufende Versuche verwerfen ihr Ergebnis), bricht den Update-Ablauf ab
    /// und entfernt ein ggf. veraltetes Angebot.
    /// </summary>
    public void ApplySavedUpdateSettings(UpdateSettings settings)
    {
        var changed = _currentUpdateSettings != settings;
        _currentUpdateSettings = settings;

        if (changed)
        {
            Interlocked.Increment(ref _updateSettingsGeneration);
            CancelRunningUpdateFlow();
            AvailableUpdate = null;
            UpdateAvailable = false;
            UpdateNotice = null;
        }

        OnPropertyChanged(nameof(UpdateCheckTooltip));
        RelayCommand.Refresh();
    }

    /// <summary>Bricht einen laufenden Update-Ablauf (Vorbereitung/Start) ab.</summary>
    public void CancelRunningUpdateFlow()
    {
        try
        {
            _updateFlowCts?.Cancel();
        }
        catch (Exception ex)
        {
            // Der Ablauf-Token kann bereits entsorgt sein, oder ein Token-Callback wirft
            // (Cancel aggregiert Callback-Exceptions). Beides darf den Aufrufer nicht treffen.
            _logger.LogDebug(ex, "Abbruch des laufenden Update-Ablaufs ist fehlgeschlagen.");
        }
    }

    /// <summary>
    /// Führt die einmalige Update-Startautomatik aus, sobald das Hauptfenster gerendert ist.
    /// Das Startkennzeichen wird vor dem ersten <c>await</c> gesetzt; weitere Aufrufe sind wirkungslos.
    /// Je nach gespeichertem Modus wird nichts, nur geprüft oder geprüft und installiert.
    /// </summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public async Task InitializeAfterWindowReadyAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _startInitializationCompleted, 1) != 0)
            return;

        _attemptLog?.LogWindowReady();

        if (_updateService is null)
            return;

        await RunUpdateAttemptAsync("startup", preparatory: false, async (settings, generation, token) =>
        {
            token.ThrowIfCancellationRequested();
            var result = await _updateService.CheckForUpdateAsync(new UpdateCheckOptions(settings.UpdatePath), token);
            if (!IsUpdateAttemptCurrent(generation))
                return;

            ApplyUpdateCheckResult(result, isManualRefresh: false);

            if (settings.Mode == UpdateMode.CheckAndRunAtStartup
                && result.Status == UpdateCheckStatus.UpdateAvailable
                && result.Update is not null)
            {
                await InstallUpdateAsync(result.Update, settings, generation, token);
            }
        }, ct);
    }

    /// <summary>Prüft manuell auf Programmupdates und aktualisiert das Angebot bzw. den Hinweis.</summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public Task CheckAsync(CancellationToken ct)
    {
        if (_updateService is null)
            return Task.CompletedTask;

        return RunUpdateAttemptAsync("pruefen", preparatory: false, async (settings, generation, token) =>
        {
            token.ThrowIfCancellationRequested();
            var result = await _updateService.CheckForUpdateAsync(new UpdateCheckOptions(settings.UpdatePath), token);
            if (!IsUpdateAttemptCurrent(generation))
                return;

            ApplyUpdateCheckResult(result, isManualRefresh: true);
        }, ct);
    }

    /// <summary>
    /// Startet den geführten Update-Ablauf: prüft erneut mit den aktuellen Optionen und
    /// installiert bei Befund. Ein älteres Angebot wird nicht als Installationsargument
    /// verwendet.
    /// </summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public Task StartAsync(CancellationToken ct)
    {
        if (_updateService is null || _cliUpdateSafetyService is null || _updateProgressDialogService is null || _dialogService is null)
            return Task.CompletedTask;

        return RunUpdateAttemptAsync("starten", preparatory: true, async (settings, generation, token) =>
        {
            token.ThrowIfCancellationRequested();
            var checkResult = await _updateService.CheckForUpdateAsync(new UpdateCheckOptions(settings.UpdatePath), token);
            if (!IsUpdateAttemptCurrent(generation))
                return;

            ApplyUpdateCheckResult(checkResult, isManualRefresh: false);
            if (checkResult.Status != UpdateCheckStatus.UpdateAvailable || checkResult.Update is null)
                return;

            await InstallUpdateAsync(checkResult.Update, settings, generation, token);
        }, ct);
    }

    /// <summary>
    /// Gemeinsames Gerüst eines Updateversuchs: Gate-Akquise, Versuchsprotokoll,
    /// Busy-Flag, Generations-Snapshot, Settings-Read und der Aus-Guard. Der
    /// versuchspezifische Kern läuft als <paramref name="kern"/>.
    /// </summary>
    private async Task RunUpdateAttemptAsync(
        string attemptKind,
        bool preparatory,
        Func<UpdateSettings, int, CancellationToken, Task> kern,
        CancellationToken ct)
    {
        if (!await _updateGate.WaitAsync(0))
            return;

        _attemptLog?.BeginAttempt(attemptKind);
        if (preparatory)
            UpdateWillPrepared = true;
        else
            UpdateCheckRunning = true;
        UpdateNotice = null;
        var generation = Volatile.Read(ref _updateSettingsGeneration);
        try
        {
            var settings = await ReadUpdateSettingsAsync(ct);
            if (settings is null)
            {
                ShowUpdateSettingsReadFailure();
                return;
            }

            if (settings.Mode == UpdateMode.Off)
            {
                RemoveUpdateOffer();
                return;
            }

            await kern(settings, generation, ct);
        }
        catch (OperationCanceledException)
        {
            // Abbruch beim Schließen oder durch geänderte Settings.
        }
        finally
        {
            if (preparatory)
                UpdateWillPrepared = false;
            else
                UpdateCheckRunning = false;
            // BeendeVersuch muss vor der Gate-Freigabe liegen: Sonst kann der nächste Versuch
            // beginnen, bevor dieser Versuch AktuellerVersuch zurückgesetzt hat.
            _attemptLog?.EndAttempt(attemptKind, UpdateAvailable, UpdateNotice);
            _updateGate.Release();
        }
    }

    private async Task InstallUpdateAsync(
        UpdateInfo update,
        UpdateSettings snapshot,
        int generation,
        CancellationToken ct)
    {
        UpdateWillPrepared = true;
        using var updateCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _updateFlowCts = updateCts;
        var progressViewModel = new UpdateProgressViewModel(updateCts.Cancel);
        try
        {
            var safety = await _cliUpdateSafetyService!.CheckAsync(updateCts.Token);
            if (!IsUpdateAttemptCurrent(generation))
                return;

            updateCts.Token.ThrowIfCancellationRequested();
            if (safety.RequiresConfirmation && !_dialogService!.ConfirmDialog(BuildSafetyMessage(safety), "Update starten?"))
                return;

            var progress = new Progress<UpdatePreparationProgress>(progressViewModel.Apply);
            _updateProgressDialogService!.Show(progressViewModel);

            // Aktualitätsnachweis unmittelbar vor der Vorbereitung: gespeicherte Werte erneut lesen.
            if (!await CheckSettingsFreshnessAsync(snapshot, generation, progressViewModel, updateCts.Token))
                return;

            updateCts.Token.ThrowIfCancellationRequested();
            var preparation = await _updateService!.PrepareUpdateAsync(update, progress, updateCts.Token);
            if (!IsUpdateAttemptCurrent(generation))
                return;

            updateCts.Token.ThrowIfCancellationRequested();

            // Letzter Aktualitätsnachweis unmittelbar vor dem Updater-Start.
            if (!await CheckSettingsFreshnessAsync(snapshot, generation, progressViewModel, updateCts.Token))
                return;

            updateCts.Token.ThrowIfCancellationRequested();
            progressViewModel.MarkUpdaterStarting();
            await _updateService.StartPreparedUpdateAsync(preparation, updateCts.Token);
        }
        catch (OperationCanceledException)
        {
            UpdateNotice = "Update-Vorbereitung wurde abgebrochen.";
            progressViewModel.SetError("Update-Vorbereitung wurde abgebrochen.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update konnte nicht vorbereitet oder gestartet werden.");
            progressViewModel.SetError("Update konnte nicht vorbereitet werden. Details wurden protokolliert.");
            UpdateNotice = "Update konnte nicht vorbereitet werden.";
        }
        finally
        {
            _updateFlowCts = null;
            UpdateWillPrepared = false;
        }
    }

    /// <summary>
    /// Aktualitätsnachweis vor einem kritischen Installationsschritt: liest die gespeicherten
    /// Werte erneut und bricht ab, wenn sie nicht lesbar sind oder seit dem Snapshot geändert
    /// wurden bzw. die Einstellungen-Generation inzwischen weitergelaufen ist.
    /// </summary>
    private async Task<bool> CheckSettingsFreshnessAsync(
        UpdateSettings snapshot,
        int generation,
        UpdateProgressViewModel progressViewModel,
        CancellationToken ct)
    {
        var current = await ReadUpdateSettingsAsync(ct);
        if (current is null)
        {
            ShowUpdateSettingsReadFailure();
            progressViewModel.SetError(UpdateSettingsNotReadableNotice);
            return false;
        }

        if (current != snapshot || !IsUpdateAttemptCurrent(generation))
        {
            RemoveUpdateOffer();
            progressViewModel.SetError(UpdateSettingsChangedNotice);
            return false;
        }

        return true;
    }

    private async Task<UpdateSettings?> ReadUpdateSettingsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<AppSettingService>();
            var settings = await settingService.GetUpdateSettingsAsync(ct);
            _currentUpdateSettings = settings;
            OnPropertyChanged(nameof(UpdateCheckTooltip));
            return settings;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update--Einstellungen konnten nicht gelesen werden.");
            return null;
        }
    }

    private bool IsUpdateAttemptCurrent(int generation)
        => Volatile.Read(ref _updateSettingsGeneration) == generation;

    private void ShowUpdateSettingsReadFailure()
    {
        RemoveUpdateOffer();
        UpdateNotice = UpdateSettingsNotReadableNotice;
    }

    private void RemoveUpdateOffer()
    {
        AvailableUpdate = null;
        UpdateAvailable = false;
    }

    private void ApplyUpdateCheckResult(UpdateCheckResult result, bool isManualRefresh)
    {
        if (result.Status == UpdateCheckStatus.UpdateAvailable && result.Update is not null)
        {
            AvailableUpdate = result.Update;
            UpdateAvailable = true;
            UpdateNotice = result.Message;
            return;
        }

        AvailableUpdate = null;
        UpdateAvailable = false;
        UpdateNotice = result.Status == UpdateCheckStatus.NotCheckable || isManualRefresh
            ? result.Message
            : null;
    }

    private static string BuildSafetyMessage(CliUpdateSafetyResult safety)
    {
        var tasks = string.Join(Environment.NewLine, safety.RiskyTasks.Take(5).Select(t => $"- {t}"));
        var suffix = safety.RiskyTaskCount > 5
            ? $"{Environment.NewLine}- weitere {safety.RiskyTaskCount - 5} Aufgabe(n)"
            : string.Empty;
        return $"Es laufen {safety.RiskyTaskCount} CLI-Aufgabe(n), die nicht auf Eingabe warten.{Environment.NewLine}{Environment.NewLine}{tasks}{suffix}{Environment.NewLine}{Environment.NewLine}Soll das Update trotzdem vorbereitet und die Anwendung beendet werden?";
    }
}
