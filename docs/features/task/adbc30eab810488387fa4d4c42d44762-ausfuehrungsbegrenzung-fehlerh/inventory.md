# Bestandsaufnahme: Ausführungsbegrenzung muss pausierte Ausführungen freigeben (Fehlerkorrektur zu Issue #410)

Analysiert wurde der Bereich „Ausführungsbegrenzung paralleler CLI-Starts" (Zulassungskontrolle, Belegungszählung, Pausen-Setzpfade und Startprüfungs-Auslöser) bezogen auf die Anforderung, dass eine pausierte Ausführung (`Aufgabe.PausiertBisUtc` in der Zukunft) keinen Platz im globalen und im Plugin-Parallel-Limit mehr belegen soll.

## Zusammenfassung

- **`ScmEventExecutionLimitService` existiert vollständig** mit atomarer Platzreservierung (`PruefeUndReserviereStartPlatzAsync`, `ZulassungsGate`, `StartPlatzReservierung`, `ReservierungsLebensdauer`), globalem Limit (`scm.execution.maxparallel`) und Plugin-Limits (`plugins.maxparallel.<prefix>`).
- **Kernbefund:** `GetBelegungAsync` (Z. 204–225) lädt eine `Aufgaben`-Projektion mit `Id` und `KiPluginPrefix` — **`PausiertBisUtc` wird nicht geladen und nicht ausgewertet**. Der globale Zähler entspricht `laufendeIds.Count` (alle Handles mit `!Process.HasExited`), der Plugin-Zähler attribuiert über lebende SCM-Sitzung bzw. `Aufgabe.KiPluginPrefix`. Pausierte, aber laufende Aufgaben zählen aktuell in beide Zähler voll mit.
- **`TimeProvider` ist bereits injiziert** (Konstruktorparameter `time`, Z. 47) — die Pausenbewertung `PausiertBisUtc > UtcNow` kann ohne neue Abhängigkeit erfolgen.
- **Das Pausenkriterium `PausiertBisUtc > UtcNow` ist im Bestand einheitlich etabliert**: `KiAusfuehrungsStatusKlassifikation` (Z. 25), `ScmEventOrchestrator.BlockReason` (Z. 993), `AufgabeRecoveryService` (Z. 51, 86), `EntwicklungsprozessService.WirfWennPausiert` (Z. 989), `PromptZeitVersandService` (Z. 203, 344). Nur `ScmDispatchPrioritaet.PrioritaetsKlasse` wertet bewusst „gesetzt (auch abgelaufen)" als Fortsetzungsmerkmal.
- **Alle produktiven Pausen-Setzpfade melden bereits an `ScmStartPruefungsAusloeser.StartpruefungAnfordern()`** — und zwar jeweils nach dem Commit: `AufgabeService.SetPauseAsync` (Z. 918), `ScmEventStore.AcceptBatchAsync`/`PauseForDelayAsync` für die `#verzögert`-Direktive (Z. 106/128), `KiPluginLimitService.VerarbeiteRateLimitAsync` (Z. 143). Einzige Schreibstelle ohne Auslöser: `ScmEventOrchestrator.CancelAsync` (Z. 442–443), die beim Abbruch eines verzögerten Ereignisses die Direktiven-Pause auf das nächste verbleibende Ziel zurückstuft bzw. aufhebt.
- **Freigabe-Ereignisse sind vorhanden**: `RunningCountChanged` bei Prozessende (sinkende Zählung löst aus), `ScmStartPruefungsService` koalesziert Auslöser (250 ms Sammelzeit) und ruft `ScmEventOrchestrator.DispatchDueEventsAsync` — denselben priorisierten Batch wie der 60-s-Poll des `ScmEventMonitoringService`. Die ausgelöste Prüfung sieht den frei werdenden Platz aber erst, wenn `GetBelegungAsync` die Pause berücksichtigt.
- **`ICliExecutionLimitSource`/`KiAusfuehrungsService.GetLaufendeAufgabenIds` ist ein geteilter Vertrag** („laufender Prozess"), der auch von `CliProcessManager.AbgleichAsync` (Laufdaten-Sicherstellung), `AufgabeRecoveryService` (`IsRunning`), der Statusanzeige (`GetLiveLaufStatus`) und indirekt `AutoShutdownOrchestrator`/`ScmStartPruefungsService` (`RunningCountChanged`) genutzt wird — pausbewusste Filterung ist dort aktuell nicht vorgesehen.
- **Fortsetzungspfad ohne Zulassungsprüfung bestätigt**: In `ScmEventOrchestrator.DispatchCoreAsync` (Z. 217) wird `PruefeUndReserviereStartPlatzAsync` nur aufgerufen, wenn keine lebende SCM-Sitzung existiert (`adapter is null || !adapter.IsAlive`); die Fortsetzung über `adapter.IsAlive`/`WritePromptAsync` durchläuft keine Limit-Prüfung.
- **Fehlend:** Jede Pausen-Berücksichtigung in `GetBelegungAsync`/`CliAusfuehrungsBelegung` sowie Tests, die Pausenfälle der Belegungszählung abdecken (pausiert → Platz frei global/pro Plugin; abgelaufene Pause → belegt; Pause während offener Reservierung; Trigger „Pause setzen → Folgestart").

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
