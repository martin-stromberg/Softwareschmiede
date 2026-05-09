using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Domain.Interfaces;

/// <summary>
/// Gemeinsame Basis aller Plugins. Definiert Name und konfigurierbare Einstellungsfelder.
/// Einstellungswerte werden vom <see cref="ICredentialStore"/> unter dem Schlüssel
/// <c>&lt;PluginPrefix&gt;.&lt;FieldKey&gt;</c> gespeichert.
/// </summary>
public interface IPlugin
{
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
}
