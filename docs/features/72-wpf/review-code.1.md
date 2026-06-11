# Code Review – Branch `72-wpf`

Reviewed diff: `main...HEAD`

```json
[
  {
    "file": "src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs",
    "line": 35,
    "summary": "BuildContextFilePath(localRepoPath) increments the context-file index on every call, so the file appended to after a run is never the file read in the next follow-up.",
    "failure_scenario": "Run 1 writes context to claude.context.md (index 1). ResolveContextFilePath is called again for run 2; GetNextContextFileIndex finds index 1 and returns 2, so contextFilePath = claude.context.2.md. AppendContextEntryAsync writes run 2's result into claude.context.2.md. BuildFollowPromptWithContextAsync calls GetLatestContextFilePath, which still returns claude.context.md (index 1 is the highest existing file). Run 3's follow-up is built from run 1's context, silently discarding run 2's context forever."
  },
  {
    "file": "src/Softwareschmiede/Infrastructure/Services/CliSessionService.cs",
    "line": 43,
    "summary": "stderr is redirected but never read, causing a process deadlock when the subprocess writes more than the OS pipe buffer (~4 KB on Windows) to stderr.",
    "failure_scenario": "claude or copilot writes error output (e.g. auth failure, startup diagnostics) exceeding ~4 KB to stderr. The OS write-pipe blocks. The process cannot write to stdout while blocked on stderr. ReadOutputLoop awaits StandardOutput.ReadLineAsync indefinitely. The KI run hangs permanently with no error surfaced to the user."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs",
    "line": 1119,
    "summary": "WriteTextAtomicallyAsync replaces File.Replace (atomic) with File.Delete + File.Move, creating a window where the target file does not exist and a concurrent reader gets an empty/missing file.",
    "failure_scenario": "AppendContextEntryAsync writes a new context version. Between File.Delete and File.Move a second concurrent call (e.g. compression triggered in parallel) calls ReadFileTextSafeAsync; File.Exists returns false and the method returns string.Empty, causing the subsequent write to overwrite all accumulated context with only the new entry. The .bak recovery path that existed before this change was also removed, so the lost data is unrecoverable."
  },
  {
    "file": "src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs",
    "line": 936,
    "summary": "CompressContextAsync tells the AI to 'write the compressed result directly into the file', but the actual file content is not provided to the AI and the file-write instruction is unactionable via the CLI's stdout-capture path.",
    "failure_scenario": "Context exceeds the soft limit. CompressContextAsync sends a prompt that references only the file name without providing the content ('Komprimiere den Inhalt der Datei claude.context.md'). The AI has no access to the file and will respond with an error or fabricated output. The previous implementation passed the file content inline and was reliable. The caller writes whatever the AI prints to stdout back into the file, silently replacing the real context with the AI's confused response."
  },
  {
    "file": "src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs",
    "line": 17,
    "summary": "BuildContextFileName(Guid aufgabeId) silently ignores its parameter and always delegates to index 1, making the overload a misleading no-op that returns the same shared filename regardless of task.",
    "failure_scenario": "A caller passes a specific aufgabeId expecting a task-scoped context file (e.g. {aufgabeId}.claude.context.md as in the old code). The method returns claude.context.md unconditionally. Multiple simultaneous tasks in the same repo share and corrupt each other's context file."
  },
  {
    "file": "src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs",
    "line": 871,
    "summary": "Task.Delay is passed the difference (requestedExecutionUtc - UtcNow) without a zero-floor guard; if the scheduled instant passes between validation and the call, the TimeSpan is negative and Task.Delay throws ArgumentOutOfRangeException.",
    "failure_scenario": "User types '14:30' at 14:29:59. TryResolveRequestedExecutionUtc computes 14:30 today (1 second future). By the time the async code reaches Task.Delay (after StateHasChanged and any awaited UI updates), the clock has passed 14:30. The subtraction yields a small negative TimeSpan. Task.Delay(-1 ms) throws ArgumentOutOfRangeException, which is not caught, causing the Blazor component to crash with an unhandled exception and no user-facing error message."
  },
  {
    "file": "src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs",
    "line": 49,
    "summary": "GetContextFileNames uses SearchOption.AllDirectories then strips the path to just the filename; ClearContextFiles and GetLatestContextFilePath then Path.Combine the bare filename with localRepoPath, producing a wrong path for any matched file in a subdirectory.",
    "failure_scenario": "A project subdirectory (e.g. docs/) contains a file matching the mask claude.context*.md. GetContextFileNames returns 'claude.context.md' (bare name). ClearContextFiles tries to delete localRepoPath/claude.context.md (the root-level file), not localRepoPath/docs/claude.context.md. The subdirectory file is never deleted, pollutes GetNextContextFileIndex with a stale high index, and ClearContextFiles silently fails to clear it."
  },
  {
    "file": "src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs",
    "line": 1024,
    "summary": "AbbrechenWartenAufAusfuehrungszeitpunkt cancels the shared _cts token, which is also used by all git operations, workspace loads, and agent queries — not only the scheduled delay.",
    "failure_scenario": "User clicks 'Warten abbrechen' while a git status check or agent package load is running in the background (both use _cts.Token). _cts.Cancel() fires, cancelling those unrelated async operations. _cts is then replaced with a new CancellationTokenSource, but any already-awaited operations using the old token throw OperationCanceledException and surface as errors, leaving the UI in a partially refreshed state."
  },
  {
    "file": "plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs",
    "line": 27,
    "summary": "RateLimitSuggestionMarker is defined as a separate constant in both ClaudeCliPlugin and EntwicklungsprozessService with identical values; a future change to one without the other silently breaks rate-limit suggestion parsing.",
    "failure_scenario": "Developer changes the marker format in ClaudeCliPlugin (e.g. adds a version prefix) but not in EntwicklungsprozessService. TryParseRateLimitSuggestion stops matching the new lines. Rate-limit events are yielded as raw marker strings to the UI, SavePromptVorschlagAsync is never called, and the user receives no suggestion or scheduled retry — the rate-limit recovery feature silently stops working."
  },
  {
    "file": "plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs",
    "line": 228,
    "summary": "On Windows, the stdin pipe approach constructs the PowerShell inline script with the full cliPrompt embedded between single-quotes, but EscapePowerShellString only escapes single-quote characters, not newlines or other PowerShell metacharacters present in multi-line prompts.",
    "failure_scenario": "cliPrompt contains a newline (which BuildCliPrompt produces via string.Join(Environment.NewLine, lines)). The resulting PowerShell script becomes: $prompt = 'Aktuelle Anfrage: foo.md\nBisheriger Kontext: ...'; echo $prompt | claude ... The newline inside the single-quoted string literal is interpreted by PowerShell as a line continuation, causing a parse error or incomplete prompt. The claude invocation receives an empty or truncated prompt, and the KI run produces an unrelated or empty response."
  }
]
```
