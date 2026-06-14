# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### RepositoryAssignViewModel.cs (RepositoryAssignViewModel)

- **Race Condition (Fire-and-Forget ohne Abbruchmechanismus)** — Zeile 51: `SetProperty(ref _selectedScmPlugin, value, () => _ = ReloadRepositoriesForSelectedPlugin())` startet `ReloadRepositoriesForSelectedPlugin` fire-and-forget ohne vorherige Läufe abzubrechen. Bei schnellen Plugin-Wechseln können mehrere Ladevorgänge gleichzeitig laufen; ein langsamer vorangehender Lauf kann den schnelleren überschreiben und die falsche Repository-Liste anzeigen.

  Empfehlung: Ein `CancellationTokenSource`-Feld `_reloadCts` anlegen. In `ReloadRepositoriesForSelectedPlugin` zu Beginn den vorherigen CTS canceln, einen neuen erstellen und dessen Token durch den gesamten Ladevorgang durchreichen.

- **ObservableCollection wird ersetzt statt befüllt** — Zeile 120: `VerfuegbareRepositories = new ObservableCollection<AvailableRepository>(sorted)` erzeugt eine neue Collection und ruft danach manuell `OnPropertyChanged` auf. Der Property-Setter (`{ get; private set; }`) löst kein `SetProperty` aus, weshalb das Binding-Refresh explizit erzwungen wird. Das ist fehleranfällig und umgeht die etablierte Konvention.

  Empfehlung: Vorhandene Collection direkt befüllen: `VerfuegbareRepositories.Clear(); foreach (var r in sorted) VerfuegbareRepositories.Add(r);` — dann entfällt der manuelle `OnPropertyChanged`-Aufruf.

### RepositoryAssignViewModelTests.cs (RepositoryAssignViewModelTests)

- **Fragile Timing durch Task.Delay** — Zeilen 86, 107, 124, 126, 148, 166: Mehrere Tests verwenden `await Task.Delay(100)` bzw. `await Task.Delay(200)`, um auf fire-and-forget Reloads zu warten, die durch Property-Setter ausgelöst werden. Auf langsamen Build-Maschinen oder unter Last können diese Tests nichtzuverlässig (flaky) werden.

  Empfehlung: Im ViewModel ein `internal Task? CurrentReloadTask`-Feld exponieren, das von `ReloadRepositoriesForSelectedPlugin` zugewiesen wird. Tests können dann direkt `await sut.CurrentReloadTask` aufrufen statt auf einen fixen Delay zu warten.

### ClaudeCliPlugin.cs / GitHubCopilotPlugin.cs (CheckHealthAsync)

- **Externe Cancellation wird nicht weitergereicht** — Zeile 81–84 (Claude) / 97–100 (Copilot): `catch (OperationCanceledException)` fängt alle Abbrüche ab — sowohl den internen Timeout (`cts.CancelAfter`) als auch eine externe Cancellation des übergebenen `ct`. Ist `ct` extern abgebrochen (z.B. App-Shutdown), gibt die Methode `false` zurück statt `OperationCanceledException` zu propagieren. Das verletzt den Cancellation-Vertrag.

  Empfehlung: `catch (OperationCanceledException) when (!ct.IsCancellationRequested)` verwenden, sodass nur der interne Timeout abgefangen wird. Externe Cancellations werden dann automatisch nach oben propagiert.

## Geprüfte Dateien

- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`
- `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs`
- `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`
- `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`
- `src/Softwareschmiede.App/Services/DarkModeService.cs`
- `src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/PluginSettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml`
- `src/Softwareschmiede.App/Themes/DarkTheme.xaml`
- `src/Softwareschmiede.App/Themes/LightTheme.xaml`
- `src/Softwareschmiede.Tests/App/ViewModels/RepositoryAssignViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/ServiceIntegration/DarkModeServiceIntegrationTests.cs`
