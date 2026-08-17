# Plan-Review: Split-Button-Muster für IDE-Öffnen-Funktion

## Ergebnis

**Status:** Vollständig umgesetzt

---

## Umgesetzte Planelemente

### Neue Klassen / Komponenten
- [x] `RibbonSplitButton` (UserControl XAML + Code-Behind) — Neue WPF-Komponente mit zwei Buttons angelegt
  - [x] DependencyProperties: `ButtonCommand`, `DropdownCommand`, `CanShowDropdown`, `ButtonIcon`, `ButtonText`, `AutomationName`
  - [x] Haupt-Button (56px breit) mit Icon und Text
  - [x] Dropdown-Button (16px breit, nur sichtbar wenn `CanShowDropdown == true`)
  - [x] Styling (Hover, Pressed, Disabled) analog zu `RibbonLargeButton`

### Änderungen an bestehenden Klassen

#### `TaskDetailViewModel` (ViewModel)
- [x] Eigenschaft `KannIdeAuswaehlen` (`bool`, read-only) — Gibt an, ob Dropdown-Button sichtbar sein soll
- [x] Eigenschaft `VerfuegbareEinstiegspunkte` (`IReadOnlyList<IdeEntryPoint>`, read-only) — Gepufferte Liste der ermittelten Einstiegspunkte
- [x] Kommando `OeffneIdeAuswahlCommand` (`AsyncRelayCommand`) — Triggert Dropdown-Button-Klick
- [x] Methode `OeffneIdeAuswahlAsync(CancellationToken)` (`private`) — Ruft `IdeOeffnenService.OpenRepositoryInIdeAsync` **mit** `waehleEntryPointAsync`-Callback auf
- [x] Methode `waehleEntryPointAsync(IReadOnlyList<IdeEntryPoint>, CancellationToken)` (`private`) — Callback für Einstiegspunkt-Auswahl bei mehreren Treffern
- [x] Hilfsmethode `AktualisiereVerfuegbareEinstiegspunkteAsync(string, CancellationToken)` (`private`) — Ermittelt Einstiegspunkte und aktualisiert `KannIdeAuswaehlen`
- [x] Methode `OeffneIdeAsync(CancellationToken)` — Unverändert; ruft `IdeOeffnenService.OpenRepositoryInIdeAsync` **ohne** Callback auf (Haupt-Button Fallback-Verhalten)

#### `TaskDetailView.xaml` (WPF View)
- [x] UI-Element-Ersetzung (Zeile 180-185) — `<controls:RibbonLargeButton>` wird durch `<controls:RibbonSplitButton>` ersetzt
- [x] Binding: Haupt-Button → `ButtonCommand="{Binding OeffneIdeCommand}"`
- [x] Binding: Dropdown-Button → `DropdownCommand="{Binding OeffneIdeAuswahlCommand}"`
- [x] Binding: Dropdown-Sichtbarkeit → `CanShowDropdown="{Binding KannIdeAuswaehlen}"`
- [x] Icon/Text/AutomationName unverändert

#### `RibbonLargeButton.xaml` (WPF Control)
- [x] Keine Änderungen — Komponente bleibt unverändert

### Unit-Tests
- [x] Test-Klasse `TaskDetailViewModelTests_IdeAuswahl` angelegt
- [x] Test: `OeffneIdeAuswahlCommand_ExecuteAsync_RuftOeffneIdeAuswahlAsyncAuf` — Prüft Kommando-Ausführung und Dialog-Anzeige
- [x] Test: `OeffneIdeAuswahlCommand_CanExecute_WhenKannIdeOeffnenFalse_ReturnsFalse` — CanExecute-Logik
- [x] Test: `KannIdeAuswaehlen_WhenOneEntryPoint_ReturnsFalse` — Property bei 1 Einstiegspunkt = false
- [x] Test: `KannIdeAuswaehlen_WhenMultipleEntryPoints_ReturnsTrue` — Property bei ≥2 Einstiegspunkten = true
- [x] Test: `KannIdeAuswaehlen_WhenNoEntryPoints_ReturnsFalse` — Property bei 0 Einstiegspunkten = false
- [x] Test: `WaehleEntryPointAsync_WithMultipleEntryPoints_ShowsDialogAndReturnsSelected` — Callback zeigt Dialog und gibt gewählten Einstiegspunkt zurück
- [x] Test: `WaehleEntryPointAsync_WithDialogAbort_ReturnsNull` — Callback bei Dialog-Abbruch
- [x] Test: `WaehleEntryPointAsync_UsesDisplayNameInDialog` — DisplayName wird in Dialog angezeigt (falls vorhanden)
- [x] Test: `VerfuegbareEinstiegspunkte_UpdatedAfterOeffneIde` — Property wird nach Aufruf aktualisiert
- [x] Test: `OeffneIdeAuswahlAsync_WithNoEntryPoints_ShowsError` — Fehlerbehandlung ohne Einstiegspunkte

### Bestehende Tests (angepasst / verifiziert)
- [x] Bestehende `TaskDetailViewModelTests` — Tests für `OeffneIdeCommand` funktionieren unverändert (keine Breaking Changes)
- [x] Test: `OeffneIdeCommand_MitEinerSolution_OeffnetOhneDialog` — Haupt-Button öffnet einzelnen Einstiegspunkt direkt
- [x] Test: `OeffneIdeCommand_MitMehrerenSolutions_ZeigtAuswahlDialog` — Haupt-Button zeigt bei mehreren noch Fallback-Dialog (alt)

### E2E-Tests
- [x] Test-Datei `E2E_TaskDetailView_IdeAuswahl.cs` angelegt
- [x] Test: `IdeAuswahl_KeineEinstiegspunkteUndDropdownAbbruch_E2E` — Prüft:
  - Fehlermeldung bei 0 Einstiegspunkten (ohne VS-Code-Fallback)
  - Dropdown-Button unsichtbar bei 0 Einstiegspunkten
  - Dropdown-Button sichtbar bei ≥2 Einstiegspunkten
  - Dropdown-Dialog wird angezeigt bei Klick auf Dropdown-Button
  - Dialog-Abbruch öffnet nichts

---

## Offene Aufgaben

Keine. Alle Planelemente sind vollständig umgesetzt.

---

## Hinweise

### Implementierungsqualität
- Die `RibbonSplitButton` Komponente folgt dem gleichen Styling-Muster wie `RibbonLargeButton` (Hover, Pressed, Disabled States)
- Automations-Names für Accessibility sind korrekt gesetzt: `IdeOeffnen` (Haupt-Button), `IdeOeffnenDropdown` (Dropdown-Button)
- Die Callback-Logik `waehleEntryPointAsync` nutzt `DisplayName` als primären Anzeigewert mit Fallback auf `Path`
- PropertyChanged-Events werden korrekt gefeuert für zwei-Wege-Binding

### Test-Abdeckung
- Unit-Tests decken alle kritischen Pfade ab (1, ≥2, 0 Einstiegspunkte)
- Dialog-Auswahl und Dialog-Abbruch werden getestet
- DisplayName-Nutzung wird explizit verifiziert
- E2E-Tests prüfen die tatsächliche UI-Interaktion mit dem neuen Split-Button

### Abhängigkeiten und Voraussetzungen
- Die Implementierung nutzt bestehende `IdeOeffnenService.OpenRepositoryInIdeAsync()`-Methode mit Callback-Parameter
- `IDialogService.ShowSolutionSelectionDialogAsync()` wird wiederverwendet für die Auswahl
- `PluginSelectionService.ResolveIdePluginAsync()` wird für Plugin-Auflösung genutzt
- Alle bestehenden Services und Interfaces bleiben unverändert

### Konsistenz mit bestehendem Code
- Fehlerbehandlung identisch zu `OeffneIdeAsync` (FehlerMeldung wird gesetzt)
- Logging-Statements vorhanden für Debugging
- Cancellation-Token-Behandlung konsistent mit bestehenden asynchronen Methoden
- PropertyChanged-Notifications folgen dem MVVM-Pattern des Projekts
