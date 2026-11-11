namespace Softwareschmiede.Domain.Enums;

/// <summary>Prüfmodus der SCM-Abschlussüberwachung.</summary>
public enum ScmTaskCompletionPruefmodus
{
    /// <summary>Nur Aufgaben mit SCM-Ereigniszuordnung werden auf ihre Referenzen geprüft (Default).</summary>
    Ereignisgesteuert = 0,

    /// <summary>Alle noch nicht beendeten Aufgaben werden geprüft.</summary>
    AlleAufgaben = 1
}
