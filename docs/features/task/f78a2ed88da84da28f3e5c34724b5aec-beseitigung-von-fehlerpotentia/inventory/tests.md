## Testklassen

### `KiAusfuehrungsServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`

- `IsRunning_ShouldReturnFalse_WhenNoProcessStarted` — kein Prozess bekannt
- `GetRunningCount_ShouldReturnZero_WhenNoProcessStarted` — Zähler initial 0
- `GetLastExitCode_ShouldReturnNull_WhenNoProcessStarted` — kein Exit-Code ohne Prozess
- `StopCliAsync_ShouldNotThrow_WhenNoProcessStarted` — Stop ohne laufenden Prozess wirft nicht
- `UpdateHeartbeat_ShouldNotThrow_WhenNoProcessStarted` — Heartbeat-Update ohne Prozess wirft nicht
- `TestCliStartAsync` — CLI wird gestartet, Handle zurückgegeben, `IsRunning`/`GetRunningCount` reflektieren dies
- `StartCliAsync_ShouldReturnHandle_WhenPluginProvidesValidProcessStartInfo` — analog
- `GetPseudoConsoleSession_GibtNull_OhneSession` — kein Handle ohne ConPTY-Start
- `ProcessExited_ScopeFactoryDisposed_PersistiertNichtUndWirftNicht` — **Regressionstest**, bereits bezogen auf F9/PersistFehlgeschlagenAsync: Wenn der `IServiceScopeFactory` beim `process.Exited`-Callback bereits disposed ist (`ObjectDisposedException`), darf dies nicht zu einer unbeobachteten `TaskScheduler.UnobservedTaskException` führen. Verifiziert nur den Pfad über `PersistFehlgeschlagenAsync`, nicht das gesamte `Exited`-Handler-Body (F9/F10 bleiben ungetestet für andere Exception-Quellen wie `RaiseRunningCountChanged()` oder `CliProcessStatusChanged?.Invoke(...)`)

Keine Tests vorhanden für: `StartWithPseudoConsoleAsync` (ConPTY-Pfad, F10/F11), Semaphore-/Concurrency-Verhalten bei `AktualisierungAsync` (F7 — diese Methode liegt in `CliProcessManager`), Fire-and-Forget-Aufruf `SendCommandDelayedAsync` (F8).

## Sonstige relevante Testklassen (ohne spezifische Fehlerbehandlungs-Tests für diese Anforderung)

### `MainWindowViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`

Testet `AktiveAufgabenAktualisierenAsync`, `IsDashboardVisible`, `NavigateZuAufgabeCommand`, `NavigateToDashboard`-Kopplung mit `DashboardViewModel`. Kein Test deckt das Fire-and-Forget-Verhalten von `CurrentView` (Setter, F15) ab — d. h. es existiert kein Test, der prüft, dass eine Exception in `AktiveAufgabenAktualisierenAsync` nicht zu einer unbeobachteten Task-Exception führt.

### `ProjectDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`

Umfangreiche Tests für `ProjektSpeichernAsync`, `ProjektLoeschenAsync`, `RepositoryZuweisenAsync`, `AufgabeOeffnen`, `LadenIssuesAsync` (inkl. `LadenIssuesAsync_HandlesExceptionGracefully`), `AufgabeAusIssueErstellenAsync`. Kein Test für das Fire-and-Forget-Verhalten des `ProjektId`-Setters (F16).

### `TaskDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

Breite Abdeckung von Property-Berechnungen (`ShowEditPanel`, `ShowCliPanel`, `KannSpeichern`, `KannLoeschen` usw.), `SpeichernCommand`, `LoeschenCommand`, `StartenCommand`, inkl. Fehlerfall-Tests (`SpeichernAsync_ShowsErrorMessage_WhenSaveFails`, `LoeschenCommand_SetzFehlerMeldung_WennDeleteScheitert`). Kein Test für das Fire-and-Forget-Verhalten des `AufgabeId`-Setters (F17).

### `AppTests`
Datei: `src/Softwareschmiede.Tests/Components/AppTests.cs`

Betrifft ausschließlich Favicon-Markup in `src/Softwareschmiede/Components/App.razor` (Blazor-Komponente eines anderen Projektteils) — **nicht** die WPF-`App.xaml.cs`. Für die WPF-`App.xaml.cs` (globale Exception-Handler, Startup-Pfade) existieren keine Tests.

## Hilfsmethoden

### `TaskDetailViewModelTestFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`

Stellt eine Factory-Methode `Create(...)` bereit, um `TaskDetailViewModel`-Instanzen mit gemocktem `DbContext`/`AufgabeService` für Tests zu erzeugen (verwendet u. a. von `MainWindowViewModelTests`).

### `TestDbContextFactory`
Erzeugt In-Memory/SQLite-Test-`DbContext`-Instanzen für Testklassen (`TestDbContextFactory.Create()`), verwendet in `MainWindowViewModelTests`, `ProjectDetailViewModelTests`, `TaskDetailViewModelTests`.

## Nicht vorhandene Testinfrastruktur

- Keine Tests oder Test-Helfer für eine `SafeFireAndForgetTaskHelper`/`AsyncTaskExtensions`-Klasse — diese existiert im Code nicht (siehe [CliProcessManager](cliprocessmanager.md), [KiAusfuehrungsService](kiausfuehrungsservice.md), [ViewModels](viewmodels.md)).
- Keine Tests für globale Exception-Handler (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) — diese sind nicht registriert (siehe [app.md](app.md)).
- Keine Tests für `TerminalControl.ReadLoopAsync`/`OnSessionChanged` (F12–F14) — `TerminalControl` hat keine dedizierte Testklasse.
