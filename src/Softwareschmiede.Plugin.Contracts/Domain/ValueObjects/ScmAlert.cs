namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Security- oder Quality-Alert aus einem SCM-Provider.</summary>
public sealed record ScmAlert(
    int AlertNumber,
    string SourceKey,
    ScmAlertType AlertType,
    string Title,
    string? Description,
    string? AlertUrl,
    string? Severity,
    string? State,
    string? ToolName,
    string? RuleId,
    string? RuleName,
    string? FilePath,
    int? StartLine);
