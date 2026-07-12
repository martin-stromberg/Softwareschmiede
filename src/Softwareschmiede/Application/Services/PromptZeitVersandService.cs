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
    private readonly Dictionary<Guid, ScheduledPromptEntry> _scheduledPrompts = new();
    private readonly object _lock = new();

    /// <inheritdoc cref="PromptZeitVersandService"/>
    public PromptZeitVersandService(KiAusfuehrungsService kiService, TimeProvider timeProvider, ILogger<PromptZeitVersandService> logger)
    {
        _kiService = kiService;
        _timeProvider = timeProvider;
        _logger = logger;
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
        ScheduledPromptInfo? info;
        lock (_lock)
        {
            if (!_scheduledPrompts.Remove(aufgabeId, out var entry))
                return;
            info = entry.Info;
            entry.Timer.Dispose();
        }

        await SendPromptAsync(aufgabeId, info.PromptText);
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
