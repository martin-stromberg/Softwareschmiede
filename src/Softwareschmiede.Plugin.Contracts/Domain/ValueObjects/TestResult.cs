using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Domain.ValueObjects;

/// <summary>Gesamtergebnis eines Testlaufs.</summary>
/// <param name="Bestanden">Gibt an, ob alle Tests bestanden wurden.</param>
/// <param name="Ergebnisse">Liste der einzelnen Testergebnisse.</param>
public sealed record TestResult(
    bool Bestanden,
    IReadOnlyList<TestErgebnisInfo> Ergebnisse
);

/// <summary>Ergebnis eines einzelnen Tests.</summary>
/// <param name="TestName">Name des Tests.</param>
/// <param name="Status">Status des Tests.</param>
/// <param name="Fehlermeldung">Optionale Fehlermeldung bei fehlgeschlagenem Test.</param>
/// <param name="Dauer">Dauer des Tests.</param>
public sealed record TestErgebnisInfo(
    string TestName,
    TestStatus Status,
    string? Fehlermeldung,
    TimeSpan Dauer
);
