using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Koordiniert den gesamten Update-Ablauf des Hauptfensters: einmalige Startautomatik,
/// manuelle Prüfung, Installation sowie die Reaktion auf gespeicherte Update-Einstellungen.
/// Kapselt Zustand (Angebot, Hinweis, laufende Prüfung/Vorbereitung), Serialisierung über
/// ein Gate und den Generationsschutz gegen veraltete Ergebnisse.
/// </summary>
public sealed class MainWindowUpdateFlow : ViewModelBase
{
    private const string UpdateEinstellungenNichtLesbarHinweis =
        "Die Update-Einstellungen konnten nicht gelesen werden. Eine Update-Prüfung ist nicht möglich.";
    private const string UpdateEinstellungenGeaendertHinweis =
        "Die Update-Einstellungen wurden geändert. Der Update-Vorgang wurde abgebrochen.";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly IUpdateService? _updateService;
    private readonly ICliUpdateSafetyService? _cliUpdateSafetyService;
    private readonly IUpdateProgressDialogService? _updateProgressDialogService;
    private readonly IDialogService? _dialogService;
    private readonly IUpdateVersuchProtokoll? _versuchProtokoll;
    private readonly SemaphoreSlim _updateGate = new(1, 1);

    private UpdateSettings? _aktuelleUpdateEinstellungen;
    private int _updateSettingsGeneration;
    private int _startInitialisierungErfolgt;
    private CancellationTokenSource? _updateAblaufCts;
    private bool _updateVerfuegbar;
    private UpdateInfo? _verfuegbaresUpdate;
    private bool _updateCheckLaeuft;
    private bool _updateWirdVorbereitet;
    private string? _updateHinweis;

    /// <inheritdoc cref="MainWindowUpdateFlow"/>
    public MainWindowUpdateFlow(
        IServiceProvider serviceProvider,
        ILogger logger,
        MainWindowUpdateDienste? dienste,
        IDialogService? dialogService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _updateService = dienste?.UpdateService;
        _cliUpdateSafetyService = dienste?.CliUpdateSafetyService;
        _updateProgressDialogService = dienste?.UpdateProgressDialogService;
        _versuchProtokoll = dienste?.VersuchProtokoll;
        _dialogService = dialogService;
    }

    /// <summary>Gibt an, ob ein neueres Programmupdate verfügbar ist.</summary>
    public bool UpdateVerfuegbar
    {
        get => _updateVerfuegbar;
        private set => SetProperty(ref _updateVerfuegbar, value, RelayCommand.Refresh);
    }

    /// <summary>Informationen zum aktuell verfügbaren Update.</summary>
    public UpdateInfo? VerfuegbaresUpdate
    {
        get => _verfuegbaresUpdate;
        private set => SetProperty(ref _verfuegbaresUpdate, value);
    }

    /// <summary>Gibt an, ob gerade eine Update-Prüfung läuft.</summary>
    public bool UpdateCheckLaeuft
    {
        get => _updateCheckLaeuft;
        private set => SetProperty(ref _updateCheckLaeuft, value, RelayCommand.Refresh);
    }

    /// <summary>Gibt an, ob gerade ein Update heruntergeladen oder vorbereitet wird.</summary>
    public bool UpdateWirdVorbereitet
    {
        get => _updateWirdVorbereitet;
        private set => SetProperty(ref _updateWirdVorbereitet, value, RelayCommand.Refresh);
    }

    /// <summary>Optionaler Hinweis zur letzten Update-Prüfung.</summary>
    public string? UpdateHinweis
    {
        get => _updateHinweis;
        private set => SetProperty(ref _updateHinweis, value);
    }

    /// <summary>Tooltip der Prüfen-Schaltfläche; begründet die Deaktivierung im Modus „Aus".</summary>
    public string UpdatePruefenTooltip
        => _aktuelleUpdateEinstellungen is { Modus: UpdateMode.Aus }
            ? "Update-Prüfung ist in den Einstellungen deaktiviert."
            : "Auf Programmupdate prüfen";

    /// <summary>Gibt an, ob eine manuelle Update-Prüfung aktuell gestartet werden kann.</summary>
    public bool KannPruefen()
        => _updateService is not null
           && _aktuelleUpdateEinstellungen is { Modus: not UpdateMode.Aus }
           && !UpdateCheckLaeuft
           && !UpdateWirdVorbereitet;

    /// <summary>Gibt an, ob die Installation des angebotenen Updates aktuell gestartet werden kann.</summary>
    public bool KannStarten()
        => _updateService is not null
           && _cliUpdateSafetyService is not null
           && _updateProgressDialogService is not null
           && _dialogService is not null
           && _aktuelleUpdateEinstellungen is { Modus: not UpdateMode.Aus }
           && UpdateVerfuegbar
           && !UpdateCheckLaeuft
           && !UpdateWirdVorbereitet;

    /// <summary>
    /// Übernimmt einen per Settings-UI gespeicherten Einstellungs-Snapshot: erhöht die
    /// Generation (laufende Versuche verwerfen ihr Ergebnis), bricht den Update-Ablauf ab
    /// und entfernt ein ggf. veraltetes Angebot.
    /// </summary>
    public void ApplyGespeicherteUpdateSettings(UpdateSettings settings)
    {
        var geaendert = _aktuelleUpdateEinstellungen != settings;
        _aktuelleUpdateEinstellungen = settings;

        if (geaendert)
        {
            Interlocked.Increment(ref _updateSettingsGeneration);
            CancelLaufendenUpdateAblauf();
            VerfuegbaresUpdate = null;
            UpdateVerfuegbar = false;
            UpdateHinweis = null;
        }

        OnPropertyChanged(nameof(UpdatePruefenTooltip));
        RelayCommand.Refresh();
    }

    /// <summary>Bricht einen laufenden Update-Ablauf (Vorbereitung/Start) ab.</summary>
    public void CancelLaufendenUpdateAblauf()
    {
        try
        {
            _updateAblaufCts?.Cancel();
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
        if (Interlocked.Exchange(ref _startInitialisierungErfolgt, 1) != 0)
            return;

        _versuchProtokoll?.ProtokolliereWindowReady();

        if (_updateService is null)
            return;

        await FuehreUpdateVersuchAsync("startup", vorbereitend: false, async (settings, generation, token) =>
        {
            token.ThrowIfCancellationRequested();
            var result = await _updateService.CheckForUpdateAsync(new UpdateCheckOptions(settings.IncludePrereleases), token);
            if (!IstUpdateVersuchAktuell(generation))
                return;

            ApplyUpdateCheckResult(result, isManualRefresh: false);

            if (settings.Modus == UpdateMode.BeiProgrammstartPruefenUndAusfuehren
                && result.Status == UpdateCheckStatus.UpdateVerfuegbar
                && result.Update is not null)
            {
                await InstalliereUpdateAsync(result.Update, settings, generation, token);
            }
        }, ct);
    }

    /// <summary>Prüft manuell auf Programmupdates und aktualisiert das Angebot bzw. den Hinweis.</summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public Task PruefenAsync(CancellationToken ct)
    {
        if (_updateService is null)
            return Task.CompletedTask;

        return FuehreUpdateVersuchAsync("pruefen", vorbereitend: false, async (settings, generation, token) =>
        {
            token.ThrowIfCancellationRequested();
            var result = await _updateService.CheckForUpdateAsync(new UpdateCheckOptions(settings.IncludePrereleases), token);
            if (!IstUpdateVersuchAktuell(generation))
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
    public Task StartenAsync(CancellationToken ct)
    {
        if (_updateService is null || _cliUpdateSafetyService is null || _updateProgressDialogService is null || _dialogService is null)
            return Task.CompletedTask;

        return FuehreUpdateVersuchAsync("starten", vorbereitend: true, async (settings, generation, token) =>
        {
            token.ThrowIfCancellationRequested();
            var checkResult = await _updateService.CheckForUpdateAsync(new UpdateCheckOptions(settings.IncludePrereleases), token);
            if (!IstUpdateVersuchAktuell(generation))
                return;

            ApplyUpdateCheckResult(checkResult, isManualRefresh: false);
            if (checkResult.Status != UpdateCheckStatus.UpdateVerfuegbar || checkResult.Update is null)
                return;

            await InstalliereUpdateAsync(checkResult.Update, settings, generation, token);
        }, ct);
    }

    /// <summary>
    /// Gemeinsames Gerüst eines Updateversuchs: Gate-Akquise, Versuchsprotokoll,
    /// Busy-Flag, Generations-Snapshot, Settings-Read und der Aus-Guard. Der
    /// versuchspezifische Kern läuft als <paramref name="kern"/>.
    /// </summary>
    private async Task FuehreUpdateVersuchAsync(
        string versuchArt,
        bool vorbereitend,
        Func<UpdateSettings, int, CancellationToken, Task> kern,
        CancellationToken ct)
    {
        if (!await _updateGate.WaitAsync(0))
            return;

        _versuchProtokoll?.BeginneVersuch(versuchArt);
        if (vorbereitend)
            UpdateWirdVorbereitet = true;
        else
            UpdateCheckLaeuft = true;
        UpdateHinweis = null;
        var generation = Volatile.Read(ref _updateSettingsGeneration);
        try
        {
            var settings = await LeseUpdateSettingsAsync(ct);
            if (settings is null)
            {
                UpdateEinstellungenLesefehlerAnzeigen();
                return;
            }

            if (settings.Modus == UpdateMode.Aus)
            {
                UpdateAngebotEntfernen();
                return;
            }

            await kern(settings, generation, ct);
        }
        catch (OperationCanceledException)
        {
            // Abbruch beim Schließen oder durch geänderte Einstellungen.
        }
        finally
        {
            if (vorbereitend)
                UpdateWirdVorbereitet = false;
            else
                UpdateCheckLaeuft = false;
            // BeendeVersuch muss vor der Gate-Freigabe liegen: Sonst kann der nächste Versuch
            // beginnen, bevor dieser Versuch AktuellerVersuch zurückgesetzt hat.
            _versuchProtokoll?.BeendeVersuch(versuchArt, UpdateVerfuegbar, UpdateHinweis);
            _updateGate.Release();
        }
    }

    private async Task InstalliereUpdateAsync(
        UpdateInfo update,
        UpdateSettings snapshot,
        int generation,
        CancellationToken ct)
    {
        UpdateWirdVorbereitet = true;
        using var updateCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _updateAblaufCts = updateCts;
        var progressViewModel = new UpdateProgressViewModel(updateCts.Cancel);
        try
        {
            var safety = await _cliUpdateSafetyService!.CheckAsync(updateCts.Token);
            if (!IstUpdateVersuchAktuell(generation))
                return;

            updateCts.Token.ThrowIfCancellationRequested();
            if (safety.RequiresConfirmation && !_dialogService!.BestaetigenDialog(BuildSafetyMessage(safety), "Update starten?"))
                return;

            var progress = new Progress<UpdatePreparationProgress>(progressViewModel.Apply);
            _updateProgressDialogService!.Show(progressViewModel);

            // Aktualitätsnachweis unmittelbar vor der Vorbereitung: gespeicherte Werte erneut lesen.
            if (!await PruefeSettingsAktualitaetAsync(snapshot, generation, progressViewModel, updateCts.Token))
                return;

            updateCts.Token.ThrowIfCancellationRequested();
            var preparation = await _updateService!.PrepareUpdateAsync(update, progress, updateCts.Token);
            if (!IstUpdateVersuchAktuell(generation))
                return;

            updateCts.Token.ThrowIfCancellationRequested();

            // Letzter Aktualitätsnachweis unmittelbar vor dem Updater-Start.
            if (!await PruefeSettingsAktualitaetAsync(snapshot, generation, progressViewModel, updateCts.Token))
                return;

            updateCts.Token.ThrowIfCancellationRequested();
            progressViewModel.MarkUpdaterStarting();
            await _updateService.StartPreparedUpdateAsync(preparation, updateCts.Token);
        }
        catch (OperationCanceledException)
        {
            UpdateHinweis = "Update-Vorbereitung wurde abgebrochen.";
            progressViewModel.SetError("Update-Vorbereitung wurde abgebrochen.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update konnte nicht vorbereitet oder gestartet werden.");
            progressViewModel.SetError("Update konnte nicht vorbereitet werden. Details wurden protokolliert.");
            UpdateHinweis = "Update konnte nicht vorbereitet werden.";
        }
        finally
        {
            _updateAblaufCts = null;
            UpdateWirdVorbereitet = false;
        }
    }

    /// <summary>
    /// Aktualitätsnachweis vor einem kritischen Installationsschritt: liest die gespeicherten
    /// Werte erneut und bricht ab, wenn sie nicht lesbar sind oder seit dem Snapshot geändert
    /// wurden bzw. die Settings-Generation inzwischen weitergelaufen ist.
    /// </summary>
    private async Task<bool> PruefeSettingsAktualitaetAsync(
        UpdateSettings snapshot,
        int generation,
        UpdateProgressViewModel progressViewModel,
        CancellationToken ct)
    {
        var aktuell = await LeseUpdateSettingsAsync(ct);
        if (aktuell is null)
        {
            UpdateEinstellungenLesefehlerAnzeigen();
            progressViewModel.SetError(UpdateEinstellungenNichtLesbarHinweis);
            return false;
        }

        if (aktuell != snapshot || !IstUpdateVersuchAktuell(generation))
        {
            UpdateAngebotEntfernen();
            progressViewModel.SetError(UpdateEinstellungenGeaendertHinweis);
            return false;
        }

        return true;
    }

    private async Task<UpdateSettings?> LeseUpdateSettingsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var einstellungService = scope.ServiceProvider.GetRequiredService<AppEinstellungService>();
            var settings = await einstellungService.GetUpdateSettingsAsync(ct);
            _aktuelleUpdateEinstellungen = settings;
            OnPropertyChanged(nameof(UpdatePruefenTooltip));
            return settings;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update-Einstellungen konnten nicht gelesen werden.");
            return null;
        }
    }

    private bool IstUpdateVersuchAktuell(int generation)
        => Volatile.Read(ref _updateSettingsGeneration) == generation;

    private void UpdateEinstellungenLesefehlerAnzeigen()
    {
        UpdateAngebotEntfernen();
        UpdateHinweis = UpdateEinstellungenNichtLesbarHinweis;
    }

    private void UpdateAngebotEntfernen()
    {
        VerfuegbaresUpdate = null;
        UpdateVerfuegbar = false;
    }

    private void ApplyUpdateCheckResult(UpdateCheckResult result, bool isManualRefresh)
    {
        if (result.Status == UpdateCheckStatus.UpdateVerfuegbar && result.Update is not null)
        {
            VerfuegbaresUpdate = result.Update;
            UpdateVerfuegbar = true;
            UpdateHinweis = null;
            return;
        }

        VerfuegbaresUpdate = null;
        UpdateVerfuegbar = false;
        UpdateHinweis = result.Status == UpdateCheckStatus.NichtPruefbar || isManualRefresh
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
