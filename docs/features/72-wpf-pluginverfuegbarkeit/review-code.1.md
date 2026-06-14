# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### `KiAusfuehrungsService` (`src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`)

- **Fehlerbehandlung** — `process.Start()` Rückgabewert wird nicht geprüft (Zeile 99); `process.Id` auf Zeile 103 wirft `InvalidOperationException`, wenn der Prozess nicht gestartet wurde

  Empfehlung: Rückgabewert von `process.Start()` prüfen. Gibt er `false` zurück (oder wirft er keine Exception), vor dem Zugriff auf `process.Id` eine `InvalidOperationException` werfen und den Status `CliProcessStatus.Gestartet` nicht feuern. Beispiel: `if (!process.Start()) throw new InvalidOperationException("Prozess konnte nicht gestartet werden.");`

### `ClaudeCliPlugin` (`plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs`)

- **Fehlerbehandlung** — `CheckHealthAsync` ruft `await process.WaitForExitAsync(ct)` ohne internen Timeout auf; bei einem hängenden `claude`-Prozess und CancellationToken.None blockiert der Health-Check unbegrenzt

  Empfehlung: Einen gebundenen CancellationToken mit Timeout (z.B. 10 Sekunden) verwenden: `using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(TimeSpan.FromSeconds(10)); await process.WaitForExitAsync(cts.Token);`

### `GitHubCopilotPlugin` (`plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`)

- **Fehlerbehandlung** — `CheckHealthAsync` hat dasselbe Problem wie `ClaudeCliPlugin`: kein Timeout für `WaitForExitAsync`, der Health-Check blockiert unbegrenzt, wenn der Copilot-CLI-Prozess hängt

  Empfehlung: Identische Korrektur wie für `ClaudeCliPlugin.CheckHealthAsync` — CancellationTokenSource mit Timeout verwenden.

- **Toter Code / Verlorene Invariante** — Die Umgebungsisolation (`USERPROFILE`, `HOME`, `LOCALAPPDATA`, `APPDATA`, `TEMP`, `TMP`) aus der früheren `GetGhEnvironment`-Methode wurde vollständig entfernt; der Copilot-CLI-Prozess erbt jetzt das echte Benutzerprofil des Hosts

  Empfehlung: Falls mehrere parallele Runs unterstützt werden sollen oder Isolation weiterhin gefordert ist, die Umgebungsvariablen in `BuildProcessStartInfo` wieder setzen. Falls Isolation bewusst aufgegeben wurde, im Kommentar festhalten, dass der Copilot-Prozess im echten Benutzerprofil läuft.

### `KiSimulatorPlugin` (`plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs`)

- **Effizienz / Verhalten** — `BuildProcessStartInfo` startet ein echtes `cmd.exe`-Fenster mit `timeout /t 30 /nobreak` und `UseShellExecute = true`, `CreateNoWindow = false`; in Tests und in der WPF-Anwendung erscheint ein sichtbares Konsolenfenster für 30 Sekunden, das nicht per Ctrl+C abgebrochen werden kann

  Empfehlung: `CreateNoWindow = true` setzen, um das Fenster zu unterdrücken. Langfristig: einen minimalistischen echten CLI-Simulator (z.B. ein separates `Softwareschmiede.Plugin.KiSimulator.Cli`-Executable) einbauen oder `UseShellExecute = false` verwenden, damit der Prozess auch programmatisch gesteuert werden kann.

### `PluginSettingsViewModel` (`src/Softwareschmiede.App/ViewModels/PluginSettingsViewModel.cs`)

- **Fehlerbehandlung** — `LadenAsync(CancellationToken ct)` ist vollständig synchron (kein `await`), überprüft `ct` nie und gibt `Task.CompletedTask` zurück; bei einer Exception mitten in der Plugin-Iteration bleibt `Plugins` in einem inkonsistenten Teilzustand (bereits hinzugefügte Plugins bleiben erhalten)

  Empfehlung: `Plugins.Clear()` in den `catch`-Block verschieben (oder eine separate Staging-Liste verwenden und erst am Ende in `Plugins` übernehmen), damit bei Fehlern kein Teilzustand sichtbar ist. Den `ct`-Parameter entweder tatsächlich nutzen oder entfernen.

### `CliKiPluginBase` (`src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`)

- **Toter Code** — `ExtractWindowTitleFromProcess(Process process)` ist `protected static` definiert, wird aber in keiner Unterklasse und keiner anderen Datei aufgerufen

  Empfehlung: Methode entfernen, bis sie einen konkreten Aufrufer hat.

## Geprüfte Dateien

- `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs`
- `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`
- `plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingFieldType.cs`
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
- `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`
- `src/Softwareschmiede/Application/Services/PluginDefaultSettingsService.cs`
- `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`
- `src/Softwareschmiede.App/ViewModels/PluginSettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
