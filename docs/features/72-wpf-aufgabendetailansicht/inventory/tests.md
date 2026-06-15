# Tests

## Testklassen

### `AufgabeServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`

- `CreateAsync_ShouldCreateAufgabeWithStatusOffen_WhenCalledWithValidData()` — Testet Erstellung einer neuen Aufgabe mit Status `Neu`
- `CreateFromIssueAsync_ShouldCreateAufgabeWithIssueReferenz_WhenCalledWithValidIssue()` — Testet Erstellung aus Issue mit IssueReferenz
- `GetByProjektAsync_ShouldReturnAufgabenForProjekt_WhenAufgabenExist()` — Testet Abruf aller Aufgaben eines Projekts
- `StartenAsync_ShouldSetStatusArbeitsverzeichnisEingerichtetAndBranchName_WhenAufgabeExists()` — Testet Status-Übergang zu `ArbeitsverzeichnisEingerichtet` mit Branch und Klonpfad
- `GetLatestDiffResultIdForFileAsync_ShouldReturnNewestMatchingDiff_WhenPathUsesDifferentSeparators()` — Testet Abruf neuester Diff-ID für Datei mit unterschiedlicher Pfadnotation
- `GetLatestDiffResultIdForFileAsync_ShouldReturnNull_WhenNoDiffForFileExists()` — Testet null-Rückgabe bei fehlender Diff
- `StatusSetzenAsync_ShouldSetStatusInArbeit_WhenAufgabeExists()` — Testet generische Status-Setzung
- `AbschliessenAsync_ShouldSetStatusBeendetAndSetAbschlussDatum_WhenAufgabeExists()` — Testet Abschluss mit Status `Beendet` und AbschlussDatum
- `UpdateAsync_ShouldUpdateTitelAndAgentenInfos_WhenAufgabeExists()` — Testet Update von Titel, Beschreibung und KI-Plugin-Prefix
- `DeleteAsync_ShouldRemoveAufgabe_WhenAufgabeExists()` — Testet Löschen einer Aufgabe

### `AufgabeRecoveryServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/AufgabeRecoveryServiceTests.cs`

Tests für Recovery-Mechanismen bei Aufgaben.

### Integration Tests
Datei: `src/Softwareschmiede.IntegrationTests/Services/AufgabeServiceTests.cs`

Integration-Tests für den AufgabeService.

## Hilfsmethoden

### `TestDbContextFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs` (wird von AufgabeServiceTests verwendet)

- `Create()` — Erstellt einen In-Memory DbContext für Tests

## Bemerkungen

- **Keine ViewModel-Tests vorhanden** — Es gibt keine Unit-Tests für `TaskDetailViewModel` in den durchsuchten Test-Verzeichnissen
- **Keine E2E-Tests** — Es gibt keine E2E-Tests für die View-Interaktionen der TaskDetailView
- **Keine Tests für Commands** — Es gibt keine Tests für die Commands `SpeichernCommand`, `LoeschenCommand`, `StatusGestartetSetzenCommand`, `AufgabeAbschliessenCommand`
- **Service-Tests vorhanden** — Es gibt Unit-Tests für den `AufgabeService` mit CRUD-Operationen und Status-Transitionen
