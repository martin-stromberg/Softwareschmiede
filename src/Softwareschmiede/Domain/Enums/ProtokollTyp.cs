namespace Softwareschmiede.Domain.Enums;

/// <summary>Typ eines Protokolleintrags.</summary>
public enum ProtokollTyp
{
    /// <summary>Prompt, der an den KI-Agenten gesendet wurde.</summary>
    Prompt,

    /// <summary>Antwort des KI-Agenten.</summary>
    KiAntwort,

    /// <summary>Statusübergang der Aufgabe.</summary>
    StatusUebergang,

    /// <summary>Ergebnis eines Testlaufs.</summary>
    TestErgebnis,

    /// <summary>Git-Aktion (Commit, Push, Branch, etc.).</summary>
    GitAktion,

    /// <summary>Ausgabezeile eines eingebetteten CLI-Prozesses.</summary>
    CliOutput,

    /// <summary>Erkannter Rate-Limit-Marker aus der CLI-Ausgabe.</summary>
    RateLimit
}
