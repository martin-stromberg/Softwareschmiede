# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs (ProjectListViewModel)

- **Fehlerbehandlung / Ressourcenverwaltung** — `_currentProjectDetailViewModel` wird beim Wechsel zur Aufgabenansicht (`ZeigeTaskDetailView`, Zeile 206-209) bewusst vor dem `Dispose()`-Aufruf des `DetailViewModel`-Setters ausgenommen (Zeile 50: `!ReferenceEquals(old, _currentProjectDetailViewModel)`), damit es beim späteren `KehreZuProjectZurueck()` wiederverwendet werden kann. Wird die Detailansicht jedoch direkt aus der Aufgabenansicht heraus geschlossen (z. B. `SchliesseDetailCommand` während `DetailViewModel` ein `TaskDetailViewModel` ist, oder die App wird beendet während eine Aufgabe offen ist), wird `_currentProjectDetailViewModel` nie disposed und nie auf `null` gesetzt. Das zugehörige `ProjectDetailViewModel` (inkl. seines `CancellationTokenSource _ladenCts`) bleibt dauerhaft referenziert und undisposed — ein Leak, der durch den in diesem Branch neu eingeführten Navigationsfluss (Projekt → separate Aufgabenansicht → zurück) aktiv ausgelöst wird.

  Empfehlung: Beim Setzen von `DetailViewModel = null` (in `SchliesseDetailCommand`) sowie beim Ersetzen durch ein neues `ProjectDetailViewModel` in `ZeigeDetail`/`ZeigeDetailErstellungsFormular` zusätzlich `_currentProjectDetailViewModel?.Dispose()` aufrufen und das Feld danach auf `null` setzen, sofern es nicht das aktuell aktive `DetailViewModel` ist.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml`
- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml`
- `src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml.cs`
- `src/Softwareschmiede.App/Controls/RibbonSmallButton.xaml`
- `src/Softwareschmiede.App/Controls/RibbonSmallButton.xaml.cs`
- `src/Softwareschmiede.App/Converters/AppConverters.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/DashboardView.xaml`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`
- `src/Softwareschmiede.App/Views/SettingsView.xaml.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`
- `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`
