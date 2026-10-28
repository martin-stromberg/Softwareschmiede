# Tasks: Entfernen des Features „Autonome Aufgaben"

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `AufgabeService.SetPauseAsync`: Autonom-Prüfung und `Include(AutonomKonfiguration)` entfernen | Offen | — |
| 2 | Logik | `AufgabeService.GetDetailAsync`: `Include(AutonomKonfiguration)` entfernen | Offen | — |
| 3 | Logik | `AufgabeRecoveryService.RecoverManuellAsync`: Include und `IstAutonom()` entfernen | Offen | — |
| 4 | Logik | `AufgabeRecoveryService.IstRecoveryStatus`: Parameter `istAutonom` entfernen | Offen | — |
| 5 | Logik | `AufgabeRecoveryService` Kandidatensuche: Filter `AutonomKonfiguration == null` entfernen | Offen | — |
| 6 | Logik | `KiPluginLimitService` Kandidatenfilter: `AutonomKonfiguration == null` entfernen | Offen | — |
| 7 | Domain | `Aufgabe.AutonomKonfiguration` Navigation entfernen | Offen | — |
| 8 | Domain | `AufgabeExtensions.cs` (enthält nur `IstAutonom`) löschen | Offen | — |
| 9 | Datenmodell | `SoftwareschmiededDbContext`: DbSets `AutonomAufgabeKonfigurationen`, `UnteragentSpezifikationen`, `SkillDefinitionen` entfernen | Offen | — |
| 10 | Datenmodell | `SoftwareschmiededDbContext`: Mappings der autonomen Entitäten und `Aufgabe`-Navigation entfernen | Offen | — |
| 11 | Datenmodell | Migration `RemoveAutonomAufgabeModels` mit `dotnet ef migrations add` erzeugen | Offen | — |
| 12 | Datenmodell | Migration: SQL-Rücksetzung des Laufzustands für Aufgaben mit Config-Zeile ergänzen (`AusfuehrungsStatus` = `NichtGestartet`, `AktiveRunId`, `PausiertBisUtc`, `LastHeartbeatUtc`) | Offen | — |
| 13 | Datenmodell | Migration: SQL-Löschung der Zeile `autonomeaufgaben.enabled` in `AppEinstellungen` ergänzen | Offen | — |
| 14 | Datenmodell | Migration: `DropTable` der drei autonomen Tabellen (Kindtabellen zuerst), `Down()` prüfen | Offen | — |
| 15 | Datenmodell | Bestehende Migrationen und deren Designer-Dateien unverändert lassen (Diff-Prüfung) | Offen | — |
| 16 | Logik | `AutonomAufgabenInitialisierungsService` löschen | Offen | — |
| 17 | Logik | `ProjektleiterAgentService` löschen | Offen | — |
| 18 | Logik | `UnteragentGovernanceService` und `UnteragentGovernanceMonitoringService` löschen | Offen | — |
| 19 | Logik | `UnteragentGitProvisioningService`, `GitBranchHelper` löschen | Offen | — |
| 20 | Logik | `SessionManagementService`, `StateJsonHelper` löschen | Offen | — |
| 21 | Logik | `AutonomAufgabenOptions` und `Domain/Exceptions/UnteragentAbbruchException.cs` löschen | Offen | — |
| 22 | Domain | Entitäten `AutonomAufgabeKonfiguration`, `UnteragentSpezifikation`, `SkillDefinition` löschen | Offen | — |
| 23 | Domain | Value Objects `RessourcenLimits`, `AutonomAufgabeInitialisierungsAnfrage` löschen | Offen | — |
| 24 | Domain | Enums `PersistenzModus`, `PermissionsJsonOption`, `UnteragentStatus`, `UnteragentAktion`, `SkillStatus` löschen | Offen | — |
| 25 | Konfiguration | `appsettings.json`: Abschnitt `AutonomAufgaben` entfernen | Offen | — |
| 26 | Konfiguration | `AppEinstellungService`: `AutonomAufgabenEnabledKey` und `GetAutonomAufgabenEnabledAsync` entfernen | Offen | — |
| 27 | App | `App.xaml.cs`: Startup-Recovery für autonome Konfigurationen und `ErstelleResumePromptNachAppNeustart` entfernen | Offen | — |
| 28 | App | `App.xaml.cs`: `Configure<AutonomAufgabenOptions>` und autonome DI-Registrierungen entfernen (`KiPluginLimitService`, `AufgabeRecoveryService` bleiben) | Offen | — |
| 29 | App | `AutonomAufgabeStartService` und `AutonomAufgabeStartResult` löschen | Offen | — |
| 30 | App | `AutonomAufgabeDetailViewModel` und `AutonomAufgabeDetailView` (inkl. `.xaml`) löschen | Offen | — |
| 31 | App | `AutonomAufgabeInitialisierungsDialogViewModel` und `AutonomAufgabeInitialisierungsDialog` (inkl. `.xaml`) löschen | Offen | — |
| 32 | App | `IDialogService` und `WpfDialogService`: `ShowAutonomAufgabeInitialisierungsDialogAsync` entfernen | Offen | — |
| 33 | App | `TaskDetailViewModel`: autonome Felder, Properties, Commands und Methoden entfernen | Offen | — |
| 34 | App | `TaskDetailViewModel`: Prädikate `!IstAutonom()` (Pause, Starten, Command-Sichtbarkeit) vereinfachen | Offen | — |
| 35 | App | `TaskDetailViewModel`: Konstruktorparameter (Start-Service, Options, DetailVM) entfernen | Offen | — |
| 36 | UI | `TaskDetailView.xaml`: Ribbon-Gruppe „Autonome Aufgabe" und `AutonomAufgabeDetailView` entfernen | Offen | — |
| 37 | UI | `TaskDetailView.xaml`: Visibility-Bindings `IsAutonomAufgabe` (Z. ~53, 58) entfernen | Offen | — |
| 38 | UI | `SettingsViewModel`: `IsAutonomAufgabenEnabled`, Lade-/Speicherzweige und `IOptions<AutonomAufgabenOptions>` entfernen | Offen | — |
| 39 | UI | `SettingsView.xaml`: Abschnitt „Automatisierung" mit Checkbox vollständig entfernen | Offen | — |
| 40 | Build | `dotnet build Softwareschmiede.slnx -c Debug` bis zur Kompilierbarkeit des Produktivcodes | Offen | — |
| 41 | Tests | Autonome Testklassen löschen (Services, ViewModels, Domain-Tests, Helper-Factories inkl. `UnteragentSpezifikationTests_GitArbeitsbereich`) | Offen | — |
| 42 | Tests | `AufgabeRecoveryServiceTests` anpassen (autonome Fälle entfernen, `IstRecoveryStatus`-Aufrufe ohne Parameter) | Offen | — |
| 43 | Tests | `AufgabeServiceTests_Pause`: Test „autonome Aufgabe kann nicht pausiert werden" entfernen | Offen | — |
| 44 | Tests | `TaskDetailViewModelTests*` und `TaskDetailViewModelTestsBase`: autonome Verweise entfernen; `_Automatisierung` prüfen und ggf. löschen | Offen | — |
| 45 | Tests | `TaskDetailViewModelTestFactory`: Konstruktorparameter und Fakes entfernen | Offen | — |
| 46 | Tests | `SettingsViewModelTests` und `SettingsViewModelTests_IdePlugin`: Feature-Flag-Tests entfernen, Konstruktoraufrufe anpassen | Offen | — |
| 47 | Tests | `MainWindowViewModelUpdateTestBase`: autonomen Verweis entfernen | Offen | — |
| 48 | Tests | IntegrationTests `EntwicklungsprozessServiceTests`: autonome Fälle entfernen oder anpassen | Offen | — |
| 49 | Tests | Neuer Migrationstest `RemoveAutonomAufgabeModelsMigrationTests`: Tabellen und Einstellungszeile entfernt, andere Einstellungen bleiben | Offen | — |
| 50 | Tests | Migrationstest: Laufzustand autonomer Aufgaben zurückgesetzt, Aufgabe-Zeile bleibt | Offen | — |
| 51 | Tests | Migrationstest: reguläre Aufgaben bleiben unverändert | Offen | — |
| 52 | Tests | Migrationstest: `Down()` erzeugt Schema ohne Daten | Offen | — |
| 53 | E2E | Autonome E2E-Dateien und Page-Objects löschen (`E2E_AutonomAufgaben*`, `Views/AutonomAufgabeDetailView`, `Views/Dialogs/AutonomAufgabeInitialisierungsDialogView`) | Offen | — |
| 54 | E2E | `E2E_SettingsFeatureFlags`, `E2E_ViewPattern`, `MainTest`, `WpfTestBase`, `Views/SettingsView`, `Views/DialogView`, `Views/WindowExtensions` bereinigen | Offen | — |
| 55 | E2E | Negativprüfung „Checkbox und Abschnitt Automatisierung fehlen" in bestehende Settings-E2E-Methode einbauen (keine neue Methode) | Offen | — |
| 56 | E2E | Negativprüfung „Ribbon-Gruppe Autonome Aufgabe fehlt" in bestehende Aufgaben-E2E-Methode einbauen (keine neue Methode) | Offen | — |
| 57 | E2E | Regressionsnachweis: `E2E_AufgabePausieren`, `E2E_SessionLimitPause`, `E2E_ScmEvents`, Startlauf in `MainTest` unverändert grün | Offen | — |
| 58 | Doku | `docs/help/aufgaben/autonome-aufgaben/` (gesamter Ordner) löschen | Offen | — |
| 59 | Doku | `docs/help/aufgaben/` (index, beschreibung, business-rules, ablauf-technisch, ablauf-anwender, api, troubleshooting) bereinigen | Offen | — |
| 60 | Doku | `docs/help/einstellungen/` (beschreibung, ablauf-anwender, installation, datenmodell) bereinigen | Offen | — |
| 61 | Doku | `docs/help/index.md` (Absatz „Neu: Autonome Aufgaben") und `docs/help/e2e-test-infrastruktur/*` bereinigen | Offen | — |
| 62 | Doku | `docs/help/terminal/api.md`: Verweis auf `ProjektleiterAgentService` entfernen | Offen | — |
| 63 | Doku | `README.md` bereinigen (10 Treffer) | Offen | — |
| 64 | Doku | Release Notes: knapper Hinweis zum Datenverlust durch die Migration (Projektleiter-, Unteragent- und Skill-Daten) über `/update-release-notes` | Offen | — |
| 65 | Verifikation | Rest-Suche nach autonomen Begriffen in `src/` (ohne Migrationen, bin, obj), `docs/help/`, `README.md` ohne Treffer | Offen | — |
| 66 | Verifikation | `dotnet format Softwareschmiede.slnx` und `--verify-no-changes` | Offen | — |
| 67 | Verifikation | Vollständiger Build `dotnet build Softwareschmiede.slnx -c Debug` ohne Fehler und Warnungen | Offen | — |
| 68 | Verifikation | Tests regulär (`Category!=OsInterface`) grün, mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, synchron | Offen | — |
| 69 | Verifikation | Tests OS-Interface (`Category=OsInterface`, inkl. FlaUI-E2E) grün, mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, synchron; Fehlschläge zuerst durch erneute Ausführung prüfen | Offen | — |
| 70 | Verifikation | IntegrationTests regulär und OS-Interface grün, synchron | Offen | — |
