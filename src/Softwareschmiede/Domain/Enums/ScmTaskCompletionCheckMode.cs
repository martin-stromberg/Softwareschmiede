namespace Softwareschmiede.Domain.Enums;

/// <summary>Prüfmodus der SCM-Abschlussüberwachung.</summary>
public enum ScmTaskCompletionCheckMode
{
    /// <summary>Nur Aufgaben mit SCM-Ereigniszuordnung werden auf ihre Referenzen geprüft (Default).</summary>
    EventDriven = 0,

    /// <summary>Alle noch nicht beendeten Aufgaben werden geprüft.</summary>
    AllWorkItems = 1
}
