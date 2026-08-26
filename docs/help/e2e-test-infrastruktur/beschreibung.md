← [Zurück zur Übersicht](index.md)

# E2E-Test-Infrastruktur — Beschreibung

## Zweck

Das View-Pattern ist eine Abstraktionsschicht über FlaUI für End-to-End-Tests. Es ermöglicht es Tests, die Benutzeroberfläche auf einem höheren Abstraktionsniveau zu steuern und zu prüfen, statt direkt mit UI-Elementen umzugehen.

**Problem, das gelöst wird:**
- E2E-Tests sind schwer zu warten: direkte FlaUI-Aufrufe verstreut in Testmethoden, wiederholte Element-Suche und Wartelogik in jedem Test
- UI-Umgestaltungen erfordern Änderungen in vielen Tests
- Tests sind schwer zu lesen: viel „Wie" (Element-Handle, FlaUI-API), wenig „Was" (Benutzeraktion)

## Funktionsweise

### Klassenhierarchie

Jede Ansicht der Anwendung (Dashboard, Projektliste, Projektdetail, Aufgabendetail, Einstellungen, Dialoge, etc.) wird durch eine spezialisierte `*View`-Klasse repräsentiert, die von `BaseWindowView` erbt.

### BaseWindowView — Basisklasse

Definiert die einheitliche API für alle Views:

- **`IsVisible`** (Property): Prüft, ob diese Ansicht gerade aktiv/fokussiert ist. Die Prüfung sucht nach charakteristischen UI-Elementen der Ansicht (z. B. für `ProjectDetailView`: das "ProjektName"-Feld und der "AufgabeNeu"-Button).

- **`ForceShow()`** (Methode): Navigiert zu dieser Ansicht. Ist die Ansicht bereits sichtbar, ist es ein No-Op (nur Synchronisation). Nicht sichtbare Ansichten werden durch UI-Klicks oder Menü-Navigation aktiviert.

- **`ForceClose(bool recurseToDashboard)`** (Methode):
  - `recurseToDashboard = false`: Schließt die aktuelle Ansicht (z. B. "Zurück"-Button, Dialog-Abbrechen).
  - `recurseToDashboard = true`: Schließt die aktuelle Ansicht und alle übergeordneten Ansichten rekursiv, bis nur noch das Dashboard sichtbar ist.

- **`Menu`** (Property): Liefert eine `MenuView`-Instanz für Zugriff auf die Navigationsmenü-Elemente.

### Spezialisierte View-Klassen

Jede View-Klasse implementiert:
- `IsVisible`: Ansicht-spezifische Erkennungsheuristik
- `ForceShow()`: Ansicht-spezifische Navigationslogik
- `ForceClose()`: Ansicht-spezifische Schließungslogik
- Zusätzliche Hilfsmethoden für ansicht-spezifische Interaktionen (z. B. `ProjectDetailView.CreateTask()`, `TaskDetailView.GetTaskTitle()`)

Beispiele:
- `MenuView`: Zugriff auf Navigationsmenü, Methoden `NavigateToDashboard()`, `NavigateToProjects()`, `NavigateToSettings()`
- `DashboardView`: Erkannt über Navigationsbuttons, No-Op-`ForceShow()`
- `ProjectListView`: Erkannt über "Neu"-Button und Projekt-Elemente
- `ProjectDetailView`: Erkannt über "ProjektName"-Feld und "AufgabeNeu"-Button, Hilfsmethode `CreateTask()`
- `TaskDetailView`: Erkannt über "EditTitel"-Feld und "Speichern"-Button, Hilfsmethode `GetTaskTitle()`, `SetTaskTitle()`
- `SettingsView`: Erkannt über Einstellungs-Tabs
- Dialoge (`RepositoryAssignDialogView`, `PluginSelectionDialogView`, etc.): Erkannt über Dialog-spezifische Elemente, haben eigene Fenster-Handles

### Automatische View-Erkennung

`Window.CurrentView()` ist eine Erweiterungsmethode für FlaUI `Window`, die:
1. Den UI-Automation-Baum des Hauptfensters durchsucht
2. Für jede registrierte View-Klasse prüft, ob die charakteristischen Marker sichtbar sind
3. Die erste übereinstimmende View-Instanz zurückgibt
4. Falls keine Ansicht erkannt wird: `InvalidOperationException` mit detaillierter Diagnose (erwartete vs. gefundene Elemente)

**Prüfreihenfolge:**
1. Modale Dialoge (haben eigene Fenster-Handles)
2. Fehlerbanner (`ErrorView`)
3. Spezielle Panel-Views (Datei-Explorer, To-Do-Liste)
4. Aufgabendetail
5. Projektdetail
6. Projektliste
7. Einstellungen
8. Autonome Aufgabendetail
9. Fallback: Dashboard

## Beispiele

### Vorher (direkter FlaUI-Zugriff)

```csharp
// Element suchen und klicken
var button = WaitForElement(mainWindow, cf => cf.ByName(" Projekte"), Short);
button.AsButton().Click();

// Auf nächste Ansicht warten
WaitForElement(mainWindow, cf => cf.ByName("Neu"), Short);

// Projekt öffnen: direkter Element-Zugriff, Element-Namen müssen bekannt sein
var projectButton = WaitForElement(mainWindow, 
    cf => cf.ByName("MeinProjekt"), Short);
projectButton.Click();
```

### Nachher (View-Pattern)

```csharp
// Ansicht erkannt automatisch
var view = mainWindow.CurrentView();

// Navigation über View-API
view.ForceShow()  // Falls noch nicht auf Projektseite, navigiert dorthin
    .Menu.NavigateToProjects();  // Oder: neue ProjectListView(mainWindow).ForceShow()

// View erkannt automatisch
var projectList = mainWindow.CurrentView() as ProjectListView;

// Ansicht-spezifische Hilfsmethode
projectList.OpenProject("MeinProjekt");
```

## Fluent-API

`ForceShow()` und `ForceClose()` geben `this` zurück, ermöglichen also Kettenaufrufe:

```csharp
view.ForceShow()
    .Menu.NavigateToDashboard();
    
new TaskDetailView(mainWindow)
    .ForceClose(recurseToDashboard: true);
```

## Fehlerbehandlung

Wenn `CurrentView()` keine Ansicht erkennt oder wenn `WaitForElement()` während des Wartens auf ein Element ein Fehlerbanner findet:

- `InvalidOperationException` mit detaillierter Nachricht, die auflistet:
  - Welche Ansichten gesucht wurden
  - Welche Marker erwartet wurden
  - Welche UI-Elemente tatsächlich sichtbar sind
  - Falls Fehlerbanner: dessen Inhalt

Dies ermöglicht schnelle Fehlerdiagnose in Tests.

## Integration mit WpfTestBase

Das View-Pattern ist **optional und additiv**:
- Bestehende Tests mit direktem `WpfTestBase`-Zugriff funktionieren weiter
- Neue Tests können auf View-Pattern migrieren
- Beide Ansätze können im selben Testprojekt koexistieren
