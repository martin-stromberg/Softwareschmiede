using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.ValueObjects;
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

    /// <summary>Baut einen Pull-Request-Body aus den Commits des Aufgabenbranches.</summary>
    public static string BuildFromCommits(Aufgabe aufgabe, IReadOnlyList<BranchCommit> commits)
    {
        var body = commits.Count == 0
            ? "Keine Commits gegenüber dem Zielbranch ermittelt."
            : string.Join(
                Environment.NewLine,
                commits.Select(commit => $"- `{ResolveShortSha(commit)}` {commit.Subject}".TrimEnd()));

        return Build(aufgabe, $"## Commits{Environment.NewLine}{Environment.NewLine}{body}");
    }

    /// <summary>Extrahiert die Issue-Nummern aller Closing-Direktiven im Text. Mit gesetztem
    /// <paramref name="repositoryId"/> zählen repository-qualifizierte Direktiven nur bei Übereinstimmung.</summary>
    internal static IReadOnlyCollection<int> ExtractClosingIssueNumbers(string? body, string? repositoryId = null)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return Array.Empty<int>();
        }

        const string pattern = @"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s+(?<repository>[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+)?#(?<number>\d+)\b";
        var numbers = new HashSet<int>();
        foreach (Match match in Regex.Matches(body, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var qualifier = match.Groups["repository"].Value;
            if (repositoryId is not null && qualifier.Length > 0
                && !string.Equals(qualifier, repositoryId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(match.Groups["number"].Value, out var number))
            {
                numbers.Add(number);
            }
        }

        return numbers;
    }

    private static bool ContainsClosingDirectiveForIssue(string body, int issueNummer)
        => ExtractClosingIssueNumbers(body).Contains(issueNummer);

    private static string ResolveShortSha(BranchCommit commit)
    {
        if (!string.IsNullOrWhiteSpace(commit.ShortSha))
        {
            return commit.ShortSha;
        }

        return commit.Sha.Length <= 7 ? commit.Sha : commit.Sha[..7];
    }
}
