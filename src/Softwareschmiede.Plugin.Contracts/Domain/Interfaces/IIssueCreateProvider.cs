using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Optionale Provider-Fähigkeit zum Anlegen von Issues.</summary>
public interface IIssueCreateProvider
{
    /// <summary>Prüft, ob Issue-Anlage für das Repository unterstützt wird.</summary>
    /// <param name="repositoryId">Repository-Identifier.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<bool> CanCreateIssueAsync(string repositoryId, CancellationToken ct = default);

    /// <summary>Legt ein Issue beim Provider an.</summary>
    /// <param name="repositoryId">Repository-Identifier.</param>
    /// <param name="request">Anlagedaten.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<IssueCreateResult> CreateIssueAsync(string repositoryId, IssueCreateRequest request, CancellationToken ct = default);
}
