namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Ergebnis der Arbeitsverzeichnis-Auflösung.</summary>
/// <param name="ResolvedPath">Effektiver, verwendbarer Basispfad.</param>
/// <param name="UsedFallback">Gibt an, ob auf den Temp-Default zurückgefallen wurde.</param>
/// <param name="ReasonCode">Grundcode für die Entscheidung.</param>
/// <param name="ConfiguredPath">Optional der konfigurierte Pfadwert.</param>
public sealed record ArbeitsverzeichnisResolutionResult(
    string ResolvedPath,
    bool UsedFallback,
    string ReasonCode,
    string? ConfiguredPath
);
