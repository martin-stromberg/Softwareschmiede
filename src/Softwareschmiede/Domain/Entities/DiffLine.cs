using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>
/// Einzelne Zeile innerhalb eines Diff-Blocks.
/// </summary>
public sealed class DiffLine
{
    /// <summary>Eindeutige ID der Diff-Zeile.</summary>
    public Guid Id { get; set; }

    /// <summary>ID des zugehörigen Diff-Blocks.</summary>
    public Guid DiffBlockId { get; set; }

    /// <summary>Status der Zeile (Added, Removed, Modified, Context).</summary>
    public DiffLineStatus LineStatus { get; set; }

    /// <summary>Inhalt der Zeile (Quellcode, Whitespace-erhalten).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Zeilennummer in der Quelldatei (null für Added-Zeilen).</summary>
    public int? SourceLineNumber { get; set; }

    /// <summary>Zeilennummer in der Zieldatei (null für Removed-Zeilen).</summary>
    public int? TargetLineNumber { get; set; }

    /// <summary>Reihenfolge der Zeile im Block (für korrekte Sortierung).</summary>
    public int LineSequence { get; set; }

    /// <summary>Navigationseigenschaft zum zugehörigen Diff-Block.</summary>
    public DiffBlock DiffBlock { get; set; } = null!;
}
