# Umsetzungsplan: Ausführungsbegrenzung — Platz bei wartender CLI am Eingabeprompt freigeben

## Übersicht

`ScmEventExecutionLimitService.GetOccupancyAsync` erhält neben der Pausenfreigabe eine zweite
Freigaberegel: Eine Aufgabe belegt weder im globalen noch im Plugin-Zähler einen Platz, wenn ihre
SCM-Promptsitzung einen verifizierten Eingabeprompt zeigt (`IsAlive && ReadyStatus == Ready`) und
kein unverarbeitetes `ScmIncomingEvent` der Aufgabe existiert. Als Auslöser der ereignisgesteuerten
Startprüfung dient ein neues `ReadyStatusChanged`-Signal am Adapter (verdrahtet über
`AiExecutionService` → `ScmStartCheckTrigger`) plus einem ergänzenden Trigger beim Erreichen eines
Ereignis-Endstatus in `ScmEventOrchestrator.DispatchCoreAsync`. Betroffen sind ausschließlich
`ScmEventExecutionLimitService`, `ScmPromptSessionAdapter`/`IScmPromptSessionAdapter`,
`AiExecutionService`, `ScmEventOrchestrator`, die zugehörigen XML-/Doku-Kommentare sowie Tests —
keine Migration, keine Konfiguration, keine UI.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Ein Belegungsplatz der SCM-Ausführungsbegrenzung: die Zählbewertung einer einzelnen laufenden CLI-Ausführung (lebendes Prozess-Handle einer Aufgabe) im globalen und im Plugin-Zähler, ausgewertet bei jeder Startzulassungsprüfung (`CheckAndReserveStartSlotAsync` / `GetStartBlockReasonAsync` → `EffectiveOccupancyAsync` → `GetOccupancyAsync`). |
| Auslösende Ereignisse | **Bewertung:** jede Startzulassung liest die Belegung live neu — kein eigener Cache, kein eigener Zeitpunkt. **Freigabesignal 1:** `IScmPromptSessionAdapter.ReadyStatusChanged` mit Übergang nach `Ready`, verdrahtet in `AiExecutionService` auf `ScmStartCheckTrigger.RequestStartCheck()`. **Freigabesignal 2 (Randfall):** Ereignis erreicht in `DispatchCoreAsync` einen Endstatus (`Verarbeitet`/`Abgebrochen`) → `RequestStartCheck()`. **Bestandsauslöser bleiben:** `RunningCountChanged` (sinkend), `WorkItemService.SetPauseAsync`, `ScmEventStore`, `AiPluginLimitService`, `ScmEventOrchestrator.CancelAsync`. **Sicherheitsnetz:** Poll des `ScmEventMonitoringService` (60 s). |
| Erwartete Wirkung | Im Zielszenario (z. B. `plugins.maxparallel.Softwareschmiede.Devin = 1`) gibt eine fertige, am Eingabeprompt wartende Aufgabe ihren Platz frei; das nächste startbare Ereignis startet ereignisgesteuert statt erst beim Poll. Konservativ zählend bleiben: fehlende Session, `ReadyStatus` `Unknown`/`TrustPrompt`/`Busy`/`Exited`, Handles ohne Aufgabenzeile, jede Aufgabe mit offenem `ScmIncomingEvent`. Kehrt eine freigegebene Aufgabe zur Arbeit zurück (`MarkInputStarted` → `Busy` oder neues offenes Ereignis), zählt sie bei der nächsten Bewertung wieder voll — eine daraus mögliche zeitweise Überschreitung des Limits ist toleriert und dokumentiert (das Limit begrenzt ausschließlich Starts). |
| Umgang mit fehlenden Objekten | Kein `IScmPromptSessionAdapter` für die Aufgabe (`GetScmPromptSession` → `null`, klassischer Start ohne SCM-Startvertrag) → konservativ belegt, kein Fehler. Keine `ScmIncomingEvent`-Zeilen zur Aufgabe → kein Pending → freigabefähig bei verifiziertem `Ready`. `ScmStartCheckTrigger` nicht registriert (manuelle DI-Konstruktionen in Tests) → optionaler Parameter, wirkungslose Anforderung, kein Fehler (Poll bleibt Fallback). |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Kein Freigabekriterium über `WorkItemRunStatus.WaitingForInput`/`CliRuntimeStatus.WaitingForInput` — nur I/O-Ruhe-Heuristik (~4 s), tritt auch mitten im Turn auf und würde das Limit faktisch aufheben; kommt auch nicht als Auslösesignal zum Zug (siehe Designentscheidung). | Explizite Issue-Vorgabe; zählt nie als Kriterium. | bestätigt |
| 2 | Kein Beenden, Drosseln oder Eingriff in den am Prompt wartenden CLI-Prozess — der Prozess läuft bewusst weiter; fachlicher Abschluss bleibt `ScmTaskCompletionMonitoringService` bzw. Anwender überlassen. | Explizite Issue-Vorgabe. | bestätigt |
| 3 | Keine Änderung an Limit-Konfiguration (`scm.execution.maxparallel`, `plugins.maxparallel.<prefix>`), Priorisierung (`ScmDispatchPriority`), Reservierungssemantik (`ScmStartSlotReservationPool`), `BlockReason`-Sperren oder der Unbegrenztheit reiner Hostaktionen/Reconcile-Versuche. | Issue-Scope; die erweiterte Freigabe wirkt ausschließlich in der Belegungszählung. | bestätigt (2026-11-21) |
| 4 | Keine UI-/Anzeigeänderung — die „Wartet"-Kachelanzeige (`WorkItemRunStatus.WaitingForInput`) bleibt rein informativ. | Issue-Scope. | bestätigt (2026-11-21) |
| 5 | Keine neue Konfiguration, keine Datenbankmigration. | Issue-Scope; Verhalten gilt implizit unter den bestehenden Limits und dem Laufzeit-Schalter der Ereignisüberwachung. | bestätigt (2026-11-21) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Auslösesignal der Startprüfung | **Variante (b):** dediziertes `ReadyStatusChanged`-Event am `IScmPromptSessionAdapter`; `AiExecutionService` abonniert es bei der Adapter-Anlage und ruft bei `Ready` `startCheckTrigger?.RequestStartCheck()`. | Feuert exakt beim Übergang des Freigabekriteriums — Variante (a) (`CliRuntimeStatus.WaitingForInput` in `CliProcessManager`) hängt von der ~4-s-I/O-Ruhe-Heuristik ab: sie feuert verzögert, feuert falsch positiv mitten in stillen Turn-Phasen (vergeudete Dispatch-Scans) und kann bei weiterlaufendem Terminal-I/O am Prompt ganz ausbleiben. Die Verdrahtung von (b) ist moderat (Event + ein optionaler Ctor-Parameter, Singleton→Singleton). |
| Ergänzender Auslöser am Ereignis-Endstatus | Ja — in `DispatchCoreAsync` wird `item` vor dem `try` deklariert und im `finally` bei `item?.Status is "Verarbeitet" or "Abgebrochen"` `startCheckTrigger?.RequestStartCheck()` angefordert. | Deckt den Randfall „Session bereits `Ready`, letztes offenes Ereignis erreicht Endstatus" deterministisch ab statt nur über den 60-s-Poll. Ein einziger Prüfpunkt hinter `try` erfasst alle `Verarbeitet`-Setzstellen (`ReconcileAttemptAsync`, `ReconcilePreparationAttemptAsync`, `DispatchPreparationAsync`), weil sie dieselbe getrackte Entität mutieren; doppelte Anforderungen (z. B. mit `CancelAsync`) koaleszieren im `ScmStartCheckService`. |
| Form des Adapter-Signals | `event Action<ScmPromptReadyStatus>? ReadyStatusChanged` — feuert bei jedem `_ready`-Wechsel mit dem neuen Wert; der Abonnent filtert auf `Ready`. | Konsistent mit den `Action`-Events im Umfeld (`CliProcessStatusChanged`, `RunningCountChanged`); kein neuer Args-Typ nötig. Alle Transitionen zu melden (statt nur `Ready`) ist genereller und erlaubt Tests, auch Rückfälle (`Ready` → `Busy`) zu beobachten. |
| Pending-Abfrage in `GetOccupancyAsync` | Eine gebündelte Query über `runningIds` (`Status != "Verarbeitet" && Status != "Abgebrochen"` → `HashSet<Guid>`), nur ausgeführt, wenn mindestens eine nicht pausierte Aufgabe das Ready-Kriterium (`IsAlive && ReadyStatus == Ready`) erfüllt. | Bestandsmuster der Pending-Prüfung (`ScmTaskCompletionMonitoringService` Z. 84–85, `ScmEventOrchestrator` Z. 182); vermeidet N+1 und — weil Sessions ohne `Ready` die Regel sind — in den meisten Aufrufen die komplette Zusatzabfrage. |

## Programmabläufe

### Belegungsauswertung mit Ready-/Pending-Freigabe

1. `ScmEventOrchestrator.DispatchCoreAsync` (oder Queue-/UI-Pfad) ruft
   `CheckAndReserveStartSlotAsync` bzw. `GetStartBlockReasonAsync` — unverändert; beide laufen
   weiter unter `ScmStartSlotReservationPool.AdmissionGate` über `EffectiveOccupancyAsync`.
2. `GetOccupancyAsync` liest `cli.GetRunningWorkItemIds()` und lädt die Aufgabenprojektion
   `{Id, AiPluginPrefix, PausedUntilUtc}` — unverändert.
3. Pausenfilter (`IsCurrentlyPaused`) bleibt erste Freigaberegel — unverändert.
4. Pro nicht-pausierter Aufgabe wird `cli.GetScmPromptSession(id)` gelesen (bereits heute für die
   Plugin-Attribution nötig). Ready-Kandidat: `session?.IsAlive == true && session.ReadyStatus == ScmPromptReadyStatus.Ready`.
5. Existiert mindestens ein Ready-Kandidat, wird die Pending-Menge gebündelt geladen:
   `db.ScmIncomingEvents` mit `runningIds.Contains(x.WorkItemId)` und `Status` ∉
   `{Verarbeitet, Abgebrochen}` → `HashSet<Guid>` der Aufgaben-IDs mit offenem Ereignis.
6. Ein Ready-Kandidat ohne Eintrag in der Pending-Menge entfällt — analog dem Pausenfilter **vor**
   globaler Zählung und Plugin-Attribution — aus beiden Zählern.
7. Alle übrigen aktiven Aufgaben zählen wie bisher: global je eine, Plugin-Attribution über
   `session.IsAlive && PluginPrefix` mit `AiPluginPrefix`-Fallback; Handles ohne Aufgabenzeile
   zählen weiterhin voll.
8. `GetStartBlockReason` (globales Limit vor Plugin-Limit) und die Reservierungssemantik bleiben
   unverändert — sie profitieren automatisch von der reduzierten Belegungszahl.

Beteiligte Klassen/Komponenten: `ScmEventExecutionLimitService`, `ICliExecutionLimitSource`/
`AiExecutionService` (`GetScmPromptSession`), `SoftwareschmiedeDbContext.ScmIncomingEvents`,
`IScmPromptSessionAdapter`, `ScmStartSlotReservationPool`.

### Ready-Auslöser der Startprüfung

1. `ScmPromptSessionAdapter` setzt seinen Bereitschaftsstand weiter ausschließlich über `SetReady`
   (`RefreshScreen` für Prompt-/Busy-/Trust-Auswertung, `ConfirmReady`, `MarkInputStarted` →
   `Busy`, `OnExited`/`OnFailed` → `Exited`).
2. `SetReady` feuert bei tatsächlichem Wertwechsel — nach dem `_changed`-TCS-Wechsel — das neue
   `ReadyStatusChanged`-Event mit dem neuen Status. Der Invoke liegt unter `_sync`; der Vertrag
   verlangt non-blocking Handler (`RequestStartCheck` ist ein non-blocking Semaphor-Release).
3. `AiExecutionService` abonniert das Event unmittelbar bei der Adapter-Anlage
   (`AiExecutionService.cs` ~Z. 377–379): bei `status == ScmPromptReadyStatus.Ready` →
   `startCheckTrigger?.RequestStartCheck()`. Direkt nach der Verdrahtung erfolgt eine einmalige
   Level-Prüfung `adapter.ReadyStatus == Ready` → gleiche Anforderung — sie deckt ab, dass
   `RefreshScreen()` bereits im Adapter-Konstruktor läuft und ein theoretisch schon sichtbares
   Prompt sonst ohne Übergang ungemeldet bliebe.
4. `ScmStartCheckService` koalesziert die Anforderung (250-ms-Sammelphase) und ruft
   `ScmEventOrchestrator.DispatchDueEventsAsync`; die Belegungsauswertung wendet die strengen
   Kriterien erneut an — ein `Ready` mit offenem Ereignis führt zu einem ergebnislosen, billigen
   Durchlauf.
5. Kehrt die Session zur Arbeit zurück (`MarkInputStarted` → `Busy`, `BufferChanged` → anderer
   Status), feuert `ReadyStatusChanged` ohne `Ready`-Filterung nicht erneut aus.

Beteiligte Klassen/Komponenten: `ScmPromptSessionAdapter`/`IScmPromptSessionAdapter`,
`AiExecutionService`, `ScmStartCheckTrigger`, `ScmStartCheckService`, `ScmEventOrchestrator`.

### Endstatus-Auslöser (Randfall „Session Ready + letztes Ereignis endet")

1. `DispatchCoreAsync` lädt das getrackte `item` und führt — je nach Pfad —
   `DispatchPreparationAsync`, `ReconcileAttemptAsync` oder die reguläre Zustellung aus; alle
   `Verarbeitet`-Setzstellen mutieren dieselbe getrackte Entität und persistieren innerhalb der
   Methode.
2. `item` wird vor dem `try` deklariert; im `finally` wird bei
   `item?.Status is "Verarbeitet" or "Abgebrochen"` `startCheckTrigger?.RequestStartCheck()`
   angefordert.
3. Damit feuert der Auslöser an genau einer Stelle für alle Endstatus-Wege des Dispatchs
   (generisch, Aufbereitung, Reconcile, `Abgebrochen`-Frühreturn). `CancelAsync` löst bereits heute
   aus — eine zusätzliche Anforderung über diesen Pfad ist harmlos und koalesziert.
4. Der Poll des `ScmEventMonitoringService` bleibt für alle übrigen Endstatus-Quellen außerhalb
   des Dispatchs (z. B. manuelle DB-Eingriffe) das Sicherheitsnetz.

Beteiligte Klassen/Komponenten: `ScmEventOrchestrator`, `ScmStartCheckTrigger`,
`ScmStartCheckService`.

### Rückkehr zur Arbeit (kein eigener Code — dokumentiertes Verhalten)

1. Trifft eine Eingabe/ein Folgeprompt ein, ruft `ScmCoordinatedTerminalSession`
   `InputStarted` → `MarkInputStarted` → `ReadyStatus = Busy`; ein neues offenes Ereignis trägt
   die Aufgabe in die Pending-Menge ein.
2. Bei der nächsten Belegungsauswertung zählt die Aufgabe wieder voll — ein inzwischen gestarteter
   Folgelauf kann das Limit zeitweise überschreiten; das ist toleriert (nur Starts werden
   begrenzt) und in der Doku festgehalten.

## Neue Klassen

Keine — das Feature erweitert ausschließlich bestehende Klassen und ein Interface um ein Event.

## Änderungen an bestehenden Klassen

### `ScmEventExecutionLimitService` (Service, `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs`)

- **Geänderte Methoden:** `GetOccupancyAsync` — zusätzlich zur Pausenfreigabe die
  Ready-/Pending-Freigabe: pro nicht-pausierter Aufgabe `cli.GetScmPromptSession(id)` auf
  `IsAlive && ReadyStatus == Ready` prüfen; bei mindestens einem Kandidaten die Pending-Menge
  gebündelt aus `db.ScmIncomingEvents` laden und Kandidaten ohne offenes Ereignis vor globaler
  Zählung und Plugin-Attribution ausfiltern.
- **Dokumentation:** XML-Kommentar von `CliExecutionOccupancy` (Z. 6–19) und `GetOccupancyAsync`
  (Z. 185–188) um die neue Freigabesemantik und die tolerierte Überschreitung bei Rückkehr zur
  Arbeit ergänzen.

### `IScmPromptSessionAdapter` (Interface, `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs`, Z. 24–55)

- **Neue Events:** `event Action<ScmPromptReadyStatus>? ReadyStatusChanged` — feuert bei jedem
  Wechsel des internen Bereitschaftsstands mit dem neuen Wert; Handler müssen non-blocking sein
  (Invoke unter dem internen `_sync`-Lock).

### `ScmPromptSessionAdapter` (Implementierung, gleiche Datei)

- **Geänderte Methoden:** `SetReady` — nach dem `_changed`-TCS-Wechsel zusätzlich
  `ReadyStatusChanged?.Invoke(ready)` auslösen (nur bei tatsächlichem Wertwechsel — die
  bestehende Frühreturn-Bedingung `_ready == ready` gilt weiter).

### `AiExecutionService` (Service, `src/Softwareschmiede/Application/Services/AiExecutionService.cs`)

- **Neue Konstruktorparameter:** `ScmStartCheckTrigger? startCheckTrigger = null` — optional,
  Muster wie bei den bestehenden Auslöser-Stellen; DI (Singleton, `App.xaml.cs` Z. 316/324 bzw.
  `ScmTestServices` Z. 40/51) füllt ihn automatisch.
- **Geänderte Methoden:** Adapter-Anlage (~Z. 377–379) — nach `new ScmPromptSessionAdapter(...)`
  `adapter.ReadyStatusChanged` abonnieren: bei `Ready` `startCheckTrigger?.RequestStartCheck()`;
  anschließend einmalige Level-Prüfung `adapter.ReadyStatus == Ready` (Konstruktor-`RefreshScreen`
  kann vor der Verdrahtung bereits `Ready` gesetzt haben).

### `ScmEventOrchestrator` (Service, `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`)

- **Geänderte Methoden:** `DispatchCoreAsync` — `item`-Deklaration vor das `try` heben; im
  `finally` bei `item?.Status is "Verarbeitet" or "Abgebrochen"`
  `startCheckTrigger?.RequestStartCheck()` anfordern. Der bereits vorhandene optionale
  Ctor-Parameter `ScmStartCheckTrigger?` wird wiederverwendet — keine Signaturänderung.

### `ScmStartCheckTrigger` / `ScmStartCheckService` (`src/Softwareschmiede/Application/Services/ScmStartCheckService.cs`)

- **Dokumentation:** XML-Kommentare (Z. 8–16, 26–35) um die neuen Auslöser ergänzen
  (`ReadyStatusChanged` → `Ready` der SCM-Promptsitzung; Endstatus-Übergang eines Ereignisses in
  `DispatchCoreAsync`).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — es werden keine neuen Eingaben verarbeitet; die Freigaberegel wertet ausschließlich
Bestandsdaten (`ReadyStatus`, `ScmIncomingEvent.Status`) aus.

## Konfigurationsänderungen

Keine — das Verhalten gilt implizit unter den bestehenden Limits
(`scm.execution.maxparallel`, `plugins.maxparallel.<prefix>`) und dem Laufzeit-Schalter
`GetScmMonitoringEnabledAsync`.

## Seiteneffekte und Risiken

- **Zusätzliche DB-Abfrage:** Die Pending-Query läuft je Belegungsauswertung nur, wenn mindestens
  eine aktive Aufgabe einen verifizierten `Ready`-Prompt zeigt — im Normalbetrieb (arbeitende oder
  CLI-lose Aufgaben) entsteht kein zusätzlicher DB-Zugriff.
- **Event-Invoke unter `_sync`:** `ReadyStatusChanged` wird im `SetReady`-Kontext unter dem
  Adapter-Lock gefeuert; Handler müssen non-blocking sein. `RequestStartCheck()` ist ein
  non-blocking Semaphor-Release und erfüllt den Vertrag; das ist im Interface-Kommentar
  festzuhalten.
- **Ergebnislose Zusatzscans:** `Ready` bei noch offenem Ereignis oder `Ready` ohne jede
  Belegungsrelevanz löst eine Startprüfung aus, die ergebnislos bleibt — koalesziert und billig,
  bewusst in Kauf genommen.
- **Tolerierte Limit-Überschreitung:** Eine freigegebene Aufgabe, die zur Arbeit zurückkehrt,
  zählt erst bei der nächsten Bewertung wieder; inzwischen gestartete Folgeläufe werden nicht
  gestoppt — dokumentiertes Verhalten, kein Eingriff in laufende Prozesse.
- **Interface-Erweiterung:** `IScmPromptSessionAdapter` erhält ein Member; die einzige
  Implementierung ist `ScmPromptSessionAdapter`, `Mock<IScmPromptSessionAdapter>` in den
  Unit-Tests ist quellkompatibel (Moq implementiert Events automatisch). Die ~13 direkten
  `new ScmPromptSessionAdapter(...)`-Stellen in Integrationstests bleiben unverändert
  kompilierbar (kein Ctor-Parameter hinzugefügt).
- **`AiExecutionService`-Ctor:** neuer optionaler Parameter — `TestAiExecutionServiceFactory`
  und alle manuellen Konstruktionen bleiben quellkompatibel; Tests, die den Trigger gezielt
  brauchen, nutzen den Fixture-Container (`AddScmTestServices` registriert beides).

## Umsetzungsreihenfolge

1. **`ReadyStatusChanged`-Signal am Adapter**
   - Voraussetzungen: Keine.
   - Beschreibung: Event auf `IScmPromptSessionAdapter` deklarieren; `SetReady` feuert es bei
     Wertwechsel; XML-Doku mit Non-blocking-Vertrag.

2. **Verdrahtung in `AiExecutionService`**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Optionaler `ScmStartCheckTrigger?`-Ctor-Parameter; Subscription bei der
     Adapter-Anlage mit `Ready`-Filter und einmaliger Level-Prüfung.

3. **`GetOccupancyAsync`-Erweiterung**
   - Voraussetzungen: Keine (fachlich unabhängig von 1–2; nutzt nur `IsAlive`/`ReadyStatus`).
   - Beschreibung: Ready-Kandidaten ermitteln, gebündelte Pending-Query, Freigabe vor beiden
     Zählern; XML-Doku von `CliExecutionOccupancy`/`GetOccupancyAsync` anpassen.

4. **Endstatus-Trigger in `DispatchCoreAsync`**
   - Voraussetzungen: Keine (`startCheckTrigger` ist bereits Ctor-Parameter).
   - Beschreibung: `item` vor `try` deklarieren; `finally`-Prüfung auf Endstatus →
     `RequestStartCheck()`; XML-Doku von `ScmStartCheckTrigger`/`ScmStartCheckService` ergänzen.

5. **Unit-Tests `ScmEventExecutionLimitServiceTests`**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: `LiveSession`-Hilfsmethode um `ReadyStatus` erweitern; neue Testmethoden für
     Freigabe, Pending-Guard, Status-Matrix, Rückkehr-Verhalten.

6. **Signal- und Trigger-Tests**
   - Voraussetzungen: Schritte 1, 2, 4.
   - Beschreibung: Adapter-Event-Test (`ReadyStatusChanged` bei `Ready`-Übergang); Endstatus-
     Trigger-Test über `fixture.Trigger.Requested`-Zählung.

7. **Integrationstests `ScmEventDispatchTests_*`**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: Platzfreigabe am `SCM_READY_V1`-Prompt mit Folgestart ohne Poll; Pending-Guard
     hält den Platz; `Ready`-Übergang löst `Trigger.Requested` aus.

8. **Dokumentation**
   - Voraussetzungen: Schritte 1–4 (beschriebene Semantik final).
   - Beschreibung: Alle verifizierten Doku-Stellen um die Ready-/Pending-Freigabe, den neuen
     Auslöser und die tolerierte Überschreitung ergänzen.

9. **Build, Format, Test-Lanes**
   - Voraussetzungen: Schritte 1–8.
   - Beschreibung: `dotnet build Softwareschmiede.slnx`, `dotnet format Softwareschmiede.slnx`,
     dann beide regulären Lanes (`Category!=OsInterface`) mit
     `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` synchron ausführen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `LiveSession(prefix, readyStatus, isAlive)` (Erweiterung der bestehenden Hilfsmethode) | `ScmEventExecutionLimitServiceTests` | Session-Mock mit steuerbarem `ReadyStatus` und `IsAlive`. |
| `GetStartBlockReasonAsync_ReadySessionNoPending_ReturnsGlobalSlotFree` | `ScmEventExecutionLimitServiceTests` | Ready+Alive ohne Ereigniszeilen → globaler Platz frei. |
| `GetStartBlockReasonAsync_ReadySessionNoPending_ReturnsPluginSlotFree` | `ScmEventExecutionLimitServiceTests` | Gleiche Situation → Plugin-Platz frei. |
| `GetStartBlockReasonAsync_ReadySessionWithPendingEvent_CountsAsOccupied` (Theory über offene Stati: `Aufgenommen`, `Wartend`, `Verzögert`, `Zugestellt`, `InBearbeitung`, `ZustellungUngewiss`, `VerarbeitungUngeklärt`) | `ScmEventExecutionLimitServiceTests` | Pending-Guard hält den Platz trotz `Ready` belegt. |
| `GetStartBlockReasonAsync_ReadySessionEventInEndStatus_ReturnsSlotFree` (Theory: `Verarbeitet`, `Abgebrochen`) | `ScmEventExecutionLimitServiceTests` | Endstatus-Ereignisse zählen nicht als Pending → frei. |
| `GetStartBlockReasonAsync_SessionNotReady_CountsAsOccupied` (Theory: `Unknown`, `Busy`, `TrustPrompt`, `Exited`) | `ScmEventExecutionLimitServiceTests` | Konservative Zählung ohne verifiziertes `Ready`. |
| `GetStartBlockReasonAsync_SessionDead_CountsAsOccupied` | `ScmEventExecutionLimitServiceTests` | `IsAlive == false` → Platz bleibt belegt. |
| `GetStartBlockReasonAsync_ReadySession_PendingOfOtherTask_Ignored` | `ScmEventExecutionLimitServiceTests` | Offenes Ereignis einer anderen Aufgabe blockiert die Freigabe nicht. |
| `GetStartBlockReasonAsync_MixedReadyFreeAndActive_CountsOnlyActive` | `ScmEventExecutionLimitServiceTests` | Gemischte Belegung: nur die arbeitende Aufgabe zählt. |
| `CheckAndReserveStartSlotAsync_ReadyRunAndOpenReservation_ReservationOccupiedSlot` | `ScmEventExecutionLimitServiceTests` | Reservierung bleibt stärker als die Ready-Freigabe (Analog zum Pausen-Test). |
| `SetReady_TransitionToReady_RaisesReadyStatusChanged` / `..._BusyTransition_RaisesWithBusy` | neue Klasse `ScmPromptSessionAdapterReadySignalTests` (Integrationstests, `Mock<ITerminalSession>` + `TerminalBuffer` nach Fixture-Muster) | Event feuert mit dem neuen Status bei `SetPrompt`-Wechsel. |
| `DispatchCoreAsync_EventReachesEndStatus_RequestsStartCheck` | `ScmEventDispatchTests_Startcheck` bzw. `ScmEventOrchestratorTests` | `fixture.Trigger.Requested`-Zählung steigt, wenn ein Dispatch-Lauf das Ereignis in `Verarbeitet` enden lässt (Exchange-Complete nach Fixture-Muster). |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `LiveSession`-Hilfsmethode in `ScmEventExecutionLimitServiceTests` | Signatur-Erweiterung um `ReadyStatus`/`IsAlive`; bisherige Aufrufer nutzen Standardwerte (`Ready`, `IsAlive = true` bleibt voreingestellt wie bisher — Achtung: heutige Aufrufe liefern `Unknown`, daher optionalen Parameter so wählen, dass bisherige Erwartungen erhalten bleiben). |

### E2E-Tests (primärer Funktionsnachweis)

Keine E2E-Tests (FlaUI/`OsInterface`) erforderlich: Die Anforderung berührt keinen über UI oder
Nutzeraktion erreichbaren Ablauf — weder Fenster, Dialog noch eine sichtbare Interaktion ändern
sich (Scope-Ausschluss 4). Der Funktionsnachweis liegt auf Systemebene und wird durch die
Integrationstests der `ScmDispatchFixture` erbracht: Sie durchlaufen den kompletten realen
Dienststapel (echter `ScmEventOrchestrator`, `ScmEventExecutionLimitService`,
`ScmPromptSessionAdapter`, `AiExecutionService`, `ScmStartCheckTrigger`/`ScmStartCheckService` auf
echtem SQLite) vom simulierten Prompt-Wechsel (`SetPrompt`) über den Trigger bis zum tatsächlichen
Folgestart — das ist der fachlich sichtbare Effekt des Features und deckt die Akzeptanzszenarien
vollständig ab.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| — | Keine | — | — | Kein Benutzerfluss betroffen; Funktionsnachweis über `ScmDispatchFixture`-Integrationstests (siehe Begründung oben). |

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Platzfreigabe am verifizierten Prompt | SCM-Ereignisse zweier Aufgaben; globales Limit 1 | Aufgabe A läuft, ihr auslösendes Ereignis ist `Verarbeitet`; Ereignis B wartet mit „globales Limit erreicht"; CLI von A beendet ihren Turn | Prompt von A erscheint → `RefreshScreen` → `SetReady(Ready)` → `ReadyStatusChanged` → `AiExecutionService` → `RequestStartCheck()` → `DispatchDueEventsAsync` → `GetOccupancyAsync` zählt A nicht mehr → B startet | B wird ohne Poll und ohne Nutzeraktion zugestellt (`Status = "Zugestellt"`); A läuft weiter | `Dispatch_TaskReadyAtPrompt_SlotFreed_WaitingEventStarts` (Integration) |
| 2 | Pending-Guard | Wie Szenario 1, aber A hat ein offenes Ereignis (`Zugestellt`/`InBearbeitung`) | A zeigt `Ready`, Ereignis nicht im Endstatus | `Ready` feuert → Startprüfung läuft → Pending-Abfrage findet A → B bleibt wartend | B bleibt `Wartend` mit Limit-Reason; kein Parallelstart | `Dispatch_TaskReadyWithPendingEvent_SlotStaysOccupied` + Unit-Theory |
| 3 | Endstatus-Randfall | Abgleich eines gelieferten Ereignisses | A steht bereits `Ready`; letztes offenes Ereignis von A wird im Reconcile `Verarbeitet` | `DispatchCoreAsync` beendet den Abgleich → `finally`-Prüfung → `RequestStartCheck()` → B startet ohne Poll | Folgeereignis B wird ereignisgesteuert gestartet | `DispatchCoreAsync_EventReachesEndStatus_RequestsStartCheck` |
| 4 | Rückkehr zur Arbeit | Eingabe/Folgeprompt an die freigegebene Session A | A war freigegeben, B wurde gestartet (Limit scheinbar überschritten) | `InputStarted` → `MarkInputStarted` → `Busy`; nächste Belegungsauswertung zählt A wieder voll | A und B laufen parallel weiter (kein Eingriff); neue Starts werden wieder begrenzt | Unit-Test `MixedReadyFreeAndActive`/Busy-Theory + Doku-Hinweis |
| 5 | Konservative Fälle | Zulassungsprüfung | Session fehlt (`GetScmPromptSession` → `null`), `ReadyStatus` `Unknown`/`TrustPrompt`/`Busy`/`Exited` oder Handle ohne Aufgabenzeile | `GetOccupancyAsync` wertet Kriterien | Platz bleibt voll belegt; kein Fehler | Unit-Theorien `SessionNotReady`/`SessionDead` |

## Offene Punkte

Keine.
