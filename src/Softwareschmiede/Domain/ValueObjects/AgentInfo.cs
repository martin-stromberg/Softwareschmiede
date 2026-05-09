namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Informationen über einen KI-Agenten.</summary>
/// <param name="Name">Name des Agenten.</param>
/// <param name="Beschreibung">Optionale Beschreibung des Agenten.</param>
/// <param name="DateiPfad">Dateipfad zur Agenten-Konfigurationsdatei.</param>
public sealed record AgentInfo(
    string Name,
    string? Beschreibung,
    string DateiPfad
);
