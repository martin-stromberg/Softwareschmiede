# Code Review — Branch `72-wpf-aufgabendetailansicht`

Reviewed at high effort (3 correctness angles + 3 cleanup angles + 1 altitude angle, 6 candidates each, 1-vote verify). 10 findings survived verification, ranked most-severe first.

---

## Findings

```json
[
  {
    "file": "src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs",
    "line": 343,
    "summary": "CliStartenAsync reloads Aufgabe with GetByIdAsync (shallow) instead of GetDetailAsync (eager-loaded), silently dropping navigation properties after CLI start",
    "failure_scenario": "After setting status to InArbeit, CliStartenAsync reloads this.Aufgabe via _aufgabeService.GetByIdAsync, which issues a bare FirstOrDefaultAsync with no Include calls. All other code paths (LadenAsync, StatusGestartetSetzenAsync, AufgabeAbschliessenAsync) use GetDetailAsync, which eagerly loads Projekt, IssueReferenz, GitRepository (with StartKonfiguration), and Protokolleintraege. After CLI start the ViewModel holds a shallow entity; any binding or logic reading Aufgabe.IssueReferenz, Aufgabe.LokalerKlonPfad (via nav props), or TestErgebnisse silently gets null, causing display failures or NullReferenceExceptions downstream."
  },
  {
    "file": "src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs",
    "line": 25,
    "summary": "Unloaded handler calls vm.Dispose(), but ProjectDetailViewModel already disposes the same instance when SelectedTaskViewModel is reassigned — double-dispose",
    "failure_scenario": "When the user navigates away from a task, ProjectDetailViewModel.SelectedTaskViewModel is set to null. The setter (ProjectDetailViewModel.cs line ~333) calls disposable.Dispose() on the old VM. Simultaneously the View's Unloaded event fires and also calls vm.Dispose(). The same TaskDetailViewModel instance is disposed twice: CancellationTokenSource.Cancel and Dispose are called twice, and _kiService.CliProcessStatusChanged is unsubscribed twice. TaskDetailViewModel has no _disposed guard, so the second Dispose may cancel an already-cancelled CTS and produce an ObjectDisposedException."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs",
    "line": 510,
    "summary": "OnCliProcessStatusChanged calls Application.Current.Dispatcher.Invoke directly, coupling the ViewModel to the WPF Application singleton and breaking unit tests",
    "failure_scenario": "Application.Current is null in any test host that does not spin up a WPF Application. Any unit test exercising the CliProcessStatusChanged event path (e.g. testing that IsCliRunning reacts correctly to a Gestoppt event) throws NullReferenceException. Fix: inject IDispatcher or SynchronizationContext, or marshal in the View layer, not inside the ViewModel."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs",
    "line": 358,
    "summary": "OnCliProcessStatusChanged sets IsCliRunning = false but never clears EmbeddedWindowHandle when the CLI process exits unexpectedly",
    "failure_scenario": "If the CLI process crashes (not stopped via CliStoppenCommand), CliProcessStatusChanged fires and IsCliRunning is set to false — but EmbeddedWindowHandle retains the dead process HWND. ProcessWindowHost remains bound to the stale handle; ResizeEmbeddedWindow and subsequent SetWindowPos/SetParent Win32 calls operate on a zombie HWND. A second CLI start then calls WaitForWindowHandleAsync which writes a new handle over the stale one without DestroyWindowCore cleanup, potentially corrupting the embedded window state."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs",
    "line": 265,
    "summary": "IsLoading race: old LadenAsync's finally block sets IsLoading = false after the new task has already set it to true when AufgabeId changes rapidly",
    "failure_scenario": "AufgabeId setter cancels the old CTS and immediately starts a new LadenAsync. The new task sets IsLoading = true. The old task catches OperationCanceledException and re-throws it (line 285), so its finally block unconditionally sets IsLoading = false. If the old finally runs after the new task's IsLoading = true, the loading indicator disappears while the new load is still in progress, producing a missing or flickering spinner on rapid navigation."
  },
  {
    "file": "src/Softwareschmiede.App/Services/DarkModeService.cs",
    "line": 57,
    "summary": "ApplyTheme() indexes _themeUris[mode] with no ContainsKey guard; any unrecognised string throws KeyNotFoundException at startup",
    "failure_scenario": "SetModeAsync() and InitializeAsync() accept any string from the database without validation. If a persisted DesignMode value is corrupted or carries a variant (e.g. 'dark' lowercase, 'Dark ' with trailing space, or a future value from a newer version), ApplyTheme throws an unguarded KeyNotFoundException on the UI thread. App startup fails, or theme-switching breaks permanently. Fix: TryGetValue with a fallback to 'Dark'."
  },
  {
    "file": "src/Softwareschmiede.App/Services/WpfAudioService.cs",
    "line": 37,
    "summary": "CancellationToken registration cancels the Task but never closes the MediaPlayer, leaking the native media resource",
    "failure_scenario": "When ct fires after playback has started, ct.Register calls tcs.TrySetCanceled — but player.Close() is only in MediaEnded and MediaFailed handlers, which never fire for a still-playing player. The MediaPlayer instance stays in _activePlayers and keeps playing in the background. Each cancelled notification leaks a native DirectShow/Media Foundation session. On app shutdown with queued notifications, multiple sessions accumulate and are never released."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs",
    "line": 48,
    "summary": "ToggleDarkModeCommand is declared but never assigned in the constructor, leaving it permanently null — the dark mode toggle button silently does nothing",
    "failure_scenario": "The constructor assigns ToggleNavigationCommand but not ToggleDarkModeCommand. Any XAML button bound to ToggleDarkModeCommand is permanently disabled (WPF treats null ICommand as CanExecute=false) or throws NullReferenceException if called directly. The user has no way to toggle dark mode from the main window toolbar, with no compile-time or runtime warning."
  },
  {
    "file": ".claude/hooks/test-csharp-startup.ps1",
    "line": 53,
    "summary": "--no-incremental forces a full MSBuild rebuild on every Stop hook invocation, wasting minutes per session",
    "failure_scenario": "Every time Claude finishes a response, the Stop hook runs dotnet build --no-incremental, discarding all incremental state and recompiling every source file. With multiple projects in the solution (main app, tests, integration tests, plugin assemblies), a full rebuild takes 20–60 seconds. Over a session with 30+ tool invocations, this wastes 10–30 minutes of wall-clock time that incremental builds would reduce to near zero. Remove --no-incremental to restore default incremental behavior."
  },
  {
    "file": ".claude/hooks/check_enum_coverage.py",
    "line": 101,
    "summary": "Hook reads all source and test files in the entire solution on every .cs save, causing an O(M*N) blocking scan per edit",
    "failure_scenario": "collect_cs_files walks the entire solution tree and reads every test file into memory before checking any enum coverage. On a solution with 100+ source files and 50+ test files, this runs synchronously on each .cs edit, blocking the tool response for several seconds while rechecking enums in completely unrelated files. Fix: restrict the scan to enums defined in or directly referenced by the edited file."
  }
]
```

---

## Methodology

Phase 0: `git diff main...HEAD` — 80 000+ line diff across 580 files (full branch vs. main).

Phase 1: Seven parallel finder angles run as sub-agents:
- **A** Line-by-line diff scan
- **B** Removed-behavior auditor
- **C** Cross-file tracer
- **D** Reuse
- **E** Simplification
- **F** Efficiency
- **G** Altitude

Phase 2: One verifier per candidate. Refuted findings dropped after reading actual files:
- "EmbeddedWindowHandle never set" — REFUTED: `TaskDetailView.WaitForWindowHandleAsync` polls `process.MainWindowHandle` and sets `vm.EmbeddedWindowHandle` explicitly.
- "DarkModeService.ApplyTheme on wrong thread" — REFUTED: `AsyncRelayCommand` does not use `ConfigureAwait(false)`, so all continuations resume on the captured WPF `SynchronizationContext` (UI thread).
- "WaitForWindowHandleAsync sets handle off-thread" — REFUTED: the method is called from a UI-thread event handler without `ConfigureAwait(false)`, so WPF's `SynchronizationContext` marshals the continuation back to the UI thread.
