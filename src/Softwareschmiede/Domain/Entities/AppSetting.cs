namespace Softwareschmiede.Domain.Entities;

/// <summary>Globale, nicht-geheime Anwendungseinstellung.</summary>
public sealed class AppSetting
{
    /// <summary>Eindeutige ID der Einstellung.</summary>
    public Guid Id { get; set; }

    /// <summary>Maschinenlesbarer Schlüssel, z.B. repositories.workdir.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Optionaler Wert; null/leer bedeutet Default verwenden.</summary>
    public string? Value { get; set; }

    /// <summary>Zeitpunkt der letzten Aktualisierung.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
