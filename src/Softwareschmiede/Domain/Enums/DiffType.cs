namespace Softwareschmiede.Domain.Enums;

/// <summary>Rendering-Typ für Diff-Darstellung.</summary>
public enum DiffType
{
    /// <summary>Unified-View (einzelner Stream mit +/- Präfixen).</summary>
    Full,

    /// <summary>Side-by-Side-View (zwei Spalten nebeneinander).</summary>
    SideBySide,

    /// <summary>Split-View (mit Gutter zwischen Original und Neu).</summary>
    Split
}
