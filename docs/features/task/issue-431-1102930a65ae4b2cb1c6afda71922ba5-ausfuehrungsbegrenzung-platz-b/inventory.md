# Bestandsaufnahme: Ausführungsbegrenzung — Platz bei wartender CLI am Eingabeprompt freigeben

Analysiert wurde die SCM-Ausführungsbegrenzung (`ScmEventExecutionLimitService`) und ihr Umfeld
(SCM-Promptsitzung, Startprüfungs-Auslöser, Ereignis-Pending, Dispatch) bezogen auf die Anforderung
in `requirement.md` (GitHub-Issue #431): Eine Aufgabe soll ihren Belegungsplatz freigeben, wenn ihre
SCM-Promptsitzung einen verifizierten Eingabeprompt zeigt und kein unverarbeitetes
`ScmIncomingEvent` der Aufgabe existiert.

## Zusammenfassung

- **Kernstelle bestätigt:** `ScmEventExecutionLimitService.GetOccupancyAsync`
  (`src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs`, Z. 189–214) zählt
  heute laufende CLI-Handles global und je Plugin. Einzige Freigaberegel ist der Pausenfilter
  (`PausedUntilUtc.IsCurrentlyPaused(now)`); die SCM-Session wird aktuell nur für die
  Plugin-Attribution (`session?.IsAlive == true` → `session.PluginPrefix`) gelesen,
  `ReadyStatus` wird dort nicht ausgewertet.
- **Freigabesignal vorhanden:** `IScmPromptSessionAdapter.IsAlive` und
  `ReadyStatus == ScmPromptReadyStatus.Ready` existieren bereits (`ScmPromptSessionAdapter.cs`
  Z. 129–140); `Ready` wird nur bei erkanntem, unterstütztem Promptformat und sichtbarer
  Eingabezeile gesetzt (`RefreshScreen`, Z. 334–372). Es gibt **kein öffentliches
  Übergangsereignis** — Statuswechsel werden intern über einen `_changed`-TCS signalisiert
  (`SetReady`, Z. 375–382), das `WaitForInputReadyAsync` bedient.
- **Pending-Guard-Muster vorhanden:** `x.WorkItemId == id && x.Status != "Verarbeitet" && x.Status != "Abgebrochen"`
  auf `db.ScmIncomingEvents` ist das etablierte Muster (`ScmTaskCompletionMonitoringService`
  Z. 84–85/133–135, `ScmEventOrchestrator` Z. 182, `ScmEventExchangeService` Z. 27). `ScmIncomingEvent.Status`
  ist ein freier `string` (Default `"Aufgenommen"`); Endstatus sind `"Verarbeitet"` und `"Abgebrochen"`.
- **Auslöser-Infrastruktur vorhanden:** `ScmStartCheckTrigger.RequestStartCheck()` wird bereits aus
  `WorkItemService.SetPauseAsync` (Z. 918), `ScmEventStore` (Z. 128), `AiPluginLimitService` (Z. 143)
  und `ScmEventOrchestrator.CancelAsync` (Z. 451) aufgerufen — stets als optionaler
  Konstruktorparameter (`ScmStartCheckTrigger? = null`). `ScmStartCheckService` koalesziert
  Auslöser und ruft `ScmEventOrchestrator.DispatchDueEventsAsync`; zusätzlich feuert
  `IRunningAutomationStatusSource.RunningCountChanged` bei sinkender Laufzahl.
  `CliProcessManager.OnRuntimeStatusChanged` (Z. 318–324) ist der bestätigte Ankoppelpunkt für
  `CliRuntimeStatus.WaitingForInput`; `CliProcessManager` ist als Singleton registriert und erhält
  `ScmStartCheckTrigger` problemlos als weiteren optionalen Ctor-Parameter.
- **Nicht als Zählkriterium geeignet (Issue-Vorgabe, im Code bestätigt):**
  `CliRuntimeStatus.WaitingForInput` ist eine reine I/O-Ruhe-Heuristik (≥ 4 s ohne Ein-/Ausgabe,
  `CliRuntimeStatusEvaluator.Determine`, `PseudoConsoleSession` Z. 125), `WorkItemRunStatus` wird
  daraus nur für die Anzeige persistiert. Beide kommen nur als Auslösesignal in Betracht.
- **Reservierungsfenster:** `ScmStartSlotReservationPool` + atomare
  `CheckAndReserveStartSlotAsync` sind vorhanden und separat dokumentiert; das Startfenster ist
  heute schon gegen Doppelbelegung gesichert.
- **Randfall „Session Ready + letztes Ereignis wird `Verarbeitet`":** Der Übergang in den
  Endstatus erfolgt innerhalb `DispatchCoreAsync`/`ReconcileAttemptAsync` unter Aufgabenlease;
  dort wird aktuell kein `RequestStartCheck()` ausgelöst — der Poll (`ScmEventMonitoringService`,
  Intervall 60 s) deckt den Fall als Sicherheitsnetz ab.
- **Dokumentation:** Alle in der Anforderung genannten Stellen existieren und beschreiben aktuell
  ausschließlich die Pausen-Freigabe (verifiziert: `README.md` Z. 30/187,
  `docs/help/einstellungen/beschreibung.md` Z. 75, `docs/help/plugins/github-ereignisse/beschreibung.md`
  Z. 11, `ablauf-technisch.md` Z. 33–34, `docs/help/aufgaben/ablauf-technisch.md` Z. 521,
  `docs/help/plugins/github-ereignisse/business-rules.md` Z. 99–105/137). Zusätzlich betroffen:
  `docs/help/aufgaben/business-rules.md` Z. 254–262 und `docs/help/aufgaben/datenmodell.md` Z. 36.
- **Tests:** Es existieren umfangreiche Unit-Tests (`ScmEventExecutionLimitServiceTests`, 20
  Methoden inkl. Pausen-Freigabe) und Integrations-Tests (`ScmEventDispatchTests_Limit`, 13
  Methoden; `ScmEventDispatchTests_Startcheck`, 8 Methoden) über die `ScmDispatchFixture`
  (echte Services, SQLite, `Mock<ITerminalSession>` mit `SCM_READY_V1`-Prompt und
  `SetPrompt`-Umschaltung). Kein Test deckt heute eine Ready-basierte Platzfreigabe ab.

**Test-Ausgangszustand:** Beide regulären Lanes sind fehlerfrei — `Softwareschmiede.Tests`
(`Category!=OsInterface`): 2747 Tests, 2744 bestanden, 3 übersprungen (plattformspezifisch,
Windows-unabhängig vom Feature), 0 fehlgeschlagen. `Softwareschmiede.IntegrationTests`
(`Category!=OsInterface`): 360 Tests, alle bestanden. Die `Category=OsInterface`-Lanes
(E2E/ConPTY/Clipboard, umgebungssensitiv) wurden nicht ausgeführt — Details und Nachweise in
[inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `WorkItem`, `ScmIncomingEvent` (relevante Eigenschaften)
- [Logik](inventory/logic.md) — `ScmEventExecutionLimitService`, `ScmEventOrchestrator`, `CliProcessManager`,
  `ScmStartCheckTrigger`/`ScmStartCheckService`, `AiExecutionService`, Pausen-Setzpfade, Pending-Guard-Stellen
- [Enums und Statuswerte](inventory/enums.md) — `ScmPromptReadyStatus`, `CliRuntimeStatus`,
  `WorkItemRunStatus`, `ScmIncomingEvent.Status`-Literale
- [Interfaces](inventory/interfaces.md) — `IScmPromptSessionAdapter`, `ICliExecutionLimitSource`,
  `IRunningAutomationStatusSource`, `ITerminalSession` (relevante Member)
- [Tests](inventory/tests.md) — Test-Ausgangszustand, Testklassen und Hilfsmethoden
- [Dokumentationsstellen](inventory/docs.md) — verifizierte Doku-Fundstellen zur Ausführungsbegrenzung
