namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Status eines Remote-Abrufs der Repository-Verzeichnisstruktur.</summary>
public enum RepositoryStructureLoadStatus
{
    /// <summary>Die Verzeichnisstruktur wurde erfolgreich geladen, auch wenn keine Unterverzeichnisse vorhanden sind.</summary>
    Success,

    /// <summary>Der Abruf ist technisch fehlgeschlagen.</summary>
    Failed,

    /// <summary>Das Plugin unterstützt den Remote-Abruf der Verzeichnisstruktur nicht.</summary>
    NotSupported,
}
