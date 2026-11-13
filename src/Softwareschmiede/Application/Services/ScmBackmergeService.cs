using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Erkennt neu enumerierte Backmerge-Pull-Requests (head = Repository-Hauptbranch,
/// base = konfigurierter Projekt-Basisbranch) und mergt sie automatisiert. Der Merge läuft zuerst
/// regulär; nur bei aktivierter Option wird ein blockierter Merge per Admin-Merge erneut versucht.
/// Draft-Pull-Requests können optional vorab automatisch aktiviert werden.</summary>
/// <remarks>
/// Ein Backmerge aktualisiert lediglich den Arbeitsbranch und benötigt keine inhaltliche Prüfung.
/// Jede Pull-Request-Referenz wird genau einmal bewertet; der Bewertungsstand liegt in einem eigenen
/// <see cref="ScmObservationCheckpoint"/> unter dem Schlüssel <c>backmerge/{provider}/{host}/{repoId}/{nummer}</c>.
/// Solange ein passender Pull Request offen, aber noch nicht mergebar ist, wird die Bewertung
/// im Abstand des konfigurierten Poll-Intervalls wiederholt.
/// </remarks>
public sealed class ScmBackmergeService(
    SoftwareschmiededDbContext db,
    AppEinstellungService einstellungen,
    ScmEventChangeNotifier notifier,
    TimeProvider time,
    IOptions<ScmEventOptions> options,
    ILogger<ScmBackmergeService> logger)
{
    /// <summary>Bewertet die enumerierten Referenzen eines Repositories auf das Backmerge-Muster und
    /// mergt zutreffende Pull Requests (regulärer Merge; optionaler Admin-Fallback).</summary>
    /// <param name="plugin">Das zuständige SCM-Plugin (führt Statusabfrage und Merge aus).</param>
    /// <param name="repository">Das kanonisch aufgelöste Ereignis-Repository.</param>
    /// <param name="discovered">Die in diesem Lauf enumerierten Referenzen des Repositories.</param>
    /// <param name="lokaleRepositories">Die diesem Remote-Repository zugeordneten lokalen Projekt-Repositories.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public async Task EvaluateDiscoveredReferencesAsync(
        IGitPlugin plugin,
        ScmEventRepository repository,
        IReadOnlyList<ScmEventReference> discovered,
        IReadOnlyList<GitRepository> lokaleRepositories,
        CancellationToken ct = default)
    {
        var modus = await einstellungen.GetScmBackmergeModusAsync(ct);
        if (modus == ScmBackmergeModus.Deaktiviert)
            return;
        var draftAktivierung = await einstellungen.GetScmBackmergeDraftAktivierungAsync(ct);
        var adminMerge = await einstellungen.GetScmBackmergeAdminMergeAsync(ct);
        // Ohne Schreibrecht des Tokens ist kein Merge möglich.
        if (!repository.CanWrite)
            return;
        var pullRequests = discovered.Where(x => x.Kind == ScmReferenceKind.PullRequest).ToList();
        if (pullRequests.Count == 0)
            return;
        var kandidaten = await FilterAktivierteRepositoriesAsync(lokaleRepositories
            .Where(x => x.Aktiv && !string.IsNullOrWhiteSpace(x.DefaultSourceBranchName)).ToList(), modus, ct);
        if (kandidaten.Count == 0)
            return;

        var repositoryId = $"{repository.Owner}/{repository.Name}";
        var kontext = new BewertungsKontext(plugin, repositoryId, kandidaten, new Dictionary<Guid, string?>(),
            time.GetUtcNow(), draftAktivierung, adminMerge);
        foreach (var reference in pullRequests)
        {
            var sourceKey = CheckpointKey(repository, reference.Number);
            var checkpoint = await db.ScmObservationCheckpoints.FindAsync([sourceKey], ct);
            if (checkpoint?.NextAttemptUtc > kontext.Now.ToUnixTimeMilliseconds())
                continue;
            if (checkpoint is null)
            {
                checkpoint = new ScmObservationCheckpoint { SourceKey = sourceKey };
                db.Add(checkpoint);
            }
            try
            {
                await BewertePullRequestAsync(reference, checkpoint, kontext, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                ScmCheckpointSupport.ApplyFailure(checkpoint, ex,
                    "Backmerge-Prüfung fehlgeschlagen: "
                    + ((ex as ScmProviderException)?.Message ?? "Status des Pull Requests konnte nicht gelesen werden."),
                    kontext.Now.ToUnixTimeMilliseconds());
            }
            await db.SaveChangesAsync(ct);
            notifier.Notify();
        }
    }

    /// <summary>Bewertet eine Pull-Request-Referenz in vier Schritten: offenen Status prüfen,
    /// Backmerge-Zuordnung zum Projekt-Repository finden, Mergebarkeit herstellen (optionale
    /// Draft-Aktivierung) und den Merge ausführen (optionaler Admin-Fallback).</summary>
    private async Task BewertePullRequestAsync(
        ScmEventReference reference,
        ScmObservationCheckpoint checkpoint, BewertungsKontext kontext, CancellationToken ct)
    {
        var status = await kontext.Plugin.GetPullRequestStatusAsync(kontext.RepositoryId, reference.Number, ct);
        if (status.Status != PullRequestStatus.Open)
        {
            Entscheide(checkpoint, "Kein offener Pull Request; keine Backmerge-Aktion.");
            return;
        }

        var (treffer, hauptbranchUnbekannt) = await FindeBackmergeZuordnungAsync(status, kontext, ct);
        if (treffer is null)
        {
            if (hauptbranchUnbekannt)
            {
                checkpoint.Diagnostic = "Hauptbranch konnte nicht ermittelt werden; Backmerge-Prüfung wird wiederholt.";
                checkpoint.NextAttemptUtc = NextRetryUtc(kontext.Now);
            }
            else
            {
                Entscheide(checkpoint, $"Kein Backmerge ({status.SourceBranch} → {status.TargetBranch}).");
            }
            return;
        }

        var mergebarerStatus = await StelleMergebarkeitHerAsync(reference, status, checkpoint, kontext, ct);
        if (mergebarerStatus is null)
            return;

        await FuehreMergeAusAsync(reference, mergebarerStatus, checkpoint, kontext, ct);
    }

    /// <summary>Sucht unter den aktivierten Kandidaten das Projekt-Repository, auf dessen vom
    /// Hauptbranch abweichenden Basisbranch das Pull-Request-Muster (Hauptbranch → Basisbranch)
    /// zutrifft. Hauptbranches werden pro Repository einmalig ermittelt und im
    /// <paramref name="kontext"/> zwischengespeichert.</summary>
    private async Task<(GitRepository? Treffer, bool HauptbranchUnbekannt)> FindeBackmergeZuordnungAsync(
        PullRequestStatusInfo status, BewertungsKontext kontext, CancellationToken ct)
    {
        var hauptbranchUnbekannt = false;
        foreach (var kandidat in kontext.Kandidaten)
        {
            if (!kontext.Hauptbranches.TryGetValue(kandidat.Id, out var hauptbranch))
            {
                hauptbranch = await LadeHauptbranchAsync(kontext, kandidat, ct);
                kontext.Hauptbranches[kandidat.Id] = hauptbranch;
            }
            if (hauptbranch is null)
            {
                hauptbranchUnbekannt = true;
                continue;
            }
            // Nur Repositories mit einem vom Hauptbranch abweichenden Basisbranch sind relevant.
            if (string.Equals(kandidat.DefaultSourceBranchName, hauptbranch, StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(status.SourceBranch, hauptbranch, StringComparison.OrdinalIgnoreCase)
                && string.Equals(status.TargetBranch, kandidat.DefaultSourceBranchName, StringComparison.OrdinalIgnoreCase))
                return (kandidat, hauptbranchUnbekannt);
        }
        return (null, hauptbranchUnbekannt);
    }

    /// <summary>Ermittelt den Hauptbranch eines Projekt-Repositories; ein Lesefehler gilt als
    /// „unbekannt“ und verzögert die Prüfung statt sie zu beenden.</summary>
    private async Task<string?> LadeHauptbranchAsync(BewertungsKontext kontext, GitRepository kandidat, CancellationToken ct)
    {
        try
        {
            return await kontext.Plugin.GetDefaultBranchAsync(kandidat.RepositoryUrl, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Hauptbranch für Repository {RepositoryUrl} konnte nicht ermittelt werden.", kandidat.RepositoryUrl);
            return null;
        }
    }

    /// <summary>Hebt bei aktivierter Option den Entwurfsstatus auf und prüft die Mergebarkeit.
    /// Gibt den mergefähigen Status zurück oder <see langword="null"/>, wenn der Checkpoint
    /// bereits auf „warten“/„fehlgeschlagen“ fortgeschrieben wurde.</summary>
    private async Task<PullRequestStatusInfo?> StelleMergebarkeitHerAsync(
        ScmEventReference reference, PullRequestStatusInfo status,
        ScmObservationCheckpoint checkpoint, BewertungsKontext kontext, CancellationToken ct)
    {
        if (status.IsDraft)
        {
            if (!kontext.DraftAktivierung)
            {
                checkpoint.Diagnostic = $"Backmerge erkannt ({status.SourceBranch} → {status.TargetBranch}); wartet auf Mergebarkeit (Pull Request ist ein Entwurf).";
                checkpoint.ErrorCode = "WaitingForMergeable";
                checkpoint.NextAttemptUtc = NextRetryUtc(kontext.Now);
                logger.LogInformation("Backmerge-PR #{Nummer} in {RepositoryId} erkannt, ist aber ein Entwurf; Draft-Aktivierung ist deaktiviert.",
                    reference.Number, kontext.RepositoryId);
                return null;
            }

            var ready = await kontext.Plugin.MarkPullRequestReadyAsync(kontext.RepositoryId, reference.Number, ct);
            if (!ready.Success)
            {
                checkpoint.Failures++;
                checkpoint.ErrorCode = "Temporary";
                checkpoint.Diagnostic = $"Draft-Aktivierung fehlgeschlagen: {ready.Message ?? "Unbekannter Fehler."}";
                checkpoint.NextAttemptUtc = ScmEventStore.RetryAt(checkpoint.Failures, kontext.Now.ToUnixTimeMilliseconds());
                logger.LogWarning("Draft-Aktivierung von Backmerge-PR #{Nummer} in {RepositoryId} fehlgeschlagen: {Message}",
                    reference.Number, kontext.RepositoryId, ready.Message);
                return null;
            }

            logger.LogInformation("Draft-Status von Backmerge-PR #{Nummer} in {RepositoryId} automatisch aufgehoben.", reference.Number, kontext.RepositoryId);
            status = await kontext.Plugin.GetPullRequestStatusAsync(kontext.RepositoryId, reference.Number, ct);
        }

        if (status.MergeStatus != PullRequestMergeStatus.Mergeable)
        {
            var nurReviewBlockiert = status.MergeStatus == PullRequestMergeStatus.Blocked
                && status.ReviewDecision == PullRequestReviewDecision.ReviewRequired
                && status.ChecksState is PullRequestChecksState.Success or PullRequestChecksState.None;
            if (nurReviewBlockiert && checkpoint.ErrorCode != "ReviewApprovalSent")
            {
                // Die Review-Pflicht ist die einzige Blockade: zuerst über den Plugin-Vertrag
                // genehmigen — dafür genügt Schreibrecht. Nur wenn die Genehmigung die Pflicht
                // nicht erfüllt (z. B. fremder Reviewer gefordert), bleibt der Admin-Fallback.
                var approve = await kontext.Plugin.CompletePullRequestAsync(kontext.RepositoryId, reference.Number,
                    new PullRequestCompletionOptions(PullRequestCompletionStrategy.ApprovalOnly), ct);
                if (approve.Success)
                {
                    checkpoint.ErrorCode = "ReviewApprovalSent";
                    logger.LogInformation("Backmerge-PR #{Nummer} in {RepositoryId} automatisch genehmigt.",
                        reference.Number, kontext.RepositoryId);
                    status = await kontext.Plugin.GetPullRequestStatusAsync(kontext.RepositoryId, reference.Number, ct);
                }
                else
                {
                    logger.LogInformation("Automatische Genehmigung von Backmerge-PR #{Nummer} in {RepositoryId} nicht möglich: {Message}",
                        reference.Number, kontext.RepositoryId, approve.Message);
                }
            }

            if (status.MergeStatus != PullRequestMergeStatus.Mergeable
                && !(nurReviewBlockiert && kontext.AdminMerge))
            {
                checkpoint.Diagnostic = nurReviewBlockiert
                    ? $"Backmerge erkannt ({status.SourceBranch} → {status.TargetBranch}); wartet auf Mergebarkeit (Review erforderlich)."
                    : $"Backmerge erkannt ({status.SourceBranch} → {status.TargetBranch}); wartet auf Mergebarkeit (Status: {status.MergeStatus}).";
                if (checkpoint.ErrorCode != "ReviewApprovalSent")
                    checkpoint.ErrorCode = "WaitingForMergeable";
                checkpoint.NextAttemptUtc = NextRetryUtc(kontext.Now);
                logger.LogInformation("Backmerge-PR #{Nummer} in {RepositoryId} erkannt, aber noch nicht mergebar ({MergeStatus}).",
                    reference.Number, kontext.RepositoryId, status.MergeStatus);
                return null;
            }
        }
        return status;
    }

    /// <summary>Führt den Backmerge aus: zuerst regulär, bei aktivierter Option mit Admin-Merge-Fallback.</summary>
    private async Task FuehreMergeAusAsync(
        ScmEventReference reference, PullRequestStatusInfo status,
        ScmObservationCheckpoint checkpoint, BewertungsKontext kontext, CancellationToken ct)
    {
        var result = await kontext.Plugin.CompletePullRequestAsync(kontext.RepositoryId, reference.Number,
            new PullRequestCompletionOptions(PullRequestCompletionStrategy.Merge, PullRequestMergeMethod.Merge,
                AllowProtectedBranchBypass: false), ct);
        if ((!result.Success || !result.PullRequestMerged) && kontext.AdminMerge)
        {
            logger.LogInformation("Regulärer Merge von Backmerge-PR #{Nummer} in {RepositoryId} gescheitert; versuche Admin-Merge.",
                reference.Number, kontext.RepositoryId);
            result = await kontext.Plugin.CompletePullRequestAsync(kontext.RepositoryId, reference.Number,
                new PullRequestCompletionOptions(PullRequestCompletionStrategy.Merge, PullRequestMergeMethod.Merge,
                    AllowProtectedBranchBypass: true), ct);
        }
        if (result.Success && result.PullRequestMerged)
        {
            checkpoint.ErrorCode = "Merged";
            Entscheide(checkpoint, $"Backmerge ({status.SourceBranch} → {status.TargetBranch}) automatisch gemergt.");
            logger.LogInformation("Backmerge-PR #{Nummer} in {RepositoryId} automatisch gemergt.", reference.Number, kontext.RepositoryId);
            return;
        }

        checkpoint.Failures++;
        checkpoint.ErrorCode = result.Blocked ? "Blocked" : "Temporary";
        checkpoint.Diagnostic = $"Backmerge-Merge fehlgeschlagen: {result.Message ?? "Unbekannter Fehler."}";
        checkpoint.NextAttemptUtc = ScmEventStore.RetryAt(checkpoint.Failures, kontext.Now.ToUnixTimeMilliseconds());
        logger.LogWarning("Backmerge-Merge von PR #{Nummer} in {RepositoryId} fehlgeschlagen: {Message}",
            reference.Number, kontext.RepositoryId, result.Message);
    }

    /// <summary>Filtert die Kandidaten nach dem Aktivierungsmodus; im projektbezogenen Modus werden
    /// die Freigabe-Flags aller betroffenen Projekte in einer Abfrage gelesen.</summary>
    private async Task<IReadOnlyList<GitRepository>> FilterAktivierteRepositoriesAsync(
        List<GitRepository> kandidaten, ScmBackmergeModus modus, CancellationToken ct)
    {
        if (modus == ScmBackmergeModus.Global || kandidaten.Count == 0)
            return kandidaten;
        var keys = kandidaten.Select(x => AppEinstellungService.BackmergeProjektKey(x.ProjektId)).Distinct().ToArray();
        var flags = await einstellungen.GetSettingsAsync(keys, ct);
        return kandidaten.Where(x => flags.TryGetValue(AppEinstellungService.BackmergeProjektKey(x.ProjektId), out var wert)
            && bool.TryParse(wert, out var aktiv) && aktiv).ToList();
    }

    /// <summary>Markiert den Checkpoint als endgültig bewertet (keine weiteren Versuche). Ein
    /// Zwischencode aus früheren Versuchen (Temporary/Blocked/WaitingForMergeable) wird
    /// überschrieben; lediglich der vom Merge-Pfad gesetzte Code „Merged“ bleibt erhalten.</summary>
    private void Entscheide(ScmObservationCheckpoint checkpoint, string diagnostic)
    {
        if (checkpoint.ErrorCode is not "Merged")
            checkpoint.ErrorCode = "Decided";
        checkpoint.Diagnostic = diagnostic;
        checkpoint.NextAttemptUtc = long.MaxValue;
    }

    private long NextRetryUtc(DateTimeOffset now)
        => now.Add(options.Value.PollInterval).ToUnixTimeMilliseconds();

    /// <summary>Checkpoint-Schlüssel der Backmerge-Bewertung einer Pull-Request-Referenz.</summary>
    public static string CheckpointKey(ScmEventRepository repository, int number)
        => string.Join('/', new[] { "backmerge", repository.ProviderPrefix.ToLowerInvariant(),
            repository.Host.ToLowerInvariant(), repository.RepositoryId,
            number.ToString(System.Globalization.CultureInfo.InvariantCulture) }.Select(Uri.EscapeDataString));

    /// <summary>Die über den Bewertungszyklus unveränderten Zyklusdaten (Plugin, Remote-Kennung,
    /// Kandidaten, Hauptbranch-Cache, Zeitpunkt und Merge-Optionen), gebündelt für die Teilmethoden
    /// der Pull-Request-Bewertung.</summary>
    private sealed record BewertungsKontext(
        IGitPlugin Plugin,
        string RepositoryId,
        IReadOnlyList<GitRepository> Kandidaten,
        Dictionary<Guid, string?> Hauptbranches,
        DateTimeOffset Now,
        bool DraftAktivierung,
        bool AdminMerge);
}
