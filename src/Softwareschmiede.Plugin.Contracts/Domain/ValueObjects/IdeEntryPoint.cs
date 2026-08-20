namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Ein Einstiegspunkt, den ein IDE-Plugin in der IDE öffnen kann.</summary>
/// <param name="Path">Pfad des Einstiegspunkts (z. B. Solution-Datei oder Repository-Verzeichnis).</param>
/// <param name="DisplayName">Optionale, für die UI-Anzeige geeignete Bezeichnung des Einstiegspunkts.</param>
public sealed record IdeEntryPoint(
    string Path,
    string? DisplayName = null
);
