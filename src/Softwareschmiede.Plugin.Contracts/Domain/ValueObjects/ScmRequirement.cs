namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Gemeinsamer UI- und Workflow-Typ für offene SCM-Anforderungen.</summary>
public sealed record ScmRequirement(ScmRequirementKind Kind, Issue? Issue, ScmAlert? Alert)
{
    /// <summary>Titel der Anforderung.</summary>
    public string Titel => Kind == ScmRequirementKind.Issue ? Issue?.Titel ?? string.Empty : Alert?.Title ?? string.Empty;

    /// <summary>Erste Anzeigezeile für den Anforderungstyp.</summary>
    public string TypText => Kind == ScmRequirementKind.Issue ? "Offene Anforderung" : "GitHub Code Scanning Alert";

    /// <summary>Issue-Nummer für normale Issues.</summary>
    public int? Nummer => Issue?.Nummer;

    /// <summary>Anzeige der Issue-Nummer für normale Issues.</summary>
    public string NummerText => Nummer is null ? string.Empty : $"#{Nummer}";

    /// <summary>Zusätzliche Detailanzeige, vor allem für Alerts.</summary>
    public string DetailText
    {
        get
        {
            if (Kind == ScmRequirementKind.Issue)
            {
                return string.Empty;
            }

            if (Alert is null)
            {
                return string.Empty;
            }

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

    /// <summary>Stabile Quellkennung für Alert-Anforderungen.</summary>
    public string? SourceKey => Alert?.SourceKey;

    /// <summary>Erzeugt eine SCM-Anforderung aus einem Issue.</summary>
    public static ScmRequirement FromIssue(Issue issue) => new(ScmRequirementKind.Issue, issue, null);

    /// <summary>Erzeugt eine SCM-Anforderung aus einem Alert.</summary>
    public static ScmRequirement FromAlert(ScmAlert alert) => new(ScmRequirementKind.Alert, null, alert);
}
