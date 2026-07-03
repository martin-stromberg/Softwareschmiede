# KiAusfuehrungsService

Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

Singleton-Service, verwaltet laufende CLI-Prozesse (klassisch und via ConPTY/Pseudo Console).

## Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsRunning(Guid)` | public | Prüft `Process.HasExited`, fängt Exceptions ab |
| `GetRunningProcess(Guid)` | public | Gibt laufenden Prozess zurück oder null |
| `GetRunningCount()` | public | Zählt laufende Prozesse |
| `StartCliAsync(...)` | public async | Startet klassischen CLI-Prozess. `process.Exited`-Handler (Zeile 115–143) **ohne try-catch (F9)** — ruft `TryGetExitCode`, `_handles.TryRemove`, `RaiseRunningCountChanged()`, `CliProcessStatusChanged?.Invoke(...)`, `_ = PersistFehlgeschlagenAsync(...)` (Fire-and-Forget, Zeile 135) |
| `StartWithPseudoConsoleAsync(...)` | public async | Startet CLI-Prozess über ConPTY. `process.Exited`-Handler (Zeile 272–305) **ohne try-catch (F10)**; ruft zusätzlich `removedHandle.PseudoConsoleSession?.Dispose()` (Zeile 279, kann `ObjectDisposedException` werfen bei parallelem Dispose). FileStream-Erstellung (Zeile 254–266) **ohne try-finally (F11)** — bei Exception zwischen Stream-Erzeugung und Session-Zuweisung keine garantierte Freigabe. Fire-and-Forget: `_ = SendCommandDelayedAsync(...)` (Zeile 327, F8) |
| `GetPseudoConsoleSession(Guid)` | public | Gibt aktive Session zurück oder null |
| `StopCliAsync(Guid, CancellationToken)` | public async | Stoppt Prozess (CloseMainWindow → 5s Wartezeit → Kill), hat eigenes try-catch |
| `GetLastExitCode(Guid)` | public | Letzter Exit-Code |
| `UpdateHeartbeat(Guid)` | public | Aktualisiert `LastHeartbeat` am Handle |
| `Dispose()` | public | Killt alle laufenden Prozesse, disposed Handles, fängt alle Exceptions per leerem catch ab |
| `PersistFehlgeschlagenAsync(Guid, int)` | private async | Wird per Fire-and-Forget aus beiden `Exited`-Handlern aufgerufen; hat eigenes try-catch inkl. `ObjectDisposedException`-Behandlung und `_isDisposed`-Prüfung |
| `RaiseRunningCountChanged()` | private | Berechnet und feuert `RunningCountChanged` |
| `TryGetExitCode(Process)` | private static | Fängt Exceptions ab, gibt null zurück |
| `WaitForExitAsync(...)` | private static async | Wartet mit Timeout via `CancellationTokenSource` |
| `SendCommandDelayedAsync(...)` | private async | Sendet verzögerten Befehl an cmd.exe; hat eigenes try-catch (`OperationCanceledException`, generisches `Exception` → `LogWarning`) |
| `BuildCliCommand(ProcessStartInfo)` | private static | Baut Kommandozeile für ConPTY |

Abonnierte Events: keine
Publizierte Events: `CliProcessStatusChanged` (`Action<Guid, CliProcessStatus>`), `RunningCountChanged` (`Action<int, int>`, von `IRunningAutomationStatusSource`)

## Weitere Klassen in dieser Datei

### `CliProcessHandle` (sealed class)
Handle auf einen laufenden CLI-Prozess.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `AufgabeId` | `Guid` | Zugehörige Aufgabe |
| `Process` | `Process` | Verwalteter Prozess |
| `LastHeartbeat` | `DateTimeOffset` | Letzter Heartbeat-Zeitstempel |
| `AbsichtlichGestoppt` | `bool` (volatile backing field) | Ob Prozess über `StopCliAsync` beendet wurde |
| `PseudoConsoleSession` | `PseudoConsoleSession?` | Zugehörige ConPTY-Session, falls vorhanden |

### `CliProcessStatus` (enum)
Siehe [enums.md](enums.md).

## Kritische Stellen (Bezug zur Anforderung)

- **F8:** Zeile 325–328 — `_ = SendCommandDelayedAsync(session, pluginCommand, aufgabeId, ct)`, Fire-and-Forget ohne zentrale Absicherung (Methode selbst hat aber try-catch)
- **F9:** Zeile 115–143 — `process.Exited`-Handler in `StartCliAsync` ohne umschließendes try-catch
- **F10:** Zeile 272–305 — `process.Exited`-Handler in `StartWithPseudoConsoleAsync` ohne umschließendes try-catch, inkl. `PseudoConsoleSession?.Dispose()` (Zeile 279)
- **F11:** Zeile 254–266 — FileStream-Erstellung ohne try-finally-Absicherung
