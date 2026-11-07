# Tasks: Devin-CLI-Versionsbereich v3000.x für SCM-Ereigniszustellung

**Hinweis:** Lifecycle nicht eingehalten — diese Tabelle und die Artefakte im Ordner `a66a5c272f0f4636ac9dd6824d4b9152-eventgetriebene-aufgabenbearbe/` wurden retrospektiv nach dem Merge (PR #327, `ee90bb4`) erstellt.

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Versionskatalog | Devin-`SupportedRanges` in `ScmCliPromptProfile` auf `[3000.0.0, 3001.0.0)` erweitern, `SupportedVersionsText` → `"v3000.x"` | Erledigt | `ScmCliPromptProfileTests.TryMatch_ClassifiesRangeBoundaries` |
| 2 | Unit-Tests | Grenzfälle und Wartegrund-Samples auf den neuen Bereich umstellen (`v3000.12.x` supported, `v3001.0.0` unsupported, Text `v3000.x`) | Erledigt | `ScmCliPromptProfileTests` (44 Tests im gefilterten Lauf grün) |
| 3 | Orchestrator-Test | `ConfirmReady_UnsupportedVersion_*` auf Banner `v3001.0.0` umstellen | Erledigt | `ScmEventOrchestratorTests` grün |
| 4 | Test-Fixture | `ScmDispatchFixture`-Kommentar auf `v3000.x` | Erledigt | Build |
| 5 | Integrationstests | Zustellung mit `v3000.12.7` nachweisen; Unsupported-Fälle auf `v3001.0.0` umstellen; `v3000.12.7` in Supported-Tabellen ergänzen | Erledigt | `ScmEventDispatchTests_Devin` + `ScmEventCliExchangeAccessTests` (106 Tests grün) |
| 6 | Dokumentation | `docs/help/plugins/github-ereignisse/*` auf `v3000.x` + Major-Erläuterung für Devin | Erledigt | Doc-Review, kein Code |
| 7 | Verifikation | Voller Build, gefilterte Testläufe, Format-Check, PR-Checks | Erledigt | `test-results.md`; CI PR #327 alle Checks grün |
| 8 | Lifecycle | Artefakte `requirement`/`inventory`/`plan`/`plan-check`/`test-results`/`review` nachziehen | Erledigt | dieses Verzeichnis |
