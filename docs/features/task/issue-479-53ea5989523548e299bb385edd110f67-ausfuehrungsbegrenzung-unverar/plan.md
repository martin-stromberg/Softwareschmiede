# Umsetzungsplan: Ausführungsbegrenzung — hängende Ereignisse halten Startplatz belegt (Issue #479)

## Übersicht

Die Pending-Abfrage in `ScmEventExecutionLimitService.GetOccupancyAsync` wird von „jeder Status außer `Verarbeitet`/`Abgebrochen` blockiert den Platz einer Prompt-bereiten Aufgabe" auf „nur In-Flight-Stati laufender Zustellung/Verarbeitung blockieren" umgestellt. Zusätzlich erhalten strukturell unzustellbare Ereignisse (Aufgabe entfernt, beendet/archiviert, ohne Repository-Zuordnung) den klaren, manuell abschließbaren Endzustand `VerarbeitungUngeklärt` statt ewig `Wartend` zu bleiben.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Die Platzbelegungsbewertung `GetOccupancyAsync` je laufender Aufgabe sowie die Status-Transition strukturell unzustellbarer `ScmIncomingEvent`-Einträge im `ScmEventOrchestrator`. |
| Auslösende Ereignisse | Jede Occupancy-Bewertung unter `AdmissionGate` (`CheckAndReserveStartSlotAsync`, `GetStartBlockReasonAsync`); jeder Dispatch-Versuch eines Ereignisses (Poll, Startprüfung, Queue-Öffnen). |
| Erwartete Wirkung | Prompt-bereite Aufgaben mit nur manuell klärbarem oder nicht gestartetem Ereignis geben ihren Platz frei; die Ereignisqueue fließt wieder. In-Flight-Zustellung hält den Platz weiterhin belegt (kein Doppelstart). Strukturell unzustellbare Ereignisse verlassen `Wartend` in einen abschließbaren Endzustand. |
| Umgang mit fehlenden Objekten | Aufgabe entfernt/beendet/archiviert/fehlende Repository-Zuordnung → Ereignis geht in `VerarbeitungUngeklärt` über (manuell abschließbar; `ResendAsync`-Weg bleibt nach Korrektur möglich). |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine Änderung an `ScmIncomingEventStatus.NonBlocking` (Abschluss-/Serialisierungssemantik bleibt: `Wartend` blockiert Folgeereignisse und Abschluss weiterhin). | Die Anforderung betrifft nur die Platzvergabe; die Serialisierung ist laut Issue bereits korrekt geprüft und abgesichert („relevant bleibt nur, dass transiente Vorgänger weiterhin blockieren"). | bestätigt (im Issue-Text selbst festgelegt, 2026-11-24) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Platzbelegende Statusmenge | Neue eigene Menge `ScmIncomingEventStatus.StartSlotFreeing` (die sieben freigebenden Stati); platzbelegend = `!StartSlotFreeing.Contains(status)` — also genau die In-Flight-Stati `ZustellungVorbereitet`, `Zugestellt`, `InBearbeitung` **plus** jeder unbekannte/zukünftige Wert (konservativ). | Entkopplung von `NonBlocking` (Issue-Risikohinweis: eine künftige `NonBlocking`-Erweiterung darf nicht automatisch die Platzvergabe ändern). Negativ-Formulierung hält die „unbekannt = blockierend"-Faustregel der Statusklasse konsistent. Ein `NOT IN`-Prädikat bleibt EF-übersetzbar. |
| „Zustellbar und platzbedürftig" (Issue-Frage 1) | Nur In-Flight blockiert: `ZustellungVorbereitet`, `Zugestellt`, `InBearbeitung`. `Aufgenommen`, `Verzögert`, `Wartend` geben frei. | Akzeptanzkriterium: „WartetAufEingabe + zustellbarem, nicht gestartetem Ereignis → Platz frei". Zustellung an die eigene lebende Sitzung benötigt nie einen neuen Startplatz (Startplatz-Wache `adapter is null \|\| !adapter.IsAlive` vor `CheckAndReserveStartSlotAsync`). In-Flight signalisiert dagegen eine laufende Zustellung/Bearbeitung — die Prompt-Ready-Evidenz ist für dieses Fenster zu schwach. |
| `Wartend` mit `NextAttemptUtc = long.MaxValue` (Issue-Frage 2) | Nicht platzbelegend. | Bewegt sich ohne Benutzereingriff nie weiter; ein manueller Neuversand läuft ohnehin in die lebende Sitzung (kein neuer Start). Ewiges Platzfesthalten reproduziert exakt den Produktivbefund. Blockierend bleibt der Status ausschließlich für Abschluss-/Serialisierungssemantik (`NonBlocking` unverändert). |
| `NonBlocking`-Wiederverwendung (Issue-Frage 3) | Eigene Menge `StartSlotFreeing`, kein `NonBlocking`-Abgleich. | Semantisch verschiedene Fragen: „blockiert Abschluss/Reihenfolge?" ≠ „blockiert den Startplatz?". |
| Endzustand unzustellbarer Ereignisse (Issue-Frage 4) | `VerarbeitungUngeklärt` mit sprechendem `Reason`. | Terminal, nur manuell klärbar, bereits `NonBlocking` (kein Einfluss auf Platz, Abschluss, Serialisierung), im Queue-UI sichtbar und abschließbar; `ResendAsync` erlaubt Neuversand nach Korrektur (z. B. Repository zugeordnet, Aufgabe reaktiviert). `Abgebrochen` verwässerte die „vom Anwender"-Semantik; ein neuer Status wäre Schema-/UI-Aufwand ohne Mehrwert. |
| Verhinderung von Poll-Rauschen | Frühe Rückkehr in `DispatchCoreAsync`: manuell klärbare Stati (`VerarbeitungUngeklärt`, `ZustellungUngewiss`) ohne `DeliveryAttemptId` → `return` (nach dem Hostaktionen-Flush). | Die Batch-Auswahl würde solche Ereignisse sonst jeden Zyklus erneut in den vollen Zustellpfad schicken — und dort bei Bedarf erneut zustellen (Schutz vor Fehlzustellung im `EnsureHostActionAsync`-Kollisionsrandfall). Mit `DeliveryAttemptId` bleibt der Reconcile-Pfad unverändert erreichbar. |
| Altbestand | Keine Datenmigration für historische `Wartend`-Ereignisse auf beendeten Aufgaben. | Der Poll-Zyklus wählt `Wartend` ohne `NextAttemptUtc` ohnehin erneut aus — der neue Endzustand konvergiert beim nächsten Dispatch-Versuch selbsttätig. |
| Remote-Sichtbarkeit | Kein Remote-Kommentar beim Übergang in den unzustellbaren Endzustand. | „Wartet auf Verarbeitung" wäre für ein nie zustellbares Ereignis irreführend; der Status ist in der Queue sichtbar und dort klärbar. |

## Programmabläufe

### Platzfreigabe einer Prompt-bereiten Aufgabe (geändert)

1. `CheckAndReserveStartSlotAsync`/`GetStartBlockReasonAsync` → `EffectiveOccupancyAsync` → `GetOccupancyAsync` unter `AdmissionGate`.
2. `GetOccupancyAsync` ermittelt `readyIds` (laufendes Handle, nicht pausiert, `session.IsAlive && ReadyStatus == Ready`).
3. Pending-Abfrage zählt nur noch WorkItemIds mit `!ScmIncomingEventStatus.StartSlotFreeing.Contains(x.Status)`.
4. Aufgabe mit ausschließlich freigebendem Ereignis-Status → Platz frei; mit In-Flight- oder unbekanntem Status → Platz belegt.

Beteiligte Klassen: `ScmEventExecutionLimitService`, `ScmIncomingEventStatus`, `ScmStartSlotReservationPool`.

### Unzustellbar-Endzustand im Dispatch (neu)

1. `DispatchCoreAsync` (generisch), `DispatchPreparationAsync`, `OpenFromQueueAsync` laden die Aufgabe.
2. `UndeliverableReason(task)` prüft in Reihenfolge: `task is null` → „Aufgabe entfernt"; `Status in {Completed, Archived}` → „beendet/archiviert"; `GitRepositoryId is null || GitRepository is null` → „keine Repository-Zuordnung".
3. Treffer → `MarkUndeliverableAsync`: `Status = VerarbeitungUngeklärt`, `Reason`, `NextAttemptUtc = null`, speichern; Rückkehr ohne Zustell-/Kommentarschritte.
4. Kein Treffer → `BlockReason(task)` wie bisher (`WaitAsync`/`WaitWithCommentAsync`, pausiert bleibt `Wartend`).
5. Zusätzlicher Guard direkt nach dem `Verarbeitet`-Check: `DeliveryAttemptId is null && Status in {VerarbeitungUngeklärt, ZustellungUngewiss}` → `return`.

Beteiligte Klassen: `ScmEventOrchestrator`, `ScmIncomingEvent`, `WorkItem`, `ScmIncomingEventStatus`.

### Serialisierung/Abschluss (unverändert)

Vorgängerprüfung (Z. 194) und Abschlussmonitoring nutzen weiter `NonBlocking`; `VerarbeitungUngeklärt` ist dort bereits freigebend — Folgeereignisse blockierter Aufgaben laufen weiter, abgeschlossene Aufgaben behindern die Queue nicht mehr.

## Neue Klassen

Keine — Erweiterung bestehender Klassen folgt den vorhandenen Mustern.

## Änderungen an bestehenden Klassen

### `ScmIncomingEventStatus` (statische Konstantenklasse)

- **Neue Eigenschaft:** `StartSlotFreeing` (`IReadOnlyCollection<string>`) — `{Aufgenommen, Verzögert, Wartend, ZustellungUngewiss, VerarbeitungUngeklärt, Verarbeitet, Abgebrochen}` mit XML-Doku der Platzbelegungsregel (nur In-Flight + unbekannt blockieren).

### `ScmEventExecutionLimitService` (Service)

- **Geänderte Methode:** `GetOccupancyAsync` — Pending-Abfrage von `x.Status != "Verarbeitet" && x.Status != "Abgebrochen"` auf `!ScmIncomingEventStatus.StartSlotFreeing.Contains(x.Status)`; ersetzt die String-Literale durch die Konstantenmenge (Issue-Konsistenzhinweis).
- **Geänderte Doku:** XML-Dokumentation des Records `CliExecutionOccupancy` und der Methode `GetOccupancyAsync` an die In-Flight-Regel anpassen („ohne platzbedürftiges Ereignis").

### `ScmEventOrchestrator` (Service)

- **Neue Methoden:**
  - `UndeliverableReason(WorkItem? task)` → `string?` — private Hilfsfunktion (siehe Ablauf); prüft `GitRepositoryId is null || GitRepository is null` für die Repository-Zuordnung (in `OpenFromQueueAsync` wird die Aufgabe dafür künftig per `Include(x => x.GitRepository)` geladen).
  - `MarkUndeliverableAsync(ScmIncomingEvent item, string reason, CancellationToken ct)` — setzt `Status = VerarbeitungUngeklärt`, `Reason`, `NextAttemptUtc = null`, speichert.
- **Geänderte Stellen:**
  - `DispatchCoreAsync`: Guard für manuell klärbare Stati ohne `DeliveryAttemptId` nach dem `Verarbeitet`-Check; `task is null` → `UndeliverableReason`; `Blocked`-Prüfungen (vor und nach dem Sitzungs-Reload) jeweils `UndeliverableReason` vor `BlockReason` vorziehen.
  - `DispatchPreparationAsync`: gleiche Ersetzung an beiden `task`-/`blocked`-Stellen.
  - `OpenFromQueueAsync`: `Include(x => x.GitRepository)` + `UndeliverableReason` vor `BlockReason`.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — keine neuen Eingaben; Statuswertebereich bleibt `ScmIncomingEventStatus`-verwaltet.

## Konfigurationsänderungen

Keine — `scm.execution.maxparallel` und `plugins.maxparallel.<prefix>` bleiben unverändert wirksam.

## Seiteneffekte und Risiken

- **Kurzzeitige Limit-Überschreitung:** Ein freigegebener Platz kann belegt werden, während eine Zustellung an die Ready-Sitzung anläuft — vom Issue als toleriert dokumentiert (nur Starts werden begrenzt); das In-Flight-Fenster minimiert den Zeitraum.
- **Veränderte Queue-Semantik:** `Wartend`-Ereignisse auf beendeten Aufgaben wechseln beim nächsten Poll/Dispatch in `VerarbeitungUngeklärt` — sichtbare Statusänderung in der Ereignis-Queue, gewollt und klärbar.
- **Bestehende Tests:** `GetStartBlockReasonAsync_ReadySessionWithPendingEvent_CountsAsOccupied`, `Dispatch_EndedOrArchivedTask_StaysBlocked`, `Monitor_ReusedBlockedTask_StaysWaitingWithoutStart` müssen den neuen Erwartungen angepasst werden.
- **Atomarität/Kosten:** Die Pending-Abfrage bleibt eine gebündelte, nur bei Ready-Kandidaten laufende Abfrage unter `AdmissionGate` — keine zusätzlichen Roundtrips.

## Umsetzungsreihenfolge

1. **`ScmIncomingEventStatus.StartSlotFreeing` ergänzen**
   - Voraussetzungen: Keine.
   - Beschreibung: Konstantenmenge + XML-Doku; kein Verbraucher zunächst.
2. **`GetOccupancyAsync` umstellen + Doku**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Pending-Abfrage und Kommentare auf die neue Menge umstellen.
3. **Orchestrator: `UndeliverableReason` + `MarkUndeliverableAsync` + Anwendung an allen fünf Stellen + Guard**
   - Voraussetzungen: Keine (unabhängig von 1–2, gleicher Commit).
   - Beschreibung: Neue private Hilfsmethoden; `UndeliverableReason` vor `BlockReason` an den `task`-/`blocked`-Stellen in `DispatchCoreAsync`, `DispatchPreparationAsync`, `OpenFromQueueAsync` (mit `Include`); Guard für manuell klärbare Stati ohne `DeliveryAttemptId`.
4. **Unit-Tests anpassen/ergänzen** (`ScmEventExecutionLimitServiceTests`, `ScmIncomingEventStatusTests`)
   - Voraussetzungen: Schritte 1–3.
   - Beschreibung: Theory splitten (In-Flight belegt vs. freigebend), `AddEventAsync` um `NextAttemptUtc` erweitern, neue Fälle (unbekannter Status, `long.MaxValue`).
5. **Integrationstests anpassen/ergänzen** (`ScmEventDispatchTests_Limit`, `ScmEventMonitoringServiceTests_AutoStartDelivery`)
   - Voraussetzungen: Schritte 1–3.
   - Beschreibung: Endzustand-Erwartungen; neue Szenarien (Ready + `VerarbeitungUngeklärt`/`Wartend` → Platz frei; fehlende Repository-Zuordnung → `VerarbeitungUngeklärt`).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetStartBlockReasonAsync_ReadySessionNotYetDeliveredEvent_FreesSlot` (Theory: `Aufgenommen`, `Verzögert`, `Wartend`, `ZustellungUngewiss`, `VerarbeitungUngeklärt`) | `ScmEventExecutionLimitServiceTests` | Platz frei bei zustellbarem, nicht gestartetem bzw. manuell klärbarem Ereignis |
| `GetStartBlockReasonAsync_ReadySessionInFlightEvent_CountsAsOccupied` (Theory: `ZustellungVorbereitet`, `Zugestellt`, `InBearbeitung`, unbekannter Status `"ZukuenftigerStatus"`) | `ScmEventExecutionLimitServiceTests` | Platz bleibt bei In-Flight bzw. konservativ bei unbekanntem Status belegt (umbenannte bestehende Theory) |
| `GetStartBlockReasonAsync_ReadySessionWaitingWithMaxAttempt_FreesSlot` | `ScmEventExecutionLimitServiceTests` | `Wartend` + `NextAttemptUtc = long.MaxValue` gibt den Platz frei |
| `StartSlotFreeing_ShouldContainExactlyTheSevenFreeingStatuses` | `ScmIncomingEventStatusTests` | Mengeninhalt exakt (Koppelschutz) |
| `StartSlotFreeing_ShouldClassifyAllKnownStatuses` + unbekannte Stati | `ScmIncomingEventStatusTests` | In-Flight + unbekannt blockieren; alle übrigen geben frei |
| `Dispatch_EndedOrArchivedTask_MarkedUndeliverable` (Theory Completed/Archived; Umbenennung der bestehenden) | `ScmEventDispatchTests_Limit` | `VerarbeitungUngeklärt` + Grund statt `Wartend`; erneuter Dispatch bleibt idempotent |
| `Dispatch_TaskWithoutRepository_MarkedUndeliverable` | `ScmEventDispatchTests_Limit` | `GitRepositoryId = null` → `VerarbeitungUngeklärt` |
| `Dispatch_RemovedTask_MarkedUndeliverable` | `ScmEventDispatchTests_Limit` | Waisen-Ereignis (Aufgabe via `ExecuteDelete` entfernt) → `VerarbeitungUngeklärt` |
| `Dispatch_TaskReadyWithUndeliverableOnlyEvent_SlotFreed` | `ScmEventDispatchTests_Limit` | Ready-Sitzung mit nur `VerarbeitungUngeklärt`-Ereignis → wartendes Folgeereignis einer anderen Aufgabe startet |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `GetStartBlockReasonAsync_ReadySessionWithPendingEvent_CountsAsOccupied` (`ScmEventExecutionLimitServiceTests`) | Nur noch In-Flight + unbekannte Stati belegen; wird zur In-Flight-Theory |
| `Dispatch_EndedOrArchivedTask_StaysBlocked` (`ScmEventDispatchTests_Limit`) | Endzustand `VerarbeitungUngeklärt` statt `Wartend`; Grundtext ändert sich |
| `Monitor_ReusedBlockedTask_StaysWaitingWithoutStart` (`ScmEventMonitoringServiceTests_AutoStartDelivery`) | Completed/Archived-Fälle → `VerarbeitungUngeklärt`; New+paused bleibt `Wartend` |

### E2E-Tests (primärer Funktionsnachweis)

Keine — die Anforderung berührt keinen Benutzerfluss: Die Platzvergabe und der Statusübergang sind reine Hintergrundlogik ohne neue oder geänderte UI-Interaktion; die Ereignis-Queue zeigt `VerarbeitungUngeklärt` bereits als bekannten Status an. Der Funktionsnachweis liegt auf Prozessebene: Die Integrationstests (`ScmDispatchFixture` mit echtem SQLite, echtem Orchestrator und deterministischer Terminal-Grenze) treiben den vollständigen Dispatch-Pfad vom Ereigniseingang bis zur Status- bzw. Platzwirkung — das ist die höchste hier sinnvoll automatisierbare Abnahmeebene; eine manuelle E2E-Abnahme der produktiven Queue-Situation obliegt dem Anwender (die Sandbox enthält keine produktive Stausituation).

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Platzfreigabe | Occupancy-Bewertung (Limit-Prüfung eines neuen Starts) | Aufgabe A: Prozess läuft, Sitzung `Ready`, Ereignis `VerarbeitungUngeklärt`; globales Limit 1; Ereignis E auf Aufgabe B wartet mit Limit-Grund | Bewertung zählt A nicht mehr → E wird bei nächstem Dispatch zugestellt | E verlässt `Wartend`, Queue fließt | `Dispatch_TaskReadyWithUndeliverableOnlyEvent_SlotFreed` + Unit-Theorien |
| 2 | In-Flight hält Platz | Occupancy-Bewertung | Aufgabe A: `Ready`-Sitzung, Ereignis `Zugestellt`/`InBearbeitung` | Bewertung zählt A voll | Kein Doppelstart; Wartegrund bleibt | `GetStartBlockReasonAsync_ReadySessionInFlightEvent_CountsAsOccupied`, `Dispatch_TaskReadyWithPendingEvent_SlotStaysOccupied` (bestehend) |
| 3 | Beendete Aufgabe | Poll-/Dispatch-Zyklus | Ereignis `Aufgenommen`/`Wartend` auf `Completed`/`Archived`-Aufgabe | Dispatch → `UndeliverableReason` → `VerarbeitungUngeklärt` | Queue-Eintrag erhält klaren, abschließbaren Zustand; kein ewiges Wiederwählen (Guard) | `Dispatch_EndedOrArchivedTask_MarkedUndeliverable` |
| 4 | Fehlende Voraussetzung: Repository | Poll-/Dispatch-Zyklus | Aufgabe ohne `GitRepositoryId` mit Ereignis | Dispatch → `VerarbeitungUngeklärt` mit Zuordnungshint | Statt Crash-Pfad `long.MaxValue`-Wartestand | `Dispatch_TaskWithoutRepository_MarkedUndeliverable` |
| 5 | Wiederaufnahme nach Klärung | Nutzer korrigiert (Repository zuordnen) → `ResendAsync`/`RetryAsync` | Ereignis `VerarbeitungUngeklärt` | Resend setzt `Aufgenommen`; regulärer Pfad | Ereignis wird zugestellt | Bestehender `ResendAsync`-Mechanismus (unverändert) |

## Offene Punkte

Keine — die vier im Issue offenen Fragen sind als Designentscheidungen festgelegt (siehe Tabelle oben; die Entscheidungen folgen den Akzeptanzkriterien des Issues).
