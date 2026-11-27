using System.Collections.Concurrent;

namespace Softwareschmiede.StatusServer.Security;

/// <summary>Kurzlebiger In-Memory-Puffer für den Klartext frisch angelegter
/// Relay-Tokens. Der <c>POST /admin/tokens</c>-Handler kann nicht direkt auf die
/// Token-Liste weiterleiten, weil der Klartext nur einmalig angezeigt werden darf
/// und nicht in der Datenbank liegt: Stattdessen wird der Klartext hier unter einem
/// unerratenen Schlüssel abgelegt und die Antwort leitet auf
/// <c>GET /admin/tokens?created={key}</c> um, wo <see cref="Take"/> ihn genau einmal
/// ausliefert. Ein Refresh zeigt danach einen Hinweis statt eines zweiten Tokens
/// (Post/Redirect/Get — kein Duplikat bei F5).</summary>
public sealed class RelayTokenPlaintextStore
{
    private static readonly TimeSpan s_entryLifetime = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();
    private readonly TimeProvider _timeProvider;

    /// <summary>Erstellt den Store.</summary>
    /// <param name="timeProvider">Die Zeitquelle.</param>
    public RelayTokenPlaintextStore(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>Hält einen Klartext unter einem neuen Schlüssel für die einmalige
    /// Anzeige bereit.</summary>
    /// <param name="plaintext">Der Klartext des neuen Tokens.</param>
    /// <returns>Der Schlüssel für <see cref="Take"/>.</returns>
    public Guid Store(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        PurgeExpired();
        var key = Guid.NewGuid();
        _entries[key] = new Entry(plaintext, _timeProvider.GetUtcNow() + s_entryLifetime);
        return key;
    }

    /// <summary>Liefert den zum Schlüssel gehörenden Klartext genau einmal —
    /// der Eintrag wird dabei entfernt.</summary>
    /// <param name="key">Der Schlüssel aus <see cref="Store"/>.</param>
    /// <returns>Der Klartext oder <see langword="null"/>, wenn der Schlüssel
    /// unbekannt, bereits abgerufen oder abgelaufen ist.</returns>
    public string? Take(Guid key)
    {
        PurgeExpired();
        return _entries.TryRemove(key, out var entry) && entry.ExpiresUtc > _timeProvider.GetUtcNow()
            ? entry.Plaintext
            : null;
    }

    private void PurgeExpired()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var pair in _entries)
        {
            if (pair.Value.ExpiresUtc <= now)
                _entries.TryRemove(pair.Key, out _);
        }
    }

    private sealed record Entry(string Plaintext, DateTimeOffset ExpiresUtc);
}
