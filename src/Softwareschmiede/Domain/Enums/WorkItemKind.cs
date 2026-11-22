namespace Softwareschmiede.Domain.Enums;

/// <summary>Fachliche Auftragsart einer Aufgabe — benennt ihren Ursprung (Issue #451). Dient
/// der sichtbaren Typ-Markierung und der Gruppierung in den Aufgabenlisten. Vom dedizierten
/// <see cref="Entities.WorkItem.Purpose"/> getrennt: <c>Purpose</c> steuert Routing und
/// Isolation der Issue-Aufbereitung, <see cref="WorkItemKind"/> ist die Anzeige-Klassifikation
/// und darf sich im Lebenszyklus ändern (Aufbereitung wird nach Zustellung zur Anweisung).</summary>
public enum WorkItemKind
{
    /// <summary>Nicht bestimmt — Bestandsdaten ohne klassifizierbaren Ursprung.</summary>
    Unspecified = 0,

    /// <summary>Manuell angelegte Aufgabe (einschließlich manueller Anlage aus einem Issue).</summary>
    Manual = 1,

    /// <summary>Aus einem SCM-Trigger-Ereignis (Issue-/Kommentar-Trigger) angelegte
    /// Anweisungsaufgabe — auch der Zieltyp der Issue-Aufbereitung nach erfolgreicher
    /// Prompt-Zustellung.</summary>
    Instruction = 2,

    /// <summary>Aus einem Pull-Request-Ereignis angelegte Aufgabe.</summary>
    PullRequest = 3,

    /// <summary>Aus einem SCM-Alert angelegte Aufgabe.</summary>
    Alert = 4,

    /// <summary>Automatische Issue-Aufbereitung (Analyse-Prompt). Wechselt nach erfolgreicher
    /// Zustellung der Analyse zu <see cref="Instruction"/>.</summary>
    IssuePreparation = 5
}
