# Bestandsaufnahme: Test-Infrastruktur

## WpfTestBase (E2E-Test-Basisklasse)

**Datei:** `src\Softwareschmiede.Tests\E2E\WpfTestBase.cs`

Abstrakte Basisklasse für WPF End-to-End-Tests mit FlaUI-Automation.

### Wichtige Eigenschaften

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `TestDbPath` | `string` | Pfad zur SQLite-Testdatenbank des laufenden App-Prozesses (`softwareschmiede_e2e_<GUID>.db` im Temp-Verzeichnis) |
| `Automation` | `UIA3Automation` | FlaUI-Automatisierungskontext für UI-Element-Suche |
| `FlaUiApp` | `FlaUI.Core.Application` | Handle zur gestarteten WPF-Anwendung |
| `Short` | `TimeSpan` | Timeout für schnell erscheinende UI-Elemente (10s) |
| `Medium` | `TimeSpan` | Timeout für UI-Elemente nach asynchronen Operationen (15s) |
| `Long` | `TimeSpan` | Timeout für initiales Erscheinen des Hauptfensters (30s) |

### Testunterstützungs-Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LaunchApp(ensureDatabaseDeleted)` | `protected` | Startet die Anwendung als Prozess mit Wartenb auf MainWindow-Sichtbarkeit; wartet zusätzlich 2000ms für WPF-Rendering und EF-Migrationen |
| `DeleteTestDatabase()` | `protected` | Löscht die temporäre Testdatenbank |
| `OpenTestDbContext()` | `protected` | Öffnet einen DbContext gegen die Testdatenbank für Testvorbedingungen über SQL |
| `WaitForElement(parent, conditionFunc, timeout)` | `protected static` | Wartet auf UI-Element mit Fail-Fast-Diagnose bei Fehlerbanner "FehlerMeldung"; bricht ab, wenn Fehlerbanner sichtbar wird |
| `WaitForWindow(title, timeout)` | `protected` | Wartet auf Top-Level-Fenster mit angegebenem Titel |
| `WaitUntilGone(parent, conditionFunc, timeout)` | `protected static` | Wartet bis ein UI-Element verschwunden ist; behauptet dann dass Element nicht mehr vorhanden ist |
| `WaitForSelectedComboBoxItem(comboBoxElement, expectedItemText, timeout)` | `protected static` | Wartet bis ComboBox einen bestimmten Eintrag anzeigt |
| `CreateLocalSourceDirectory(repositoryFolderName)` | `protected` | Erstellt temporäres lokales Quellverzeichnis mit Unterordner für LocalDirectoryPlugin-Tests |
| `CreateProject(mainWindow, name)` | `protected` | Legt über UI ein neues Projekt an und speichert es |
| `OpenProject(mainWindow, name)` | `protected` | Öffnet ein Projekt aus der Liste |
| `CreateAndOpenProject(mainWindow, name)` | `protected` | Legt Projekt an und öffnet es sofort |
| `NavigateToProjecten(mainWindow)` | `protected` | Klickt auf "Projekte"-Button |
| `SelectComboBoxItemByClick(comboBoxElement, itemText, timeout)` | `protected static` | Wählt ComboBox-Eintrag durch Klick (robuster als FlaUI's Select) |
| `ConfigureLocalDirectoryPlugin(mainWindow, sourceDirectory, useInSourceDirectoryMode)` | `protected` | Konfiguriert LocalDirectoryPlugin mit Quellverzeichnis über Einstellungs-UI |
| `AssignLocalDirectoryRepository(mainWindow)` | `protected` | Öffnet Repository-Zuweisungs-Dialog und wählt erstes Repository |
| `SetupProjectMitNeuerAufgabe(repositoryFolderName, projektName, useInSourceDirectoryMode)` | `protected` | Komplettes Setup: LaunchApp -> LocalDirectoryPlugin konfigurieren -> Projekt anlegen -> Repository zuweisen -> neue Aufgabe erstellen |
| `StartenUndPluginWaehlen(mainWindow, pluginName)` | `protected` | Klickt "Starten" und bedient Plugin-Auswahl-Dialog |
| `ConfirmLocalDirectoryGitInitInSourceDirectory()` | `protected static` | Setzt Windows-Credential "LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory" auf "true" |
| `SetLocalDirectoryWorkspaceMode(workspaceMode)` | `protected static` | Setzt Windows-Credential "LocalDirectoryPlugin.WorkspaceMode" |
| `Dispose()` | `public` | Beendet Anwendung, gibt Automation frei, löscht Testdatenbank und räumt Windows-Credentials auf |

### Fehlerbehandlung

- Bei `WaitForElement` wird zusätzlich auf einen Fehlerbanner mit dem Namen "FehlerMeldung" geprüft: Wenn dieser sichtbar wird, wirft die Methode `InvalidOperationException` mit dem Text des Fehlermeldungs-Elements statt auf das Timeout zu warten.
- Zusätzlich versucht die Methode beim Fehlerbanner einen letzten Versuch, die Zielsuche durchzuführen, um False-Positive zu vermeiden (falls conditionFunc selbst auf "FehlerMeldung" zielt).
- `Dispose()` fängt alle Exceptions bei Clean-up-Operationen und schreibt sie in Debug-Output statt zu werfen.

### Ressourcen-Cleanup

- `Dispose()` ruft auf:
  - `_application?.Close()` — Schließt die Anwendung
  - `_application?.WaitWhileMainHandleIsMissing(5s)` — Wartet auf Fenster-Schließung
  - `_automation?.Dispose()` — Gibt Automation-Kontext frei
  - `DeleteTestDatabase()` — Löscht Testdatenbank
  - `Environment.SetEnvironmentVariable("SOFTWARESCHMIEDE_TEST_DB_PATH", null)` — Setzt Umgebungsvariable zurück
  - `DeleteLocalDirectoryPluginCredentials()` — Löscht Windows-Credentials: "LocalDirectoryPlugin.WorkspaceMode", "LocalDirectoryPlugin.SourceDirectory", "LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory", "Softwareschmiede.Codex.ExecutablePath"

## Testklassen

### `ProjectDetailE2ETests` 
**Datei:** `src\Softwareschmiede.Tests\E2E\ProjectDetailE2ETests.cs`

Erbt von `WpfTestBase`, markiert mit `[Trait("Category", "E2E")]` und `[Collection("E2E")]` (verhindert parallele Ausführung).

| Testmethode | Szenario |
|-------------|----------|
| `NeuanlageAbbrechen_ErstesProjektNochAufrufbar_E2E()` | Projekt anlegen → Neuanlage starten → über Zurück abbrechen → erstes Projekt sollte noch in Liste aufrufbar sein |
| `ProjektOeffnenUndZurueck_ErneutOeffnen_E2E()` | Projekt anlegen → öffnen → via Zurück schließen → erneut öffnen sollte funktionieren |
| `ProjektNamenAendern_KachelAktualisiert_UndErneutoeffnen_E2E()` | Projekt umbenennen, zurücknavigieren, erneut öffnen — Kachel sollte neuen Namen zeigen |

### `WpfE2EPlaceholderTests`
**Datei:** `src\Softwareschmiede.Tests\E2E\WpfE2EPlaceholderTests.cs`

Erbt von `WpfTestBase`, markiert mit `[Trait("Category", "E2E")]` und `[Collection("E2E")]`.

| Testmethode | Szenario |
|-------------|----------|
| `ProjektErstellen_ZeigtAufgabenListe_E2E()` | Nach Projekterstellung sollte Aufgabenliste sichtbar sein |
| `ProjektErstellen_UndNeueAufgabeAnlegen_E2E()` | Nach Projekterstellung neue Aufgabe anlegen, Status sollte nicht "Gestartet" sein |
| `AufgabeAnlegen_ZeigtStartenButton_E2E()` | Nach Aufgabenanlage "Starten"-Button sichtbar und Fenster hat gültiges Handle |
| `DarkModeAktivierenUndPersistieren_E2E()` | Dark Mode in Einstellungen umschalten und über Persistierung verifizieren |
| `Dashboard_KeineRecoveryBanner_BeiSauberemStart_E2E()` | Beim Clean Start sollte kein Recovery-Banner angezeigt werden |
| `EinstellungenOeffnen_ZeigtEinstellungsSeite_E2E()` | Einstellungsseite sollte öffnen und Speichern-Button zeigen |
| `EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E()` | Arbeitsverzeichnis ändern und speichern |
| `EinstellungenNavigation_BleibtNachMehrerenKlicks_Stabil_E2E()` | Mehrfache Navigation zu Einstellungsseite sollte stabil bleiben |

## Test-Konfiguration

- **Collection: "E2E"** — Alle E2E-Tests laufen sequenziell (keine Parallelisierung), da sie eine `SOFTWARESCHMIEDE_TEST_DB_PATH`-Umgebungsvariable prozessweit setzen und mehrere Tests diese Datenbankinstanz konfliktieren könnten.
- **Umgebungsvariable `SOFTWARESCHMIEDE_TEST_DB_PATH`** — Zeigt der Anwendung auf temporäre Testdatenbank statt des System-DB-Pfads (`%LOCALAPPDATA%\Softwareschmiede\softwareschmiede.db`).
- **CI-Ausschluss:** `dotnet test --filter "Category!=E2E"` (E2E braucht interaktive Desktop-Session).
- **Lokal ausführen:** `dotnet test --filter Category=E2E`.

## Bekannte Anforderungen

- Softwareschmiede.App muss im **Debug-Modus** gebaut sein (Ressourcen können nicht im Release-Mode korrekt aufgelöst werden für Tests).
- **Windows-Desktop-Session erforderlich** — Die Tests funktionieren nicht in headless/CI-Umgebungen, weil FlaUI auf die Windows-UI-Automation-API angewiesen ist.
- **App-Pfad-Auflösung:** Die Methode `ResolveAppExePath()` sucht nach `Softwareschmiede.App.exe` in `../../../Softwareschmiede.App/bin/Debug/<TargetFramework>/` oder `../Release/...`, je nachdem welche existiert.
