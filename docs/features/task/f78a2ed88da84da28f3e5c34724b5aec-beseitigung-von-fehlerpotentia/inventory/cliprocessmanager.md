# CliProcessManager

Datei: `src/Softwareschmiede/Application/Services/CliProcessManager.cs`

Singleton-Service, verwaltet Heartbeat-Timer pro Aufgabe und abonniert `KiAusfuehrungsService.CliProcessStatusChanged`.

## Felder

| Feld | Typ | Zweck |
|------|-----|-------|
| `_heartbeatTimers` | `ConcurrentDictionary<Guid, Timer>` | Ein Timer pro Aufgabe |
| `HeartbeatInterval` | `static readonly TimeSpan` (30s) | Intervall des Heartbeat-Timers |
| **`_updateSemaphore`** | — | **Nicht vorhanden** (F7: kein Concurrency-Schutz für `AktualisierungAsync`) |

## Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartHeartbeat(Guid aufgabeId)` | public | Erstellt `Timer` mit Callback `_ = AktualisierungAsync(aufgabeId)` (Zeile 38–42) — **Fire-and-Forget ungeschützt (F6)** |
| `StopHeartbeat(Guid aufgabeId)` | public | Entfernt und disposed Timer |
| `AktualisierungAsync(Guid aufgabeId)` | private async | Heartbeat-Update; hat eigenes try-catch (`LogWarning`), aber **kein Semaphore/Concurrency-Schutz (F7)** — parallele Timer-Ticks können überlappend laufen |
| `OnCliProcessStatusChanged(Guid, CliProcessStatus)` | private | Reagiert auf Status-Events: startet/stoppt Heartbeat |
| `Dispose()` | public | Deabonniert Event, disposed alle Timer |

Der Timer-Callback (Zeile 38–42) ruft `AktualisierungAsync` per `_ = ...` auf, ohne Exception-Handling auf Aufrufer-Seite — die Methode selbst fängt Exceptions per try-catch ab, aber es gibt keine zentrale `SafeFireAndForget`-Absicherung.

Abonnierte Events: `_kiService.CliProcessStatusChanged` (von `KiAusfuehrungsService`)
Publizierte Events: keine
