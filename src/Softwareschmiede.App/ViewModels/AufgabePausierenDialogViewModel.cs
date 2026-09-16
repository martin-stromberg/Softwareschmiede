using System.Windows.Input;
using Softwareschmiede.App.Services;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// ViewModel für den Dialog zum Einstellen oder Aufheben einer Aufgaben-Pause.
/// Bietet Datums-/Zeiteingabe (lokal), eine Anzeige der aktuell gesetzten Pause und
/// das Ergebnis als <see cref="AufgabePausierenErgebnis"/> (UTC-Zeitpunkt oder Aufheben).
/// </summary>
public sealed class AufgabePausierenDialogViewModel : ViewModelBase
{
    private readonly TimeProvider _timeProvider;
    private DateTime? _pausiertDatum;
    private int? _pausiertStunde;
    private int? _pausiertMinute;
    private string? _validierungsFehler;
    private DateTimeOffset? _aktuellePauseUtc;

    /// <inheritdoc cref="AufgabePausierenDialogViewModel"/>
    public AufgabePausierenDialogViewModel(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;

        SetzeStandardVorbelegung();

        BestaetigenCommand = new RelayCommand(Bestaetigen, () => KannBestaetigen);
        AufhebenCommand = new RelayCommand(Aufheben, () => IstAktuellPausiert);
        AbbrechenCommand = new RelayCommand(Abbrechen);

        AktualisiereValidierung();
    }

    /// <summary>Wird ausgelöst wenn der Dialog geschlossen werden soll. Parameter: true = bestätigt, false = abgebrochen.</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Gewähltes Datum des Pause-Endes (lokale Zeitzone).</summary>
    public DateTime? PausiertDatum
    {
        get => _pausiertDatum;
        set
        {
            if (SetProperty(ref _pausiertDatum, value))
                AktualisiereValidierung();
        }
    }

    /// <summary>Gewählte Stunde des Pause-Endes (0-23, lokale Zeitzone).</summary>
    public int? PausiertStunde
    {
        get => _pausiertStunde;
        set
        {
            if (SetProperty(ref _pausiertStunde, value))
                AktualisiereValidierung();
        }
    }

    /// <summary>Gewählte Minute des Pause-Endes (0-59, lokale Zeitzone).</summary>
    public int? PausiertMinute
    {
        get => _pausiertMinute;
        set
        {
            if (SetProperty(ref _pausiertMinute, value))
                AktualisiereValidierung();
        }
    }

    /// <summary>Anzeigetext der aktuell gesetzten Pause, oder null wenn keine Pause aktiv ist (Ende liegt in der Zukunft).</summary>
    public string? AktuellePauseAnzeige
        => _aktuellePauseUtc is { } pause && pause > _timeProvider.GetUtcNow()
            ? $"Aktuell pausiert bis {TimeZoneInfo.ConvertTime(pause, _timeProvider.LocalTimeZone):dd.MM.yyyy HH:mm}"
            : null;

    /// <summary>true, wenn aktuell eine aktive Pause gesetzt ist (Ende liegt in der Zukunft).</summary>
    public bool IstAktuellPausiert => _aktuellePauseUtc > _timeProvider.GetUtcNow();

    /// <summary>Aktueller Validierungsfehler der Eingabe, oder null wenn die Eingabe gültig ist.</summary>
    public string? ValidierungsFehler
    {
        get => _validierungsFehler;
        private set => SetProperty(ref _validierungsFehler, value);
    }

    /// <summary>true, wenn die Eingabe gültig ist und übernommen werden kann.</summary>
    public bool KannBestaetigen => ValidierungsFehler is null;

    /// <summary>Übernimmt die gewählte Pause und schließt den Dialog.</summary>
    public ICommand BestaetigenCommand { get; }

    /// <summary>Hebt eine bestehende Pause auf und schließt den Dialog.</summary>
    public ICommand AufhebenCommand { get; }

    /// <summary>Bricht den Dialog ohne Änderung ab.</summary>
    public ICommand AbbrechenCommand { get; }

    /// <summary>Das Dialogergebnis; null solange der Anwender nicht bestätigt/aufgehoben hat.</summary>
    public AufgabePausierenErgebnis? Ergebnis { get; private set; }

    /// <summary>Initialisiert den Dialog mit der aktuell gesetzten Pause der Aufgabe.</summary>
    /// <param name="aktuellePauseUtc">Der aktuell persistierte Pause-Endzeitpunkt (UTC), oder null.</param>
    public void Initialize(DateTimeOffset? aktuellePauseUtc)
    {
        _aktuellePauseUtc = aktuellePauseUtc;

        if (aktuellePauseUtc is { } pause && pause > _timeProvider.GetUtcNow())
        {
            var lokal = TimeZoneInfo.ConvertTime(pause, _timeProvider.LocalTimeZone).DateTime;
            _pausiertDatum = lokal.Date;
            _pausiertStunde = lokal.Hour;
            _pausiertMinute = lokal.Minute;
        }
        else
        {
            // Abgelaufene oder fehlende Pause: Standardvorbelegung statt vergangenem Zeitstempel.
            SetzeStandardVorbelegung();
        }

        OnPropertyChanged(nameof(PausiertDatum));
        OnPropertyChanged(nameof(PausiertStunde));
        OnPropertyChanged(nameof(PausiertMinute));
        OnPropertyChanged(nameof(AktuellePauseAnzeige));
        OnPropertyChanged(nameof(IstAktuellPausiert));
        AktualisiereValidierung();
    }

    /// <summary>
    /// Vorbelegung „aktueller Zeitpunkt": Da die Eingabefelder nur volle Minuten erfassen, ist die
    /// nächste volle Minute der nächstgelegene gültige Zeitpunkt — die laufende Minute läge durch den
    /// Sekunden-Abschnitt bereits in der Vergangenheit und erzeugte sofort einen Validierungsfehler.
    /// </summary>
    private void SetzeStandardVorbelegung()
    {
        var vorbelegung = _timeProvider.GetLocalNow().AddMinutes(1);
        _pausiertDatum = vorbelegung.Date;
        _pausiertStunde = vorbelegung.Hour;
        _pausiertMinute = vorbelegung.Minute;
    }

    private void AktualisiereValidierung()
    {
        ValidierungsFehler = BerechneValidierungsFehler();
        OnPropertyChanged(nameof(KannBestaetigen));
    }

    /// <summary>Ermittelt den gewählten Pause-Endzeitpunkt in UTC, oder null bei ungültiger Eingabe.</summary>
    public DateTimeOffset? BerechnePausiertBisUtc()
    {
        if (_pausiertDatum is not { } datum || _pausiertStunde is not { } stunde || _pausiertMinute is not { } minute)
            return null;

        if (stunde is < 0 or > 23 || minute is < 0 or > 59)
            return null;

        // Die Eingabefelder werden über GetLocalNow() vorbelegt und folgen daher der Zeitzone des
        // TimeProviders — nicht zwingend der Maschinen-Zone (DateTimeKind.Local). Die Interpretation
        // muss dieselbe Quelle nutzen, sonst wäre die Vorbelegung bei abweichender Zone ungültig.
        var lokal = new DateTime(datum.Year, datum.Month, datum.Day, stunde, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(lokal, _timeProvider.LocalTimeZone.GetUtcOffset(lokal)).ToUniversalTime();
    }

    private string? BerechneValidierungsFehler()
    {
        if (_pausiertDatum is null)
            return "Bitte ein Datum wählen.";

        if (_pausiertStunde is not { } stunde || stunde is < 0 or > 23)
            return "Stunde muss zwischen 0 und 23 liegen.";

        if (_pausiertMinute is not { } minute || minute is < 0 or > 59)
            return "Minute muss zwischen 0 und 59 liegen.";

        var pausiertBisUtc = BerechnePausiertBisUtc();
        if (pausiertBisUtc is null || pausiertBisUtc <= _timeProvider.GetUtcNow())
            return "Der Zeitpunkt muss in der Zukunft liegen.";

        return null;
    }

    private void Bestaetigen()
    {
        var pausiertBisUtc = BerechnePausiertBisUtc();
        if (pausiertBisUtc is null)
            return;

        Ergebnis = new AufgabePausierenErgebnis(pausiertBisUtc, Aufheben: false);
        CloseRequested?.Invoke(this, true);
    }

    private void Aufheben()
    {
        Ergebnis = new AufgabePausierenErgebnis(null, Aufheben: true);
        CloseRequested?.Invoke(this, true);
    }

    private void Abbrechen() => CloseRequested?.Invoke(this, false);
}
