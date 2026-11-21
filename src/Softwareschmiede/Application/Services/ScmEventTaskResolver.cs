using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Ergebnisart der Aufgabenauflösung einer SCM-Referenz.</summary>
public enum ScmTaskResolutionKind
{
    /// <summary>Genau eine Aufgabe wurde gefunden oder angelegt.</summary>
    Assigned,
    /// <summary>Mehrere Aufgaben tragen dieselbe Referenz.</summary>
    Ambiguous,
    /// <summary>Keine Aufgabe zuordenbar und keine anlegbar.</summary>
    Unassigned
}

/// <summary>Ergebnis der Aufgabenauflösung: zugeordnete Aufgabe oder Grund der Nicht-Zuordnung.</summary>
public sealed record ScmTaskResolution(ScmTaskResolutionKind Kind, Guid? WorkItemId)
{
    /// <summary>Mehrere Aufgaben tragen dieselbe Referenz.</summary>
    public static readonly ScmTaskResolution Ambiguous = new(ScmTaskResolutionKind.Ambiguous, null);
    /// <summary>Keine Aufgabe zuordenbar und keine anlegbar.</summary>
    public static readonly ScmTaskResolution Unassigned = new(ScmTaskResolutionKind.Unassigned, null);
    /// <summary>Genau eine Aufgabe wurde gefunden oder angelegt.</summary>
    public static ScmTaskResolution Assigned(Guid workItemId)
        => workItemId == Guid.Empty
            ? throw new InvalidOperationException("Eine zugeordnete Auflösung benötigt eine Aufgaben-ID.")
            : new ScmTaskResolution(ScmTaskResolutionKind.Assigned, workItemId);
}

/// <summary>Löst eine SCM-Referenz zu genau einer lokalen Aufgabe auf: Wiederverwendung bei
/// eindeutigem Treffer, Anlage aus den Ereignisdaten bei fehlendem Treffer, Mehrdeutigkeitsdiagnose
/// bei mehreren Treffern. Läuft vor der Inbox-Aufnahme und hält deren Aufgabeninvariante.</summary>
public sealed class ScmEventTaskResolver(
    SoftwareschmiedeDbContext db, WorkItemService workItems, ILogger<ScmEventTaskResolver> logger)
{
    /// <summary>Findet die eindeutig passende Aufgabe zur Referenz der qualifizierten Beobachtung
    /// <paramref name="trigger"/> oder legt sie an; <paramref name="repositories"/> enthält die
    /// lokalen Repositories, die kanonisch auf denselben Provider-Repository-Datensatz auflösen.</summary>
    public async Task<ScmTaskResolution> ResolveAsync(ScmEventReference reference, IScmEventSource source,
        ScmEventObservation trigger, IReadOnlyList<GitRepository> repositories, CancellationToken ct = default)
        => reference.Kind == ScmReferenceKind.Issue
            ? await ResolveIssueAsync(reference, source, trigger, repositories, ct)
            : await ResolvePullRequestAsync(reference, source, trigger, repositories, ct);

    private async Task<ScmTaskResolution> ResolveIssueAsync(ScmEventReference reference, IScmEventSource source,
        ScmEventObservation trigger, IReadOnlyList<GitRepository> repositories, CancellationToken ct)
    {
        var issue = await source.ReadIssueAsync(reference, ct);
        if (issue is null || issue.Number != reference.Number)
        {
            if (issue is not null)
                logger.LogWarning("Issue-Anlagedaten von {Number} weichen von der Referenz {Reference} ab.", issue.Number, reference.Number);
            return ScmTaskResolution.Unassigned;
        }
        var repositoryIds = repositories.Select(x => (Guid?)x.Id).ToArray();
        var taskIds = await db.WorkItems.AsNoTracking()
            .Where(a => repositoryIds.Contains(a.GitRepositoryId)
                && a.Purpose == null
                && a.IssueReference != null && a.IssueReference.IssueNumber == reference.Number)
            .Select(a => a.Id).Distinct().ToListAsync(ct);
        if (taskIds.Count > 1) return ScmTaskResolution.Ambiguous;
        if (taskIds.Count == 1) return ScmTaskResolution.Assigned(taskIds[0]);
        var repository = ChooseRepository(repositories);
        var composed = issue with { Body = ComposeBody(issue.Body, trigger) };
        var task = await workItems.CreateFromIssueEventAsync(repository.ProjectId, composed,
            repository.Id, IssueSourceKey(reference), ct);
        return ScmTaskResolution.Assigned(task.Id);
    }

    private async Task<ScmTaskResolution> ResolvePullRequestAsync(ScmEventReference reference, IScmEventSource source,
        ScmEventObservation trigger, IReadOnlyList<GitRepository> repositories, CancellationToken ct)
    {
        var pullRequest = await source.ReadPullRequestAsync(reference, ct);
        if (pullRequest is null || pullRequest.Number != reference.Number)
        {
            if (pullRequest is not null)
                logger.LogWarning("Pull-Request-Anlagedaten von {Number} weichen von der Referenz {Reference} ab.", pullRequest.Number, reference.Number);
            return ScmTaskResolution.Unassigned;
        }
        var repositoryId = PullRequestRepositoryId.Normalize(pullRequest.Provider,
            $"{reference.Repository.Owner}/{reference.Repository.Name}");
        var taskIds = await PullRequestTaskIdsAsync(pullRequest.Provider, repositoryId, reference.Number, ct);
        if (taskIds.Count > 1) return ScmTaskResolution.Ambiguous;
        if (taskIds.Count == 1) return ScmTaskResolution.Assigned(taskIds[0]);
        var contextTaskIds = await FindActiveContextTaskIdsAsync(pullRequest, repositories, repositoryId, ct);
        if (contextTaskIds.Count > 1) return ScmTaskResolution.Ambiguous;
        if (contextTaskIds.Count == 1) return ScmTaskResolution.Assigned(contextTaskIds[0]);
        var repository = ChooseRepository(repositories);
        var context = new ScmRepositoryContext(repository.Id, repository.PluginType, repositoryId, reference.Repository.Account);
        var composed = pullRequest with { Body = ComposeBody(pullRequest.Body, trigger) };
        try
        {
            var task = await workItems.CreateFromPullRequestAsync(repository.ProjectId, composed, context, ct);
            return ScmTaskResolution.Assigned(task.Id);
        }
        catch (InvalidOperationException)
        {
            // Anlagerennen: Die parallele Anlage gewinnt über den Unique-Index des Referenzschlüssels.
            var concurrent = await PullRequestTaskIdsAsync(pullRequest.Provider, repositoryId, reference.Number, ct);
            if (concurrent.Count >= 1) return ScmTaskResolution.Assigned(concurrent[0]);
            throw;
        }
    }

    private Task<List<Guid>> PullRequestTaskIdsAsync(PullRequestProvider provider, string repositoryId,
        int number, CancellationToken ct)
        => db.PullRequestReferences.AsNoTracking()
            .Where(p => p.Provider == provider && p.RepositoryId == repositoryId && p.PullRequestNumber == number)
            .Select(p => p.WorkItemId).Distinct().ToListAsync(ct);

    /// <summary>Projektion der für den Kontextabgleich benötigten Aufgabenfelder.</summary>
    private sealed record ContextCandidate(Guid Id, string? BranchName, int? IssueNumber);

    /// <summary>Aktive Aufgaben (Status nicht <see cref="WorkItemStatus.Completed"/>/<see cref="WorkItemStatus.Archived"/>)
    /// im Repository-Scope, deren Branch- oder Issue-Bezug denselben Aufgabenkontext wie der Pull Request beschreibt.
    /// Die Datenbankabfrage lädt nur potenzielle Treffer (Branch-Kandidat oder passende Issue-Nummer) als Projektion;
    /// der abschließende Groß-/Kleinschreibungsvergleich des Branchnamens bleibt im Speicher, weil die SQLite-Provider
    /// kein <see cref="StringComparison.OrdinalIgnoreCase"/> übersetzen.</summary>
    private async Task<List<Guid>> FindActiveContextTaskIdsAsync(PullRequest pullRequest,
        IReadOnlyList<GitRepository> repositories, string repositoryId, CancellationToken ct)
    {
        var repositoryIds = repositories.Select(x => (Guid?)x.Id).ToArray();
        var closingNumbers = PullRequestBodyBuilder.ExtractClosingIssueNumbers(pullRequest.Body, repositoryId).ToArray();
        var branchMatchPossible = !string.IsNullOrWhiteSpace(pullRequest.BranchName)
            && string.Equals(pullRequest.SourceRepositoryId, repositoryId, StringComparison.OrdinalIgnoreCase);
        var candidates = await db.WorkItems.AsNoTracking()
            .Where(a => repositoryIds.Contains(a.GitRepositoryId)
                && a.Purpose == null
                && a.Status != WorkItemStatus.Completed && a.Status != WorkItemStatus.Archived
                && ((branchMatchPossible && a.BranchName != null)
                    || (a.IssueReference != null && a.IssueReference.IssueNumber != null
                        && closingNumbers.Contains(a.IssueReference.IssueNumber.Value))))
            .Select(a => new ContextCandidate(a.Id, a.BranchName,
                a.IssueReference == null ? null : a.IssueReference.IssueNumber))
            .ToListAsync(ct);
        return candidates
            .Where(a => MatchesBranch(pullRequest, repositoryId, a) || MatchesClosingIssue(closingNumbers, a))
            .Select(a => a.Id).Distinct().ToList();
    }

    private static bool MatchesBranch(PullRequest pullRequest, string repositoryId, ContextCandidate candidate)
        => !string.IsNullOrWhiteSpace(pullRequest.BranchName)
            && string.Equals(pullRequest.SourceRepositoryId, repositoryId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.BranchName, pullRequest.BranchName, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesClosingIssue(IReadOnlyCollection<int> closingNumbers, ContextCandidate candidate)
        => candidate.IssueNumber is { } issueNumber && closingNumbers.Contains(issueNumber);

    private static GitRepository ChooseRepository(IReadOnlyList<GitRepository> repositories)
        => repositories.OrderBy(x => x.RepositoryUrl, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).FirstOrDefault()
            ?? throw new InvalidOperationException("Die Referenz besitzt kein aufgelöstes lokales Repository.");

    /// <summary>Auftragstext: Referenzbeschreibung (ohne Duplikat des auslösenden Textes),
    /// auslösende Beobachtung und Herkunftsverweis.</summary>
    private static string ComposeBody(string? referenceBody, ScmEventObservation trigger)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(referenceBody)
            && !string.Equals(referenceBody.Trim(), trigger.Body.Trim(), StringComparison.Ordinal))
            parts.Add(referenceBody.Trim());
        if (!string.IsNullOrWhiteSpace(trigger.Body)) parts.Add(trigger.Body.Trim());
        if (!string.IsNullOrWhiteSpace(trigger.Url)) parts.Add(trigger.Url);
        return string.Join($"{Environment.NewLine}{Environment.NewLine}", parts);
    }

    /// <summary>Kanonischer Dedup-Schlüssel einer Issue-Referenz (Namensraum wie <see cref="ScmEventObservation.ContentKey"/>).</summary>
    private static string IssueSourceKey(ScmEventReference reference)
        => string.Join('/', new[] { reference.Repository.ProviderPrefix.ToLowerInvariant(),
            reference.Repository.Host.ToLowerInvariant(), reference.Repository.RepositoryId,
            reference.Kind.ToString(), reference.Number.ToString(CultureInfo.InvariantCulture) }
            .Select(Uri.EscapeDataString));
}
