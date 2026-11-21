# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Verifizierte Code-Annahmen des Plans

Alle im Plan genannten Annahmen über den Bestand wurden stichprobenartig gegen den Code geprüft und stimmen:

| Annahme im Plan | Verifikation |
|-----------------|--------------|
| `GetOccupancyAsync` Z. 189–214: Pausenfilter vor globaler Zählung und Plugin-Attribution | Bestätigt (`ScmEventExecutionLimitService.cs` Z. 189–214, Filter Z. 199, Plugin-Attribution Z. 203–209, `runningIds.Count - workItems.Count`-Rest Z. 213) |
| `CliExecutionOccupancy`-XML-Doku Z. 6–19, `GetOccupancyAsync`-Doku Z. 185–188 | Bestätigt |
| `IScmPromptSessionAdapter` Z. 24–55 ohne öffentliches Übergangsereignis; `SetReady` Z. 375–382 nur interner `_changed`-TCS mit Frühreturn bei gleichem Wert | Bestätigt — `SetReady` wird von allen Aufrufern unter `lock (_sync)` aufgerufen (`RefreshScreen` Z. 336, `ConfirmReady` Z. 250, `MarkInputStarted` Z. 269, `OnExited`/`OnFailed` Z. 385–386); Non-blocking-Vertrag für `ReadyStatusChanged`-Handler ist korrekt gefordert |
| Adapter-Anlage `AiExecutionService.cs` ~Z. 377–379 inkl. `session.InputStarted = adapter.MarkInputStarted`; Konstruktor-`RefreshScreen` (Z. 117) kann `Ready` vor Verdrahtung setzen → einmalige Level-Prüfung nötig | Bestätigt — Z. 377–379 exakt wie beschrieben; `RefreshScreen()` im Adapter-Ctor Z. 117 ruft `SetReady` ohne dass ein Abonnent existieren könnte |
| `GetScmPromptSession` Z. 480 liefert `handle.ScmPromptSession` oder `null` | Bestätigt (Z. 480–481; `CliProcessHandle.ScmPromptSession` ist `ScmPromptSessionAdapter?`, Z. 859) |
| `ScmEventOrchestrator` hat bereits optionalen `ScmStartCheckTrigger?`-Ctor-Parameter → keine Signaturänderung | Bestätigt (Primärkonstruktor Z. 22); bereits verwendet in `CancelAsync` Z. 451 |
| `DispatchCoreAsync`: `item` vor `try` deklarierbar; ein `finally`-Prüfpunkt erfasst alle Endstatus-Wege | Bestätigt — `item`-Laden liegt aktuell bei Z. 130 innerhalb des `try` (Z. 127); alle `Verarbeitet`-Zuweisungen (Z. 537 `ReconcileAttemptAsync`, Z. 666 `DispatchPreparationAsync`, Z. 915/932 `ReconcilePreparationAttemptAsync`) mutieren dieselbe getrackte Entität, die per Referenz übergeben wird; `finally` liegt Z. 346–352 |
| `ScmStartCheckTrigger` Singleton mit `event Action? Requested`; `RequestStartCheck()` non-blocking | Bestätigt (`ScmStartCheckService.cs` Z. 17–24; `OnRequested` = `_signal.Release()` mit geschluckten `SemaphoreFull`/`ObjectDisposed`, Z. 150–158; 250-ms-Sammelphase Z. 39) |
| DI füllt optionalen Parameter automatisch (`App.xaml.cs` Z. 316/324; `ScmTestServices` Z. 40/51) | Bestätigt — beide Male `AddSingleton<AiExecutionService>()` und `AddSingleton<ScmStartCheckTrigger>()`; optionale Ctor-Parameter nutzen bei fehlender Registrierung den Default |
| `fixture.Trigger.Requested`-Zählung als Testnachweis | Bestätigt (`ScmDispatchFixture.cs` Z. 214; Muster in `ScmEventDispatchTests_Startcheck` Z. 137/164) |
| `LiveSession`-Hilfsmethode setzt `ReadyStatus` nicht → Moq-Default `Unknown`, bestehende Erwartungen bleiben | Bestätigt (`ScmEventExecutionLimitServiceTests.cs` Z. 72–78); Hinweis im Plan zu optionalen Standardwerten ist korrekt und nötig |
| ~13 direkte `new ScmPromptSessionAdapter(...)`-Stellen bleiben kompilierbar (kein Ctor-Parameter) | Bestätigt in der Sache (kein Ctor-Parameter geplant); tatsächlich sind es 15 Aufrufstellen (9× `ScmEventCliExchangeAccessTests.cs`, 2× `.RestrictedProcess.cs`, 4× `.PendingProbe.cs`) — ohne Auswirkung auf die Aussage |
| `TestAiExecutionServiceFactory` bleibt quellkompatibel | Bestätigt — positionales `new(...)` mit 6 Argumenten (`TestAiExecutionServiceFactory.cs` Z. 35–41); optionaler 7. Parameter kompatibel |
| Alle produktiven Endstatus-Setzpfade liegen in `DispatchCoreAsync`/`CancelAsync` | Bestätigt — außerhalb des Orchestrierers existieren keine `Status = "Verarbeitet"`/`"Abgebrochen"`-Schreibstellen; `CancelAsync` (`ExecuteUpdate`, Z. 426–431) fordert bereits selbst an (Z. 451); In-Dispatch-Abbruchprüfungen (`IsCancelledAsync`, Z. 305/318/320/325) werden vom `CancelAsync`-Auslöser abgedeckt |

Bestehende Tests wurden auf Bruchgefahr geprüft: Alle Limit-/Startcheck-Integrationstests verlassen das erste Ereignis im Pending-Status `"Zugestellt"` → der Platz bleibt auch unter der neuen Regel belegt; `Requested`-Zählungen nutzen `>= 1`-Assertionen → zusätzliche Auslöser sind harmlos. Die Liste betroffener bestehender Tests (nur `LiveSession`) ist damit vollständig.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Zweiter Freigabezustand: `IsAlive && ReadyStatus == Ready` und kein offenes `ScmIncomingEvent` → frei in globalem und Plugin-Zähler, greift vor beiden Zählern wie der Pausenfilter | Schritt 3 + Programmablauf „Belegungsauswertung" (Kandidatenfilter vor globaler Zählung und Plugin-Attribution) | `GetStartBlockReasonAsync_ReadySessionNoPending_ReturnsGlobalSlotFree` / `_ReturnsPluginSlotFree`, `GetStartBlockReasonAsync_MixedReadyFreeAndActive_CountsOnlyActive` | Abgedeckt |
| Pending-Guard: jedes nicht-endständige Ereignis der Aufgabe hält den Platz trotz `Ready` | Schritt 3, gebündelte Pending-Query `Status ∉ {Verarbeitet, Abgebrochen}` → `HashSet<Guid>` | Theory `GetStartBlockReasonAsync_ReadySessionWithPendingEvent_CountsAsOccupied` über alle 7 offenen Stati; `ReadySession_PendingOfOtherTask_Ignored`; `ReadySessionEventInEndStatus_ReturnsSlotFree` | Abgedeckt |
| Ereignisgesteuerte Startprüfung beim Übergang in den Wartezustand (`ScmStartCheckTrigger`) | Schritt 1+2: `ReadyStatusChanged`-Event am Adapter, Verdrahtung in `AiExecutionService` mit `Ready`-Filter und Level-Prüfung | `SetReady_TransitionToReady_RaisesReadyStatusChanged`; Integration „`Ready`-Übergang löst `Trigger.Requested` aus"; Happy-Path-Integrationstest | Abgedeckt |
| Randfall „Session bereits `Ready`, letztes Ereignis wird `Verarbeitet`" deterministisch ausgelöst | Schritt 4: `finally`-Prüfung auf Endstatus in `DispatchCoreAsync` mit vorhandenem optionalem `startCheckTrigger` | `DispatchCoreAsync_EventReachesEndStatus_RequestsStartCheck` (`fixture.Trigger.Requested`-Zählung) | Abgedeckt |
| Konservative Zählung: keine Session, `Unknown`/`TrustPrompt`/`Busy`/`Exited`, Handles ohne Aufgabenzeile | Programmablauf Schritt 7; unveränderte Restzählung `runningIds.Count - workItems.Count` | Theory `SessionNotReady` (4 Stati), `SessionDead`; Null-Session indirekt über Bestandstests (`GlobalLimitReaches_Locks` etc.) | Abgedeckt |
| Rückkehr zur Arbeit zählt wieder voll; tolerierte zeitweise Überschreitung dokumentiert | Programmablauf „Rückkehr zur Arbeit"; Dokumentation in Scope-Klärung, XML-Doku und Doku-Schritt 8 | Busy-Theory-Fall + `MixedReadyFreeAndActive`; Doku-Hinweis | Abgedeckt |
| Fehlende Objekte ohne Fehler: kein Adapter → belegt; keine Event-Zeilen → freigabefähig; kein Trigger → optionaler Parameter | Scope-Klärung „Umgang mit fehlenden Objekten"; `?.`-Aufrufe; optionale Ctor-Parameter an beiden Injektionsstellen | `SessionNotReady`/`SessionDead`-Theorien; Fixture-Tests (DI liefert Trigger; manuelle Konstruktionen ohne) | Abgedeckt |
| Reservierungsfenster geschützt; keine Kaskaden-Parallelstarts (frisch gestarteter Lauf im Zustellfenster bleibt belegt) | Unveränderte Reservierungssemantik; Pending-Guard (`ZustellungVorbereitet`/`Zugestellt` sind offen) + `MarkInputStarted` → `Busy` | `CheckAndReserveStartSlotAsync_ReadyRunAndOpenReservation_ReservationOccupiedSlot`; Pending-Theory | Abgedeckt |
| Kein Zählkriterium über `CliRuntimeStatus`/`WorkItemRunStatus.WaitingForInput` | Scope-Ausschluss 1 (bestätigt); Variante (b) als Auslöser gewählt statt der Heuristik | — (Ausschluss, kein Code) | Abgedeckt |
| Kein Eingriff in laufende Prozesse; keine Limit-/Priorisierungs-/Reservierungs-/UI-/Konfigurations-/Migrationsänderung | Scope-Ausschlüsse 2–5 (alle mit Begründung + Status `bestätigt`) | — | Abgedeckt |
| Live-Auswertung ohne eigenen Cache; Poll als Sicherheitsnetz | Scope-Klärung „Auslösende Ereignisse" — Bewertung live je Zulassung; Poll 60 s bleibt | Bestehende Monitor-Tests | Abgedeckt |
| Kein N+1: Pending-Abfrage gebündelt, nur wenn Ready-Kandidat existiert | Designentscheidung „Pending-Abfrage" (eine Query über `runningIds`) | Implizit über alle Limit-Tests | Abgedeckt |
| XML-Doku (`CliExecutionOccupancy`, `GetOccupancyAsync`, `ScmStartCheckTrigger`/`ScmStartCheckService`) + Produktdoku | Schritte 3, 4 und 8 | — | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Gesamte Anforderung (systemseitige Platzfreigabe ohne UI-Berührung) | Keine (FlaUI/`OsInterface`) | Nicht erforderlich mit Begründung: Die Anforderung berührt keinen über UI oder Nutzeraktion erreichbaren Ablauf (Scope-Ausschluss 4 — keine UI-Änderung; Auslöser ist ein Terminal-Pufferwechsel, keine Nutzerinteraktion). Der Funktionsnachweis wird auf Systemebene durch `ScmDispatchFixture`-Integrationstests erbracht (echter Dienststapel auf echtem SQLite: `SetPrompt` → `ReadyStatusChanged` → `ScmStartCheckTrigger` → `DispatchDueEventsAsync` → tatsächlicher Folgestart), inklusive Happy Path (`Dispatch_TaskReadyAtPrompt_SlotFreed_WaitingEventStarts`), Pending-Guard (`Dispatch_TaskReadyWithPendingEvent_SlotStaysOccupied`) und Endstatus-Randfall. Die Begründung im Plan (Z. 288–296) ist nachvollziehbar und zutreffend. |

## Hinweise

- Die `finally`-Prüfung in `DispatchCoreAsync` feuert auch dann eine Startprüfung, wenn das Ereignis bereits beim Eintritt in den Dispatch im Endstatus stand (Frühreturns Z. 132/161). Das ist eine harmlose Mehrauslösung (koalesziert, ergebnislos), kann bei der Umsetzung optional auf den tatsächlichen Übergang eingegrenzt werden — kein Muss.
- Die Happy-Path-Integrationstests (`Dispatch_TaskReadyAtPrompt_SlotFreed_WaitingEventStarts`, `Dispatch_TaskReadyWithPendingEvent_SlotStaysOccupied`) sind über die Nachweis-Spalte der Akzeptanzszenarien und Schritt 7 der Umsetzungsreihenfolge spezifiziert, fehlen aber als eigene Zeile in der Tabelle „Neue Tests" — inhaltlich abgedeckt, bei der Umsetzung darauf achten, dass sie wirklich angelegt werden.
- Der konservative Fall „Handle ohne auflösbare Aufgabenzeile" (in Akzeptanzszenario 5 genannt) hat keinen dedizierten neuen Test; das Verhalten (`runningIds.Count - workItems.Count`) ist unverändert und durch die Struktur der Regel implizit gesichert — ein kleiner Regressionstest wäre optional sinnvoll.
- Doku-Schritt 8 verweist auf „alle verifizierten Doku-Stellen" — gemeint ist die vollständige Liste in `inventory/docs.md` inklusive der zusätzlich gefundenen Stellen (`docs/help/aufgaben/business-rules.md` Z. ~254–262, `docs/help/aufgaben/datenmodell.md` Z. ~36); bei der Umsetzung diese Liste explizit abarbeiten.
