# Tasks: Devin CLI im SCM-Antwortkanal

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Evaluator | `ScmCliAccessEvaluator.Capture`: Devin-Konfigurationspfade erfassen (`~/.devin/config.json`, `~/.config/devin/config.json`, `<workdir>/.devin/config.json`) | Offen | — |
| 2 | Evaluator | `ScmCliAccessEvaluator.Evaluate`: Prefix-Gate um `Softwareschmiede.Devin` erweitern | Offen | — |
| 3 | Evaluator | `Evaluate`: Devin-JSON-Auswertung (`permissions.deny`/`ask` → `Denied`, `defaultMode`/`permissionMode` als Modus, übrige Schlüssel tolerant) | Offen | — |
| 4 | Evaluator | `Evaluate`: Devin-Token-Whitelist (`--permission-mode`, `--respect-workspace-trust`, `--model`, `-c/--continue`, `-r/--resume`, `--sandbox`) | Offen | — |
| 5 | Evaluator | `Evaluate`: `DEVIN_PERMISSION_MODE`-Env-Wertung + Supported-Regel (`accept-edits`/`dangerous`/`bypass`) | Offen | — |
| 6 | Adapter | `TrustDialogMarkers` um `do you trust the authors of this directory` erweitern | Offen | — |
| 7 | Adapter | `RecognizeStartup`: Devin-Banner `Devin CLI` + `v3000.11.92` | Offen | — |
| 8 | Adapter | `RefreshScreen`: Devin-Busy-Marker (`esc twice/again to interrupt`) und `❭`-Promptregel | Offen | — |
| 9 | Tests | `ScmDispatchFixture`: optionales Ki-Profil (Prefix, Banner, Prompt, Args, Env, Probe-Callback) | Offen | — |
| 10 | Tests | `ScmEventCliExchangeAccessTests.Spec`: Devin-Provider-Mapping | Offen | — |
| 11 | Tests | Devin-InlineData in `ActualStartSpec_*`, `ConfigurationFixtures_*`, `LivingRun_*`, PendingProbe-Theorien | Offen | — |
| 12 | Tests | Devin-Args/Env-Evaluierungsfälle (`--resume`, `--continue`, `--sandbox`, `DEVIN_PERMISSION_MODE`) | Offen | — |
| 13 | Tests | Dispatch-Test Devin-Profil bis `Zugestellt` + wartender Fall mit `auto`-Modus | Offen | — |
| 14 | Build | `dotnet build Softwareschmiede.slnx` + gefilterte Testlane | Offen | — |
| 15 | Doku | `docs/help/plugins/github-ereignisse/` Devin-Unterstützung ergänzen | Offen | — |
| 16 | Doku | `README.md` CLI-Unterstützungsabschnitt aktualisieren | Offen | — |
| 17 | Doku | `changes.log` Eintrag | Offen | — |
