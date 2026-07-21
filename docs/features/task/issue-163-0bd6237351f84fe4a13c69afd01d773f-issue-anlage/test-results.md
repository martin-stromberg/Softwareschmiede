# Testergebnisse

Datum: 2026-07-21
Status: Keine Fehler

## Ausgefuehrt

- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~MainWindowViewModelTests.AktiveAufgabenAktualisierenAsync_ShouldSkip_WhenAlreadyRunning"`
  - Ergebnis: Bestanden, 1 erfolgreich, 0 fehlgeschlagen, 0 uebersprungen.
- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~MainWindowViewModelTests"`
  - Ergebnis: Bestanden, 31 erfolgreich, 0 fehlgeschlagen, 0 uebersprungen.

## Hinweise

- Beim ersten Lauf wurden bestehende Compiler-/Analyzer-Warnungen ausgegeben; sie betreffen nicht die geaenderte Teststelle.
- Der korrigierte Test ersetzt die bisherige 250-ms-Stoppuhr-Assertion durch eine direkte Re-Entrancy-Absicherung ueber eine kontrolliert blockierte und gezaehlte Test-Datenquelle.

## Fehlgeschlagene Tests

Keine.
