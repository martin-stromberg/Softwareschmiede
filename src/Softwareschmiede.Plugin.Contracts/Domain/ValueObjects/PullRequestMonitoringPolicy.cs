using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Zentrale Monitoring- und Auto-Complete-Policy fuer Pull-Request-Referenzen.</summary>
public static class PullRequestMonitoringPolicy
{
    /// <summary>Gibt an, ob Status und Workflows gelesen werden duerfen.</summary>
    public static bool CanMonitor(PullRequestReferenzRolle rolle, PullRequestProvider provider)
        => provider == PullRequestProvider.GitHub;

    /// <summary>Gibt an, ob ein periodischer Lauf den Pull Request aktiv abschliessen darf.</summary>
    public static bool CanAutoComplete(PullRequestReferenzRolle rolle, PullRequestProvider provider)
        => rolle == PullRequestReferenzRolle.CreatedByTask && provider == PullRequestProvider.GitHub;

    /// <summary>Liefert die Initialphase einer neu gespeicherten Referenz.</summary>
    public static PullRequestMonitoringPhase GetInitialPhase(PullRequestProvider provider)
        => provider == PullRequestProvider.GitHub
            ? PullRequestMonitoringPhase.Created
            : PullRequestMonitoringPhase.NotMonitored;
}
