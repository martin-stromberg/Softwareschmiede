namespace Softwareschmiede.Domain.Enums;

/// <summary>Fachliche Zustandsklassifikation der KI-Ausführung (gemeinsame Basis der Status-Converter und der Lauf-Zählung).</summary>
public enum AiExecutionState
{
    /// <summary>Aufgabe ist pausiert (PausiertBisUtc in der Zukunft).</summary>
    Paused,

    /// <summary>Ein zeitgesteuerter Prompt steht in der Warteschlange.</summary>
    PromptQueued,

    /// <summary>CLI-Ausführung läuft (aktiver Run ohne WartetAufEingabe-Substatus).</summary>
    Running,

    /// <summary>Ausführung wartet (Substatus WartetAufEingabe oder Aufgabenstatus Wartend).</summary>
    Waiting,

    /// <summary>Keine aktive Ausführung erkennbar.</summary>
    Ready
}
