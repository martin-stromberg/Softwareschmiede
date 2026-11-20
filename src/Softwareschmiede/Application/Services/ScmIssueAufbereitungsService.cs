using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Erkennt am repository-weiten Enumerationskanal neu angelegte Issues und reiht dafür
/// einen dedizierten, trigger-freien Aufbereitungsauftrag ein: eine als Zweck markierte Aufgabe mit
/// <see cref="IssueReference"/> im eigenen <c>IssueAufbereitung</c>-Namensraum plus ein internes
/// <see cref="ScmIncomingEvent"/>, das ohne Trigger-Validierung angelegt wird und sofort fällig
/// über den dedizierten Aufbereitungs-Ausführungspfad des Orchestrierers verarbeitet wird
/// (Entwicklungsschritt 6).</summary>
/// <param name="db">Datenbankkontext.</param>
/// <param name="workItems">Anlage der Aufbereitungs-Aufgabe samt SourceKey-Dedup.</param>
/// <param name="einstellungen">Persistierte Funktionskonfiguration (Plugin-Schlüssel, Kommentar-Schalter).</param>
/// <param name="notifier">Benachrichtigt Ansichten über dauerhafte Änderungen.</param>
/// <param name="time">Zeitquelle.</param>
/// <param name="logger">Protokoll.</param>
public sealed class ScmIssueAufbereitungsService(
    SoftwareschmiedeDbContext db, AufgabeService workItems, AppEinstellungService einstellungen,
    ScmEventChangeNotifier notifier, TimeProvider time, ILogger<ScmIssueAufbereitungsService> logger)
{
    /// <summary>Zweck-Markierung (<see cref="WorkItem.Purpose"/> bzw. <see cref="ScmIncomingEvent.Purpose"/>)
    /// der Issue-Aufbereitung; einziger Zweck-Token des Systems.</summary>
    public const string Purpose = "IssueAufbereitung";

    /// <summary>Checkpoint-Schlüssel der Erstbeobachtungs-Baseline eines Ereignis-Repositorys
    /// (eigener Namensraum neben den Quell-Checkpoints; wird einmalig gesetzt und nie fortgeschrieben).</summary>
    /// <param name="repository">Die kanonische Ereignis-Identität des Repositorys.</param>
    public static string BaselineKey(ScmEventRepository repository)
        => string.Join('/', new[] { "issueaufbereitung", repository.ProviderPrefix.ToLowerInvariant(),
            repository.Host.ToLowerInvariant(), repository.RepositoryId }.Select(Uri.EscapeDataString));

    /// <summary>SourceKey der Aufbereitungs-Aufgabe einer Issue-Referenz im eigenen
    /// <c>IssueAufbereitung</c>-Namensraum (Isolationsentscheidung: kollidiert niemals mit dem
    /// kanonischen <c>…/Issue/{number}</c>-Schlüssel und sichert die Einmaligkeit je Issue).</summary>
    /// <param name="reference">Die Issue-Referenz.</param>
    public static string AufbereitungsSourceKey(ScmEventReference reference)
        => string.Join('/', new[] { reference.Repository.ProviderPrefix.ToLowerInvariant(),
            reference.Repository.Host.ToLowerInvariant(), reference.Repository.RepositoryId,
            Purpose, reference.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) }
            .Select(Uri.EscapeDataString));

    /// <summary>Deterministischer, deduplizierender EventKey des internen Aufbereitungsauftrags:
    /// eindeutig je Issue-Beschreibung (ContentKey) und Zweck, unabhängig von Revision und BodyHash.</summary>
    /// <param name="observation">Die auslösende Beschreibungs-Beobachtung.</param>
    public static string AufbereitungsEventKey(ScmEventObservation observation)
        => $"{observation.ContentKey}/{Purpose}";

    /// <summary>Checkpoint-Schlüssel der abgeschlossenen Aufbereitungs-Auswertung einer Issue-Referenz
    /// (<c>issueaufbereitung-issue/{provider}/{host}/{repoId}/{number}</c>) — das authoritative
    /// „bereits ausgewertet"-Kriterium beider Erkennungspfade (Muster: der
    /// <c>backmerge/{provider}/{host}/{repoId}/{nummer}</c>-Präzedenzfall). Der Endzustand ist
    /// <see cref="ScmObservationCheckpoint.NextAttemptUtc"/> = <see cref="long.MaxValue"/> ohne
    /// offenen Fehler.</summary>
    /// <param name="reference">Die Issue-Referenz.</param>
    public static string AuswertungsCheckpointKey(ScmEventReference reference)
        => string.Join('/', new[] { "issueaufbereitung-issue", reference.Repository.ProviderPrefix.ToLowerInvariant(),
            reference.Repository.Host.ToLowerInvariant(), reference.Repository.RepositoryId,
            reference.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) }
            .Select(Uri.EscapeDataString));

    /// <summary>Prüft das „bereits ausgewertet"-Kriterium beider Erkennungspfade: Der
    /// je-Issue-Checkpoint (<see cref="AuswertungsCheckpointKey"/>) ist vorhanden ohne offenen
    /// Fehler, oder sein Fehler-Backoff ist noch nicht erreicht — in beiden Fällen entfällt der
    /// Beschreibungs-Read (Rate-Limit-Schonung).</summary>
    /// <param name="reference">Die Issue-Referenz.</param>
    /// <param name="ct">Abbruchtoken.</param>
    internal async Task<bool> AuswertungUeberspringenAsync(ScmEventReference reference, CancellationToken ct)
    {
        var checkpoint = await db.ScmObservationCheckpoints.FindAsync([AuswertungsCheckpointKey(reference)], ct);
        return checkpoint is not null
            && (checkpoint.ErrorCode is null || checkpoint.NextAttemptUtc > time.GetUtcNow().ToUnixTimeMilliseconds());
    }

    /// <summary>Legt die Erstbeobachtungs-Baseline eines Ereignis-Repositorys an (Watermark =
    /// jetzt) und markiert die zum Beobachtungsbeginn bereits bekannten Issue-Referenzen ohne
    /// Beschreibungs-Read als ausgewertet (Endzustand) — kein retroaktiver Scan des Bestands.</summary>
    /// <param name="repository">Die kanonische Ereignis-Identität des Repositorys.</param>
    /// <param name="ausgewertet">Bereits zum Beobachtungsbeginn bekannte Issue-Referenzen
    /// (leer, wenn nur die Baseline angelegt wird).</param>
    /// <param name="ct">Abbruchtoken.</param>
    internal async Task BaselineAnlegenAsync(ScmEventRepository repository,
        IReadOnlyList<ScmEventReference> ausgewertet, CancellationToken ct)
    {
        var baselineKey = BaselineKey(repository);
        db.ScmObservationCheckpoints.Add(new ScmObservationCheckpoint
        {
            SourceKey = baselineKey,
            WatermarkUtc = time.GetUtcNow().ToUnixTimeMilliseconds()
        });
        foreach (var issue in ausgewertet)
            db.ScmObservationCheckpoints.Add(new ScmObservationCheckpoint
            {
                SourceKey = AuswertungsCheckpointKey(issue),
                NextAttemptUtc = long.MaxValue
            });
        await db.SaveChangesAsync(ct);
        notifier.Notify();
        logger.LogInformation("Issue-Aufbereitung: Beobachtungsbeginn für {Key} festgelegt.", baselineKey);
    }

    /// <summary>Schreibt den Auswertungs-Checkpoint einer Issue-Referenz in den Endzustand
    /// (<see cref="ScmObservationCheckpoint.NextAttemptUtc"/> = <see cref="long.MaxValue"/>, kein
    /// offener Fehler): abgeschlossene Auswertung — Einreihung oder begründeter Skip. Fehlt der
    /// Checkpoint (z. B. frühe Rückkehr ohne Auswertung), wird er angelegt.</summary>
    /// <param name="reference">Die ausgewertete Issue-Referenz.</param>
    /// <param name="ct">Abbruchtoken.</param>
    internal async Task AuswertungAbschliessenAsync(ScmEventReference reference, CancellationToken ct)
    {
        var auswertungsKey = AuswertungsCheckpointKey(reference);
        var auswertung = await db.ScmObservationCheckpoints.FindAsync([auswertungsKey], ct);
        if (auswertung is null)
        {
            auswertung = new ScmObservationCheckpoint { SourceKey = auswertungsKey };
            db.ScmObservationCheckpoints.Add(auswertung);
        }
        auswertung.Failures = 0;
        auswertung.NextAttemptUtc = long.MaxValue;
        auswertung.ErrorCode = null;
        auswertung.Diagnostic = null;
        await db.SaveChangesAsync(ct);
        notifier.Notify();
    }

    /// <summary>Wertet einen gelesenen Issue-Beschreibungs-Batch auf „Issue erstellt" aus.
    /// „Bereits ausgewertet" gilt über den je-Issue-Checkpoint (<see cref="AuswertungsCheckpointKey"/>):
    /// vorhanden ohne offenen Fehler ⇒ überspringen; nach jeder abgeschlossenen Auswertung
    /// (Einreihung oder begründeter Skip) wird er mit Endzustand geschrieben, damit Folgezyklen
    /// die Referenz ohne Beschreibungs-Read überspringen. Die erste Beobachtung des Repositorys
    /// legt nur die Baseline an (kein retroaktiver Scan); danach lösen Beschreibungen aus, deren
    /// <see cref="ScmEventObservation.CreatedAt"/> nach dem Beobachtungsbeginn liegt.</summary>
    /// <param name="source">Die ereignisfähige SCM-Quelle des aktuellen Plugin-Durchlaufs.</param>
    /// <param name="reference">Die beobachtete Issue-Referenz.</param>
    /// <param name="batch">Der gelesene Beschreibungs-Batch samt Anfrage-Metadaten.</param>
    /// <param name="repositories">Die lokalen Repositories, die kanonisch auf dasselbe
    /// Provider-Repository auflösen; nur <see cref="GitRepository.IssuePreparationEnabled"/>
    /// freigeschaltete erzeugen Aufbereitungsaufträge.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public async Task ErkennenUndEinreihenAsync(IScmEventSource source, ScmEventReference reference,
        ScmEventBatch batch, IReadOnlyList<GitRepository> repositories, CancellationToken ct = default)
    {
        var freigeschaltet = repositories.Where(x => x.IssuePreparationEnabled)
            .OrderBy(x => x.RepositoryUrl, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).ToList();
        if (freigeschaltet.Count == 0 || batch.Observations.Count == 0)
            return;
        var baseline = await db.ScmObservationCheckpoints.FindAsync([BaselineKey(reference.Repository)], ct);
        if (baseline is null)
        {
            // Erstbeobachtung der Quelle: Baseline ohne Auslösung — kein retroaktiver Scan des
            // Bestands. Jede beobachtete Beobachtung hat zwangsläufig CreatedAt <= jetzt.
            await BaselineAnlegenAsync(reference.Repository, [], ct);
            return;
        }
        var baselineUtc = baseline.WatermarkUtc ?? 0;
        var repository = freigeschaltet[0];
        if (await AuswertungUeberspringenAsync(reference, ct))
            return; // Bereits ausgewertet bzw. Fehler-Backoff noch nicht erreicht.
        foreach (var observation in batch.Observations)
        {
            if (observation.CreatedAt.ToUnixTimeMilliseconds() <= baselineUtc)
                continue; // Altbestand: vor dem Beobachtungsbeginn angelegt.
            var eventKey = AufbereitungsEventKey(observation);
            if (await db.ScmIncomingEvents.AnyAsync(x => x.EventKey == eventKey, ct))
                continue; // Aufbereitungsauftrag bereits eingereiht.
            var issue = await source.ReadIssueAsync(reference, ct);
            if (issue is null)
            {
                logger.LogWarning("Issue-Aufbereitung: Anlagedaten des Issues {Number} konnten nicht gelesen werden.", reference.Number);
                continue;
            }
            var pluginPrefix = await einstellungen.GetScmIssueAufbereitungPluginPrefixAsync(ct);
            var task = await workItems.CreateFromIssueEventAsync(repository.ProjectId, issue,
                repository.Id, AufbereitungsSourceKey(reference), ct, Purpose, pluginPrefix);
            // Sofort fällig (NextAttemptUtc bleibt leer): Plugin-Auflösung, Kommentar-Schalter und
            // Ausführungsbegrenzung wirken erst zum Ausführungszeitpunkt im Orchestrator.
            var item = new ScmIncomingEvent
            {
                WorkItemId = task.Id,
                EventKey = eventKey,
                ObservationJson = JsonSerializer.Serialize(observation, ScmEventStore.JsonOptions),
                ReceivedUtc = time.GetUtcNow().ToUnixTimeMilliseconds(),
                Purpose = Purpose,
                Reason = $"Interner Aufbereitungsauftrag für Issue #{issue.Nummer}."
            };
            db.ScmIncomingEvents.Add(item);
            db.LogEntries.Add(new LogEntry
            {
                Id = Guid.NewGuid(),
                WorkItemId = task.Id,
                Type = LogEntryType.SystemMessage,
                Content = $"Issue-Aufbereitung für Issue #{issue.Nummer} eingereiht (interner, trigger-freier Auftrag).",
                Timestamp = time.GetUtcNow()
            });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Anlagerennen über den EventKey-Unique-Index: Der Auftrag ist bereits eingereiht.
                db.ChangeTracker.Clear();
                continue;
            }
            notifier.Notify();
            logger.LogInformation("Issue-Aufbereitung: interner Auftrag {EventKey} auf Aufgabe {AufgabeId} eingereiht.", eventKey, task.Id);
        }
        // Abgeschlossene Auswertung (Einreihung oder begründeter Skip wie Altbestand,
        // bereits eingereihter Auftrag oder fehlende Anlagedaten): Die Referenz gilt für
        // beide Erkennungspfade als ausgewertet (Endzustand).
        await AuswertungAbschliessenAsync(reference, ct);
    }
}
