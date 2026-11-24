# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Am verifizierten Prompt wartende Aufgabe mit nur manuell zu klärendem oder nicht zustellbarem Ereignis gibt Platz frei; Queue fließt | `GetOccupancyAsync` → `!StartSlotFreeing` (In-Flight-only); freigebend: `Aufgenommen`, `Verzögert`, `Wartend`, `ZustellungUngewiss`, `VerarbeitungUngeklärt`, `Verarbeitet`, `Abgebrochen` | `GetStartBlockReasonAsync_ReadySessionNotYetDeliveredEvent_FreesSlot`, `Dispatch_TaskReadyWithUndeliverableOnlyEvent_SlotFreed` | Abgedeckt |
| Schutzziel gewahrt: In-Flight-Zustellung/Verarbeitung zählt weiterhin voll; kein Doppelstart | `StartSlotFreeing` enthält die drei In-Flight-Stati nicht; unbekannte Stati blockieren konservativ | `GetStartBlockReasonAsync_ReadySessionInFlightEvent_CountsAsOccupied`, bestehender `Dispatch_TaskReadyWithPendingEvent_SlotStaysOccupied` | Abgedeckt |
| Ereignisse auf beendete/archivierte Aufgaben erhalten definierten Endzustand | `UndeliverableReason` + `MarkUndeliverableAsync` → `VerarbeitungUngeklärt` an allen Dispatch-Stellen | `Dispatch_EndedOrArchivedTask_MarkedUndeliverable`, `Monitor_ReusedBlockedTask_StaysWaitingWithoutStart` (angepasst) | Abgedeckt |
| Strukturell unzustellbare Ereignisse (fehlende Repository-Zuordnung, entfernte Aufgabe) | `UndeliverableReason`-Prüfungen `task is null`, `GitRepositoryId`/`GitRepository is null` | `Dispatch_TaskWithoutRepository_MarkedUndeliverable`, `Dispatch_RemovedTask_MarkedUndeliverable` | Abgedeckt |
| Regressionstests „WartetAufEingabe + `VerarbeitungUngeklärt`/`ZustellungUngewiss` → frei" | In Unit-Theory `..._NotYetDeliveredEvent_FreesSlot` enthalten | Dieselbe | Abgedeckt |
| „Sitzung beschäftigt / In-Flight → belegt" | `Ready`-Guard + In-Flight-Menge | `GetStartBlockReasonAsync_SessionNotReady_CountsAsOccupied` (bestehend), In-Flight-Theory | Abgedeckt |
| Konsistenz: String-Literale `„Verarbeitet“`/`„Abgebrochen“` durch Konstanten ersetzen | Pending-Abfrage nutzt `ScmIncomingEventStatus.StartSlotFreeing` | Implizit durch alle angepassten Tests | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Platzfreigabe / Endzustand | — | Nicht erforderlich mit Begründung: reine Hintergrundlogik ohne neue/geänderte UI-Interaktion; `VerarbeitungUngeklärt` ist der Queue-UI bereits als Status bekannt. Funktionsnachweis auf Prozessebene via `ScmDispatchFixture`-Integrationstests (echter Orchestrator, echtes SQLite, deterministische Terminal-Grenze) vom Ereigniseingang bis Status-/Platzwirkung. |

## Hinweise

- Die vier offenen Punkte des Issues sind im Plan als Designentscheidungen mit Begründung festgelegt; der Abschnitt `Offene Punkte` ist leer — kein Anwender-Blocker.
- Scope-Ausschluss 1 (keine `NonBlocking`-Änderung) ist im Issue-Text selbst festgelegt → `bestätigt`.
- Betroffene bestehende Tests sind vollständig benannt (`ScmEventExecutionLimitServiceTests`-Theory, `Dispatch_EndedOrArchivedTask_StaysBlocked`, `Monitor_ReusedBlockedTask_StaysWaitingWithoutStart`); benötigte Fixture-Hilfen (`SetEventStatusAsync`, `SetPrompt`, `AddEventAsync`-Erweiterung um `NextAttemptUtc`) sind benannt.
- Der Guard „manuell klärbare Stati ohne `DeliveryAttemptId` → Rückkehr" verhindert Poll-Rauschen und Fehlzustellung im Kollisionsrandfall — abgeleitet aus `DispatchDueEventsAsync`-Auswahlprädikat.
