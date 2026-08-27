# Tests

## Testklassen

### `AutonomAufgabenInitialisierungsServiceTests`
Datei: `src\Softwareschmiede.Tests\Application\Services\AutonomAufgabenInitialisierungsServiceTests.cs`

Unit-Tests für `AutonomAufgabenInitialisierungsService`.

**Bekannte Testmethoden:**
- `InitialisiereAsync_ErzeugtArbeitsverzeichnis()` – Prüft, dass die vollständige Arbeitsverzeichnisstruktur mit `plan.md`, `progress.md`, `governance.md`, `skills/`, `clones/`, `tasks/`, `logs/` erstellt wird
- `InitialisiereAsync_ErzeugtRepositoryKlon()` – Prüft, dass der Repository-Klon im `clones/repo_main/`-Verzeichnis erstellt wird
- `InitialisiereAsync_KlontDirectVonGitRepository()` – Prüft, dass direkt von `aufgabe.GitRepository.RepositoryUrl` geklont wird, nicht von `aufgabe.LokalerKlonPfad`
- (weitere Tests für Edge Cases wie Retry, idempotente Klone, etc.)

**Setup:**
- Nutzt `TestDbContextFactory.Create()` für In-Memory DB
- Nutzt `AutonomAufgabenInitialisierungsServiceTestFactory` für Mock-Setup:
  - `CreateCliRunnerMockMitErfolgreicherGitAusfuehrung()`
  - `CreateGitPluginMockMitErfolgreichemKlon()`
  - `CreateService(...)`
  - `ErstelleProjekt(_db)`
  - `ErstelleAufgabeMitLokalemKlon(_db, projektId, pfad, titel)`

---

### `EntwicklungsprozessServiceTests`
Datei: 
- `src\Softwareschmiede.Tests\Application\Services\EntwicklungsprozessServiceTests.cs` (Unit-Tests)
- `src\Softwareschmiede.IntegrationTests\Services\EntwicklungsprozessServiceTests.cs` (Integration-Tests)

Tests für `EntwicklungsprozessService` (Repository-Setup für nicht-autonome Aufgaben).

**Bekannte Test-Bereiche:**
- Repository-Setup und Klon-Verzeichnis-Vorbereitung
- Branch-Anlage und Checkout
- Status-Verwaltung (`AufgabeStatus.Gestartet`)
- Fehlerbehandlung und Rollback bei Fehlern

---

## E2E-Tests

### `E2E_AutonomAufgabenInitialisierung`
Datei: `src\Softwareschmiede.Tests\E2E\E2E_AutonomAufgabenInitialisierung.cs`

End-to-End-Tests für die autonome Aufgaben-Initialisierung (nutzt FlaUI für UI-Automation).

**Bekannte Test-Szenarien:**
- Dialog-Öffnung für autonome Aufgaben
- Branch-Eingabe und -Validierung
- Token-Budget und Runtime-Limit-Eingabe
- Persistenz-Modus-Auswahl
- Erfolgreiche Initialisierung und Detail-Ansicht-Anzeige

---

### `E2E_AutonomAufgabenAgentExecution`
Datei: `src\Softwareschmiede.Tests\E2E\E2E_AutonomAufgabenAgentExecution.cs`

End-to-End-Tests für die Ausführung des Projektleiter-Agenten (mit echtem CLI-Prozess).

**Bekannte Test-Szenarien:**
- Agent-Start und PseudoConsole-Initialisierung
- Prompt-Versand und CLI-Reaktion
- Fehlerbehandlung und Prozess-Stopp
- Session-Pause und Resume nach App-Neustart

---

## Hilfsmethoden

### `AutonomAufgabenInitialisierungsServiceTestFactory`
Datei: (Verzeichnis indiziert, genaue Datei nicht vollständig gelesen)

Hilfsfactory für Test-Setup des `AutonomAufgabenInitialisierungsService`.

**Hilfsmethoden:**
- `CreateCliRunnerMockMitErfolgreicherGitAusfuehrung()` – Mock für `ICliRunner` mit erfolgreicher Git-Ausführung
- `CreateGitPluginMockMitErfolgreichemKlon()` – Mock für `IGitPlugin` mit erfolgreichem Klon
- `CreateService(DbContext, ICliRunner, IGitPlugin)` – Erzeugt `AutonomAufgabenInitialisierungsService` mit Mocks
- `ErstelleProjekt(DbContext)` – Erstellt Test-Projekt; gibt `Guid` zurück
- `ErstelleAufgabeMitLokalemKlon(DbContext, projektId, pfad, titel)` – Erstellt Test-Aufgabe mit Git-Repository

### `TestDbContextFactory`
Datei: (indiziert, genaue Datei nicht vollständig gelesen)

Hilfsfactory für Test-DbContext-Setup.

**Hilfsmethoden:**
- `Create()` – Erzeugt In-Memory `SoftwareschmiededDbContext` für Tests

---

## Testabdeckung für Feature-Flag (geplant, nicht vorhanden)

**Fehlende Tests für das geplante Feature-Flag-Gating:**

### Unit-Tests (zu ergänzen)
1. `AutonomAufgabenInitialisierungsServiceTests`:
   - `WhenEnabledFlagIsFalse_InitialisiereAsync_ShouldThrowOrReturn()` – Prüft Guard-Klausel
   - `WhenEnabledFlagIsFalse_ShouldNotStartAgent()` – Prüft, dass Agent nicht startet

2. `ProjektleiterAgentServiceTests` (wenn Testklasse existiert):
   - `WhenEnabledFlagIsFalse_StarteAgentAsync_ShouldThrowOrFallback()` – Prüft Fallback-Verhalten

3. `EntwicklungsprozessServiceTests`:
   - `WhenFeatureFlagDisabled_ShouldUseFallbackPath()` – Prüft, dass einfacher Weg weiterhin funktioniert

### Integration-Tests (zu ergänzen)
1. `EntwicklungsprozessServiceTests`:
   - `ProzessStartenAsync_ShouldSkipAutonomInitialization_WhenFeatureFlagDisabled()`
   - `ProzessStartenAsync_ShouldExecuteSimpleCliStart_WhenFeatureFlagDisabled()`

### E2E-Tests (zu ergänzen)
1. `E2E_AutonomAufgabenInitialisierung`:
   - `WhenAutonomAufgabenDisabled_UIElementsShouldNotBeDisplayed()` – Prüft, dass Dialog nicht gezeigt wird
   - `WhenAutonomAufgabenDisabled_SimpleStartButtonShouldBeAvailable()` – Prüft Fallback-UI

2. `E2E_AutonomAufgabenAgentExecution`:
   - `WhenAutonomAufgabenDisabled_AgentShouldNotStart()` – Prüft, dass Agent nicht startet
   - `WhenAutonomAufgabenDisabled_SimpleCliShouldStart()` – Prüft, dass einfacher Weg funktioniert
