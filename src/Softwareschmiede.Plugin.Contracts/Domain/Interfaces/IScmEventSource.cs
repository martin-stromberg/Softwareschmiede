using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Optionale Fähigkeit, vollständige beobachtete Textstände einer verknüpften Referenz zu lesen.</summary>
public interface IScmEventSource
{
    /// <summary>Lädt die authentifizierte Identität und den kanonischen Repository-Kontext.</summary>
    Task<ScmEventRepository> GetEventRepositoryAsync(ScmRepositoryContext repository, CancellationToken ct = default);

    /// <summary>Liest eine Quelle vollständig; bei Seitenfehlern wird kein Teilresultat geliefert.</summary>
    Task<ScmEventBatch> ReadEventsAsync(ScmEventReadRequest request, CancellationToken ct = default);
}
