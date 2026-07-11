using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.Application.Services;

/// <summary>Löst Platzhalter in Promptvorlagen anhand des aktuellen Aufgabenkontexts auf.</summary>
public sealed class PromptVorlagenPlatzhalterService
{
    /// <summary>Ersetzt bekannte Platzhalter im Prompttext.</summary>
    public string Resolve(string prompttext, Aufgabe? aufgabe)
    {
        if (string.IsNullOrEmpty(prompttext))
            return string.Empty;

        return prompttext
            .Replace("%ProjectName%", aufgabe?.Projekt?.Name ?? string.Empty, StringComparison.Ordinal)
            .Replace("%TaskName%", aufgabe?.Titel ?? string.Empty, StringComparison.Ordinal)
            .Replace("%RepositoryUrl%", aufgabe?.GitRepository?.RepositoryUrl ?? string.Empty, StringComparison.Ordinal);
    }
}
