using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Persistierter Workflow-Run zu einem Pull Request.</summary>
public sealed class PullRequestWorkflowRun
{
    /// <summary>Eindeutige ID des Workflow-Runs.</summary>
    public Guid Id { get; set; }

    /// <summary>ID der Pull-Request-Referenz.</summary>
    public Guid PullRequestReferenzId { get; set; }

    /// <summary>Run-ID beim Provider.</summary>
    public string ProviderRunId { get; set; } = string.Empty;

    /// <summary>Name des Workflows oder Checks.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL zum Workflow-Run.</summary>
    public string? Url { get; set; }

    /// <summary>Head-SHA des Workflow-Runs.</summary>
    public string? HeadSha { get; set; }

    /// <summary>Branch des Workflow-Runs.</summary>
    public string? BranchName { get; set; }

    /// <summary>Status des Workflow-Runs.</summary>
    public WorkflowRunStatus Status { get; set; } = WorkflowRunStatus.Unknown;

    /// <summary>Abschlussbewertung des Workflow-Runs.</summary>
    public WorkflowRunConclusion Conclusion { get; set; } = WorkflowRunConclusion.Unknown;

    /// <summary>Startzeitpunkt, sofern bekannt.</summary>
    public DateTimeOffset? StartedAtUtc { get; set; }

    /// <summary>Abschlusszeitpunkt, sofern bekannt.</summary>
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>Gibt an, ob der Run nach dem Merge zugeordnet wurde.</summary>
    public bool IsPostMerge { get; set; }

    /// <summary>Letzter Aktualisierungszeitpunkt des lokalen Eintrags.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }

    /// <summary>Navigation zur Pull-Request-Referenz.</summary>
    public PullRequestReferenz PullRequestReferenz { get; set; } = null!;
}
