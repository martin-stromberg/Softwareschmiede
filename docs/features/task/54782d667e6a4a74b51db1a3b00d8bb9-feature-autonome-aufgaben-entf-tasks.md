# Tasks: Entfernen des Features „Autonome Aufgaben"

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `AufgabeService.SetPauseAsync`: Autonom-Prüfung und `Include(AutonomKonfiguration)` entfernen | Erledigt | `AufgabeServiceTests_Pause.SetPauseAsync_ShouldPersistPausiertBisUtc_AndWriteSystemLog` |
| 2 | Logik | `AufgabeService.GetDetailAsync`: `Include(AutonomKonfiguration)` entfernen | Erledigt | Kein direkter Test |
| 3 | Logik | `AufgabeRecoveryService.RecoverManuellAsync`: Include und `IstAutonom()` entfernen | Erledigt | `AufgabeRecoveryServiceTests.RecoverManuellAsync_ShouldSetStatusAndCreateAudit_WhenTaskIsInArbeitAndNotRunning` |
| 4 | Logik | `AufgabeRecoveryService.IstRecoveryStatus`: Parameter `istAutonom` entfernen | Erledigt | `AufgabeRecoveryServiceTests.IstRecoveryStatus_ShouldMatchAllowedStates` |
| 5 | Logik | `AufgabeRecoveryService` Kandidatensuche: Filter `AutonomKonfiguration == null` entfernen | Erledigt | `AufgabeRecoveryServiceTests.ScanForRecoveryCandidates_ShouldExcludeRunningTasks` (Kandidatenabfrage für reguläre Aufgaben) |
| 6 | Logik | `KiPluginLimitService` Kandidatenfilter: `AutonomKonfiguration == null` entfernen | Erledigt | `KiPluginLimitServiceTests.VerarbeiteRateLimitAsync_ShouldPauseSamePrefixTasks_AndNotify` |
| 7 | Domain | `Aufgabe.AutonomKonfiguration` Navigation entfernen | Erledigt | Kein direkter Test (Kompilierung) |
| 8 | Domain | `AufgabeExtensions.cs` (enthält nur `IstAutonom`) löschen | Erledigt | Kein direkter Test (Datei entfernt) |
| 9 | Datenmodell | `SoftwareschmiededDbContext`: DbSets `AutonomAufgabeKonfigurationen`, `UnteragentSpezifikationen`, `SkillDefinitionen` entfernen | Erledigt | `RemoveAutonomAufgabeModelsMigrationTests.Migration_RemoveAutonomAufgabeModels_EntferntTabellenUndEinstellung` |
| 10 | Datenmodell | `SoftwareschmiededDbContext`: Mappings der autonomen Entitäten und `Aufgabe`-Navigation entfernen | Erledigt | `RemoveAutonomAufgabeModelsMigrationTests.Migration_RemoveAutonomAufgabeModels_EntferntTabellenUndEinstellung` |
| 11 | Datenmodell | Migration `RemoveAutonomAufgabeModels` erzeugen | Erledigt | `RemoveAutonomAufgabeModelsMigrationTests.Migration_RemoveAutonomAufgabeModels_EntferntTabellenUndEinstellung` |
| 12 | Datenmodell | Migration: SQL-Rücksetzung des Laufzustands für Aufgaben mit Config-Zeile | Erledigt | `RemoveAutonomAufgabeModelsMigrationTests.Migration_RemoveAutonomAufgabeModels_SetztLaufzustandZurueck` |
| 13 | Datenmodell | Migration: SQL-Löschung der Zeile `autonomeaufgaben.enabled` in `AppEinstellungen` | Erledigt | `RemoveAutonomAufgabeModelsMigrationTests.Migration_RemoveAutonomAufgabeModels_EntferntTabellenUndEinstellung` |
| 14 | Datenmodell | Migration: `DropTable` der drei autonomen Tabellen (Kindtabellen zuerst), `Down()` prüfen | Erledigt | `RemoveAutonomAufgabeModelsMigrationTests.Migration_RemoveAutonomAufgabeModels_Down_ErzeugtSchemaOhneDaten` |
| 15 | Datenmodell | Bestehende Migrationen und deren Designer-Dateien unverändert lassen (Diff-Prüfung) | Erledigt | Kein direkter Test (Review-Prüfung per `git status`: nur `SoftwareschmiededDbContextModelSnapshot.cs` geändert) |
| 16 | Logik | `AutonomAufgabenInitialisierungsService` löschen | Erledigt | Kein direkter Test (Datei entfernt) |
| 17 | Logik | `ProjektleiterAgentService` löschen | Erledigt | Kein direkter Test (Datei entfernt) |
| 18 | Logik | `UnteragentGovernanceService` und `UnteragentGovernanceMonitoringService` löschen | Erledigt | Kein direkter Test (Dateien entfernt) |
| 19 | Logik | `UnteragentGitProvisioningService`, `GitBranchHelper` löschen | Erledigt | Kein direkter Test (Dateien entfernt) |
| 20 | Logik | `SessionManagementService`, `StateJsonHelper` löschen | Erledigt | Kein direkter Test (Dateien entfernt) |
| 21 | Logik | `AutonomAufgabenOptions` und `Domain/Exceptions/UnteragentAbbruchException.cs` löschen | Erledigt | Kein direkter Test (Dateien entfernt) |
| 22 | Domain | Entitäten `AutonomAufgabeKonfiguration`, `UnteragentSpezifikation`, `SkillDefinition` löschen | Erledigt | Kein direkter Test (Dateien entfernt) |
| 23 | Domain | Value Objects `RessourcenLimits`, `AutonomAufgabeInitialisierungsAnfrage` löschen | Erledigt | Kein direkter Test (Dateien entfernt) |
| 24 | Domain | Enums `PersistenzModus`, `PermissionsJsonOption`, `UnteragentStatus`, `UnteragentAktion`, `SkillStatus` löschen | Erledigt | Kein direkter Test (Dateien entfernt) |
| 25 | Konfiguration | `appsettings.json`: Abschnitt `AutonomAufgaben` entfernen | Erledigt | Kein direkter Test (Konfigurationseintrag) |
| 26 | Konfiguration | `AppEinstellungService`: `AutonomAufgabenEnabledKey` und `GetAutonomAufgabenEnabledAsync` entfernen | Erledigt | Kein direkter Test (Konstante und Methode entfernt) |
| 27 | App | `App.xaml.cs`: Startup-Recovery für autonome Konfigurationen und `ErstelleResumePromptNachAppNeustart` entfernen | Erledigt | `MainTest.General_BasisUndEinstellungen_E2E` (App-Start ohne autonome Konfiguration) |
| 28 | App | `App.xaml.cs`: `Configure<AutonomAufgabenOptions>` und autonome DI-Registrierungen entfernen (`KiPluginLimitService`, `AufgabeRecoveryService` bleiben) | Erledigt | `MainTest.General_BasisUndEinstellungen_E2E` (App-Start mit DI) |
| 29 | App | `AutonomAufgabeStartService` und `AutonomAufgabeStartResult` löschen | Erledigt | Kein direkter Test (Dateien entfernt) |
| 30 | App | `AutonomAufgabeDetailViewModel` und `AutonomAufgabeDetailView` (inkl. `.xaml`) löschen | Erledigt | Kein direkter Test (Dateien entfernt) |
| 31 | App | `AutonomAufgabeInitialisierungsDialogViewModel` und `AutonomAufgabeInitialisierungsDialog` (inkl. `.xaml`) löschen | Erledigt | Kein direkter Test (Dateien entfernt) |
| 32 | App | `IDialogService` und `WpfDialogService`: `ShowAutonomAufgabeInitialisierungsDialogAsync` entfernen | Erledigt | Kein direkter Test (Methode entfernt) |
| 33 | App | `TaskDetailViewModel`: autonome Felder, Properties, Commands und Methoden entfernen | Erledigt | `TaskDetailViewModelTests_Pause.KannPausieren_ShouldBeTrue_FuerPausierbareStati` |
| 34 | App | `TaskDetailViewModel`: Prädikate `!IstAutonom()` (Pause, Starten, Command-Sichtbarkeit) vereinfachen | Erledigt | `TaskDetailViewModelTests_Pause.KannPausieren_ShouldBeTrue_FuerPausierbareStati` |
| 35 | App | `TaskDetailViewModel`: Konstruktorparameter (Start-Service, Options, DetailVM) entfernen | Erledigt | Kein direkter Test (Kompilierung; genutzt von `TaskDetailViewModelTestFactory`) |
| 36 | UI | `TaskDetailView.xaml`: Ribbon-Gruppe „Autonome Aufgabe" und `AutonomAufgabeDetailView` entfernen | Erledigt | `E2E_AufgabePausieren.AufgabePausieren_DialogCountdownAbblendungUndAufheben_E2E` (Negativprüfung Ribbon-Gruppe) |
| 37 | UI | `TaskDetailView.xaml`: Visibility-Bindings `IsAutonomAufgabe` entfernen | Erledigt | `E2E_AufgabePausieren.AufgabePausieren_DialogCountdownAbblendungUndAufheben_E2E` (Starten/PauseEinstellen sichtbar) |
| 38 | UI | `SettingsViewModel`: `IsAutonomAufgabenEnabled`, Lade-/Speicherzweige und `IOptions<AutonomAufgabenOptions>` entfernen | Erledigt | `SettingsViewModelTests.LadenAsync_LaedtDefaultPlugine` |
| 39 | UI | `SettingsView.xaml`: Abschnitt „Automatisierung" mit Checkbox vollständig entfernen | Erledigt | `MainTest.General_BasisUndEinstellungen_E2E` (Negativprüfung Checkbox und Abschnitt) |
| 40 | Build | `dotnet build Softwareschmiede.slnx -c Debug` bis zur Kompilierbarkeit des Produktivcodes | Offen | — |
| 41 | Tests | Autonome Testklassen löschen (Services, ViewModels, Domain-Tests, Helper-Factories) | Erledigt | Kein direkter Test (Dateien entfernt) |
| 42 | Tests | `AufgabeRecoveryServiceTests` anpassen (autonome Fälle entfernen, `IstRecoveryStatus`-Aufrufe ohne Parameter) | Erledigt | `AufgabeRecoveryServiceTests.IstRecoveryStatus_ShouldMatchAllowedStates` |
| 43 | Tests | `AufgabeServiceTests_Pause`: Test „autonome Aufgabe kann nicht pausiert werden" entfernen | Erledigt | `AufgabeServiceTests_Pause.SetPauseAsync_ShouldAllowActiveStatuses` |
| 44 | Tests | `TaskDetailViewModelTests*` und `TaskDetailViewModelTestsBase`: autonome Verweise entfernen; `_Automatisierung` löschen | Erledigt | `TaskDetailViewModelTests.ShowCliPanel_IsTrue_WhenStatusGestartet` |
| 45 | Tests | `TaskDetailViewModelTestFactory`: Konstruktorparameter und Fakes entfernen | Erledigt | Kein direkter Test (Hilfsklasse; genutzt von `TaskDetailViewModelTests_Pause`) |
| 46 | Tests | `SettingsViewModelTests` und `SettingsViewModelTests_IdePlugin`: Feature-Flag-Tests entfernen, Konstruktoraufrufe anpassen | Erledigt | `SettingsViewModelTests.LadenAsync_LaedtDefaultPlugine` |
| 47 | Tests | `MainWindowViewModelUpdateTestBase`: autonomen Verweis entfernen | Erledigt | Kein direkter Test (Testbasis) |
| 48 | Tests | IntegrationTests `EntwicklungsprozessServiceTests`: autonome Fälle entfernen oder anpassen | Erledigt | `EntwicklungsprozessServiceTests.ProzessStartenAsync_ShouldPersistBranchAndKlonPfad_WhenAufgabeExists` |
| 49 | Tests | Neuer Migrationstest `RemoveAutonomAufgabeModelsMigrationTests`: Tabellen und Einstellungszeile entfernt, andere Einstellungen bleiben | Erledigt | `RemoveAutonomAufgabeModelsMigrationTests.Migration_RemoveAutonomAufgabeModels_EntferntTabellenUndEinstellung` |
| 50 | Tests | Migrationstest: Laufzustand autonomer Aufgaben zurückgesetzt, Aufgabe-Zeile bleibt | Erledigt | `RemoveAutonomAufgabeModelsMigrationTests.Migration_RemoveAutonomAufgabeModels_SetztLaufzustandZurueck` |
| 51 | Tests | Migrationstest: reguläre Aufgaben bleiben unverändert | Erledigt | `RemoveAutonomAufgabeModelsMigrationTests.Migration_RemoveAutonomAufgabeModels_BehaeltRegulaereAufgaben` |
| 52 | Tests | Migrationstest: `Down()` erzeugt Schema ohne Daten | Erledigt | `RemoveAutonomAufgabeModelsMigrationTests.Migration_RemoveAutonomAufgabeModels_Down_ErzeugtSchemaOhneDaten` |
| 53 | E2E | Autonome E2E-Dateien und Page-Objects löschen | Erledigt | Kein direkter Test (Dateien entfernt) |
| 54 | E2E | `E2E_SettingsFeatureFlags`, `E2E_ViewPattern`, `MainTest`, `WpfTestBase`, `Views/*` bereinigen | Erledigt | `MainTest.General_BasisUndEinstellungen_E2E` (Methodenanzahl in `MainTest` unverändert: 12) |
| 55 | E2E | Negativprüfung „Checkbox und Abschnitt Automatisierung fehlen" in bestehende Settings-E2E-Methode einbauen (keine neue Methode) | Erledigt | `MainTest.General_BasisUndEinstellungen_E2E` (ruft `Einstellungen_AllgemeinEnthaeltNurVorhandeneAbschnitte_E2E`) |
| 56 | E2E | Negativprüfung „Ribbon-Gruppe Autonome Aufgabe fehlt" in bestehende Aufgaben-E2E-Methode einbauen (keine neue Methode) | Erledigt | `E2E_AufgabePausieren.AufgabePausieren_DialogCountdownAbblendungUndAufheben_E2E` |
| 57 | E2E | Regressionsnachweis: `E2E_AufgabePausieren`, `E2E_SessionLimitPause`, `E2E_ScmEvents`, Startlauf in `MainTest` grün | Offen | — (Testlauf ist Verifikation, nicht Teil dieses Reviews) |
| 58 | Doku | `docs/help/aufgaben/autonome-aufgaben/` (gesamter Ordner) löschen | Folgt in Dokumentationsschritt | — |
| 59 | Doku | `docs/help/aufgaben/` bereinigen | Folgt in Dokumentationsschritt | — |
| 60 | Doku | `docs/help/einstellungen/` bereinigen | Folgt in Dokumentationsschritt | — |
| 61 | Doku | `docs/help/index.md` und `docs/help/e2e-test-infrastruktur/*` bereinigen | Folgt in Dokumentationsschritt | — |
| 62 | Doku | `docs/help/terminal/api.md`: Verweis auf `ProjektleiterAgentService` entfernen | Folgt in Dokumentationsschritt | — |
| 63 | Doku | `README.md` bereinigen | Folgt in Dokumentationsschritt | — |
| 64 | Doku | Release Notes: knapper Hinweis zum Datenverlust durch die Migration | Folgt in Dokumentationsschritt | — |
| 65 | Verifikation | Rest-Suche nach autonomen Begriffen in `src/` (ohne Migrationen, bin, obj), `docs/help/`, `README.md` ohne Treffer | Offen | — (src-Teil im Review geprüft: nur begründete Treffer; `docs/help/` und `README.md` folgen im Dokumentationsschritt) |
| 66 | Verifikation | `dotnet format Softwareschmiede.slnx` und `--verify-no-changes` | Offen | — |
| 67 | Verifikation | Vollständiger Build `dotnet build Softwareschmiede.slnx -c Debug` ohne Fehler und Warnungen | Offen | — |
| 68 | Verifikation | Tests regulär (`Category!=OsInterface`) grün, synchron | Offen | — |
| 69 | Verifikation | Tests OS-Interface (`Category=OsInterface`, inkl. FlaUI-E2E) grün, synchron | Offen | — |
| 70 | Verifikation | IntegrationTests regulär und OS-Interface grün, synchron | Offen | — |
| 71 | Datenmodell | `SoftwareschmiededDbContextModelSnapshot.cs` durch EF aktualisiert | Erledigt | Kein direkter Test (EF-generiert; Schema-Abdeckung durch `RemoveAutonomAufgabeModelsMigrationTests`) |
