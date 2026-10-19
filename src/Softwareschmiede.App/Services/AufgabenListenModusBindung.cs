using Microsoft.Extensions.Logging;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.App.Services;

/// <summary>
/// Gemeinsame Bindungslogik für den persistierten Anzeigemodus einer Aufgabenliste:
/// hält den aktuellen Wert, persistiert Anwender-Änderungen über den
/// <see cref="AufgabenListenModusService"/> und stellt den gespeicherten Modus beim Start
/// wieder her, ohne ihn zurückzuschreiben. Wird von <see cref="MainWindowViewModel"/> und
/// <see cref="DashboardViewModel"/> delegiert benutzt, damit beide ViewModels die
/// Race-Guard-/Write-Back-freie Logik nicht duplizieren.
/// </summary>
public sealed class AufgabenListenModusBindung
{
    private readonly Func<CancellationToken, Task<AufgabenListenModus>>? _ladeModusAsync;
    private readonly Func<AufgabenListenModus, CancellationToken, Task>? _speichereModusAsync;
    private readonly ILogger _logger;
    private readonly string _logKontext;
    private readonly Action<Action> _dispatch;

    private AufgabenListenModus _wert = AufgabenListenModus.Datum;
    private bool _geaendert;

    /// <inheritdoc cref="AufgabenListenModusBindung"/>
    /// <param name="ladeModusAsync">
    /// Delegat zum Laden des gespeicherten Modus (z. B. <c>AufgabenListenModusService.LadeMenueModusAsync</c>);
    /// <c>null</c> deaktiviert die Wiederherstellung.
    /// </param>
    /// <param name="speichereModusAsync">
    /// Delegat zum Speichern des Modus (z. B. <c>AufgabenListenModusService.SpeichereMenueModusAsync</c>);
    /// <c>null</c> deaktiviert die Persistenz.
    /// </param>
    /// <param name="logger">Logger für die Fire-and-Forget-Kontexte.</param>
    /// <param name="logKontext">Kontextpräfix für die Fire-and-Forget-Protokollierung (z. B. "Aufgabenlisten-Modus").</param>
    /// <param name="dispatch">
    /// Optionaler Dispatcher für die Wiederherstellung (z. B. UI-Thread des Hauptfensters);
    /// <c>null</c> führt die Wiederherstellung direkt auf dem Fortsetzungskontext aus.
    /// </param>
    public AufgabenListenModusBindung(
        Func<CancellationToken, Task<AufgabenListenModus>>? ladeModusAsync,
        Func<AufgabenListenModus, CancellationToken, Task>? speichereModusAsync,
        ILogger logger,
        string logKontext,
        Action<Action>? dispatch = null)
    {
        _ladeModusAsync = ladeModusAsync;
        _speichereModusAsync = speichereModusAsync;
        _logger = logger;
        _logKontext = logKontext;
        _dispatch = dispatch ?? (a => a());
    }

    /// <summary>Erstellt die Bindung für die Seitenleisten-Aufgabenliste (Menü-Schlüssel).</summary>
    /// <param name="modusService">Der Persistenzdienst; <c>null</c> deaktiviert Laden und Speichern.</param>
    /// <param name="logger">Logger für die Fire-and-Forget-Kontexte.</param>
    /// <param name="dispatch">Optionaler Dispatcher für die Wiederherstellung (z. B. UI-Thread).</param>
    /// <returns>Die konfigurierte Bindung.</returns>
    public static AufgabenListenModusBindung FuerMenue(
        AufgabenListenModusService? modusService,
        ILogger logger,
        Action<Action>? dispatch = null)
    {
        Func<CancellationToken, Task<AufgabenListenModus>>? lade =
            modusService is null ? null : modusService.LadeMenueModusAsync;
        Func<AufgabenListenModus, CancellationToken, Task>? speichere =
            modusService is null ? null : modusService.SpeichereMenueModusAsync;
        return new AufgabenListenModusBindung(lade, speichere, logger, "Aufgabenlisten-Modus", dispatch);
    }

    /// <summary>Erstellt die Bindung für die Dashboard-Aufgabenliste (Dashboard-Schlüssel).</summary>
    /// <param name="modusService">Der Persistenzdienst; <c>null</c> deaktiviert Laden und Speichern.</param>
    /// <param name="logger">Logger für die Fire-and-Forget-Kontexte.</param>
    /// <returns>Die konfigurierte Bindung.</returns>
    public static AufgabenListenModusBindung FuerDashboard(
        AufgabenListenModusService? modusService,
        ILogger logger)
    {
        Func<CancellationToken, Task<AufgabenListenModus>>? lade =
            modusService is null ? null : modusService.LadeDashboardModusAsync;
        Func<AufgabenListenModus, CancellationToken, Task>? speichere =
            modusService is null ? null : modusService.SpeichereDashboardModusAsync;
        return new AufgabenListenModusBindung(lade, speichere, logger, "Dashboard-Aufgabenlisten-Modus");
    }

    /// <summary>
    /// Wird nach jeder Wertänderung und nach der Wiederherstellung ausgelöst, damit das
    /// ViewModel <c>OnPropertyChanged</c> für seine gebundene Modus-Property melden kann.
    /// </summary>
    public event Action? WertGeaendert;

    /// <summary>
    /// Aktueller Anzeigemodus. Das Setzen löst <see cref="WertGeaendert"/> aus und persistiert
    /// den Wert per Fire-and-Forget; ein so geänderter Wert wird von einer noch laufenden
    /// Wiederherstellung nicht mehr überschrieben.
    /// </summary>
    public AufgabenListenModus Wert
    {
        get => _wert;
        set
        {
            if (_wert == value)
                return;

            _wert = value;
            _geaendert = true;
            WertGeaendert?.Invoke();
            if (_speichereModusAsync is not null)
            {
                _speichereModusAsync(value, CancellationToken.None)
                    .SafeFireAndForget(_logger, $"{_logKontext} speichern");
            }
        }
    }

    /// <summary>
    /// Lädt den persistierten Modus und wendet ihn an, ohne den Wert zurückzuschreiben.
    /// Ein zwischenzeitlich gesetzter Wert (Race-Guard <see cref="_geaendert"/>, geprüft vor
    /// und nach dem <c>await</c>) wird nicht überschrieben.
    /// </summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public async Task GespeichertenWertLadenAsync(CancellationToken ct = default)
    {
        if (_ladeModusAsync is null || _geaendert)
            return;

        var modus = await _ladeModusAsync(ct);
        _dispatch(() =>
        {
            if (_geaendert)
                return;

            // Wiederherstellung ohne Write-Back: nur Wert + Benachrichtigung,
            // damit der Start keinen Persistenz-Schreibzugriff auslöst.
            _wert = modus;
            WertGeaendert?.Invoke();
        });
    }
}
