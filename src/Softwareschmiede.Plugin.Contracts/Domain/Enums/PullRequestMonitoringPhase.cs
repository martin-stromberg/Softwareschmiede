namespace Softwareschmiede.Domain.Enums;

/// <summary>Phase der lokalen Pull-Request-Ueberwachung.</summary>
public enum PullRequestMonitoringPhase
{
    /// <summary>Pull Request wurde erstellt und wartet auf die erste Statuspruefung.</summary>
    Created,

    /// <summary>Pre-Merge-Workflows laufen oder wurden noch nicht abgeschlossen.</summary>
    PreMergeRunning,

    /// <summary>Pre-Merge-Workflows wurden erfolgreich abgeschlossen.</summary>
    PreMergeSucceeded,

    /// <summary>Automatischer Abschluss wird ausgefuehrt.</summary>
    Completing,

    /// <summary>Pull Request wurde abgeschlossen oder gemergt.</summary>
    Completed,

    /// <summary>Pull Request wurde genehmigt, bleibt aber offen und wird weiter ueberwacht.</summary>
    Approved,

    /// <summary>Pull Request ist gemergt, Post-Merge-Workflow-Zuordnung ist aber noch unklar.</summary>
    PostMergeUncertain,

    /// <summary>Post-Merge-Workflows laufen oder wurden noch nicht abgeschlossen.</summary>
    PostMergeRunning,

    /// <summary>Post-Merge-Workflows wurden erfolgreich abgeschlossen.</summary>
    PostMergeSucceeded,

    /// <summary>Mindestens ein Post-Merge-Workflow ist fehlgeschlagen.</summary>
    PostMergeFailed,

    /// <summary>Ueberwachung oder Abschluss ist durch Rechte, Regeln oder Voraussetzungen blockiert.</summary>
    Blocked,

    /// <summary>Ueberwachung oder Abschluss ist fehlgeschlagen.</summary>
    Failed
}
