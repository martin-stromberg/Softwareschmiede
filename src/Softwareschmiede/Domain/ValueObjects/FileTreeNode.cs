namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Repräsentiert einen Knoten im Dateibaum eines Agentenpakets.</summary>
public sealed class FileTreeNode
{
    /// <summary>Anzeigename des Knotens.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Pfad relativ zum Paket-Root. Leer für den Paket-Root selbst.</summary>
    public string RelativePath { get; init; } = string.Empty;

    /// <summary>Name des zugehörigen Agentenpakets.</summary>
    public string PackageName { get; init; } = string.Empty;

    /// <summary>Gibt an, ob der Knoten ein Verzeichnis ist.</summary>
    public bool IsDirectory { get; init; }

    /// <summary>Gibt an, ob dies der Paket-Root-Knoten ist (RelativePath ist leer und IsDirectory ist true).</summary>
    public bool IsPackageRoot => IsDirectory && RelativePath.Length == 0;

    /// <summary>Gibt an, ob der Knoten aufgeklappt angezeigt wird (nur für Verzeichnisse relevant).</summary>
    public bool IsExpanded { get; set; }

    /// <summary>Unterknoten dieses Verzeichnisses.</summary>
    public List<FileTreeNode> Children { get; init; } = [];
}
