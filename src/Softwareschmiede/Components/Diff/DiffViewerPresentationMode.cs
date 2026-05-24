namespace Softwareschmiede.Components.Diff;

/// <summary>
/// Defines how the diff viewer should be presented.
/// </summary>
public enum DiffViewerPresentationMode
{
    /// <summary>Rendered inside another page (without page-level chrome).</summary>
    Embedded,

    /// <summary>Rendered as standalone page content.</summary>
    Standalone,
}
