using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Entities;

/// <summary>Persistierte Pull-Request-Referenz einer Aufgabe.</summary>
public sealed class PullRequestReferenz
{
    /// <summary>Eindeutige ID der Pull-Request-Referenz.</summary>
    public Guid Id { get; set; }

    /// <summary>ID der zugehoerigen Aufgabe.</summary>
    public Guid AufgabeId { get; set; }

    /// <summary>Provider des Pull Requests.</summary>
    public PullRequestProvider Provider { get; set; }

    /// <summary>Repository-Identifier beim Provider, z. B. owner/repo.</summary>
    public string RepositoryId { get; set; } = string.Empty;

    /// <summary>Pull-Request-Nummer beim Provider.</summary>
    public int PullRequestNumber { get; set; }

    /// <summary>Optionale eindeutige Provider-ID des Pull Requests.</summary>
    public string? ProviderPullRequestId { get; set; }

    /// <summary>URL des Pull Requests.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Titel des Pull Requests.</summary>
    public string Titel { get; set; } = string.Empty;

    /// <summary>Quellbranch.</summary>
    public string SourceBranch { get; set; } = string.Empty;

    /// <summary>Zielbranch.</summary>
    public string TargetBranch { get; set; } = string.Empty;

    /// <summary>Head-SHA des Pull Requests.</summary>
    public string? HeadSha { get; set; }

    /// <summary>Merge-Commit-SHA, sofern bekannt.</summary>
    public string? MergeCommitSha { get; set; }

    /// <summary>Status des Pull Requests.</summary>
    public PullRequestStatus Status { get; set; } = PullRequestStatus.Unknown;

    /// <summary>Merge-Status des Pull Requests.</summary>
    public PullRequestMergeStatus MergeStatus { get; set; } = PullRequestMergeStatus.Unknown;

    /// <summary>Aktuelle Monitoring-Phase.</summary>
    public PullRequestMonitoringPhase MonitoringPhase { get; set; } = PullRequestMonitoringPhase.Created;

    /// <summary>Zeitpunkt der letzten Statuspruefung.</summary>
    public DateTimeOffset? LastCheckedUtc { get; set; }

    /// <summary>Zeitpunkt der naechsten Statuspruefung.</summary>
    public DateTimeOffset? NextCheckUtc { get; set; }

    /// <summary>Letzter sichtbarer Fehler oder Blockierungsgrund.</summary>
    public string? LastError { get; set; }

    /// <summary>Erstellungszeitpunkt des lokalen Referenzeintrags.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Navigation zur Aufgabe.</summary>
    public Aufgabe Aufgabe { get; set; } = null!;

    /// <summary>Zugeordnete Workflow-Runs.</summary>
    public List<PullRequestWorkflowRun> WorkflowRuns { get; set; } = [];
}
