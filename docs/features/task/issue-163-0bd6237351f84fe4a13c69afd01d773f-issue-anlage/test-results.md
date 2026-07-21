# Testergebnisse

Datum: 2026-07-21
Status: Keine Fehler

## Ausgefuehrt

- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~IssueCreateDialogViewModelTests|FullyQualifiedName~CodexPluginTests|FullyQualifiedName~ClaudeCliPluginTests"`
  - Ergebnis: Bestanden, 38 erfolgreich, 0 fehlgeschlagen.
- `dotnet build Softwareschmiede.slnx --no-restore`
  - Ergebnis: Erfolgreich, 0 Fehler, 4 bestehende Warnungen in `Softwareschmiede.IntegrationTests`.
- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-restore --filter "Category!=OsInterface"`
  - Ergebnis: Bestanden, 1039 erfolgreich, 1 uebersprungen, 0 fehlgeschlagen.

## Nicht erfolgreich abgeschlossen

- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-restore`
  - Ergebnis: Tool-Timeout nach 184 Sekunden. Der Lauf enthielt OS-Interface-/E2E-Tests und lieferte vor dem Timeout kein abschliessendes Testergebnis. Danach wurden haengende Testprozesse beendet und der Build-Server sauber heruntergefahren.

## Fehlgeschlagene Tests

Keine.
