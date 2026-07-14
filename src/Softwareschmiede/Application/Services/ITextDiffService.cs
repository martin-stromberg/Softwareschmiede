using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Abstraktion des Präsentations-Zeilendiffs für die Dateiexplorer-Anzeige.</summary>
public interface ITextDiffService
{
    /// <summary>Baut aus altem und neuem Dateiinhalt einen zeilenweisen Diff für die Anzeige.</summary>
    /// <param name="originalContent">Ursprünglicher Dateiinhalt, oder <c>null</c>.</param>
    /// <param name="currentContent">Aktueller Dateiinhalt, oder <c>null</c>.</param>
    /// <returns>Der berechnete <see cref="FileTextDiff"/>.</returns>
    FileTextDiff BuildDiff(string? originalContent, string? currentContent);
}
