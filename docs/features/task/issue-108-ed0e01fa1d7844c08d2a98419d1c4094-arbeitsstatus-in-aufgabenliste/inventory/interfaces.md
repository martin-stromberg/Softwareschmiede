# Interfaces

## `IRunningAutomationStatusSource`
Datei: `src\Softwareschmiede\Domain\Interfaces\IRunningAutomationStatusSource.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRunningCount()` | - | `int` | Gibt die Anzahl aktuell laufender Automatisierungen zurück |
| `IsRunning(Guid aufgabeId)` | `Guid aufgabeId` | `bool` | Gibt an, ob für eine konkrete Aufgabe aktuell eine Automatisierung läuft |

| Event | Signatur | Zweck |
|-------|----------|-------|
| `RunningCountChanged` | `event Action<int, int>?` | Wird ausgelöst, wenn sich die Anzahl laufender Automatisierungen ändert (Argumente: vorheriger und aktueller Wert) |

### Implementierung:
- **`KiAusfuehrungsService`** implementiert dieses Interface
- `GetRunningCount()` zählt alle Prozesse in `_handles`, deren Prozess nicht beendet ist
- `IsRunning(aufgabeId)` prüft, ob ein Handle für die Aufgabe existiert und der Prozess läuft
- `RunningCountChanged` wird in `RaiseRunningCountChanged()` ausgelöst, wenn sich der Running-Count ändert

### Verwendung in der Anforderung:
- **`AufgabeRecoveryService`** nutzt `IRunningAutomationStatusSource` (Dependency Injection), um zu prüfen, ob ein Prozess noch läuft (vor Recovery)
- `IsRunning(aufgabeId)` wird in `ScanForRecoveryCandidatesAsync()` aufgerufen, um verwaiste Aufgaben zu identifizieren
- **Potenzielle Verwendung:** ViewModels könnten `RunningCountChanged` abonnieren, um periodische Aktualisierungen zu triggern
