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
    public bool HasComparison => !IsBinary && !IsTooBig;
}
