# View-Pattern für E2E-Tests

Dieser Namespace (`Softwareschmiede.Tests.E2E.Views`) stellt eine Abstraktionsschicht über der
rohen FlaUI-API bereit. Sie ist additiv: bestehende E2E-Tests, die direkt `WpfTestBase`-Hilfsmethoden
nutzen, funktionieren unverändert weiter. Neue Tests können wahlweise das View-Pattern nutzen.

## Grundidee

Jede Anwendungsansicht wird durch eine `*View`-Klasse repräsentiert, die von `BaseWindowView`
(Haupt-Views) oder `DialogView` (modale Dialoge) erbt und vier Dinge kapselt:

- `IsVisible` — ist diese Ansicht gerade aktiv/sichtbar?
- `ForceShow()` — navigiere zu dieser Ansicht (No-Op, falls schon sichtbar)
- `ForceClose(recurseToDashboard)` — schließe diese Ansicht, optional bis zum Dashboard durchgereicht
- `Menu` — Zugriff auf das Navigationsmenü (`MenuView`)

Die Erweiterungsmethode `Window.CurrentView()` (`WindowExtensions.cs`) erkennt anhand
charakteristischer UI-Marker automatisch, welche View gerade aktiv ist, und gibt die passende
Instanz zurück. Wird keine Ansicht erkannt, wirft sie eine `InvalidOperationException` mit einer
Liste der erwarteten Marker und der aktuell sichtbaren Elemente.

## Beispiel

**Vorher (direkte FlaUI-Aufrufe über `WpfTestBase`):**

```csharp
var button = WaitForElement(mainWindow, cf => cf.ByName(" Projekte"), Short);
button.AsButton().Click();
CreateProject(mainWindow, "Mein Projekt");
OpenProject(mainWindow, "Mein Projekt");
```

**Nachher (View-Pattern):**

```csharp
var projectListView = new ProjectListView(mainWindow).ForceShow();
var projectDetailView = projectListView.CreateProject("Mein Projekt").OpenProject("Mein Projekt");

Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
```

## Naming-Konventionen

- Klassen-Suffix `View` für Haupt-Views (z. B. `ProjectDetailView`), konsistent mit
  `src/Softwareschmiede.App/Views/`.
- Dialog-Klassen erben von `DialogView` und liegen unter `Views/Dialogs/`.
- Konstruktor nimmt stets das FlaUI-Hauptfenster (`Window`) entgegen - auch Dialog-Klassen, die ihr
  eigenes Fenster über `Window.Automation.GetDesktop()` suchen.

## Verhalten von `ForceShow()` / `ForceClose()`

- `ForceShow()` ist ein No-Op, wenn die Ansicht bereits sichtbar ist.
- Beide Methoden geben stets `this` zurück (Fluent-API auf der aktuellen View). Für einen
  Ansichtswechsel ruft der Aufrufer explizit `mainWindow.CurrentView()` auf oder nutzt eine
  Hilfsmethode mit passendem Rückgabetyp (z. B. `ProjectListView.OpenProject(name)` liefert die
  geöffnete `ProjectDetailView`).
- `ForceClose(recurseToDashboard: true)` schließt rekursiv übergeordnete Ansichten, bis das
  Dashboard sichtbar ist.

## Hinweise für neue View-Klassen

- View-Klassen erben bewusst nicht von `WpfTestBase` (getrennte Schichten). Benötigte
  Hilfsmethoden (`WaitForElement`, `WaitUntilGone`, …) sind lokal in `BaseWindowView` implementiert.
- `WaitForElement`/`ElementExists` ignorieren Treffer, die zwar im Automation-Baum vorhanden, aber
  nicht sichtbar (`IsOffscreen`) sind - wichtig, da z. B. eine fensterumfassende `TaskDetailView`
  nach dem Navigieren weg davon verdeckt im Baum verbleiben kann.
- Dialog-Sichtbarkeitsprüfungen suchen gezielt nach `ControlType.Window` mit passendem Titel auf dem
  Desktop, nicht nur nach dem Namen - sonst könnten gleichnamige Elemente irgendwo im Inhalt eines
  anderen offenen Fensters fälschlich als Treffer zählen.
