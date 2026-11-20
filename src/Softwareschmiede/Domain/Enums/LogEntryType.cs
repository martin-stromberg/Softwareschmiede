namespace Softwareschmiede.Domain.Enums;

/// <summary>Typ eines Protokolleintrags.</summary>
public enum LogEntryType
{
    /// <summary>Prompt, der an den KI-Agenten gesendet wurde.</summary>
    Prompt,

    /// <summary>Antwort des KI-Agenten.</summary>
    AiResponse,

    /// <summary>Statusübergang der Aufgabe.</summary>
    StatusTransition,

    /// <summary>Ergebnis eines Testlaufs.</summary>
    TestResult,

    /// <summary>Git-Aktion (Commit, Push, Branch, etc.).</summary>
    GitAction,

    /// <summary>Ausgabezeile eines eingebetteten CLI-Prozesses.</summary>
    CliOutput,

    /// <summary>Erkannter Rate-Limit-Marker aus der CLI-Ausgabe.</summary>
    RateLimit,

    /// <summary>Interne Systemmeldung (z. B. Prozessende mit Fehlercode).</summary>
    SystemMessage
}
