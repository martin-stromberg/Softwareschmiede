using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Singleton-Service, der die Laufzeit-Warteschlange zeitgesteuerter Prompts pro Aufgabe verwaltet
/// und bei Erreichen der Zielzeit den Versand an die aktive <see cref="PseudoConsoleSession"/> auslöst.
/// Es gibt keine Persistierung — die Verzögerung ist rein sitzungsgebunden.
/// </summary>
public sealed class PromptZeitVersandService
{
    private sealed class ScheduledPromptEntry
    {
        public required ScheduledPromptInfo Info { get; init; }
        public required ITimer Timer { get; init; }
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
    /// <param name="scopeFactory">Optionale Scope-Factory zum Laden des aktuellen <c>PausiertBisUtc</c>-Werts der Aufgabe vor dem Versand. Ohne Registrierung (z. B. Unit-Tests) entfällt die Pausenprüfung.</param>
    public PromptZeitVersandService(KiAusfuehrungsService kiService, TimeProvider timeProvider, ILogger<PromptZeitVersandService> logger, IServiceScopeFactory? scopeFactory = null)
    {
        _kiService = kiService;
        _timeProvider = timeProvider;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <summary>Wird ausgelöst, nachdem ein zeitgesteuerter Prompt erfolgreich an die CLI-Session versendet wurde.</summary>
    public event Action<Guid>? PromptSent;

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
            RemoveEntry(aufgabeId)?.Dispose();
            await SendPromptAsync(aufgabeId, promptText);
            return;
        }

        RemoveEntry(aufgabeId)?.Dispose();

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
    }

    /// <summary>Bricht einen für die Aufgabe geplanten Prompt-Versand ab, falls vorhanden.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    public void CancelScheduledPrompt(Guid aufgabeId)
    {
        RemoveEntry(aufgabeId)?.Dispose();
    }

    /// <summary>Gibt Informationen zum aktuell für die Aufgabe geplanten Prompt zurück, oder null wenn keiner geplant ist.</summary>
    /// <param name="aufgabeId">ID der Aufgabe.</param>
    /// <returns>Die <see cref="ScheduledPromptInfo"/> des geplanten Prompts, oder null.</returns>
    public ScheduledPromptInfo? GetScheduledPromptStatus(Guid aufgabeId)
    {
        lock (_lock)
        {
            return _scheduledPrompts.TryGetValue(aufgabeId, out var entry) ? entry.Info : null;
        }
    }

    private ITimer? RemoveEntry(Guid aufgabeId)
    {
        lock (_lock)
        {
            return _scheduledPrompts.Remove(aufgabeId, out var entry) ? entry.Timer : null;
        }
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
                    entry.Timer.Change(bis - now, Timeout.InfiniteTimeSpan);
                    neuEingeplant = true;
                }
            }

            if (neuEingeplant)
            {
                _logger.LogInformation(
                    "Zeitgesteuerter Prompt für Aufgabe {AufgabeId} auf das Pausenende {PausiertBisUtc} verschoben.",
                    aufgabeId,
                    bis);
            }
            else
            {
                entry.Timer.Dispose();
            }

            return;
        }

        entry.Timer.Dispose();
        await SendPromptAsync(aufgabeId, entry.Info.PromptText);
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

    private async Task SendPromptAsync(Guid aufgabeId, string promptText)
    {
        var session = _kiService.GetPseudoConsoleSession(aufgabeId);
        if (session is null)
        {
            _logger.LogWarning(
                "Zeitgesteuerter Prompt für Aufgabe {AufgabeId} konnte nicht versendet werden, da keine aktive CLI-Session vorhanden ist.",
                aufgabeId);
            return;
        }

        try
        {
            await session.WritePromptAsync(promptText, CancellationToken.None);
            PromptSent?.Invoke(aufgabeId);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogDebug(
                ex,
                "Zeitgesteuerter Prompt für Aufgabe {AufgabeId} konnte nicht versendet werden, da die Session zwischenzeitlich disposed wurde.",
                aufgabeId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Zeitgesteuerter Prompt für Aufgabe {AufgabeId} konnte nicht versendet werden.", aufgabeId);
        }
    }
}
