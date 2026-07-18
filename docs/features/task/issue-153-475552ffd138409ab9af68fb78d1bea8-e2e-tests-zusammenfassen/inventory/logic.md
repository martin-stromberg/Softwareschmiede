# Bestandsaufnahme: Logik-Klassen

## `WpfTestBase`
Datei: `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`

### Navigation und Projektmanagement

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `NavigateToProjecten()` | protected | Klickt den "Projekte"-Button und navigiert zur Projektliste |
| `NavigateToSettings()` | protected | Navigiert zur Einstellungsseite und wartet, bis Settings-Tabs geladen sind |
| `StartAndNavigateToProjects()` | protected | Startet App, wartet auf Hauptfenster, navigiert zur Projektliste, optional legt Projekt an und öffnet es |
| `CreateProject()` | protected | Legt ein neues Projekt an, füllt Namen ein und speichert; navigiert nach dem Speichern automatisch zurück |
| `OpenProject()` | protected | Öffnet ein Projekt anhand des Namens aus der Liste |
| `CreateAndOpenProject()` | protected | Kombiniert CreateProject + OpenProject |

### Element-Wartenlogik und UI-Interaktion

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `WaitForElement()` | protected static | Wartet auf ein Element im Teilbaum eines Parent; wirft TimeoutException bei Timeout; bietet Fail-Fast-Diagnose bei Fehlerbannern |
| `WaitUntilGone()` | protected static | Wartet, bis ein Element verschwunden ist; assertiert anschließend, dass das Element nicht mehr vorhanden ist |
| `WaitForWindow()` | protected | Wartet auf ein Top-Level-Fenster mit angegebenem Titel |
| `SelectComboBoxItemByClick()` | protected static | Wählt einen ComboBox-Eintrag durch Klick auf das Item (robuster als FlaUI's Select-Methode) |
| `WaitForSelectedComboBoxItem()` | protected static | Wartet, bis eine ComboBox den erwarteten selektierten Eintrag anzeigt |

### Repository und Plugin-Konfiguration

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateLocalSourceDirectory()` | protected | Erstellt ein temporäres lokales Quellverzeichnis mit Unterordner (simuliertes Repository) für LocalDirectoryPlugin-Tests |
| `ConfigureLocalDirectoryPlugin()` | protected | Öffnet Einstellungen, wählt LocalDirectoryPlugin, setzt WorkspaceMode und SourceDirectory, speichert, navigiert zurück |
| `AssignLocalDirectoryRepository()` | protected | Öffnet Repository-Zuweisungs-Dialog, wählt erstes Repository und bestätigt Zuweisung |
| `SetupProjectMitNeuerAufgabe()` | protected | Komplexe Setup-Methode: startet App, konfiguriert LocalDirectoryPlugin, legt Projekt an, öffnet es, weist Repository zu, erstellt neue Aufgabe |

### Plugin-Auswahl und Ausführung

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartenUndPluginWaehlen()` | protected | Klickt "Starten"-Button und bedient den anschließend erscheinenden Plugin-Auswahl-Dialog (wählt Plugin, bestätigt mit OK) |
| `SkipWennConPtyNichtVerfuegbar()` | protected static | Überspringt Test via Skip.If, wenn SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 gesetzt ist (für Tests mit CLI-Prozess via ConPTY) |

### Datenbank und Konfiguration

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LaunchApp()` | protected | Startet die Anwendung als Prozess mit temporärem DB-Pfad; wartet, bis Hauptfenster sichtbar ist; führt Startup-Log-Diagnose durch |
| `OpenTestDbContext()` | protected | Öffnet einen DbContext gegen die SQLite-Testdatenbank für direkte DB-Vorbedingungen |
| `DeleteTestDatabase()` | protected | Löscht die temporäre Testdatenbank, falls sie existiert |
| `ConfirmLocalDirectoryGitInitInSourceDirectory()` | protected static | Setzt Credential für LocalDirectoryPlugin, um git init im Quellverzeichnis zu erlauben |
| `SetLocalDirectoryWorkspaceMode()` | protected static | Setzt den Workspace-Modus des LocalDirectoryPlugins (InSourceDirectory oder SeparateWorkingDirectory) |

### Logging und Fehlerdiagnose

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetLatestAppLogContent()` | protected | Gibt den seit LaunchApp angehängten Inhalt der neuesten App-Log-Datei zurück |
| `CheckAppStartupException()` | protected | Prüft die seit Start angehängten Log-Zeilen auf Startup-Fehlersignatur |

### Lifecycle und Ressourcenverwaltung

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Dispose()` | public | Schließt die Anwendung, wartet auf Prozess-Exit, löscht Testdatenbank, stellt Credential-Store-Zustand wieder her |

### Properties

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| `Automation` | UIA3Automation (protected) | Gibt den FlaUI-Automatisierungskontext zurück; wirft InvalidOperationException, wenn LaunchApp nicht aufgerufen wurde |
| `FlaUiApp` | FlaUI.Core.Application (protected) | Gibt den gestarteten FlaUI-Application-Handle zurück |
| `TestDbPath` | string (protected) | Pfad zur SQLite-Testdatenbank des laufenden App-Prozesses |
| `Short` | TimeSpan (protected static) | Kurzes Timeout (20s) für schnell erscheinende UI-Elemente |
| `Medium` | TimeSpan (protected static) | Mittleres Timeout (15s) für UI-Elemente nach asynchronen Operationen |
| `Long` | TimeSpan (protected static) | Langes Timeout (30s), z.B. für das initiale Erscheinen des Hauptfensters |

## Zusammenfassung

Die `WpfTestBase`-Klasse ist die zentrale Infrastruktur-Komponente für alle E2E-Tests. Sie bietet:

- **Bereits gut strukturierte Hilfsmethoden** für häufige Testoperationen (Navigation, Projekt-CRUD, Element-Wartenlogik, Plugin-Konfiguration)
- **Fachlich-semantische Methodennamen** ("CreateProject" statt "ClickCreateButton")
- **Robuste Fehlerbehandlung** mit Fail-Fast-Diagnosen (Fehlerbanner-Checks, Startup-Log-Inspektion)
- **Wiederverwendbarkeit** durch protected-Zugriff und klare Preconditions/Postconditions
- **Konfigurierbare Timeouts** (Short, Medium, Long) für unterschiedliche Szenarien
- **Credential- und Datenbank-Verwaltung** für Test-Isolierung

Abonnierte Events: Keine expliziten Event-Abonnements in WpfTestBase; die Klasse interagiert mit FlaUI-Automation und der Windows-UI-Automation.

Publizierte Events: Keine; WpfTestBase ist eine Testinfrastruktur-Klasse ohne öffentliche Events.
