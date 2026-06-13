# Code Review: Branch 72-wpf-redesign

```json
[
  {
    "file": "src/Softwareschmiede.App/App.xaml.cs",
    "line": 74,
    "summary": "Scoped services resolved from the root IServiceProvider become effective singletons sharing one DbContext for the entire app lifetime.",
    "failure_scenario": "MainWindow is resolved from _host.Services (root provider, line 74). MainWindowViewModel, ProjectDetailViewModel, and ProjectListViewModel all receive and store this root IServiceProvider. When ProjectDetailViewModel calls _serviceProvider.GetRequiredService<TaskDetailViewModel>() (ViewModels/ProjectDetailViewModel.cs), every Scoped dependency — AufgabeService, EntwicklungsprozessService, ProtokollService, IGitPlugin — is resolved from the root scope rather than a child scope, turning them into de-facto singletons. The single shared SoftwareschmiededDbContext is then used concurrently across multiple TaskDetailViewModel instances (e.g., LadenAsync on two open tasks simultaneously), triggering Entity Framework's 'A second operation was started on this context before a previous operation completed' exception."
  },
  {
    "file": "src/Softwareschmiede.App/App.xaml.cs",
    "line": 145,
    "summary": "IGitPlugin Scoped factory throws InvalidOperationException when no SCM plugin is installed, crashing DI activation of EntwicklungsprozessService on first scope resolution.",
    "failure_scenario": "The factory `sp => sp.GetRequiredService<IPluginManager>().GetDefaultSourceCodeManagementPlugin()` calls PluginManager.GetDefaultSourceCodeManagementPlugin() (PluginManager.cs line 48), which throws `InvalidOperationException('Kein Source-Code-Management-Plugin verfügbar.')` when _gitPlugins is empty (plugins/ directory absent or empty). Because EntwicklungsprozessService injects IGitPlugin non-optionally, every navigation to a task detail view fails with an opaque DI activation exception on a fresh installation without pre-built plugin DLLs."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs",
    "line": 338,
    "summary": "OnCliProcessStatusChanged sets WPF-bound properties from a thread-pool thread (Process.Exited), violating WPF's cross-thread access rule.",
    "failure_scenario": "KiAusfuehrungsService.StartCliAsync attaches process.Exited (KiAusfuehrungsService.cs line 79), which fires on a thread-pool thread. From there, CliProcessStatusChanged is invoked (line 96), which calls TaskDetailViewModel.OnCliProcessStatusChanged, which sets IsCliRunning via SetProperty, raising PropertyChanged. Controls bound to KannCliStarten (derived from IsCliRunning) can throw InvalidOperationException on non-UI threads. Additionally, a race between process exit and TaskDetailViewModel.Dispose() (unsubscribe at line 349) means the handler may run on a disposed instance, accessing _aufgabeId and _isCliRunning while the UI thread is tearing down."
  },
  {
    "file": "src/Softwareschmiede.App/Services/WpfAudioService.cs",
    "line": 69,
    "summary": "MediaPlayer added to _activePlayers is never removed when CancellationToken fires before MediaEnded, leaking native audio resources in the Singleton for the app lifetime.",
    "failure_scenario": "If the caller cancels ct before MediaEnded fires, the CancellationTokenRegistration at line 37 calls tcs.TrySetCanceled, and PlayAudioAsync returns via OperationCanceledException. The MediaPlayer was added to _activePlayers at line 69 but the only Remove call is inside the MediaEnded handler (line 47), which is never reached. Because WpfAudioService is a Singleton, _activePlayers accumulates abandoned MediaPlayer COM objects. In a task-management app with frequent notifications cancelled by navigation, this becomes a growing leak of audio engine handles and file locks on audio files."
  },
  {
    "file": "src/Softwareschmiede.App/Controls/ProcessWindowHost.cs",
    "line": 107,
    "summary": "GetWindowLong failure is detected by checking for return value 0, but 0 is a valid window style value, producing false positives and masking real failures.",
    "failure_scenario": "A window with no style flags returns 0 from GetWindowLong legitimately, causing EmbedWindow to log 'GetWindowLong fehlgeschlagen' on a perfectly valid window. Conversely, if GetWindowLong actually fails and returns 0 with an error code, the code continues to SetWindowLong with a stale style value of 0, stripping all existing window styles and potentially making the embedded window invisible or non-functional. The correct pattern is to call SetLastError(0) before GetWindowLong and check Marshal.GetLastWin32Error() != 0 after."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs",
    "line": 83,
    "summary": "Cached top-level ViewModels (DashboardViewModel, ProjectListViewModel, SettingsViewModel) are never disposed, leaking event subscriptions and root-scope service references.",
    "failure_scenario": "NavigateToDashboard, NavigateToProjectList, and NavigateToSettings cache their ViewModels with `??=` (lines 83, 92, 101). MainWindowViewModel.Dispose() only unsubscribes from DarkModeChanged and does not call Dispose() on any cached ViewModel. If DashboardViewModel or ProjectListViewModel holds a CancellationTokenSource for an in-progress LadenAsync or subscribes to a Singleton event (e.g., via nested TaskDetailViewModels), those resources leak for the entire app lifetime. Combined with the root-provider issue, this also means the root-scope DbContext is kept alive and in use indefinitely."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/PluginSettingsViewModel.cs",
    "line": 1604,
    "summary": "LadenAsync sets IsLoading = true then immediately resets it to false without any await, so the loading indicator never renders.",
    "failure_scenario": "The method body contains no await expression — it returns Task.CompletedTask at the end. Both IsLoading = true and IsLoading = false are dispatched in the same synchronous call. WPF coalesces the rendering pass and the loading spinner bound to IsLoading never becomes visible, giving users no feedback during what could be a blocking plugin discovery operation in the future."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/NavigationViewModel.cs",
    "line": 36,
    "summary": "Toggle() fires OnPropertyChanged(nameof(NavigationWidth)) twice — once explicitly and once via the IsExpanded setter.",
    "failure_scenario": "The IsExpanded setter (line 16-18) already calls OnPropertyChanged(nameof(NavigationWidth)) when the value changes. Toggle() then calls it again explicitly at line 36, causing every sidebar toggle to dispatch two redundant binding notifications, triggering two layout measure/arrange passes on all controls bound to NavigationWidth."
  },
  {
    "file": "src/Softwareschmiede.App/Controls/ProcessWindowHost.cs",
    "line": 115,
    "summary": "SetWindowLong return value 0 is checked for failure without first clearing SetLastError, causing stale error codes to produce spurious failure logs.",
    "failure_scenario": "If the window's previous style value happened to be 0, SetWindowLong returns 0 on success. The code then calls Marshal.GetLastWin32Error() to distinguish success-with-0 from failure, but without first calling SetLastError(0) before SetWindowLong, the error code may be a stale value from a prior API call that failed, triggering a false 'SetWindowLong fehlgeschlagen' log entry while the embedding actually succeeded."
  },
  {
    "file": "src/Softwareschmiede.App/Services/WpfAudioService.cs",
    "line": 15,
    "summary": "_activePlayers HashSet has no thread-safety and no IDisposable cleanup, leaving MediaPlayer COM objects unreleased on app shutdown.",
    "failure_scenario": "WpfAudioService is registered as Singleton with no IDisposable implementation. On shutdown, if audio is in progress (players in _activePlayers), their COM objects and audio engine handles are never released via player.Close(). On a system with strict audio resource limits or during rapid repeated launches (e.g., in automated tests), this can exhaust audio engine slots or leave file handles open on audio files."
  }
]
```
