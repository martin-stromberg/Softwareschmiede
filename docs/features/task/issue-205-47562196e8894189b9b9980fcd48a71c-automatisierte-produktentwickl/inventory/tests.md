# Tests

## Testklassen

### `ProjektleiterAgentServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests.cs`

- `StarteAgentAsync_StartetAgentMitInitialprompt()` — Testet, dass `StarteAgentAsync` den Projektleiter-Agenten mit dem Initialprompt startet, `AusfuehrungsStatus = Aktiv` setzt, und die Skill-Datei generiert.
- `SteuereUnteragentAsync_ErzetArbeitsverzMischnisUndGit()` — (weitere Testmethoden in Datei nicht vollständig gelesen)

**Beobachtung**: Tests für die geplanten Methoden `StarteAgenNachAppNeustartAsync` und `StoppeAgenExplizitAsync` sind **nicht vorhanden**, da diese Methoden noch nicht implementiert sind.

### `ProjektleiterAgentServiceTests_Fehlerfaelle`
Datei: `src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests_Fehlerfaelle.cs`

- Fehlerfall-Testmethoden für `ProjektleiterAgentService` (Details nicht vollständig gelesen)

### `SessionManagementServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/SessionManagementServiceTests.cs`

- Tests für `SessionManagementService` (Details nicht vollständig gelesen)

### `AufgabeRecoveryServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/AufgabeRecoveryServiceTests.cs`

- Tests für `AufgabeRecoveryService.ScanForRecoveryCandidatesAsync` und `RecoverManuellAsync` (Details nicht vollständig gelesen)

### `AutonomAufgabeDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeDetailViewModelTests.cs`

- Tests für das `AutonomAufgabeDetailViewModel` (Details nicht vollständig gelesen)

### `AutonomAufgabeStartServiceTests`
Datei: `src/Softwareschmiede.Tests/App/Services/AutonomAufgabeStartServiceTests.cs`

- Tests für `AutonomAufgabeStartService` (Details nicht vollständig gelesen)

### `KiAusfuehrungsServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`

- Tests für `KiAusfuehrungsService.StartCliAsync` und `StartWithPseudoConsoleAsync` (Details nicht vollständig gelesen)

### `TaskDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

- Tests für `TaskDetailViewModel` (Details nicht vollständig gelesen)

## Test-Hilfsmethoden

### `ProjektleiterAgentServiceTestDatenFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/ProjektleiterAgentServiceTestDatenFactory.cs`

- `ErstelleAutonomeAufgabeAsync(db, projektId, testRoot)` — Erstellt Test-Daten für eine Autonome Aufgabe (wird in `ProjektleiterAgentServiceTests.StarteAgentAsync_StartetAgentMitInitialprompt()` verwendet)

### `TaskDetailViewModelTestFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`

- Hilfsmethoden zum Erstellen von `TaskDetailViewModel`-Instanzen für Tests (Details nicht vollständig gelesen)

## E2E-Tests

### `E2E_ArbeitsstatusAktualisierung`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_ArbeitsstatusAktualisierung.cs`

- E2E-Tests für Arbeitsstatus-Aktualisierung (CliProcessStatus-Referenzen vorhanden)

**Beobachtung**: E2E-Tests für die geplanten Anforderungen sind **nicht vorhanden**:
- Test für Ribbon-Button „Start" startet echte CLI
- Test für automatische Wiederaufnahme nach App-Neustart
- Test für explizites Stoppen

## Architektur-Beobachtungen

1. **CLI-Prozess-Start**: `KiAusfuehrungsService` wird direkt für den Prozessstart verwendet (keine abstrakte `IAgentRuntime`-Schicht vorhanden, wie in der Anforderung ursprünglich vermutet).

2. **optionalParameters-Handling**: Die Plugins erhalten optionale Parameter über `IKiPlugin.StartCliAsync(localRepoPath, parameters)`. Es ist zu prüfen, ob alle implementierten Plugins diese korrekt weitergeben.

3. **Session-Continuation-Support**: `IKiPlugin.SupportsSessionContinuation()` ist bereits definiert, aber es ist unklar, welche Plugins diese Methode implementieren und was die Semantik des Support-Flags ist.

4. **Recovery-Service für Autonome Aufgaben**: `AufgabeRecoveryService` schließt gezielt `a.AutonomKonfiguration == null` aus, d. h. Recovery für Autonome Aufgaben ist **nicht** implementiert — dies soll durch eine neue Methode (`StarteAgenNachAppNeustartAsync`) in `ProjektleiterAgentService` erfolgen.

5. **ExplicitStop-Flag**: `AutonomAufgabeKonfiguration.ExplizitGestoppt` ist **nicht vorhanden**, daher ist eine Migration erforderlich.
