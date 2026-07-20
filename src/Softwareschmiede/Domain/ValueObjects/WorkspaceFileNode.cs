using System.Collections.ObjectModel;

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

    /// <summary>Gibt an, ob die Kinder bereits geladen wurden.</summary>
    public bool ChildrenLoaded { get; set; }

    /// <summary>Ebene des Knotens relativ zur Wurzel (0 = oberste Ebene).</summary>
    public int Depth { get; init; }

    /// <summary>Gibt an, ob es sich um einen technischen Platzhalter-Kindknoten handelt, der nur die Anzeige des Aufklapp-Pfeils erzwingt.</summary>
    public bool IsPlaceholder { get; init; }

    /// <summary>Unterknoten.</summary>
    public ObservableCollection<WorkspaceFileNode> Children { get; init; } = [];

    /// <summary>Erstellt einen technischen Platzhalter-Kindknoten für die angegebene Ebene.</summary>
    /// <param name="depth">Ebene des Platzhalter-Knotens relativ zur Wurzel.</param>
    /// <returns>Ein neuer Platzhalter-Knoten mit <see cref="IsPlaceholder"/> gleich <c>true</c>.</returns>
    public static WorkspaceFileNode CreatePlaceholder(int depth) => new()
    {
        Name = string.Empty,
        RelativePath = string.Empty,
        IsDirectory = false,
        IsPlaceholder = true,
        Depth = depth,
    };
}
