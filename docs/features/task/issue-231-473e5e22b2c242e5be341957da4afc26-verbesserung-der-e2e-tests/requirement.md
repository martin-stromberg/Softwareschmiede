# Anforderungsübersetzung: Verbesserung der E2E-Tests

**Aufgaben-ID:** 473e5e22-b2c2-42e5-be34-1957da4afc26  
**Branch:** task/issue-231-473e5e22b2c242e5be341957da4afc26-verbesserung-der-e2e-tests  
**Erstellt:** 2026-08-25

---

## Fachliche Zusammenfassung

Die E2E-Tests sollen durch die Einführung eines strukturierten **View-Pattern** wartbar und leserlich gemacht werden. Derzeit erfolgt das Auslesen und Steuern von WPF-UI-Elementen ad hoc und uneinheitlich direkt über FlaUI-API aufrufe. Eine gemeinsame `BaseWindowView`-Hierarchie abstrahiert wiederholte UI-Interaktionsmuster (Sichtbarkeitsprüfung, Navigation, Menüzugriff) und bietet spezialisierte `*View`-Klassen für jede Anwendungsansicht. Eine Erweiterungsmethode ermöglicht es Tests, anhand des aktuellen Fensterinhalts automatisch die passende View-Instanz zu erzeugen.

---

## Betroffene Klassen und Komponenten

### Neue Klassen und Interfaces

- **`BaseWindowView(Window wnd)`** — Basisklasse für alle UI-View-Helper
  - `IsVisible: bool` — prüft, ob diese Ansicht gerade im Fenster sichtbar ist
  - `ForceShow(): BaseWindowView` — navigiert zu dieser Ansicht (virtuelle Methode, Overrides in Subklassen)
  - `ForceClose(bool recurseToDashboard): BaseWindowView` — schließt diese Ansicht; optional alle darüber liegenden Ansichten bis zum Dashboard
  - `Menu: MenuView` — Property zur Steuerung des Menüs

- **`MenuView(Window wnd) : BaseWindowView`** — spezialisierte View für Menü-Interaktionen
  - Methoden zum Zugriff auf Menü-Elemente und deren Aktivierung

- **Ansicht-spezifische Subklassen** (Beispiele, nicht abschließend):
  - `DashboardView(Window wnd) : BaseWindowView`
  - `ProjectListView(Window wnd) : BaseWindowView`
  - `ProjectDetailView(Window wnd) : BaseWindowView`
  - `TaskDetailView(Window wnd) : BaseWindowView`
  - `SettingsView(Window wnd) : BaseWindowView`
  - `CliView(Window wnd) : BaseWindowView`
  - weitere je nach Anzahl der Anwendungsansichten

### Erweiterungsmethoden

- **`Window.CurrentView()`** — Erweiterungsmethode für `FlaUI.Core.AutomationElements.Window`
  - Analysiert den aktuellen Fensterinhalt (UI-Struktur, sichtbare Elemente)
  - Gibt die entsprechende `BaseWindowView`-Subklasse-Instanz zurück
  - Wirft `InvalidOperationException`, wenn keine Ansicht erkannt werden kann

### Betroffene bestehende Klassen

- **`WpfTestBase`** — bleibt die Test-Basisklasse und wird ggf. um View-API-Helper ergänzt
  - Bestehende Hilfsmethoden (z. B. `NavigateToProjects()`, `CreateProject()`) können später in die entsprechenden `*View`-Klassen migriert werden, ohne `WpfTestBase` zu brechen

### Tests

- Neue Unit/Integration-Tests für die View-Klassen:
  - Tests für `IsVisible`-Implementierung in jeder View
  - Tests für `ForceShow`-Navigation (wo zutreffend)
  - Tests für `ForceClose` und `recurseToDashboard`-Verhalten
  - Tests für `CurrentView()`-Erkennung verschiedener Ansichtszustände

---

## Implementierungsansatz

### 1. Klassenhierarchie etablieren

- **`BaseWindowView`** im neuen Namespace `Softwareschmiede.Tests.E2E.Views` oder direkter in `E2E` als zentrale Abstraktionsebene
- Jede spezialisierte View erbt von `BaseWindowView` und implementiert:
  - `IsVisible`: Logik zur Prüfung bestimmter UI-Marker dieser Ansicht (z. B. "Speichern"-Button in `ProjectDetailView`)
  - `ForceShow`: Navigation zur Ansicht (z. B. Klick auf Buttons, Menüpfade)
  - View-spezifische Zugriffsmethoden auf UI-Elemente (z. B. `GetProjectName()` in `ProjectDetailView`)

### 2. `CurrentView()`-Erkennung implementieren

- Analyse-Logik in `BaseWindowView` oder separater Klasse (z. B. `ViewRecognizer`)
- Heuristik: nach charakteristischen Elementen einer Ansicht suchen, z. B.:
  - Dashboard: Button "Projekte", Button "Einstellungen"
  - ProjectList: Button "Neu", Project-Kacheln
  - ProjectDetail: Feld "ProjektName", Button "AufgabeNeu"
  - TaskDetail: Feld "EditTitel", Button "Speichern" + Button "Zurück"
  - Settings: Tab "Plugins", weitere Tabs
- Fallback-Verhalten, wenn keine Ansicht erkannt wird (Exception mit Diagnose)

### 3. `MenuView` implementieren

- Zugriff auf Haupt-Navigationsmenü (Top-Level-Buttons Dashboard, Projekte, Einstellungen)
- Methoden zum Navigieren (z. B. `NavigateToDashboard()`, `NavigateToSettings()`)
- ggf. Sub-Menü-Unterstützung (Rechtsklick-Menüs, etc.) je nach Anwendungsanforderung

### 4. Verhalten der Basismethoden detaillieren

#### `IsVisible`

- Prüft, ob ein oder mehrere für diese Ansicht charakteristische UI-Elemente sichtbar und aktiv sind
- Beispiel `ProjectDetailView.IsVisible`: "ProjektName"-Feld und "AufgabeNeu"-Button sichtbar

#### `ForceShow`

- Navigiert zur Ansicht durch eine Kette von UI-Klicks
- **Überload-Beispiel `DashboardView.ForceShow()`**: Findet den "Dashboard"-Button im Menü und klickt ihn
- **Überload-Beispiel `SettingsView.ForceShow()`**: Klickt auf " Einstellungen"-Button oder Menüpfad
- Wartet nach der Navigation auf charakteristische Elemente der Zielansicht (Synchronisation)
- Gibt `this` zurück (Fluent-API-Unterstützung)

#### `ForceClose(bool recurseToDashboard)`

- **`recurseToDashboard = false`**: Schließt die aktuelle Ansicht (z. B. "Zurück"-Button, bei Dialogen "Abbrechen")
- **`recurseToDashboard = true`**: 
  - Schließt die aktuelle Ansicht
  - Prüft, ob eine übergeordnete Ansicht sichtbar wird (z. B. nach Schließen von TaskDetail wird ProjectDetail sichtbar)
  - Falls ja: ruft `ForceClose(recurseToDashboard: true)` auf der übergeordneten Ansicht auf (rekursiv)
  - Stoppt die Rekursion, wenn nur noch Dashboard sichtbar ist
  - Ggf. navigiert explizit zum Dashboard, falls keine weitere Ansicht verfügbar ist
- Gibt `this` zurück

### 5. Integration in Tests

- Tests importieren die View-Klassen statt roher FlaUI-Aufrufe
- Beispiel-Refactoring:

  **Vorher (aktuell in `WpfTestBase`):**
  ```csharp
  var button = WaitForElement(mainWindow, cf => cf.ByName(" Projekte"), Short);
  button.AsButton().Click();
  ```

  **Nachher:**
  ```csharp
  var view = mainWindow.CurrentView();
  var projectListView = view.ForceShow(); // je nach Kontext; oder explizit:
  // var projectListView = new ProjectListView(mainWindow).ForceShow();
  ```

- Bestehende Tests brauchen nicht vollständig umgestellt zu werden, solange `WpfTestBase` parallel verfügbar bleibt (Migration kann iterativ erfolgen)

### 6. Optional: Weitere gemeinsame Basisklassen

- `DialogView(Window wnd) : BaseWindowView` — für Modal-Dialoge (Repository Selection, Plugin Choice, etc.)
- `ErrorView(Window wnd) : BaseWindowView` — für Fehler-Banner-Interaktionen

---

## Konfiguration

**Kein Konfigurationsbedarf.** Das View-Pattern ist eine reine Test-Infrastruktur-Erweiterung ohne produktiven oder benutzergesteuerten Konfigurationsscope.

---

## Offene Fragen

1. **Vollständige Ansichtsliste:** Welche Ansichten sollen initial abgedeckt werden?
   - Sichtbare Views aus UI: Dashboard, ProjectList, ProjectDetail, TaskDetail, Settings, CLI-Panel
   - Dialoge: Repository Selection, Plugin Choice, Delete Confirmation (MessageBox)
   - Fehler-Anzeige: FehlerMeldung-Banner
   - Priorisierung: sollte die Implementierung mit den Haupt-Views (Dashboard, ProjectDetail, TaskDetail) starten?

2. **`IsVisible`-Semantik bei mehreren sichtbaren Views:** Angenommen, TaskDetail ist offen — ist ProjectDetail (dahinter) auch "sichtbar"?
   - **Annahme:** `IsVisible` prüft nur, ob diese Ansicht gerade *aktiv/fokussiert* ist (z. B. das oberste Modal).
   - **Alternative:** Auch überlagerte Views als "teilweise sichtbar" klassifizieren?

3. **`ForceShow` bei bereits sichtbarer Ansicht:** Sollte `ForceShow()` auf einer Ansicht, die bereits sichtbar ist, ein No-Op sein oder trotzdem Navigation durchführen (z. B. State-Reset)?
   - **Annahme:** No-Op — nur Warten auf Synchronisation; Aufrufer können explizit schließen+öffnen, wenn Reset gewünscht.

4. **Fehlerbehandlung:** Wenn `CurrentView()` keine Ansicht erkennt, welche Debug-Info soll geworfen werden?
   - Automation-Baum-Dump? Screenshot? Liste aller sichtbaren UI-Elemente?
   - **Annahme:** Ausführliche Exception mit Liste der erwarteten Marker und aktuell gefundenen Elementen.

5. **Fluent-API vs. klassische Rückgabe:** Sollten `ForceShow()`/`ForceClose()` `this` oder die nächste View zurückgeben?
   - **Annahme:** `this` als default (Fluent-Kette auf der aktuellen View); Aufrufer können explizit `CurrentView()` aufrufen, um zur nächsten View zu wechseln.

6. **Namenskonventionen:** Klassennamens-Suffix `View` vs. `ViewHelper` vs. `PageObject`?
   - **Annahme:** `*View` (z. B. `ProjectDetailView`) ist konsistent mit WPF-Namensgebung und FlaUI-Idiomatic.

7. **Abhängigkeiten von Basis-Navigations-Methoden:** Können `ForceShow()`-Implementierungen bestehende Methoden in `WpfTestBase` nutzen (z. B. `WaitForElement`), oder sollte alles in `BaseWindowView` abstrahiert werden?
   - **Annahme:** `BaseWindowView` erbt nicht von `WpfTestBase`, nutzt aber geschützte statische Hilfsmethoden aus `WpfTestBase` (z. B. `WaitForElement`) oder implementiert Sie lokal.

8. **Produktionscode vs. Test-Code:** Sind View-Klassen ausschließlich für E2E-Tests bestimmt, oder könnten Sie auch für Integration-Tests (z. B. ViewModel-Tests mit manueller UI-Kontrolle) relevant sein?
   - **Annahme:** Ausschließlich E2E-Tests; ggf. später auch für WPF-Integration-Tests.

9. **Performance-Optimierung:** `IsVisible` wird bei `CurrentView()` möglicherweise mehrmals hintereinander geprüft. Sollte die Erkennung gecacht werden?
   - **Annahme:** Kein Cache nötig; eine Erkennung ist in FlaUI-Zeiten fast sofort.

10. **Kompatibilität mit Tests außerhalb dieser Hierarchie:** Müssen bestehende Tests, die nicht refaktoriert werden, weiter funktionieren?
    - **Annahme:** Ja; die neue View-Hierarchie ist additive, es wird keine bestehende `WpfTestBase`-Funktionalität entfernt.
