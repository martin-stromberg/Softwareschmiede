using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Optionale SCM-Provider-Fähigkeit zum Laden von Security- und Quality-Alerts.</summary>
public interface IScmAlertProvider
{
    /// <summary>Ruft offene Alerts aus dem Repository ab.</summary>
    /// <param name="repositoryId">Repository-Identifier oder Repository-URL.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<IEnumerable<ScmAlert>> GetAlertsAsync(string repositoryId, CancellationToken ct = default);
}
