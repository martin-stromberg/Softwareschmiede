namespace Softwareschmiede.Domain.Enums;

/// <summary>Status eines einzelnen Testergebnisses.</summary>
public enum TestStatus
{
    /// <summary>Test wurde erfolgreich bestanden.</summary>
    Passed,

    /// <summary>Test ist fehlgeschlagen.</summary>
    Failed,

    /// <summary>Test wurde übersprungen.</summary>
    Skipped
}
