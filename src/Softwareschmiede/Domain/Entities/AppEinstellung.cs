namespace Softwareschmiede.Domain.Entities;

/// <summary>Globale, nicht-geheime Anwendungseinstellung.</summary>
public sealed class AppEinstellung
{
    /// <summary>Eindeutige ID der Einstellung.</summary>
    public Guid Id { get; set; }

    /// <summary>Maschinenlesbarer Schlüssel, z.B. repositories.workdir.</summary>
    public string Schluessel { get; set; } = string.Empty;

    /// <summary>Optionaler Wert; null/leer bedeutet Default verwenden.</summary>
    public string? Wert { get; set; }

    /// <summary>Zeitpunkt der letzten Aktualisierung.</summary>
    public DateTimeOffset AktualisiertAm { get; set; }
}
