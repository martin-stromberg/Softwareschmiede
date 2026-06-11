using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Softwareschmiede.Application.Services;

/// <summary>In-Memory-Hub für Abschlussereignisse von KI-Aufgaben.</summary>
public sealed class KiAufgabenBenachrichtigungsHub
{
    private readonly ConcurrentDictionary<Guid, Func<KiAufgabenAbschlussEreignis, Task>> _subscriber = new();
    private readonly ILogger<KiAufgabenBenachrichtigungsHub> _logger;

    /// <inheritdoc cref="KiAufgabenBenachrichtigungsHub"/>
    public KiAufgabenBenachrichtigungsHub(ILogger<KiAufgabenBenachrichtigungsHub> logger)
    {
        _logger = logger;
    }

    public IDisposable Subscribe(Func<KiAufgabenAbschlussEreignis, Task> callback)
    {
        var id = Guid.NewGuid();
        _subscriber[id] = callback;
        return new Subscription(() => _subscriber.TryRemove(id, out _));
    }

    public async Task PublishAsync(KiAufgabenAbschlussEreignis ereignis)
    {
        var snapshot = _subscriber.Values.ToArray();
        foreach (var callback in snapshot)
        {
            try
            {
                await callback(ereignis);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fehler beim Verteilen eines KI-Abschlussereignisses für Aufgabe {AufgabeId}.", ereignis.AufgabeId);
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
