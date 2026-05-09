namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Informationen über ein Agentenpaket.</summary>
/// <param name="Name">Name des Pakets.</param>
/// <param name="Pfad">Dateisystempfad des Pakets.</param>
/// <param name="Agenten">Verfügbare Agenten in diesem Paket.</param>
/// <param name="Dateien">Liste aller Dateien im Paket (relative Pfade).</param>
public sealed record AgentPackageInfo(
    string Name,
    string Pfad,
    IReadOnlyList<AgentInfo> Agenten,
    IReadOnlyList<string> Dateien
);
