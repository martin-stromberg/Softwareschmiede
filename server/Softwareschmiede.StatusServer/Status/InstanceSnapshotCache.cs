using System.Collections.Concurrent;
using Softwareschmiede.StatusContracts;

namespace Softwareschmiede.StatusServer.Status;

/// <summary>In-Memory-Cache der zuletzt gemeldeten Status-Snapshots je Instanz.</summary>
public sealed class InstanceSnapshotCache
{
    /// <summary>Standard-Aufbewahrungszeit getrennter Instanz-Records, bis sie aus dem
    /// Cache entfernt werden (Schutz vor unbegrenztem Wachstum durch Fremd-Clients).</summary>
    public static readonly TimeSpan DefaultDisconnectedRetention = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<string, InstanceRecord> _records = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _staleAfter;
    private readonly TimeSpan _disconnectedRetention;

    /// <summary>Erstellt den Cache.</summary>
    /// <param name="timeProvider">Die Zeitquelle für Stale-Berechnungen.</param>
    /// <param name="staleAfter">Zeitraum, nach dem ein Snapshot als veraltet gilt.</param>
    /// <param name="disconnectedRetention">Aufbewahrungszeit getrennter Records
    /// (Standard: <see cref="DefaultDisconnectedRetention"/>).</param>
    public InstanceSnapshotCache(
        TimeProvider timeProvider,
        TimeSpan staleAfter,
        TimeSpan? disconnectedRetention = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (staleAfter <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(staleAfter));

        _timeProvider = timeProvider;
        _staleAfter = staleAfter;
        _disconnectedRetention = disconnectedRetention ?? DefaultDisconnectedRetention;
    }

    /// <summary>Markiert eine Instanz als verbunden (ohne Snapshot).</summary>
    /// <param name="connectionId">Die SignalR-Verbindungs-ID.</param>
    /// <param name="instanceId">Die Instanz-ID der App.</param>
    /// <param name="instanceName">Der optionale Anzeigename.</param>
    public void MarkConnected(string connectionId, string instanceId, string? instanceName)
    {
        var now = _timeProvider.GetUtcNow();
        // Eine Verbindung gehört höchstens einer Instanz: Wechselt eine Verbindung per
        // erneutem RegisterInstance die Instanz-ID, darf die alte connectionId die alte
        // Instanz nicht fälschlich als verbunden markieren.
        RemoveConnectionIdFromAll(connectionId, exceptInstanceId: instanceId);
        EvictDisconnected(now);

        var record = _records.GetOrAdd(instanceId, _ => new InstanceRecord());
        lock (record.Sync)
        {
            record.ConnectionIds.Add(connectionId);
            record.InstanceName = instanceName ?? record.InstanceName;
            record.LastSeenUtc = now;
            record.RegisteredUtc ??= now;
        }
    }

    /// <summary>Entfernt eine Verbindung; bei der letzten Verbindung bleibt der Snapshot für die Seite erhalten.</summary>
    /// <param name="connectionId">Die SignalR-Verbindungs-ID.</param>
    public void MarkDisconnected(string connectionId)
    {
        RemoveConnectionIdFromAll(connectionId, exceptInstanceId: null);
    }

    /// <summary>Speichert einen neuen Snapshot einer Instanz.</summary>
    /// <param name="instanceId">Die Instanz-ID.</param>
    /// <param name="snapshot">Der empfangene Snapshot.</param>
    public void UpdateSnapshot(string instanceId, StatusSnapshot snapshot)
    {
        var record = _records.GetOrAdd(instanceId, _ => new InstanceRecord());
        lock (record.Sync)
        {
            record.Snapshot = snapshot with { InstanceId = instanceId };
            record.InstanceName = snapshot.InstanceName ?? record.InstanceName;
            record.LastSeenUtc = _timeProvider.GetUtcNow();
            record.RegisteredUtc ??= record.LastSeenUtc;
        }
    }

    /// <summary>Liefert eine Momentaufnahme aller Instanzen inkl. Verbindungs- und Veraltungsstatus.</summary>
    /// <returns>Die Instanzen, sortiert nach Anzeigename/ID.</returns>
    public IReadOnlyList<InstanceSnapshotView> GetAll()
    {
        var now = _timeProvider.GetUtcNow();
        EvictDisconnected(now);

        var result = new List<InstanceSnapshotView>(_records.Count);
        foreach (var (instanceId, record) in _records)
        {
            lock (record.Sync)
            {
                var connected = record.ConnectionIds.Count > 0;
                var staleAfterSeconds = record.LastSeenUtc is DateTimeOffset lastSeen
                    ? (now - lastSeen).TotalSeconds
                    : double.PositiveInfinity;
                var isStale = !connected || staleAfterSeconds > _staleAfter.TotalSeconds;
                result.Add(new InstanceSnapshotView(
                    instanceId,
                    record.InstanceName,
                    record.Snapshot,
                    connected,
                    record.RegisteredUtc,
                    record.LastSeenUtc,
                    isStale));
            }
        }

        return result
            .OrderBy(i => i.InstanceName ?? i.InstanceId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RemoveConnectionIdFromAll(string connectionId, string? exceptInstanceId)
    {
        foreach (var (instanceId, record) in _records)
        {
            if (instanceId == exceptInstanceId)
                continue;

            lock (record.Sync)
                record.ConnectionIds.Remove(connectionId);
        }
    }

    /// <summary>Entfernt Records, die seit länger als <see cref="_disconnectedRetention"/>
    /// ohne Verbindung sind (gebundenes Wachstum; erneute Registrierung legt sie neu an).</summary>
    private void EvictDisconnected(DateTimeOffset now)
    {
        foreach (var (instanceId, record) in _records)
        {
            lock (record.Sync)
            {
                if (record.ConnectionIds.Count > 0)
                    continue;

                var cutoff = record.LastSeenUtc ?? record.RegisteredUtc;
                if (cutoff is null || now - cutoff > _disconnectedRetention)
                    _records.TryRemove(instanceId, out _);
            }
        }
    }

    private sealed class InstanceRecord
    {
        public object Sync { get; } = new();
        public HashSet<string> ConnectionIds { get; } = new(StringComparer.Ordinal);
        public string? InstanceName { get; set; }
        public StatusSnapshot? Snapshot { get; set; }
        public DateTimeOffset? RegisteredUtc { get; set; }
        public DateTimeOffset? LastSeenUtc { get; set; }
    }
}

/// <summary>Read-Only-Sicht einer Instanz für Seite und JSON-Endpunkt.</summary>
/// <param name="InstanceId">Die Instanz-ID.</param>
/// <param name="InstanceName">Der optionale Anzeigename.</param>
/// <param name="Snapshot">Der letzte Snapshot; <see langword="null"/>, solange noch keiner empfangen wurde.</param>
/// <param name="Connected"><see langword="true"/>, solange mindestens eine Relay-Verbindung besteht.</param>
/// <param name="RegisteredUtc">Erster Registrierungszeitpunkt der Instanz in diesem Prozess (UTC).</param>
/// <param name="LastSeenUtc">Letzter bekannter Lebenszeitpunkt (UTC).</param>
/// <param name="IsStale"><see langword="true"/>, wenn die Instanz getrennt ist oder der Snapshot veraltet ist.</param>
public sealed record InstanceSnapshotView(
    string InstanceId,
    string? InstanceName,
    StatusSnapshot? Snapshot,
    bool Connected,
    DateTimeOffset? RegisteredUtc,
    DateTimeOffset? LastSeenUtc,
    bool IsStale);
