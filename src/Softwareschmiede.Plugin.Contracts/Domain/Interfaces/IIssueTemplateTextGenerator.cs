namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Optionale KI-Fähigkeit zum einmaligen Ausfüllen eines Issue-Templates.</summary>
public interface IIssueTemplateTextGenerator
{
    /// <summary>Füllt ein Issue-Template anhand der Originalanforderung aus.</summary>
    /// <param name="templateBody">Template-Inhalt.</param>
    /// <param name="originalRequirement">Originalanforderung der Aufgabe.</param>
    /// <param name="ct">Cancellation Token.</param>
    Task<string> FillIssueTemplateAsync(string templateBody, string? originalRequirement, CancellationToken ct = default);
}
