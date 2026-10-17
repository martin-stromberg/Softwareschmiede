using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.App.Extensions;
using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.App.ViewModels;

/// <summary>Persistente SCM-Queue mit ausschließlich expliziten, rungebundenen Benutzeraktionen.</summary>
public sealed class ScmEventQueueViewModel : ViewModelBase, IDisposable
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ScmEventChangeNotifier _notifier;
    private readonly IDialogService _dialogs;
    private readonly ILogger<ScmEventQueueViewModel> _logger;
    private readonly Action<Action> _dispatch;
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private ScmEventQueueItem? _selectedEvent;
    private string _selectedFilter = "Wartend";
    private string? _message;
    private string? _diagnostics;
    private bool _isBusy;
    private bool _isLoading;
    private bool _hasLoaded;
    private string? _loadError;
    private string? _monitoringNotice;
    private bool _disposed;

    /// <summary>Initialisiert eine Ansicht ohne einen langlebigen Datenbankkontext.</summary>
    public ScmEventQueueViewModel(IServiceScopeFactory scopes, ScmEventChangeNotifier notifier, IDialogService dialogs,
        ILogger<ScmEventQueueViewModel> logger)
    {
        _scopes = scopes;
        _notifier = notifier;
        _dialogs = dialogs;
        _logger = logger;
        _dispatch = DispatcherInvokeFactory.Create(null);
        RefreshCommand = Command(async ct => await RefreshAsync(ct));
        RetrySourcesCommand = Command(async ct =>
        {
            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>().ScmObservationCheckpoints
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.NextAttemptUtc, (long?)null), ct);
            await scope.ServiceProvider.GetRequiredService<ScmEventMonitoringService>().RunOnceAsync(ct);
        });
        OpenEventCommand = Command(OpenAsync, () => SelectedEvent is { IsProcessed: false, IsBlocked: false, AttemptId: null });
        RetryCommand = Command(ct => WithEventAsync((service, item) => service.RetryAsync(item.Id, ct), ct), () => SelectedEvent is not null);
        CheckStatusCommand = Command(ct => WithEventAsync((service, item) => service.DispatchAsync(item.Id, ct: ct), ct), () => SelectedEvent?.AttemptId is not null);
        ConfirmReadyCommand = Command(ct => WithEventAsync((service, item) => service.ConfirmReadyAsync(item.Id, item.CliRunId!, ct), ct),
            () => SelectedEvent is { CanConfirmReady: true });
        ResendCommand = Command(ResendAsync, () => SelectedEvent is { CanResend: true, IsBlocked: false });
        NavigateToTaskCommand = new RelayCommand(() => { if (SelectedEvent is { } item) NavigateToTaskAction?.Invoke(item.TaskId); }, () => SelectedEvent is not null && !IsBusy);
        NavigateToSettingsCommand = new RelayCommand(() => NavigateToSettingsAction?.Invoke(), () => !IsBusy);
        BackCommand = new RelayCommand(() => BackAction?.Invoke(), () => !IsBusy);
        _notifier.Changed += OnChanged;
    }

    /// <summary>Wird vom Hauptfenster auf dessen Aufgabennavigation gesetzt.</summary>
    public Action<Guid>? NavigateToTaskAction { get; set; }
    /// <summary>Öffnet die regulären CLI-Einstellungen.</summary>
    public Action? NavigateToSettingsAction { get; set; }
    /// <summary>Führt zum Dashboard zurück.</summary>
    public Action? BackAction { get; set; }
    /// <summary>Gefilterte, dauerhafte Ereignisse.</summary>
    public ObservableCollection<ScmEventQueueItem> Events { get; } = new();
    /// <summary>Verfügbare Anzeigefilter.</summary>
    public IReadOnlyList<string> Filters { get; } = ["Wartend", "Verarbeitet", "Alle"];
    /// <summary>Reiner Anzeigefilter; löst keine Ereignisverarbeitung aus.</summary>
    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (!SetProperty(ref _selectedFilter, value)) return;
            Events.Clear();
            SelectedEvent = null;
            _hasLoaded = false;
            OnPropertyChanged(nameof(IsEmpty));
            RefreshInBackground();
        }
    }
    /// <summary>Aktueller Detaildatensatz. Das Selektieren alleine startet keine CLI.</summary>
    public ScmEventQueueItem? SelectedEvent
    {
        get => _selectedEvent;
        set { if (SetProperty(ref _selectedEvent, value)) { OnPropertyChanged(nameof(HasSelection)); CommandManager.InvalidateRequerySuggested(); } }
    }
    /// <summary>Ein Detail ist ausgewählt.</summary>
    public bool HasSelection => SelectedEvent is not null;
    /// <summary>Es gibt im aktuellen Filter keine Ereignisse.</summary>
    public bool IsEmpty => _hasLoaded && !IsLoading && LoadError is null && Events.Count == 0;
    /// <summary>Ladephase wird vom erfolgreich leeren Ergebnis unterschieden.</summary>
    public bool IsLoading { get => _isLoading; private set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(IsEmpty)); } }
    /// <summary>Ladefehler mit Wiederholungsmöglichkeit, auch bei Hintergrundaktualisierungen.</summary>
    public string? LoadError { get => _loadError; private set { SetProperty(ref _loadError, value); OnPropertyChanged(nameof(IsEmpty)); } }
    /// <summary>Sichtbare Auswirkung angehaltener Quellen außerhalb der technischen Details.</summary>
    public string? MonitoringNotice { get => _monitoringNotice; private set => SetProperty(ref _monitoringNotice, value); }
    /// <summary>Text des zum Filter passenden Leerzustands.</summary>
    public string EmptyText => SelectedFilter == "Wartend" ? "Keine wartenden SCM-Ereignisse" : "Keine SCM-Ereignisse in diesem Filter";
    /// <summary>Operation/Dialog sperrt weitere verändernde Commands.</summary>
    public bool IsBusy { get => _isBusy; private set { SetProperty(ref _isBusy, value); CommandManager.InvalidateRequerySuggested(); } }
    /// <summary>Verständliche Rückmeldung der letzten Benutzeraktion.</summary>
    public string? Message { get => _message; private set => SetProperty(ref _message, value); }
    /// <summary>Fehler aus Quellen und einzelnen Empfangs-/Antwortaktionen.</summary>
    public string? Diagnostics { get => _diagnostics; private set => SetProperty(ref _diagnostics, value); }
    /// <summary>Lädt den aktuellen Bestand.</summary>
    public AsyncRelayCommand RefreshCommand { get; }
    /// <summary>Prüft Quellen auch ohne Queue-Eintrag erneut, beispielsweise nach einer Anmeldung.</summary>
    public AsyncRelayCommand RetrySourcesCommand { get; }
    /// <summary>Öffnet die CLI-Auswahl bzw. bestätigt eine bestehende Sitzung.</summary>
    public AsyncRelayCommand OpenEventCommand { get; }
    /// <summary>Prüft gezielt Fehler und Berechtigungen erneut.</summary>
    public AsyncRelayCommand RetryCommand { get; }
    /// <summary>Gleicht den aktuellen Attempt ab, ohne ihn erneut zu senden.</summary>
    public AsyncRelayCommand CheckStatusCommand { get; }
    /// <summary>Bestätigt ausschließlich den tatsächlich angezeigten Run.</summary>
    public AsyncRelayCommand ConfirmReadyCommand { get; }
    /// <summary>Erlaubt einen bewussten Neuversand nach Warnung.</summary>
    public AsyncRelayCommand ResendCommand { get; }
    /// <summary>Navigation zur zugehörigen Aufgabe.</summary>
    public ICommand NavigateToTaskCommand { get; }
    /// <summary>Navigation zu den Einstellungen.</summary>
    public ICommand NavigateToSettingsCommand { get; }
    /// <summary>Zurück zum Dashboard.</summary>
    public ICommand BackCommand { get; }

    /// <summary>Liest Queue und Diagnosen in einem eigenen Scope und aktualisiert über den UI-Dispatcher.</summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        ct = cancellation.Token;
        await _refresh.WaitAsync(ct);
        try
        {
            _dispatch(() => IsLoading = true);
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            var filter = SelectedFilter;
            var query = db.ScmIncomingEvents.AsNoTracking();
            if (filter == "Wartend") query = query.Where(x => x.Status != "Verarbeitet");
            if (filter == "Verarbeitet") query = query.Where(x => x.Status == "Verarbeitet");
            var records = await query.OrderBy(x => x.Sequence).ToListAsync(ct);
            var taskIds = records.Select(x => x.AufgabeId).Distinct().ToArray();
            var tasks = await db.Aufgaben.AsNoTracking().Where(x => taskIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            var entries = records.Select(item => ScmEventQueueItem.From(item, tasks.GetValueOrDefault(item.AufgabeId))).ToList();
            var sourceErrors = await db.ScmObservationCheckpoints.AsNoTracking().Where(x => x.Diagnostic != null).ToListAsync(ct);
            var actionErrors = await db.ScmOutgoingActions.AsNoTracking().Where(x => x.Error != null).ToListAsync(ct);
            var diagnostic = string.Join(Environment.NewLine,
                sourceErrors.Select(x => $"Quelle {x.SourceKey}: {x.Diagnostic} ({NextCheck(x.NextAttemptUtc)})")
                    .Concat(actionErrors.Select(x => $"{ActionLabel(x.Purpose)}: {x.Error} ({NextCheck(x.NextAttemptUtc)})")));
            _dispatch(() =>
            {
                if (_disposed || SelectedFilter != filter) return;
                var selectedId = SelectedEvent?.Id;
                // Preserve unchanged rows and UI selection while background polling reports progress.
                for (var index = 0; index < entries.Count; index++)
                {
                    if (index >= Events.Count) Events.Add(entries[index]);
                    else if (Events[index] != entries[index]) Events[index] = entries[index];
                }
                while (Events.Count > entries.Count) Events.RemoveAt(Events.Count - 1);
                SelectedEvent = Events.FirstOrDefault(x => x.Id == selectedId);
                _hasLoaded = true;
                LoadError = null;
                Diagnostics = diagnostic;
                MonitoringNotice = sourceErrors.Count == 0 ? null
                    : "Die GitHub-Überwachung ist für betroffene Quellen unterbrochen; neue Anfragen können fehlen. "
                      + sourceErrors[0].Diagnostic
                      + (sourceErrors.Count > 1 ? $" ({sourceErrors.Count} betroffene Quellen; Details unter Integrationshinweise.)" : string.Empty)
                      + " Nach der Korrektur: Quellen erneut prüfen.";
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(EmptyText));
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SCM-Ereignisübersicht konnte nicht geladen werden.");
            _dispatch(() => LoadError = "SCM-Ereignisse konnten nicht geladen werden. Bitte erneut laden."
                + (Events.Count > 0 ? " Angezeigt wird der vorherige Stand; neue Änderungen fehlen möglicherweise." : string.Empty));
        }
        finally
        {
            _dispatch(() => IsLoading = false);
            _refresh.Release();
        }
    }

    private AsyncRelayCommand Command(Func<CancellationToken, Task> action, Func<bool>? allowed = null)
        => new(async ct =>
        {
            if (IsBusy || _disposed || !(allowed?.Invoke() ?? true)) return;
            IsBusy = true;
            Message = null;
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
            try { await action(cancellation.Token); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception ex)
            {
                Message = ex is InvalidOperationException or IOException or UnauthorizedAccessException
                    ? ex.Message : "Die Aktion konnte nicht abgeschlossen werden. Bitte erneut prüfen.";
                _logger.LogWarning(ex, "SCM-Queue-Aktion fehlgeschlagen.");
            }
            finally
            {
                IsBusy = false;
                if (!_disposed) await RefreshAsync(_lifetime.Token);
            }
        }, () => !IsBusy && !_disposed && (allowed?.Invoke() ?? true));

    private async Task OpenAsync(CancellationToken ct)
    {
        if (SelectedEvent is not { } selected) return;
        using var scope = _scopes.CreateScope();
        var selection = scope.ServiceProvider.GetRequiredService<PluginSelectionService>();
        await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().OpenFromQueueAsync(selected.Id, async (task, session, token) =>
        {
            if (session is not null)
            {
                var confirmed = false;
                _dispatch(() => confirmed = _dialogs.BestaetigenDialog(
                    $"An laufende CLI übergeben: {session.PluginPrefix}\nSitzung: {session.CliRunId:D}\nEine beschäftigte CLI wartet auf Eingabebereitschaft.", "SCM-Ereignis öffnen"));
                return confirmed ? session.PluginPrefix : null;
            }
            var available = await selection.GetAvailableKiPluginPrefixesAsync(token);
            if (available.Count == 0)
            {
                _dispatch(() => Message = "Keine aktivierte CLI verfügbar. Bitte in den Einstellungen eine CLI aktivieren.");
                return null;
            }
            // Absichtlich kein Single-Plugin-Kurzschluss: auch ein einziges Plugin erfordert die Auswahlbestätigung.
            var result = await _dialogs.ShowPluginSelectionDialogAsync(available, task.KiPluginPrefix, token);
            if (result.SelectedPluginPrefix is null) return null;
            if (result.SaveAsProjectDefault)
                await selection.SaveProjectDefaultPluginPrefixAsync(task.ProjektId, PluginType.DevelopmentAutomation, result.SelectedPluginPrefix, token);
            return result.SelectedPluginPrefix;
        }, ct);
    }

    private async Task WithEventAsync(Func<ScmEventOrchestrator, ScmEventQueueItem, Task> action, CancellationToken ct)
    {
        if (SelectedEvent is not { } item) return;
        using var scope = _scopes.CreateScope();
        ct.ThrowIfCancellationRequested();
        await action(scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>(), item);
        var current = await scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>().ScmIncomingEvents
            .AsNoTracking().SingleOrDefaultAsync(x => x.Id == item.Id, ct);
        if (current is not null)
            Message = current.Status == item.Status
                ? "Prüfung abgeschlossen. Status unverändert: " + item.StatusText + "."
                : "Prüfung abgeschlossen. Der Status wurde aktualisiert.";
    }

    private async Task ResendAsync(CancellationToken ct)
    {
        if (SelectedEvent is not { CanResend: true, AttemptId: { } attempt } item) return;
        if (!_dialogs.BestaetigenDialog("Der vorherige Versuch könnte bereits verarbeitet worden sein. Neu senden kann eine Doppelbearbeitung und doppelte Antworten verursachen. Trotzdem neu senden?", "Mögliche Doppelbearbeitung")) return;
        using var scope = _scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().ResendAsync(item.Id, attempt, ct);
    }

    private static string ActionLabel(string purpose) => purpose == "receipt" ? "Empfangsbestätigung" : purpose == "waiting" ? "Wartehinweis" : "KI-Antwort";
    private static string NextCheck(long? next) => next == long.MaxValue ? "Erneut prüfen erforderlich" : next is > 0 ? $"nächste Prüfung {DateTimeOffset.FromUnixTimeMilliseconds(next.Value).ToLocalTime():g}" : "Prüfung ausstehend";
    private void OnChanged() => _dispatch(RefreshInBackground);
    private void RefreshInBackground() { if (!_disposed) RefreshAsync(_lifetime.Token).SafeFireAndForget(_logger, "ScmEventQueue.Refresh"); }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifier.Changed -= OnChanged;
        _lifetime.Cancel();
    }
}

/// <summary>Unveränderlicher Anzeigesnapshot; Identitäten für Bestätigungen werden nie nachträglich ausgetauscht.</summary>
public sealed record ScmEventQueueItem(Guid Id, Guid TaskId, string TaskTitle, string Repository, string Reference,
    string Kind, string Received, string Status, string StatusText, string Body, string Origin, string? Reason,
    string? CliRunId, Guid? AttemptId, bool IsBlocked)
{
    /// <summary>Der aktuelle Attempt ist abgeschlossen.</summary>
    public bool IsProcessed => Status == "Verarbeitet";
    /// <summary>Unsicherheit benötigt eine ausdrückliche Warnungsbestätigung.</summary>
    public bool CanResend => AttemptId is not null && Status is "ZustellungUngewiss" or "VerarbeitungUngeklärt";
    /// <summary>Nur eine unbekannte, noch nicht zugestellte Bereitschaft kann bestätigt werden.</summary>
    public bool CanConfirmReady => !IsBlocked && AttemptId is null && CliRunId is not null && Reason?.Contains("Eingabebereitschaft unbekannt", StringComparison.Ordinal) == true;
    /// <summary>Erstellt die menschenlesbare Projektion aus dauerhaften Daten.</summary>
    public static ScmEventQueueItem From(ScmIncomingEvent item, Aufgabe? task)
    {
        var observation = ScmEventStore.Observation(item);
        var blocked = task is null || task.Status is AufgabeStatus.Beendet or AufgabeStatus.Archiviert || task.PausiertBisUtc > DateTimeOffset.UtcNow;
        var reason = task is null ? "Zugehörige Aufgabe wurde entfernt." : task.Status is AufgabeStatus.Beendet or AufgabeStatus.Archiviert
            ? "Aufgabe beendet/archiviert; Start gesperrt." : task.PausiertBisUtc > DateTimeOffset.UtcNow ? "Aufgabe pausiert; Start gesperrt." : item.Reason;
        return new(item.Id, item.AufgabeId, task?.Titel ?? "Entfernte Aufgabe",
            $"{observation.Reference.Repository.Owner}/{observation.Reference.Repository.Name}",
            $"{(observation.Reference.Kind == Domain.ValueObjects.ScmReferenceKind.Issue ? "Issue" : "Pull Request")} #{observation.Reference.Number}",
            observation.ContentKind switch { Domain.ValueObjects.ScmContentKind.Description => "Beschreibung", Domain.ValueObjects.ScmContentKind.ReviewComment => "Review-Kommentar", Domain.ValueObjects.ScmContentKind.PullRequestReview => "Review", _ => "Kommentar" },
            DateTimeOffset.FromUnixTimeMilliseconds(item.ReceivedUtc).ToLocalTime().ToString("g"), item.Status,
            item.Status switch { "ZustellungVorbereitet" => "Zustellung vorbereitet", "InBearbeitung" => "In Bearbeitung", "ZustellungUngewiss" => "Zustellung ungewiss", "VerarbeitungUngeklärt" => "Verarbeitung ungeklärt", _ => item.Status },
            observation.Body, observation.Url, reason, item.CliRunId, item.DeliveryAttemptId, blocked);
    }
}
