using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Atomare Inboxaufnahme, Wiederholungsplanung und instanzübergreifende Aufgabenleases.</summary>
public sealed class ScmEventStore(SoftwareschmiededDbContext db, ScmEventChangeNotifier notifier, TimeProvider time)
{
    /// <summary>Diagnose für Referenzen, die mehreren Aufgaben zugeordnet werden könnten.</summary>
    public const string AmbiguousAssignmentDiagnostic = "Mehrdeutige Zuordnung zu Aufgaben";
    /// <summary>Diagnose für Referenzen ohne zuordenbare lokale Aufgabe.</summary>
    public const string UnassignedDiagnostic = "Keine Aufgabe zugeordnet";
    /// <summary>Serialisierungsregeln für die unveränderlichen Providerdaten.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    /// <summary>Liest den unveränderlichen Ursprung.</summary>
    public static ScmEventObservation Observation(ScmIncomingEvent item)
        => JsonSerializer.Deserialize<ScmEventObservation>(item.ObservationJson, JsonOptions)!;
    /// <summary>Deterministische ID einer Hostaktion.</summary>
    public static Guid ActionId(Guid eventId, string purpose)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes($"{eventId:D}:{purpose}")).AsSpan(0, 16));
    /// <summary>Wiederholungsverzögerung mit beschränktem Jitter.</summary>
    public static long RetryAt(int failures, long now, DateTimeOffset? retryAfter = null)
        => retryAfter?.ToUnixTimeMilliseconds() ?? now + (failures switch { <= 1 => 30_000L, 2 => 120_000L, 3 => 600_000L, _ => 1_800_000L }) + Random.Shared.Next(0, 3000);

    /// <summary>Speichert eine vollständig gelesene Quelle und ihren Watermark gemeinsam.</summary>
    /// <param name="aufgabeId">Die eindeutig zugeordnete Aufgabe oder <see langword="null"/> für unzugeordnete Quellen.</param>
    /// <param name="sourceKey">Der Checkpoint-Schlüssel der Quelle.</param>
    /// <param name="batch">Der gelesene Batch samt Anfrage-Metadaten.</param>
    /// <param name="unassigned">Kennzeichnet, ob die Quelle keiner lokalen Aufgabe zugeordnet ist.</param>
    /// <param name="triggerKeyword">Das aktuell konfigurierte Trigger-Keyword für die
    /// Ereignisvalidierung; leere Werte fallen auf den Standard zurück.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public async Task AcceptBatchAsync(Guid? aufgabeId, string sourceKey, ScmEventBatch batch,
        bool unassigned = false, string? triggerKeyword = null, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var checkpoint = await db.ScmObservationCheckpoints.FindAsync([sourceKey], ct);
        var visibleChange = checkpoint?.Diagnostic is not null;
        if (checkpoint is null)
        {
            checkpoint = new ScmObservationCheckpoint { SourceKey = sourceKey };
            db.Add(checkpoint);
        }
        var reference = batch.Request.Reference;
        var contentPrefix = string.Join('/', new[] { reference.Repository.ProviderPrefix.ToLowerInvariant(),
            reference.Repository.Host.ToLowerInvariant(), reference.Repository.RepositoryId, reference.Kind.ToString(),
            reference.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), batch.Request.ContentKind.ToString() }
            .Select(Uri.EscapeDataString)) + "/";
        var sourceSnapshots = await db.ScmContentSnapshots.Where(x => x.ContentKey.StartsWith(contentPrefix)).ToListAsync(ct);
        var unassignedDiagnostic = unassigned ? UnassignedDiagnostic : AmbiguousAssignmentDiagnostic;
        foreach (var observation in batch.Observations.OrderBy(x => x.Revision).ThenBy(x => x.EventKey, StringComparer.Ordinal))
        {
            var snapshot = await db.ScmContentSnapshots.FindAsync([observation.ContentKey], ct);
            var rejection = aufgabeId is null ? unassignedDiagnostic : ScmEventValidationService.GetRejection(observation, triggerKeyword);
            var resolved = aufgabeId is not null && rejection != "Bearbeiter nicht bestimmbar";
            if (snapshot is not null && snapshot.BodyHash == observation.BodyHash && snapshot.IdentityResolved
                && snapshot.Diagnostic != AmbiguousAssignmentDiagnostic && snapshot.Diagnostic != UnassignedDiagnostic)
                continue;
            if (snapshot is null)
            {
                snapshot = new ScmContentSnapshot { ContentKey = observation.ContentKey };
                db.Add(snapshot);
                sourceSnapshots.Add(snapshot);
            }
            snapshot.BodyHash = observation.BodyHash;
            snapshot.Revision = observation.Revision.ToString("O");
            snapshot.IdentityResolved = resolved;
            snapshot.Diagnostic = rejection;
            if (rejection is not null || await db.ScmIncomingEvents.AnyAsync(x => x.EventKey == observation.EventKey, ct))
                continue;
            db.Add(new ScmIncomingEvent
            {
                AufgabeId = aufgabeId!.Value,
                EventKey = observation.EventKey,
                ObservationJson = JsonSerializer.Serialize(observation, JsonOptions),
                ReceivedUtc = time.GetUtcNow().ToUnixTimeMilliseconds()
            });
            visibleChange = true;
        }
        checkpoint.WatermarkUtc = batch.Request.WatermarkUtc.ToUnixTimeMilliseconds();
        checkpoint.Failures = 0;
        checkpoint.NextAttemptUtc = null;
        checkpoint.ErrorCode = null;
        checkpoint.Diagnostic = aufgabeId is null ? unassignedDiagnostic
            : sourceSnapshots.Any(x => x.Diagnostic == AmbiguousAssignmentDiagnostic) ? AmbiguousAssignmentDiagnostic
            : sourceSnapshots.Any(x => x.Diagnostic == UnassignedDiagnostic) ? UnassignedDiagnostic : null;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        if (visibleChange || checkpoint.Diagnostic is not null) notifier.Notify();
    }

    /// <summary>Beansprucht eine Aufgabe atomar für einen Prozess.</summary>
    public async Task<bool> ClaimTaskAsync(Guid taskId, string owner, CancellationToken ct)
    {
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        var until = now + 60_000;
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ScmTaskLeases (AufgabeId, Owner, ExpiresUtc) VALUES ({taskId}, {owner}, {until}) ON CONFLICT(AufgabeId) DO UPDATE SET Owner = {owner}, ExpiresUtc = {until} WHERE ExpiresUtc < {now} OR Owner = {owner}", ct);
        return changed == 1;
    }
    /// <summary>Verlängert ausschließlich die eigene Lease.</summary>
    public Task<int> RenewTaskAsync(Guid taskId, string owner, CancellationToken ct)
        => db.ScmTaskLeases.Where(x => x.AufgabeId == taskId && x.Owner == owner)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.ExpiresUtc, time.GetUtcNow().ToUnixTimeMilliseconds() + 60_000), ct);
    /// <summary>Gibt ausschließlich die eigene Lease frei.</summary>
    public Task<int> ReleaseTaskAsync(Guid taskId, string owner, CancellationToken ct)
        => db.ScmTaskLeases.Where(x => x.AufgabeId == taskId && x.Owner == owner).ExecuteDeleteAsync(ct);
}
