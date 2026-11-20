using Microsoft.Extensions.Logging;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;

namespace Softwareschmiede.App.Services;

/// <summary>
/// Gemeinsame Bindungslogik für den persistierten Anzeigemodus einer Aufgabenliste:
/// hält den aktuellen Wert, persistiert Anwender-Änderungen über den
/// <see cref="WorkItemListModeService"/> und stellt den gespeicherten Modus beim Start
/// wieder her, ohne ihn zurückzuschreiben. Wird von <see cref="MainWindowViewModel"/> und
/// <see cref="DashboardViewModel"/> delegiert benutzt, damit beide ViewModels die
/// Race-Guard-/Write-Back-freie Logik nicht duplizieren.
/// </summary>
public sealed class WorkItemListModeBinding
{
    private readonly Func<CancellationToken, Task<WorkItemListMode>>? _loadModeAsync;
    private readonly Func<WorkItemListMode, CancellationToken, Task>? _saveModeAsync;
    private readonly ILogger _logger;
    private readonly string _logContext;
    private readonly Action<Action> _dispatch;

    private WorkItemListMode _value = WorkItemListMode.Date;
    private bool _changed;

    /// <inheritdoc cref="WorkItemListModeBinding"/>
    /// <param name="loadModeAsync">
    /// Delegat zum Laden des gespeicherten Modus (z. B. <c>WorkItemListModeService.LoadMenuModeAsync</c>);
    /// <c>null</c> deaktiviert die Wiederherstellung.
    /// </param>
    /// <param name="saveModeAsync">
    /// Delegat zum Speichern des Modus (z. B. <c>WorkItemListModeService.SaveMenuModeAsync</c>);
    /// <c>null</c> deaktiviert die Persistenz.
    /// </param>
    /// <param name="logger">Logger für die Fire-and-Forget-Kontexte.</param>
    /// <param name="logContext">Kontextpräfix für die Fire-and-Forget-Protokollierung (z. B. "Aufgabenlisten-Modus").</param>
    /// <param name="dispatch">
    /// Optionaler Dispatcher für die Wiederherstellung (z. B. UI-Thread des Hauptfensters);
    /// <c>null</c> führt die Wiederherstellung direkt auf dem Fortsetzungskontext aus.
    /// </param>
    public WorkItemListModeBinding(
        Func<CancellationToken, Task<WorkItemListMode>>? loadModeAsync,
        Func<WorkItemListMode, CancellationToken, Task>? saveModeAsync,
        ILogger logger,
        string logContext,
        Action<Action>? dispatch = null)
    {
        _loadModeAsync = loadModeAsync;
        _saveModeAsync = saveModeAsync;
        _logger = logger;
        _logContext = logContext;
        _dispatch = dispatch ?? (a => a());
    }

    /// <summary>Erstellt die Bindung für die Seitenleisten-Aufgabenliste (Menü-Schlüssel).</summary>
    /// <param name="modeService">Der Persistenzdienst; <c>null</c> deaktiviert Laden und Speichern.</param>
    /// <param name="logger">Logger für die Fire-and-Forget-Kontexte.</param>
    /// <param name="dispatch">Optionaler Dispatcher für die Wiederherstellung (z. B. UI-Thread).</param>
    /// <returns>Die konfigurierte Bindung.</returns>
    public static WorkItemListModeBinding ForMenu(
        WorkItemListModeService? modeService,
        ILogger logger,
        Action<Action>? dispatch = null)
    {
        Func<CancellationToken, Task<WorkItemListMode>>? load =
            modeService is null ? null : modeService.LoadMenuModeAsync;
        Func<WorkItemListMode, CancellationToken, Task>? save =
            modeService is null ? null : modeService.SaveMenuModeAsync;
        return new WorkItemListModeBinding(load, save, logger, "Aufgabenlisten-Modus", dispatch);
    }

    /// <summary>Erstellt die Bindung für die Dashboard-Aufgabenliste (Dashboard-Schlüssel).</summary>
    /// <param name="modeService">Der Persistenzdienst; <c>null</c> deaktiviert Laden und Speichern.</param>
    /// <param name="logger">Logger für die Fire-and-Forget-Kontexte.</param>
    /// <returns>Die konfigurierte Bindung.</returns>
    public static WorkItemListModeBinding ForDashboard(
        WorkItemListModeService? modeService,
        ILogger logger)
    {
        Func<CancellationToken, Task<WorkItemListMode>>? load =
            modeService is null ? null : modeService.LoadDashboardModeAsync;
        Func<WorkItemListMode, CancellationToken, Task>? save =
            modeService is null ? null : modeService.SaveDashboardModeAsync;
        return new WorkItemListModeBinding(load, save, logger, "Dashboard-Aufgabenlisten-Modus");
    }

    /// <summary>
    /// Wird nach jeder Wertänderung und nach der Wiederherstellung ausgelöst, damit das
    /// ViewModel <c>OnPropertyChanged</c> für seine gebundene Modus-Property melden kann.
    /// </summary>
    public event Action? ValueChanged;

    /// <summary>
    /// Aktueller Anzeigemodus. Das Setzen löst <see cref="ValueChanged"/> aus und persistiert
    /// den Wert per Fire-and-Forget; ein so geänderter Wert wird von einer noch laufenden
    /// Wiederherstellung nicht mehr überschrieben.
    /// </summary>
    public WorkItemListMode Value
    {
        get => _value;
        set
        {
            if (_value == value)
                return;

            _value = value;
            _changed = true;
            ValueChanged?.Invoke();
            if (_saveModeAsync is not null)
            {
                _saveModeAsync(value, CancellationToken.None)
                    .SafeFireAndForget(_logger, $"{_logContext} speichern");
            }
        }
    }

    /// <summary>
    /// Lädt den persistierten Modus und wendet ihn an, ohne den Wert zurückzuschreiben.
    /// Ein zwischenzeitlich gesetzter Wert (Race-Guard <see cref="_changed"/>, geprüft vor
    /// und nach dem <c>await</c>) wird nicht überschrieben.
    /// </summary>
    /// <param name="ct">Token zum Abbrechen der Operation.</param>
    public async Task LoadSavedValueAsync(CancellationToken ct = default)
    {
        if (_loadModeAsync is null || _changed)
            return;

        var mode = await _loadModeAsync(ct);
        _dispatch(() =>
        {
            if (_changed)
                return;

            // Wiederherstellung ohne Write-Back: nur Wert + Benachrichtigung,
            // damit der Start keinen Persistenz-Schreibzugriff auslöst.
            _value = mode;
            ValueChanged?.Invoke();
        });
    }
}
