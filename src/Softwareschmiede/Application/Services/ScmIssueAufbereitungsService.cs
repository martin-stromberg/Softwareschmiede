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
/// <see cref="IssueReferenz"/> im eigenen <c>IssueAufbereitung</c>-Namensraum plus ein internes
/// <see cref="ScmIncomingEvent"/>, das ohne Trigger-Validierung angelegt, geparkt wird
/// (<see cref="ScmIncomingEvent.NextAttemptUtc"/> = <see cref="long.MaxValue"/>) und erst durch den
/// Aufbereitungs-Ausführungspfad (Entwicklungsschritt 6) verarbeitet wird.</summary>
/// <param name="db">Datenbankkontext.</param>
/// <param name="aufgaben">Anlage der Aufbereitungs-Aufgabe samt SourceKey-Dedup.</param>
/// <param name="einstellungen">Persistierte Funktionskonfiguration (Plugin-Schlüssel, Kommentar-Schalter).</param>
/// <param name="notifier">Benachrichtigt Ansichten über dauerhafte Änderungen.</param>
/// <param name="time">Zeitquelle.</param>
/// <param name="logger">Protokoll.</param>
public sealed class ScmIssueAufbereitungsService(
    SoftwareschmiededDbContext db, AufgabeService aufgaben, AppEinstellungService einstellungen,
    ScmEventChangeNotifier notifier, TimeProvider time, ILogger<ScmIssueAufbereitungsService> logger)
{
    /// <summary>Zweck-Markierung (<see cref="Aufgabe.Zweck"/> bzw. <see cref="ScmIncomingEvent.Zweck"/>)
    /// der Issue-Aufbereitung; einziger Zweck-Token des Systems.</summary>
    public const string Zweck = "IssueAufbereitung";

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
            Zweck, reference.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) }
            .Select(Uri.EscapeDataString));

    /// <summary>Deterministischer, deduplizierender EventKey des internen Aufbereitungsauftrags:
    /// eindeutig je Issue-Beschreibung (ContentKey) und Zweck, unabhängig von Revision und BodyHash.</summary>
    /// <param name="observation">Die auslösende Beschreibungs-Beobachtung.</param>
    public static string AufbereitungsEventKey(ScmEventObservation observation)
        => $"{observation.ContentKey}/{Zweck}";

    /// <summary>Wertet einen gelesenen Issue-Beschreibungs-Batch auf „Issue erstellt" aus.
    /// Voraussetzung: Der Aufruf läuft vor <see cref="ScmEventStore.AcceptBatchAsync"/> desselben
    /// Batches, solange noch kein <see cref="ScmContentSnapshot"/> zum ContentKey existiert —
    /// die Erstbeobachtung ist der Auslöser. Die erste Beobachtung des Repositorys legt nur die
    /// Baseline an (kein retroaktiver Scan); danach lösen Beschreibungen aus, deren
    /// <see cref="ScmEventObservation.CreatedAt"/> nach dem Beobachtungsbeginn liegt.</summary>
    /// <param name="source">Die ereignisfähige SCM-Quelle des aktuellen Plugin-Durchlaufs.</param>
    /// <param name="reference">Die beobachtete Issue-Referenz.</param>
    /// <param name="batch">Der gelesene Beschreibungs-Batch samt Anfrage-Metadaten.</param>
    /// <param name="repositories">Die lokalen Repositories, die kanonisch auf dasselbe
    /// Provider-Repository auflösen; nur <see cref="GitRepository.IssueAufbereitungAktiv"/>
    /// freigeschaltete erzeugen Aufbereitungsaufträge.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public async Task ErkennenUndEinreihenAsync(IScmEventSource source, ScmEventReference reference,
        ScmEventBatch batch, IReadOnlyList<GitRepository> repositories, CancellationToken ct = default)
    {
        var freigeschaltet = repositories.Where(x => x.IssueAufbereitungAktiv)
            .OrderBy(x => x.RepositoryUrl, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).ToList();
        if (freigeschaltet.Count == 0 || batch.Observations.Count == 0)
            return;
        var baselineKey = BaselineKey(reference.Repository);
        var baseline = await db.ScmObservationCheckpoints.FindAsync([baselineKey], ct);
        if (baseline is null)
        {
            // Erstbeobachtung der Quelle: Baseline ohne Auslösung — kein retroaktiver Scan des
            // Bestands. Jede beobachtete Beobachtung hat zwangsläufig CreatedAt <= jetzt.
            db.ScmObservationCheckpoints.Add(new ScmObservationCheckpoint
            {
                SourceKey = baselineKey,
                WatermarkUtc = time.GetUtcNow().ToUnixTimeMilliseconds()
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Issue-Aufbereitung: Beobachtungsbeginn für {Key} festgelegt.", baselineKey);
            return;
        }
        var baselineUtc = baseline.WatermarkUtc ?? 0;
        var repository = freigeschaltet[0];
        foreach (var observation in batch.Observations)
        {
            if (observation.CreatedAt.ToUnixTimeMilliseconds() <= baselineUtc)
                continue; // Altbestand: vor dem Beobachtungsbeginn angelegt.
            if (await db.ScmContentSnapshots.AnyAsync(x => x.ContentKey == observation.ContentKey, ct))
                continue; // Keine Erstbeobachtung (u. a. spätere oder selbst verursachte Beschreibungsänderungen).
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
            var kommentar = await einstellungen.GetScmIssueAufbereitungKommentarEnabledAsync(ct);
            var task = await aufgaben.CreateFromIssueEventAsync(repository.ProjektId, issue,
                repository.Id, AufbereitungsSourceKey(reference), ct, Zweck, pluginPrefix);
            var item = new ScmIncomingEvent
            {
                AufgabeId = task.Id,
                EventKey = eventKey,
                ObservationJson = JsonSerializer.Serialize(observation, ScmEventStore.JsonOptions),
                ReceivedUtc = time.GetUtcNow().ToUnixTimeMilliseconds(),
                Zweck = Zweck,
                NextAttemptUtc = long.MaxValue,
                Reason = $"Interner Aufbereitungsauftrag für Issue #{issue.Nummer}." +
                    (kommentar ? " Die Analyse soll als Issue-Kommentar dokumentiert werden." : string.Empty)
            };
            db.ScmIncomingEvents.Add(item);
            db.Protokolleintraege.Add(new Protokolleintrag
            {
                Id = Guid.NewGuid(),
                AufgabeId = task.Id,
                Typ = ProtokollTyp.SystemMeldung,
                Inhalt = $"Issue-Aufbereitung für Issue #{issue.Nummer} eingereiht (interner, trigger-freier Auftrag).",
                Zeitstempel = time.GetUtcNow()
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
    }
}
