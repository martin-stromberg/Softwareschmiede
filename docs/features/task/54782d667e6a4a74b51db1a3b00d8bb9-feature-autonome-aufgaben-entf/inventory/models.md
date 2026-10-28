# Datenmodell: Autonome Aufgaben vs. gemeinsam genutzte Felder

Stand: Commit `8c98efa`, Branch `task/54782d667e6a4a74b51db1a3b00d8bb9-feature-autonome-aufgaben-entf`. Keine Codeänderungen.

## Ausschließlich autonom (würden entfernt)

## `AutonomAufgabeKonfiguration`
Datei: `src/Softwareschmiede/Domain/Entities/AutonomAufgabeKonfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel |
| `AufgabeId` | `Guid` | FK auf `Aufgabe` (1:1, Unique-Index) |
| `ProjektBranchName` | `string` | Projektbranch der autonomen Aufgabe |
| `InitialPrompt` | `string` | Initialprompt für den Projektleiter |
| `PermissionsJsonPfad` | `string` | Pfad zur `permissions.json` |
| `TokenBudget` / `TokenBudgetErweitert` / `LaufzeitLimitMinuten` | `int` / `int?` / `int` | Ressourcenlimits (Convenience-Property `RessourcenLimits`, `[NotMapped]`) |
| `PersistenzModus` | `PersistenzModus` | Session-Persistenz (als String gespeichert) |
| `SkillAutogeneration` | `bool` | Flag: Skills automatisch generieren |
| `ArbeitsverzeichnisPfad` | `string` | Arbeitsverzeichnis der autonomen Aufgabe |
| `ProjektleiterAgentId` | `string?` | ID des laufenden Projektleiter-Agenten |
| `SessionPauseUtc` | `DateTimeOffset?` | Session-Pause wegen Budget-Limit (nicht identisch mit `Aufgabe.PausiertBisUtc`) |
| `AktiveUnteragenten` | `int?` | Anzahl aktiver Unteragenten |
| `ExplizitGestoppt` | `bool` | Benutzer hat die CLI explizit gestoppt |
| `Aufgabe` | `Aufgabe` | Navigation zurück |
| `Unteragenten` | `List<UnteragentSpezifikation>` | Navigation |
| `Skills` | `List<SkillDefinition>` | Navigation |

Referenziert von: `ProjektleiterAgentService`, `SessionManagementService`, `AutonomAufgabenInitialisierungsService`, `UnteragentGovernanceMonitoringService`, `App.xaml.cs` (Startup-Recovery), `TaskDetailViewModel`, `AutonomAufgabeDetailViewModel`.

## `UnteragentSpezifikation`
Datei: `src/Softwareschmiede/Domain/Entities/UnteragentSpezifikation.cs`

Eigenschaften u. a.: `Id`, `AutonomAufgabeId` (FK), `ExterneAgentId`, `TaskId`, `Scope`, `Prompt`, `VerzeichnisPfad`, `Branch`, `ClonePfad`, `Status` (`UnteragentStatus`), `ErzeugungsDatum`, `AbschlussDatum`; Navigation `AutonomAufgabe`.

Genutzt von: `ProjektleiterAgentService`, `UnteragentGitProvisioningService`, `UnteragentGovernanceService`, `UnteragentGovernanceMonitoringService`, `AutonomAufgabeDetailViewModel`.

## `SkillDefinition`
Datei: `src/Softwareschmiede/Domain/Entities/SkillDefinition.cs`

Eigenschaften u. a.: `Id`, `AutonomAufgabeId` (FK), `Name`, `Version`, `Content`, `Status` (`SkillStatus`), `ErstellungsDatum`, `FreigabeDatum`; Navigation `AutonomAufgabe`.

Genutzt nur von der autonomen Konfiguration und `AutonomAufgabeDetailViewModel`.

## `RessourcenLimits` (Value Object)
Datei: `src/Softwareschmiede/Domain/ValueObjects/RessourcenLimits.cs`

Record `(TokenBudget, TokenBudgetErweitert, LaufzeitLimitMinuten)`. Nur von der autonomen Konfiguration und dem Initialisierungs-Dialog verwendet.

## `AutonomAufgabeInitialisierungsAnfrage` (Value Object)
Datei: `src/Softwareschmiede/Domain/ValueObjects/AutonomAufgabeInitialisierungsAnfrage.cs`

Record mit Initialisierungsparametern (u. a. `RessourcenLimits`, `PersistenzModus`, `PermissionsQuelle`). Nur von `AutonomAufgabenInitialisierungsService` und dem Dialog-ViewModel verwendet.

## `AutonomAufgabenOptions` (Konfiguration)
Datei: `src/Softwareschmiede/Application/Services/AutonomAufgabenOptions.cs`

Section `AutonomAufgaben` in `src/Softwareschmiede/appsettings.json` (Felder u. a. `Enabled`, `DefaultTokenBudget`, `DefaultRuntimeLimitMinutes`). Gebunden in `src/Softwareschmiede.App/App.xaml.cs` (Z. ~234).

## Gemeinsam genutzt (bleiben erhalten)

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Gemeinsam? | Anmerkung |
|-------------|-----------|-----------|
| `Status`, `AusfuehrungsStatus`, `AktiveRunId`, `LaufStatus`, `LastHeartbeatUtc`, `LetzterCliStartUtc`, `RecoveryVersion` | Ja | Ausführungs-/Recovery-Modell regulärer Aufgaben |
| `PausiertBisUtc` | Ja | Pausen-Mechanismus. Genutzt von `KiPluginLimitService` (Session-Limit), `AufgabeService.SetPauseAsync`, `PromptZeitVersandService`, `EntwicklungsprozessService`, `ScmEventOrchestrator`, `AufgabeRecoveryService`. **Kein autonomes Feld**, trotz Erwähnung in der Anforderung. |
| `ScmAbschlussErkanntUtc`, `ScmAbschlussReferenz` | Ja | Abschlussüberwachung ereignisgetriebener Aufgaben (`ScmTaskCompletionMonitoringService`) |
| `BranchName`, `LokalerKlonPfad`, `BasisBranchName`, `GitArbeitsbereich` | Ja | `BasisBranchName` wird u. a. von `GitOrchestrationService` (Z. ~245) genutzt |
| `VorschlagPrompt`, `VorschlagAusfuehrenAbUtc` | Ja | Zeitgesteuerte Prompts (`PromptZeitVersandService`) |
| `KiPluginPrefix` | Ja | `KiPluginLimitService`, Plugin-Logik |
| `AutonomKonfiguration` (Navigation) | **Nein** | Einziger Verweis der Aufgabe auf das autonome Feature; muss entfernt werden |

## `AufgabeExtensions.IstAutonom()`
Datei: `src/Softwareschmiede/Domain/Entities/AufgabeExtensions.cs`

Implementierung: `aufgabe.AutonomKonfiguration is not null`. Aufrufer:
- `AufgabeService.SetPauseAsync` (Z. ~846): verweigert Pause für autonome Aufgaben
- `AufgabeRecoveryService.RecoverManuellAsync` (Z. ~84): Übergabe an `IstRecoveryStatus`
- `TaskDetailViewModel` (`IsAutonomAufgabe` und mehrere Command-/Sichtbarkeits-Prädikate, z. B. Z. 251, 488, 578, 756)

Hinweis: Diese Abfragen liegen auf gemeinsamen Pfaden (Recovery, Pause, Ribbon-Logik) und müssen bereinigt, nicht nur gelöscht werden.

## `AufgabeAusfuehrungsStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatus.cs`

Werte `NichtGestartet`, `Aktiv`, `Beendet`. Gemeinsam genutzt. Die Migration `NormalisiereAutonomAufgabeAusfuehrungsStatus` hat nur historische Daten bereinigt; die Enum-Werte bleiben.

## Datenbank

Datei: `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`

- Autonome DbSets: `AutonomAufgabeKonfigurationen` (Z. 91), `UnteragentSpezifikationen` (Z. 94), `SkillDefinitionen` (Z. 97).
- Autonome Mappings: `Aufgabe.AutonomKonfiguration` 1:1 (Z. 239–243), `AutonomAufgabeKonfiguration` (Z. 245–264), `UnteragentSpezifikation` (Z. 266–281), `SkillDefinition` (Z. 283–294).
- Gemeinsam: `Aufgabe`, `PullRequestReferenz`, `IssueReferenz`, `AlertReferenz`, `ScmEventInbox`, `ScmTaskLease` u. a. bleiben unverändert.

### Migrationen (`src/Softwareschmiede/Migrations/`)

Rein autonom (Historie, bleiben unverändert):
- `20260820175118_AddAutonomAufgabeModels`
- `20260821160341_AddAutonomAufgabeMaxLengthConstraints`
- `20260821192052_RenameSkillDefinitionProperties`
- `20260821193422_RenameUnteragentSpezifikationProperties`
- `20260822045627_VerschiebeProjektleiterFelderZuAutonomKonfiguration`
- `20260822052653_NormalisiereAutonomAufgabeAusfuehrungsStatus`
- `20260824173849_AddExplizitGestopptToAutonomAufgabeKonfiguration`

Zu prüfen (Name Autonom-bezogen, Inhalt nicht im Detail untersucht): `20260823091609_AddRepositoryInitialisierungKonfiguration`. Dessen Schema betrifft laut Modellnamen die Repository-Initialisierung; die Zuordnung zur autonomen Konfiguration oder zu `GitRepository` ist offen.

Gemeinsam (bleiben): `20260824175841_AddAufgabeBasisBranchName`, `20260901142800_AddPullRequestReviewSource`, `20260916094502_AddAufgabePausiertBisUtc`, `20261011045239_AddScmEventInbox`, `20261017195348_AddIssueReferenzSourceKey`, `20261020062227_AddAufgabeScmAbschluss`.

Die Entfernung der Tabellen erfordert eine neue, additive Migration; bestehende Migrationen werden nicht verändert. Ob Daten gelöscht werden, ist offene Frage 1 der Anforderung.
