namespace Softwareschmiede.Domain.Enums;

/// <summary>Kompatibilitätsergebnis eines IDE-Plugins zu einem Repository.</summary>
public enum IdePluginCompatibility
{
    /// <summary>Das IDE-Plugin ist explizit kompatibel (z.B. .sln gefunden) - höchste Priorität.</summary>
    Explicit,

    /// <summary>Das IDE-Plugin wird als Rückfall verwendet, wenn kein Plugin explizit kompatibel ist.</summary>
    Fallback,

    /// <summary>Das IDE-Plugin ist nicht kompatibel und wird bei der Auswahl nicht berücksichtigt.</summary>
    Incompatible
}
