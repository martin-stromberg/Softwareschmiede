using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Singleton-Service, der laufende KI-Ausführungen verwaltet.
/// Ermöglicht Hintergrundausführung unabhängig von der Blazor-Komponenten-Lebensdauer:
/// Der Anwender kann wegnavigieren und zurückkehren – der Lauf läuft weiter und
/// der Puffer der bisherigen Ausgabe ist noch verfügbar.
/// </summary>
/// <remarks>
/// Ownership der Sessions: Dieser Service ist Singleton und hält alle aktiven <see cref="KiSession"/>-Objekte.
/// Abgeschlossene Sessions bleiben nach Ende kurzzeitig erhalten (bis die Komponente die Daten gelesen hat)
/// und werden beim nächsten Laden der Seite bereinigt.
/// </remarks>
public sealed class KiAusfuehrungsService
{
    private readonly ConcurrentDictionary<Guid, KiSession> _sessions = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KiAusfuehrungsService> _logger;

    /// <summary>Erstellt eine neue Instanz des <see cref="KiAusfuehrungsService"/>.</summary>
    public KiAusfuehrungsService(IServiceScopeFactory scopeFactory, ILogger<KiAusfuehrungsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Gibt an, ob für die Aufgabe aktuell eine KI-Ausführung läuft.</summary>
    public bool IsRunning(Guid aufgabeId)
        => _sessions.TryGetValue(aufgabeId, out var session) && session.IsRunning;

    /// <summary>
    /// Gibt alle bisher gepufferten Ausgabezeilen einer laufenden oder gerade beendeten Session zurück.
    /// Gibt eine leere Liste zurück, wenn keine Session existiert.
    /// </summary>
    public IReadOnlyList<string> GetBufferedLines(Guid aufgabeId)
        => _sessions.TryGetValue(aufgabeId, out var session)
            ? session.GetLines()
            : [];

    /// <summary>
    /// Abonniert neue Ausgabezeilen einer laufenden Session.
    /// Das zurückgegebene <see cref="IDisposable"/> beendet das Abonnement beim Dispose.
    /// </summary>
    /// <param name="aufgabeId">Aufgabe, auf die abonniert werden soll.</param>
    /// <param name="onLine">Callback, der für jede neue Zeile aufgerufen wird.</param>
    /// <returns>IDisposable zum Beenden des Abonnements – muss beim Component-Dispose aufgerufen werden.</returns>
    public IDisposable? Subscribe(Guid aufgabeId, Action<string> onLine)
    {
        if (!_sessions.TryGetValue(aufgabeId, out var session) || !session.IsRunning)
        {
            return null;
        }

        return session.Subscribe(onLine);
    }

    /// <summary>
    /// Startet einen KI-Lauf im Hintergrund.
    /// Kehrt sofort zurück – der eigentliche Lauf findet in einem Background-Task statt.
    /// </summary>
    /// <param name="aufgabeId">Aufgabe, für die die KI gestartet wird.</param>
    /// <param name="prompt">Anforderungs-Prompt des Anwenders.</param>
    /// <param name="agent">Gewählter Agent.</param>
    /// <param name="model">Optionales KI-Modell.</param>
    /// <param name="onStarted">Optionaler Callback nach erfolgreicher Initialisierung (Status gesetzt).</param>
    /// <param name="onCompleted">Optionaler Callback nach Abschluss oder Fehler.</param>
    public void StartKiLauf(
        Guid aufgabeId,
        string prompt,
        AgentInfo agent,
        string? model = null,
        FolgeanweisungsKontextmodus? kontextmodus = null,
        Action? onStarted = null,
        Action? onStatus = null,
        Action<bool>? onCompleted = null)
    {
        if (IsRunning(aufgabeId))
        {
            _logger.LogWarning("KI-Lauf für Aufgabe {AufgabeId} läuft bereits – zweiter Start abgewiesen.", aufgabeId);
            return;
        }

        var session = new KiSession();
        _sessions[aufgabeId] = session;

        _logger.LogInformation("KI-Hintergrundlauf für Aufgabe {AufgabeId} mit Agent {AgentName} gestartet.", aufgabeId, agent.Name);


        var completed = false;
        var statusChanged = false;
        var statusEntryCount = 0;
        var maxStatusCount = 10;
        var lastStatusEvent = DateTime.MinValue;
        var statusInterval = TimeSpan.FromSeconds(10);
        _ = Task.Run(async () =>
        {
            while (!completed)
            {
                await Task.Delay(100);

                if (!statusChanged)
                    return;
                if (lastStatusEvent.Add(statusInterval) > DateTime.MinValue && (statusEntryCount < maxStatusCount))
                    return;
                lastStatusEvent = DateTime.MinValue;
                statusEntryCount = 0;
                statusChanged = false;
                onStatus?.Invoke();
            }
        });

        // Hintergrund-Task: eigener Scope für Scoped-Services (EntwicklungsprozessService, DbContext etc.)
        _ = Task.Run(async () =>
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var entwicklungsprozessService = scope.ServiceProvider.GetRequiredService<EntwicklungsprozessService>();

            bool fehler = false;
            try
            {
                // Scoped Service im Background-Task-Scope ausführen
                await foreach (var line in entwicklungsprozessService.KiStartenAsync(
                    aufgabeId, prompt, agent, model, kontextmodus, session.CancellationToken))
                {
                    var hasJustStarted = !session.GetLines().Any();
                    session.AddLine(line);

                    // Einmalig nach der ersten Zeile den "gestartet"-Callback auslösen
                    if (hasJustStarted)
                    {
                        onStarted?.Invoke();
                    }
                    statusEntryCount += 1;
                    statusChanged = true;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("KI-Lauf für Aufgabe {AufgabeId} wurde abgebrochen.", aufgabeId);
                fehler = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler im KI-Hintergrundlauf für Aufgabe {AufgabeId}.", aufgabeId);
                session.AddLine($"[Fehler] {ex.Message}");
                fehler = true;
            }
            finally
            {
                completed = true;
                session.Complete(fehler);
                onCompleted?.Invoke(fehler);
                _logger.LogInformation("KI-Hintergrundlauf für Aufgabe {AufgabeId} beendet (Fehler: {Fehler}).", aufgabeId, fehler);
            }
        });
    }

    /// <summary>Bricht den laufenden KI-Lauf für die angegebene Aufgabe ab.</summary>
    public void AbortKiLauf(Guid aufgabeId)
    {
        if (_sessions.TryGetValue(aufgabeId, out var session))
        {
            session.Cancel();
        }
    }

    /// <summary>Entfernt eine beendete Session aus dem Puffer (Cleanup nach Seiten-Reload).</summary>
    public void SessionBereinigen(Guid aufgabeId)
    {
        if (_sessions.TryGetValue(aufgabeId, out var session) && !session.IsRunning)
        {
            _sessions.TryRemove(aufgabeId, out _);
        }
    }
}

/// <summary>
/// Repräsentiert eine einzelne laufende oder gerade beendete KI-Ausführung.
/// Thread-sicher: alle Zugriffe auf <see cref="_lines"/> und <see cref="_subscribers"/> sind durch einen Lock geschützt.
/// </summary>
internal sealed class KiSession : IDisposable
{
    private static readonly TimeSpan BlockPause = TimeSpan.FromSeconds(2);
    private readonly Func<DateTimeOffset> _nowProvider;
    private readonly List<string> _lines = [];
    private readonly List<Action<string>> _subscribers = [];
    private readonly Lock _lock = new();
    private readonly CancellationTokenSource _cts = new();
    private DateTimeOffset? _lastLineAt;

    /// <summary>Gibt an, ob die Session noch aktiv ist.</summary>
    public bool IsRunning { get; private set; } = true;

    /// <summary>Gibt an, ob die Session mit einem Fehler beendet wurde.</summary>
    public bool IsError { get; private set; }

    /// <summary>CancellationToken für den Background-Task.</summary>
    public CancellationToken CancellationToken => _cts.Token;

    internal KiSession(Func<DateTimeOffset>? nowProvider = null)
    {
        _nowProvider = nowProvider ?? (() => DateTimeOffset.Now);
    }

    /// <summary>Gibt eine Kopie der bisher gepufferten Ausgabezeilen zurück.</summary>
    public IReadOnlyList<string> GetLines()
    {
        lock (_lock)
        {
            return [.. _lines];
        }
    }

    /// <summary>Fügt eine Ausgabezeile hinzu und benachrichtigt alle Subscriber.</summary>
    public void AddLine(string line)
    {
        var now = _nowProvider();
        List<Action<string>> subscribers;
        List<string> additionalLines = new List<string>
        lock (_lock)
        {
            if (_lastLineAt is null || now - _lastLineAt >= BlockPause)
            {
                additionalLines.Add(string.Empty);
                additionalLines.Add(now.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"));
                _lines.AddRange(additionalLines);
            }

            _lines.Add(line);
            _lastLineAt = now;
            subscribers = [.. _subscribers];
        }

        // Subscriber außerhalb des Locks aufrufen, um Deadlocks zu vermeiden
        foreach (var subscriber in subscribers)
        {
            try
            {
                foreach (var addLine in additionalLines)
                {
                    subscriber(addLine);
                }
                subscriber(line);
            }
            catch (Exception)
            {
                // Subscriber-Fehler sollen die Session nicht abbrechen
            }
        }
    }

    /// <summary>Registriert einen Subscriber für neue Ausgabezeilen.</summary>
    /// <param name="callback">Callback, der für jede neue Zeile aufgerufen wird.</param>
    /// <returns>IDisposable zum Beenden des Abonnements.</returns>
    public IDisposable Subscribe(Action<string> callback)
    {
        lock (_lock)
        {
            _subscribers.Add(callback);
        }

        return new Unsubscriber(() =>
        {
            lock (_lock)
            {
                _subscribers.Remove(callback);
            }
        });
    }

    /// <summary>Markiert die Session als abgeschlossen.</summary>
    public void Complete(bool fehler)
    {
        lock (_lock)
        {
            IsRunning = false;
            IsError = fehler;
            _subscribers.Clear();
        }
    }

    /// <summary>Bricht den Background-Task ab.</summary>
    public void Cancel() => _cts.Cancel();

    /// <inheritdoc/>
    public void Dispose() => _cts.Dispose();

    private sealed class Unsubscriber(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
