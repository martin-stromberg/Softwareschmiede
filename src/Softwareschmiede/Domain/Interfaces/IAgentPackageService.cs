using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Service für Agentenpakete.</summary>
public interface IAgentPackageService
{
    /// <summary>Gibt alle verfügbaren Agentenpakete zurück.</summary>
    /// <param name="ct">Cancellation Token.</param>
    Task<IEnumerable<AgentPackageInfo>> GetPackagesAsync(CancellationToken ct = default);

    /// <summary>Gibt ein spezifisches Agentenpaket zurück.</summary>
    /// <param name="name">Name des Agentenpaketes.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<AgentPackageInfo?> GetPackageAsync(string name, CancellationToken ct = default);
}
