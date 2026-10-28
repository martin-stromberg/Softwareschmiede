# Enums: Autonome Aufgaben

## Ausschließlich autonom (würden entfernt)

## `PersistenzModus`
Datei: `src/Softwareschmiede/Domain/Enums/PersistenzModus.cs`
Verwendet in: `AutonomAufgabeKonfiguration.PersistenzModus`, `AutonomAufgabenInitialisierungsService`, `AutonomAufgabeInitialisierungsDialogViewModel`.

## `PermissionsJsonOption`
Datei: `src/Softwareschmiede/Domain/Enums/PermissionsJsonOption.cs`
Verwendet in: `AutonomAufgabeInitialisierungsAnfrage`, `AutonomAufgabenInitialisierungsService`, `AutonomAufgabeInitialisierungsDialogViewModel`.

## `UnteragentStatus`
Datei: `src/Softwareschmiede/Domain/Enums/UnteragentStatus.cs`
Verwendet in: `UnteragentSpezifikation.Status`, `ProjektleiterAgentService`, `UnteragentGovernanceMonitoringService`.

## `UnteragentAktion`
Datei: `src/Softwareschmiede/Domain/Enums/UnteragentAktion.cs`
Verwendet in: `UnteragentGovernanceService`, `ProjektleiterAgentService`.

## `SkillStatus`
Datei: `src/Softwareschmiede/Domain/Enums/SkillStatus.cs`
Verwendet in: `SkillDefinition.Status`.

## Gemeinsam genutzt (bleiben erhalten)

## `AufgabeAusfuehrungsStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatus.cs`
Werte: `NichtGestartet`, `Aktiv`, `Beendet`. Gemeinsam genutzt von `AufgabeRecoveryService`, `AufgabeService`, `KiPluginLimitService`, `App.xaml.cs`, `AppConverters`, `AktiveAufgabePanelItem`, `TaskDetailViewModel`. Autonome Nutzer sind nur `SessionManagementService` und `UnteragentGovernanceMonitoringService` (Filter auf `Aktiv`); diese werden mit dem Feature entfernt. Die Enum-Werte bleiben.

## `AufgabeStatus`, `AufgabeLaufStatus`
Dateien: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`, `AufgabeLaufStatus.cs`
Gemeinsam (regulär und ereignisgetrieben), nicht autonomspezifisch.

## `AufgabeAusfuehrungsStatusExtensions`, `AufgabeStatusExtensions`
Gemeinsam. `DarfAusfuehrungStarten` und `SollCliAnzeigen` bleiben erhalten.
