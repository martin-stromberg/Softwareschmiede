# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### KiAusfuehrungsService.cs (KiAusfuehrungsService)

- **Fehlerbehandlung / Kopplung** — `StartCliAsync` (Zeilen 47–103): Double-checked Locking mit Lücke. Das Semaphor wird nach dem ersten Check freigegeben (Zeile 58), dann läuft `await kiPlugin.StartCliAsync` ohne Lock (Zeile 63), danach wird ein zweites Mal gelockt. In der Lücke können zwei gleichzeitige Aufrufe für dieselbe `aufgabeId` beide die erste Prüfung passieren und beide mit dem Process-Setup beginnen. Der zweite Check (Zeile 72–77) verhindert zwar das doppelte Eintragen in `_handles`, aber der erste Aufruf hat bereits einen Prozess erstellt, der dann weggeworfen wird — der Prozess ist gestartet, das `process.Start()` (Zeile 96) wird jedoch nur einmal ausgeführt. Tatsächlich wurde der Prozess zu diesem Zeitpunkt noch **nicht** gestartet, daher kommt es zu einem verwaisten nicht-gestarteten `Process`-Objekt (Dispose ohne Start). Die eigentliche Absicherung ist schwächer als sie aussieht.

  Empfehlung: Das Semaphor über die gesamte Operation halten (von der ersten Prüfung bis nach `process.Start()`), oder den Zustand „Start in Vorbereitung" als eigenen Eintrag in `_handles` tracken, bevor das Lock freigegeben wird.

- **Toter Code** — `IsCliRunning` (Zeile 138): Die Methode delegiert 1:1 auf `IsRunning`, ohne eigene Logik hinzuzufügen. Sie ist ein reiner Middle-Man-Wrapper, der Mehraufwand bei Suche und Navigation erzeugt.

  Empfehlung: `IsCliRunning` entfernen und alle Aufrufer direkt auf `IsRunning` zeigen lassen.

### CliProcessManager.cs (CliProcessManager)

- **Kopplung** — `AktualisierungDurchfuehren` (Zeile 57–59): Fire-and-forget `_ = AktualisierungAsync(aufgabeId)` aus einem Timer-Callback heraus. Eine Exception in `AktualisierungAsync`, die nicht durch den try/catch abgedeckt wird (z. B. ObjectDisposedException nach Shutdown), bricht die TaskScheduler-Unhandled-Exception-Kette.

  Empfehlung: Die Methode `AktualisierungAsync` hat bereits einen `catch (Exception)`, der alles abfängt — das Muster ist korrekt, aber der Name `AktualisierungDurchfuehren` verschleiert das Fire-and-Forget. Die Wrapper-Methode ist redundant; den Timer-Callback direkt auf `_ = AktualisierungAsync(aufgabeId)` setzen und die Zwischenmethode entfernen.

- **Kopplung** — `AufgabeService aufgabeService`-Parameter im Konstruktor (Zeile 21): `CliProcessManager` ist als Singleton registriert, ruft aber `_aufgabeService.UpdateHeartbeatAsync` auf — ein Scoped Service. Der Singleton hält direkt eine Scoped-Instanz, nicht über IServiceScopeFactory. Das führt zu einem Scope-Leak: der Scoped Service lebt so lange wie der Singleton, nicht wie die HTTP-Request-Lifetime. Da die App WPF (kein Request-Scoping) ist, ist der konkrete `AufgabeService` (der einen `DbContext` hält) de facto ein Singleton-Scope.

  Empfehlung: `IServiceScopeFactory` injizieren und für jeden Heartbeat-Tick einen neuen Scope erstellen und nach der Operation verwerfen.

### ProjectDetailViewModel.cs (ProjectDetailViewModel)

- **Fehlerbehandlung** — `ProjektId`-Setter (Zeile 29–33): `_ = LadenAsync(CancellationToken.None)` — Fire-and-forget ohne Cancellation-Token-Verwaltung. Wenn `ProjektId` zweimal schnell gesetzt wird (Navigation zurück und sofort zu einem anderen Projekt), laufen zwei parallele `LadenAsync`-Calls; das zuletzt abgeschlossene überschreibt den Zustand, unabhängig davon, welches Projekt aktuell angezeigt werden soll. Im Gegensatz zu `TaskDetailViewModel` (das einen `CancellationTokenSource`-basierten Abbruch implementiert) fehlt dieses Muster hier.

  Empfehlung: Analoges Muster zu `TaskDetailViewModel.AufgabeId` übernehmen: `CancellationTokenSource _ladenCts` als Feld, bei jeder neuen Zuweisung den vorherigen CTS canceln und einen neuen erstellen.

### TaskListViewModel.cs (TaskListViewModel)

- **Fehlerbehandlung** — `ProjektId`-Setter (Zeile 26–31): Identisches Problem wie in `ProjectDetailViewModel.ProjektId`: `_ = LadenAsync(CancellationToken.None)` ohne Abbruchmechanismus.

  Empfehlung: Dasselbe CancellationTokenSource-Muster wie in `TaskDetailViewModel` anwenden; `IDisposable` implementieren um den CTS zu verwerfen.

### SettingsViewModel.cs (SettingsViewModel)

- **Fehlerbehandlung** — `IsDarkMode`-Setter (Zeile 36–40): `_ = _darkModeService.SetDarkModeAsync(value, CancellationToken.None)` — Fire-and-forget async in einem Property-Setter. Der `DarkModeService` persistiert die Einstellung in der Datenbank; schlägt das fehl, gibt es keine Fehleroberfläche. Der Setter kehrt zurück, bevor der async-Pfad abgeschlossen ist. Die UI zeigt den neuen Zustand als Erfolg an, obwohl die Persistierung möglicherweise fehlgeschlagen ist.

  Empfehlung: Den Dark-Mode-Toggle ausschließlich über `MainWindowViewModel.ToggleDarkModeCommand` (das bereits `AsyncRelayCommand` verwendet) steuern. `IsDarkMode` im `SettingsViewModel` als read-only Property belassen, die den `DarkModeService`-Zustand spiegelt (über den `DarkModeChanged`-Event), und das asynchrone Umschalten nicht in den Setter legen.

### CliSessionService.cs (CliSessionService)

- **Namenskonventionen / Struktur** — Gesamte Datei (Zeilen 1–76): Der Service ist inkonsistent mit dem übrigen Codebase-Stil: keine XML-Dokumentation, `public class` statt `public sealed class`, doppelter `using System.Diagnostics;`-Import (Zeilen 1 und 5), keine DI-konforme Interface-Extraktion, hardcodierte CLI-Kommandos (`"copilot chat ."`, `"claude chat ."`) mit einer `switch`-Anweisung auf `cliName.ToLower()`, die bei unbekanntem Wert eine unhandled Exception wirft.

  Empfehlung: XML-Dokumentation ergänzen, `sealed` hinzufügen, doppelten Import entfernen, `ICliSessionService`-Interface extrahieren. Die hardcodierten CLI-Kommandos gegen Konfiguration oder Plugin-basiertes Lookup ersetzen — analoges Muster zu `KiAusfuehrungsService` + `IKiPlugin.StartCliAsync` verwenden.

- **Fehlerbehandlung** — `ReadOutputLoop` (Zeilen 48–55): Keine Exception-Behandlung. Wirft `_process.StandardOutput.ReadLineAsync()` eine Exception (z. B. `IOException` bei Prozessabbruch), terminiert der Loop ohne Log-Eintrag und der Aufrufer bekommt keine Fehlermeldung.

  Empfehlung: try/catch mit Logging um den Output-Loop ergänzen; analoges Muster zu `KiAusfuehrungsService.WaitForExitAsync`.

### AppEinstellungService.cs (AppEinstellungService)

- **Doppelter Code / God-Methode** — `GetWindowGeometryAsync` (Zeilen 106–113): Vier separate Datenbankabfragen (je ein `GetIntSettingAsync`-Aufruf), die durch vier `await`-Stellen sequenziell ausgeführt werden. Das Muster ist korrekt aber ineffizient, und die Hilfsmethode `GetIntSettingAsync` (Zeile 56–63) kapselt seinerseits `GetSettingAsync`, was eine dreistufige Delegation für eine einzelne DB-Row erzeugt.

  Empfehlung: `GetWindowGeometryAsync` in einer einzigen Datenbankabfrage zusammenfassen, die alle vier Schlüssel gleichzeitig liest (z. B. `WHERE Schluessel IN (...)` + `ToDictionaryAsync`).

### ProcessWindowHost.cs (ProcessWindowHost)

- **Fehlende Kapselung** — Die `NativeMethods`-Klasse (Zeilen 117–146) ist als private statische Nested-Klasse definiert. Für P/Invoke-Deklarationen ist das ein gängiges Muster, aber die `SetLastError = true`-Annotation fehlt bei `SetParent`, `SetWindowPos`, `GetWindowLong` und `SetWindowLong`. Nur `CreateWindowEx` hat `SetLastError = true`. Das führt dazu, dass die Fehlerprüfung in `BuildWindowCore` (`Marshal.GetLastWin32Error()`, Zeile 58) korrekt ist, aber analoge Fehlerprüfungen nach `SetParent`/`SetWindowLong` in `EmbedWindow` fehlen komplett — stille Fehler sind möglich.

  Empfehlung: `SetLastError = true` zu allen `[DllImport]`-Attributen ergänzen und Rückgabewerte von `SetParent` und `SetWindowLong` nach dem Aufruf prüfen; bei Fehler loggen.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/Controls/ProcessWindowHost.cs`
- `src/Softwareschmiede.App/Controls/RecoveryBannerControl.xaml.cs`
- `src/Softwareschmiede.App/Controls/StatusIndicatorControl.xaml.cs`
- `src/Softwareschmiede.App/Converters/AppConverters.cs`
- `src/Softwareschmiede.App/Services/DarkModeService.cs`
- `src/Softwareschmiede.App/Services/WpfAudioService.cs`
- `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/NavigationViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/PluginSettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/TaskListViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs`
- `src/Softwareschmiede.App/Views/DashboardView.xaml.cs`
- `src/Softwareschmiede.App/Views/MainWindow.xaml.cs`
- `src/Softwareschmiede.App/Views/PluginSettingsView.xaml.cs`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml.cs`
- `src/Softwareschmiede.App/Views/ProjectListView.xaml.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs`
- `src/Softwareschmiede.App/Views/TaskListView.xaml.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IAiCliProvider.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingFieldType.cs`
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`
- `src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede/Application/Services/BenachrichtigungsService.cs`
- `src/Softwareschmiede/Application/Services/CliProcessManager.cs`
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- `src/Softwareschmiede/Application/Services/KiAufgabenBenachrichtigungsHub.cs`
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
- `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`
- `src/Softwareschmiede/Application/Services/ProtokollService.cs`
- `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`
- `src/Softwareschmiede/Infrastructure/Services/AgentPackageReader.cs`
- `src/Softwareschmiede/Infrastructure/Services/CliSessionService.cs`
- `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs`
- `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`
- `plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs`
