# Tasks: Ausführungsbegrenzung — Platz bei wartender CLI am Eingabeprompt freigeben

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `GetOccupancyAsync` in `ScmEventExecutionLimitService` um die Ready-/Pending-Freigabe erweitern (Ready-Kandidaten ermitteln, gebündelte Pending-Query auf `ScmIncomingEvents`, Freigabe vor globaler Zählung und Plugin-Attribution) | Erledigt | — |
| 2 | Logik | XML-Doku von `CliExecutionOccupancy` und `GetOccupancyAsync` um die neue Freigabesemantik und die tolerierte Überschreitung ergänzen | Erledigt | — |
| 3 | Auslöser | `event Action<ScmPromptReadyStatus>? ReadyStatusChanged` auf `IScmPromptSessionAdapter` deklarieren (Non-blocking-Vertrag im Kommentar) | Erledigt | — |
| 4 | Auslöser | `ScmPromptSessionAdapter.SetReady` feuert `ReadyStatusChanged` bei Wertwechsel | Erledigt | — |
| 5 | Auslöser | `AiExecutionService`: optionalen Ctor-Parameter `ScmStartCheckTrigger? startCheckTrigger = null` ergänzen | Erledigt | — |
| 6 | Auslöser | `AiExecutionService`: bei Adapter-Anlage `ReadyStatusChanged` abonnieren (`Ready` → `RequestStartCheck()`) plus einmalige Level-Prüfung `adapter.ReadyStatus == Ready` nach der Verdrahtung | Erledigt | — |
| 7 | Auslöser | `ScmEventOrchestrator.DispatchCoreAsync`: `item` vor `try` deklarieren; `finally` bei `item?.Status is "Verarbeitet" or "Abgebrochen"` `startCheckTrigger?.RequestStartCheck()` anfordern | Erledigt | — |
| 8 | Auslöser | XML-Doku von `ScmStartCheckTrigger` und `ScmStartCheckService` um die neuen Auslöser ergänzen | Erledigt | — |
| 9 | Tests | `LiveSession`-Hilfsmethode in `ScmEventExecutionLimitServiceTests` um steuerbaren `ReadyStatus`/`IsAlive` erweitern (rückwärtskompatibel) | Erledigt | — |
| 10 | Tests | Unit-Tests Platzfreigabe: Ready+Alive ohne Pending → globaler und Plugin-Platz frei (`GetStartBlockReasonAsync_ReadySessionNoPending_*`) | Erledigt | — |
| 11 | Tests | Unit-Tests Pending-Guard: offene Ereignis-Stati (Theory) und Endstati `Verarbeitet`/`Abgebrochen` (Theory) | Erledigt | — |
| 12 | Tests | Unit-Tests konservative Zählung: `ReadyStatus`-Matrix (`Unknown`/`Busy`/`TrustPrompt`/`Exited`), `IsAlive == false`, Pending einer anderen Aufgabe, gemischte Belegung, Reservierung > Ready-Freigabe | Erledigt | — |
| 13 | Tests | Adapter-Event-Test (`ScmPromptSessionAdapterReadySignalTests` o. ä.): `SetReady`-Übergänge feuern `ReadyStatusChanged` mit dem neuen Status | Erledigt | — |
| 14 | Tests | Endstatus-Trigger-Test: Dispatch-Lauf, der das Ereignis in `Verarbeitet` enden lässt, erhöht `fixture.Trigger.Requested` | Erledigt | — |
| 15 | Tests | Integrationstest Platzfreigabe ohne Poll: `SetPrompt(SCM_READY_V1)` bei globalem Limit 1 → wartendes Ereignis startet (`Dispatch_TaskReadyAtPrompt_SlotFreed_WaitingEventStarts`) | Erledigt | — |
| 16 | Tests | Integrationstest Pending-Guard: `Ready` mit offenem Ereignis → Platz bleibt belegt, kein Folgestart | Erledigt | — |
| 17 | Tests | Integrationstest Auslöser: `ReadyStatus → Ready`-Übergang löst `ScmStartCheckTrigger.Requested` aus | Erledigt | — |
| 18 | Doku | `README.md` (Z. ~30 und ~187): Ready-/Pending-Freigabe neben der Pausen-Freigabe beschreiben | Erledigt | — |
| 19 | Doku | `docs/help/einstellungen/beschreibung.md` (Z. ~75): Feld „Maximale parallele Ausführungen" um die neue Freigaberegel ergänzen | Erledigt | — |
| 20 | Doku | `docs/help/plugins/github-ereignisse/beschreibung.md` (Z. ~11): Funktionsbeschreibung um Ready-/Pending-Freigabe ergänzen | Erledigt | — |
| 21 | Doku | `docs/help/plugins/github-ereignisse/ablauf-technisch.md` (Z. ~33–34): Belegungszählung und `ScmStartCheckTrigger`-Auslöserliste aktualisieren | Erledigt | — |
| 22 | Doku | `docs/help/plugins/github-ereignisse/business-rules.md` (Z. ~99–107, ~137): Regel „belegt keinen Platz" und Auslöser-Liste erweitern (inkl. tolerierter Überschreitung) | Erledigt | — |
| 23 | Doku | `docs/help/aufgaben/ablauf-technisch.md` (Z. ~521): Abschnitt „Gegenläufige Wirkung auf die SCM-Ausführungsbegrenzung" aktualisieren | Erledigt | — |
| 24 | Doku | `docs/help/aufgaben/business-rules.md` (Z. ~254–262) und `docs/help/aufgaben/datenmodell.md` (Z. ~36): Auswertungsstellen/Freigaberegeln ergänzen | Erledigt | — |
| 25 | Abschluss | `dotnet build Softwareschmiede.slnx`, `dotnet format Softwareschmiede.slnx`, beide reguläre Test-Lanes synchron mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` ausführen | Erledigt | — |
