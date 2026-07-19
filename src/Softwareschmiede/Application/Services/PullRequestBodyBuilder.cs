using Softwareschmiede.Domain.Entities;
using System.Text.RegularExpressions;

namespace Softwareschmiede.Application.Services;

/// <summary>Baut Pull-Request-Bodys fuer Aufgaben mit optionaler Issue-Referenz.</summary>
internal static class PullRequestBodyBuilder
{
    /// <summary>Ergaenzt bei gueltiger Issue-Referenz eine GitHub-Closing-Direktive, ohne sie zu duplizieren.</summary>
    public static string Build(Aufgabe aufgabe, string? body)
    {
        var prBody = body ?? $"Automatisch erstellt für Aufgabe: {aufgabe.Titel}";
        var issueNummer = aufgabe.IssueReferenz?.IssueNummer;

        if (issueNummer is not > 0)
        {
            return prBody;
        }

        if (ContainsClosingDirectiveForIssue(prBody, issueNummer.Value))
        {
            return prBody;
        }

        var trimmedBody = prBody.TrimEnd();
        return string.IsNullOrWhiteSpace(trimmedBody)
            ? $"Closes #{issueNummer.Value}"
            : $"{trimmedBody}{Environment.NewLine}{Environment.NewLine}Closes #{issueNummer.Value}";
    }

    private static bool ContainsClosingDirectiveForIssue(string body, int issueNummer)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        var pattern = $@"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s+(?:[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+)?#{issueNummer}\b";
        return Regex.IsMatch(body, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
