namespace Softwareschmiede.Domain.Enums;

/// <summary>Kontomodus der SCM-Ereignisüberwachung; wählt zugleich das zugehörige Credential.</summary>
/// <remarks>Der Modus gilt ausschließlich für die Ereignispfade (Beobachtung, Zustellung,
/// Ereignis-Antworten). Nicht-Ereignis-APIs — Repository-/Issue-/PR-Listen, PR-Status,
/// Workflow-Runs, Alerts, Templates sowie alle gh-/git-Schreib- und Arbeitskopien-Operationen —
/// laufen bewusst weiter unter dem Benutzerkonto (Token-Grenze).</remarks>
public enum ScmKontoModus
{
    /// <summary>Überwachung und Antworten laufen unter dem Benutzer-Token (bisheriges Verhalten).</summary>
    Benutzerkonto = 0,

    /// <summary>Überwachung und Antworten laufen unter dem Token eines separaten Kontos (Notifications-Inbox).</summary>
    SeparatesKonto = 1
}
