using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

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

    /// <summary>Sichtbare Providerbezeichnung.</summary>
    public string ProviderText => PullRequestProviderDescriptor.GetDisplayName(Provider);

    /// <summary>Fachliche Rolle dieser Referenz an der Aufgabe.</summary>
    public PullRequestReferenzRolle Rolle { get; set; } = PullRequestReferenzRolle.CreatedByTask;

    /// <summary>Sichtbare Rollenbezeichnung.</summary>
    public string RolleText => Rolle == PullRequestReferenzRolle.ReviewSource
        ? "Review-Quelle"
        : "Aus Aufgabe erstellt";

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

    /// <summary>Provider-Identifier des Quell-Repositories.</summary>
    public string SourceRepositoryId { get; set; } = string.Empty;

    /// <summary>Clone-URL des Quell-Repositories, falls es vom Ziel-Repository abweicht.</summary>
    public string? SourceRepositoryUrl { get; set; }

    /// <summary>Providerseitig fetchbare Quell-Referenz.</summary>
    public string? SourceRef { get; set; }

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

    /// <summary>Sichtbare Monitoring-Phase.</summary>
    public string MonitoringPhaseText => MonitoringPhase == PullRequestMonitoringPhase.NotMonitored
        ? "Nicht automatisch ueberwacht"
        : MonitoringPhase.ToString();

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
