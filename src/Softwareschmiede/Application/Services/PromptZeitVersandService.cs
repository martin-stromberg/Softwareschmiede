using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Singleton-Service, der die Laufzeit-Warteschlange zeitgesteuerter Prompts pro Aufgabe verwaltet
/// und bei Erreichen der Zielzeit den Versand an die aktive <see cref="ITerminalSession"/> auslöst.
/// Geplante Prompts werden über den <see cref="ZeitgesteuerterPromptStore"/> persistiert und beim
/// App-Start via <see cref="RestoreScheduledPromptsAsync"/> wiederhergestellt; ohne zustellfähige
/// Session wartet der Plan auf den nächsten CLI-Start (<see cref="KiAusfuehrungsService.CliProcessStatusChanged"/>).
/// </summary>
public sealed class PromptZeitVersandService : IDisposable
{
    private sealed class ScheduledPromptEntry
    {
        public required ScheduledPromptInfo Info { get; init; }
        public ITimer? Timer { get; init; }
        public bool WartetAufSession { get; init; }
    }

    private readonly KiAusfuehrungsService _kiService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PromptZeitVersandService> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly Dictionary<Guid, ScheduledPromptEntry> _scheduledPrompts = new();
    private readonly object _lock = new();

    /// <inheritdoc cref="PromptZeitVersandService"/>
    /// <param name="kiService">Der KI-Ausführungsservice, über den der Prompt an die laufende CLI-Session gesendet wird.</param>
    /// <param name="timeProvider">Zeitquelle für Timer und Zeitpunktvergleiche.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="scopeFactory">Optionale Scope-Factory zum Laden des aktuellen <c>PausiertBisUtc</c>-Werts der Aufgabe vor dem Versand sowie für die Persistenz der geplanten Prompts. Ohne Registrierung (z. B. Unit-Tests) entfallen Pausenprüfung und Persistenz.</param>
    public PromptZeitVersandService(KiAusfuehrungsService kiService, TimeProvider timeProvider, ILogger<PromptZeitVersandService> logger, IServiceScopeFactory? scopeFactory = null)
    {
        _kiService = kiService;
        _timeProvider = timeProvider;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _kiService.CliProcessStatusChanged += OnCliProcessStatusChanged;
    }

    /// <summary>Wird ausgelöst, nachdem ein zeitgesteuerter Prompt erfolgreich an die CLI-Session versendet wurde.</summary>
    public event Action<Guid>? PromptSent;

    /// <summary>Wird ausgelöst, wenn sich der Wartestellungs-Zustand eines geplanten Prompts ändert (Planen, Storno, Versand, „wartet auf Session").</summary>
    public event Action<Guid>? ScheduledPromptChanged;

    /// <summary>
    /// Löst das <see cref="KiAusfuehrungsService.CliProcessStatusChanged"/>-Abo, entschärft alle
    /// armierten Timer und leert die Warteschlange — eine disposed Instanz ist damit vollständig
    /// inert (relevant für Szenarien mit mehreren Service-Instanzen, z. B. simulierter
    /// App-Neustart in Tests). Persistierte Datensätze bleiben bewusst bestehen und werden beim
    /// nächsten Start via <see cref="RestoreScheduledPromptsAsync"/> wiederhergestellt.
    /// </summary>
    public void Dispose()
    {
        _kiService.CliProcessStatusChanged -= OnCliProcessStatusChanged;

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
    /// <param name="aufgabeId">ID der Aufgabe, deren Session den Prompt erhalten soll.</param>
    /// <param name="promptText">Der bereits platzhalteraufgelöste Prompttext.</param>
    /// <param name="targetTime">Der Zeitpunkt, zu dem der Prompt versendet werden soll.</param>
    public async Task SchedulePromptAsync(Guid aufgabeId, string promptText, DateTimeOffset targetTime)
    {
        var now = _timeProvider.GetUtcNow();

        // Aktive Pause: Der Versand wird auf das Pausenende verschoben statt sofort/zur Zielzeit
        // zu senden — der Prompt geht nicht verloren.
        var pausiertBis = await GetPausiertBisUtcAsync(aufgabeId);
        if (pausiertBis is { } bis && bis > now && bis > targetTime)
        {
            targetTime = bis;
        }

        if (targetTime <= now)
        {
            var sofortInfo = new ScheduledPromptInfo(aufgabeId, promptText, targetTime);
            var ersetzterEintrag = RemoveEntry(aufgabeId);
            ersetzterEintrag?.Timer?.Dispose();
            // Sofortversand: bewusst kein persistierter Datensatz im Erfolgsfall — es gibt dann
            // keine Wartestellung, die einen App-Neustart überleben müsste. Schlägt der Versand
            // fehl, wird der neue Plan persistiert, damit nach einem Neustart nicht der ersetzte
            // (ältere) Datensatz wiederhergestellt wird. Der Schreibzugriff ist doppelt konditional:
            // Erst legt MarkiereWartetAufSession den Eintrag unter _lock nur zurück, wenn kein
            // neuerer Plan eingetragen wurde; danach schreibt der Upsert nur, wenn der Datensatz
            // noch dem Vorgängerstand entspricht — sonst würde der Alt-Versand einen während des
            // in-flight-Versands persistierten neueren Plan überschreiben (dessen Datensatz die
            // Löschung nach dem späteren Versand per Mismatch verweigern und als Leiche überleben
            // würde).
            if (!await SendPromptAsync(aufgabeId, promptText, nachzustellung: false, ersetzterEintrag?.Info ?? sofortInfo)
                && MarkiereWartetAufSession(aufgabeId, sofortInfo))
            {
                await UpsertDatensatzWennAktuellAsync(aufgabeId, sofortInfo, ersetzterEintrag?.Info);
            }
            return;
        }

        // DB zuerst: Der Datensatz muss vor dem Timer-Armieren stehen, damit ein sofort feuernder
        // Timer-Callback den Plan nicht vor der Persistenz entfernt.
        await UpsertDatensatzAsync(aufgabeId, promptText, targetTime);

        RemoveEntry(aufgabeId)?.Timer?.Dispose();

        var info = new ScheduledPromptInfo(aufgabeId, promptText, targetTime);
        // Timer zunächst inaktiv (Timeout.InfiniteTimeSpan) anlegen und erst NACH dem Eintragen in
        // _scheduledPrompts scharfschalten (timer.Change): Würde der Timer bereits mit der echten
        // Restlaufzeit erzeugt, könnte sein Callback bei sehr kurzer Restlaufzeit auf einem ThreadPool-Thread
        // feuern, bevor die nachfolgende Zeile den Eintrag unter _lock einfügt — HandleTimerElapsedAsync fände
        // dann keinen Eintrag und würde den Prompt kommentarlos nie versenden.
        var timer = _timeProvider.CreateTimer(
            _ => HandleTimerElapsedAsync(aufgabeId).SafeFireAndForget(_logger, "PromptZeitVersandService.HandleTimerElapsedAsync"),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);

        lock (_lock)
        {
            _scheduledPrompts[aufgabeId] = new ScheduledPromptEntry { Info = info, Timer = timer };
        }

        timer.Change(targetTime - now, Timeout.InfiniteTimeSpan);
        ScheduledPromptChanged?.Invoke(aufgabeId);
    }

    /// <summary>Bricht einen für die Aufgabe geplanten Prompt-Versand ab (Eintrag + persistierter Datensatz), falls vorhanden.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    public async Task CancelScheduledPromptAsync(Guid aufgabeId)
    {
        RemoveEntry(aufgabeId)?.Timer?.Dispose();
        await LoescheDatensatzAsync(aufgabeId);
        ScheduledPromptChanged?.Invoke(aufgabeId);
    }

    /// <summary>Gibt Informationen zum aktuell für die Aufgabe geplanten Prompt zurück, oder null wenn keiner geplant ist.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <returns>Die <see cref="ScheduledPromptInfo"/> des geplanten Prompts, oder null.</returns>
    public ScheduledPromptInfo? GetScheduledPromptStatus(Guid aufgabeId)
    {
        lock (_lock)
        {
            return _scheduledPrompts.TryGetValue(aufgabeId, out var entry)
                ? entry.Info with { WartetAufSession = entry.WartetAufSession }
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
            var store = scope.ServiceProvider.GetService<ZeitgesteuerterPromptStore>();
            var aufgabeService = scope.ServiceProvider.GetService<AufgabeService>();
            if (store is null || aufgabeService is null)
                return;

            var plaene = await store.GetAlleAsync();
            foreach (var plan in plaene)
            {
                // Fehler pro Datensatz isolieren: Ein einzelner defekter Plan darf die
                // Wiederherstellung der übrigen Pläne nicht blockieren.
                try
                {
                    var aufgabe = await aufgabeService.GetByIdAsync(plan.AufgabeId);
                    if (aufgabe is null)
                    {
                        // Verwaister Datensatz (dürfte durch FK-Cascade nicht vorkommen) — defensiv entfernen.
                        await store.LoeschenAsync(plan.AufgabeId);
                        continue;
                    }

                    var now = _timeProvider.GetUtcNow();
                    var ziel = plan.ZielZeitpunktUtc;
                    if (aufgabe.PausiertBisUtc is { } bis && bis > now && bis > ziel)
                    {
                        ziel = bis;
                        await store.UpdateZielZeitpunktAsync(plan.AufgabeId, bis);
                    }

                    var info = new ScheduledPromptInfo(plan.AufgabeId, plan.PromptText, ziel);
                    if (ziel <= now)
                    {
                        lock (_lock)
                        {
                            _scheduledPrompts[plan.AufgabeId] = new ScheduledPromptEntry
                            {
                                Info = info,
                                Timer = null,
                                WartetAufSession = true
                            };
                        }
                        continue;
                    }

                    var aufgabeId = plan.AufgabeId;
                    var timer = _timeProvider.CreateTimer(
                        _ => HandleTimerElapsedAsync(aufgabeId).SafeFireAndForget(_logger, "PromptZeitVersandService.HandleTimerElapsedAsync"),
                        null,
                        Timeout.InfiniteTimeSpan,
                        Timeout.InfiniteTimeSpan);

                    lock (_lock)
                    {
                        _scheduledPrompts[aufgabeId] = new ScheduledPromptEntry { Info = info, Timer = timer };
                    }

                    try
                    {
                        timer.Change(ziel - now, Timeout.InfiniteTimeSpan);
                    }
                    catch
                    {
                        // Armiierung fehlgeschlagen (z. B. Zielzeit jenseits des Timer-Maximums):
                        // den soeben eingetragenen, nie feuernden Eintrag wieder entfernen, damit
                        // kein untoter Plan in der Warteschlange zurückbleibt.
                        RemoveEntry(aufgabeId)?.Timer?.Dispose();
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Persistierter zeitgesteuerter Prompt {PromptId} für Aufgabe {AufgabeId} konnte nicht wiederhergestellt werden — die übrigen Pläne werden fortgesetzt.",
                        plan.Id,
                        plan.AufgabeId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Persistierte zeitgesteuerte Prompts konnten beim App-Start nicht wiederhergestellt werden.");
        }
    }

    private ScheduledPromptEntry? RemoveEntry(Guid aufgabeId)
    {
        lock (_lock)
        {
            return _scheduledPrompts.Remove(aufgabeId, out var entry) ? entry : null;
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
    private bool MarkiereWartetAufSession(Guid aufgabeId, ScheduledPromptInfo info)
    {
        var markiert = false;
        lock (_lock)
        {
            if (!_scheduledPrompts.ContainsKey(aufgabeId))
            {
                _scheduledPrompts[aufgabeId] = new ScheduledPromptEntry
                {
                    Info = info,
                    Timer = null,
                    WartetAufSession = true
                };
                markiert = true;
            }
        }

        if (markiert)
            ScheduledPromptChanged?.Invoke(aufgabeId);
        return markiert;
    }

    private void OnCliProcessStatusChanged(Guid aufgabeId, CliProcessStatus status)
    {
        if (status != CliProcessStatus.Gestartet)
            return;

        ZustellungNachSessionStartAsync(aufgabeId).SafeFireAndForget(_logger, "PromptZeitVersandService.ZustellungNachSessionStart");
    }

    /// <summary>Stellt wartende bzw. fällige Pläne beim Start einer CLI-Session der Aufgabe zu.</summary>
    private async Task ZustellungNachSessionStartAsync(Guid aufgabeId)
    {
        ScheduledPromptEntry entry;
        lock (_lock)
        {
            if (!_scheduledPrompts.TryGetValue(aufgabeId, out var vorhanden))
                return;

            if (!vorhanden.WartetAufSession && vorhanden.Info.TargetTime > _timeProvider.GetUtcNow())
                return;

            _scheduledPrompts.Remove(aufgabeId);
            entry = vorhanden;
        }

        entry.Timer?.Dispose();
        if (!await SendPromptAsync(aufgabeId, entry.Info.PromptText, nachzustellung: true, entry.Info))
            MarkiereWartetAufSession(aufgabeId, entry.Info);
    }

    private async Task HandleTimerElapsedAsync(Guid aufgabeId)
    {
        ScheduledPromptEntry entry;
        lock (_lock)
        {
            if (!_scheduledPrompts.Remove(aufgabeId, out var removed))
                return;
            entry = removed;
        }

        // Pause kann NACH dem Planen gesetzt worden sein: aktuellen PausiertBisUtc-Wert laden und
        // bei aktiver Pause den Timer auf das Pausenende neu scharf machen statt zu senden.
        var pausiertBis = await GetPausiertBisUtcAsync(aufgabeId);
        var now = _timeProvider.GetUtcNow();
        if (pausiertBis is { } bis && bis > now)
        {
            var neuEingeplant = false;
            lock (_lock)
            {
                // Nur zurücklegen, wenn nicht zwischenzeitlich ein neuer Prompt geplant wurde.
                if (!_scheduledPrompts.ContainsKey(aufgabeId))
                {
                    // TargetTime auf das Pausenende aktualisieren, damit GetScheduledPromptStatus
                    // die tatsächliche neue Zielzeit meldet statt des verstrichenen Zeitpunkts.
                    _scheduledPrompts[aufgabeId] = new ScheduledPromptEntry
                    {
                        Info = new ScheduledPromptInfo(aufgabeId, entry.Info.PromptText, bis),
                        Timer = entry.Timer
                    };
                    entry.Timer?.Change(bis - now, Timeout.InfiniteTimeSpan);
                    neuEingeplant = true;
                }
            }

            if (neuEingeplant)
            {
                _logger.LogInformation(
                    "Zeitgesteuerter Prompt für Aufgabe {AufgabeId} auf das Pausenende {PausiertBisUtc} verschoben.",
                    aufgabeId,
                    bis);
                await UpdateZielZeitpunktDatensatzAsync(aufgabeId, bis);
                ScheduledPromptChanged?.Invoke(aufgabeId);
            }
            else
            {
                entry.Timer?.Dispose();
            }

            return;
        }

        entry.Timer?.Dispose();
        if (!await SendPromptAsync(aufgabeId, entry.Info.PromptText, nachzustellung: false, entry.Info))
            MarkiereWartetAufSession(aufgabeId, entry.Info);
    }

    private async Task<DateTimeOffset?> GetPausiertBisUtcAsync(Guid aufgabeId)
    {
        if (_scopeFactory is null)
            return null;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var aufgabeService = scope.ServiceProvider.GetRequiredService<AufgabeService>();
            var aufgabe = await aufgabeService.GetByIdAsync(aufgabeId);
            return aufgabe?.PausiertBisUtc;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Pausenstatus für Aufgabe {AufgabeId} konnte nicht geladen werden — Versand wird nicht verschoben.",
                aufgabeId);
            return null;
        }
    }

    /// <param name="aufgabeId">ID der Aufgabe, deren Session den Prompt erhalten soll.</param>
    /// <param name="promptText">Der zu versendende Prompttext.</param>
    /// <param name="nachzustellung"><c>true</c> bei einer Nachzustellung nach einem Session-Start (steuert den Protokolleintrag).</param>
    /// <param name="datensatzReferenz">
    /// Der Plan, auf den sich der persistierte Datensatz voraussichtlich noch bezieht: Nach
    /// erfolgreichem Versand wird der Datensatz nur gelöscht, wenn er inhaltlich diesem Plan
    /// entspricht — ein zwischenzeitlich neu persistierter Plan derselben Aufgabe bleibt erhalten
    /// (Lost-Update-Schutz). Für den Sofortversand ist das der ersetzte Eintrag bzw. der
    /// versendete Plan selbst, wenn keiner ersetzt wurde.
    /// </param>
    /// <returns><c>true</c>, wenn der Prompt zugestellt wurde; <c>false</c>, wenn der Plan auf eine Session wartet.</returns>
    private async Task<bool> SendPromptAsync(Guid aufgabeId, string promptText, bool nachzustellung, ScheduledPromptInfo datensatzReferenz)
    {
        var session = _kiService.GetTerminalSession(aufgabeId);
        if (session is null)
        {
            _logger.LogWarning(
                "Zeitgesteuerter Prompt für Aufgabe {AufgabeId} wartet auf eine CLI-Session, da aktuell keine aktive vorhanden ist.",
                aufgabeId);
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
                aufgabeId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Zeitgesteuerter Prompt für Aufgabe {AufgabeId} konnte nicht versendet werden — der Plan wartet auf die nächste Session.",
                aufgabeId);
            return false;
        }

        await LoescheDatensatzWennAktuellAsync(aufgabeId, datensatzReferenz);
        PromptSent?.Invoke(aufgabeId);
        ScheduledPromptChanged?.Invoke(aufgabeId);
        await SchreibeVersandProtokollAsync(aufgabeId, nachzustellung);
        return true;
    }

    /// <summary>
    /// Schreibt einen SystemMeldung-Protokolleintrag für einen Hintergrundversand: bei Nachzustellung
    /// nach einem Session-Start immer, bei Timer-/Sofortversand nur wenn keine Detailansicht den
    /// Versand beobachtet (<see cref="PromptSent"/> ohne Abonnenten). Fehler (z. B. eine
    /// zwischenzeitlich gelöschte Aufgabe) brechen den Versand nicht.
    /// </summary>
    private async Task SchreibeVersandProtokollAsync(Guid aufgabeId, bool nachzustellung)
    {
        if (_scopeFactory is null)
            return;

        if (!nachzustellung && PromptSent is not null)
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var protokollService = scope.ServiceProvider.GetRequiredService<ProtokollService>();
            var inhalt = nachzustellung
                ? "Zeitgesteuerter Prompt nach CLI-Start zugestellt."
                : "Zeitgesteuerter Prompt versendet.";
            await protokollService.AddEintragAsync(aufgabeId, ProtokollTyp.SystemMeldung, inhalt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Protokolleintrag für den zeitgesteuerten Prompt-Versand der Aufgabe {AufgabeId} konnte nicht geschrieben werden.",
                aufgabeId);
        }
    }

    private Task UpsertDatensatzAsync(Guid aufgabeId, string promptText, DateTimeOffset zielZeitpunktUtc)
        => MitStoreAsync(
            store => store.UpsertAsync(aufgabeId, promptText, zielZeitpunktUtc),
            "Zeitgesteuerter Prompt für Aufgabe {AufgabeId} konnte nicht persistiert werden — der Plan bleibt sitzungsgebunden.",
            aufgabeId);

    /// <summary>
    /// Persistiert den Plan nur, wenn der Datensatz noch dem erwarteten Vorgängerstand entspricht
    /// (<paramref name="erwarteterVorgaenger"/> ist der ersetzte Eintrag bzw. <c>null</c>) —
    /// verhindert, dass der Fehlerpfad eines alten Sofortversands einen zwischenzeitlich
    /// persistierten neueren Plan überschreibt.
    /// </summary>
    private Task UpsertDatensatzWennAktuellAsync(Guid aufgabeId, ScheduledPromptInfo plan, ScheduledPromptInfo? erwarteterVorgaenger)
        => MitStoreAsync(
            store => store.UpsertWennUebereinstimmendAsync(aufgabeId, plan.PromptText, plan.TargetTime, erwarteterVorgaenger),
            "Zeitgesteuerter Prompt für Aufgabe {AufgabeId} konnte nicht persistiert werden — der Plan bleibt sitzungsgebunden.",
            aufgabeId);

    private Task LoescheDatensatzAsync(Guid aufgabeId)
        => MitStoreAsync(
            store => store.LoeschenAsync(aufgabeId),
            "Persistierter zeitgesteuerter Prompt für Aufgabe {AufgabeId} konnte nicht gelöscht werden.",
            aufgabeId);

    /// <summary>
    /// Löscht den Datensatz des versendeten Plans. Ein vom versendeten Plan abweichender Datensatz
    /// wird nur behalten, wenn zum Löschzeitpunkt noch ein In-Memory-Plan für die Aufgabe
    /// existiert (Lost-Update-Schutz: der Datensatz gehört dann einem zwischenzeitlich
    /// persistierten neueren Plan). Ohne In-Memory-Eintrag ist ein abweichender Datensatz eine
    /// Leiche des versendeten Plans — z. B. nach einem fehlgeschlagenen Store-Schreibzugriff —
    /// und wird mitgelöscht, damit der bereits zugestellte Prompt nach einem Neustart nicht
    /// erneut zugestellt wird.
    /// </summary>
    private Task LoescheDatensatzWennAktuellAsync(Guid aufgabeId, ScheduledPromptInfo datensatzReferenz)
    {
        bool neuererPlanAktiv;
        lock (_lock)
        {
            neuererPlanAktiv = _scheduledPrompts.ContainsKey(aufgabeId);
        }

        return MitStoreAsync(
            store => store.LoeschenNachVersandAsync(aufgabeId, datensatzReferenz.PromptText, datensatzReferenz.TargetTime, neuererPlanAktiv),
            "Persistierter zeitgesteuerter Prompt für Aufgabe {AufgabeId} konnte nach dem Versand nicht gelöscht werden.",
            aufgabeId);
    }

    private Task UpdateZielZeitpunktDatensatzAsync(Guid aufgabeId, DateTimeOffset zielZeitpunktUtc)
        => MitStoreAsync(
            store => store.UpdateZielZeitpunktAsync(aufgabeId, zielZeitpunktUtc),
            "Zielzeitpunkt des persistierten zeitgesteuerten Prompts für Aufgabe {AufgabeId} konnte nicht aktualisiert werden.",
            aufgabeId);

    /// <summary>Führt eine Store-Aktion in einem eigenen Scope aus; Fehler werden geloggt statt den Aufrufer zu brechen.</summary>
    private async Task MitStoreAsync(Func<ZeitgesteuerterPromptStore, Task> aktion, string fehlerText, params object[] argumente)
    {
        if (_scopeFactory is null)
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetService<ZeitgesteuerterPromptStore>();
            if (store is null)
                return;
            await aktion(store);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, fehlerText, argumente);
        }
    }
}
