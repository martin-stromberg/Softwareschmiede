namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Gemeinsamer UI- und Workflow-Typ fuer offene SCM-Anforderungen.</summary>
public sealed record ScmRequirement(
    ScmRequirementKind Kind,
    Issue? Issue,
    ScmAlert? Alert,
    PullRequest? PullRequest,
    ScmRepositoryContext? RepositoryContext)
{
    /// <summary>Titel der Anforderung.</summary>
    public string Titel => Kind switch
    {
        ScmRequirementKind.Issue => Issue?.Titel ?? string.Empty,
        ScmRequirementKind.PullRequest => PullRequest?.Titel ?? string.Empty,
        _ => Alert?.Title ?? string.Empty
    };

    /// <summary>Erste Anzeigezeile fuer den Anforderungstyp.</summary>
    public string TypText => Kind switch
    {
        ScmRequirementKind.Issue => "Offene Anforderung",
        ScmRequirementKind.PullRequest => "Pull Request",
        _ => "GitHub Code Scanning Alert"
    };

    /// <summary>Issue- oder Pull-Request-Nummer.</summary>
    public int? Nummer => Issue?.Nummer ?? PullRequest?.Nummer;

    /// <summary>Anzeige der Issue- oder Pull-Request-Nummer.</summary>
    public string NummerText => Nummer is null ? string.Empty : $"#{Nummer}";

    /// <summary>Stabile sichtbare Providerbezeichnung fuer Pull Requests.</summary>
    public string ProviderText => PullRequest is null
        ? string.Empty
        : PullRequestProviderDescriptor.GetDisplayName(PullRequest.Provider);

    /// <summary>Zusaetzliche Detailanzeige.</summary>
    public string DetailText
    {
        get
        {
            if (Kind == ScmRequirementKind.Issue)
                return string.Empty;

            if (Kind == ScmRequirementKind.PullRequest && PullRequest is not null)
            {
                var branchText = string.IsNullOrWhiteSpace(PullRequest.TargetBranch)
                    ? PullRequest.BranchName
                    : $"{PullRequest.BranchName} -> {PullRequest.TargetBranch}";
                return $"{ProviderText} - {branchText}";
            }

            if (Alert is null)
                return string.Empty;

            var parts = new[]
            {
                Alert.Severity,
                Alert.RuleName ?? Alert.RuleId,
                Alert.FilePath is null
                    ? null
                    : Alert.StartLine is null ? Alert.FilePath : $"{Alert.FilePath}:{Alert.StartLine}"
            }.Where(part => !string.IsNullOrWhiteSpace(part));

            return string.Join(" - ", parts);
        }
    }

    /// <summary>Stabile Quellkennung fuer Alert-Anforderungen.</summary>
    public string? SourceKey => Alert?.SourceKey;

    /// <summary>Strukturierte Automatisierungskennung des Vorschlags.</summary>
    public string AutomationText => RepositoryContext is null
        ? $"{Kind}:{NummerText}:{Titel}"
        : $"{Kind}:{ProviderText}:{NummerText}:{RepositoryContext.GitRepositoryId:N}:{RepositoryContext.RepositoryId}";

    /// <summary>Erzeugt eine SCM-Anforderung aus einem Issue und Repository-Snapshot.</summary>
    public static ScmRequirement FromIssue(Issue issue, ScmRepositoryContext repositoryContext)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(repositoryContext);
        return new ScmRequirement(ScmRequirementKind.Issue, issue, null, null, repositoryContext);
    }

    /// <summary>Erzeugt eine SCM-Anforderung aus einem Pull Request und Repository-Snapshot.</summary>
    public static ScmRequirement FromPullRequest(PullRequest pullRequest, ScmRepositoryContext repositoryContext)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        ArgumentNullException.ThrowIfNull(repositoryContext);
        return new ScmRequirement(ScmRequirementKind.PullRequest, null, null, pullRequest, repositoryContext);
    }

    /// <summary>Erzeugt eine SCM-Anforderung aus einem Alert.</summary>
    public static ScmRequirement FromAlert(ScmAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        return new ScmRequirement(ScmRequirementKind.Alert, null, alert, null, null);
    }
}
