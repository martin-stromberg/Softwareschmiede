using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Repräsentiert eine Zeile im Präsentations-Zeilendiff einer Datei.</summary>
/// <param name="Content">Textinhalt der Zeile (bei <see cref="DiffLineStatus.Modified"/> der neue Zeileninhalt).</param>
/// <param name="Status">Änderungsstatus der Zeile.</param>
/// <param name="OldLineNumber">Zeilennummer im alten Inhalt, oder <c>null</c> bei <see cref="DiffLineStatus.Added"/>.</param>
/// <param name="NewLineNumber">Zeilennummer im neuen Inhalt, oder <c>null</c> bei <see cref="DiffLineStatus.Removed"/>.</param>
/// <param name="InlineSegments">Teilabschnitte der Zeile für die Inline-Hervorhebung geänderter Wortteile.</param>
/// <returns>Eine neue <see cref="TextDiffLine"/>.</returns>
public sealed record TextDiffLine(
    string Content,
    DiffLineStatus Status,
    int? OldLineNumber,
    int? NewLineNumber,
    IReadOnlyList<InlineDiffSegment> InlineSegments);
