using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Optionale Fähigkeit, vollständige beobachtete Textstände einer verknüpften Referenz zu lesen.</summary>
public interface IScmEventSource
{
    /// <summary>Lädt die authentifizierte Identität und den kanonischen Repository-Kontext.</summary>
    Task<ScmEventRepository> GetEventRepositoryAsync(ScmRepositoryContext repository, CancellationToken ct = default);

    /// <summary>Liest eine Quelle vollständig; bei Seitenfehlern wird kein Teilresultat geliefert.</summary>
    Task<ScmEventBatch> ReadEventsAsync(ScmEventReadRequest request, CancellationToken ct = default);

    /// <summary>Listet die zu überwachenden Referenzen eines Repositories: immer die offenen,
    /// zusätzlich geschlossene mit Aktivität ab <paramref name="sinceUtc"/> (<c>null</c> = nur offene).</summary>
    Task<IReadOnlyList<ScmEventReference>> EnumerateReferencesAsync(ScmEventRepository repository,
        DateTimeOffset? sinceUtc, CancellationToken ct = default);

    /// <summary>Lädt die Anlagedaten einer Issue-Referenz (Titel, Text, Metadaten, URL);
    /// <c>null</c>, wenn die Referenz nicht (mehr) oder nicht als Issue existiert.</summary>
    Task<Issue?> ReadIssueAsync(ScmEventReference reference, CancellationToken ct = default);

    /// <summary>Lädt die Anlagedaten einer Pull-Request-Referenz inklusive Quell- und Ziel-Branch;
    /// <c>null</c>, wenn die Referenz nicht (mehr) oder nicht als Pull Request existiert.</summary>
    Task<PullRequest?> ReadPullRequestAsync(ScmEventReference reference, CancellationToken ct = default);
}
