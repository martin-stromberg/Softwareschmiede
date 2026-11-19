# Tasks: Ausführungsbegrenzung muss pausierte Ausführungen freigeben (Fehlerkorrektur zu Issue #410)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `GetBelegungAsync` in `ScmEventExecutionLimitService`: Projektion um `x.PausiertBisUtc` erweitern | Erledigt | `ScmEventExecutionLimitServiceTests.GetStartBlockReasonAsync_PausierteAufgabe_GibtGlobalenPlatzFrei` |
| 2 | Logik | `GetBelegungAsync`: `jetzt = time.GetUtcNow()` einmal bestimmen und Aufgaben mit `PausiertBisUtc > jetzt` aus der `Global`-Zählung herausfiltern (Handles ohne Aufgabenzeile zählen weiterhin) | Erledigt | `GetStartBlockReasonAsync_PausierteAufgabe_GibtGlobalenPlatzFrei`, `GetStartBlockReasonAsync_GemischtPausiertUndAktiv_ZaehltNurAktive` |
| 3 | Logik | `GetBelegungAsync`: pausierte Aufgaben vor der Plugin-Attribution überspringen, sodass sie aus `ProPlugin` entfallen | Erledigt | `GetStartBlockReasonAsync_PausierteAufgabe_GibtPluginPlatzFrei`, `GetStartBlockReasonAsync_PausierteAufgabeOhnePrefix_EntfaelltAusBeidenZaehlern` |
| 4 | Logik | XML-Dokumentation von `GetBelegungAsync`/`CliAusfuehrungsBelegung` um die Pausen-Semantik ergänzen | Erledigt | Kein direkter Test |
| 5 | Logik | `ScmEventOrchestrator`: optionalen Konstruktorparameter `ScmStartPruefungsAusloeser?` ergänzen | Erledigt | `ScmEventDispatchTests_*` (Orchestrator wird pro Dispatch über DI aufgelöst) |
| 6 | Logik | `ScmEventOrchestrator.CancelAsync`: nach der Pausen-Rückstufung bei `changed != 0` `startPruefungsAusloeser?.StartpruefungAnfordern()` aufrufen | Erledigt | Kein direkter Test (Auslöser-Muster selbst über `StartPruefung_PauseGesetzt_WartendesEreignisStartetOhnePoll` abgedeckt) |
| 7 | Tests | `ScmEventExecutionLimitServiceTests.LaufendeAufgabeAsync` um optionalen `pausiertBisUtc`-Parameter erweitern | Erledigt | Alle neuen `*_Pausiert*`-Tests nutzen den Parameter |
| 8 | Tests | `GetStartBlockReasonAsync_PausierteAufgabe_GibtGlobalenPlatzFrei` schreiben | Erledigt | Test vorhanden, grün (25/25 in `ScmEventExecutionLimitServiceTests`) |
| 9 | Tests | `GetStartBlockReasonAsync_PausierteAufgabe_GibtPluginPlatzFrei` schreiben | Erledigt | Test vorhanden, grün |
| 10 | Tests | `GetStartBlockReasonAsync_PausierteAufgabeOhnePrefix_EntfaelltAusBeidenZaehlern` schreiben | Erledigt | Test vorhanden, grün |
| 11 | Tests | `GetStartBlockReasonAsync_AbgelaufenePause_ZaehltAlsBelegt` schreiben | Erledigt | Test vorhanden, grün |
| 12 | Tests | `GetStartBlockReasonAsync_GemischtPausiertUndAktiv_ZaehltNurAktive` schreiben | Erledigt | Test vorhanden, grün |
| 13 | Tests | `PruefeUndReserviereStartPlatzAsync_PausierterLaufUndOffeneReservierung_ReservierungBelegtPlatz` schreiben | Erledigt | Test vorhanden, grün |
| 14 | E2E-Tests | `Dispatch_TaskPaused_SlotFreed_WaitingEventStarts` in `ScmEventDispatchTests_Limits` schreiben (globales Limit) | Erledigt | Test vorhanden, grün (Klassenlauf 16/16) |
| 15 | E2E-Tests | `Dispatch_TaskPaused_PluginSlotFreed_WaitingEventStarts` in `ScmEventDispatchTests_Limits` schreiben (Plugin-Limit) | Erledigt | Test vorhanden, grün |
| 16 | E2E-Tests | `Dispatch_TaskPauseExpired_SlotStillOccupied` in `ScmEventDispatchTests_Limits` schreiben | Erledigt | Test vorhanden, grün |
| 17 | E2E-Tests | `StartPruefung_PauseGesetzt_WartendesEreignisStartetOhnePoll` in `ScmEventDispatchTests_Startpruefung` schreiben | Erledigt | Test vorhanden, grün (Klassenlauf 8/8) |
| 18 | Verifikation | `dotnet build` der Lösung ausführen | Erledigt | `dotnet build Softwareschmiede.slnx` — 0 Fehler, 0 Warnungen |
| 19 | Verifikation | `dotnet test` auf `Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests` (SCM-Dispatch-/Limit-Suiten) ausführen | Erledigt | `ScmEventExecutionLimitServiceTests` 25/25, `ScmEventDispatchTests_Limits` 16/16, `ScmEventDispatchTests_Startpruefung` 8/8 (siehe Hinweis in review.md zur Parallel-Flake) |
| 20 | Verifikation | `dotnet format Softwareschmiede.slnx --verify-no-changes` ausführen | Erledigt | Exit 0, keine Verletzungen |
