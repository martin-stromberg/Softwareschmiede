# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `ScmEventExecutionLimitService.GetBelegungAsync` (geänderte Methode) — Projektion `new { x.Id, x.KiPluginPrefix }` um `x.PausiertBisUtc` erweitert (`ScmEventExecutionLimitService.cs:218`); `jetzt = time.GetUtcNow()` einmal bestimmt (Z. 220); pausierte Aufgaben (`PausiertBisUtc > jetzt`) über `aktive`-Filterung aus `Global` und `ProPlugin` herausgenommen (Z. 221–232); `Global` zählt weiterhin laufende Handles ohne auflösbare Aufgabenzeile via `laufendeIds.Count - aufgaben.Count` (Z. 233–235)
- [x] XML-Dokumentation der Pausen-Semantik — am Record `CliAusfuehrungsBelegung` (Z. 6–11) und an `GetBelegungAsync` (Z. 207–210) ergänzt
- [x] `ScmEventOrchestrator` — optionaler Konstruktorparameter `ScmStartPruefungsAusloeser? startPruefungsAusloeser = null` als letzter Parameter (`ScmEventOrchestrator.cs:22`); DI-Auflösung gesichert (Singleton in `App.xaml.cs:313` und `ScmDispatchFixture.cs:170`)
- [x] `ScmEventOrchestrator.CancelAsync` (geänderte Methode) — `startPruefungsAusloeser?.StartpruefungAnfordern()` nach dem Pausen-Rückstufungsblock bei `changed != 0` (Z. 445–451)
- [x] `LaufendeAufgabeAsync` (Test-Hilfsmethode, erweitert) — optionaler `pausiertBisUtc`-Parameter setzt `Aufgabe.PausiertBisUtc` (`ScmEventExecutionLimitServiceTests.cs:51–63`)
- [x] `GetStartBlockReasonAsync_PausierteAufgabe_GibtGlobalenPlatzFrei` — vorhanden, grün (Z. 338)
- [x] `GetStartBlockReasonAsync_PausierteAufgabe_GibtPluginPlatzFrei` — vorhanden, grün (Z. 349)
- [x] `GetStartBlockReasonAsync_PausierteAufgabeOhnePrefix_EntfaelltAusBeidenZaehlern` — vorhanden, grün (Z. 361)
- [x] `GetStartBlockReasonAsync_AbgelaufenePause_ZaehltAlsBelegt` — vorhanden, grün (Z. 373)
- [x] `GetStartBlockReasonAsync_GemischtPausiertUndAktiv_ZaehltNurAktive` — vorhanden, grün (Z. 386)
- [x] `PruefeUndReserviereStartPlatzAsync_PausierterLaufUndOffeneReservierung_ReservierungBelegtPlatz` — vorhanden, grün (Z. 402)
- [x] `Dispatch_TaskPaused_SlotFreed_WaitingEventStarts` in `ScmEventDispatchTests_Limits` — vorhanden, grün (Z. 351)
- [x] `Dispatch_TaskPaused_PluginSlotFreed_WaitingEventStarts` in `ScmEventDispatchTests_Limits` — vorhanden, grün (Z. 377)
- [x] `Dispatch_TaskPauseExpired_SlotStillOccupied` in `ScmEventDispatchTests_Limits` — vorhanden, grün (Z. 404)
- [x] `StartPruefung_PauseGesetzt_WartendesEreignisStartetOhnePoll` in `ScmEventDispatchTests_Startpruefung` — vorhanden, grün (Z. 56)
- [x] Verifikation — `dotnet build Softwareschmiede.slnx` (0 Fehler/0 Warnungen), `dotnet format Softwareschmiede.slnx --verify-no-changes` (sauber), `ScmEventExecutionLimitServiceTests` 25/25, `ScmEventDispatchTests_Limits` 16/16, `ScmEventDispatchTests_Startpruefung` 8/8

## Hinweise

- **Parallel-Testflake (vorbestehend, nicht plangegenständlich):** `Reservierungen`/`ZulassungsGate` in `ScmEventExecutionLimitService` sind prozessweit statisch. Laufen `ScmEventDispatchTests_Limits` und `ScmEventDispatchTests_Startpruefung` in einem `dotnet test`-Aufruf parallel, kann eine offene `StartPlatzReservierung` der einen Fixture die Belegung der anderen kurzzeitig erhöhen — beobachtet als zweimalig unterschiedlicher Einzelfehler (`Dispatch_GlobalLimitReached_EventWaitsWithReason` bzw. `StartPruefung_PauseGesetzt_WartendesEreignisStartetOhnePoll`, Timeout 30 s). Beide Klassen einzeln und alle betroffenen Tests isoliert sind stabil grün; die Interferenz existiert seit Einführung der Reservierungsmechanik (Issue 410, Schritt 2) und ist keine Lücke dieser Korrektur. Für die Nachimplementierung/CI ggf. erwägen: `ReservierungenZuruecksetzen()` in `ScmDispatchFixture.InitializeAsync` oder gemeinsame xUnit-Collection für beide Klassen.
- `CancelAsync` selbst hat keinen direkten Auslöser-Test (im Plan auch keiner gefordert); der neue Aufruf liegt korrekt hinter dem `changed != 0`-Gate.
