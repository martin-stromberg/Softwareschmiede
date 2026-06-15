# ViewModel und Views

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| `AufgabeId` | `Guid` | Die ID der angezeigten Aufgabe |
| `Aufgabe` | `Aufgabe?` | Die geladene Aufgabe |
| `AufgabeTitel` | `string` | Titel der Aufgabe (berechnete Eigenschaft) |
| `AufgabeStatus` | `AufgabeStatus` | Status der Aufgabe (berechnete Eigenschaft) |
| `IsLoading` | `bool` | Gibt an, ob Daten geladen werden |
| `FehlerMeldung` | `string?` | Fehlermeldung bei Fehlern |
| `IsCliRunning` | `bool` | Gibt an, ob ein CLI-Prozess läuft |
| `KannCliStarten` | `bool` | Gibt an, ob ein CLI-Prozess gestartet werden kann (berechnete Eigenschaft) |
| `KannCliStoppen` | `bool` | Gibt an, ob der laufende CLI-Prozess gestoppt werden kann (berechnete Eigenschaft) |
| `SelectedKiPluginPrefix` | `string?` | Gewähltes KI-Plugin (Prefix) |
| `OptionalCliParameters` | `string?` | Optionale Parameter für den CLI-Start |
| `EmbeddedWindowHandle` | `IntPtr` | Handle des eingebetteten CLI-Fensters (für ProcessWindowHost) |
| `Protokolleintraege` | `ObservableCollection<Protokolleintrag>` | Protokolleinträge der Aufgabe |
| `VerfuegbareKiPlugins` | `ObservableCollection<string>` | Verfügbare KI-Plugin-Prefixe |

**Commands:**
- `LadenCommand` — Lädt die Aufgabe
- `CliStartenCommand` — Startet den CLI-Prozess (CanExecute: `KannCliStarten`)
- `CliStoppenCommand` — Stoppt den CLI-Prozess (CanExecute: `KannCliStoppen`)
- `StatusGestartetSetzenCommand` — Setzt den Status auf `Gestartet`
- `AufgabeAbschliessenCommand` — Schließt die Aufgabe ab (Status: Beendet)

**Events:**
- `CliProzessGestartet` — Wird ausgelöst, wenn ein CLI-Prozess gestartet wurde und das Handle verfügbar ist

**Abhängigkeiten:**
- `AufgabeService` — Für CRUD und Status-Operationen
- `ProtokollService` — Für das Laden von Protokolleinträgen
- `KiAusfuehrungsService` — Für CLI-Start/Stop und Status-Überwachung
- `EntwicklungsprozessService` — Für Aufgaben-Abschluss
- `PluginSelectionService` — Für das Laden verfügbarer KI-Plugins

**Abonnierte Events:**
- `KiAusfuehrungsService.CliProcessStatusChanged` — Aktualisiert `IsCliRunning`

**Bemerkungen:**
- Das ViewModel ruft `LadenAsync()` auf, wenn `AufgabeId` gesetzt wird
- Es lädt sowohl Aufgaben-Details (`GetDetailAsync`) als auch Protokolleinträge
- `IsCliRunning` wird basierend auf der Rückgabe von `KiAusfuehrungsService.IsRunning()` gesetzt
- Commands für CLI-Start und -Stop sind implementiert
- Der Befehl `StatusGestartetSetzenCommand` nutzt `AufgabeService.SetStatusAsync(AufgabeStatus.Gestartet)`
- Der Befehl `AufgabeAbschliessenCommand` nutzt `EntwicklungsprozessService.AbschliessenAsync()`


## `TaskDetailView` (XAML)
Datei: `src/Softwareschmiede.App/Views/TaskDetailView.xaml`

**Layout-Struktur:**
1. **Header (Grid.Row=0):**
   - Zeigt Aufgabentitel und Status
   - Enthält KI-Plugin-Dropdown (sichtbar wenn CLI nicht läuft)
   - Buttons: "▶ CLI Starten", "■ Stoppen", "✓ Abschließen"

2. **Fehler-Border (Grid.Row=1):**
   - Zeigt Fehlermeldungen

3. **Hauptinhalt (Grid.Row=2):**
   - **Eingebettetes CLI-Fenster:** `ProcessWindowHost` (sichtbar wenn `IsCliRunning`)
   - **Protokoll-Ansicht:** ListBox mit Protokolleinträgen (sichtbar wenn CLI nicht läuft)

4. **Statusleiste (Grid.Row=3):**
   - Zeigt aktuellen Status

**Bindings:**
- Titel: `{Binding AufgabeTitel}`
- Status: `{Binding AufgabeStatus}`
- CLI-Plugin-Dropdown: `{Binding VerfuegbareKiPlugins}`, `{Binding SelectedKiPluginPrefix}`
- CLI-Fenster-Handle: `{Binding EmbeddedWindowHandle}`
- CLI-Sichtbarkeit: `{Binding IsCliRunning, Converter={StaticResource BoolToVisibilityConverter}}`
- Protokoll-ListBox: `{Binding Protokolleintraege}`

**Bekannte UI-Elemente:**
- `ProcessWindowHost` Control für Fenstereinbettung
- Converter: `BoolToVisibilityConverter`, `InverseBoolToVisibilityConverter`, `NullOrEmptyToVisibilityConverter`
- Brushes: `BackgroundBrush`, `SurfaceBrush`, `BorderBrush`, `PrimaryTextBrush`, `SecondaryTextBrush`, `AccentBrush`, `ErrorBrush`, `SuccessBrush`

**Bemerkungen:**
- Es gibt keine Ribbon-Menü-Struktur implementiert
- Es gibt keine Status-abhängigen Content-Switching (z.B. für Status Neu)
- Es gibt keine Diff-Ansicht für Status Beendet
- Es gibt keinen Toggle-Button zwischen Info-Ansicht und CLI-Ansicht
