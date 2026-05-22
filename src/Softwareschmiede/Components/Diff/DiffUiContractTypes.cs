namespace Softwareschmiede.Components.Diff;

/// <summary>
/// Diff view mode enumeration for display options.
/// </summary>
public enum DiffViewMode
{
    /// <summary>Side-by-side view with source and target columns.</summary>
    SideBySide,

    /// <summary>Split view with gutter between source and target.</summary>
    Split,

    /// <summary>Unified view with single column and +/- indicators.</summary>
    Unified
}

/// <summary>
/// Navigation direction for moving between changes.
/// </summary>
public enum NavigationDirection
{
    /// <summary>Move to previous change.</summary>
    Previous,

    /// <summary>Move to next change.</summary>
    Next
}

/// <summary>
/// Export format for diff data.
/// </summary>
public enum ExportFormat
{
    /// <summary>HTML format with styling.</summary>
    Html,

    /// <summary>PDF format for printing.</summary>
    Pdf,

    /// <summary>Plain text format.</summary>
    Text
}
