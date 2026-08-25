← [Zurück zur Übersicht](index.md)

# E2E-Test-Infrastruktur — API-Referenz

## Namespace

```
Softwareschmiede.Tests.E2E.Views
```

## Basisklasse: BaseWindowView

Abstrakte Basisklasse für alle View-Klassen. Definiert die einheitliche API.

### Properties

#### `Window`

```csharp
public Window Window { get; }
```

Das FlaUI-Hauptfenster, auf das sich diese View bezieht.

#### `IsVisible`

```csharp
public abstract bool IsVisible { get; }
```

Abstrakte Property. `true`, wenn diese Ansicht gerade aktiv/fokussiert ist. Die Prüfung sucht nach charakteristischen UI-Markern der Ansicht.

#### `Menu`

```csharp
public virtual MenuView Menu => new(Window);
```

Gibt eine `MenuView`-Instanz für Zugriff auf das Navigationsmenü zurück. Standard-Implementierung erzeugt eine neue Instanz.

### Methoden

#### `ForceShow()`

```csharp
public abstract BaseWindowView ForceShow();
```

Navigiert zu dieser Ansicht. No-Op, wenn bereits sichtbar (nur Synchronisation). Gibt `this` zurück für Fluent-API.

**Rückgabe:** Diese Instanz (`this`)

**Exceptions:**
- `TimeoutException` — Zielansicht wurde nicht rechtzeitig sichtbar
- `InvalidOperationException` — Während Navigation erscheint Fehlerbanner

#### `ForceClose(bool recurseToDashboard)`

```csharp
public abstract BaseWindowView ForceClose(bool recurseToDashboard);
```

Schließt diese Ansicht.

**Parameter:**
- `recurseToDashboard` — Wenn `true`: schließt auch alle übergeordneten Ansichten bis zum Dashboard; wenn `false`: schließt nur diese Ansicht

**Rückgabe:** Diese Instanz (`this`)

**Exceptions:**
- `TimeoutException` — Ansicht wurde nicht rechtzeitig geschlossen
- `InvalidOperationException` — Während Schließen erscheint Fehlerbanner

### Geschützte Methoden

#### `ElementExists()`

```csharp
protected static bool ElementExists(
    AutomationElement parent, 
    Func<ConditionFactory, ConditionBase> conditionFunc)
```

Prüft, ob ein Matching-Element existiert und sichtbar ist.

**Parameter:**
- `parent` — Element, dessen Teilbaum durchsucht wird
- `conditionFunc` — FlaUI-Suchbedingung

**Rückgabe:** `true`, wenn Element sichtbar gefunden, sonst `false`

#### `WaitForElement()`

```csharp
protected static AutomationElement WaitForElement(
    AutomationElement parent,
    Func<ConditionFactory, ConditionBase> conditionFunc,
    TimeSpan timeout)
```

Wartet, bis ein sichtbares Element gefunden wird. Bricht ab, wenn Fehlerbanner erscheint (und nicht das gesuchte Ziel ist).

**Parameter:**
- `parent` — Element, dessen Teilbaum durchsucht wird
- `conditionFunc` — FlaUI-Suchbedingung
- `timeout` — Maximale Wartezeit

**Rückgabe:** Das gefundene Element

**Exceptions:**
- `TimeoutException` — Element nicht rechtzeitig gefunden
- `InvalidOperationException` — Fehlerbanner während Suche erschienen

#### `WaitUntilGone()`

```csharp
protected static void WaitUntilGone(
    AutomationElement parent,
    Func<ConditionFactory, ConditionBase> conditionFunc,
    TimeSpan timeout)
```

Wartet, bis ein Element verschwunden ist (nicht mehr sichtbar).

**Parameter:**
- `parent` — Element, dessen Teilbaum durchsucht wird
- `conditionFunc` — FlaUI-Suchbedingung
- `timeout` — Maximale Wartezeit

**Exceptions:**
- `TimeoutException` — Element wurde nicht rechtzeitig ausgeblendet

### Timeout-Konstanten

```csharp
protected static readonly TimeSpan Short = ElementWaitHelper.Short;  // 20 Sekunden
protected static readonly TimeSpan Medium = ElementWaitHelper.Medium;  // 15 Sekunden
```

## Spezialisierte View-Klassen

### MenuView : BaseWindowView

Zugriff auf das Navigationsmenü der Anwendung.

#### Methoden

##### `NavigateToDashboard()`

```csharp
public MenuView NavigateToDashboard()
```

Navigiert zum Dashboard über Menü-Button.

**Rückgabe:** Diese Instanz

**Exceptions:**
- `TimeoutException` — Dashboard-Button nicht gefunden oder klickbar

##### `NavigateToProjects()`

```csharp
public MenuView NavigateToProjects()
```

Navigiert zur Projektliste über Menü-Button " Projekte".

**Rückgabe:** Diese Instanz

##### `NavigateToSettings()`

```csharp
public MenuView NavigateToSettings()
```

Navigiert zu Einstellungen über Menü-Button.

**Rückgabe:** Diese Instanz

### DashboardView : BaseWindowView

Dashboard-Ansicht. Erkannt über Navigationsbuttons (Projekte, Einstellungen).

#### Methoden

##### `IsVisible`

Prüft, ob Navigationsbuttons sichtbar sind.

##### `ForceShow()`

No-Op (Dashboard ist im Ausgangszustand immer erreichbar).

### ProjectListView : BaseWindowView

Projektlisten-Ansicht. Erkannt über "Neu"-Button und Projekt-Elemente.

#### Methoden

##### `CreateProject(string projectName)`

```csharp
public void CreateProject(string projectName)
```

Erstellt ein neues Projekt über Dialog.

**Parameter:**
- `projectName` — Name des neuen Projekts

##### `OpenProject(string projectName)`

```csharp
public void OpenProject(string projectName)
```

Öffnet ein Projekt. Wechselt zu `ProjectDetailView`.

**Parameter:**
- `projectName` — Name des zu öffnenden Projekts

**Exceptions:**
- `TimeoutException` — Projekt nicht gefunden oder nicht klickbar

### ProjectDetailView : BaseWindowView

Projektdetail-Ansicht. Erkannt über "ProjektName"-Feld und "AufgabeNeu"-Button.

#### Methoden

##### `CreateTask()`

```csharp
public void CreateTask()
```

Erstellt eine neue Aufgabe. Wechselt zu `TaskDetailView`.

**Exceptions:**
- `TimeoutException` — "AufgabeNeu"-Button nicht gefunden

##### `DeleteProject()`

```csharp
public void DeleteProject()
```

Löscht das aktuelle Projekt. Erfordert Bestätigung in Dialog.

### TaskDetailView : BaseWindowView

Aufgabendetail-Ansicht. Erkannt über "EditTitel"-Feld und "Speichern"-Button.

#### Methoden

##### `GetTaskTitle()`

```csharp
public string GetTaskTitle()
```

Gibt den aktuellen Titel der Aufgabe zurück.

**Rückgabe:** Aufgabentitel als String

##### `SetTaskTitle(string title)`

```csharp
public void SetTaskTitle(string title)
```

Setzt den Aufgabentitel.

**Parameter:**
- `title` — Neuer Aufgabentitel

##### `SaveTask()`

```csharp
public void SaveTask()
```

Speichert die aktuelle Aufgabe.

### SettingsView : BaseWindowView

Einstellungen-Ansicht. Erkannt über Einstellungs-Tabs.

#### Methoden

##### `SwitchTab(string tabName)`

```csharp
public void SwitchTab(string tabName)
```

Wechselt zu einem anderen Tab in den Einstellungen.

**Parameter:**
- `tabName` — Name des Tabs (z. B. "Plugins")

### ErrorView : BaseWindowView

Fehler-Banner-View. Wird angezeigt, wenn ein Fehler in der Anwendung auftritt.

#### Methoden

##### `GetErrorMessage()`

```csharp
public string GetErrorMessage()
```

Gibt den Text des Fehlerbaanners zurück.

**Rückgabe:** Fehlermeldung als String

### DialogView : BaseWindowView

Abstrakte Basisklasse für modale Dialoge. Unterscheidet sich von Haupt-Views in der Fenster-Behandlung.

### Dialogs (DialogView-Subklassen)

#### RepositoryAssignDialogView

Dialog für Repository-Zuweisungsverwaltung.

##### `SelectRepository(string repositoryName)`

```csharp
public void SelectRepository(string repositoryName)
```

Wählt ein Repository aus der Liste.

##### `Confirm()`

```csharp
public void Confirm()
```

Bestätigt die Auswahl und schließt den Dialog.

#### PluginSelectionDialogView

Dialog für KI-Plugin-Auswahl.

##### `SelectPlugin(string pluginName)`

```csharp
public void SelectPlugin(string pluginName)
```

Wählt ein Plugin aus der Liste.

##### `Confirm()`

```csharp
public void Confirm()
```

Bestätigt die Auswahl und schließt den Dialog.

#### DeleteConfirmationDialogView

Dialog für Lösch-Bestätigung (native Windows MessageBox).

##### `Confirm()`

```csharp
public void Confirm()
```

Bestätigt die Löschung.

##### `Cancel()`

```csharp
public void Cancel()
```

Bricht die Löschung ab.

## Erweiterungsmethoden

### WindowExtensions

#### `CurrentView()`

```csharp
public static BaseWindowView CurrentView(this Window window)
```

Erkennt anhand charakteristischer UI-Marker die aktuell aktive Ansicht und gibt die passende `BaseWindowView`-Subklasse-Instanz zurück.

**Parameter:**
- `window` — FlaUI-Hauptfenster der Anwendung

**Rückgabe:** Erkannte View-Instanz

**Exceptions:**
- `InvalidOperationException` — Keine bekannte Ansicht erkannt; Exception-Nachricht enthält Diagnose (erwartete vs. gefundene Marker)

## Verwendungsbeispiele

### Ansicht erkannt automatisch

```csharp
var view = mainWindow.CurrentView();
if (view is ProjectListView projectList)
{
    projectList.CreateProject("MeinProjekt");
}
```

### Navigation mit Fluent-API

```csharp
new ProjectListView(mainWindow)
    .ForceShow()
    .OpenProject("MeinProjekt");
```

### Schließen mit Rekursion

```csharp
var taskDetail = mainWindow.CurrentView() as TaskDetailView;
taskDetail?.ForceClose(recurseToDashboard: true);  // Schließt auch Projektdetail bis zum Dashboard
```

### Fehlerbehandlung

```csharp
try
{
    var view = mainWindow.CurrentView();
    view.ForceShow();
}
catch (TimeoutException ex)
{
    Assert.Fail($"View nicht rechtzeitig sichtbar: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Assert.Fail($"Fehler während Navigation: {ex.Message}");
}
```
