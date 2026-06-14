# Code Review — Branch `72-wpf-pluginverfuegbarkeit`

Reviewed files: `plugins/`, `src/Softwareschmiede.App/`, `src/Softwareschmiede.Plugin.Contracts/`, `KiAusfuehrungsService.cs`, `CliTerminal.razor`, `AppEinstellungService.cs`

Effort: **high** — 7 finder angles × up to 6 candidates, 1-vote verify (recall-biased).

---

## Findings

```json
[
  {
    "file": "src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs",
    "line": 662,
    "summary": "Calls resolvedPlugin.GetAvailableAgentsAsync() on IKiPlugin, but this method was removed from the interface in this branch — compile error.",
    "failure_scenario": "IKiPlugin now only declares StartCliAsync, GetProcessWindowTitle, SupportsSessionContinuation, CheckHealthAsync. AufgabeDetail.razor.cs line 662 still calls GetAvailableAgentsAsync on a resolved IKiPlugin. This produces a CS1061 compile error that prevents the app from building. Test files AufgabeDetailGitActionsBunitTests.cs and AufgabeDetailFolgePromptTests.cs also mock IsAgentPackageCompatibleAsync and DeployAgentPackageAsync on Mock<IKiPlugin>, producing additional compile errors."
  },
  {
    "file": "src/Softwareschmiede/Components/Shared/CliTerminal.razor",
    "line": 44,
    "summary": "ReceiveLoop Task.Run is fire-and-forget with CancellationToken.None and no IDisposable/IAsyncDisposable — the loop outlives the component.",
    "failure_scenario": "When Blazor disposes CliTerminal (e.g. the task leaves AufgabeStatus.Gestartet), the background ReceiveLoop continues running because it was started with Task.Run and no cancellation token, and the component has no disposal path. The loop keeps calling InvokeAsync on the disposed renderer, which throws ObjectDisposedException repeatedly until the WebSocket eventually closes. On pages that frequently show/hide the terminal, this leaks one thread-pool worker per render."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs",
    "line": 104,
    "summary": "_handles[aufgabeId] is assigned AFTER process.Start() — if the process exits immediately, the Exited handler fires before the handle is stored.",
    "failure_scenario": "process.Exited is wired at line 79, process.Start() is called at line 99, but _handles[aufgabeId] = handle is only at line 104. If the process exits nearly instantly (e.g., CLI binary not found, returns exit code 1 before the OS scheduler returns control), the Exited handler fires and calls RaiseRunningCountChanged(), which iterates _handles without finding the new entry. The running count is reported stale, and CliProcessStatusChanged fires with status Fehler before the handle is even in the dictionary, so StopCliAsync and GetLastExitCode would return nothing for the task."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs",
    "line": 16,
    "summary": "_handles is never pruned — completed Process objects accumulate indefinitely, making GetRunningCount() O(N) over all historical tasks.",
    "failure_scenario": "Every StartCliAsync appends an entry to _handles. The Exited handler never removes the entry. Dispose() clears on shutdown only. In a long-running server session with N completed tasks, GetRunningCount() iterates all N entries and calls Process.HasExited on each (a kernel call per entry). RaiseRunningCountChanged is called on every process exit, so this scan runs on the hot path. Process objects (holding a SafeProcessHandle) also accumulate in memory until app shutdown."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs",
    "line": 87,
    "summary": "User-initiated StopCliAsync (Kill) produces exit code != 0, which triggers PersistFehlgeschlagenAsync — a deliberate stop is persisted as a failure.",
    "failure_scenario": "StopCliAsync calls process.Kill() when CloseMainWindow times out. The process exits with a non-zero code. The Exited handler at line 87 checks exitCode != 0, sets status = CliProcessStatus.Fehler, and calls PersistFehlgeschlagenAsync which sets AufgabeStatus.Beendet. There is no flag distinguishing a user abort from a real crash. Tasks explicitly stopped by the user appear in the UI as failed."
  },
  {
    "file": "src/Softwareschmiede/Components/Shared/CliTerminal.razor",
    "line": 26,
    "summary": "Hard-coded ws://localhost:3001 requires a separately running Node.js sidecar — no health-check, no user-facing error if it is down.",
    "failure_scenario": "If the node-pty sidecar (terminal-backend/server.js) is not running, ConnectAsync throws an unhandled WebSocketException inside OnFirstRender/OnAfterRenderAsync. The terminal renders as an empty box with no error message. Any deployment that forgets to start the sidecar silently breaks all CLI terminal output."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs",
    "line": 66,
    "summary": "Double-start guard calls existing.Process.HasExited inside _startLock without try/catch — throws ObjectDisposedException if a previous handle's Process was disposed.",
    "failure_scenario": "IsRunning() (line 41) wraps Process.HasExited in try/catch. The equivalent guard inside StartCliAsync at line 66 does not. If Dispose() was called concurrently and disposed a Process in _handles, the next StartCliAsync for that aufgabeId throws an unhandled ObjectDisposedException inside the _startLock critical section, aborting the start and leaving _startLock released (finally block) but the task stuck with no process."
  },
  {
    "file": "src/Softwareschmiede/Infrastructure/Services/CliSessionService.cs",
    "line": 1,
    "summary": "ICliSessionService / CliSessionService is never registered in DI and never injected anywhere — dead code alongside the WebSocket sidecar approach.",
    "failure_scenario": "CliTerminal.razor bypasses CliSessionService entirely and connects directly to ws://localhost:3001. ICliSessionService has zero usages outside its own files; it is not registered in Program.cs. The fully-formed abstraction (with stdin/stdout, cancellation, async disposal) exists unused while CliTerminal uses a fragile out-of-process sidecar instead. Any future developer seeing both will not know which is the intended path."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs",
    "line": 114,
    "summary": "SettingsViewModel subscribes to DarkModeService.DarkModeChanged with an anonymous lambda but never unsubscribes — event leak if the ViewModel scope changes.",
    "failure_scenario": "DarkModeService is a singleton. SettingsViewModel subscribes at construction with a closure lambda. SettingsViewModel does not implement IDisposable. Currently MainWindowViewModel caches it in a field (_settingsViewModel ??=), so only one instance exists per app lifetime — benign today. If SettingsViewModel is ever registered as transient or a second instance is created, each old instance is kept alive indefinitely by the singleton's event, holding all injected services (AppEinstellungService, ArbeitsverzeichnisSettingsService, DarkModeService) in memory for the app's lifetime."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/AppEinstellungService.cs",
    "line": 132,
    "summary": "SetWindowGeometryAsync issues 4 sequential DB round-trips (SELECT + SaveChanges each) instead of one batch.",
    "failure_scenario": "SetWindowGeometryAsync calls SetIntSettingAsync four times sequentially. Each call executes FirstOrDefaultAsync + SaveChangesAsync — 8 DB operations total. GetWindowGeometryAsync (line 106) already batches the reads into one query as a counterexample. If SetWindowGeometryAsync is called on SizeChanged / LocationChanged events (frequent during window drag), this hammers the SQLite-backed DbContext with 8 sequential operations per event, competing with any concurrent DB access on the scoped context."
  }
]
```

---

## Summary by severity

| # | Severity | File | Finding |
|---|----------|------|---------|
| 1 | **Compile error** | `AufgabeDetail.razor.cs:662` | `GetAvailableAgentsAsync` removed from `IKiPlugin` but still called — app does not build |
| 2 | **Resource leak / crash** | `CliTerminal.razor:44` | `ReceiveLoop` never cancelled on component disposal → `ObjectDisposedException` spam |
| 3 | **Logic race** | `KiAusfuehrungsService.cs:104` | `_handles` written after `process.Start()` → Exited handler fires before handle is stored |
| 4 | **Resource leak** | `KiAusfuehrungsService.cs:16` | `_handles` never pruned → O(N) `GetRunningCount()` and unbounded Process accumulation |
| 5 | **Wrong status** | `KiAusfuehrungsService.cs:87` | User `StopCliAsync`/Kill → exit code != 0 → task persisted as `Beendet` (failure) |
| 6 | **Availability** | `CliTerminal.razor:26` | Hard-coded `ws://localhost:3001` sidecar dependency with no error handling |
| 7 | **Potential crash** | `KiAusfuehrungsService.cs:66` | `HasExited` in double-start guard lacks `try/catch` → `ObjectDisposedException` |
| 8 | **Dead code** | `CliSessionService.cs:1` | `ICliSessionService` never registered or used — orphaned abstraction |
| 9 | **Potential leak** | `SettingsViewModel.cs:114` | `DarkModeChanged` subscription with no unsubscription — safe today, fragile structurally |
| 10 | **Efficiency** | `AppEinstellungService.cs:132` | `SetWindowGeometryAsync` issues 4 sequential DB round-trips instead of one batch |
