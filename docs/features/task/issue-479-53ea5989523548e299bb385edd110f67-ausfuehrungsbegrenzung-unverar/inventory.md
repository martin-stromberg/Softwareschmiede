# Bestandsaufnahme: Ausführungsbegrenzung — hängende Ereignisse halten Startplatz belegt

Analyse des SCM-Ereignis-Dispatch und der Ausführungsbegrenzung bezogen auf Issue #479: ein offenes `ScmIncomingEvent` einer am verifizierten Eingabeprompt wartenden Aufgabe hält deren Startplatz aktuell für jeden nicht-finalen Status belegt.

## Zusammenfassung

- `ScmEventExecutionLimitService.GetOccupancyAsync` enthält die Pending-Abfrage (Zeile 218–223): jede `ScmIncomingEvent`-Zeile einer Ready-Aufgabe mit Status ≠ `"Verarbeitet"`/`"Abgebrochen"` (String-Literale, nicht die Konstanten) hält den Platz belegt — inklusive `VerarbeitungUngeklärt`, `ZustellungUngewiss`, `Wartend`, `Verzögert`, `Aufgenommen`.
- `ScmIncomingEventStatus.NonBlocking` enthält bereits die vier freigebenden Stati (`Verarbeitet`, `Abgebrochen`, `VerarbeitungUngeklärt`, `ZustellungUngewiss`); sie wird von Abschlussprüfung (`ScmTaskCompletionMonitoringService`) und Vorgänger-Serialisierung (`ScmEventOrchestrator` Z. 194) genutzt — nicht von der Platzlogik.
- `CheckAndReserveStartSlotAsync` wird ausschließlich unter der Wache `adapter is null || !adapter.IsAlive` aufgerufen (Orchestrator Z. 233/261 generisch, Z. 725/750 Aufbereitung) — die Zustellung an eine lebende Sitzung benötigt nie einen Startplatz.
- Strukturell unzustellbare Ereignisse enden aktuell in `Wartend` ohne `NextAttemptUtc`-Grenze: Aufgabe entfernt (kein FK auf `ScmIncomingEvent.WorkItemId` — Waisen möglich), beendet/archiviert (`BlockReason` → „Aufgabe beendet/archiviert; Start gesperrt."), fehlende `GitRepositoryId` (`!.Value` → `InvalidOperationException` → generischer Catch → `NextAttemptUtc = long.MaxValue` + `Wartend`). Alle werden bei jedem Poll erneut ausgewählt und verlassen die Queue nie.
- `Wartend` für eine pausierte Aufgabe („Aufgabe pausiert; Start gesperrt.") ist transient und muss `Wartend` bleiben — die Pause kann enden.
- Tests: `ScmEventExecutionLimitServiceTests` (Unit, InMemory-SQLite via `TestDbContextFactory`), `ScmEventDispatchTests_Limit`/`_Ordering` (Integration, `ScmDispatchFixture` mit echtem SQLite + gemocktem Terminal/SCM), `ScmIncomingEventStatusTests` (Statusmengen). `ScmDispatchFixture.SetEventStatusAsync` setzt beliebige Stati direkt.

Test-Ausgangszustand: Beide stabilen Lanes fehlerfrei — `Softwareschmiede.Tests` 2825 erfolgreich / 0 fehlgeschlagen / 3 übersprungen; `Softwareschmiede.IntegrationTests` 430 erfolgreich / 0 fehlgeschlagen / 0 übersprungen (Nachweis: [tests.md](inventory/tests.md)).

## Details

- [Logik](inventory/logic.md)
- [Datenmodell](inventory/models.md)
- [Stati (Enum-Ersatz)](inventory/enums.md)
- [Tests](inventory/tests.md)
