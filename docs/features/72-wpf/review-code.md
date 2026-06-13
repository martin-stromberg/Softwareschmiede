# Code Review – Branch 72-wpf

Reviewed: 2026-06-13  
Effort: high (3 correctness + 3 cleanup + 1 altitude angles, 6 candidates each, 1-vote verify)  
Scope: `git diff main...HEAD` + uncommitted working-tree changes

---

## Findings (JSON)

```json
[
  {
    "file": "src/Softwareschmiede.App/Services/WpfAudioService.cs",
    "line": 54,
    "summary": "args.ErrorException dereferenced without null check in MediaFailed handler — NullReferenceException hangs PlayAudioAsync forever",
    "failure_scenario": "WPF's ExceptionEventArgs.ErrorException can be null in certain file-not-found or codec-missing scenarios. Line 54 accesses args.ErrorException.Message unconditionally; if null, a NullReferenceException is thrown inside the MediaFailed callback, which fires before tcs.TrySetException is reached. The TaskCompletionSource is left unresolved: await tcs.Task in PlayAudioAsync hangs forever (or until ct fires), blocking the calling notification pipeline."
  },
  {
    "file": "src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs",
    "line": 219,
    "summary": "_streamingLines is never appended to — IsStreamingContainerVisible is permanently false",
    "failure_scenario": "_streamingLines is reset to [] at the start of each KI run (line ~902) but nothing in the new architecture ever calls Add on it. The old KiAusfuehrungsService had a Subscribe/AddLine pipeline; the new one only manages Process handles and fires no output callbacks into the component. IsStreamingContainerVisible evaluates _streamingLines.Count > 0 and can therefore never be true, so the live output panel is permanently hidden regardless of what the CLI process writes."
  },
  {
    "file": "src/Softwareschmiede/Infrastructure/Services/CliSessionService.cs",
    "line": 130,
    "summary": "ICliSessionService exposes no Stop or Dispose — background I/O loops and the child process are never cleaned up",
    "failure_scenario": "ICliSessionService defines only IsRunning, StartAsync, and SendAsync. CliSessionService owns _loopCts, _process, _outputLoopTask, and _stderrLoopTask but has no Dispose or StopAsync. When the WPF host shuts down (App.xaml.cs line 85: host.StopAsync), the DI container cannot stop registered ICliSessionService instances. The background read loops keep the process handle alive and may log errors after app exit. On the next StartAsync call the guard 'if (IsRunning) return' allows re-use of a process that may actually have exited."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/AufgabeService.cs",
    "line": 338,
    "summary": "AbschliessenAsync no longer clears BranchName or LokalerKlonPfad in the database",
    "failure_scenario": "The old AbschliessenAsync nulled out BranchName and LokalerKlonPfad before saving. The new version only sets Status = Beendet and AbschlussDatum. EntwicklungsprozessService.AbschliessenAsync deletes the directory on disk but also does not clear those DB fields. After task completion the entity retains a stale path to a directory that no longer exists; downstream code that reads LokalerKlonPfad (e.g. git status checks, workspace display, recovery logic) will attempt to operate on a non-existent path and may throw or show incorrect UI."
  },
  {
    "file": "src/Softwareschmiede.App/Services/WpfAudioService.cs",
    "line": 72,
    "summary": "Race condition: Aborted event subscribed after InvokeAsync; dispatcher operation may abort before the handler is attached",
    "failure_scenario": "dispatcher.InvokeAsync() enqueues the operation and returns a DispatcherOperation immediately. The Aborted += handler is subscribed at line 72 after InvokeAsync returns. If the dispatcher aborts the operation in this window (e.g. during shutdown), the event fires before the handler is registered. The status check at line 78 is a partial mitigation but has a TOCTOU race: the operation status can transition to Aborted between the status check and any subsequent await. In this window tcs is never completed and PlayAudioAsync hangs."
  },
  {
    "file": "src/Softwareschmiede.App/Services/WpfBannerService.cs",
    "line": 44,
    "summary": "Toast AppId 'Softwareschmiede' is not a registered AUMID; banner feature silently fails on non-packaged deployments",
    "failure_scenario": "Windows Toast Notifications require the AppId to match either a packaged app AUMID or a Start Menu shortcut registered under that name. On developer machines and xcopy deployments, 'Softwareschmiede' is not registered. ToastNotificationManager.CreateToastNotifier throws a COM exception which is caught and logged only as a warning. Banners never appear, with no visible indicator to the user. The entire IBenachrichtigungsBannerService contract is silently broken by default."
  },
  {
    "file": "src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs",
    "line": 398,
    "summary": "Identical prompt-loading block duplicated in InitialLadenAsync and HandleUpdateAsync",
    "failure_scenario": "The 18-line block that resolves _prompt and _ausfuehrenAbZeitText from VorschlagPrompt / AnforderungsBeschreibung appears identically in two lifecycle methods (line ~398 and ~462). Any future change to the loading logic must be applied in both places; divergence causes inconsistent UI state depending on whether the component is freshly mounted or navigated to. Extract to a private InitialisierePromptAusFeld() method."
  },
  {
    "file": "src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs",
    "line": 961,
    "summary": "ResolveCliName hardcodes plugin names via fragile substring matching instead of using a plugin-provided identifier",
    "failure_scenario": "ResolveCliName returns 'claude' or 'copilot' by checking whether the plugin prefix Contains('ClaudeCli') or Contains('GitHubCopilot'). Adding or renaming a plugin requires editing ResolveCliName separately from the plugin itself. If the prefix naming convention changes (e.g. to 'AnthropicClaude'), the method silently returns null — no terminal label is shown and the CLI terminal panel is hidden even when a CLI plugin is active. The plugin contracts (IKiPlugin / IAiCliProvider) should expose the CLI executable name directly."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs",
    "line": 180,
    "summary": "CLI process exit does not persist a failure status to the database for error exits",
    "failure_scenario": "The process.Exited handler (line ~133) fires CliProcessStatusChanged with CliProcessStatus.Fehler when ExitCode != 0. AufgabeDetail.razor.cs receives this event and sets _fehler text, but neither the event handler nor KiAusfuehrungsService writes a new status to the database. The task remains at InArbeit in the DB after a failed CLI run. On next page load IsStreamingContainerVisible evaluates the DB status, and the recovery / heartbeat checks see an 'active' task with no running process, requiring manual status reset."
  },
  {
    "file": "src/Softwareschmiede.App/Controls/ProcessWindowHost.cs",
    "line": 143,
    "summary": "SetAlwaysOnTopFallback has misleading comment and hardcodes 800×600 without reading actual window dimensions",
    "failure_scenario": "The comment '// SWP_NOMOVE | SWP_NOSIZE ignoriert' suggests the position and size arguments are ignored, but the flag values 0x0002 | 0x0001 ARE SWP_NOMOVE | SWP_NOSIZE — so whether size is ignored depends on correct flag interpretation. If a developer 'corrects' the comment by removing those flags, the embedded window will be forcibly resized to the hardcoded 800×600. Even in the correct interpretation, the fallback always positions the window at (0,0) without reading the actual target position, causing unexpected layout if SetParent fails mid-session."
  }
]
```

---

## Severity Summary

| # | Severity | Category | File |
|---|----------|----------|------|
| 1 | High | Bug – crash / hang | WpfAudioService.cs:54 – null deref in MediaFailed handler |
| 2 | High | Bug – dead feature | AufgabeDetail.razor.cs:219 – streaming panel permanently hidden |
| 3 | High | Bug – resource leak | CliSessionService.cs:130 – no Stop/Dispose on ICliSessionService |
| 4 | Medium | Bug – data integrity | AufgabeService.cs:338 – stale BranchName/LokalerKlonPfad after AbschliessenAsync |
| 5 | Medium | Bug – race condition | WpfAudioService.cs:72 – Aborted event subscribed after InvokeAsync |
| 6 | Medium | Bug – silent failure | WpfBannerService.cs:44 – unregistered Toast AppId, feature dead by default |
| 7 | Low | Cleanup | AufgabeDetail.razor.cs:398 – duplicated prompt-loading block |
| 8 | Low | Cleanup / altitude | AufgabeDetail.razor.cs:961 – hardcoded plugin name strings in ResolveCliName |
| 9 | Low | Bug – missing persistence | KiAusfuehrungsService.cs – error exit does not persist Fehlgeschlagen status |
| 10 | Low | Cleanup | ProcessWindowHost.cs:143 – misleading fallback comment and hardcoded 800×600 |
