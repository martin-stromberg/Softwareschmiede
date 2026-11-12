using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Gemeinsame Checkpoint-Verwaltung der zyklischen SCM-Beobachtungsdienste:
/// Fehleraufzeichnung mit Backoff und repository-weite Enumeration mit Watermark.</summary>
internal static class ScmCheckpointSupport
{
    /// <summary>Speichert einen Quellenfehler samt Diagnose und nächstem Prüfzeitpunkt.</summary>
    /// <param name="scopes">Scope-Factory für einen eigenen Datenbankkontext.</param>
    /// <param name="time">Zeitquelle.</param>
    /// <param name="sourceKey">Der Checkpoint-Schlüssel der Quelle.</param>
    /// <param name="error">Der aufgetretene Fehler.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <param name="diagnosticSuffix">Ergänzender Diagnosetext.</param>
    /// <param name="retryPermanent">Ob auch „Permanent“-Fehler per Backoff statt dauerhaft gesperrt werden.</param>
    internal static async Task RecordFailureAsync(IServiceScopeFactory scopes, TimeProvider time,
        string sourceKey, Exception error, CancellationToken ct,
        string diagnosticSuffix = "", bool retryPermanent = false)
    {
        using var failureScope = scopes.CreateScope();
        var failureDb = failureScope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var checkpoint = await failureDb.ScmObservationCheckpoints.FindAsync([sourceKey], ct);
        if (checkpoint is null)
        {
            checkpoint = new ScmObservationCheckpoint { SourceKey = sourceKey };
            failureDb.Add(checkpoint);
        }
        ApplyFailure(checkpoint, error,
            ((error as ScmProviderException)?.Message ?? "Quelle konnte nicht vollständig gelesen werden.") + diagnosticSuffix,
            time.GetUtcNow().ToUnixTimeMilliseconds(), retryPermanent);
        await failureDb.SaveChangesAsync(ct);
        failureScope.ServiceProvider.GetRequiredService<ScmEventChangeNotifier>().Notify();
    }

    /// <summary>Schreibt einen Fehler auf einem bereits getrackten Checkpoint fort:
    /// Fehlerzähler, Fehlercode, Diagnose und nächster Prüfzeitpunkt (Backoff bzw. dauerhafte
    /// Sperre bei Authentifizierungs-/Berechtigungs- und — ohne <paramref name="retryPermanent"/> —
    /// Permanent-Fehlern).</summary>
    /// <param name="checkpoint">Der zu aktualisierende Checkpoint.</param>
    /// <param name="error">Der aufgetretene Fehler.</param>
    /// <param name="diagnostic">Der vollständige Diagnosetext.</param>
    /// <param name="nowUtcMs">Der aktuelle Zeitpunkt in Unix-Millisekunden.</param>
    /// <param name="retryPermanent">Ob auch „Permanent“-Fehler per Backoff statt dauerhaft gesperrt werden.</param>
    internal static void ApplyFailure(ScmObservationCheckpoint checkpoint, Exception error,
        string diagnostic, long nowUtcMs, bool retryPermanent = false)
    {
        checkpoint.Failures++;
        var failure = error as ScmProviderException;
        checkpoint.ErrorCode = failure?.FailureKind.ToString() ?? "Temporary";
        checkpoint.Diagnostic = diagnostic;
        var blockedForever = failure?.FailureKind is ScmProviderFailureKind.Authentication or ScmProviderFailureKind.Permission
            || (failure?.FailureKind is ScmProviderFailureKind.Permanent && !retryPermanent);
        checkpoint.NextAttemptUtc = blockedForever
            ? long.MaxValue : ScmEventStore.RetryAt(checkpoint.Failures, nowUtcMs, failure?.RetryAfterUtc);
    }

    /// <summary>Enumeriert ein kanonisch aufgelöstes Repository einmal pro Lauf und verwaltet dessen
    /// eigenen Watermark-/Backoff-Checkpoint unter <paramref name="checkpointKeyPrefix"/>/<c>{provider}/{host}/{repoId}</c>.
    /// Enumerationsfehler werden als Checkpoint-Fehler aufgezeichnet und isoliert.</summary>
    /// <param name="scopes">Scope-Factory für einen eigenen Datenbankkontext.</param>
    /// <param name="time">Zeitquelle.</param>
    /// <param name="pollInterval">Das konfigurierte Poll-Intervall (zusätzlicher Lookback zur Watermark).</param>
    /// <param name="source">Die Ereignisquelle des Plugins.</param>
    /// <param name="repository">Das kanonisch aufgelöste Ereignis-Repository.</param>
    /// <param name="checkpointKeyPrefix">Das Schlüssel-Präfix des Enumerations-Checkpoints.</param>
    /// <param name="enumerated">Bereits in diesem Lauf enumerierte Checkpoint-Schlüssel (Dedup).</param>
    /// <param name="verarbeite">Verarbeitung der enumerierten Referenzen; läuft innerhalb des
    /// Watermark-Commits — Fehler verhindern den Commit und gelten als Enumerationsfehler.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns><see langword="false"/>, wenn die Enumeration fehlschlug (Fehler wurde aufgezeichnet).</returns>
    internal static async Task<bool> EnumerateRepositoryAsync(
        IServiceScopeFactory scopes, TimeProvider time, TimeSpan pollInterval,
        IScmEventSource source, ScmEventRepository repository, string checkpointKeyPrefix,
        HashSet<string> enumerated,
        Func<IReadOnlyList<ScmEventReference>, CancellationToken, Task> verarbeite,
        CancellationToken ct)
    {
        var enumerationKey = string.Join('/', new[] { checkpointKeyPrefix, repository.ProviderPrefix.ToLowerInvariant(),
            repository.Host.ToLowerInvariant(), repository.RepositoryId }.Select(Uri.EscapeDataString));
        if (!enumerated.Add(enumerationKey)) return true;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        var checkpoint = await db.ScmObservationCheckpoints.FindAsync([enumerationKey], ct);
        var now = time.GetUtcNow();
        if (checkpoint?.NextAttemptUtc > now.ToUnixTimeMilliseconds()) return true;
        try
        {
            var sinceUtc = checkpoint?.WatermarkUtc is long watermark
                ? DateTimeOffset.FromUnixTimeMilliseconds(watermark) - pollInterval * 2
                : (DateTimeOffset?)null;
            var discovered = await source.EnumerateReferencesAsync(repository, sinceUtc, ct);
            await verarbeite(discovered, ct);
            if (checkpoint is null)
            {
                checkpoint = new ScmObservationCheckpoint { SourceKey = enumerationKey };
                db.Add(checkpoint);
            }
            checkpoint.WatermarkUtc = now.ToUnixTimeMilliseconds();
            checkpoint.Failures = 0;
            checkpoint.NextAttemptUtc = null;
            checkpoint.ErrorCode = null;
            checkpoint.Diagnostic = null;
            await db.SaveChangesAsync(ct);
            scope.ServiceProvider.GetRequiredService<ScmEventChangeNotifier>().Notify();
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            await RecordFailureAsync(scopes, time, enumerationKey, ex, ct);
            return false;
        }
    }
}
