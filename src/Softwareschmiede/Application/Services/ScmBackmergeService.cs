using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Erkennt neu enumerierte Backmerge-Pull-Requests (head = Repository-Hauptbranch,
/// base = konfigurierter Projekt-Basisbranch) und bestätigt sie automatisiert per Admin-Merge.</summary>
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
    /// mergt zutreffende Pull Requests per Admin-Merge (<c>gh pr merge --admin --merge</c>).</summary>
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
        var hauptbranches = new Dictionary<Guid, string?>();
        foreach (var reference in pullRequests)
        {
            var sourceKey = CheckpointKey(repository, reference.Number);
            var checkpoint = await db.ScmObservationCheckpoints.FindAsync([sourceKey], ct);
            var now = time.GetUtcNow();
            if (checkpoint?.NextAttemptUtc > now.ToUnixTimeMilliseconds())
                continue;
            if (checkpoint is null)
            {
                checkpoint = new ScmObservationCheckpoint { SourceKey = sourceKey };
                db.Add(checkpoint);
            }
            try
            {
                await BewertePullRequestAsync(plugin, repositoryId, reference.Number, checkpoint, kandidaten, hauptbranches, now, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                checkpoint.Failures++;
                var failure = ex as ScmProviderException;
                checkpoint.ErrorCode = failure?.FailureKind.ToString() ?? "Temporary";
                checkpoint.Diagnostic = "Backmerge-Prüfung fehlgeschlagen: " + (failure?.Message ?? "Status des Pull Requests konnte nicht gelesen werden.");
                checkpoint.NextAttemptUtc = failure?.FailureKind is ScmProviderFailureKind.Authentication
                    or ScmProviderFailureKind.Permission or ScmProviderFailureKind.Permanent
                    ? long.MaxValue
                    : ScmEventStore.RetryAt(checkpoint.Failures, now.ToUnixTimeMilliseconds(), failure?.RetryAfterUtc);
            }
            await db.SaveChangesAsync(ct);
            notifier.Notify();
        }
    }

    private async Task BewertePullRequestAsync(
        IGitPlugin plugin,
        string repositoryId,
        int nummer,
        ScmObservationCheckpoint checkpoint,
        IReadOnlyList<GitRepository> kandidaten,
        Dictionary<Guid, string?> hauptbranches,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var status = await plugin.GetPullRequestStatusAsync(repositoryId, nummer, ct);
        if (status.Status != PullRequestStatus.Open)
        {
            Entscheide(checkpoint, "Kein offener Pull Request; keine Backmerge-Aktion.");
            return;
        }

        var hauptbranchUnbekannt = false;
        GitRepository? treffer = null;
        foreach (var kandidat in kandidaten)
        {
            if (!hauptbranches.TryGetValue(kandidat.Id, out var hauptbranch))
            {
                try
                {
                    hauptbranch = await plugin.GetDefaultBranchAsync(kandidat.RepositoryUrl, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Hauptbranch für Repository {RepositoryUrl} konnte nicht ermittelt werden.", kandidat.RepositoryUrl);
                    hauptbranch = null;
                }
                hauptbranches[kandidat.Id] = hauptbranch;
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
            {
                treffer = kandidat;
                break;
            }
        }

        if (treffer is null)
        {
            if (hauptbranchUnbekannt)
            {
                checkpoint.Diagnostic = "Hauptbranch konnte nicht ermittelt werden; Backmerge-Prüfung wird wiederholt.";
                checkpoint.NextAttemptUtc = NextRetryUtc(now);
            }
            else
            {
                Entscheide(checkpoint, $"Kein Backmerge ({status.SourceBranch} → {status.TargetBranch}).");
            }
            return;
        }

        if (status.MergeStatus != PullRequestMergeStatus.Mergeable)
        {
            checkpoint.Diagnostic = $"Backmerge erkannt ({status.SourceBranch} → {status.TargetBranch}); wartet auf Mergebarkeit (Status: {status.MergeStatus}).";
            checkpoint.ErrorCode = "WaitingForMergeable";
            checkpoint.NextAttemptUtc = NextRetryUtc(now);
            logger.LogInformation("Backmerge-PR #{Nummer} in {RepositoryId} erkannt, aber noch nicht mergebar ({MergeStatus}).",
                nummer, repositoryId, status.MergeStatus);
            return;
        }

        var result = await plugin.CompletePullRequestAsync(repositoryId, nummer,
            new PullRequestCompletionOptions(PullRequestCompletionStrategy.Merge, PullRequestMergeMethod.Merge,
                AllowProtectedBranchBypass: true), ct);
        if (result.Success && result.PullRequestMerged)
        {
            checkpoint.ErrorCode = "Merged";
            Entscheide(checkpoint, $"Backmerge ({status.SourceBranch} → {status.TargetBranch}) automatisch gemergt.");
            logger.LogInformation("Backmerge-PR #{Nummer} in {RepositoryId} automatisch per Admin-Merge bestätigt.", nummer, repositoryId);
            return;
        }

        checkpoint.Failures++;
        checkpoint.ErrorCode = result.Blocked ? "Blocked" : "Temporary";
        checkpoint.Diagnostic = $"Backmerge-Merge fehlgeschlagen: {result.Message ?? "Unbekannter Fehler."}";
        checkpoint.NextAttemptUtc = ScmEventStore.RetryAt(checkpoint.Failures, now.ToUnixTimeMilliseconds());
        logger.LogWarning("Backmerge-Merge von PR #{Nummer} in {RepositoryId} fehlgeschlagen: {Message}",
            nummer, repositoryId, result.Message);
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

    /// <summary>Markiert den Checkpoint als endgültig bewertet (keine weiteren Versuche).</summary>
    private void Entscheide(ScmObservationCheckpoint checkpoint, string diagnostic)
    {
        checkpoint.ErrorCode ??= "Decided";
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
}
