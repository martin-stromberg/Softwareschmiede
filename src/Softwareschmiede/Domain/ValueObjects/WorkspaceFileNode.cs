namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Repräsentiert einen Eintrag im Workspace-Browser.</summary>
public sealed class WorkspaceFileNode
{
    /// <summary>Anzeigename des Knotens.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Pfad relativ zum Repository-Root.</summary>
    public string RelativePath { get; init; } = string.Empty;

    /// <summary>Gibt an, ob der Knoten ein Verzeichnis ist.</summary>
    public bool IsDirectory { get; init; }

    /// <summary>Gibt an, ob der Knoten gelöscht ist.</summary>
    public bool IsDeleted { get; init; }

    /// <summary>Ursprünglicher Pfad bei Rename/Copy.</summary>
    public string? SourceRelativePath { get; init; }

    /// <summary>Git-Status des Knotens.</summary>
    public WorkspaceFileStatus? Status { get; init; }

    /// <summary>Optionaler Commit-Hash, wenn der Knoten aus einem Commit-Baum stammt.</summary>
    public string? CommitSha { get; init; }

    /// <summary>Gibt an, ob das Verzeichnis aufgeklappt angezeigt wird.</summary>
    public bool IsExpanded { get; set; }

    /// <summary>Gibt an, ob die Kinder bereits geladen wurden.</summary>
    public bool ChildrenLoaded { get; set; }

    /// <summary>Unterknoten.</summary>
    public List<WorkspaceFileNode> Children { get; init; } = [];

    /// <summary>Anzahl geänderter Dateien unterhalb dieses Knotens.</summary>
    public int ChangedFileCount { get; set; }
}
