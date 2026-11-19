# Tasks: Ausführungsbegrenzung muss pausierte Ausführungen freigeben (Fehlerkorrektur zu Issue #410)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `GetBelegungAsync` in `ScmEventExecutionLimitService`: Projektion um `x.PausiertBisUtc` erweitern | Offen | — |
| 2 | Logik | `GetBelegungAsync`: `jetzt = time.GetUtcNow()` einmal bestimmen und Aufgaben mit `PausiertBisUtc > jetzt` aus der `Global`-Zählung herausfiltern (Handles ohne Aufgabenzeile zählen weiterhin) | Offen | — |
| 3 | Logik | `GetBelegungAsync`: pausierte Aufgaben vor der Plugin-Attribution überspringen, sodass sie aus `ProPlugin` entfallen | Offen | — |
| 4 | Logik | XML-Dokumentation von `GetBelegungAsync`/`CliAusfuehrungsBelegung` um die Pausen-Semantik ergänzen | Offen | — |
| 5 | Logik | `ScmEventOrchestrator`: optionalen Konstruktorparameter `ScmStartPruefungsAusloeser?` ergänzen | Offen | — |
| 6 | Logik | `ScmEventOrchestrator.CancelAsync`: nach der Pausen-Rückstufung bei `changed != 0` `startPruefungsAusloeser?.StartpruefungAnfordern()` aufrufen | Offen | — |
| 7 | Tests | `ScmEventExecutionLimitServiceTests.LaufendeAufgabeAsync` um optionalen `pausiertBisUtc`-Parameter erweitern | Offen | — |
| 8 | Tests | `GetStartBlockReasonAsync_PausierteAufgabe_GibtGlobalenPlatzFrei` schreiben | Offen | — |
| 9 | Tests | `GetStartBlockReasonAsync_PausierteAufgabe_GibtPluginPlatzFrei` schreiben | Offen | — |
| 10 | Tests | `GetStartBlockReasonAsync_PausierteAufgabeOhnePrefix_EntfaelltAusBeidenZaehlern` schreiben | Offen | — |
| 11 | Tests | `GetStartBlockReasonAsync_AbgelaufenePause_ZaehltAlsBelegt` schreiben | Offen | — |
| 12 | Tests | `GetStartBlockReasonAsync_GemischtPausiertUndAktiv_ZaehltNurAktive` schreiben | Offen | — |
| 13 | Tests | `PruefeUndReserviereStartPlatzAsync_PausierterLaufUndOffeneReservierung_ReservierungBelegtPlatz` schreiben | Offen | — |
| 14 | E2E-Tests | `Dispatch_TaskPaused_SlotFreed_WaitingEventStarts` in `ScmEventDispatchTests_Limits` schreiben (globales Limit) | Offen | — |
| 15 | E2E-Tests | `Dispatch_TaskPaused_PluginSlotFreed_WaitingEventStarts` in `ScmEventDispatchTests_Limits` schreiben (Plugin-Limit) | Offen | — |
| 16 | E2E-Tests | `Dispatch_TaskPauseExpired_SlotStillOccupied` in `ScmEventDispatchTests_Limits` schreiben | Offen | — |
| 17 | E2E-Tests | `StartPruefung_PauseGesetzt_WartendesEreignisStartetOhnePoll` in `ScmEventDispatchTests_Startpruefung` schreiben | Offen | — |
| 18 | Verifikation | `dotnet build` der Lösung ausführen | Offen | — |
| 19 | Verifikation | `dotnet test` auf `Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests` (SCM-Dispatch-/Limit-Suiten) ausführen | Offen | — |
| 20 | Verifikation | `dotnet format Softwareschmiede.slnx --verify-no-changes` ausführen | Offen | — |
