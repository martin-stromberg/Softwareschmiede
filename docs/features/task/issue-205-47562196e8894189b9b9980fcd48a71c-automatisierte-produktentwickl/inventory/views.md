# Views (XAML)

## `TaskDetailView`

Datei: `src/Softwareschmiede.App/Views/TaskDetailView.xaml`

Haupt-UserControl für die Aufgaben-Detailansicht. Struktur:

### Ribbon-Menü (Grid.Row="0", Zeilen 16–245)

- **Gruppe "Navigation"** (Zeilen 27–35)
  - Button "Zurück" → `ZurueckCommand`
  
- **Gruppe "Aufgabe"** (Zeilen 37–68)
  - Button "Speichern" → `SpeichernCommand`
  - Button "Löschen" → `LoeschenCommand`
  - Button "Starten" → `StartenCommand`
  - Button "Beenden" → `AufgabeAbschliessenCommand`
  - TextBlock mit Badge für offene To-Dos

- **Gruppe "CLI"** (Zeilen 70–145) — sichtbar wenn `IsCliViewSelected`
  - Plugin-Auswahl, CLI-Start, CLI-Stop, Promptvorlage-Verwaltung

- **Gruppe "Dateien"** (Zeilen 147–170) — sichtbar wenn `IsFileExplorerViewSelected`
  - Datei-Ansichten (Standard, Vergleich, Aktualisieren, Öffnen)

- **Gruppe "Werkzeuge"** (Zeilen 172–188)
  - Arbeitsverzeichnis öffnen, IDE öffnen (Split-Button mit Dropdown)

- **Gruppe "Autonome Aufgabe"** (Zeilen 190–200) — **zu erweitern**
  - Aktuell: Button "Autonome Aufgabe starten" → `AutonomAufgabeInitialisierenCommand`
  - **Zu erweitern:** Buttons für Start/Stop/Resume (sichtbar, wenn `ShowAutomatisierungPanel`)

- **Gruppe "Issue"** (Zeilen 202–223) — sichtbar wenn `ShowIssueGroup`
  - Issue anlegen, Issue zuweisen, Issue öffnen

- **Gruppe "Pull Request"** (Zeilen 225–241) — sichtbar wenn `ShowPullRequestPanel`
  - PR erstellen, PRs aktualisieren

### Fehler-Banner (Grid.Row="1", Zeilen 247–255)

Border mit `ErrorBrush`-Hintergrund, zeigt `FehlerMeldung` wenn nicht leer.

### Haupt-Inhaltsbereich (Grid.Row="2", Zeilen 257–583)

#### Ansicht-Buttons (StackPanel, Zeilen 264–312)

Buttons zum Umschalten zwischen verschiedenen Ansichten:
- "Info" → `InfoViewCommand`
- "CLI" → `CliViewCommand` (sichtbar wenn `ShowCliPanel`)
- "Diff" → `DiffViewCommand` (sichtbar wenn `ShowDiffPanel`)
- "Dateien" → `DateiViewCommand` (sichtbar wenn `ShowFileExplorerPanel`)
- "PR" → `PullRequestViewCommand` (sichtbar wenn `ShowPullRequestPanel`)
- "Todos" → `TodoList.TodoAnsichtCommand`

**Zu erweitern:** Button "Automatisierung" (sichtbar wenn `ShowAutomatisierungPanel`) → `AutomatisierungViewCommand`

#### Ansicht-Inhalte (Grid.Row="1", Zeilen 314–583)

Verschiedene `ScrollViewer`/`Grid`-Container mit `Visibility`-Bindings:

- **Info-Ansicht** (Zeilen 315–437): Stammdaten, Protokoll
- **CLI-Ansicht** (Zeilen 439–448): TerminalControl
- **Diff-Ansicht** (Zeilen 450–467): Platzhalter
- **Dateiexplorer** (Zeilen 469–470): `FileExplorerView`
- **Pull Requests** (Zeilen 472–579): ItemsControl mit PR-Details
- **Todos** (Zeile 581–582): `TodoListView`

**Zu erweitern:** Container für **Automatisierung-Ansicht** (sichtbar wenn `IsAutomatisierungViewSelected`) mit eingebettetem `AutonomAufgabeDetailView`

### Statusleiste (Grid.Row="3", Zeilen 586–617)

StatusIndicatorControl, CLI-Status-Text, aktiver CLI-Name.

## `AutonomAufgabeDetailView`

Datei: `src/Softwareschmiede.App/Views/AutonomAufgabeDetailView.xaml`

UserControl für die Detail-Ansicht einer Autonomen Aufgabe. Kann als eigenständiger Dialog oder eingebettete Registerkarte verwendet werden.

### Struktur (Grid mit 3 Rows, `Margin="24"`)

- **Row 0:** Fehlermeldung (Border mit ErrorBrush, sichtbar wenn `ErrorMessage` nicht leer)
- **Row 1:** Action-Buttons
  - "Start" → `StartCommand`
  - "Stop" → `StopCommand`
  - "Resume" → `ResumeCommand`
- **Row 2:** TabControl mit 5 Registerkarten
  - **Konfiguration:** ProjektBranchName, TokenBudget, LaufzeitLimitMinuten, PersistenzModus, ArbeitsverzeichnisPfad (read-only)
  - **Plan:** TextBox für `PlanContent`, Button "Plan speichern"
  - **Fortschritt:** TextBox für `ProgressContent` (read-only)
  - **Governance:** TextBox für `GovernanceContent` (read-only)
  - **Skills:** ListBox mit `Skills`
  - **Unteragenten:** DataGrid mit `Unteragenten` (Agent-ID, Scope, Status, Erzeugt, Abgeschlossen)

**Zu beachten:** Die `Margin="24"` ist mit Standard-Style für eigenständige Dialog-Ansichten gedacht. Bei Einbettung als Registerkarte in TaskDetailView kann diese Margin zu Konflikten führen (Anforderung fragt danach, ob Spacing angepasst werden muss).

## `AutonomAufgabeDetailDialog`

Datei: `src/Softwareschmiede.App/Views/AutonomAufgabeDetailDialog.xaml` / `.xaml.cs`

Wrapper-Fenster, das `AutonomAufgabeDetailView` als eigenständigen modalen Dialog anzeigt.

### XAML-Struktur

- Window-Element mit Titel "Autonome Aufgabe", 900x700 Pixel, resizable
- Enthält direkt: `<views:AutonomAufgabeDetailView />`

### CodeBehind

- Konstruktor akzeptiert `AutonomAufgabeDetailViewModel` und setzt es als `DataContext`
- Keine weitere Logik

**Status:** Diese Klasse wird nach der Integration weiterhin benötigt (als Fallback-Dialog) oder kann entfernt werden, wenn sich die Anforderungen später ändern. Aktuell wird sie von `WpfDialogService.ShowAutonomAufgabeDetailAsync()` verwendet.
