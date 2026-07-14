using System.Collections.ObjectModel;

namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Repräsentiert einen Branch-Commit im Repository-Explorer.</summary>
public sealed class BranchCommit
{
    /// <summary>Vollständiger SHA-Hash des Commits.</summary>
    public string Sha { get; init; } = string.Empty;

    /// <summary>Abgekürzter SHA-Hash (7 Zeichen).</summary>
    public string ShortSha { get; init; } = string.Empty;

    /// <summary>Commit-Nachricht (erste Zeile).</summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>Gibt an, ob der Commit-Knoten im Tree-View aufgeklappt ist.</summary>
    public bool IsExpanded { get; set; }

    /// <summary>Gibt an, ob die Kind-Elemente (geänderte Dateien) bereits geladen wurden.</summary>
    public bool ChildrenLoaded { get; set; }

    /// <summary>Gibt an, ob die Dateien gerade geladen werden.</summary>
    public bool IsLoadingFiles { get; set; }

    /// <summary>Fehlermeldung beim Laden der Dateien, falls aufgetreten.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Liste der im Commit geänderten Dateien.</summary>
    public ObservableCollection<WorkspaceFileNode> Files { get; set; } = [];
}
