namespace Softwareschmiede.Domain.Enums;

/// <summary>Status eines einzelnen Testergebnisses.</summary>
public enum TestStatus
{
    /// <summary>Test wurde erfolgreich bestanden.</summary>
    Bestanden,

    /// <summary>Test ist fehlgeschlagen.</summary>
    Fehlgeschlagen,

    /// <summary>Test wurde übersprungen.</summary>
    Uebersprungen
}
