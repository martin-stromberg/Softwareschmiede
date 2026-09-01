namespace Softwareschmiede.Domain.Enums;

/// <summary>Phase der lokalen Pull-Request-Ueberwachung.</summary>
public enum PullRequestMonitoringPhase
{
    /// <summary>Pull Request wurde erstellt und wartet auf die erste Statuspruefung.</summary>
    Created = 0,

    /// <summary>Pre-Merge-Workflows laufen oder wurden noch nicht abgeschlossen.</summary>
    PreMergeRunning = 1,

    /// <summary>Pre-Merge-Workflows wurden erfolgreich abgeschlossen.</summary>
    PreMergeSucceeded = 2,

    /// <summary>Automatischer Abschluss wird ausgefuehrt.</summary>
    Completing = 3,

    /// <summary>Pull Request wurde abgeschlossen oder gemergt.</summary>
    Completed = 4,

    /// <summary>Pull Request wurde genehmigt, bleibt aber offen und wird weiter ueberwacht.</summary>
    Approved = 5,

    /// <summary>Pull Request ist gemergt, Post-Merge-Workflow-Zuordnung ist aber noch unklar.</summary>
    PostMergeUncertain = 6,

    /// <summary>Post-Merge-Workflows laufen oder wurden noch nicht abgeschlossen.</summary>
    PostMergeRunning = 7,

    /// <summary>Post-Merge-Workflows wurden erfolgreich abgeschlossen.</summary>
    PostMergeSucceeded = 8,

    /// <summary>Mindestens ein Post-Merge-Workflow ist fehlgeschlagen.</summary>
    PostMergeFailed = 9,

    /// <summary>Ueberwachung oder Abschluss ist durch Rechte, Regeln oder Voraussetzungen blockiert.</summary>
    Blocked = 10,

    /// <summary>Ueberwachung oder Abschluss ist fehlgeschlagen.</summary>
    Failed = 11,

    /// <summary>Der Provider wird fuer diese Referenz nicht automatisch ueberwacht.</summary>
    NotMonitored = 12
}
