using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Provider-Workflow-Run, der einem Pull Request zugeordnet wurde.</summary>
public sealed record PullRequestWorkflowRunInfo(
    string ProviderRunId,
    string Name,
    string? Url,
    string? HeadSha,
    string? BranchName,
    WorkflowRunStatus Status,
    WorkflowRunConclusion Conclusion,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool IsPostMerge);
