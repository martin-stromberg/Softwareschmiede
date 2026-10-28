# Bestandsaufnahme: Entfernen des Features „Autonome Aufgaben“

Analysiert wurde der Code unter `src/` (Stand Commit `8c98efa`, Branch `task/54782d667e6a4a74b51db1a3b00d8bb9-feature-autonome-aufgaben-entf`) bezogen auf die übersetzte Anforderung [requirement.md](requirement.md). Ziel ist die Abgrenzung: Was gehört ausschließlich zum autonomen Feature, und was wird von der ereignisgetriebenen Bearbeitung weiterhin benötigt. Es wurden keine Codeänderungen vorgenommen und nichts committet.

## Zusammenfassung

### Abgrenzung (Kernbefund)

**Ausschließlich autonom (kann entfernt werden):**
- Entitäten `AutonomAufgabeKonfiguration`, `UnteragentSpezifikation`, `SkillDefinition` samt DbSets und Mappings in `SoftwareschmiededDbContext`
- Value Objects `RessourcenLimits`, `AutonomAufgabeInitialisierungsAnfrage`; Enums `PersistenzModus`, `PermissionsJsonOption`, `UnteragentStatus`, `UnteragentAktion`, `SkillStatus`
- Services `AutonomAufgabenInitialisierungsService`, `ProjektleiterAgentService`, `UnteragentGovernanceService`, `UnteragentGovernanceMonitoringService` (Hosted Service), `UnteragentGitProvisioningService`, `SessionManagementService`, `GitBranchHelper`, `StateJsonHelper`, `AutonomAufgabenOptions`
- App: `AutonomAufgabeStartService`, `AutonomAufgabeDetailViewModel`/View, `AutonomAufgabeInitialisierungsDialogViewModel`/View, `IDialogService.ShowAutonomAufgabeInitialisierungsDialogAsync`
- Tests und Hilfsklassen (siehe [tests.md](inventory/tests.md))
- Migrationen (Historie bleibt; neue additive Migration für das Entfernen der Tabellen nötig)
- Hilfeseiten `docs/help/aufgaben/autonome-aufgaben/` (siehe [dokumentation.md](inventory/dokumentation.md))

**Gemeinsam, muss erhalten bleiben (ereignisgetriebene Bearbeitung und einfacher Aufgabenstart):**
- `Aufgabe` (inkl. `PausiertBisUtc`, `ScmAbschlussErkanntUtc`, `ScmAbschlussReferenz`, `BasisBranchName`, `KiPluginPrefix`, Ausführungs-/Recovery-Felder)
- Der Pausen-Mechanismus `PausiertBisUtc`. Er wird von `KiPluginLimitService` (Session-Limit), `AufgabeService.SetPauseAsync`, `PromptZeitVersandService`, `EntwicklungsprozessService`, `ScmEventOrchestrator` und `AufgabeRecoveryService` genutzt. Er ist **kein** autonomes Feature, trotz der Erwähnung in der Anforderung.
- Komplette `ScmEvent*`-Kette (`ScmEventMonitoringService`, `ScmEventOrchestrator`, `ScmEventStore`, `ScmEventOutbox`, `ScmEventExchangeService`, `ScmEventValidationService`, `ScmEventTaskResolver`, `ScmPromptAppendix`, `ScmCliAccessEvaluator`, ...), `ScmTaskCompletionMonitoringService`, `ScmBackmergeService`. Keine Verweise auf das autonome Feature.
- `EntwicklungsprozessService` (einfacher Start), `GitOrchestrationService` (`BasisBranchName`), `PromptZeitVersandService`
- `AufgabeRecoveryService` (Heartbeat-Recovery), `KiPluginLimitService` (Plugin-Session-Limit)
- `AufgabeAusfuehrungsStatus`, `AufgabeStatus`, `AufgabeLaufStatus` und deren Extensions
- `AppEinstellungService` (Schlüssel `ScmMonitoringEnabledKey`, `GetScmMonitoringEnabledAsync` u. a.)

**Kopplungspunkte, die bei der Entfernung bereinigt werden müssen (nicht einfach löschen):**
1. `Aufgabe.AutonomKonfiguration` (Navigation) und `AufgabeExtensions.IstAutonom()`. Aufrufer: `AufgabeService.SetPauseAsync` (Pause-Verweigerung für autonome Aufgaben), `AufgabeRecoveryService.RecoverManuellAsync`, `TaskDetailViewModel` (mehrere UI-Prädikate, z. B. Z. 251, 488, 578, 756). Das ist die stärkste Kopplung.
2. Der Filter `a.AutonomKonfiguration == null` in `AufgabeRecoveryService` (Z. ~54) und `KiPluginLimitService` (Z. ~85). Nach dem Entfernen entfällt der Filter; das Verhalten für reguläre Aufgaben muss gleich bleiben.
3. `App.xaml.cs`: Startup-Recovery für autonome Konfigurationen (Z. ~114–136), `services.Configure<AutonomAufgabenOptions>` (Z. ~234), DI-Registrierungen (Z. ~241–245, 299, 334–335, 397). Die Registrierung von `SessionManagementService` entfällt; `KiPluginLimitService` (Z. 285) und `AufgabeRecoveryService` (Z. 290) bleiben.
4. `TaskDetailViewModel`: Felder und Properties des autonomen Ribbon-Bereichs (`ShowAutonomAufgabeRibbonGruppe`, `ShowAutomatisierungPanel`, `AutonomAufgabeDetailViewModel`, `AutonomAufgabeInitialisierenCommand`). Die Ribbon-Gruppe „Autonome Aufgabe“ in `TaskDetailView.xaml` (Z. ~214–237, 648) entfällt.
5. `AppEinstellungService.AutonomAufgabenEnabledKey` (`autonomeaufgaben.enabled`) und `GetAutonomAufgabenEnabledAsync` (Z. ~43, ~179). `SettingsViewModel`, `SettingsView.xaml` (Checkbox Z. ~284–286), `appsettings.json` (Section `AutonomAufgaben`, Z. 17).
6. Migrationen: `20260823091609_AddRepositoryInitialisierungKonfiguration` ist nicht eindeutig zuzuordnen und muss vor der Entscheidung geprüft werden.

### Test-Ausgangszustand

Alle vier Testläufe sind grün (Build Exit 0, 0 Warnungen, 0 Fehler):

| Suite | Lauf | Gesamt | Bestanden | Fehlgeschlagen | Übersprungen |
|-------|------|--------|-----------|----------------|--------------|
| Tests | regulär (`Category!=OsInterface`) | 2190 | 2189 | 0 | 1 (Plattform: Nicht-Windows-Test) |
| Tests | OS-Interface (`Category=OsInterface`) | 71 | 66 | 0 | 5 (ConPTY, env-gesteuert) |
| IntegrationTests | regulär | 237 | 237 | 0 | 0 |
| IntegrationTests | OS-Interface | 9 | 9 | 0 | 0 |

Summe: 2507 Tests, 2501 bestanden, 6 übersprungen, **0 fehlgeschlagen**. Es gibt daher keinen nachgewiesenen bestehenden Testfehler. Nachweise (Logs, TRX, Build-Log) liegen unter [inventory/test-results/](inventory/test-results/), Details in [tests.md](inventory/tests.md).

Testlücken:
- Die 5 ConPTY-Tests (`End2EndTest.*`, `E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung`) sind in dieser Sandbox übersprungen (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`). Ein Vergleich vor/nach der Änderung ist hier nicht möglich; er muss in einer interaktiven Sitzung ergänzt werden.
- Die E2E-Dateien `E2E_AutonomAufgabenAgentExecution` und `E2E_AutonomAufgabenInitialisierung` enthalten keine Testmethoden (Hilfs-/Basisklassen). Ihr Inhalt wurde nicht im Detail untersucht.

### Offene Punkte für die Planung (aus der Anforderung, nicht entschieden)

1. Löschen der autonomen Tabellen per Migration oder Belassen als ungenutzte Tabellen.
2. Umgang mit bestehenden autonomen Aufgaben (`AutonomKonfiguration != null`): Die Aufgaben bleiben als reguläre Aufgaben ohne Konfiguration, oder sie werden gesperrt bzw. migriert.
3. Die Migration `AddRepositoryInitialisierungKonfiguration` ist zuzuordnen.
4. Der Abschnitt „Automatisierung“ in den Einstellungen entfällt, da nach der Entfernung keine Einstellung mehr darin liegt (Anforderung, offene Frage 5). Zu bestätigen.

## Details

- [Datenmodell](inventory/models.md): Entitäten, Value Objects, Mappings, Migrationen (autonom vs. gemeinsam)
- [Logik](inventory/logic.md): Services, ViewModels, Kopplungspunkte, Abgrenzung der ereignisgetriebenen Bearbeitung
- [Enums](inventory/enums.md): autonome und gemeinsame Enums
- [Interfaces](inventory/interfaces.md): `IDialogService` und konkrete Dienste ohne Interface
- [Tests](inventory/tests.md): Test-Ausgangszustand, Testläufe, übersprungene Tests, Testklassen mit autonomem Bezug
- [Dokumentation](inventory/dokumentation.md): betroffene Hilfeseiten und README
- [Testnachweise](inventory/test-results/): Build-Log, Testlogs und TRX-Reports des Ausgangslaufs
