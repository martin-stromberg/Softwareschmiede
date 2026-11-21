using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Singleton-Service, der die Laufzeit-Warteschlange zeitgesteuerter Prompts pro Aufgabe verwaltet
/// und bei Erreichen der Zielzeit den Versand an die aktive <see cref="ITerminalSession"/> auslöst.
/// Geplante Prompts werden über den <see cref="ScheduledPromptStore"/> persistiert und beim
/// App-Start via <see cref="RestoreScheduledPromptsAsync"/> wiederhergestellt; ohne zustellfähige
/// Session wartet der Plan auf den nächsten CLI-Start (<see cref="AiExecutionService.CliProcessStatusChanged"/>).
/// </summary>
public sealed class PromptScheduleDispatchService : IDisposable
{
    private sealed class ScheduledPromptEntry
    {
        public required ScheduledPromptInfo Info { get; init; }
        public ITimer? Timer { get; init; }
        public bool WaitingForSession { get; init; }
    }

    private readonly AiExecutionService _aiService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PromptScheduleDispatchService> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly Dictionary<Guid, ScheduledPromptEntry> _scheduledPrompts = new();
    private readonly object _lock = new();

    /// <inheritdoc cref="PromptScheduleDispatchService"/>
    /// <param name="aiService">Der KI-Ausführungsservice, über den der Prompt an die laufende CLI-Session gesendet wird.</param>
    /// <param name="timeProvider">Zeitquelle für Timer und Zeitpunktvergleiche.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="scopeFactory">Optionale Scope-Factory zum Laden des aktuellen <c>PausedUntilUtc</c>-Werts der Aufgabe vor dem Versand sowie für die Persistenz der geplanten Prompts. Ohne Registrierung (z. B. Unit-Tests) entfallen Pausenprüfung und Persistenz.</param>
    public PromptScheduleDispatchService(AiExecutionService aiService, TimeProvider timeProvider, ILogger<PromptScheduleDispatchService> logger, IServiceScopeFactory? scopeFactory = null)
    {
        _aiService = aiService;
        _timeProvider = timeProvider;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _aiService.CliProcessStatusChanged += OnCliProcessStatusChanged;
    }

    /// <summary>Wird ausgelöst, nachdem ein zeitgesteuerter Prompt erfolgreich an die CLI-Session versendet wurde.</summary>
    public event Action<Guid>? PromptSent;

    /// <summary>Wird ausgelöst, wenn sich der Wartestellungs-Zustand eines geplanten Prompts ändert (Planen, Storno, Versand, „wartet auf Session").</summary>
    public event Action<Guid>? ScheduledPromptChanged;

    /// <summary>
    /// Löst das <see cref="AiExecutionService.CliProcessStatusChanged"/>-Abo, entschärft alle
    /// armierten Timer und leert die Warteschlange — eine disposed Instanz ist damit vollständig
    /// inert (relevant für Szenarien mit mehreren Service-Instanzen, z. B. simulierter
    /// App-Neustart in Tests). Persistierte Datensätze bleiben bewusst bestehen und werden beim
    /// nächsten Start via <see cref="RestoreScheduledPromptsAsync"/> wiederhergestellt.
    /// </summary>
    public void Dispose()
    {
        _aiService.CliProcessStatusChanged -= OnCliProcessStatusChanged;

        // Timer erst unter _lock einsammeln und außerhalb des Locks disposen: Ihre Callbacks
        // (HandleTimerElapsedAsync) greifen selbst auf _lock zu.
        List<ITimer> timer;
        lock (_lock)
        {
            timer = _scheduledPrompts.Values.Select(e => e.Timer).OfType<ITimer>().ToList();
            _scheduledPrompts.Clear();
        }

        foreach (var t in timer)
            t.Dispose();
    }

    /// <summary>
    /// Plant den Versand eines Prompts zur angegebenen Zielzeit. Liegt die Zielzeit in der
    /// Vergangenheit oder Gegenwart, wird sofort versendet. Ein bereits geplanter Prompt für
    /// dieselbe Aufgabe wird ersetzt (alter Timer wird abgebrochen).
    /// </summary>
    /// <param name="workItemId">ID der Aufgabe, deren Session den Prompt erhalten soll.</param>
    /// <param name="promptText">Der bereits platzhalteraufgelöste Prompttext.</param>
    /// <param name="targetTime">Der Zeitpunkt, zu dem der Prompt versendet werden soll.</param>
    public async Task SchedulePromptAsync(Guid workItemId, string promptText, DateTimeOffset targetTime)
    {
        var now = _timeProvider.GetUtcNow();

        // Aktive Pause: Der Versand wird auf das Pausenende verschoben statt sofort/zur Zielzeit
        // zu senden — der Prompt geht nicht verloren.
        var pausedUntil = await GetPausedUntilUtcAsync(workItemId);
        if (pausedUntil is { } until && until > now && until > targetTime)
        {
            targetTime = until;
        }

        if (targetTime <= now)
        {
            var immediateInfo = new ScheduledPromptInfo(workItemId, promptText, targetTime);
            var replacedEntry = RemoveEntry(workItemId);
            replacedEntry?.Timer?.Dispose();
            // Sofortversand: bewusst kein persistierter Datensatz im Erfolgsfall — es gibt dann
            // keine Wartestellung, die einen App-Neustart überleben müsste. Schlägt der Versand
            // fehl, wird der neue Plan persistiert, damit nach einem Neustart nicht der ersetzte
            // (ältere) Datensatz wiederhergestellt wird. Der Schreibzugriff ist doppelt konditional:
            // Erst legt MarkWaitingForSession den Eintrag unter _lock nur zurück, wenn kein
            // neuerer Plan eingetragen wurde; danach schreibt der Upsert nur, wenn der Datensatz
            // noch dem Vorgängerstand entspricht — sonst würde der Alt-Versand einen während des
            // in-flight-Versands persistierten neueren Plan überschreiben (dessen Datensatz die
            // Löschung nach dem späteren Versand per Mismatch verweigern und als Leiche überleben
            // würde).
            if (!await SendPromptAsync(workItemId, promptText, redelivery: false, replacedEntry?.Info ?? immediateInfo)
                && MarkWaitingForSession(workItemId, immediateInfo))
            {
                await UpsertRecordIfCurrentAsync(workItemId, immediateInfo, replacedEntry?.Info);
            }
            return;
        }

        // DB zuerst: Der Datensatz muss vor dem Timer-Armieren stehen, damit ein sofort feuernder
        // Timer-Callback den Plan nicht vor der Persistenz entfernt.
        await UpsertRecordAsync(workItemId, promptText, targetTime);

        RemoveEntry(workItemId)?.Timer?.Dispose();

        var info = new ScheduledPromptInfo(workItemId, promptText, targetTime);
        // Timer zunächst inaktiv (Timeout.InfiniteTimeSpan) anlegen und erst NACH dem Eintragen in
        // _scheduledPrompts scharfschalten (timer.Change): Würde der Timer bereits mit der echten
        // Restlaufzeit erzeugt, könnte sein Callback bei sehr kurzer Restlaufzeit auf einem ThreadPool-Thread
        // feuern, bevor die nachfolgende Zeile den Eintrag unter _lock einfügt — HandleTimerElapsedAsync fände
        // dann keinen Eintrag und würde den Prompt kommentarlos nie versenden.
        var timer = _timeProvider.CreateTimer(
            _ => HandleTimerElapsedAsync(workItemId).SafeFireAndForget(_logger, "PromptScheduleDispatchService.HandleTimerElapsedAsync"),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);

        lock (_lock)
        {
            _scheduledPrompts[workItemId] = new ScheduledPromptEntry { Info = info, Timer = timer };
        }

        timer.Change(targetTime - now, Timeout.InfiniteTimeSpan);
        ScheduledPromptChanged?.Invoke(workItemId);
    }

    /// <summary>Bricht einen für die Aufgabe geplanten Prompt-Versand ab (Eintrag + persistierter Datensatz), falls vorhanden.</summary>
    /// <param name="workItemId">ID der Aufgabe.</param>
    public async Task CancelScheduledPromptAsync(Guid workItemId)
    {
        RemoveEntry(workItemId)?.Timer?.Dispose();
        await DeleteRecordAsync(workItemId);
        ScheduledPromptChanged?.Invoke(workItemId);
    }

    /// <summary>Gibt Informationen zum aktuell für die Aufgabe geplanten Prompt zurück, oder null wenn keiner geplant ist.</summary>
    /// <param name="workItemId">ID der Aufgabe.</param>
    /// <returns>Die <see cref="ScheduledPromptInfo"/> des geplanten Prompts, oder null.</returns>
    public ScheduledPromptInfo? GetScheduledPromptStatus(Guid workItemId)
    {
        lock (_lock)
        {
            return _scheduledPrompts.TryGetValue(workItemId, out var entry)
                ? entry.Info with { WaitingForSession = entry.WaitingForSession }
                : null;
        }
    }

    /// <summary>
    /// Stellt persistierte geplante Prompts nach dem App-Start wieder her: zukünftige Zielzeiten
    /// armierten einen Timer, verstrichene Zielzeiten gehen in „wartet auf Session" über und werden
    /// beim nächsten CLI-Start der Aufgabe zugestellt. Verwaiste Datensätze (Aufgabe nicht mehr
    /// vorhanden) werden defensiv gelöscht.
    /// </summary>
    public async Task RestoreScheduledPromptsAsync()
    {
        if (_scopeFactory is null)
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetService<ScheduledPromptStore>();
            var workItemService = scope.ServiceProvider.GetService<WorkItemService>();
            if (store is null || workItemService is null)
                return;

            var plans = await store.GetAllAsync();
            foreach (var plan in plans)
            {
                // Fehler pro Datensatz isolieren: Ein einzelner defekter Plan darf die
                // Wiederherstellung der übrigen Pläne nicht blockieren.
                try
                {
                    var workItem = await workItemService.GetByIdAsync(plan.WorkItemId);
                    if (workItem is null)
                    {
                        // Verwaister Datensatz (dürfte durch FK-Cascade nicht vorkommen) — defensiv entfernen.
                        await store.DeleteAsync(plan.WorkItemId);
                        continue;
                    }

                    var now = _timeProvider.GetUtcNow();
                    var target = plan.TargetTimeUtc;
                    if (workItem.PausedUntilUtc is { } until && until > now && until > target)
                    {
                        target = until;
                        await store.UpdateTargetTimeAsync(plan.WorkItemId, until);
                    }

                    var info = new ScheduledPromptInfo(plan.WorkItemId, plan.PromptText, target);
                    if (target <= now)
                    {
                        lock (_lock)
                        {
                            _scheduledPrompts[plan.WorkItemId] = new ScheduledPromptEntry
                            {
                                Info = info,
                                Timer = null,
                                WaitingForSession = true
                            };
                        }
                        continue;
                    }

                    var workItemId = plan.WorkItemId;
                    var timer = _timeProvider.CreateTimer(
                        _ => HandleTimerElapsedAsync(workItemId).SafeFireAndForget(_logger, "PromptScheduleDispatchService.HandleTimerElapsedAsync"),
                        null,
                        Timeout.InfiniteTimeSpan,
                        Timeout.InfiniteTimeSpan);

                    lock (_lock)
                    {
                        _scheduledPrompts[workItemId] = new ScheduledPromptEntry { Info = info, Timer = timer };
                    }

                    try
                    {
                        timer.Change(target - now, Timeout.InfiniteTimeSpan);
                    }
                    catch
                    {
                        // Armiierung fehlgeschlagen (z. B. Zielzeit jenseits des Timer-Maximums):
                        // den soeben eingetragenen, nie feuernden Eintrag wieder entfernen, damit
                        // kein untoter Plan in der Warteschlange zurückbleibt.
                        RemoveEntry(workItemId)?.Timer?.Dispose();
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Persistierter zeitgesteuerter Prompt {PromptId} für Aufgabe {AufgabeId} konnte nicht wiederhergestellt werden — die übrigen Pläne werden fortgesetzt.",
                        plan.Id,
                        plan.WorkItemId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Persistierte zeitgesteuerte Prompts konnten beim App-Start nicht wiederhergestellt werden.");
        }
    }

    private ScheduledPromptEntry? RemoveEntry(Guid workItemId)
    {
        lock (_lock)
        {
            return _scheduledPrompts.Remove(workItemId, out var entry) ? entry : null;
        }
    }

    /// <summary>
    /// Hält den Plan im Zustand „wartet auf Session" — Zustellung beim nächsten CLI-Start.
    /// Legt den Eintrag nur zurück, wenn nicht zwischenzeitlich ein neuerer Plan eingetragen
    /// wurde: Sonst würde der neu armierte Timer verwaist auf den veralteten Wartestellungs-Eintrag
    /// treffen und den alten Prompttext zur neuen Zielzeit versenden (Guard analog zur
    /// Pausen-Umplanung in <see cref="HandleTimerElapsedAsync"/>).
    /// </summary>
    /// <returns><c>true</c>, wenn der Eintrag zurückgelegt wurde; <c>false</c>, wenn ein neuerer Eintrag die Markierung verweigerte.</returns>
    private bool MarkWaitingForSession(Guid workItemId, ScheduledPromptInfo info)
    {
        var marked = false;
        lock (_lock)
        {
            if (!_scheduledPrompts.ContainsKey(workItemId))
            {
                _scheduledPrompts[workItemId] = new ScheduledPromptEntry
                {
                    Info = info,
                    Timer = null,
                    WaitingForSession = true
                };
                marked = true;
            }
        }

        if (marked)
            ScheduledPromptChanged?.Invoke(workItemId);
        return marked;
    }

    private void OnCliProcessStatusChanged(Guid workItemId, CliProcessStatus status)
    {
        if (status != CliProcessStatus.Started)
            return;

        DeliverAfterSessionStartAsync(workItemId).SafeFireAndForget(_logger, "PromptScheduleDispatchService.DeliverAfterSessionStartAsync");
    }

    /// <summary>Stellt wartende bzw. fällige Pläne beim Start einer CLI-Session der Aufgabe zu.</summary>
    private async Task DeliverAfterSessionStartAsync(Guid workItemId)
    {
        ScheduledPromptEntry entry;
        lock (_lock)
        {
            if (!_scheduledPrompts.TryGetValue(workItemId, out var existing))
                return;

            if (!existing.WaitingForSession && existing.Info.TargetTime > _timeProvider.GetUtcNow())
                return;

            _scheduledPrompts.Remove(workItemId);
            entry = existing;
        }

        entry.Timer?.Dispose();
        if (!await SendPromptAsync(workItemId, entry.Info.PromptText, redelivery: true, entry.Info))
            MarkWaitingForSession(workItemId, entry.Info);
    }

    private async Task HandleTimerElapsedAsync(Guid workItemId)
    {
        ScheduledPromptEntry entry;
        lock (_lock)
        {
            if (!_scheduledPrompts.Remove(workItemId, out var removed))
                return;
            entry = removed;
        }

        // Pause kann NACH dem Planen gesetzt worden sein: aktuellen PausedUntilUtc-Wert laden und
        // bei aktiver Pause den Timer auf das Pausenende neu scharf machen statt zu senden.
        var pausedUntil = await GetPausedUntilUtcAsync(workItemId);
        var now = _timeProvider.GetUtcNow();
        if (pausedUntil is { } until && until > now)
        {
            var rescheduled = false;
            lock (_lock)
            {
                // Nur zurücklegen, wenn nicht zwischenzeitlich ein neuer Prompt geplant wurde.
                if (!_scheduledPrompts.ContainsKey(workItemId))
                {
                    // TargetTime auf das Pausenende aktualisieren, damit GetScheduledPromptStatus
                    // die tatsächliche neue Zielzeit meldet statt des verstrichenen Zeitpunkts.
                    _scheduledPrompts[workItemId] = new ScheduledPromptEntry
                    {
                        Info = new ScheduledPromptInfo(workItemId, entry.Info.PromptText, until),
                        Timer = entry.Timer
                    };
                    entry.Timer?.Change(until - now, Timeout.InfiniteTimeSpan);
                    rescheduled = true;
                }
            }

            if (rescheduled)
            {
                _logger.LogInformation(
                    "Zeitgesteuerter Prompt für Aufgabe {AufgabeId} auf das Pausenende {PausiertBisUtc} verschoben.",
                    workItemId,
                    until);
                await UpdateTargetTimeRecordAsync(workItemId, until);
                ScheduledPromptChanged?.Invoke(workItemId);
            }
            else
            {
                entry.Timer?.Dispose();
            }

            return;
        }

        entry.Timer?.Dispose();
        if (!await SendPromptAsync(workItemId, entry.Info.PromptText, redelivery: false, entry.Info))
            MarkWaitingForSession(workItemId, entry.Info);
    }

    private async Task<DateTimeOffset?> GetPausedUntilUtcAsync(Guid workItemId)
    {
        if (_scopeFactory is null)
            return null;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var workItemService = scope.ServiceProvider.GetRequiredService<WorkItemService>();
            var workItem = await workItemService.GetByIdAsync(workItemId);
            return workItem?.PausedUntilUtc;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Pausenstatus für Aufgabe {AufgabeId} konnte nicht geladen werden — Versand wird nicht verschoben.",
                workItemId);
            return null;
        }
    }

    /// <param name="workItemId">ID der Aufgabe, deren Session den Prompt erhalten soll.</param>
    /// <param name="promptText">Der zu versendende Prompttext.</param>
    /// <param name="redelivery"><c>true</c> bei einer Nachzustellung nach einem Session-Start (steuert den Protokolleintrag).</param>
    /// <param name="recordReference">
    /// Der Plan, auf den sich der persistierte Datensatz voraussichtlich noch bezieht: Nach
    /// erfolgreichem Versand wird der Datensatz nur gelöscht, wenn er inhaltlich diesem Plan
    /// entspricht — ein zwischenzeitlich neu persistierter Plan derselben Aufgabe bleibt erhalten
    /// (Lost-Update-Schutz). Für den Sofortversand ist das der ersetzte Eintrag bzw. der
    /// versendete Plan selbst, wenn keiner ersetzt wurde.
    /// </param>
    /// <returns><c>true</c>, wenn der Prompt zugestellt wurde; <c>false</c>, wenn der Plan auf eine Session wartet.</returns>
    private async Task<bool> SendPromptAsync(Guid workItemId, string promptText, bool redelivery, ScheduledPromptInfo recordReference)
    {
        var session = _aiService.GetTerminalSession(workItemId);
        if (session is null)
        {
            _logger.LogWarning(
                "Zeitgesteuerter Prompt für Aufgabe {AufgabeId} wartet auf eine CLI-Session, da aktuell keine aktive vorhanden ist.",
                workItemId);
            return false;
        }

        try
        {
            await session.WritePromptAsync(promptText, CancellationToken.None);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogDebug(
                ex,
                "Zeitgesteuerter Prompt für Aufgabe {AufgabeId} konnte nicht versendet werden, da die Session zwischenzeitlich disposed wurde — der Plan wartet auf die nächste Session.",
                workItemId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Zeitgesteuerter Prompt für Aufgabe {AufgabeId} konnte nicht versendet werden — der Plan wartet auf die nächste Session.",
                workItemId);
            return false;
        }

        await DeleteRecordIfCurrentAsync(workItemId, recordReference);
        PromptSent?.Invoke(workItemId);
        ScheduledPromptChanged?.Invoke(workItemId);
        await WriteDispatchLogAsync(workItemId, redelivery);
        return true;
    }

    /// <summary>
    /// Schreibt einen SystemMeldung-Protokolleintrag für einen Hintergrundversand: bei Nachzustellung
    /// nach einem Session-Start immer, bei Timer-/Sofortversand nur wenn keine Detailansicht den
    /// Versand beobachtet (<see cref="PromptSent"/> ohne Abonnenten). Fehler (z. B. eine
    /// zwischenzeitlich gelöschte Aufgabe) brechen den Versand nicht.
    /// </summary>
    private async Task WriteDispatchLogAsync(Guid workItemId, bool redelivery)
    {
        if (_scopeFactory is null)
            return;

        if (!redelivery && PromptSent is not null)
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var logService = scope.ServiceProvider.GetRequiredService<LogService>();
            var content = redelivery
                ? "Zeitgesteuerter Prompt nach CLI-Start zugestellt."
                : "Zeitgesteuerter Prompt versendet.";
            await logService.AddEntryAsync(workItemId, LogEntryType.SystemMessage, content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Protokolleintrag für den zeitgesteuerten Prompt-Versand der Aufgabe {AufgabeId} konnte nicht geschrieben werden.",
                workItemId);
        }
    }

    private Task UpsertRecordAsync(Guid workItemId, string promptText, DateTimeOffset targetTimeUtc)
        => WithStoreAsync(
            store => store.UpsertAsync(workItemId, promptText, targetTimeUtc),
            "Zeitgesteuerter Prompt für Aufgabe {AufgabeId} konnte nicht persistiert werden — der Plan bleibt sitzungsgebunden.",
            workItemId);

    /// <summary>
    /// Persistiert den Plan nur, wenn der Datensatz noch dem erwarteten Vorgängerstand entspricht
    /// (<paramref name="expectedPredecessor"/> ist der ersetzte Eintrag bzw. <c>null</c>) —
    /// verhindert, dass der Fehlerpfad eines alten Sofortversands einen zwischenzeitlich
    /// persistierten neueren Plan überschreibt.
    /// </summary>
    private Task UpsertRecordIfCurrentAsync(Guid workItemId, ScheduledPromptInfo plan, ScheduledPromptInfo? expectedPredecessor)
        => WithStoreAsync(
            store => store.UpsertIfMatchingAsync(workItemId, plan.PromptText, plan.TargetTime, expectedPredecessor),
            "Zeitgesteuerter Prompt für Aufgabe {AufgabeId} konnte nicht persistiert werden — der Plan bleibt sitzungsgebunden.",
            workItemId);

    private Task DeleteRecordAsync(Guid workItemId)
        => WithStoreAsync(
            store => store.DeleteAsync(workItemId),
            "Persistierter zeitgesteuerter Prompt für Aufgabe {AufgabeId} konnte nicht gelöscht werden.",
            workItemId);

    /// <summary>
    /// Löscht den Datensatz des versendeten Plans. Ein vom versendeten Plan abweichender Datensatz
    /// wird nur behalten, wenn zum Löschzeitpunkt noch ein In-Memory-Plan für die Aufgabe
    /// existiert (Lost-Update-Schutz: der Datensatz gehört dann einem zwischenzeitlich
    /// persistierten neueren Plan). Ohne In-Memory-Eintrag ist ein abweichender Datensatz eine
    /// Leiche des versendeten Plans — z. B. nach einem fehlgeschlagenen Store-Schreibzugriff —
    /// und wird mitgelöscht, damit der bereits zugestellte Prompt nach einem Neustart nicht
    /// erneut zugestellt wird.
    /// </summary>
    private Task DeleteRecordIfCurrentAsync(Guid workItemId, ScheduledPromptInfo recordReference)
    {
        bool newerScheduleActive;
        lock (_lock)
        {
            newerScheduleActive = _scheduledPrompts.ContainsKey(workItemId);
        }

        return WithStoreAsync(
            store => store.DeleteAfterDispatchAsync(workItemId, recordReference.PromptText, recordReference.TargetTime, newerScheduleActive),
            "Persistierter zeitgesteuerter Prompt für Aufgabe {AufgabeId} konnte nach dem Versand nicht gelöscht werden.",
            workItemId);
    }

    private Task UpdateTargetTimeRecordAsync(Guid workItemId, DateTimeOffset targetTimeUtc)
        => WithStoreAsync(
            store => store.UpdateTargetTimeAsync(workItemId, targetTimeUtc),
            "Zielzeitpunkt des persistierten zeitgesteuerten Prompts für Aufgabe {AufgabeId} konnte nicht aktualisiert werden.",
            workItemId);

    /// <summary>Führt eine Store-Aktion in einem eigenen Scope aus; Fehler werden geloggt statt den Aufrufer zu brechen.</summary>
    private async Task WithStoreAsync(Func<ScheduledPromptStore, Task> action, string errorText, params object[] arguments)
    {
        if (_scopeFactory is null)
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetService<ScheduledPromptStore>();
            if (store is null)
                return;
            await action(store);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, errorText, arguments);
        }
    }
}
