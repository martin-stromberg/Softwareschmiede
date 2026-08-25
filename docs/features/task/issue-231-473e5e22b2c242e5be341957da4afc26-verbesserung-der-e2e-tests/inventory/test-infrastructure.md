# Test-Infrastruktur und WpfTestBase

## `WpfTestBase`

**Datei:** `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`

**Zweck:** Basis-Testklasse für WPF-E2E-Tests. Startet die Anwendung als separaten Prozess, verwaltet das Hauptfenster und beendet den Prozess nach dem Test.

### Konstanten und Timeouts

| Konstante | Wert | Beschreibung |
|-----------|------|-------------|
| `Short` | 20s | Timeout für schnell erscheinende UI-Elemente (einmaliger JIT-/Rendering-Warmup-Puffer) |
| `Medium` | 15s | Timeout für UI-Elemente nach asynchronen Operationen |
| `Long` | 30s | Timeout für Fenster-Erscheinen beim App-Start |
| `BuildConfigDebug` | "Debug" | Build-Konfiguration |
| `BuildConfigRelease` | "Release" | Build-Konfiguration |
| `TargetFramework` | "net10.0-windows10.0.17763.0" | .NET-Target-Framework |

### Verwaltete Credential-Schlüssel

| Schlüssel | Beschreibung |
|-----------|-------------|
| `Softwareschmiede.Codex.CommandLineParameters` | CLI-Parameter |
| `LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory` | Git-Init-Bestätigung |
| `LocalDirectoryPlugin.WorkspaceMode` | Workspace-Modus des LocalDirectoryPlugins |
| `LocalDirectoryPlugin.SourceDirectory` | Quellverzeichnis des Plugins |
| `Softwareschmiede.Codex.ExecutablePath` | Pfad zum ausführbaren Programm |

### Zentrale Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LaunchApp(bool ensureDatabaseDeleted = true)` | protected | Startet die Anwendung als Prozess, wartet auf Hauptfenster |
| `LaunchAppAndGetMainWindow()` | protected | Startet die App und gibt das Hauptfenster direkt zurück |
| `OpenTestDbContext()` | protected | Öffnet DbContext gegen die Testdatenbank |
| `WaitForElement(AutomationElement parent, Func<ConditionFactory, ConditionBase> conditionFunc, TimeSpan timeout)` | protected static | Wartet bis Element gefunden oder Timeout |
| `WaitForWindow(string title, TimeSpan timeout)` | protected | Wartet bis Top-Level-Fenster mit Titel erscheint |
| `WaitUntilGone(AutomationElement parent, Func<ConditionFactory, ConditionBase> conditionFunc, TimeSpan timeout)` | protected static | Wartet bis Element verschwindet |
| `NavigateToProjects(AutomationElement mainWindow)` | protected | Navigiert zur Projektliste (Button " Projekte") |
| `NavigateBackFromProjectCardToProjectsList(AutomationElement mainWindow)` | protected | Navigiert von Projekt-Kachel zur Projektliste |
| `NavigateBackFromTaskToProject(Window mainWindow)` | protected | Navigiert von Aufgabendetail zur Projektdetail |
| `NavigateBackToDashboard(AutomationElement mainWindow)` | protected | Navigiert zum Dashboard (Button "Dashboard") |
| `NavigateToSettings(AutomationElement mainWindow)` | protected | Navigiert zu Einstellungen, wartet auf Tabs |
| `NavigateToProjectsAndCreateProject(Window mainWindow, string projectName)` | protected | Navigiert zu Projekten und erstellt Projekt |
| `CreateProject(AutomationElement mainWindow, string name)` | protected | Erstellt und speichert neues Projekt |
| `OpenProject(AutomationElement mainWindow, string name)` | protected | Öffnet Projekt aus der Liste |
| `CreateAndOpenProject(AutomationElement mainWindow, string name)` | protected | Erstellt und öffnet Projekt in einem Schritt |
| `StartAndNavigateToProjects(string? projektName = null)` | protected | Startet App und navigiert zu Projekten |
| `SelectComboBoxItemByClick(AutomationElement comboBoxElement, string itemText, TimeSpan timeout)` | protected static | Wählt ComboBox-Eintrag per Klick |
| `WaitForSelectedComboBoxItem(AutomationElement comboBoxElement, string expectedItemText, TimeSpan timeout)` | protected static | Wartet bis ComboBox einen bestimmten Eintrag zeigt |
| `CreateLocalSourceDirectory(string repositoryFolderName, bool initializeGitRepository = true)` | protected | Erstellt temporäres lokales Quellverzeichnis |
| `ConfigureLocalDirectoryPlugin(AutomationElement mainWindow, string sourceDirectory, bool useInSourceDirectoryMode = true)` | protected | Konfiguriert LocalDirectoryPlugin über UI |
| `AssignLocalDirectoryRepository(AutomationElement mainWindow)` | protected | Öffnet Repository-Zuweisungs-Dialog und bestätigt |
| `OpenRepositoryAssignDialog(AutomationElement mainWindow)` | protected | Öffnet Repository-Zuweisungs-Dialog |
| `WaitForFirstRepositoryItem(AutomationElement dialog)` | protected static | Wartet auf erstes Item in Repository-Liste |
| `SetupProjectMitNeuerAufgabe(Window mainWindow, string repositoryFolderName, string projektName, bool useInSourceDirectoryMode = true, bool initializeSourceGitRepository = true)` | protected | Konfiguriert Plugin, erstellt Projekt und Aufgabe |
| `SetupProjectMitNeuerAufgabeForStartedApp(Window mainWindow, string repositoryFolderName, string projektName, bool useInSourceDirectoryMode = true, bool initializeSourceGitRepository = true)` | protected | Gleich wie oben, ohne App zu starten |
| `SkipWennConPtyNichtVerfuegbar()` | protected static | Überspringt Test wenn ConPTY nicht verfügbar |
| `StartenUndPluginWaehlen(AutomationElement mainWindow, string pluginName, bool fuerProjektVerwenden = false)` | protected | Klickt "Starten" und bedient Plugin-Auswahl-Dialog |
| `NeueAufgabeAnlegen(AutomationElement mainWindow)` | protected | Klickt "AufgabeNeu"-Button, wartet auf "EditTitel" |
| `AufgabeTitelSetzen(AutomationElement mainWindow, string titel)` | protected | Setzt Titel der Aufgabe im Edit-Feld |
| `AufgabeDetailSpeichern(AutomationElement mainWindow, bool navigateBackToProject)` | protected | Speichert Aufgabendetail über "Speichern"-Button |
| `AufgabeDetailZurueck(AutomationElement mainWindow)` | protected | Verwirft Bearbeitung über "Zurück"-Button |
| `DeleteCurrentProject(AutomationElement mainWindow)` | protected | Löscht aktuelles Projekt über "Löschen"-Button |
| `DeleteCurrentTask(AutomationElement mainWindow)` | protected | Löscht aktuelle Aufgabe über "Löschen"-Button |
| `OffeneAufgabenItems(AutomationElement mainWindow)` | protected | Wartet auf "OffeneAufgabenListe" und gibt Items zurück |
| `ErsteOffeneAufgabeOeffnen(AutomationElement[] items)` | protected static | Öffnet erstes Item aus Aufgabenliste per Doppelklick |
| `AufgabeAusListeOeffnen(AutomationElement mainWindow, string titel)` | protected | Sucht Aufgabe in Liste und öffnet sie |
| `ProjektNamenAendernUndSpeichern(AutomationElement mainWindow, string neuerName)` | protected | Ändert Projektname und speichert |
| `WechsleAufgabenansicht(Window mainWindow, string viewButtonName)` | protected | Wechselt Ansicht in Aufgabendetail (z. B. Info → Protokoll) |
| `GetHelpTextOrName(AutomationElement element)` | protected static | Liest HelpText oder Name eines Elements |
| `WaitForProzessStartEintragAsync(string substring, TimeSpan? timeout = null, string sinceContent = "")` | protected | Wartet bis Prozessstart-Logdatei einen Eintrag enthält |
| `DeleteTestDatabase()` | protected | Löscht temporäre Testdatenbank |
| `ConfirmLocalDirectoryGitInitInSourceDirectory()` | protected static | Bestätigt Git-Init im Quellverzeichnis |
| `SetLocalDirectoryWorkspaceMode(string workspaceMode)` | protected static | Setzt Workspace-Modus des Plugins |
| `ResolveProzessStartLogPfad()` | protected | Löst Pfad der Prozessstart-Logdatei auf |
| `CheckAppStartupException()` | protected | Prüft Log auf Startup-Fehler |
| `Dispose()` | public | Bereinigung: beendet App, speichert Logs, löscht Datenbank |

### Properties

| Property | Sichtbarkeit | Typ | Beschreibung |
|----------|-------------|-----|-------------|
| `TestDbPath` | protected | string | Pfad zur SQLite-Testdatenbank des laufenden App-Prozesses |
| `Automation` | protected | UIA3Automation | FlaUI-Automatisierungskontext (wirft InvalidOperationException wenn LaunchApp nicht aufgerufen) |
| `FlaUiApp` | protected | FlaUI.Core.Application | Gestarteter FlaUI-Application-Handle (wirft InvalidOperationException wenn LaunchApp nicht aufgerufen) |

### Fehlerbehandlung

- `WaitForElement()` prüft auf "FehlerMeldung"-Banner als Fail-Fast-Diagnose
- App-Startup-Log wird inspiziert, um bei Fehlen des Hauptfensters eine aussagekräftige Fehlermeldung zu liefern (statt generischem "Timeout")
- Credential-Store-Zustand wird vor jedem Test gesichert und nach dem Test wiederhergestellt
- Prozessausstieg wird mit Timeout überwacht

### Abhängigkeiten und Integrationen

- **FlaUI:** `FlaUI.Core.AutomationElements`, `FlaUI.Core.Conditions`, `FlaUI.Core.Input`, `FlaUI.UIA3`
- **EntityFramework:** `Microsoft.EntityFrameworkCore` für Testdatenbank
- **App Views:** Imports aus `Softwareschmiede.App.Views` (nicht direkt genutzt, aber Navigation zielt auf diese Views ab)
- **Infrastructure Services:** `Softwareschmiede.Infrastructure.Services` (z. B. `AufzeichnenderProzessStarter`, `WindowsCredentialStore`)
- **Test Services:** `Softwareschmiede.Tests.Infrastructure.Services`

### Bestehende Test-Struktur

**Haupttest-Klasse:** `End2EndTest` in `src/Softwareschmiede.Tests/E2E/MainTest.cs`

- Erbt von `WpfTestBase`
- Enthält zwei Haupttest-Methoden: `RunGeneralTests()` und `RunConPtyTests()`
- Ruft Szenario-Methoden auf, die in separaten `E2E_*.cs`-Dateien definiert sind
- Nutzt `[Collection("E2E")]` für Serialisierung von Tests (prozessweite Umgebungsvariable)
- Nutzt `[SkippableFact]` für ConPTY-Tests mit Umgebungsvariablen-Probe

**Szenario-Dateien:** 29 Dateien wie `E2E_TaskDetailNavigation.cs`, `E2E_PluginAktivierung.cs`, etc.

- Enthalten protected Methoden mit Suffix `_E2E`
- Werden von Test-Klasse aufgerufen
- Nutzen `WpfTestBase`-Hilfsmethoden direkt für UI-Interaktion
- Übergeben `Window`/`AutomationElement` zwischen Methoden

### Konfiguration und Umgebungsvariablen

| Variable | Zweck |
|----------|-------|
| `SOFTWARESCHMIEDE_TEST_DB_PATH` | Pfad zur Testdatenbank für jeden laufenden Test |
| `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS` | Wenn `"1"`, werden ConPTY-abhängige Tests übersprungen |

### Weitere Test-Hilfsmittel im E2E-Verzeichnis

- **`AppStartupLogInspector.cs`:** Hilfklasse zum Auslesen und Filtern von App-Log-Dateien während Tests
- **`ConPtyEnvironmentProbe.cs`:** Bestätigt Verfügbarkeit von ConPTY in der Ausführungsumgebung
