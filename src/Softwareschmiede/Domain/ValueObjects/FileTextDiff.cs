namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Ergebnis eines Präsentations-Zeilendiffs zwischen zwei Dateiinhalten.</summary>
/// <param name="Lines">Die einzelnen Diff-Zeilen in Anzeigereihenfolge.</param>
/// <param name="AddedCount">Anzahl hinzugefügter Zeilen.</param>
/// <param name="RemovedCount">Anzahl entfernter Zeilen.</param>
/// <param name="ModifiedCount">Anzahl modifizierter Zeilen.</param>
/// <returns>Ein neues <see cref="FileTextDiff"/>.</returns>
public sealed record FileTextDiff(
    IReadOnlyList<TextDiffLine> Lines,
    int AddedCount,
    int RemovedCount,
    int ModifiedCount);
