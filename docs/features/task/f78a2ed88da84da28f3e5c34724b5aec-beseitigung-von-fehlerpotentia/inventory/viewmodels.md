# ViewModels: Fire-and-Forget-Aufrufe und Event-Handler

## `MainWindowViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CurrentView` (Setter, Zeile 32–40) | public property | Ruft `_ = AktiveAufgabenAktualisierenAsync();` (Zeile 38) bei jedem View-Wechsel — **Fire-and-Forget ungeschützt (F15)** |
| `AktiveAufgabenAktualisierenAsync(CancellationToken)` | public async (Zeile 136–151) | Lädt aktive Aufgaben; try-catch fängt `OperationCanceledException` (rethrow) und generische `Exception` (`LogWarning`) ab — die Methode selbst ist also robust, nur der Fire-and-Forget-Aufrufer (Zeile 38) nicht |
| `NavigateToDashboard()` / `NavigateToProjectList()` / `NavigateToSettings()` | private | Synchrone Navigation, holt ViewModels via DI |
| `NavigateZuAufgabe(Guid)` | private | Navigiert zu `TaskDetailViewModel` |

Abonnierte Events: keine
Publizierte Events: keine

## `ProjectDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProjektId` (Setter, Zeile 54–68) | public property | Bricht alten `_ladenCts` ab, erstellt neuen, ruft `_ = LadenAsync(_ladenCts.Token);` (Zeile 65) — **Fire-and-Forget ungeschützt (F16)** |
| `LadenAsync(CancellationToken)` | private async | Lädt Projekt, Aufgaben, Issues; try-catch mit `OperationCanceledException` (rethrow) und generischer `Exception` → `SetFehler(ex)` |
| `AufgabeErstellenAsync`, `ProjektSpeichernAsync`, `ProjektLoeschenAsync`, `RepositoryZuweisenAsync`, `LadenIssuesAsync`, `AufgabeAusIssueErstellenAsync` | private async | Alle mit konsistentem try-catch-Muster (`OperationCanceledException` rethrow, generisch → Logging + `SetFehler`) |
| `RepositoryOeffnen()` | private | Startet Browser-Prozess; try-catch vorhanden |
| `Dispose()` | public | Implementiert `IDisposable`; bricht `_ladenCts` ab und disposed ihn; setzt `_disposed = true` |

Abonnierte Events: keine
Publizierte Events: keine (nutzt Callback-Delegates: `ZurueckAction`, `ProjektListeAktualisierenCallback`, `NavigateToTaskViewCallback`, `NavigateBackToProjectCallback`)

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AufgabeId` (Setter, Zeile 55–68) | public property | Bricht alten `_ladenCts` ab, erstellt neuen, ruft `_ = LadenAsync(_ladenCts.Token);` (Zeile 65) — **Fire-and-Forget ungeschützt (F17)** |
| `LadenAsync(CancellationToken)` | private async | Lädt Aufgabe, Protokoll, Plugins; ggf. Auto-Restart der CLI; try-catch vorhanden |
| `CliStoppenAsync`, `CliNeustartenAsync`, `AufgabeAbschliessenAsync`, `SpeichernAsync`, `LoeschenAsync`, `IssueZuweisenAsync`, `StartenAsync`, `PluginWechselAsync`, `CliAutomatischNeustartenAsync`, `StartCliAndUpdateStateAsync` | private async | Konsistentes try-catch-Muster mit `OperationCanceledException` rethrow und generischer Behandlung |
| `OnCliProcessStatusChanged(Guid, CliProcessStatus)` | private | Reagiert per `_dispatcherInvoke` auf Statuswechsel; kein try-catch um den Dispatcher-Callback-Body |
| `AttachCliStatusSession(PseudoConsoleSession?)` / `OnCliRuntimeStatusChanged(...)` / `UpdateCliStatusText(...)` | private | Verwaltung des CLI-Status-Textes; abonniert/deabonniert `RuntimeStatusChanged` der Session |
| `Dispose()` | public | Deabonniert `_kiService.CliProcessStatusChanged`, löst Session-Bindung, bricht `_ladenCts` ab |

Abonnierte Events: `_kiService.CliProcessStatusChanged` (Konstruktor, Zeile 304); `_cliStatusSession.RuntimeStatusChanged` (dynamisch über `AttachCliStatusSession`)
Publizierte Events: `PseudoConsoleSessionGestartet` (`Action<PseudoConsoleSession>`), `CliGestoppt` (`Action`)

## Kritische Stellen (Bezug zur Anforderung)

- **F15:** `MainWindowViewModel.cs` Zeile 38 — `_ = AktiveAufgabenAktualisierenAsync();` im `CurrentView`-Setter
- **F16:** `ProjectDetailViewModel.cs` Zeile 65 — `_ = LadenAsync(_ladenCts.Token);` im `ProjektId`-Setter
- **F17:** `TaskDetailViewModel.cs` Zeile 65 — `_ = LadenAsync(_ladenCts.Token);` im `AufgabeId`-Setter

Alle drei Fire-and-Forget-Aufrufe rufen Methoden auf, die selbst bereits ein try-catch mit generischer Exception-Behandlung besitzen. Das Fehlerpotential besteht in Exceptions, die außerhalb dieses try-catch auftreten könnten (z. B. `SetProperty`-Callback selbst, `OnPropertyChanged`-Aufrufe) oder in `TaskScheduler.UnobservedTaskException`, falls die Task-Exception nicht beobachtet wird.
