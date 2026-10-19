using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>
/// Gemeinsame Basis aller Plugins. Definiert Name und konfigurierbare Einstellungsfelder.
/// Einstellungswerte werden vom <see cref="ICredentialStore"/> unter dem Schlüssel
/// <c>&lt;PluginPrefix&gt;.&lt;FieldKey&gt;</c> gespeichert.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Fallback-Glyph für Plugins ohne eigenes <see cref="PluginSymbol"/> und für
    /// nicht auflösbare Plugin-Referenzen (z. B. in der Aufgabenliste).
    /// </summary>
    const string FallbackSymbol = "◻";

    /// <summary>Eindeutiger Anzeigename des Plugins (z.B. "GitHub", "GitHub Copilot").</summary>
    string PluginName { get; }

    /// <summary>
    /// Präfix für alle Credential-Store-Schlüssel dieses Plugins (z.B. "Softwareschmiede.GitHub").
    /// Einzelne Felder werden als <c>&lt;PluginPrefix&gt;.&lt;FieldKey&gt;</c> gespeichert.
    /// </summary>
    string PluginPrefix { get; }

    /// <summary>
    /// Gibt die Einstellungsgruppen mit ihren Feldern zurück.
    /// Die Reihenfolge der Gruppen und Felder bestimmt die Anzeigereihenfolge in der UI.
    /// </summary>
    IReadOnlyList<PluginSettingGroup> GetSettingGroups();

    /// <summary>Plugin-Typ zur automatischen Zuordnung im PluginManager.</summary>
    PluginType PluginType { get; }

    /// <summary>
    /// Unicode-Glyph zur Anzeige des Plugins in der Aufgabenliste (z. B. "🐙").
    /// Als Default-Interface-Member binärkompatibel — bereits kompilierte Plugin-DLLs
    /// ohne eigenes Symbol laden weiter und erhalten den Fallback-Glyph <see cref="FallbackSymbol"/>.
    /// </summary>
    string PluginSymbol => FallbackSymbol;
}
