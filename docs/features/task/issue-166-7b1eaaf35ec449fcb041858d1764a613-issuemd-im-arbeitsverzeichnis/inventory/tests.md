# Tests

## Testklassen

### `EntwicklungsprozessServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`

#### Testmethoden
- `ProzessStartenAsync_ShouldCloneAndCreateBranch_WhenAufgabeExists` — Prüft, dass Clone und Branch-Erstellung funktionieren.
- `ProzessStartenUndCliStartenAsync_ShouldStartCliAndSetStatusGestartet_WhenStartSucceeds` — Prüft die kombinierte Klone- und CLI-Start-Funktion.
- `ProzessStartenUndCliStartenAsync_ShouldRollbackStatus_WhenRepositoryCloneFails` — Prüft Rollback bei Clone-Fehler.
- `ProzessStartenUndCliStartenAsync_ShouldRollbackStatus_WhenCliStartFails` — Prüft Rollback bei CLI-Start-Fehler.
- `ProzessStartenUndCliStartenAsync_ShouldUseConfiguredWorkingDirectory_WhenStartConfigHasWorkingDirectory` (**[RELEVANT]**) — Prüft, dass der KI-Plugin das konfigurierte Arbeitsverzeichnis erhält (Zeile 205–273). Überprüft, dass `usedPath` korrekt auf `Path.Combine(updatedAufgabe.LokalerKlonPfad!, "backend")` gesetzt wird.
- `ProzessStartenUndCliStartenAsync_ShouldRollback_WhenConfiguredWorkingDirectoryMissing` (**[RELEVANT]**) — Prüft Rollback, wenn das konfigurierte Arbeitsverzeichnis fehlt (Zeile 280–330). Erwartet `DirectoryNotFoundException`.
- `ProzessStartenAsync_ShouldContinue_WhenRepositoryStartScriptFails` — Prüft, dass das Startskript-Fehler nicht den Prozess blockiert.
- `ProzessStartenAsync_ShouldCreateIssueBranch_WhenAufgabeHasIssueReference` — Prüft Branch-Benennung mit Issue-Referenz.
- `ProzessStartenUndCliStartenAsync_ShouldStartTaskWithoutIssueReference_WhenSingleProjectRepositoryExists` — Prüft Start ohne Issue.
- `ProzessStartenAsync_ShouldThrowInvalidOperationException_WhenAufgabeDoesNotExist` — Prüft Exception bei fehlender Aufgabe.
- `AbschliessenAsync_ShouldSetStatusAbgeschlossenAndAddProtokoll_WhenAufgabeExists` — Prüft Abschließen der Aufgabe.

#### Hilfsmethoden
- `SetupCloneWithDirectoryCreation(gitignoreContent?)` — Richtet Mock-Clone mit optionalem `.gitignore`-Inhalt ein (Zeile 84–96).
- `SetupCloneMocks()` — Richtet grundlegende Clone- und Branch-Mocks auf (Zeile 102–108).
- `DeleteDirectoryIfExists(path)` — Hilfsfunktion zum Löschen von Testverzeichnissen (Zeile 110–116).
- `CreatePluginSelectionService(params IKiPlugin[])` — Erstellt Mock-PluginSelectionService (Zeile 72–82).

---

### `EntwicklungsprozessServiceTests_WorkingDirectoryValidation`
Datei: `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests_WorkingDirectoryValidation.cs`

#### Testmethoden
- `ProzessStartenAsync_ShouldThrowDirectoryNotFoundImmediatelyAfterClone_WhenConfiguredWorkingDirectoryMissing` (**[RELEVANT]**) — Prüft, dass `DirectoryNotFoundException` direkt nach dem Klon geworfen wird, wenn das Arbeitsverzeichnis fehlt; Branch-Erstellung soll nicht stattfinden (Zeile 145–166).
- `ProzessStartenAsync_ShouldContinue_WhenConfiguredWorkingDirectoryExists` (**[RELEVANT]**) — Prüft normalen Prozessablauf, wenn das konfigurierte Arbeitsverzeichnis existiert (Zeile 173–199).
- `ProzessStartenAsync_ShouldNotValidate_WhenGitOrchestrationServiceNotConfigured` (**[RELEVANT]**) — Prüft Rückwärtskompatibilität: Wenn `GitOrchestrationService` nicht konfiguriert, keine Validierung (Zeile 207–239).

#### Hilfsmethoden
- `SetupCloneWithDirectoryCreation()` — Richtet Mock-Clone ein (Zeile 94–106).
- `DeleteDirectoryIfExists(path)` — Hilfsfunktion zum Löschen von Testverzeichnissen (Zeile 108–114).
- `CreateRepositoryWithWorkingDirectoryAsync(workingDirectoryRelativePath)` — Erstellt Test-Repository mit optionalem Arbeitsverzeichnis (Zeile 116–137).
- `CreatePassthroughPluginManagerMock()` — Erstellt Mock-PluginManager für Rückwärtskompatibilitätsprüfung (Zeile 241–249).

---

## Bestehende Test-Abdeckung für `issue.md` / `.gitignore`

**Keine direkten Tests für `CreateIssueFileAsync` oder `UpdateGitignoreAsync` vorhanden.**

Die Testklassen beschäftigen sich primär mit:
- Repository-Klon und Branch-Setup
- CLI-Start und Arbeitsverzeichnis-Übergabe an KI-Plugin
- Arbeitsverzeichnis-Validierung nach Klon
- Fehlerbehandlung und Rollback

Die `issue.md`-Erstellung und `.gitignore`-Aktualisierung werden implizit in `FinalizeStartAsync` getestet, aber es gibt keine dedizierten Test-Cases für:
- `issue.md`-Pfad mit/ohne Arbeitsverzeichnis
- `.gitignore`-Pfad mit/ohne Arbeitsverzeichnis
- `issue.md` und `.gitignore`-Verzeichnis-Erstellung, wenn das Arbeitsverzeichnis nicht existiert

---

## Setup und Abhängigkeiten

- **Test-DB:** `TestDbContextFactory.Create()` für Datenbank-Operationen.
- **Mocks:**
  - `IGitPlugin` — für Git-Operationen (Clone, Branch, etc.)
  - `IKiPlugin` — für KI-Automation
  - `IArbeitsverzeichnisResolver` — für Pfad-Auflösung
  - `IPluginManager` — für Plugin-Selektion
- **Real Services:**
  - `AufgabeService`, `ProtokollService`, `ProjektService`
  - `EntwicklungsprozessService`, `GitOrchestrationService`
  - `PluginSelectionService`, `PluginDefaultSettingsService`
  - `KiAusfuehrungsService` (Real für E2E-Tests mit Pseudo-Console)
