# Tests und Infrastruktur

Zurück: [../inventory.md](../inventory.md)

## Bestehende Abdeckung (verifiziert)

### `src/Softwareschmiede.Tests` (stabiler Lane: `--filter "Category!=OsInterface"`)

- `Application/Services/ScmEventOrchestratorTests.cs` (107 Zeilen): MissingWriteAccess-Reason, NoDefault-Aktivierung, BlockedTask-Theory, `Confirmation_ChannelFailureThenManualInput_DoesNotAuthorizeLaterUnknownPrompt` (ConfirmReady → `InvalidOperationException` bei Wieder-`Unknown`).
- `Helpers/ScmDispatchFixture.cs`: echte SQLite-DB + echte Services; Mock-`IKiPlugin` Prefix `Softwareschmiede.KiSimulator`; `ITerminalSessionFactory`-Mock mit echtem `cmd.exe`-Trägerprozess; `SetPrompt(taskId, prompt)` schaltet den `TerminalBuffer` um (Row 0 `SCM_SIMULATOR_PROTOCOL_V1`, Cursorzeile frei belegbar — ein Trust-Dialog-Text ist als Buffer-Inhalt simulierbar); `Prompts`-Liste protokolliert `WritePromptAsync`. Konstruktor akzeptiert optionalen `TimeProvider` (Z. 36, 88).
- Simulator-Pfad: `PluginPrefix == "Softwareschmiede.KiSimulator"` **überspringt** die Dateitool-Probe (`ScmPromptSessionAdapter.cs:150`) — der Probe-Deadlock ist mit dem Simulator **nicht** abbildbar; dafür die Integrationstests nutzen.

### `src/Softwareschmiede.IntegrationTests` (CI-Lane `Category!=OsInterface` vorhanden)

- `Services/ScmEventCliExchangeAccessTests.cs` (186 Zeilen): echte `CodexPlugin`/`ClaudeCliPlugin`-Specs via `GetTerminalStartSpecAsync(_root)` + `ICredentialStore`-Mock für `CommandLineParameters`; `Session(banner, prompt)`-Mock mit `TerminalBuffer`; `GitInit(_root)` (für `EnsureGitExcludedAsync` nötig); **`CompleteProbeAsync`** (Z. 150-158) führt den Probe-Prompt wie ein echtes Dateitool aus (challenge lesen → proof.tmp → proof.json → challenge→challenge-read umbenennen) — ein „Probe bleibt aus"-Szenario = `WritePromptAsync` ohne Completion.
- `Services/ScmEventCliExchangeAccessTests.RestrictedProcess.cs`, `ScmRestrictedWindowsProcess.cs`: echte Windows-ACLs/Prozesse.
- Evaluator-Matrix: `ActualStartSpec_RejectsOverridesWithoutMutatingThem`, `ConfigurationFixtures_EnforceRestrictionsAndPreserveFiles` — `[projects.*]`-Whitelist direkt als `InlineData`-Fälle erweiterbar (`ConfigurationFixtures` nutzt Datei unter `_root`).

### Projektregeln

- Voll-Build vor Tests, nie `--no-build`; `dotnet test` synchron; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` inline bei jedem Lauf.
- FlaUI-E2E minimal konsolidiert; `Softwareschmiede.App.exe` nie beenden; bei Dateilocks Teil-Build (`dotnet build src/Softwareschmiede/Softwareschmiede.csproj`).
- Deutsche UI-/Status-/Kommentartexte; SCM-Status englische String-Keys.

## Hilfsmethoden

### `ScmDispatchFixture`
- `AddAsync(status, configured, cloned)` → `(TaskId, EventId)`; `DispatchAsync(eventId)`; `EventAsync(eventId)`; `SetPrompt(taskId, prompt)`; `Sessions[taskId]` (Mock); `Prompts`; `Responses`; `CloneGate`.

### `ScmEventCliExchangeAccessTests`
- `Spec(provider, arguments)` → `(Prefix, TerminalSessionStartSpec)`; `Session(banner, prompt)` → `Mock<ITerminalSession>`; `CompleteProbeAsync(prompt, ct)`; `GitInit` (partiell, in `RestrictedProcess`-Datei bzw. derselben Klasse).
