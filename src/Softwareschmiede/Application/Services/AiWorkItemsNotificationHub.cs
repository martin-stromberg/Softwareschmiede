using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Application.Services;

/// <summary>In-Memory-Hub für Abschlussereignisse von KI-Aufgaben.</summary>
public sealed class AiWorkItemsNotificationHub
{
    private readonly ConcurrentDictionary<Guid, Func<AiWorkItemCompletionEvent, Task>> _subscriber = new();
    private readonly ILogger<AiWorkItemsNotificationHub> _logger;

    /// <inheritdoc cref="AiWorkItemsNotificationHub"/>
    public AiWorkItemsNotificationHub(ILogger<AiWorkItemsNotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>Registriert einen Callback für KI-Aufgaben-Abschlussereignisse.</summary>
    public IDisposable Subscribe(Func<AiWorkItemCompletionEvent, Task> callback)
    {
        var id = Guid.NewGuid();
        _subscriber[id] = callback;
        return new Subscription(() => _subscriber.TryRemove(id, out _));
    }

    /// <summary>Verteilt ein KI-Aufgaben-Abschlussereignis an alle Abonnenten.</summary>
    public async Task PublishAsync(AiWorkItemCompletionEvent evt)
    {
        var snapshot = _subscriber.Values.ToArray();
        foreach (var callback in snapshot)
        {
            try
            {
                await callback(evt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fehler beim Verteilen eines KI-Abschlussereignisses für Aufgabe {AufgabeId}.", evt.WorkItemId);
            }
        }
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        public void Dispose()
        {
            onDispose();
        }
    }
}
