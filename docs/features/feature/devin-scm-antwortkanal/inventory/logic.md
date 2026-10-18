# Bestandsaufnahme: Logik

## `ScmCliAccessEvaluator`
Datei: `src/Softwareschmiede/Application/Services/ScmCliAccessEvaluator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Capture(prefix, spec)` | public static | Erfasst Codex- (`config.toml`, `requirements.toml`, `managed_config.toml` in `CODEX_HOME`/`~/.codex`/Managed/Projektkette) und Claude-Konfigurationen (`settings.json` in `CLAUDE_CONFIG_DIR`/`~/.claude`, Projekt-`.claude`, Managed). Für andere Prefixe leere Liste. |
| `IsUnchanged(files)` | public static | Bytegleicher Vergleich der erfassten Dateien; I/O-Fehler gelten als geändert. |
| `Evaluate(prefix, spec, runId, files)` | public static | Klassifiziert `Supported`/`Denied`/`Unknown`. Simulator-Sonderpfad (Env `SOFTWARESCHMIEDE_SCM_SIMULATOR`/`_READONLY`), dann Prefix-Gate (Zeile 94): alles außer Codex/Claude → `Unknown` „kein Antwortkanal-Adapter verfügbar". Dateien mit `managed` im Namen oder `requirements.toml` → `Unknown`. Codex: TOML-Whitelist (`sandbox_mode`, `[sandbox_workspace_write]`, `[features]` shell_tool/apply_patch_freeform, `[projects.*] trust_level`); Claude: JSON-Whitelist (`permissions.defaultMode`/`deny`/`ask`/`allow`, `model`, `$schema`). Token-Whitelist: `--model`/`-m` (beide), Codex `--sandbox`/`-s`/`--ask-for-approval`/`-a`, Claude `--permission-mode`. Ergebnis: Codex `workspace-write` / Claude `acceptEdits` → `Supported`; sonst `Unknown`. |
| `ReadConfiguration(path)` | private static | `EnsureSafePath`, ≤ 1 MiB, fehlende Datei → `null`. |
| `Tokenize(arguments)` | private static | Whitespace-/Quote-Tokenizer; Steuerzeichen und unvollständige Quotes → `ArgumentException`. |

## `ScmPromptSessionAdapter`
Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| Konstruktor | public | Bindet Lauf/Spec/Session, wertet `ScmCliAccessEvaluator.Evaluate` einmal aus, abonniert `OutputChunk`/`BufferChanged`/`Exited`/`Failed`, initiales `RefreshScreen()`. |
| `WaitForInputReadyAsync` | public | Liefert Ready/Exited sofort, sonst wartet auf Buffer-Signal oder 2 s. |
| `CheckExchangeAccessAsync` | public | Serialisiert über `ScmTaskCoordinator` auf `CheckExchangeAccessCoreAsync`. |
| `CheckExchangeAccessCoreAsync` | private | Lebende Sitzung + unveränderte Konfiguration + Kanalpfad unterhalb der Schreibwurzel + `_recognizedCli` nötig; bei `Supported` (außer Simulator) wird `ScmRestrictedProcessAccess.CaptureWorkspaceCapabilities` gelesen und die `ScmExchangeAccessProbe` vorbereitet/gesendet (max. 3 Sends, Resend ≥ 60 s); erst `ReadAccessProbeAsync` bestätigt `Supported`. |
| `SendAccessProbeAsync` | private | `MarkInputStarted` + `Session.WritePromptAsync(BuildAccessProbePrompt)`; kein Ereignisinhalt. |
| `ConfirmReady(runId)` | public | Manuell bestätigte Bereitschaft; nur bei erkannter CLI, nicht Busy/TrustPrompt. |
| `MarkInputStarted` | public | Setzt Busy und verwirft manuelle Bestätigung; wird vom Host bei jeder Eingabe gerufen (`session.InputStarted`). |
| `WritePromptAsync` | public | Steuerzeichen-Verbot, erneute `CheckExchangeAccessAsync` (muss `Supported`), Ready-Recheck, `MarkInputStarted`, dann `Session.WritePromptAsync`. |
| `OnOutput` → `RecognizeStartup` | private | Startup-Puffer bis 32 KiB; `_recognizedCli` bei Codex-Banner `OpenAI Codex (v0.114.0)`, Claude `Claude Code v2.1.0`, Simulator `SCM_SIMULATOR_PROTOCOL_V1` (nur bei Supported/Denied-Zugriff). |
| `RefreshScreen` | private | Buffer → Zeilen; `RecognizeStartup`; vorzeitiger Ausstieg ohne `_recognizedCli`/bei manueller Bestätigung/ohne neue Ausgabe; Cursorzeile = Prompt; `TrustDialogMarkers` → `TrustPrompt`; Simulator `SCM_READY_V1`/`SCM_BUSY_V1`; Busy-Marker `esc to interrupt`/`Esc to cancel`; Codex `›`-Varianten, Claude `❯` → `Ready`; sonst `Unknown`. |
| `OnBufferChanged`/`OnExited`/`OnFailed`/`Dispose` | private | Revisionszähler, `Exited`-Status, Event-Abmeldung. |

`TrustDialogMarkers` (private static): `do you trust the files in this folder`,
`is this a project you created or one you trust`, `do you trust the contents of this directory`.

## `ScmCliWorkspaceTrust`
Datei: `src/Softwareschmiede/Application/Services/ScmCliWorkspaceTrust.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureTrusted(pluginPrefix, spec)` | public static | Marker `.claude`/`.codex`; andere Prefixe → No-Op. Verzeichnisse mit projektgebundener Konfiguration behalten den manuellen Dialog. |
| `EnsureClaudeTrusted`/`EnsureCodexTrusted` | internal static | Schreibt `hasTrustDialogAccepted` bzw. `trust_level = "trusted"`, ohne bestehende Entscheidungen zu ändern. |

Aufgerufen aus `KiAusfuehrungsService` (Zeile 239) vor `ScmCliAccessEvaluator.Capture`.

## `ScmEventOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DispatchAsync`/`OpenFromQueueAsync`/`DispatchCoreAsync` | public/private | Zustellpfad: Aufgabe starten (`ProzessStartenUndCliStartenAsync`/`CliNeustartenAsync`; Simulator-Sonderparameter `--scm-exchange` bei Prefix-Match „KiSimulator"), `adapter.WaitForInputReadyAsync` (TrustPrompt/Busy → Wartegrund), `CheckExchangeAccessAsync` (≠ Supported → Wartegrund „Antwortkanal … nicht verfügbar"), `exchange.PrepareAsync`, `adapter.WritePromptAsync` → Status `Zugestellt`. Prefixagnostisch. |
| `ConfirmReadyAsync`/`ResendAsync`/`RetryAsync` | public | Manuelle Bestätigung/Neuversand gebunden an sichtbaren Lauf. |

## `DevinPlugin`
Datei: `plugins/Softwareschmiede.Plugin.Devin/DevinPlugin.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PluginPrefix` | override | `"Softwareschmiede.Devin"`. |
| `BuildProcessStartInfo` | protected override | `devin.exe`, `WorkingDirectory = localRepoPath`, `Arguments = parameters` + `AppendCommandLineParameters` (Credential `Softwareschmiede.Devin.CommandLineParameters`). |
| `BuildIssueTemplateFillInvocation` | internal | Einmal-Lauf `-p` + `--respect-workspace-trust false` + CommandLineParameters. |
| `SupportsSessionContinuation` | override | `true` → Host übergibt `--continue` als `optionalParameters`. |
| `TerminalCapabilities` | override | `RequiresPty \| SupportsPty`. |

## `KiAusfuehrungsService` (Auszug)
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

- Zeile 238–247: `GetTerminalStartSpecAsync` → `ScmCliWorkspaceTrust.EnsureTrusted` → `ScmCliAccessEvaluator.Capture`.
- Zeile 307–309: `new ScmPromptSessionAdapter(...)`, `session.InputStarted = adapter.MarkInputStarted`.
