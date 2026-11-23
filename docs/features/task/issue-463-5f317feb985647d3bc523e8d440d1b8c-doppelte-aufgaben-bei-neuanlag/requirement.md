# Übersetzte Anforderung: Doppelte Aufgaben bei Neuanlage

**Quelle:** Issue #463 / Aufgaben-ID `5f317feb-9856-47d3-bc52-3e8d440d1b8c`
**Branch:** `task/issue-463-5f317feb985647d3bc523e8d440d1b8c-doppelte-aufgaben-bei-neuanlag`

## Fachliche Zusammenfassung

Im SCM-Ereigniskanal entstehen bei einer Issue-Neuanlage mit unmittelbar folgendem Bearbeitungs-Kommentar aktuell zwei Aufgaben für dasselbe Issue: Die `ScmContentKind.Description`-Beobachtung löst über `ScmIssuePreparationService.DetectAndEnqueueAsync` eine zweck-markierte Aufbereitungs-Aufgabe (`WorkItem.Purpose` = `ScmIssuePreparationService.Purpose`, `WorkItemKind.IssuePreparation`, `IssueReference.SourceKey` aus `PreparationSourceKey`) samt internem `ScmIncomingEvent` (`Purpose = "IssueAufbereitung"`) aus; die `ScmContentKind.IssueComment`-Beobachtung wird anschließend über `ScmEventTaskResolver.ResolveIssueAsync` einer Aufgabe zugeordnet — der Resolver filtert jedoch auf `a.Purpose == null` und schließt die Aufbereitungs-Aufgabe damit aus, sodass `WorkItemService.CreateFromIssueEventAsync` eine zweite, reguläre Anweisungsaufgabe anlegt. Die Anforderung verlangt: Beide Ereignisse werden weiterhin vollständig aufgenommen, das Aufbereitungs-Ereignis erhält Vorrang in der Verarbeitungsreihenfolge und am Ausführungs-Slot des betreffenden KI-Plugins, und das Kommentar-Ereignis wird der bereits vorhandenen Aufgabe zugeordnet statt eine neue anzulegen. Ergänzend ist die Zustellung in eine wartende CLI um eine Stabilisierungsfrist von 10 Sekunden abzusichern.

## Fachlicher Scope

- **Verarbeitungseinheit:** Ein `ScmIncomingEvent` des SCM-Ereigniskanals im Neuanlage-Szenario — konkret das Paar aus internem Aufbereitungs-Ereignis (`Purpose` = `ScmIssuePreparationService.Purpose`, `EventKey` aus `PreparationEventKey`, erzeugt aus der `Description`-Beobachtung) und externem Kommentar-Ereignis (`ScmContentKind.IssueComment`) derselben `ScmEventReference` (`Kind` = `ScmReferenceKind.Issue`, gleiche `Number` im selben `ScmEventRepository`).
- **Auslösende Ereignisse:**
  - Poll-Zyklus des `ScmEventMonitoringService` (repository-weiter Enumerationskanal): „Issue erstellt"-Erkennung über den Beschreibungs-Batch → `DetectAndEnqueueAsync`; qualifizierte Kommentar-Beobachtung → `ScmEventTaskResolver.ResolveAsync` + `ScmEventStore.AcceptBatchAsync`.
  - Freiwerden eines Ausführungs-Slots (`ScmEventExecutionLimitService.CheckAndReserveStartSlotAsync`): ereignisgesteuerte Startprüfung via `ScmStartCheckTrigger.RequestStartCheck` bzw. `ScmEventOrchestrator.DispatchDueEventsAsync` als Poll-Sicherheitsnetz.
  - Statuswechsel der laufenden CLI-Sitzung auf Eingabebereitschaft bzw. „Wartend" während eines Zustellversuchs (`IScmPromptSessionAdapter.ReadyStatus`/`ReadyStatusChanged`, `ScmPromptReadyStatus`).
- **Erwartete Wirkung:**
  - Es existiert anschließend genau eine `WorkItem` für das Issue (sichtbar in der Aufgabenliste); kein Duplikat aus dem Kommentar-Ereignis.
  - Beide Ereignisse sind in der SCM-Ereignisliste sichtbar; das Kommentar-Ereignis trägt die `WorkItemId` der einen Aufgabe.
  - Der Aufbereitungs-Prompt (`DispatchPreparationAsync`, `ScmIssuePreparationPrompt`) wird vor der Zustellung des Kommentar-Ereignisses ausgeführt, sofern beide auf denselben Ausführungs-Slot konkurrieren.
  - Ein Prompt wird erst nach 10 Sekunden stabil anhaltendem Wartezustand in die laufende CLI geschrieben (`IScmPromptSessionAdapter.WritePromptAsync`); wechselt der Status innerhalb der Frist, wird der Zustellversuch abgebrochen und das Ereignis bleibt/kehrt auf `ScmIncomingEventStatus.Wartend`.
- **Umgang mit fehlenden Objekten:**
  - *Zweck-markierte Aufbereitungs-Aufgabe bereits vorhanden* (Normalfall: der `Description`-Batch läuft im Poll-Zyklus vor dem `IssueComment`-Batch): **wiederverwenden** — das Kommentar-Ereignis wird dieser Aufgabe zugeordnet, keine neue Aufgabe anlegen.
  - *Noch keine Aufgabe zur Issue-Referenz bei der Kommentar-Auflösung* (entgegengesetzte Eingangsreihenfolge, Inbox-Modus, getrennte Poll-Zyklen): ungeklärt — reguläre Anlage mit ggf. nachträglicher Zusammenführung oder Warten auf die Aufbereitungs-Aufgabe; Kunden-Klärungsfrage, siehe Abschnitt „Offene Punkte".
- **Scope-Ausschlüsse:**
  - Kein Verwerfen von Ereignissen: Aufnahme und Registrierung beider Ereignisse bleiben unverändert (explizite Kundenanforderung).
  - Der beschriebene Duplikatschutz betrifft das Neuanlage-Szenario (Issue-Neuanlage + Kommentar); ob die 10-Sekunden-Regel generell für jede Prompt-Zustellung in eine wartende CLI gilt, ist offen (siehe „Offene Punkte").
  - Keine Änderung an der Trigger-Validierung (`ScmEventValidationService.GetRejection`), den Hostaktionen/`ScmOutgoingAction`-Outbox oder dem Exchange-Protokoll (`ScmEventExchangeProtocol`) aus der Anforderung ableitbar — Annahme.
  - Bereits bestehende Doppel-Aufgaben aus vergangenen Läufen werden nicht nachträglich bereinigt — Annahme, in der Anforderung nicht adressiert.

## Betroffene Klassen und Komponenten

### Bestehende, voraussichtlich zu ändernde Artefakte

- `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` — `ResolveIssueAsync` filtert bei der Aufgabensuche auf `a.Purpose == null` (Zeilen ~63–67) und schließt zweck-markierte Aufbereitungs-Aufgaben damit aus; Kern der Änderung für die Anforderung „keine doppelte Aufgabenanlage".
- `src/Softwareschmiede/Application/Services/ScmIssuePreparationService.cs` — `Purpose`-Konstante, `PreparationSourceKey`, `PreparationEventKey`, `DetectAndEnqueueAsync` (Anlage der Aufbereitungs-Aufgabe + internes Ereignis); Bezugspunkt für die Auflösung des Kommentar-Ereignisses auf die Aufbereitungs-Aufgabe.
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` — `DispatchCoreAsync` (generischer Zustellpfad, Vorgängerprüfung Zeile ~183, Slot-Admission Zeile ~246, `WritePromptAsync` Zeile ~320) und `DispatchPreparationAsync` (dedizierter Aufbereitungspfad, Slot-Admission Zeile ~713, `WritePromptAsync` Zeile ~796); Ort für Slot-Vorrang, Schutz der laufenden Aufbereitung und die Stabilisierungsfrist vor `WritePromptAsync`.
- `src/Softwareschmiede/Application/Services/ScmDispatchPriority.cs` — `OrderForDispatch`/`PriorityClass` bestimmen die Dispatch-Reihenfolge fälliger Ereignisse; zu erweitern um den Vorrang des Aufbereitungs-Ereignisses (`Purpose`) bei gleicher Eingangsordnung.
- `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs` / `ScmStartSlotReservationPool.cs` — atomare Platzprüfung/-belegung je `PluginPrefix` (Ausführungsbegrenzung); Berührungspunkt des Slot-Vorrangs.
- `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs` — `IScmPromptSessionAdapter` mit `ReadyStatus`, `IsBusy`, `WaitForInputReadyAsync`, `ReadyStatusChanged` und `WritePromptAsync`; technische Basis des „wartende CLI"-Zustands für die Stabilisierungsfrist.
- `src/Softwareschmiede/Application/Services/ScmEventStore.cs` — `AcceptBatchAsync` (atomare Inbox-Aufnahme; Eingangsordnung über `ScmIncomingEvent.Sequence`/`ReceivedUtc`).
- `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs` — Poll-Ablauf je Referenzgruppe und Quell-Kind (`Description` vor `IssueComment`, Zeilen ~170–214); Bezugspunkt der Verarbeitungsreihenfolge und des Inbox-Modus.
- Statusbegriffe des „Wartend" (zur Namensklärung, siehe Offene Punkte): `ScmIncomingEventStatus.Wartend` (Ereignisstatus), `WorkItemStatus.Waiting` (Aufgaben-Lebenszyklus, Rate-Limit-Pause), `WorkItemRunStatus.WaitingForInput` (Lauf-Substatus, persistiert als „WartetAufEingabe"), `ScmPromptReadyStatus` (`Ready`/`Busy`/`TrustPrompt`/`Exited`), `AiExecutionState.Waiting` (fachliche Klassifikation via `AiExecutionStatusClassification.Classify`).
- `src/Softwareschmiede/Application/Services/WorkItemService.cs` — `CreateFromIssueEventAsync` (Anlage samt `IssueReference.SourceKey`-Dedup); bleibt Anlagepfad, wird im Normalfall umgangen.
- `src/Softwareschmiede/Domain/Entities/WorkItem.cs` / `IssueReference.cs` — `WorkItem.Purpose`, `WorkItem.Kind` (`WorkItemKind.IssuePreparation` → `Instruction` nach Zustellung), `IssueReference.IssueNumber`/`SourceKey`; kein Schema-Eingriff aus der Anforderung ableitbar — Annahme.

### Neu zu erstellende Artefakte (voraussichtlich, Namen sind Vorschläge)

- Voraussichtlich **keine** neue Entity und keine Migration erforderlich — Annahme (hängt davon ab, ob die Stabilisierungsfrist konfigurierbar wird).
- Möglicherweise eine gekapselte Stabilisierungsprüfung (Warte- + Re-Check-Logik vor der Zustellung), z. B. als Erweiterung des `ScmEventOrchestrator` oder des `ScmPromptSessionAdapter` — Entwurfsentscheidung in der Planung.

### Tests

- `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_IssuePreparation.cs` — Neuanlage-Szenario: genau eine Aufgabe, beide Ereignisse derselben `WorkItemId` zugeordnet.
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_TaskAssignment.cs` — Auflösung des Kommentar-Ereignisses auf die zweck-markierte Aufbereitungs-Aufgabe.
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_IssuePreparation.cs` / `ScmEventDispatchTests_Ordering.cs` — Verarbeitungsreihenfolge, Slot-Vorrang und Verhalten bei Wartezustand der CLI.
- `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` — Fixture für Dispatch-/Zustelltests.
- `src/Softwareschmiede.Tests/E2E/E2E_IssuePreparation.cs` / `E2E_ScmEvents.cs` — E2E-Absicherung des Neuanlage-Szenarios (konsolidierte Testmethoden gemäß Projektregel).

## Implementierungsansatz

- **Aufgabenzuordnung (keine Doppelanlage):** `ScmEventTaskResolver.ResolveIssueAsync` so erweitern, dass neben regulären Aufgaben (`Purpose == null`) auch die zweck-markierte Aufbereitungs-Aufgabe derselben Issue-Nummer gefunden wird (z. B. über `IssueReference.SourceKey == ScmIssuePreparationService.PreparationSourceKey(reference)` oder `Purpose == ScmIssuePreparationService.Purpose`). Das Kommentar-Ereignis erhält deren `WorkItemId`; es entsteht keine zweite Aufgabe. Die `Ambiguous`-Diagnose bleibt für mehrere konkurrierende Aufgaben bestehen — die fachliche Regel, welche Aufgabe im Mehrdeutigkeitsfall gilt, ist Teil der Offenen Punkte.
- **Verarbeitungsreihenfolge:** Im Enumerationskanal läuft der `Description`-Read bereits vor dem `IssueComment`-Read und `DetectAndEnqueueAsync` vor `AcceptBatchAsync`; die Aufbereitungs-Aufgabe existiert daher in diesem Szenario zum Zeitpunkt der Kommentar-Auflösung bereits. Innerhalb derselben Aufgabe blockiert die Vorgängerprüfung des Orchestrierers (`ScmEventOrchestrator.cs` Zeile ~183: früheres Ereignis ohne `NonBlocking`-Status) die Zustellung des Kommentars automatisch bis zum Abschluss des Aufbereitungs-Ereignisses. Für gleiche Eingangsordnung über Aufgabengrenzen hinweg ist `ScmDispatchPriority` um einen `Purpose`-Vorrang zu erweitern.
- **Slot-Vorrang:** Bei freiwerdendem Platz (Startprüfung via `ScmStartCheckService`/`ScmStartCheckTrigger` oder Poll über `DispatchDueEventsAsync`) muss die Auswahl fälliger Ereignisse das Aufbereitungs-Ereignis vorziehen — Erweiterung der Priorisierung (`ScmDispatchPriority.OrderForDispatch`/`PriorityClass`), sodass die Aufbereitung die erste `CheckAndReserveStartSlotAsync`-Zulassung des Plugins erhält.
- **Schutz der laufenden Aufbereitung:** Während die Aufbereitungs-CLI ausgeführt wird und ihr Ausführungsstatus auf „Wartend" wechselt, darf das Kommentar-Ereignis nicht dazwischen zugestellt werden. Konkreter Statusbegriff (Sitzungs-`ReadyStatus`/`IsBusy`, `WorkItemRunStatus.WaitingForInput` oder `WorkItemStatus.Waiting`) ist offener Kundenpunkt; der technische Eingriffspunkt liegt an denselben Stellen, an denen der Orchestrierer heute „CLI beschäftigt; wartet auf Eingabebereitschaft." meldet.
- **Stabilisierungsfrist:** Am Zustellpunkt vor `IScmPromptSessionAdapter.WritePromptAsync` (generischer Pfad in `DispatchCoreAsync`; analoge Erwägung für `DispatchPreparationAsync` und `PromptScheduleDispatchService` — Geltungsbereich offen) eine Warte-Re-Check-Sequenz einbauen: Bei erkannter Eingabebereitschaft/wartender CLI zunächst 10 Sekunden warten, dann den Status erneut prüfen. Bei anhaltendem Wartezustand erfolgt die Zustellung; andernfalls Abbruch des Zustellversuchs und `ScmIncomingEvent.Status` = `ScmIncomingEventStatus.Wartend` (kein `ZustellungUngewiss`, da kein Schreibversuch begonnen wurde). Der Vorgängerstatus/Wiederanlauf wird über `NextAttemptUtc`/`Attempts` gesteuert.
- **Abhängigkeiten:** `ScmEventStore`, `WorkItemService`, `IScmPromptSessionAdapter`, `ScmEventExecutionLimitService`, `ScmDispatchPriority`, `TimeProvider`, `ScmStartCheckTrigger`, `AppSettingService` (nur falls die Frist konfigurierbar wird).

## Konfiguration

- Die Frist von 10 Sekunden ist in der Anforderung fest genannt; Konfigurierbarkeit ist ein offener Kundenpunkt (siehe „Offene Punkte"). Bei Konfigurierbarkeit läge die Ebene anwendungsweit im `AppSettingService` (Muster: `GetScmIssuePreparationEnabledAsync`, `GetScmIssuePreparationPluginPrefixAsync`, `GetScmIssuePreparationCommentEnabledAsync`), nicht pro Repository oder pro Aufgabe — Annahme, sofern Konfigurierbarkeit gewünscht wird.

## Offene Fragen

Die vom Kunden gestellten Klärungsfragen stehen unverändert im Abschnitt „Offene Punkte" am Ende dieses Dokuments. Ergänzend intern zu klären:

1. **Technischer Statusbegriff der Frist:** Auf welchen konkreten Zustand bezieht sich „wartende CLI" technisch — `IScmPromptSessionAdapter.ReadyStatus`/`IsBusy` (`ScmPromptReadyStatus`), den Lauf-Substatus `WorkItemRunStatus.WaitingForInput` oder den Lebenszyklusstatus `WorkItemStatus.Waiting`? (Spiegelt die Kundenfrage; die konkrete Implementierung hängt davon ab.)
2. **Geltungsbereich der Stabilisierungsprüfung:** Betrifft sie neben dem SCM-Zustellpfad auch andere Prompt-Zustellungen in laufende CLIs (`PromptScheduleDispatchService.WritePromptAsync`, manueller Versand)? — Annahme: nein, Scope ist der SCM-Ereigniskanal.
3. **Übergang der Aufgabenart:** Die Aufbereitungs-Aufgabe wechselt `WorkItem.Kind` nach erfolgreicher Zustellung des Analyse-Prompts zu `Instruction`; `WorkItem.Purpose` bleibt zur Routing-Isolation `"IssueAufbereitung"`. Ist die Zuordnung des regulären Kommentar-Ereignisses (`Purpose == null` am Ereignis) zu einer `Purpose`-Aufgabe mit dieser Isolation verträglich (generischer Zustellpfad greift weiterhin, da die Ereignis-`Purpose` steuert, nicht die Aufgaben-`Purpose`) — Annahme: ja, zu verifizieren.
4. **Retry-Semantik der Frist:** Falls die Frist je Zustellversuch wiederholt wird (siehe Kundenfrage), ist das Backoff über `ScmEventStore.RetryAt`/`Attempts` anzuwenden oder eine feste Neuplanung? — Annahme: bestehender `Wartend`-Mechanismus mit normalem Re-Poll.

## Offene Punkte

- **Zielaufgabe des Kommentar-Ereignisses:** Ist mit „bereits vorliegender Aufgabe" die zweck-markierte Aufbereitungs-Aufgabe gemeint, und soll das Kommentar-Ereignis dieser Aufgabe direkt zugeordnet werden (Folge-Anweisung in derselben Aufgabe/CLI-Sitzung)? Zweck-markierte Aufgaben sind aktuell von der regulären Aufgabenauflösung ausgenommen.
- **Begriff „Ausführungsstatus = Wartend":** Welcher konkrete Status ist gemeint — die Eingabebereitschaft der laufenden CLI-Sitzung (wartet auf Eingabe), der Aufgaben-Lebenszyklusstatus „Wartend" (Rate-Limit-Pause) oder der Ereignisstatus „Wartend"?
- **Geltungsbereich der 10-Sekunden-Regel:** Gilt sie nur für Kommentar-Ereignisse im Neuanlage-Szenario oder generell für jede Prompt-Zustellung in eine wartende CLI?
- **Wiederholung der Frist:** Wird die 10-Sekunden-Prüfung einmalig je Zustellversuch angewendet oder bei jedem erneuten Versuch? Wie verhält sich das Ereignis, wenn der Status dauerhaft wechselt (Gefahr endlosen Wartens)?
- **Konfigurierbarkeit:** Soll die Frist (10 Sekunden) fest verdrahtet oder konfigurierbar sein?
- **Entgegengesetzte Eingangsreihenfolge:** Was gilt, wenn das Kommentar-Ereignis zeitlich vor der Aufbereitungs-Erkennung eingelesen wird (z. B. Inbox-Modus, getrennte Poll-Zyklen)? Soll die Aufgabenzuordnung nachträglich korrigiert werden?
