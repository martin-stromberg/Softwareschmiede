# Testergebnisse

Datum: 2026-07-21
Status: Keine Fehler

## Ausgefuehrt

- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~CliKiPluginBaseTests|FullyQualifiedName~IssueCreateDialogViewModelTests" -p:BaseOutputPath=$out`
  - Ergebnis: Bestanden, 30 erfolgreich, 0 fehlgeschlagen, 0 uebersprungen.
  - Hinweis: Separater `BaseOutputPath` wurde verwendet, weil eine laufende `Softwareschmiede.App`-Instanz die normalen Debug-DLLs sperrte.
- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-restore --filter "Category!=OsInterface" -p:BaseOutputPath=.tmp-test-bin/<guid>`
  - Ergebnis: Bestanden, 1041 erfolgreich, 0 fehlgeschlagen, 1 uebersprungen.
  - Hinweis: Der separate `BaseOutputPath` lag innerhalb des Repositories, damit bestehende Tests, die `Softwareschmiede.slnx` relativ zum Test-Binaerpfad suchen, funktionieren.

## Nicht erfolgreich abgeschlossen

- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~CliKiPluginBaseTests|FullyQualifiedName~IssueCreateDialogViewModelTests"`
  - Ergebnis: Build abgebrochen durch gesperrte Dateien im normalen `bin`-Ordner. Sperrende Prozesse: `Microsoft Visual Studio (4920)`, `Softwareschmiede.App (17400)`.
  - Bewertung: Infrastruktur-/Dateisperre, kein fehlgeschlagener Test. Der gleiche fokussierte Lauf war mit separatem Ausgabepfad erfolgreich.
- `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-restore --filter "Category!=OsInterface" -p:BaseOutputPath=$env:TEMP/<guid>`
  - Ergebnis: 1 bestehender Test (`TaskDetailViewTests.Xaml_ContainsPullRequestActionButton`) konnte `Softwareschmiede.slnx` nicht finden, weil der Test-Binaerpfad ausserhalb des Repositories lag.
  - Bewertung: Ausgabepfad-Artefakt, kein Produktfehler. Der gleiche breite Lauf war mit separatem Ausgabepfad innerhalb des Repositories erfolgreich.

## Fehlgeschlagene Tests

Keine.
