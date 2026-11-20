using System.Windows.Input;
using Softwareschmiede.App.Services;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// ViewModel für den Dialog zum Einstellen oder Aufheben einer Aufgaben-Pause.
/// Bietet Datums-/Zeiteingabe (lokal), eine Anzeige der aktuell gesetzten Pause und
/// das Ergebnis als <see cref="PauseWorkItemResult"/> (UTC-Zeitpunkt oder Aufheben).
/// </summary>
public sealed class PauseWorkItemDialogViewModel : ViewModelBase
{
    private readonly TimeProvider _timeProvider;
    private DateTime? _pausedDate;
    private int? _pausedHour;
    private int? _pausedMinute;
    private string? _validationError;
    private DateTimeOffset? _currentPauseUtc;

    /// <inheritdoc cref="PauseWorkItemDialogViewModel"/>
    public PauseWorkItemDialogViewModel(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;

        SetDefaultPrefill();

        ConfirmCommand = new RelayCommand(Confirm, () => CanConfirm);
        AufhebenCommand = new RelayCommand(Aufheben, () => IsCurrentlyPaused);
        CancelCommand = new RelayCommand(Cancel);

        UpdateValidation();
    }

    /// <summary>Wird ausgelöst wenn der Dialog geschlossen werden soll. Parameter: true = bestätigt, false = abgebrochen.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Gewähltes Datum des Pause-Endes (lokale Zeitzone).</summary>
    public DateTime? PausedDate
    {
        get => _pausedDate;
        set
        {
            if (SetProperty(ref _pausedDate, value))
                UpdateValidation();
        }
    }

    /// <summary>Gewählte Stunde des Pause-Endes (0-23, lokale Zeitzone).</summary>
    public int? PausedHour
    {
        get => _pausedHour;
        set
        {
            if (SetProperty(ref _pausedHour, value))
                UpdateValidation();
        }
    }

    /// <summary>Gewählte Minute des Pause-Endes (0-59, lokale Zeitzone).</summary>
    public int? PausedMinute
    {
        get => _pausedMinute;
        set
        {
            if (SetProperty(ref _pausedMinute, value))
                UpdateValidation();
        }
    }

    /// <summary>Anzeigetext der aktuell gesetzten Pause, oder null wenn keine Pause aktiv ist (Ende liegt in der Zukunft).</summary>
    public string? CurrentPauseDisplay
        => _currentPauseUtc is { } pause && pause > _timeProvider.GetUtcNow()
            ? $"Aktuell pausiert bis {TimeZoneInfo.ConvertTime(pause, _timeProvider.LocalTimeZone):dd.MM.yyyy HH:mm}"
            : null;

    /// <summary>true, wenn aktuell eine aktive Pause gesetzt ist (Ende liegt in der Zukunft).</summary>
    public bool IsCurrentlyPaused => _currentPauseUtc > _timeProvider.GetUtcNow();

    /// <summary>Aktueller Validierungsfehler der Eingabe, oder null wenn die Eingabe gültig ist.</summary>
    public string? ValidationError
    {
        get => _validationError;
        private set => SetProperty(ref _validationError, value);
    }

    /// <summary>true, wenn die Eingabe gültig ist und übernommen werden kann.</summary>
    public bool CanConfirm => ValidationError is null;

    /// <summary>Übernimmt die gewählte Pause und schließt den Dialog.</summary>
    public ICommand ConfirmCommand { get; }

    /// <summary>Hebt eine bestehende Pause auf und schließt den Dialog.</summary>
    public ICommand AufhebenCommand { get; }

    /// <summary>Bricht den Dialog ohne Änderung ab.</summary>
    public ICommand CancelCommand { get; }

    /// <summary>Das Dialogergebnis; null solange der Anwender nicht bestätigt/aufgehoben hat.</summary>
    public PauseWorkItemResult? Result { get; private set; }

    /// <summary>Initialisiert den Dialog mit der aktuell gesetzten Pause der Aufgabe.</summary>
    /// <param name="currentPauseUtc">Der aktuell persistierte Pause-Endzeitpunkt (UTC), oder null.</param>
    public void Initialize(DateTimeOffset? currentPauseUtc)
    {
        _currentPauseUtc = currentPauseUtc;

        if (currentPauseUtc is { } pause && pause > _timeProvider.GetUtcNow())
        {
            var local = TimeZoneInfo.ConvertTime(pause, _timeProvider.LocalTimeZone).DateTime;
            _pausedDate = local.Date;
            _pausedHour = local.Hour;
            _pausedMinute = local.Minute;
        }
        else
        {
            // Abgelaufene oder fehlende Pause: Standardvorbelegung statt vergangenem Zeitstempel.
            SetDefaultPrefill();
        }

        OnPropertyChanged(nameof(PausedDate));
        OnPropertyChanged(nameof(PausedHour));
        OnPropertyChanged(nameof(PausedMinute));
        OnPropertyChanged(nameof(CurrentPauseDisplay));
        OnPropertyChanged(nameof(IsCurrentlyPaused));
        UpdateValidation();
    }

    /// <summary>
    /// Vorbelegung „aktueller Zeitpunkt": Da die Eingabefelder nur volle Minuten erfassen, ist die
    /// nächste volle Minute der nächstgelegene gültige Zeitpunkt — die laufende Minute läge durch den
    /// Sekunden-Abschnitt bereits in der Vergangenheit und erzeugte sofort einen Validierungsfehler.
    /// </summary>
    private void SetDefaultPrefill()
    {
        var prefill = _timeProvider.GetLocalNow().AddMinutes(1);
        _pausedDate = prefill.Date;
        _pausedHour = prefill.Hour;
        _pausedMinute = prefill.Minute;
    }

    private void UpdateValidation()
    {
        ValidationError = CalculateValidationError();
        OnPropertyChanged(nameof(CanConfirm));
    }

    /// <summary>Ermittelt den gewählten Pause-Endzeitpunkt in UTC, oder null bei ungültiger Eingabe.</summary>
    public DateTimeOffset? CalculatePausedUntilUtc()
    {
        if (_pausedDate is not { } date || _pausedHour is not { } hour || _pausedMinute is not { } minute)
            return null;

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
            return null;

        // Die Eingabefelder werden über GetLocalNow() vorbelegt und folgen daher der Zeitzone des
        // TimeProviders — nicht zwingend der Maschinen-Zone (DateTimeKind.Local). Die Interpretation
        // muss dieselbe Quelle nutzen, sonst wäre die Vorbelegung bei abweichender Zone ungültig.
        var local = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, _timeProvider.LocalTimeZone.GetUtcOffset(local)).ToUniversalTime();
    }

    private string? CalculateValidationError()
    {
        if (_pausedDate is null)
            return "Bitte ein Datum wählen.";

        if (_pausedHour is not { } hour || hour is < 0 or > 23)
            return "Stunde muss zwischen 0 und 23 liegen.";

        if (_pausedMinute is not { } minute || minute is < 0 or > 59)
            return "Minute muss zwischen 0 und 59 liegen.";

        var pausedUntilUtc = CalculatePausedUntilUtc();
        if (pausedUntilUtc is null || pausedUntilUtc <= _timeProvider.GetUtcNow())
            return "Der Zeitpunkt muss in der Zukunft liegen.";

        return null;
    }

    private void Confirm()
    {
        var pausedUntilUtc = CalculatePausedUntilUtc();
        if (pausedUntilUtc is null)
            return;

        Result = new PauseWorkItemResult(pausedUntilUtc, Aufheben: false);
        CloseRequested?.Invoke(this, true);
    }

    private void Aufheben()
    {
        Result = new PauseWorkItemResult(null, Aufheben: true);
        CloseRequested?.Invoke(this, true);
    }

    private void Cancel() => CloseRequested?.Invoke(this, false);
}
