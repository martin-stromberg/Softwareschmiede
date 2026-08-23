# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine fehlgeschlagenen Tests vorhanden.

## Zusammenfassung

- Gesamt: 1414
- Bestanden: 1413
- Fehlgeschlagen: 0
- Übersprungen: 1

## Testabdeckung

**Abdeckung:** Nicht messbar

(Coverage-Befehl gemäß CLAUDE.md-Vorgaben nicht ausgeführt; regulärer Test-Lane nur mit Abdeckungslücken-Analyse nach Dateinamen-Konvention)

## Fehlende Tests

Quelle: `Dateinamen-Konvention`

Die folgenden Produktionsdateien aus der Anforderung (Issue 228) weisen keine korrespondierende Testdatei auf:

- `src/Softwareschmiede/Application/Services/RepositoryScriptExecutor.cs` — Keine Testdatei gefunden
- `src/Softwareschmiede/Domain/Entities/RepositoryInitialisierungKonfiguration.cs` — Keine Testdatei gefunden (Entität, indirekt getestet)
- `src/Softwareschmiede/Domain/Entities/GitRepository.cs` — Keine Testdatei gefunden (Entität, indirekt getestet)
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs` — Keine Testdatei gefunden (DbContext, indirekt getestet)

### Dateien mit vorhandenen Tests

Die folgenden Dateien verfügen über entsprechende Testdateien:

- `src/Softwareschmiede/Application/Services/RepositoryInitialisierungService.cs` → `RepositoryInitialisierungServiceTests.cs` (vorhanden)
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` → `EntwicklungsprozessServiceTests.cs` und `EntwicklungsprozessServiceTests_Initialisierungsskript.cs` (vorhanden)
- `src/Softwareschmiede/Application/Services/ProjektService.cs` → `ProjektServiceTests.cs` (vorhanden)
- `src/Softwareschmiede/Application/Services/AufgabeService.cs` → `AufgabeServiceTests.cs` (vorhanden)
- `src/Softwareschmiede/App/ViewModels/ProjectDetailViewModel.cs` → `ProjectDetailViewModelTests.cs` und `ProjectDetailViewModelTests_Initialisierungsskript.cs` (vorhanden)

## Hinweise

- Der Test-Lauf wurde mit dem Filter `Category!=OsInterface` durchgeführt (14 ConPTY-Tests übersprungen).
- Der übersprungene Test (1 von 1414) ist eine OS-Interface-Kategorie-Test.
- Alle regelmäßigen Unit-Tests sind erfolgreich bestanden.
