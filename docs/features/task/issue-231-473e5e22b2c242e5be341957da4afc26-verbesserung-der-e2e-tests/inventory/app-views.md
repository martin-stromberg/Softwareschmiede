# Bestehende App-Views

Diese Datei dokumentiert die bestehenden WPF-Views (XAML + Code-Behind) in der Anwendung, die als Ziele für das neue E2E-View-Pattern dienen.

**Verzeichnis:** `src/Softwareschmiede.App/Views/`

## Hauptansichten (nicht-Modal)

| View-Klasse | Datei | Beschreibung |
|-------------|-------|-------------|
| `MainWindow` | `MainWindow.xaml.cs` | Hauptfenster der Anwendung, Container für alle anderen Views |
| `DashboardView` | `DashboardView.xaml.cs` | Dashboard-Startansicht, zeigt Navigationsbuttons (Projekte, Einstellungen) |
| `ProjectListView` | `ProjectListView.xaml.cs` | Liste der vorhandenen Projekte mit "Neu"-Button |
| `ProjectDetailView` | `ProjectDetailView.xaml.cs` | Detailansicht eines geöffneten Projekts mit Aufgabenliste und "AufgabeNeu"-Button |
| `TaskDetailView` | `TaskDetailView.xaml.cs` | Separate, fensterumfassende Detailansicht für Aufgabenbearbeitung (Edit-Panel) |
| `SettingsView` | `SettingsView.xaml.cs` | Einstellungsseite mit Tabs (Plugins, Kommandozeilenparameter, etc.) |
| `FileExplorerView` | `FileExplorerView.xaml.cs` | Datei-Explorer-Ansicht für Arbeitsverzeichnis-Navigation |
| `AutonomAufgabeDetailView` | `AutonomAufgabeDetailView.xaml.cs` | Detailansicht für autonome Aufgaben |
| `TodoListView` | `TodoListView.xaml.cs` | Liste von To-Dos in einer Aufgabe |

## Dialog-Ansichten (Modal)

| Dialog-Klasse | Datei | Beschreibung |
|---------------|-------|-------------|
| `RepositoryAssignDialog` | `RepositoryAssignDialog.xaml.cs` | Dialog zur Zuweisung von Repositories zu Projekten |
| `PluginSelectionDialog` | `PluginSelectionDialog.xaml.cs` | Dialog zur Auswahl von KI-Plugins beim Starten einer Aufgabe |
| `IssueSelectionDialog` | `IssueSelectionDialog.xaml.cs` | Dialog zur Auswahl von Issues/Problemen |
| `IssueCreateDialog` | `IssueCreateDialog.xaml.cs` | Dialog zum Erstellen neuer Issues |
| `AutonomAufgabeInitialisierungsDialog` | `AutonomAufgabeInitialisierungsDialog.xaml.cs` | Dialog zur Initialisierung autonomer Aufgaben |
| `AutonomAufgabeDetailDialog` | `AutonomAufgabeDetailDialog.xaml.cs` | Dialog für Details autonomer Aufgaben |
| `ArbeitsverzeichnisBearbeitenDialog` | `ArbeitsverzeichnisBearbeitenDialog.xaml.cs` | Dialog zur Bearbeitung des Arbeitsverzeichnisses |
| `OpenTodosDialog` | `OpenTodosDialog.xaml.cs` | Dialog zur Anzeige offener To-Dos |
| `HelpTextDialog` | `HelpTextDialog.xaml.cs` | Dialog zur Anzeige von Hilfetexten |
| `SolutionSelectionDialog` | `SolutionSelectionDialog.xaml.cs` | Dialog zur Auswahl von Visual Studio-Lösungen |
| `UpdateProgressDialog` | `UpdateProgressDialog.xaml.cs` | Dialog zur Anzeige von Update-Fortschritt |

## UI-Steuerungselemente (in Tests identifizierbar über Automation Name)

### Dashboard-Ebene

| Element | Automation Name | Typ | Zweck |
|---------|-----------------|-----|-------|
| Dashboard-Button | " Dashboard" | Button | Navigation zum Dashboard |
| Projekte-Button | " Projekte" | Button | Navigation zur Projektliste |
| Einstellungen-Button | " Einstellungen" | Button | Navigation zu Einstellungen |

### Projektlisten-Ebene

| Element | Automation Name | Typ | Zweck |
|---------|-----------------|-----|-------|
| Neu-Button | "Neu" | Button | Erstellt neues Projekt |
| Projekt-Name (TextBox) | "ProjektName" | TextBox | Eingabe des Projektnamens |
| Speichern-Button | "Speichern" | Button | Speichert neues/geändertes Projekt |
| Löschen-Button | "Löschen" | Button | Löscht Projekt |
| Zurück-Button | "Zurück" | Button | Navigiert zurück zur vorherigen Ansicht |

### Projektdetail-Ebene

| Element | Automation Name | Typ | Zweck |
|---------|-----------------|-----|-------|
| ProjektName-Feld | "ProjektName" | TextBox | Anzeige/Bearbeitung des Projektnamens |
| AufgabeNeu-Button | "AufgabeNeu" | Button | Erstellt neue Aufgabe |
| Zuweisen-Button | "Zuweisen" | Button | Öffnet Repository-Zuweisungs-Dialog |
| OffeneAufgabenListe | "OffeneAufgabenListe" | List | Liste offener Aufgaben |
| Speichern-Button | "Speichern" | Button | Speichert Projektänderungen |
| Löschen-Button | "Löschen" | Button | Löscht Projekt |

### Aufgabendetail-Ebene (TaskDetailView)

| Element | Automation Name | Typ | Zweck |
|---------|-----------------|-----|-------|
| EditTitel-Feld | "EditTitel" | TextBox | Bearbeitung des Aufgabentitels |
| Starten-Button | "Starten" | Button | Startet Aufgabenausführung (mit Plugin-Wahl-Dialog) |
| Speichern-Button | "Speichern" | Button | Speichert Aufgabenänderungen |
| Zurück-Button | "Zurück" | Button | Navigiert zurück zur Projektdetailansicht |
| Löschen-Button | "Löschen" | Button | Löscht aktuelle Aufgabe |

### Einstellungen-Ebene (SettingsView)

| Element | Automation Name | Typ | Zweck |
|---------|-----------------|-----|-------|
| Plugins-Tab | "Plugins" | Tab | Konfiguration von Plugins |
| WorkspaceMode (ComboBox) | "WorkspaceMode" | ComboBox | Einstellung des Workspace-Modus |
| SourceDirectory (TextBox) | "SourceDirectory" | TextBox | Eingabe des Quellverzeichnisses |
| Speichern-Button | "Speichern" | Button | Speichert Einstellungen |
| Einstellungen gespeichert.-Banner | "Einstellungen gespeichert." | TextBlock | Bestätigung des Speicherns |

### Dialog-Elemente

| Dialog | Element | Automation Name | Typ |
|--------|---------|-----------------|-----|
| Repository-Zuweisungs-Dialog | Dialog-Titel | "Repository zuweisen" | Window |
| Repository-Zuweisungs-Dialog | Repository-Liste | (List mit ListItems) | List |
| Repository-Zuweisungs-Dialog | Zuweisen-Button | "Zuweisen" | Button |
| KI-Plugin-Auswahl-Dialog | Dialog-Titel | "KI-Plugin auswählen" | Window |
| KI-Plugin-Auswahl-Dialog | Plugin-ComboBox | "PluginAuswahl" | ComboBox |
| KI-Plugin-Auswahl-Dialog | Für Projekt verwenden (Checkbox) | "FuerProjektVerwenden" | CheckBox |
| KI-Plugin-Auswahl-Dialog | OK-Button | "OK" | Button |
| Lösch-Bestätigungsdialog (MessageBox) | Dialog-Titel | "Löschen bestätigen" | Window (native MessageBox) |
| Lösch-Bestätigungsdialog | Ja-Button | Automation ID "6" (IDYES) | Button |

### Fehlerbehandlung

| Element | Automation Name | Typ | Zweck |
|---------|-----------------|-----|-------|
| Fehlerbanner | "FehlerMeldung" | TextBlock | Anzeige von Fehlermeldungen, wird von WaitForElement als Fail-Fast-Signal genutzt |

## Hinweise für View-Pattern-Implementierung

1. **Haupt-Views:** Dashboard, ProjectList, ProjectDetail, TaskDetail, Settings sind die primären Ansichten, deren Sichtbarkeit über charakteristische Elemente bestimmt werden kann
2. **Dialoge:** Separate Window-Elemente mit eigenem Titel, können über `WaitForWindow()` abgefragt werden
3. **Navigation:** Ansichtswechsel erfolgen durch Button-Klicks auf Top-Level-Buttons (Dashboard, Projekte, Einstellungen) oder Rückkehr-Buttons (Zurück)
4. **Geschachtelte Struktur:** TaskDetailView verdeckt ProjectDetailView (fensterumfassend); ProjectDetailView verdeckt ProjectListView; ProjectListView verdeckt DashboardView
5. **UI-Elemente zur Erkennung:** Jede View hat charakteristische Elemente (z. B. "ProjektName" in ProjectDetail, "EditTitel" in TaskDetail), die zur `IsVisible`-Prüfung herangezogen werden können
