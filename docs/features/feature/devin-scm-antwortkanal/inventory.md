# Bestandsaufnahme: Devin CLI im SCM-Antwortkanal

Analysiert wurde der SCM-Antwortkanal (Startvertrag-Auswertung, Prompt-Erkennung, Kanalnachweis,
Zustellpfad) bezogen auf die Unterstützung der Devin-CLI (`Softwareschmiede.Devin`).

## Zusammenfassung

- `ScmCliAccessEvaluator.Evaluate` lehnt jeden Prefix außer `Softwareschmiede.Codex` und
  `Softwareschmiede.ClaudeCli` mit `Unknown` ab (Simulator wird vorab separat behandelt). Für Devin
  gibt es weder Konfigurationserfassung noch eine Parameter-Whitelist.
- `ScmPromptSessionAdapter` erkennt nur Codex-Banner `OpenAI Codex (v0.114.0)`, Claude-Banner
  `Claude Code v2.1.0` und das Simulator-Protokoll `SCM_SIMULATOR_PROTOCOL_V1`. Eingabebereitschaft
  wird über Cursorzeilen-Prompt (`›` bzw. `❯`), Busy-Marker (`esc to interrupt`, `Esc to cancel`)
  und `TrustDialogMarkers` erkannt.
- Der Kanalnachweis ist generisch: `CheckExchangeAccessCoreAsync` sendet bei `Supported` eine
  `ScmExchangeAccessProbe` (kein Ereignisinhalt) und kippt erst auf `Supported`, wenn
  `ReadAccessProbeAsync` den Nachweis liest. Dieser Pfad ist prefixunabhängig und muss nicht
  geändert werden.
- `ScmCliWorkspaceTrust.EnsureTrusted` kennt nur `.claude`/`.codex`-Marker; für Devin existiert kein
  Trust-Vorbelegungsmechanismus. Devin wird vom Plugin bereits mit
  `--respect-workspace-trust false` gestartet (vgl. `BuildIssueTemplateFillInvocation`);
  der Adapter markiert offene Trust-Dialoge ohnehin als eigenen Wartezustand.
- `ScmEventOrchestrator.DispatchCoreAsync` ist prefixagnostisch (Simulator-Sonderfall nur für den
  Startparameter `--scm-exchange`); `item.CliRunId`, Wartegründe und `Zugestellt`-Pfad greifen für
  jede CLI.
- Verifiziertes Devin-Format (installierte `devin.exe` v3000.11.92,
  `C:\Users\Martin\AppData\Local\devin\cli\bin\devin.exe`): Banner `Devin CLI` mit `v3000.11.92`,
  Eingabezeile `❭ <Platzhalter>` (Glyph U+276D), Trust-Dialog `Do you trust the authors of this
  directory?`, Busy-Hinweise `esc twice to interrupt` / `esc again to interrupt`. Permission-Modes
  laut `--help`: `auto`, `accept-edits`, `smart`, `dangerous` (Env `DEVIN_PERMISSION_MODE`);
  der produktiv beobachtete Wert `bypass` ist in dieser Version nicht dokumentiert.
- `DevinPlugin` setzt `spec.Arguments` aus `optionalParameters` (z. B. `--continue` bei Resume) und
  dem Credential `Softwareschmiede.Devin.CommandLineParameters`; es werden keine Umgebungsvariablen
  gesetzt.

## Details

- [Logik](inventory/logic.md) — Evaluator, Adapter, Workspace-Trust, Orchestrator, Plugin
- [Enums und Records](inventory/enums.md) — `ScmExchangeAccessStatus`, `ScmPromptReadyStatus`,
  `ScmExchangeAccess`, `TerminalSessionStartSpec`
- [Tests](inventory/tests.md) — `ScmEventCliExchangeAccessTests`, `ScmDispatchFixture`,
  Dispatch-Tests, Trust-Tests
