using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Optionale Provider-Fähigkeit zum Laden von Issue-Templates.</summary>
public interface IIssueTemplateProvider
{
    /// <summary>Lädt Issue-Templates für ein Repository.</summary>
    /// <param name="repositoryId">Repository-Identifier.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<IssueTemplateLoadResult> GetIssueTemplatesAsync(string repositoryId, CancellationToken ct = default);
}
