# Tasks: Devin CLI im SCM-Antwortkanal

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Evaluator | `ScmCliAccessEvaluator.Capture`: Devin-Konfigurationspfade erfassen (`~/.devin/config.json`, `~/.config/devin/config.json`, `<workdir>/.devin/config.json`) | Erledigt | `ScmEventDispatchTests_Devin.DevinTask_*` (Capture läuft im realen `KiAusfuehrungsService`-Pfad) |
| 2 | Evaluator | `ScmCliAccessEvaluator.Evaluate`: Prefix-Gate um `Softwareschmiede.Devin` erweitern | Erledigt | `ActualStartSpec_RejectsOverridesWithoutMutatingThem` (Devin-Fälle) |
| 3 | Evaluator | `Evaluate`: Devin-JSON-Auswertung (`permissions.deny`/`ask` → `Denied`, `defaultMode`/`permissionMode` als Modus, übrige Schlüssel tolerant) | Erledigt | `ConfigurationFixtures_EnforceRestrictionsAndPreserveFiles` (Devin-Fälle) |
| 4 | Evaluator | `Evaluate`: Devin-Token-Whitelist (`--permission-mode`, `--respect-workspace-trust`, `--model`, `-c/--continue`, `-r/--resume`, `--sandbox`) | Erledigt | `ActualStartSpec_RejectsOverridesWithoutMutatingThem` (Devin-Fälle) |
| 5 | Evaluator | `Evaluate`: `DEVIN_PERMISSION_MODE`-Env-Wertung + Supported-Regel (`accept-edits`/`dangerous`/`bypass`) | Erledigt | `DevinEnvironmentPermissionMode_IsEvaluated`, `DevinPermissionModeArgument_OverridesEnvironment` |
| 6 | Adapter | `TrustDialogMarkers` um `do you trust the authors of this directory` erweitern | Erledigt | `TrustPrompt_NeverReceivesProbeOrConfirmation`/`ProbeLostToTrustDialog_ResumesAfterAnswer` (Devin) |
| 7 | Adapter | `RecognizeStartup`: Devin-Banner `Devin CLI` + `v3000.11.92` | Erledigt | `ActualStartSpec_AllowsWorkspaceChannelWithoutAddingBypass` (Devin) |
| 8 | Adapter | `RefreshScreen`: Devin-Busy-Marker (`esc twice/again to interrupt`) und `❭`-Promptregel | Erledigt | `PendingProbe_ResendsAfterDelay_UpToCap` (Devin), Dispatch-Test |
| 9 | Tests | `ScmDispatchFixture`: optionales `ScmKiProfile` (Prefix, Banner, Prompt, Args, Env, Probe-Callback) | Erledigt | `ScmEventDispatchTests` (Bestand unverändert) + `ScmEventDispatchTests_Devin` |
| 10 | Tests | `ScmEventCliExchangeAccessTests.Spec`: Devin-Provider-Mapping | Erledigt | alle Devin-Theory-Fälle |
| 11 | Tests | Devin-InlineData in `ActualStartSpec_*`, `ConfigurationFixtures_*`, `LivingRun_*`, PendingProbe-Theorien, RestrictedProcess | Erledigt | Testlauf 192/192 |
| 12 | Tests | Devin-Args/Env-Evaluierungsfälle (`--resume`, `--continue`, `--sandbox`, `DEVIN_PERMISSION_MODE`) | Erledigt | `ScmEventDispatchTests_Devin.*` |
| 13 | Tests | Dispatch-Test Devin-Profil bis `Zugestellt` + wartender Fall mit `auto`-Modus | Erledigt | `DevinTask_WithGrantingStartContract_DeliversEvent`, `DevinTask_WithAutoPermissionMode_WaitsWithoutPrompt` |
| 14 | Build | `dotnet build Softwareschmiede.slnx` + gefilterte Testlane | Erledigt | Build 0 Fehler; Tests 1986+192 grün |
| 15 | Doku | `docs/help/plugins/github-ereignisse/` Devin-Unterstützung ergänzen | Erledigt | — |
| 16 | Doku | `README.md` CLI-Unterstützungsabschnitt aktualisieren | Erledigt | — |
| 17 | Doku | `changes.log` Eintrag | Erledigt | — |
