namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Repräsentiert einen Eintrag in der Verzeichnisstruktur eines externen Repositories.</summary>
/// <param name="Path">Relativer Pfad des Eintrags innerhalb des Repositories.</param>
/// <param name="IsDirectory">Gibt an, ob es sich bei dem Eintrag um ein Verzeichnis handelt.</param>
public sealed record RepositoryDirectoryEntry(string Path, bool IsDirectory);
