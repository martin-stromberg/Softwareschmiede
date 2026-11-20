using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.Application.Services;

/// <summary>Löst Platzhalter in Promptvorlagen anhand des aktuellen Aufgabenkontexts auf.</summary>
public sealed class PromptTemplatePlaceholderService
{
    /// <summary>Ersetzt bekannte Platzhalter im Prompttext.</summary>
    public string Resolve(string prompttext, WorkItem? workItem)
    {
        if (string.IsNullOrEmpty(prompttext))
            return string.Empty;

        return prompttext
            .Replace("%ProjectName%", workItem?.Project?.Name ?? string.Empty, StringComparison.Ordinal)
            .Replace("%TaskName%", workItem?.Title ?? string.Empty, StringComparison.Ordinal)
            .Replace("%RepositoryUrl%", workItem?.GitRepository?.RepositoryUrl ?? string.Empty, StringComparison.Ordinal);
    }
}
