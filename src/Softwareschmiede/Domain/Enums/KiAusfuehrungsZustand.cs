namespace Softwareschmiede.Domain.Enums;

/// <summary>Fachliche Zustandsklassifikation der KI-Ausführung (gemeinsame Basis der Status-Converter und der Lauf-Zählung).</summary>
public enum KiAusfuehrungsZustand
{
    /// <summary>Aufgabe ist pausiert (PausiertBisUtc in der Zukunft).</summary>
    Pausiert,

    /// <summary>Ein zeitgesteuerter Prompt steht in der Warteschlange.</summary>
    PromptInWartestellung,

    /// <summary>CLI-Ausführung läuft (aktiver Run ohne WartetAufEingabe-Substatus).</summary>
    Laeuft,

    /// <summary>Ausführung wartet (Substatus WartetAufEingabe oder Aufgabenstatus Wartend).</summary>
    Wartet,

    /// <summary>Keine aktive Ausführung erkennbar.</summary>
    Bereit
}
