# Tasks: Verlässliche Fortsetzung der CLI-Zustellung nach der Vertrauenswürdigkeitsabfrage

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Adapter | `ScmPromptReadyStatus.TrustPrompt` Enum-Wert ergänzen | Erledigt | `ScmEventCliExchangeAccessTests.TrustPrompt_NeverReceivesProbeOrConfirmation`, `ScmEventOrchestratorTests.TrustDialog_BlocksUntilAnswered_ThenDeliversAutomatically` |
| 2 | Adapter | `RefreshScreen`: Trust-Dialog-Marker → `TrustPrompt` | Erledigt | `ScmEventOrchestratorTests.TrustDialog_BlocksUntilAnswered_ThenDeliversAutomatically` |
| 3 | Adapter | `ConfirmReady`: Bildschirm-Recheck + Ablehnung bei `TrustPrompt` | Erledigt | `ScmEventCliExchangeAccessTests.TrustPrompt_NeverReceivesProbeOrConfirmation`, `ScmEventOrchestratorTests.TrustDialog_BlocksUntilAnswered_ThenDeliversAutomatically` (ConfirmReadyAsync wirft) |
| 4 | Adapter | Kanalprobe: `TimeProvider`, `_accessProbeSentUtc`/`_accessProbeSends`, Resend mit Frist + Kappe | Erledigt | `ScmEventCliExchangeAccessTests.PendingProbe_ResendsAfterDelay_UpToCap`, `ProbeLostToTrustDialog_ResumesAfterAnswer` |
| 5 | Orchestrator | `DispatchCoreAsync`: `TrustPrompt` → eigener Wartegrund | Erledigt | `ScmEventOrchestratorTests.TrustDialog_BlocksUntilAnswered_ThenDeliversAutomatically` (Reason-Text) |
| 6 | Orchestrator | `ConfirmReadyAsync`: `NextAttemptUtc = null` setzen | Erledigt | `ScmEventOrchestratorTests.ConfirmReady_LiftsNextAttemptLock` |
| 7 | Trust-Vorbelegung | `ScmCliWorkspaceTrust` neu (Claude-JSON-Merge, Codex-TOML-Append, Guards) | Erledigt | `ScmCliWorkspaceTrustTests` (7 Fälle) |
| 8 | Trust-Vorbelegung | `ScmCliAccessEvaluator`: `[projects.*]`/`trust_level`-Whitelist | Erledigt | `ScmEventCliExchangeAccessTests.ConfigurationFixtures_EnforceRestrictionsAndPreserveFiles` (neue InlineData-Fälle) |
| 9 | Startpfad | `KiAusfuehrungsService`: `EnsureTrusted` vor `Capture`, `TimeProvider` an Adapter | Erledigt | Kompilierung + Adapter-Tests über injizierten `TimeProvider` (Kein direkter Test des Startpfads) |
| 10 | Tests | `ScmEventOrchestratorTests`: Trust-Wartegrund, ConfirmReady-Reject, Fortsetzung, NextAttempt-Sperre | Erledigt | `TrustDialog_BlocksUntilAnswered_ThenDeliversAutomatically`, `ConfirmReady_LiftsNextAttemptLock` |
| 11 | Tests | `ScmEventCliExchangeAccessTests`: Probe-Resend/-Kappe, Trust-Recovery, `[projects.*]`-Evaluator | Erledigt | 3 neue Theory-Methoden + 4 InlineData-Fälle |
| 12 | Tests | `ScmCliWorkspaceTrustTests`: Merges, Guards, `CODEX_HOME` | Erledigt | `ScmCliWorkspaceTrustTests` (CODEX_HOME-Auflösung indirekt über `EnsureCodexTrusted`-Pfadparameter; direkter CODEX_HOME-Fall über `EnsureTrusted` nicht separat getestet) |
| 13 | Doku | `docs/help/plugins/github-ereignisse/troubleshooting.md` (Trust-/Wartend-Hinweise) + Empfehlung | Erledigt | — |
| 14 | Doku | README/Help-Index prüfen und ggf. aktualisieren | Erledigt | — |
