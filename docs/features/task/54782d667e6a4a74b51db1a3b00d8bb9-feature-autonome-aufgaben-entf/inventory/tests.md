# Tests: Ausgangszustand und Bestand (Autonome Aufgaben)

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): Build 2026-10-28 14:46 (+01:00); Testläufe 14:46 bis 15:06 (+01:00)
- Branch und Commit-ID: `task/54782d667e6a4a74b51db1a3b00d8bb9-feature-autonome-aufgaben-entf`, `8c98efad73e8958572604510b2c301df0046707b`
- Uncommittete Änderungen im getesteten Stand: nur das Verzeichnis `docs/features/task/54782d667e6a4a74b51db1a3b00d8bb9-feature-autonome-aufgaben-entf/` (untracked: `requirement.md`, `todo.md`, dieses Inventar). Keine Änderungen an Code, Tests oder Konfiguration. Ein zusätzlicher untracked Ordner `.claude/` war zu Beginn sichtbar und ist beim späteren `git status` nicht mehr gelistet.
- Testumgebung und Runtime-/SDK-Versionen: Windows 11 Pro 10.0.26300, .NET SDK `10.0.403` (`C:\Program Files\dotnet\sdk`), Konfiguration `Debug`, Projekt-Zieltarget `net10.0-windows10.0.17763.0`. Umgebungsvariable `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` gesetzt (gemäß `CLAUDE.md`, Sandbox-Regel für ConPTY-Tests).
- Ermittelte Testsuiten und Quellen der Testbefehle: Projektprojekte `src/Softwareschmiede.Tests` und `src/Softwareschmiede.IntegrationTests`; Befehle gemäß `CLAUDE.md` und `.github/workflows/staging-ci.yml` (Filter `Category!=OsInterface` und `Category=OsInterface`). Vor den Läufen `dotnet build Softwareschmiede.slnx -c Debug` (Exit 0, 0 Warnungen, 0 Fehler; die Build-Zeit war 1,27 s, also inkrementell ohne Neukompilierung, siehe [build-baseline.log](test-results/build-baseline.log)).

Hinweis zur Ausführung: Die OS-Interface-Läufe wurden nach Timeout des Foreground-Kommandos vom Harness in den Hintergrund verschoben. Der Lauf wurde danach nicht abgebrochen, sondern über einen Monitor bis zum Ende abgewartet (Exit-Code 0). Die Testläufe selbst waren nicht als Hintergrundaufruf beauftragt.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build Softwareschmiede.slnx -c Debug` | Repo-Root | 0 | – | – | – | [build-baseline.log](test-results/build-baseline.log) |
| Tests regulär | `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category!=OsInterface"` | Repo-Root | 0 | 2189 | 0 | 1 | [Log](test-results/baseline-regular-tests.log), [TRX](test-results/baseline-regular-tests.trx) |
| Tests OS-Interface | `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category=OsInterface"` | Repo-Root | 0 | 66 | 0 | 5 | [Log](test-results/baseline-osinterface-tests.log), [TRX](test-results/baseline-osinterface-tests.trx) |
| IntegrationTests regulär | `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category!=OsInterface"` | Repo-Root | 0 | 237 | 0 | 0 | [Log](test-results/baseline-regular-integration.log), [TRX](test-results/baseline-regular-integration.trx) |
| IntegrationTests OS-Interface | `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category=OsInterface"` | Repo-Root | 0 | 9 | 0 | 0 | [Log](test-results/baseline-osinterface-integration.log), [TRX](test-results/baseline-osinterface-integration.trx) |

Gesamtergebnis Ausgangslauf (Summe aller vier Läufe): 2 507 Tests, davon 2 501 bestanden, 6 übersprungen, 0 fehlgeschlagen. Ein Wiederholungslauf wurde nicht nötig, daher gibt es keine Abweichungen.

Hinweis zu den Dateigrößen: Das OS-Interface-Log der Tests-Suite ist ca. 32 MB groß (viele wiederholte SQL-`UPDATE`-Zeilen aus dem Testlauf); die TRX-Datei ca. 34 MB. Die Dateien wurden auf Geheimnisse geprüft (keine Treffer für Token-/Passwort-Muster).

### Nachgewiesene bestehende Testfehler

Keine. In keinem der fünf Läufe ist ein Test fehlgeschlagen. Eine Vorbelastung durch bestehende Fehler ist daher im Ausgangszustand nicht nachgewiesen.

### Übersprungene Tests (mit Grund)

| Test | Suite | Grund |
|------|-------|-------|
| `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` | Tests regulär | Plattformbedingte Überspringung (Nicht-Windows-Test auf Windows); kein Testfehler |
| `E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` | Tests OS-Interface | `SkipWennConPtyNichtVerfuegbar()` mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (ConPTY-Test, bewusst übersprungen) |
| `End2EndTest.ConPty_LifecycleUndReplay_E2E` | Tests OS-Interface | dito |
| `End2EndTest.ConPty_WeitereLebenszyklusSzenarien_E2E` | Tests OS-Interface | dito |
| `End2EndTest.ConPty_SessionReattach_KeineDoppelausgabe_E2E` | Tests OS-Interface | dito |
| `End2EndTest.TerminalText_LiveMarkCopyAndKeepSelection_E2E` | Tests OS-Interface | dito |

Die 14 ConPTY-E2E-Tests der Suite (siehe `CLAUDE.md`) werden in dieser Sandbox durch die Umgebungsvariable übersprungen. Ihre Ausführung in einer interaktiven Sitzung ist nicht Teil dieses Ausgangslaufs. Für die Entfernung des Features bedeutet das: Ein Vergleich ConPTY-E2E vor/nach der Änderung ist in dieser Umgebung nicht möglich und muss in einer interaktiven Sitzung ergänzt werden.

### Testlücken und Ausführungsprobleme

- Kein Build- oder Setup-Fehler im Ausgangslauf.
- Keine Testläufe unter Visual Studio oder in einer interaktiven Desktop-Sitzung.

## Testklassen mit Bezug zu Autonomen Aufgaben

Die folgenden Klassen gehören ausschließlich zum autonomen Feature (würden entfernt). Anzahl Testmethoden per Attribut-Suche; bei E2E-Dateien ohne Testmethoden ist das vermerkt.

### Ausschließlich autonom (`src/Softwareschmiede.Tests`)

| Testklasse / Datei | Anzahl Testmethoden | Was wird getestet |
|--------------------|---------------------|-------------------|
| `App/Services/AutonomAufgabeStartServiceTests` | 6 | Start-Ablauf, Feature-Flag-Fallback (DB vs. Options) |
| `App/ViewModels/AutonomAufgabeDetailViewModelTests` | 14 | Plan/Progress/Governance laden, Start/Stop/Resume, CLI-Status-Ereignisse, `StopCommand` |
| `App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModelTests` | 6 | Validierung Token-Budget, Laufzeit, Prompt; Abbrechen |
| `App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModelTests_BranchUndVorlagen` | 7 | Branch-Auswahl, Branch-Anlage, Vorlagen |
| `Application/Services/AutonomAufgabenInitialisierungsServiceTests` | 23 | Arbeitsverzeichnis, Klon, Projektbranch, `state.json`, `permissions.json`, Feature-Flag-Fallback |
| `Application/Services/ProjektleiterAgentServiceTests` | 7 | Start mit Initialprompt, Unteragent-Anlage, Ergebnis-Integration, Feature-Flag |
| `Application/Services/ProjektleiterAgentServiceTests_CliIntegration` | 8 | CLI-Start, Initialprompt und Resume über PseudoConsole, `ExplizitGestoppt` |
| `Application/Services/ProjektleiterAgentServiceTests_Fehlerfaelle` | 11 | Fehlerfälle (Validierung Scope/Branch/Pfade, Git-Fehler) |
| `Application/Services/SessionManagementServiceTests` | 10 | Budget-Pause, `state.json`-Update, Weitermachen-Prompt, Unterbruch-Erkennung |
| `Application/Services/UnteragentGovernanceServiceTests` | 10 | Berechtigungsprüfung Arbeitsbereich, Token-/Laufzeitlimit |
| `Application/Services/UnteragentGovernanceMonitoringServiceTests` | 5 | Periodische Prüfung aktiver Unteragenten |
| `Application/Services/UnteragentGitProvisioningServiceTests` | 3 | Verzeichnis-, Branch- und Klon-Anlage für Unteragenten |
| `Domain/Entities/AufgabeTests_IstAutonom` | 3 | `IstAutonom()`-Verhalten |
| `Domain/Entities/AutonomAufgabeKonfigurationTests_RessourcenLimits` | 2 | Value-Object-Mapping der Ressourcenlimits |
| `E2E/E2E_AutonomAufgabenFeatureFlagDisabled` | 1 (`[SkippableFact]`) | Fehlermeldung bei deaktiviertem Feature-Flag |
| `E2E/E2E_AutonomAufgabenAgentExecution` | 0 (keine Testmethoden gefunden) | Hilfs-/Basisklasse für autonome E2E-Szenarien (nicht im Detail untersucht) |
| `E2E/E2E_AutonomAufgabenInitialisierung` | 0 (keine Testmethoden gefunden) | Hilfs-/Basisklasse für Initialisierung (nicht im Detail untersucht) |
| `E2E/Views/AutonomAufgabeDetailView`, `E2E/Views/Dialogs/AutonomAufgabeInitialisierungsDialogView` | 0 | Page-Objects für autonome Views |

Hilfsklassen (ausschließlich autonom):
- `Helpers/AutonomAufgabenInitialisierungsServiceTestFactory`: Testdaten und Fakes für die Initialisierung.
- `Helpers/ProjektleiterAgentServiceTestDatenFactory`: Testdaten für Projektleiter-Tests.

### Gemeinsam genutzt, Anpassung nötig

| Testklasse / Datei | Autonomer Anteil |
|--------------------|------------------|
| `Application/Services/AufgabeRecoveryServiceTests` (12) | Autonome Fälle, Filter `AutonomKonfiguration == null` |
| `Application/Services/AufgabeServiceTests_Pause` (7) | Prüfung „autonome Aufgabe kann nicht pausiert werden“ |
| `Application/Services/KiPluginLimitServiceTests` (8) | Keine autonomen Verweise (bleibt) |
| `App/ViewModels/TaskDetailViewModelTests` (115) | 54 autonome Verweise (Ribbon-Gruppe, `IsAutonomAufgabe`, Feature-Flag) |
| `App/ViewModels/TaskDetailViewModelTests_Automatisierung` (12) | 70 autonome Verweise, ganze Datei autonom |
| `App/ViewModels/TaskDetailViewModelTests_Pause` (9) | 3 autonome Verweise |
| `App/ViewModels/TaskDetailViewModelTests_PluginAktivierung` (2), `_CliRawExport` (5), `_CliReplayExport` (6), `_ZeitgesteuerterPrompt` (8) | Einzelne autonome Verweise |
| `App/ViewModels/TaskDetailViewModelTestsBase` | Basisklasse mit 6 autonomen Verweisen |
| `Helpers/TaskDetailViewModelTestFactory` | Factory mit 23 autonomen Verweisen |
| `App/ViewModels/SettingsViewModelTests` (35) | 16 autonome Verweise (Feature-Flag) |
| `App/ViewModels/SettingsViewModelTests_IdePlugin` (9) | 1 Verweis |
| `App/ViewModels/MainWindowViewModelUpdateTestBase` | 1 Verweis |
| `E2E/E2E_SettingsFeatureFlags` | 9 Verweise (Checkbox „Autonome Aufgaben aktivieren“); enthält keine Testmethode |
| `E2E/E2E_ViewPattern`, `E2E/MainTest` (12), `E2E/WpfTestBase` (4), `E2E/Views/SettingsView`, `E2E/Views/DialogView`, `E2E/Views/WindowExtensions` | Verweise auf autonome Views, Dialoge oder Feature-Flag |
| `IntegrationTests/Services/EntwicklungsprozessServiceTests` (11) | 7 autonome Verweise |
| `IntegrationTests/Scripts/StartPs1IntegrationTests` (9) | 1 Verweis (vermutlich `appsettings`/`AutonomAufgaben`-Konfiguration, zu prüfen) |

Die übrigen Tests (z. B. `ScmEvent*`, `PromptZeitVersandServiceTests`, `ScmEventOrchestratorTests`, `CliOutputProtokollWriterTests`, `DashboardViewModelTests`, `MainWindowViewModelTests`, `AufgabeLaufAktivitaetTests`, `CliUpdateSafetyServiceTests`, `ServiceIntegration/RecoveryServiceIntegrationTests`, `E2E_ScmEvents`, `E2E_AufgabePausieren`, `E2E_SessionLimitPause`) enthalten keine autonomen Verweise und bleiben als Regressionsbasis erhalten.

## Hinweise für die spätere Vergleichsbasis

- Die gemeinsamen Tests zu Pause, Recovery, Session-Limit und ereignisgetriebener Bearbeitung (`ScmEvent*`, `PromptZeitVersandServiceTests`, `KiPluginLimitServiceTests`, `AufgabeRecoveryServiceTests` mit regulären Fällen) müssen nach der Entfernung weiterhin grün sein. Dieser Ausgangslauf ist dafür die Referenz (0 Fehler).
- Die 5 übersprungenen ConPTY-Tests sind in dieser Sandbox nicht ausführbar; ein Vergleich mit einem interaktiven Lauf ist offen.
