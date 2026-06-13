# Code-Review: Branch 72-wpf

Basis: `git diff main...HEAD` (455 geänderte Dateien, ~18 000 Zeilen diff)
Methode: 7 Finder-Winkel (Zeile-für-Zeile, entfernte Invarianten, Cross-File, Wiederverwendung, Vereinfachung, Effizienz, Altitude) × bis zu 6 Kandidaten je Winkel → 1-Vote-Verifikation (recall-biased) → Top 10

---

## Ergebnisse

```json
[
  {
    "file": "src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs",
    "line": 203,
    "summary": "Calls to deleted KiAusfuehrungsService methods (GetBufferedLines, SessionBereinigen, Subscribe, StartKiLauf) cause compile errors and dead KI execution",
    "failure_scenario": "Lines 203, 916, 919, 937, 1040 call GetBufferedLines(), SessionBereinigen(), Subscribe(), and StartKiLauf() on the rewritten KiAusfuehrungsService, which no longer exposes any of these methods. The project does not compile; the entire KI-launch and live-streaming flow is broken. Navigating back to a running task would also lose buffered output with no replacement."
  },
  {
    "file": "src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs",
    "line": 727,
    "summary": "UpdateAsync called with 6 arguments (including removed agentenpaketName/agentenName) but new signature only accepts 4 — compile error",
    "failure_scenario": "Lines 727-733 and 802-808 pass Id, titel, anforderung, AgentenpaketName, AgentenName, KiPluginPrefix to AufgabeService.UpdateAsync. The new signature is (id, titel, anforderungsBeschreibung, kiPluginPrefix?, ct). CS1503/CS7036 compile error; both 'save requirement' and 'save before start' paths are broken. Even after fixing, AgentenpaketName/AgentenName would silently stop being persisted."
  },
  {
    "file": "src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs",
    "line": 1702,
    "summary": "AbbrechenAsync() calls AbschliessenAsync() — the cancel action marks the task as completed (Beendet) with AbschlussDatum, indistinguishable from a genuine completion",
    "failure_scenario": "User clicks 'Abbrechen' intending to discard the current run. AufgabeService.AbschliessenAsync is called, setting Status=Beendet and AbschlussDatum=now. The UI shows 'Aufgabe abgebrochen.' but the database record is identical to a successful completion. No way to distinguish cancelled from completed in history, reports, or archiving workflows."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs",
    "line": 168,
    "summary": "RaiseRunningCountChanged() reads and writes _previousRunningCount without synchronisation — called from both the Exited event (thread pool) and StartCliAsync (under _startLock, which is already released when Raise is called)",
    "failure_scenario": "Two CLI processes exit within milliseconds. Both Exited handlers read _previousRunningCount=2, both call GetRunningCount()=0, both write 0 back, both fire RunningCountChanged(2,0). Subscribers never see the intermediate count of 1; UI badges or notification triggers misfire. Also, GetRunningCount() calls Process.HasExited inside this unguarded path — if a Process is disposed concurrently, InvalidOperationException propagates unhandled on the thread pool and crashes the application."
  },
  {
    "file": "src/Softwareschmiede.App/Services/WpfAudioService.cs",
    "line": 43,
    "summary": "MediaPlayer is added to _activePlayers before Open() is called; if Open() throws (e.g. relative path passed to UriKind.Absolute), the player is never removed and its COM handle leaks permanently",
    "failure_scenario": "Caller passes a relative path like 'sounds/ping.wav'. new Uri(filePath, UriKind.Absolute) throws UriFormatException synchronously. The catch block calls tcs.TrySetException but does not call _activePlayers.Remove(player) or player.Close(). Each failed invocation leaks one MediaPlayer COM object into the singleton's _activePlayers set for the lifetime of the application, eventually exhausting handles."
  },
  {
    "file": "src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs",
    "line": 1023,
    "summary": "AbbrechenWartenAufAusfuehrungszeitpunkt() disposes _cts while it is still captured by in-flight async operations, risking ObjectDisposedException",
    "failure_scenario": "User clicks 'Warten abbrechen' while LadeWorkspaceAsync is awaiting GitWorkspaceBrowserService.LoadSnapshotAsync(_cts.Token). _cts.Dispose() is called (line 1028) and _cts is replaced (line 1029). The disposed CancellationTokenSource's token is still observed by a WaitHandle inside the linked registration; accessing it after Dispose throws ObjectDisposedException from within the awaiting service call, propagating as an unhandled exception."
  },
  {
    "file": "src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailGitActionsBunitTests.cs",
    "line": 880,
    "summary": "Test references removed enum value AufgabeStatus.InBearbeitung — compile error in the test project",
    "failure_scenario": "The enum value InBearbeitung no longer exists (renamed to Gestartet/ArbeitsverzeichnisEingerichtet). CS0117 compile error; the entire AufgabeDetailGitActionsBunitTests class fails to build, silently dropping its test coverage."
  },
  {
    "file": "src/Softwareschmiede/Infrastructure/Services/CliSessionService.cs",
    "line": 47,
    "summary": "ReadOutputLoop and DrainStderrLoop are fire-and-forget Tasks with no CancellationToken and no stored reference — they outlive their owning service instance and deliver stale output after a restart",
    "failure_scenario": "A CLI session crashes and the caller re-invokes StartAsync. The old ReadOutputLoop is still alive (no cancellation path), still routing output from the defunct process through the _onOutput delegate, which now points at the new session's handler. Output from the dead process is injected into the new session's log, corrupting the conversation display."
  },
  {
    "file": "src/Softwareschmiede.App/Services/WpfBannerService.cs",
    "line": 27,
    "summary": "ct.ThrowIfCancellationRequested() in a non-async Task method throws synchronously instead of returning a faulted Task — callers using try/await/catch(OperationCanceledException) will not catch it",
    "failure_scenario": "If ct is already cancelled when ShowAsync is called, ThrowIfCancellationRequested() throws OperationCanceledException synchronously before the method returns any Task. Since the method is not async, the exception propagates to the caller's synchronous frame, not into the awaited Task. Callers that do 'await bannerService.ShowAsync(msg, ct)' inside a try/catch(OperationCanceledException) will not catch the exception, causing it to propagate further up the call stack unhandled."
  },
  {
    "file": "src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs",
    "line": 964,
    "summary": "ResolveCliName() hardcodes plugin-prefix string matching in the UI layer instead of reading CliKiPluginBase.ProviderDateiPraefix from the already-loaded plugin list",
    "failure_scenario": "A new CLI plugin (e.g. AmazonQ) is added with its own ProviderDateiPraefix. ResolveCliName returns null because neither 'ClaudeCli' nor 'GitHubCopilot' is matched. ShowCliTerminal evaluates to false; the embedded terminal panel is silently absent for all tasks using the new plugin, with no error or diagnostic message. Developer must remember to also update this UI method on every new plugin addition."
  }
]
```

---

## Schweregrad-Einstufung

| # | Schwere | Kategorie |
|---|---------|-----------|
| 1 | Kritisch – kompiliert nicht | Correctness (entfernte API) |
| 2 | Kritisch – kompiliert nicht | Correctness (Signatur-Mismatch) |
| 3 | Hoch – falsches Verhalten | Correctness (Semantik) |
| 4 | Hoch – Race Condition / Crash | Correctness (Thread-Safety) |
| 5 | Hoch – Resource Leak | Correctness (Memory/Handle) |
| 6 | Mittel – ObjectDisposedException | Correctness (Async/CTS) |
| 7 | Mittel – kompiliert nicht (Tests) | Correctness (Enum-Umbenennung) |
| 8 | Mittel – Output-Korruption | Correctness (Lifetime) |
| 9 | Niedrig – Exception-Handling | Correctness (async void Ersatz) |
| 10 | Niedrig – Wartbarkeit | Altitude (Plugin-Abstraktion) |
