# Tasks: Verlässliche Fortsetzung der CLI-Zustellung nach der Vertrauenswürdigkeitsabfrage

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Adapter | `ScmPromptReadyStatus.TrustPrompt` Enum-Wert ergänzen | Offen | — |
| 2 | Adapter | `RefreshScreen`: Trust-Dialog-Marker → `TrustPrompt` | Offen | — |
| 3 | Adapter | `ConfirmReady`: Bildschirm-Recheck + Ablehnung bei `TrustPrompt` | Offen | — |
| 4 | Adapter | Kanalprobe: `TimeProvider`, `_accessProbeSentUtc`/`_accessProbeSends`, Resend mit Frist + Kappe | Offen | — |
| 5 | Orchestrator | `DispatchCoreAsync`: `TrustPrompt` → eigener Wartegrund | Offen | — |
| 6 | Orchestrator | `ConfirmReadyAsync`: `NextAttemptUtc = null` setzen | Offen | — |
| 7 | Trust-Vorbelegung | `ScmCliWorkspaceTrust` neu (Claude-JSON-Merge, Codex-TOML-Append, Guards) | Offen | — |
| 8 | Trust-Vorbelegung | `ScmCliAccessEvaluator`: `[projects.*]`/`trust_level`-Whitelist | Offen | — |
| 9 | Startpfad | `KiAusfuehrungsService`: `EnsureTrusted` vor `Capture`, `TimeProvider` an Adapter | Offen | — |
| 10 | Tests | `ScmEventOrchestratorTests`: Trust-Wartegrund, ConfirmReady-Reject, Fortsetzung, NextAttempt-Sperre | Offen | — |
| 11 | Tests | `ScmEventCliExchangeAccessTests`: Probe-Resend/-Kappe, Trust-Recovery, `[projects.*]`-Evaluator | Offen | — |
| 12 | Tests | `ScmCliWorkspaceTrustTests`: Merges, Guards, `CODEX_HOME` | Offen | — |
| 13 | Doku | `docs/help/plugins/github-ereignisse/troubleshooting.md` (Trust-/Wartend-Hinweise) + Empfehlung | Offen | — |
| 14 | Doku | README/Help-Index prüfen und ggf. aktualisieren | Offen | — |
