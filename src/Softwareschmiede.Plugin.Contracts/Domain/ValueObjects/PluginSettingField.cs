namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>
/// Beschreibt ein einzelnes konfigurierbares Einstellungsfeld eines Plugins.
/// Der Schlüssel wird unter <c>&lt;PluginPrefix&gt;.&lt;Key&gt;</c> im Credential Store gespeichert.
/// </summary>
/// <param name="Key">Eindeutiger Schlüssel innerhalb des Plugins (wird mit Plugin-Prefix kombiniert).</param>
/// <param name="Label">Anzeigename des Feldes.</param>
/// <param name="FieldType">Datentyp und Darstellung des Feldes.</param>
/// <param name="Placeholder">Beispieltext für das Eingabefeld (z.B. "ghp_...").</param>
/// <param name="Description">Optionale Beschreibung / Hinweistext unterhalb des Feldes.</param>
/// <param name="IsRequired">Gibt an ob das Feld Pflicht ist.</param>
/// <param name="EnumOptions">Zulässige Optionen für Enum-Felder.</param>
public sealed record PluginSettingField(
    string Key,
    string Label,
    PluginSettingFieldType FieldType = PluginSettingFieldType.Text,
    string? Placeholder = null,
    string? Description = null,
    bool IsRequired = false,
    IReadOnlyList<string>? EnumOptions = null);
