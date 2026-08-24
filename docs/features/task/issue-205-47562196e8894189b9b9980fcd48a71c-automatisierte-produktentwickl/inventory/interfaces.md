# Interfaces

## `IKiPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `StartCliAsync` | `localRepoPath: string`, `parameters: string?`, `ct: CancellationToken` | `Task<ProcessStartInfo>` | Startet den CLI-Prozess mit optionalen Parametern (z. B. Session-ID für `--continue`) und gibt ProcessStartInfo zurück. |
| `GetProcessWindowTitle` | `aufgabeId: Guid` | `string` | Gibt einen Hinweis auf den erwarteten Fenstertitel des CLI-Prozesses zurück (optional). |
| `SupportsSessionContinuation` | — | `bool` | Gibt an, ob das Plugin Session-Fortsetzung unterstützt. |
| `CheckHealthAsync` | `ct: CancellationToken` | `Task<bool>` | Prüft ob das Plugin verfügbar ist. |

**Erbt von**: `IPlugin`

**Beobachtung**: Die Methode `SupportsSessionContinuation()` ist bereits definiert, was bedeutet, dass Plugins bereits als Session-Forsetzung-fähig gekennzeichnet werden können (Anforderung Punkt 6: "Welche Plugins unterstützen es bereits?").

## `IRunningAutomationStatusSource`
Referenziert in: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

**Implementiert von**: `KiAusfuehrungsService`

| Methode/Event | Typ | Beschreibung |
|---------------|----|--|
| `IsRunning(Guid)` | `bool` | Prüft, ob ein Automation-Prozess läuft |
| `GetRunningCount()` | `int` | Gibt die Anzahl laufender Automation-Prozesse zurück |
| `RunningCountChanged` | `event Action<int, int>?` | Event wenn sich die Anzahl laufender Prozesse ändert |

**Beobachtung**: Dieses Interface wird von `AufgabeRecoveryService` und `KiAusfuehrungsService` verwendet, um den laufenden Status zu prüfen.

## `IGitPlugin`
Referenziert in: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` und Tests

**Verwendung**: Wird als optionaler Parameter in `StartCliAsync` und `StartWithPseudoConsoleAsync` übergeben, um den tatsächlichen Repository-Pfad zu ermitteln (z. B. bei `LocalDirectoryPlugin` im `InSourceDirectory`-Modus).
