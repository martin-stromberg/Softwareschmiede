# Übersetzte Anforderung: Ereignisgesteuerte Startprüfung bei Beendigung oder Pausierung (Entwicklungsschritt 3, Issue #410 Teil 2)

**Quelle:** Projektplan `docs/projects/task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung/project-plan.md`, „Schritt 3" + Vorgehensentscheidung 2 (Auslöser der Startprüfung); GitHub-Issue #410.
**Basisbranch (Diff-Basis):** `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung`
**Vorarbeiten (bereits gemergt):** Schritt 1 (`ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync`, Plugin-Fallback), Schritt 2 (`ScmEventExecutionLimitService` mit atomarer `PruefeUndReserviereStartPlatzAsync`-Slot-Reservierung, `ScmDispatchPrioritaet`, priorisierte Batch-Auswahl im `ScmEventMonitoringService`, Limit-Prüfung in `ScmEventOrchestrator.DispatchCoreAsync`, Settings-UI).

## Fachliche Zusammenfassung

Zweiter, im Issue explizit belegter Auslöser der Ausführungsstartprüfung aus Schritt 2: Sobald eine über ein SCM-Ereignis ausgeführte Aufgabe als **beendet** oder **pausiert** erkannt wird, läuft dieselbe Startprüfung sofort an — ein frei werdender Slot wird ohne Wartezeit auf den nächsten 60-Sekunden-Poll mit dem priorisiert nächsten Ereignis gefüllt. Die Erkennung dockt an zentralen, bereits vorhandenen Signalen an; es wird kein zusätzlicher Polling-Mechanismus eingeführt. Die Prüfung ist mehrfachauslösbar und bleibt serialisiert/idempotent (bestehende Aufgabensperren/Leases und die atomare Slot-Reservierung werden nicht umgangen); der 60-s-Poll bleibt als langsamere Sicherheitsnetz-Wiederholung bestehen.

## Fachlicher Scope

- **Verarbeitungseinheit:** Eine Ausführung der Startprüfung = dieselbe priorisierte Batch-Auswahl fälliger Ereignisse, die der Poll-Zyklus des `ScmEventMonitoringService` heute am Ende von `RunOnceAsync` ausführt (`ScmDispatchPrioritaet.OrderForDispatch` → `ScmEventOrchestrator.DispatchAsync` je Ereignis).
- **Auslösende Ereignisse:**
  - **Beendet-Erkennung:** Sinkende Laufzählung über `IRunningAutomationStatusSource.RunningCountChanged` (`KiAusfuehrungsService`) — deckt reguläres Prozessende, manuelles Stoppen und fehlerhaftes Ende ab, unabhängig davon, ob die Beendigung durch `ScmTaskCompletionMonitoringService`, den Anwender oder die CLI selbst ausgelöst wurde.
  - **Pausiert-Erkennung:** die Pausen-Setzpfade `AufgabeService.SetPauseAsync`, `ScmEventStore.PauseForDelayAsync` (Verzögerungsdirektive `#verzögert`) und `KiPluginLimitService.VerarbeiteRateLimitAsync` (Session-Limit-Pausen).
- **Erwartete Wirkung:**
  - Nach Beendigung einer SCM-ausgeführten Aufgabe (manuell oder über `ScmTaskCompletionMonitoringService`) wird ein wartendes, startbares Ereignis unmittelbar geprüft und bei freiem Slot gestartet — nicht erst beim nächsten Poll.
  - Nach Pausierung einer solchen Aufgabe findet die Startprüfung ebenfalls statt (Priorisierung/Freigabe wird neu bewertet).
  - Mehrfache/überlappende Auslöser führen zu keinem Doppelstart und nie zu einem Start über das Limit hinaus (atomare Slot-Reservierung aus Schritt 2); Nicht-SCM-Ausführungen lösen die Prüfung zwar aus, verfälschen die Zählung aber nicht.
  - Bei deaktivierter SCM-Überwachung (`GetScmMonitoringEnabledAsync`) findet — wie im Poll-Zyklus — keine automatische Zustellung statt.
  - Der Poll bleibt als Sicherheitsnetz-Wiederholung bestehen.
- **Umgang mit fehlenden Objekten:**
  - Keine wartenden/fälligen Ereignisse → Prüfung ist ein leerer, harmloser Durchlauf.
  - Auslöser ohne auslösenden Abonnenten (Dienst nicht gestartet, z. B. Tests) → das Signal verfällt wirkungslos; der Poll bleibt Fallback.
  - Trigger ohne registrierte Auslöser-Instanz in manuell aufgebauten DI-Containern → optionale Abhängigkeit, kein Fehler.
- **Scope-Ausschlüsse:**
  - Kein neuer Polling-Mechanismus; kein Wecken des Poll-Intervalls. *(Status: bestätigt — verbindliche Projektplan-Vorgabe.)*
  - Keine Unterbrechung laufender Ausführungen, kein Umgehen bestehender Sperren (`BlockReason`, `NextAttemptUtc`, ältere offene Ereignisse derselben Aufgabe, `ScmTaskCoordinator`/Leases). *(Status: bestätigt — Projektplan.)*
  - Keine UI-Änderung; die Ereignisübersicht zeigt Ergebnisse über den bestehenden `ScmEventChangeNotifier`. *(Status: bestätigt — Schritt 3 betrifft nur Backend-Signale.)*
  - Keine Behandlung fachlicher Reaktivierung beendeter/archivierter Aufgaben — `BlockReason` bleibt unverändert. *(Status: bestätigt — Projektplan Schritt 2.)*

## Betroffene Klassen und Komponenten

- `KiAusfuehrungsService` (`src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`): `RunningCountChanged` (Z. 85, Raise in Z. 246/416/710, Logik Z. 774-786) — feuert bei Prozessstart und in `HandleSessionEndedAsync` vor `PersistAusfuehrungBeendetAsync` (Z. 727); implementiert `IRunningAutomationStatusSource`.
- `ScmEventMonitoringService` (`src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs` Z. 229-258): die zu teilende Startprüfungs-Schleife (Batch-Auswahl + priorisierte Reihenfolge + Dispatch je Ereignis).
- `ScmEventOrchestrator` (`src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`): `DispatchAsync`/`DispatchCoreAsync` mit Lease + atomarer Zulassung (Z. 190-208); natürliche Heimat der extrahierten Batch-Startprüfung.
- `AufgabeService.SetPauseAsync` (`AufgabeService.cs` Z. 853-892): setzt/hebt `PausiertBisUtc`.
- `ScmEventStore.PauseForDelayAsync` (`ScmEventStore.cs` Z. 139-154, Aufruf Z. 99 innerhalb `AcceptBatchAsync`-Transaktion).
- `KiPluginLimitService.VerarbeiteRateLimitAsync` (`KiPluginLimitService.cs` Z. 50-131): pausiert alle aktiv laufenden Aufgaben eines Plugin-Prefix.
- `ScmTaskCompletionMonitoringService` (`ScmTaskCompletionMonitoringService.cs`): beendet Aufgaben fachlich; die zugehörige CLI-Beendigung läuft über denselben `RunningCountChanged`-Pfad.
- DI-Verdrahtung `App.xaml.cs` (SCM-Block Z. 222-238, `IRunningAutomationStatusSource` Z. 314-315); `PeriodicBackgroundService`, `ScmEventStartupGate`, `ScmEventChangeNotifier` als Muster.
- Tests: `src/Softwareschmiede.IntegrationTests` (`ScmDispatchFixture`-Muster aus `ScmEventDispatchTests_Limits.cs`), ggf. `src/Softwareschmiede.Tests` für Signal-/Service-Unit-Tests.

## Implementierungsansatz

- Neuer Singleton-`BackgroundService` (z. B. `ScmStartPruefungsService`): wartet `ScmEventStartupGate` ab, abonniert `RunningCountChanged` (Auslösung bei sinkender Zählung) und ein neues Auslöser-Signal für die Pausenpfade (Singleton-Notifier im `ScmEventChangeNotifier`-Muster); führt getriggert die gemeinsame Batch-Startprüfung serialisiert aus (Single-Consumer-Schleife mit Signal-Semaphore, zusammengefasste Burst-Auslöser).
- Die Pausenpfade melden die Zustandsänderung an den Auslöser — optional injiziert (Standard `null`), damit die zahlreichen manuellen `new`-Konstruktionen in Tests unverändert bleiben.
- Die Batch-Startprüfung (bisheriges Schleifenende von `ScmEventMonitoringService.RunOnceAsync`) wird in eine wiederverwendbare Methode extrahiert (naheliegend `ScmEventOrchestrator`); Poll und Trigger teilen sich exakt denselben Code.
- Serialisierung/Idempotenz kommt aus dem Bestand: `ScmTaskCoordinator.Shared`-Gates, `ScmTaskLeases`-Claim und atomare `PruefeUndReserviereStartPlatzAsync` — der Trigger fügt keine Parallelität hinzu, die diese Sperren umgeht.

## Konfiguration

Keine neue Konfiguration; wirkt unter dem bestehenden Laufzeit-Schalter „Aufgaben ausführen" (`GetScmMonitoringEnabledAsync`) und den Schritt-2-Limits.

## Offene Fragen

Keine — alle Entscheidungen sind durch den verbindlichen Projektplan abgedeckt; Detailentscheidungen (Signalform, Platz der extrahierten Methode, kurze Sammelphase zum Bündeln von Burst-Auslösern) werden in `plan.md` als dokumentierte Annahmen festgelegt.
