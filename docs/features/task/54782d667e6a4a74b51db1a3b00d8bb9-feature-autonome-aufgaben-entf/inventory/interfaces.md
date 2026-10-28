# Interfaces: Autonome Aufgaben

## `IDialogService` (Präsentationsschicht)
Datei: `src/Softwareschmiede.App/Services/IDialogService.cs` (Z. ~50–52)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ShowAutonomAufgabeInitialisierungsDialogAsync` | `AutonomAufgabeInitialisierungsDialogViewModel viewModel` (weitere Parameter gemäß Signatur) | `Task<AutonomAufgabeKonfiguration?>` | Zeigt den Initialisierungsdialog; `null` bei Abbruch. **Ausschließlich autonom.** Implementierung in `src/Softwareschmiede.App/Services/WpfDialogService.cs` (Z. ~141) entfällt. |

Die übrigen Methoden von `IDialogService` (z. B. Dialog für `AufgabePausierenDialog`) sind gemeinsam genutzt und bleiben.

## Interfaces in Domäne und Plugin-Contracts
Eine Suche nach `Autonom`, `Unteragent` und `ProjektleiterAgent` in `src/Softwareschmiede/Domain/Interfaces` und `src/Softwareschmiede.Plugin.Contracts` ergab keine Treffer. Die autonomen Dienste sind konkrete Klassen ohne eigene Interfaces.

## Konkrete Dienste ohne Interface (ausschließlich autonom)
- `AutonomAufgabenInitialisierungsService` (`src/Softwareschmiede/Application/Services/AutonomAufgabenInitialisierungsService.cs`)
- `ProjektleiterAgentService` (`src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs`)
- `UnteragentGovernanceService` (`src/Softwareschmiede/Application/Services/UnteragentGovernanceService.cs`)
- `UnteragentGovernanceMonitoringService` (`src/Softwareschmiede/Application/Services/UnteragentGovernanceMonitoringService.cs`)
- `UnteragentGitProvisioningService` (`src/Softwareschmiede/Application/Services/UnteragentGitProvisioningService.cs`)
- `SessionManagementService` (`src/Softwareschmiede/Application/Services/SessionManagementService.cs`)
- `AutonomAufgabeStartService` (`src/Softwareschmiede.App/Services/AutonomAufgabeStartService.cs`)
