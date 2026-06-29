namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Datentyp eines Plugin-Einstellungsfeldes.</summary>
public enum PluginSettingFieldType
{
    /// <summary>Einzeiliger Text.</summary>
    Text,

    /// <summary>Maskiertes Passwort- oder Token-Feld.</summary>
    Secret,

    /// <summary>URL-Eingabe.</summary>
    Url,

    /// <summary>Ganzzahl.</summary>
    Integer,

    /// <summary>Wahrheitswert (Checkbox).</summary>
    Boolean,

    /// <summary>Auswahl über feste Werte (Select).</summary>
    Enum,

    /// <summary>Dateipfad mit Datei-Auswahl-Dialog.</summary>
    FilePath,

    // Wert 7 ist reserviert / nicht vergeben.

    /// <summary>Kommandozeilenparameter für CLI-Aufrufe.</summary>
    CommandLineParameters = 8
}
