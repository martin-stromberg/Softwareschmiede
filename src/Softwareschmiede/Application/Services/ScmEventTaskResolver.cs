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
public sealed record ScmTaskResolution(ScmTaskResolutionKind Kind, Guid? AufgabeId)
{
    /// <summary>Mehrere Aufgaben tragen dieselbe Referenz.</summary>
    public static readonly ScmTaskResolution Ambiguous = new(ScmTaskResolutionKind.Ambiguous, null);
    /// <summary>Keine Aufgabe zuordenbar und keine anlegbar.</summary>
    public static readonly ScmTaskResolution Unassigned = new(ScmTaskResolutionKind.Unassigned, null);
    /// <summary>Genau eine Aufgabe wurde gefunden oder angelegt.</summary>
    public static ScmTaskResolution Assigned(Guid aufgabeId)
        => aufgabeId == Guid.Empty
            ? throw new InvalidOperationException("Eine zugeordnete Auflösung benötigt eine Aufgaben-ID.")
            : new ScmTaskResolution(ScmTaskResolutionKind.Assigned, aufgabeId);
}

/// <summary>Löst eine SCM-Referenz zu genau einer lokalen Aufgabe auf: Wiederverwendung bei
/// eindeutigem Treffer, Anlage aus den Ereignisdaten bei fehlendem Treffer, Mehrdeutigkeitsdiagnose
/// bei mehreren Treffern. Läuft vor der Inbox-Aufnahme und hält deren Aufgabeninvariante.</summary>
public sealed class ScmEventTaskResolver(
    SoftwareschmiededDbContext db, AufgabeService aufgaben, ILogger<ScmEventTaskResolver> logger)
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
        if (issue is null || issue.Nummer != reference.Number)
        {
            if (issue is not null)
                logger.LogWarning("Issue-Anlagedaten von {Number} weichen von der Referenz {Reference} ab.", issue.Nummer, reference.Number);
            return ScmTaskResolution.Unassigned;
        }
        var repositoryIds = repositories.Select(x => (Guid?)x.Id).ToArray();
        var taskIds = await db.Aufgaben.AsNoTracking()
            .Where(a => repositoryIds.Contains(a.GitRepositoryId)
                && a.IssueReferenz != null && a.IssueReferenz.IssueNummer == reference.Number)
            .Select(a => a.Id).Distinct().ToListAsync(ct);
        if (taskIds.Count > 1) return ScmTaskResolution.Ambiguous;
        if (taskIds.Count == 1) return ScmTaskResolution.Assigned(taskIds[0]);
        var repository = ChooseRepository(repositories);
        var composed = issue with { Body = ComposeBody(issue.Body, trigger) };
        var task = await aufgaben.CreateFromIssueEventAsync(repository.ProjektId, composed,
            repository.Id, IssueSourceKey(reference), ct);
        return ScmTaskResolution.Assigned(task.Id);
    }

    private async Task<ScmTaskResolution> ResolvePullRequestAsync(ScmEventReference reference, IScmEventSource source,
        ScmEventObservation trigger, IReadOnlyList<GitRepository> repositories, CancellationToken ct)
    {
        var pullRequest = await source.ReadPullRequestAsync(reference, ct);
        if (pullRequest is null || pullRequest.Nummer != reference.Number)
        {
            if (pullRequest is not null)
                logger.LogWarning("Pull-Request-Anlagedaten von {Number} weichen von der Referenz {Reference} ab.", pullRequest.Nummer, reference.Number);
            return ScmTaskResolution.Unassigned;
        }
        var repositoryId = PullRequestRepositoryId.Normalize(pullRequest.Provider,
            $"{reference.Repository.Owner}/{reference.Repository.Name}");
        var taskIds = await PullRequestTaskIdsAsync(pullRequest.Provider, repositoryId, reference.Number, ct);
        if (taskIds.Count > 1) return ScmTaskResolution.Ambiguous;
        if (taskIds.Count == 1) return ScmTaskResolution.Assigned(taskIds[0]);
        var repository = ChooseRepository(repositories);
        var context = new ScmRepositoryContext(repository.Id, repository.PluginTyp, repositoryId, reference.Repository.Konto);
        var composed = pullRequest with { Body = ComposeBody(pullRequest.Body, trigger) };
        try
        {
            var task = await aufgaben.CreateFromPullRequestAsync(repository.ProjektId, composed, context, ct);
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
        => db.PullRequestReferenzen.AsNoTracking()
            .Where(p => p.Provider == provider && p.RepositoryId == repositoryId && p.PullRequestNumber == number)
            .Select(p => p.AufgabeId).Distinct().ToListAsync(ct);

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
