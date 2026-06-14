namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Repräsentiert die Vorschau einer selektierten Datei.</summary>
public sealed record FilePreview(
    string RelativePath,
    string? SourceRelativePath,
    bool IsDeleted,
    bool IsBinary,
    bool IsTooBig,
    string? CurrentContent,
    string? OriginalContent,
    string? Hint)
{
    /// <summary>Gibt an, ob ein Vergleich der Dateiinhalte möglich ist.</summary>
    public bool HasComparison => !IsBinary && !IsTooBig;
}
