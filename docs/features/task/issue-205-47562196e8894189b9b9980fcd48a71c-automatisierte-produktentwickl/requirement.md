# Technische Anforderungsübersetzung: UI-Integration Autonomer Aufgaben

## Fachliche Zusammenfassung

Der bisherige separate Dialog für Autonome Aufgaben (`AutonomAufgabeDetailDialog`) soll aus dem Fenster-Lifecycle genommen und sein Inhalt als neue Registerkarte "Automatisierung" in die bestehende `TaskDetailView` integriert werden. Die drei Aktionsbuttons "Start", "Stop", "Resume" sollen vom Dialog-Fenster in das Ribbon-Menü der Aufgaben-Detailansicht (in die bestehende Gruppe "Autonome Aufgabe") migriert werden.

## Betroffene Klassen und Komponenten

### UI-Komponenten / Views
- **`TaskDetailView.xaml`** — Neue Registerkarte "Automatisierung" hinzufügen neben Info/CLI/Diff/Dateien/PR/Todos
- **`AutonomAufgabeDetailView.xaml`** — Bleibt erhalten, wird aber als UserControl in die neue Registerkarte eingebettet (nicht mehr in eigenem Window)
- **`AutonomAufgabeDetailDialog.xaml` / `AutonomAufgabeDetailDialog.xaml.cs`** — Wird entfernt (oder nur noch als Fallback beibehalten)

### ViewModels
- **`TaskDetailViewModel.cs`** — Muss erweitert werden:
  - Neuer Eintrag im `DetailAnsicht` Enum für "Automatisierung" (aktuell: Info, Cli, Diff, Dateibrowser, PullRequests, Todos)
  - Neue Property zur Verwaltung, ob die "Automatisierung"-Ansicht sichtbar/verfügbar ist
  - Neue Property/Command für die Umschaltung zur "Automatisierung"-Registerkarte (wie `InfoViewCommand`, `CliViewCommand`, etc.)
  - Integration einer `AutonomAufgabeDetailViewModel`-Instanz als Property (wenn eine Autonome Aufgabe vorhanden ist)
  - Weitergabe der Ribbon-Commands (Start/Stop/Resume) an die `AutonomAufgabeDetailViewModel`

- **`AutonomAufgabeDetailViewModel.cs`** — Bleibt weitgehend erhalten:
  - `StartCommand`, `StopCommand`, `ResumeCommand` werden vom Ribbon in `TaskDetailView` gebunden statt vom Dialog
  - Keine Breaking Changes nötig

### Ribbon-Menü (TaskDetailView.xaml)
- **Gruppe "Autonome Aufgabe"** (Zeilen 190–200) — Wird erweitert:
  - Bisheriger "Autonome Aufgabe starten" Button bleibt (initialisiert neue Aufgabe)
  - Neue Buttons "Start", "Stop", "Resume" hinzufügen (gebunden an `AutonomAufgabeDetailViewModel` Commands)
  - Diese Buttons sollten nur sichtbar sein, wenn eine Autonome Aufgabe aktiv ist (`Visibility`-Binding)

### Services
- **`AutonomAufgabeStartService.cs`** — Muss angepasst werden:
  - Statt `_dialogService.ShowAutonomAufgabeDetailAsync(...)` (öffnet separaten Dialog) müssen die neuen Daten in die `TaskDetailViewModel` gespeichert werden
  - Entweder: `TaskDetailViewModel` erhält eine neue öffentliche Methode `ZeigAutonomAufgabeDetailAsync(...)`, die die neue Registerkarte anzeigt
  - Oder: `AutonomAufgabeStartService` triggert ein Event/Callback, das `TaskDetailViewModel` mitteilt, zur "Automatisierung"-Registerkarte zu wechseln

- **`IDialogService` / `WpfDialogService`** — `ShowAutonomAufgabeDetailAsync()` kann entfernt/deprecated werden

### Datenmodell (Falls zutreffend)
- Keine neuen Entities notwendig; `AutonomAufgabeKonfiguration` und `AutonomAufgabeDetailViewModel` bleiben unverändert

### Tests
- **`TaskDetailViewModelTests`** — Neue Tests für:
  - Sichtbarkeit der "Automatisierung"-Registerkarte abhängig von Autonomer Aufgabe
  - Umschaltung zur neuen Registerkarte
  - Ribbon-Commands (Start/Stop/Resume) gebunden an `AutonomAufgabeDetailViewModel`
- **`AutonomAufgabeDetailViewModelTests`** — Bestehende Tests sollten weiterhin grün werden

## Implementierungsansatz

### 1. TaskDetailView (XAML-Änderungen)
- In der Tab-Navigation (Zeile 273 ff.) neue Button "Automatisierung" hinzufügen (analog zu "Info", "CLI", etc.)
- In Grid.Row="1" neue ScrollViewer/Container für die "Automatisierung"-Ansicht einfügen, der `AutonomAufgabeDetailView` hostet, wenn aktiv
- Im Ribbon (Gruppe "Autonome Aufgabe", Zeile 190–200) die drei neuen Buttons ergänzen, mit `Visibility`-Binding auf `HasAutonomAufgabe` oder ähnlich

### 2. TaskDetailViewModel (C#-Änderungen)
- `DetailAnsicht` Enum um `Automatisierung` erweitern
- Property `AutonomAufgabeDetailViewModel? { get; private set; }` hinzufügen
- Property `IsAutomatisierungViewSelected { get; }` zur Sichtbarkeitskontrolle in der View
- Property `ShowAutomatisierungPanel { get; }` (true, wenn eine Autonome Aufgabe vorhanden)
- Command `AutomatisierungViewCommand` hinzufügen (setzt `_ausgewaehlteAnsicht = DetailAnsicht.Automatisierung`)
- Neue (interne) Methode `SetzeAutonomAufgabeDetailViewAsync(AutonomAufgabeDetailViewModel vm)` zum Setzen des ViewModels und Umschalten der Ansicht

### 3. AutonomAufgabeStartService (Anpassung)
- Statt `await _dialogService.ShowAutonomAufgabeDetailAsync(detailVm, ct);`
- Neuen Mechanismus verwenden, um `TaskDetailViewModel` mitzuteilen, dass die Autonome Aufgabe angezeigt werden soll:
  - **Option A:** `AutonomAufgabeStartService` erhält eine neue Dependency (z. B. `Action<AutonomAufgabeDetailViewModel>` oder ein Event)
  - **Option B:** `TaskDetailViewModel` wird direkt übergeben und `SetzeAutonomAufgabeDetailViewAsync` wird aufgerufen
  - **Option C:** Ein neuer Service/Event-Handler coordinated die Integration (z. B. `AutonomAufgabeIntegrationService`)

### 4. Ribbon-Buttons binden
- Neue Buttons in der "Autonome Aufgabe"-Gruppe:
  ```xaml
  <controls:RibbonLargeButton ButtonIcon="▶" ButtonText="Start" 
                              AutomationName="AutonomAufgabeStart"
                              ButtonCommand="{Binding AutonomAufgabeDetailViewModel.StartCommand}"
                              Visibility="{Binding ShowAutomatisierungPanel, ...}" />
  ```
  - Analog für Stop und Resume

### 5. Dialog-Fenster entfernen oder behalten
- **Vollständiges Entfernen:** `AutonomAufgabeDetailDialog`, `AutonomAufgabeDetailDialog.xaml.cs` und `ShowAutonomAufgabeDetailAsync` aus `IDialogService`/`WpfDialogService` löschen
- **Oder:** Als Fallback behalten für bestimmte Szenarien (z. B. standalone Aufruf), aber Standardfall ist die Registerkarte

## Konfiguration

Keine zusätzliche Konfiguration notwendig. Die Sichtbarkeit der "Automatisierung"-Registerkarte und deren Buttons wird zur Laufzeit anhand der Präsenz einer initiialisierten Autonomen Aufgabe gesteuert.

## Offene Fragen

1. **Zeitpunkt der Integration:** Sollen die Start/Stop/Resume Buttons im Ribbon auch dann verfügbar sein, bevor der Benutzer die "Automatisierung"-Registerkarte öffnet? Oder nur sichtbar, wenn zur Registerkarte gewechselt wird?

2. **Fenster vs. Registerkarte – Verhalten:** Wenn eine Autonome Aufgabe läuft und der Benutzer die Aufgabe in der Liste wechselt (zu einer anderen Aufgabe), soll die laufende Autonome Aufgabe weiterhin in der Registerkarte angezeigt werden, oder die "Automatisierung"-Registerkarte ausgeblendet werden?

3. **Dialog-Fallback:** Sollen `AutonomAufgabeDetailDialog` und `ShowAutonomAufgabeDetailAsync` komplett entfernt werden, oder als Fallback erhalten bleiben (z. B. für spätere Anforderungen)?

4. **Abhängigkeits-Auflösung:** Wie soll `TaskDetailViewModel` Zugriff auf `AutonomAufgabeDetailViewModel` erhalten?
   - Über Dependency Injection (neuer Parameter im Konstruktor, lazy-loaded)?
   - Oder wird es von `AutonomAufgabeStartService` direkt gesetzt?

5. **Initialisierung beim Laden:** Wenn die Aufgabe geladen wird, soll auch automatisch prüft werden, ob eine Autonome Aufgabe existiert, und diese angezeigt werden? Oder nur, wenn der Benutzer den "Starten"-Button klickt?

6. **Responsive Design:** Die AutonomAufgabeDetailView hat aktuell Margin="24" und verschiedene Größen. Passt diese noch, wenn sie als Registerkarte eingebettet ist, oder muss die Spacing angepasst werden?
