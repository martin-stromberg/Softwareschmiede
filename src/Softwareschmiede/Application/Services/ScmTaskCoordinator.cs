using System.Collections.Concurrent;

namespace Softwareschmiede.Application.Services;

/// <summary>Gemeinsame Serialisierung von SCM-Dispatch, manuellen Starts und Terminaleingaben.</summary>
public sealed class ScmTaskCoordinator
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();
    private static readonly AsyncLocal<IReadOnlySet<Guid>?> OwnedTasks = new();

    /// <summary>Prozessweite Instanz; auch DI-Instanzen teilen denselben Sperrzustand.</summary>
    public static ScmTaskCoordinator Shared { get; } = new();

    /// <summary>Führt eine Operation unter der Aufgabensperre aus; verschachtelte Serviceaufrufe behalten den Besitzer.</summary>
    public async Task<T> RunAsync<T>(Guid taskId, Func<Task<T>> operation, CancellationToken ct = default)
    {
        if (OwnedTasks.Value?.Contains(taskId) == true) return await operation().ConfigureAwait(false);
        var gate = Gates.GetOrAdd(taskId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        var previous = OwnedTasks.Value;
        OwnedTasks.Value = new HashSet<Guid>(previous ?? new HashSet<Guid>()) { taskId };
        try { return await operation().ConfigureAwait(false); }
        finally { OwnedTasks.Value = previous; gate.Release(); }
    }

    /// <summary>Führt eine Operation ohne Rückgabewert unter derselben Aufgabensperre aus.</summary>
    public Task RunAsync(Guid taskId, Func<Task> operation, CancellationToken ct = default)
        => RunAsync(taskId, async () => { await operation().ConfigureAwait(false); return true; }, ct);
}
