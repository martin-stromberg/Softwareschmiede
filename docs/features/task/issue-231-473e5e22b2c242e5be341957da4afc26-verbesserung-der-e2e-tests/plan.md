# Umsetzungsplan: Verbesserung der E2E-Tests — View-Pattern

## Übersicht

Das View-Pattern wird als neue Test-Infrastruktur-Schicht über der FlaUI-API eingeführt. Eine Klassenhierarchie `BaseWindowView` mit spezialisierten Subklassen für jede Anwendungsansicht abstrahiert wiederholte UI-Interaktionsmuster und macht E2E-Tests wartbar und leserlich. Eine Erweiterungsmethode `Window.CurrentView()` erkennt automatisch die aktuelle Ansicht anhand ihres UI-Inhalts. Die Implementierung ist additiv — bestehende Tests bleiben unverändert und funktionsfähig.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| View-Klassenhierarchie | `BaseWindowView`-Basisklasse mit spezialisierten Subklassen | Folgt WPF-Namensgebung, ist FlaUI-idiomatisch, ermöglicht Code-Reuse über gemeinsame Basislogik |
| View-Erkennung | Heuristik-basierte Analyse charakteristischer UI-Elemente pro Ansicht | Robust gegen UI-Umgestaltungen, kein zusätzlicher Coupling zur Produktions-ViewModel-Hierarchie, klare Fehlerdiagnose bei fehlenden Markern |
| `IsVisible`-Semantik | Prüft nur, ob Ansicht gerade aktiv/fokussiert ist (oberste Modal / sichtbare Haupt-View) | Vereinfacht Logik; überlagerte Views sind nicht „sichtbar" |
| `ForceShow()`-Verhalten bei bereits sichtbarer Ansicht | No-Op (nur Warten auf Synchronisation) | Verhindert unerwünschte State-Resets; Tests können explizit schließen+öffnen, falls nötig |
| Rückgabewert von `ForceShow()` / `ForceClose()` | `this` (Fluent-API-Unterstützung) | Ermöglicht Kettenaufrufe auf der aktuellen View; Aufrufer können explizit `CurrentView()` aufrufen für View-Wechsel |
| View-Klassen-Abhängigkeiten | Nutzen geschützte statische Hilfsmethoden aus `WpfTestBase` (z. B. `WaitForElement`) | Vermeidet Vererbung von `BaseWindowView` zu `WpfTestBase`; hält Test-Code-Schichten separat |
| Fehlerbehandlung in `CurrentView()` | Ausführliche `InvalidOperationException` mit Liste der erwarteten Marker und aktuell gefundenen Elemente | Schnellere Fehlerdiagnose im Test-Debugging |
| Namenskonventionen | Suffix `View` für Klassen (z. B. `ProjectDetailView`, `MenuView`) | Konsistent mit WPF-Namensgebung in `src/Softwareschmiede.App/Views/` |
| Dialog-Behandlung | Separate `DialogView`-Basisklasse von `BaseWindowView` | Dialoge sind modale Top-Level-Fenster mit eigenem Titel/Handle, andere Navigationslogik als Haupt-Views |

## Programmabläufe

### View-Erkennung via `CurrentView()`

1. Test aufgerufen mit Hauptfenster (`Window` aus FlaUI)
2. `CurrentView()` wird aufgerufen
3. Methode durchsucht Fenster nach charakteristischen Elementen für jede registrierte View
4. Falls Marker gefunden → Instanz der erkannten View-Klasse wird erzeugt und zurückgegeben
5. Falls keine Ansicht erkannt → `InvalidOperationException` mit Diagnose (gefundene Elemente, erwartete Marker)

Beteiligte Klassen/Komponenten: `BaseWindowView`, Alle `*View`-Subklassen, `Window` (FlaUI)

### Navigation zu Ansicht via `ForceShow()`

1. Test ruft `view.ForceShow()` auf
2. `ForceShow()`-Implementierung navigiert durch UI-Klicks (z. B. Button-Klicks, Menüpfade)
3. Nach Navigation wartet Methode auf charakteristische Elemente der Zielansicht (Synchronisation via `WaitForElement`)
4. Gibt `this` zurück

Beteiligte Klassen/Komponenten: `BaseWindowView`, Spezifische `*View`-Subklassen

### Schließen von Ansicht via `ForceClose()`

1. Test ruft `view.ForceClose(recurseToDashboard: false)` oder `view.ForceClose(recurseToDashboard: true)` auf
2. **`recurseToDashboard = false`:** Schließt aktuelle Ansicht durch "Zurück"-Button oder "Abbrechen" in Dialog; gibt `this` zurück
3. **`recurseToDashboard = true`:**
   - Schließt aktuelle Ansicht
   - Prüft, ob übergeordnete Ansicht sichtbar wird (z. B. nach Schließen von TaskDetail wird ProjectDetail sichtbar)
   - Falls ja: ruft rekursiv `ForceClose(recurseToDashboard: true)` auf übergeordneter Ansicht auf
   - Stoppt Rekursion, wenn nur noch Dashboard sichtbar ist
   - Ggf. navigiert explizit zum Dashboard, falls keine weitere Ansicht verfügbar ist
   - Gibt `this` zurück

Beteiligte Klassen/Komponenten: `BaseWindowView`, Spezifische `*View`-Subklassen

### Menu-Interaktion via `Menu`-Property

1. Test ruft `view.Menu` auf (Property vom Typ `MenuView`)
2. `MenuView` stellt Methoden zum Zugriff auf und Aktivierung von Menü-Elementen bereit (z. B. `NavigateToDashboard()`, `NavigateToProjects()`, `NavigateToSettings()`)
3. Test kann über Menü navigieren oder deren State prüfen

Beteiligte Klassen/Komponenten: `BaseWindowView`, `MenuView`, `Window` (FlaUI)

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `BaseWindowView` | Abstrakte Klasse | Basisklasse für alle UI-View-Helper; definiert Schnittstelle für View-Interaktionen (IsVisible, ForceShow, ForceClose, Menu) |
| `MenuView` | Konkrete Klasse (extends `BaseWindowView`) | Spezialisierte View für Menü-Interaktionen; stellt Methoden zum Zugriff auf und Aktivierung von Navigations-Elementen bereit |
| `DashboardView` | Konkrete Klasse (extends `BaseWindowView`) | View für Dashboard-Ansicht; erkennt sich über Navigationsbuttons (Projekte, Einstellungen) |
| `ProjectListView` | Konkrete Klasse (extends `BaseWindowView`) | View für Projektlisten-Ansicht; erkennt sich über "Neu"-Button und Projekt-Elemente |
| `ProjectDetailView` | Konkrete Klasse (extends `BaseWindowView`) | View für Projektdetail-Ansicht; erkennt sich über "ProjektName"-Feld und "AufgabeNeu"-Button |
| `TaskDetailView` | Konkrete Klasse (extends `BaseWindowView`) | View für Aufgabendetail-Ansicht; erkennt sich über "EditTitel"-Feld und "Speichern"/"Zurück"-Buttons |
| `SettingsView` | Konkrete Klasse (extends `BaseWindowView`) | View für Einstellungen-Ansicht; erkennt sich über Tabs (Plugins, etc.) |
| `FileExplorerView` | Konkrete Klasse (extends `BaseWindowView`) | View für Datei-Explorer-Ansicht; erkennt sich über charakteristische Datei-Explorer-Elemente |
| `AutonomAufgabeDetailView` | Konkrete Klasse (extends `BaseWindowView`) | View für autonome Aufgabendetail-Ansicht |
| `TodoListView` | Konkrete Klasse (extends `BaseWindowView`) | View für To-Do-Listen-Ansicht |
| `DialogView` | Abstrakte Klasse (extends `BaseWindowView`) | Basisklasse für modale Dialoge; unterscheidet sich in Navigationslogik und Fenster-Handling von Haupt-Views |
| `RepositoryAssignDialogView` | Konkrete Klasse (extends `DialogView`) | View für Repository-Zuweisungs-Dialog |
| `PluginSelectionDialogView` | Konkrete Klasse (extends `DialogView`) | View für KI-Plugin-Auswahl-Dialog |
| `IssueSelectionDialogView` | Konkrete Klasse (extends `DialogView`) | View für Issue-Auswahl-Dialog |
| `IssueCreateDialogView` | Konkrete Klasse (extends `DialogView`) | View für Issue-Erstellung-Dialog |
| `AutonomAufgabeInitialisierungsDialogView` | Konkrete Klasse (extends `DialogView`) | View für autonome Aufgaben-Initialisierungs-Dialog |
| `AutonomAufgabeDetailDialogView` | Konkrete Klasse (extends `DialogView`) | View für autonome Aufgabendetail-Dialog |
| `ArbeitsverzeichnisBearbeitenDialogView` | Konkrete Klasse (extends `DialogView`) | View für Arbeitsverzeichnis-Bearbeitungs-Dialog |
| `OpenTodosDialogView` | Konkrete Klasse (extends `DialogView`) | View für offene To-Dos-Dialog |
| `HelpTextDialogView` | Konkrete Klasse (extends `DialogView`) | View für Hilfetext-Dialog |
| `SolutionSelectionDialogView` | Konkrete Klasse (extends `DialogView`) | View für Visual Studio-Lösungs-Auswahl-Dialog |
| `UpdateProgressDialogView` | Konkrete Klasse (extends `DialogView`) | View für Update-Fortschritts-Dialog |
| `DeleteConfirmationDialogView` | Konkrete Klasse (extends `DialogView`) | View für Lösch-Bestätigungs-Dialog (native MessageBox) |
| `ErrorView` | Konkrete Klasse (extends `BaseWindowView`) | View für Fehler-Banner-Interaktionen; erkennt sich über "FehlerMeldung"-TextBlock |

## Änderungen an bestehenden Klassen

### `WpfTestBase` (`Softwareschmiede.Tests/E2E/WpfTestBase.cs`)

- **Neue Methoden:** Keine erforderlich. Existierende Hilfsmethoden (`WaitForElement`, `NavigateToProjects`, etc.) können von `BaseWindowView` genutzt werden.
- **Geänderte Methoden:** Keine erforderlich.
- **Kompatibilität:** `WpfTestBase` bleibt unverändert und behält alle bestehenden Methoden. View-Pattern ist optionale Erweiterung; existierende Tests bleiben funktionsfähig.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Optionale Migration:** Tests können schrittweise auf View-Pattern migriert werden, ohne `WpfTestBase` zu brechen. Keine erzwungene Umstellung.
- **View-Erkennung bei UI-Umgestaltungen:** Falls Produktions-UI erheblich umgestaltet wird, müssen `IsVisible`-Implementierungen in View-Klassen ggf. angepasst werden. Risiko ist gering, da Marker charakteristische Elemente sind, die lange stabil bleiben.
- **Performance:** View-Erkennung via `CurrentView()` durchsucht den UI-Automation-Baum. In Tests mit vielen Elementen könnte dies marginal langsamer sein als direkter Element-Zugriff. Risiko ist vernachlässigbar (FlaUI-Abfragen laufen ohnehin in UI-Automation-Zeiten).
- **Dialog-Handling:** Modale Dialoge haben eigene Fenster-Handles; `DialogView`-Subklassen müssen explizit über `WaitForWindow()` abgefragt werden. Unterscheidet sich von Haupt-Views.

Keine weiteren bekannten Seiteneffekte.

## Umsetzungsreihenfolge

1. **`BaseWindowView`-Klasse anlegen (abstrakte Basisklasse)**
   - Voraussetzungen: FlaUI-NuGet-Pakete (`FlaUI.Core`, `FlaUI.UIA3`), bestehender `WpfTestBase`
   - Beschreibung: 
     - Anlegen von `src/Softwareschmiede.Tests/E2E/Views/BaseWindowView.cs`
     - Definiere abstrakte Properties/Methoden: `Window`, `IsVisible`, `ForceShow()`, `ForceClose(bool recurseToDashboard)`, `Menu` (Property vom Typ `MenuView`)
     - Konstruktor nimmt `Window` (FlaUI) als Parameter
     - Schützte Hilfsmethoden für Element-Suche, die `WpfTestBase`-Methoden verwenden

2. **`MenuView`-Klasse anlegen (erbt von `BaseWindowView`)**
   - Voraussetzungen: `BaseWindowView` aus Schritt 1
   - Beschreibung:
     - Anlegen von `src/Softwareschmiede.Tests/E2E/Views/MenuView.cs`
     - Implementiere spezifische Methoden für Menü-Navigation: `NavigateToDashboard()`, `NavigateToProjects()`, `NavigateToSettings()`
     - Implementiere `IsVisible`: Prüfe, ob Top-Level-Navigations-Buttons sichtbar sind
     - Implementiere `ForceShow()`: No-Op (Menü ist immer sichtbar im Hauptfenster)

3. **`DialogView`-Klasse anlegen (abstrakte Basisklasse für modale Dialoge)**
   - Voraussetzungen: `BaseWindowView` aus Schritt 1
   - Beschreibung:
     - Anlegen von `src/Softwareschmiede.Tests/E2E/Views/DialogView.cs`
     - Definiere Unterschiede zu `BaseWindowView`: Dialog hat eigenes Fenster-Handle, `ForceShow()` wartet auf Dialog-Fenster statt auf Elemente im Hauptfenster
     - Geschützte Methode `GetDialogWindow()` für Dialog-Fenster-Ermittlung

4. **`DashboardView`-Klasse anlegen**
   - Voraussetzungen: `BaseWindowView` aus Schritt 1, `MenuView` aus Schritt 2
   - Beschreibung:
     - Anlegen von `src/Softwareschmiede.Tests/E2E/Views/DashboardView.cs`
     - Implementiere `IsVisible`: Prüfe, ob Navigationsbuttons (Projekte, Einstellungen) sichtbar sind
     - Implementiere `ForceShow()`: Klicke auf "Dashboard"-Button im Menü oder navigiere explizit
     - Implementiere Navigation zu übergeordnete Ansicht (keine, da Dashboard oberste Ebene)

5. **`ProjectListView`-Klasse anlegen**
   - Voraussetzungen: `BaseWindowView` aus Schritt 1
   - Beschreibung:
     - Anlegen von `src/Softwareschmiede.Tests/E2E/Views/ProjectListView.cs`
     - Implementiere `IsVisible`: Prüfe auf "Neu"-Button und Projekt-Elemente (z. B. Projekt-Kacheln)
     - Implementiere `ForceShow()`: Klicke auf " Projekte"-Button
     - Implementiere Hilfsmethoden: `GetProjectElements()`, `CreateProject(string name)`, `OpenProject(string name)`

6. **`ProjectDetailView`-Klasse anlegen**
   - Voraussetzungen: `BaseWindowView` aus Schritt 1
   - Beschreibung:
     - Anlegen von `src/Softwareschmiede.Tests/E2E/Views/ProjectDetailView.cs`
     - Implementiere `IsVisible`: Prüfe auf "ProjektName"-Feld und "AufgabeNeu"-Button
     - Implementiere `ForceShow()`: Öffne Projekt aus Projektliste
     - Implementiere Hilfsmethoden: `GetProjectName()`, `CreateTask()`, `DeleteProject()`, `GetTaskElements()`

7. **`TaskDetailView`-Klasse anlegen**
   - Voraussetzungen: `BaseWindowView` aus Schritt 1
   - Beschreibung:
     - Anlegen von `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs`
     - Implementiere `IsVisible`: Prüfe auf "EditTitel"-Feld und "Speichern"/"Zurück"-Buttons
     - Implementiere `ForceShow()`: Öffne Task aus Projektdetail
     - Implementiere Hilfsmethoden: `GetTaskTitle()`, `SetTaskTitle(string title)`, `SaveTask()`, `DeleteTask()`, `GoBack()`

8. **`SettingsView`-Klasse anlegen**
   - Voraussetzungen: `BaseWindowView` aus Schritt 1
   - Beschreibung:
     - Anlegen von `src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs`
     - Implementiere `IsVisible`: Prüfe auf Einstellungs-Tabs (Plugins, etc.)
     - Implementiere `ForceShow()`: Klicke auf " Einstellungen"-Button
     - Implementiere Hilfsmethoden: `GetActiveTab()`, `SwitchTab(string tabName)`, `SaveSettings()`

9. **`FileExplorerView`, `AutonomAufgabeDetailView`, `TodoListView` anlegen**
   - Voraussetzungen: `BaseWindowView` aus Schritt 1, vorangegangene Schritt-Erkenntnisse
   - Beschreibung:
     - Anlegen entsprechender Klassen in `src/Softwareschmiede.Tests/E2E/Views/`
     - Jede Klasse implementiert `IsVisible`, `ForceShow()`, `ForceClose()` gemäß UI-Struktur
     - Hilfsmethoden für view-spezifische Interaktionen

10. **`ErrorView`-Klasse anlegen**
    - Voraussetzungen: `BaseWindowView` aus Schritt 1
    - Beschreibung:
      - Anlegen von `src/Softwareschmiede.Tests/E2E/Views/ErrorView.cs`
      - Implementiere `IsVisible`: Prüfe auf "FehlerMeldung"-TextBlock
      - Implementiere Hilfsmethoden: `GetErrorMessage()`, `DismissError()`

11. **Dialog-View-Klassen anlegen (RepositoryAssignDialogView, PluginSelectionDialogView, etc.)**
    - Voraussetzungen: `DialogView` aus Schritt 3, vorangegangene Erkenntnis
    - Beschreibung:
      - Anlegen aller Dialog-Subklassen in `src/Softwareschmiede.Tests/E2E/Views/Dialogs/`
      - Jede Klasse implementiert `IsVisible` und Dialog-spezifische Interaktionsmethoden
      - Beispiele:
        - `RepositoryAssignDialogView`: Methode `SelectRepository(string name)`, `Confirm()`
        - `PluginSelectionDialogView`: Methode `SelectPlugin(string name)`, `ConfirmForProject()`, `Confirm()`
        - `DeleteConfirmationDialogView`: Methode `Confirm()`, `Cancel()`

12. **`Window.CurrentView()`-Erweiterungsmethode anlegen**
    - Voraussetzungen: Alle View-Klassen aus Schritten 1-11
    - Beschreibung:
      - Anlegen von `src/Softwareschmiede.Tests/E2E/Views/WindowExtensions.cs`
      - Implementiere `CurrentView()` als Erweiterungsmethode für FlaUI `Window`
      - Durchsuche Fenster nach Markern; erkenne aktuelle View anhand charakteristischer Elemente
      - Reihenfolge der Prüfung: Dialog-Views (haben eigene Fenster), dann TaskDetailView, dann ProjectDetailView, dann ProjectListView, dann SettingsView, etc., fallback zu DashboardView
      - Werfe `InvalidOperationException` mit Diagnose, falls keine Ansicht erkannt

13. **Tests für `BaseWindowView` und alle *View-Subklassen anlegen (Unit/Integration-Tests)**
    - Voraussetzungen: Alle View-Klassen aus Schritten 1-12
    - Beschreibung:
      - Anlegen von `src/Softwareschmiede.Tests/E2E/ViewsTests/BaseWindowViewTests.cs` und spezifischen Test-Klassen für jede View
      - Tests für `IsVisible`-Implementierung jeder View (prüfe auf erwartete charakteristische Elemente)
      - Tests für `ForceShow()`-Navigation (navigiere zur Ansicht, prüfe auf sichtbare Marker)
      - Tests für `ForceClose()` und `recurseToDashboard`-Verhalten
      - Tests für `CurrentView()`-Erkennung verschiedener Ansichtszustände

14. **E2E-Tests für View-Pattern anlegen (neue E2E-Szenarien)**
    - Voraussetzungen: Alle View-Klassen und Unit-Tests aus Schritten 1-13
    - Beschreibung:
      - Anlegen von `src/Softwareschmiede.Tests/E2E/E2E_ViewPattern.cs` (neues Szenario)
      - Happy-Path-Test: Starte App → erkenne Dashboard via `CurrentView()` → navigiere zu Projektliste → erkenne ProjectListView → öffne Projekt → erkenne ProjectDetailView → erstelle Aufgabe → erkenne TaskDetailView → schließe Task mit `ForceClose()` → prüfe auf ProjectDetailView → navigiere zum Dashboard mit `ForceClose(recurseToDashboard: true)`
      - Test für Dialog-Erkennung: Öffne Repository-Zuweisungs-Dialog → erkenne RepositoryAssignDialogView via `CurrentView()` → wähle Repository → schließe Dialog
      - Test für Error-Handling: Erzwinge Fehler → erkenne ErrorView → Fehlermeldung lesen
      - Tests für Menu-Navigation: Nutze MenuView zur Navigation zwischen Ansichten

15. **Dokumentation aktualisieren (optional, am Ende)**
    - Voraussetzungen: Alle Klassen und Tests aus Schritten 1-14
    - Beschreibung:
      - Aktualisiere `src/Softwareschmiede.Tests/E2E/README.md` (falls vorhanden) oder erstelle Dokumentation
      - Beschreibe View-Pattern-Nutzung mit Beispielen (Vorher/Nachher aus requirement.md)
      - Dokumentiere Naming-Konventionen und Best Practices für neue View-Klassen

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `BaseWindowViewTests` | `Softwareschmiede.Tests.E2E.ViewsTests.BaseWindowViewTests` | Basis-Verhalten: Konstruktor, Fenster-Referenz, Menu-Property |
| `DashboardViewTests` | `Softwareschmiede.Tests.E2E.ViewsTests.DashboardViewTests` | IsVisible erkennt Dashboard, ForceShow navigiert zum Dashboard, Menu ist verfügbar |
| `ProjectListViewTests` | `Softwareschmiede.Tests.E2E.ViewsTests.ProjectListViewTests` | IsVisible erkennt Projektliste, ForceShow navigiert zu Projektliste, GetProjectElements() funktioniert |
| `ProjectDetailViewTests` | `Softwareschmiede.Tests.E2E.ViewsTests.ProjectDetailViewTests` | IsVisible erkennt Projektdetail, ForceShow öffnet Projekt, CreateTask() funktioniert, GetTaskElements() funktioniert |
| `TaskDetailViewTests` | `Softwareschmiede.Tests.E2E.ViewsTests.TaskDetailViewTests` | IsVisible erkennt Aufgabendetail, ForceShow öffnet Aufgabe, GetTaskTitle() liest Titel, SetTaskTitle() ändert Titel |
| `SettingsViewTests` | `Softwareschmiede.Tests.E2E.ViewsTests.SettingsViewTests` | IsVisible erkennt Einstellungen, ForceShow navigiert zu Einstellungen, SwitchTab() funktioniert |
| `MenuViewTests` | `Softwareschmiede.Tests.E2E.ViewsTests.MenuViewTests` | NavigateToDashboard(), NavigateToProjects(), NavigateToSettings() funktionieren |
| `CurrentViewTests` | `Softwareschmiede.Tests.E2E.ViewsTests.CurrentViewTests` | CurrentView() erkennt DashboardView, ProjectListView, ProjectDetailView, TaskDetailView, SettingsView, Dialog-Views korrekt; wirft InvalidOperationException bei unbekannter Ansicht mit aussagekräftiger Diagnose |
| `DialogViewTests` | `Softwareschmiede.Tests.E2E.ViewsTests.DialogViewTests` | Dialog-Basis-Verhalten: Fenster-Handle, IsVisible-Prüfung, ForceShow() wartet auf Dialog-Fenster |
| `RepositoryAssignDialogViewTests` | `Softwareschmiede.Tests.E2E.ViewsTests.Dialogs.RepositoryAssignDialogViewTests` | IsVisible erkennt Dialog, SelectRepository() funktioniert, Confirm() schließt Dialog |
| `PluginSelectionDialogViewTests` | `Softwareschmiede.Tests.E2E.ViewsTests.Dialogs.PluginSelectionDialogViewTests` | IsVisible erkennt Dialog, SelectPlugin() funktioniert, Confirm()/ConfirmForProject() funktionieren |
| `ErrorViewTests` | `Softwareschmiede.Tests.E2E.ViewsTests.ErrorViewTests` | IsVisible erkennt Fehler-Banner, GetErrorMessage() liest Fehlermeldung |
| `E2E_ViewPattern` (Happy Path) | `Softwareschmiede.Tests.E2E.E2E_ViewPattern` | Starte App → erkenne Dashboard → navigiere zu Projektliste → öffne Projekt → erkenne Projektdetail → erstelle Aufgabe → erkenne Aufgabendetail → speichere/schließe Aufgabe → schließe mit recurseToDashboard |
| `E2E_ViewPattern` (Dialog Navigation) | `Softwareschmiede.Tests.E2E.E2E_ViewPattern` | Repository-Zuweisungs-Dialog: öffne → erkenne Dialog → wähle Repository → schließe |
| `E2E_ViewPattern` (Error Handling) | `Softwareschmiede.Tests.E2E.E2E_ViewPattern` | Erzwinge Fehler → erkenne ErrorView → Fehlermeldung prüfen |
| `E2E_ViewPattern` (Menu Navigation) | `Softwareschmiede.Tests.E2E.E2E_ViewPattern` | Nutze MenuView zur Navigation zwischen Haupt-Views; prüfe View-Erkennung nach jedem Schritt |

### Betroffene bestehende Tests

Keine. Das View-Pattern ist additiv und bricht keine bestehenden Tests. Existierende E2E-Tests in `E2E_*.cs` Files funktionieren weiter mit `WpfTestBase`-Hilfsmethoden. Optionale Migration auf View-Pattern kann später erfolgen.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Happy Path: App-Start und View-Navigation | `Softwareschmiede.Tests.E2E.E2E_ViewPattern.RunViewPatternHappyPath()` | App startet, `CurrentView()` erkennt DashboardView, Navigation über `ForceShow()` funktioniert, Ansichtwechsel wird erkannt |
| View-Erkennung: DashboardView | `E2E_ViewPattern.RecognizeViewsCorrectly()` | Nach App-Start erkennt `CurrentView()` DashboardView anhand von Navigationsbuttons |
| View-Erkennung: ProjectListView | `E2E_ViewPattern.RecognizeViewsCorrectly()` | Nach Navigation zu Projektliste erkennt `CurrentView()` ProjectListView anhand von "Neu"-Button und Projekt-Elementen |
| View-Erkennung: ProjectDetailView | `E2E_ViewPattern.RecognizeViewsCorrectly()` | Nach Öffnen eines Projekts erkennt `CurrentView()` ProjectDetailView anhand von "ProjektName"-Feld und "AufgabeNeu"-Button |
| View-Erkennung: TaskDetailView | `E2E_ViewPattern.RecognizeViewsCorrectly()` | Nach Erstellen/Öffnen einer Aufgabe erkennt `CurrentView()` TaskDetailView anhand von "EditTitel"-Feld und "Speichern"-Button |
| View-Erkennung: SettingsView | `E2E_ViewPattern.RecognizeViewsCorrectly()` | Nach Navigation zu Einstellungen erkennt `CurrentView()` SettingsView anhand von Tabs |
| Navigation via MenuView | `E2E_ViewPattern.MenuNavigationWorks()` | `view.Menu.NavigateToDashboard()` funktioniert, `view.Menu.NavigateToProjects()` funktioniert, `view.Menu.NavigateToSettings()` funktioniert |
| ForceShow() Navigation | `E2E_ViewPattern.ForceShowNavigatesCorrectly()` | `DashboardView.ForceShow()` navigiert zum Dashboard, `ProjectListView.ForceShow()` navigiert zur Projektliste, `ProjectDetailView.ForceShow()` navigiert zu Projektdetail |
| ForceClose() ohne Rekursion | `E2E_ViewPattern.ForceCloseWithoutRecursion()` | `TaskDetailView.ForceClose(recurseToDashboard: false)` schließt Aufgabendetail, ProjectDetailView wird sichtbar |
| ForceClose() mit Rekursion | `E2E_ViewPattern.ForceCloseWithRecursion()` | `TaskDetailView.ForceClose(recurseToDashboard: true)` schließt Aufgabendetail und ProjectDetailView, navigiert zum Dashboard |
| Dialog-Erkennung: RepositoryAssignDialog | `E2E_ViewPattern.RecognizeDialogsCorrectly()` | Öffne Repository-Zuweisungs-Dialog; `CurrentView()` erkennt RepositoryAssignDialogView |
| Dialog-Erkennung: PluginSelectionDialog | `E2E_ViewPattern.RecognizeDialogsCorrectly()` | Öffne Plugin-Auswahl-Dialog; `CurrentView()` erkennt PluginSelectionDialogView |
| ErrorView-Erkennung | `E2E_ViewPattern.RecognizeErrorViewCorrectly()` | Erzwinge Fehler; `CurrentView()` erkennt ErrorView anhand von "FehlerMeldung"-TextBlock |
| View-Erkennung: Fehlerfall | `E2E_ViewPattern.UnrecognizedViewThrowsDetailedException()` | Falls `CurrentView()` keine Ansicht erkennt, wird `InvalidOperationException` geworfen mit Liste der erwarteten Marker und aktuellen Elementen |

Keine bestehenden E2E-Tests müssen angepasst werden.

## Offene Punkte

Keine. Alle in der Anforderung formulierten Annahmen/Entscheidungspunkte sind im Plan bereits eingearbeitet:

- Haupt-Views zum initialen Abdecken: Dashboard, ProjectList, ProjectDetail, TaskDetail, Settings, FileExplorer, AutonomAufgabeDetail, TodoList (Punkt 1 der Anforderung, als Annahme geklärt)
- `IsVisible`-Semantik: Prüft nur aktive/fokussierte Ansicht (Punkt 2, geklärt)
- `ForceShow()` bei bereits sichtbarer Ansicht: No-Op (Punkt 3, geklärt)
- Fehlerbehandlung in `CurrentView()`: Ausführliche Exception mit Marker-Diagnose (Punkt 4, geklärt)
- Fluent-API: Rückgabe von `this` (Punkt 5, geklärt)
- Namenskonventionen: Suffix `View` (Punkt 6, geklärt)
- Abhängigkeiten: Statische Hilfsmethoden aus `WpfTestBase` nutzen (Punkt 7, geklärt)
- Scope: Ausschließlich E2E-Tests (Punkt 8, geklärt)
- Caching: Nicht erforderlich (Punkt 9, geklärt)
- Kompatibilität: Existierende Tests bleiben funktionsfähig (Punkt 10, geklärt)
