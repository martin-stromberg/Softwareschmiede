using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>
/// Gruppierte Änderungsblöcke innerhalb eines Diffs (zusammenhängende Änderungen).
/// </summary>
public sealed class DiffBlock
{
    /// <summary>Eindeutige ID des Diff-Blocks.</summary>
    public Guid Id { get; set; }

    /// <summary>ID des zugehörigen Diff-Ergebnisses.</summary>
    public Guid DiffResultId { get; set; }

    /// <summary>Typ des Blocks (Added, Removed, Modified, Context).</summary>
    public DiffBlockType BlockType { get; set; }

    /// <summary>Startzeile in der Quelldatei (0 für Added-Blöcke).</summary>
    public int SourceStartLine { get; set; }

    /// <summary>Endzeile in der Quelldatei.</summary>
    public int SourceEndLine { get; set; }

    /// <summary>Startzeile in der Zieldatei (0 für Removed-Blöcke).</summary>
    public int TargetStartLine { get; set; }

    /// <summary>Endzeile in der Zieldatei.</summary>
    public int TargetEndLine { get; set; }

    /// <summary>Reihenfolge des Blocks im Diff (für korrekte Sortierung).</summary>
    public int BlockSequence { get; set; }

    /// <summary>Navigationseigenschaft zum zugehörigen Diff-Ergebnis.</summary>
    public DiffResult DiffResult { get; set; } = null!;

    /// <summary>Zeilen dieses Blocks (kaskadierendes Löschen).</summary>
    public List<DiffLine> DiffLines { get; set; } = [];
}
