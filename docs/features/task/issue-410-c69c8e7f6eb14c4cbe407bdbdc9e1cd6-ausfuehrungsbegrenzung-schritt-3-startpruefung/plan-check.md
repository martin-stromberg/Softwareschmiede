# Plan-Prüfung: Ereignisgesteuerte Startprüfung bei Beendet/Pausiert (Schritt 3, #410 Teil 2)

**Status: Plan vollständig**

Geprüft: `plan.md` gegen `requirement.md` und `inventory.md` (+ Detaildokumente), Ist-Stand
des Codes auf dem Arbeitsbranch (Basis: gemergte Schritte 1+2).

## Abdeckung der Anforderung

| Anforderung | Plan-Abdeckung |
|-------------|----------------|
| Auslöser „beendet" an zentralem Signal | `IRunningAutomationStatusSource.RunningCountChanged` (nur bei sinkender Zählung) — deckt manuelle Beendigung, reguläres Prozessende und `ScmTaskCompletionMonitoringService`-Abschluss ab (letzterer beendet die CLI → dasselbe Signal) |
| Auslöser „pausiert" an Pausen-Setzpfaden | `ScmStartPruefungsAusloeser` + optionale Injection in `AufgabeService.SetPauseAsync`, `ScmEventStore.PauseForDelayAsync`, `KiPluginLimitService.VerarbeiteRateLimitAsync` — alle drei im Issue genannten Pfade |
| Kein zusätzlicher Polling-Mechanismus | `BackgroundService` mit Semaphore-Signal, rein ereignisgesteuert; `Task.Delay` nur als 250-ms-Sammelphase, keine Schleifen-Abfrage |
| Dieselbe Startprüfung wie Schritt 2 | Extraktion der Poll-Batch-Schleife in `ScmEventOrchestrator.DispatchDueEventsAsync` — Poll und Trigger teilen denselben Code |
| Serialisiert/idempotent, Sperren bleiben | Single-Consumer + `ScmTaskCoordinator`-Gates + `ScmTaskLeases` + atomare `PruefeUndReserviereStartPlatzAsync`; keine Sperre umgangen |
| Poll bleibt Sicherheitsnetz | `ScmEventMonitoringService` unverändert periodisch; ruft dieselbe Methode |
| Unmittelbar (nicht erst Poll) | Signal → ≤ ~250 ms Sammelphase → Durchlauf; begründet als A2 |
| Nicht-SCM-Ausführungen | lösen aus, Zählung unverändert (Belegungslogik Schritt 2 unberührt) — Test 6 |
| Tests mit ScmDispatchFixture/Fake-TimeProvider | 7 Integrationsfälle (Fixture) + Unit-Signaltests; Fake-Zeit für `NextAttemptUtc`-Fälligkeit; `Sammelzeit` intern setzbar |

## Verifizierte technische Punkte

- `RunningCountChanged` feuert vor `PersistAusfuehrungBeendetAsync` → Sammelphase A2 abgefedert.
- Abonnenten müssen non-blocking sein → Handler nur `SemaphoreSlim.Release()`.
- ~44 manuelle `new`-Konstruktionen der drei Pausenpfad-Klassen → optionale Parameter (A6).
- `scm.monitoring.enabled`-Schalter im Trigger respektiert (Test 7).
- `AcceptBatchAsync`: Trigger erst nach `CommitAsync` → Trigger sieht persistierten Zustand.
- `ScmDispatchFixture`: fehlende Registrierungen (`IRunningAutomationStatusSource`, Auslöser,
  Dienst, `KiPluginLimitService`, `AufgabeLaufdatenChangedNotifier`) benannt.

## Lücken/Risiken

- Keine fachlichen Lücken gefunden. Restrisiko: asynchrone Timing-Pfade in den
  Trigger-Integrationstests → mit großzügigen Warte-Schleifen (30 s) abgesichert;
  deterministische Anteile (Signalzählung, Priorisierung) über direkte Assertions.
- UI: keine Änderung → Usability-Review wird als nicht anwendbar dokumentiert.

## Ergebnis

Der Plan deckt die Anforderung einschließlich aller Akzeptanzkriterien vollständig ab;
Akzeptanzszenarien und Testbedarf sind benannt. **Plan vollständig.**
