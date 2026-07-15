namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Ergebnis eines Präsentations-Zeilendiffs zwischen zwei Dateiinhalten.</summary>
/// <param name="Lines">Die einzelnen Diff-Zeilen in Anzeigereihenfolge.</param>
/// <returns>Ein neues <see cref="FileTextDiff"/>.</returns>
public sealed record FileTextDiff(IReadOnlyList<TextDiffLine> Lines);
