# Bestandsaufnahme: Tests

## Testklassen

### `ScmEventCliExchangeAccessTests` (`src/Softwareschmiede.IntegrationTests/Services/`, partial)
- `ReadOnlySimulator_ReportsChannelDenialWithoutLosingPromptRecognition` — Simulator-Readonly → `Denied`, Prompt-Erkennung bleibt.
- `ActualStartSpec_AllowsWorkspaceChannelWithoutAddingBypass` (Theory Codex/Claude) — voller Adapterpfad: `Unknown` bis Probe, Probe-Prompt ohne `request`, außerhalb liegender Kanal → `Denied`, `WritePromptAsync` wirft vor Nachweis.
- `ActualStartSpec_RejectsOverridesWithoutMutatingThem` (Theory) — `read-only` → `Denied`; `--cd`, `--profile`, `-c`-Overrides, Claude `default`/`plan`/Tool-/Settings-Overrides, `bypassPermissions` → `Unknown`.
- `ConfigurationFixtures_EnforceRestrictionsAndPreserveFiles` (Theory) — Konfigurations-Fixtures → `Supported`/`Denied`/`Unknown`; Dateiinhalte bleiben bytegleich.
- `LivingRun_RequiresUnchangedCapturedConfiguration` (Theory) — geänderte Konfiguration und Laufwechsel → `Unknown`; `CompleteProbeAsync` vollendet die Probe deterministisch.
- `.PendingProbe.cs`: `PendingProbe_ResendsAfterDelay_UpToCap`, `ProbeLostToTrustDialog_ResumesAfterAnswer`, `TrustPrompt_NeverReceivesProbeOrConfirmation` (alle Theory Codex/Claude).
- `.RestrictedProcess.cs`: `RestrictedWindowsProcess_ReadsWritesAndCompletesChannel_ButCannotWriteAppData`, `RestrictingSid_RejectsAccountsAndBroadPrincipals`, `RestrictedWindowsProcess_WithoutDedicatedCapability_DeniesChannel` (echte eingeschränkte Windows-Kindprozesse).

### `ScmEventDispatchTests` + weitere (`src/Softwareschmiede.IntegrationTests/Services/`)
- `Monitor_ProcessesPastOneHundredWaitingEvents`, `ConcurrentDispatchAndManualStart_...` — vollständiger Dispatch bis `Zugestellt` über `ScmDispatchFixture` (KI-Plugin = Simulator-Prefix).

### `ScmCliWorkspaceTrustTests` (`src/Softwareschmiede.Tests/Application/Services/`)
- Merge-/Guard-Fälle für `EnsureClaudeTrusted`/`EnsureCodexTrusted`.

### `ScmEventOrchestratorTests`, `ScmEventExchangeServiceTests`, `E2E_ScmEvents`, `E2E_ScmRegressionSimulator`
- Orchestrator-/Exchange-Verhalten und FlaUI-E2E mit Simulator.

## Hilfsmethoden

### `ScmDispatchFixture` (`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`)
- `KiPrefix` = `Softwareschmiede.KiSimulator` (konstant); `Ki`-Mock liefert StartSpec mit `SOFTWARESCHMIEDE_SCM_SIMULATOR=1`; `ITerminalSession`-Mock mit `TerminalBuffer`-Screen (`SCM_SIMULATOR_PROTOCOL_V1` + `SCM_READY_V1`) und `WritePromptAsync` → `Prompts`-Liste.
- `AddAsync(status, configured, cloned)` — Projekt/Repository/Aufgabe + eingeliefertes Issue-Kommentar-Ereignis.
- `DispatchAsync(id)` — `ScmEventOrchestrator.DispatchAsync`.
- `SetPrompt(task, prompt)` — ersetzt Buffer-Screen.
- `InitializeAsync`, `EventAsync`, `DisposeAsync` (killt gestartete `cmd.exe`-Platzhalterprozesse).

### `ScmEventCliExchangeAccessTests`-Helfer
- `Spec(provider, arguments)` — baut echte `CodexPlugin`/`ClaudeCliPlugin`-StartSpecs über gemockten `ICredentialStore` (`{prefix}.CommandLineParameters`).
- `Session(banner, prompt)` — `ITerminalSession`-Mock: `TerminalBuffer(120,10)` mit Banner + Cursor auf Zeile 2 + Promptzeile; `Process` = aktueller Prozess.
- `SetScreen(session, banner, prompt)` — Buffer austauschen + `BufferChanged` raisen.
- `CompleteProbeAsync(prompt)` — führt die Dateioperationen der Kanalprobe direkt aus (Challenge lesen → `temporaryProofPath` schreiben → `proofPath`/`readChallengePath` umbenennen).
- `GitInit(directory)` — `git init --quiet`.
