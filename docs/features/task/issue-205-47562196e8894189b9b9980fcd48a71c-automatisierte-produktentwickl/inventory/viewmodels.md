# ViewModels

## `TaskDetailViewModel`

Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

ViewModel für die Aufgaben-Detailansicht. Verwaltet Status, Protokoll, CLI-Prozessstart, Fenstereinbettung und Ansicht-Umschaltung.

### Konstanten und Abhängigkeiten

- Private `DetailAnsicht` enum (Info, Cli, Diff, Dateibrowser, PullRequests, Todos) — steuert Ansicht-Umschaltung
- Abhängigkeiten: AufgabeService, ProtokollService, KiAusfuehrungsService, EntwicklungsprozessService, PluginSelectionService, PromptVorlagenService, PromptVorlagenPlatzhalterService, PromptZeitVersandService, IDialogService, IPluginManager, IServiceProvider, FileExplorerViewModel, TodoListViewModel, ArbeitsverzeichnisOeffnenService, AutonomAufgabeStartService

### Wichtigste Properties (für die Anforderung relevant)

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| `Aufgabe` | `Aufgabe?` | Geladene Aufgabe (setter triggeert Property-Notify-Cascade und `WaehleStandardAnsicht()`) |
| `IsInfoViewSelected` | `bool` | True wenn `_ausgewaehlteAnsicht == DetailAnsicht.Info` |
| `IsCliViewSelected` | `bool` | True wenn `_ausgewaehlteAnsicht == DetailAnsicht.Cli` |
| `IsDiffViewSelected` | `bool` | True wenn `_ausgewaehlteAnsicht == DetailAnsicht.Diff` |
| `IsFileExplorerViewSelected` | `bool` | True wenn `_ausgewaehlteAnsicht == DetailAnsicht.Dateibrowser` |
| `IsPullRequestViewSelected` | `bool` | True wenn `_ausgewaehlteAnsicht == DetailAnsicht.PullRequests` |
| `IsTodoViewSelected` | `bool` | True wenn `_ausgewaehlteAnsicht == DetailAnsicht.Todos` |
| `ShowCliPanel` | `bool` | True wenn Status erfordert CLI-Ansicht **und** nicht autonom |
| `ShowFileExplorerPanel` | `bool` | True wenn lokaler Klonpfad existiert |
| `ShowPullRequestPanel` | `bool` | True wenn Aufgabe vorhanden |

**Fehlt:** `IsAutomatisierungViewSelected`, `ShowAutomatisierungPanel`, `AutonomAufgabeDetailViewModel` Property

### Commands (für die Anforderung relevant)

| Command | Funktionalität |
|---------|----------------|
| `InfoViewCommand` | Ruft `WaehleAnsicht(DetailAnsicht.Info)` auf |
| `CliViewCommand` | Ruft `WaehleAnsicht(DetailAnsicht.Cli)` auf (CanExecute: ShowCliPanel) |
| `DiffViewCommand` | Ruft `WaehleAnsicht(DetailAnsicht.Diff)` auf (CanExecute: ShowDiffPanel) |
| `DateiViewCommand` | Ruft `WaehleAnsicht(DetailAnsicht.Dateibrowser)` auf (CanExecute: ShowFileExplorerPanel) |
| `PullRequestViewCommand` | Ruft `WaehleAnsicht(DetailAnsicht.PullRequests)` auf und aktualisiert PRs (CanExecute: ShowPullRequestPanel) |
| `AutonomAufgabeInitialisierenCommand` | Ruft `AutonomAufgabeInitialisierenAsync()` auf (CanExecute: Aufgabe != null) |

**Zu erweitern:** Ein neuer Command `AutomatisierungViewCommand` für Ansicht-Umschaltung zur neuen Registerkarte.

### Private Methoden (für die Anforderung relevant)

| Methode | Beschreibung |
|---------|-------------|
| `WaehleAnsicht(DetailAnsicht ansicht)` | Umschalter für Ansicht-Selection; triggert PropertyChanged für alle `IsXxxViewSelected` Properties |
| `WaehleStandardAnsicht()` | Wählt bei Aufgaben-Laden die beste Ansicht (Cli wenn ShowCliPanel, sonst Diff wenn Status==Beendet, sonst Info) |
| `AutonomAufgabeInitialisierenAsync(CancellationToken ct)` | Ruft `_autonomAufgabeStartService.StarteAsync()` auf und aktualisiert `Aufgabe` falls erforderlich |

### Konstruktor

Erwartet alle Abhängigkeiten als Parameter, einschließlich `AutonomAufgabeStartService`.

## `AutonomAufgabeDetailViewModel`

Datei: `src/Softwareschmiede.App/ViewModels/AutonomAufgabeDetailViewModel.cs`

ViewModel für die Detail-Ansicht einer Autonomen Aufgabe. Verwaltet Konfiguration, plan.md/progress.md/governance.md und Start/Stop/Resume-Kontrollen.

### Abhängigkeiten

- `ProjektleiterAgentService` — startet/stoppt Agenten
- `SessionManagementService` — pausiert/setzt fort
- `ILogger<AutonomAufgabeDetailViewModel>`
- `Aufgabe` (Parameter)
- `AutonomAufgabeKonfiguration` (Parameter)

### Commands

| Command | Funktionalität |
|---------|----------------|
| `StartCommand` | Ruft `StarteAgentAsync()` auf (CanExecute: !IsBusy) |
| `StopCommand` | Ruft `StoppeAgentAsync()` auf (CanExecute: !IsBusy) |
| `ResumeCommand` | Ruft `ResumeAgentAsync()` auf (CanExecute: !IsBusy) |
| `SavePlanCommand` | Speichert plan.md (CanExecute: !IsBusy) |

### Properties

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| `Konfiguration` | `AutonomAufgabeKonfiguration` | Read-only |
| `Unteragenten` | `ObservableCollection<UnteragentSpezifikation>` | Sichtbar in Unteragenten-Tab |
| `Skills` | `ObservableCollection<SkillDefinition>` | Sichtbar in Skills-Tab |
| `PlanContent` | `string` | plan.md-Inhalt |
| `ProgressContent` | `string` | progress.md-Inhalt (read-only) |
| `GovernanceContent` | `string` | governance.md-Inhalt (read-only) |
| `ErrorMessage` | `string?` | Fehlermeldungen |
| `IsBusy` | `bool` | True während Operation läuft |

### Konstruktor

```csharp
public AutonomAufgabeDetailViewModel(
    Aufgabe aufgabe,
    AutonomAufgabeKonfiguration konfiguration,
    ProjektleiterAgentService projektleiterAgentService,
    SessionManagementService sessionManagementService,
    ILogger<AutonomAufgabeDetailViewModel> logger,
    IReadOnlyList<UnteragentSpezifikation>? unteragenten = null,
    IReadOnlyList<SkillDefinition>? skills = null)
```

**Zu beachten:** Abhängigkeiten werden als Parameter übergeben, nicht über ServiceProvider aufgelöst (im Gegensatz zu TaskDetailViewModel). Dies ermöglicht die Übergabe eines bereits initialisierten ViewModels an TaskDetailViewModel.
