# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-11-11, ca. 18:47–19:07 Uhr CET (UTC+01:00)
- **Branch und Commit-ID:** `task/issue-348-56a2145eee9e4c28a79a3f2ef54cf331-funktionstrennung` @ `530f4682dcf45dddf3f252eb2a56d018bb72d1a5` („Backmerge from main to staging (#346)")
- **Uncommittete Änderungen im getesteten Stand:** Keine geänderten Dateien; einzige Abweichung ist das untracked Verzeichnis `docs/features/task/issue-348-56a2145eee9e4c28a79a3f2ef54cf331-funktionstrennung/` (enthielt zum Testzeitpunkt nur `requirement.md`; die TRX-Reports dieses Laufs liegen dort ebenfalls unter `inventory/test-results/`). Kein Produktiv- oder Testcode betroffen.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows (Git Bash/MSYS), .NET SDK `10.0.403`, Zielframeworks `net10.0-windows10.0.17763.0` (`Softwareschmiede.Tests`), `net10.0` (`Softwareschmiede.IntegrationTests`), Test-Framework xUnit 2.9.3, Umgebungsvariable `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` gesetzt (Projektvorgabe für diese Sandbox, siehe `CLAUDE.md` Abschnitt Testing). Vorab wurde `dotnet build Softwareschmiede.slnx -c Debug` erfolgreich ausgeführt (0 Warnungen, 0 Fehler, ~23 s).
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Zwei Testprojekte (`src/Softwareschmiede.Tests`, `src/Softwareschmiede.IntegrationTests`), jeweils in zwei Lanes: reguläre Tests (`--filter "Category!=OsInterface"`) und umgebungsabhängige OS-Interface-/E2E-Tests (`--filter "Category=OsInterface"`). Befehle übernommen aus `CLAUDE.md` und den CI-Workflows `.github/workflows/pr-staging-ci.yml` bzw. `staging-ci.yml` (dort mit `--no-build`; hier bewusst mit vorangegangenem Vollbuild ausgeführt, Projektvorgabe).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|---------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Regulär `Softwareschmiede.Tests` | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category!=OsInterface" --logger "trx;LogFileName=test-results-regular-tests.trx" --results-directory docs/features/task/.../inventory/test-results/regular-tests` | Repo-Wurzel | 0 | 2245 | 0 | 1 | [test-results-regular-tests.trx](test-results/regular-tests/test-results-regular-tests.trx) (Dauer ~3 m 10 s) |
| Regulär `Softwareschmiede.IntegrationTests` | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category!=OsInterface" --logger "trx;LogFileName=test-results-regular-integration.trx" --results-directory docs/features/task/.../inventory/test-results/regular-integration` | Repo-Wurzel | 0 | 264 | 0 | 0 | [test-results-regular-integration.trx](test-results/regular-integration/test-results-regular-integration.trx) (Dauer ~30 s) |
| OsInterface `Softwareschmiede.IntegrationTests` | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category=OsInterface" --logger "trx;LogFileName=test-results-os-interface-integration.trx" --results-directory docs/features/task/.../inventory/test-results/os-interface-integration` | Repo-Wurzel | 0 | 9 | 0 | 0 | [test-results-os-interface-integration.trx](test-results/os-interface-integration/test-results-os-interface-integration.trx) (Dauer ~4 s) |
| OsInterface `Softwareschmiede.Tests` (E2E/FlaUI u. a.) | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category=OsInterface" --logger "trx;LogFileName=test-results-os-interface-tests.trx" --results-directory docs/features/task/.../inventory/test-results/os-interface-tests` | Repo-Wurzel | 0 | 65 | 0 | 5 | [test-results-os-interface-tests.trx](test-results/os-interface-tests/test-results-os-interface-tests.trx) (Dauer ~15 m 33 s) |

### Nachgewiesene bestehende Testfehler

Es wurden **keine** fehlgeschlagenen Tests nachgewiesen. Alle vier Lanes liefen fehlerfrei durch (jeweils Exit-Code 0, 0 fehlgeschlagene Tests im TRX-Report).

### Testlücken und Ausführungsprobleme

- **1 übersprungener Test in der regulären Lane** (`Softwareschmiede.Tests`): `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — Skip-Begründung im TRX: „Test gilt nur für Nicht-Windows-Betriebssysteme, siehe ArbeitsverzeichnisOeffnenService.ResolveOeffnenBefehl." Plattformbedingter, vom Test selbst gesetzter Skip; auf dieser Windows-Umgebung prinzipiell nicht ausführbar.
- **5 übersprungene Tests in der OsInterface-Lane** (`Softwareschmiede.Tests`), alle mit derselben Skip-Begründung „Umgebungsvariable SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 gesetzt (siehe CLAUDE.md, Abschnitt Testing) — bestätigte Sandbox-Limitation dieses Ausführungskontexts, keine automatische Erkennung.":
  - `Softwareschmiede.Tests.E2E.End2EndTest.ConPty_LifecycleUndReplay_E2E`
  - `Softwareschmiede.Tests.E2E.End2EndTest.ConPty_WeitereLebenszyklusSzenarien_E2E`
  - `Softwareschmiede.Tests.E2E.End2EndTest.ConPty_SessionReattach_KeineDoppelausgabe_E2E`
  - `Softwareschmiede.Tests.E2E.End2EndTest.TerminalText_LiveMarkCopyAndKeepSelection_E2E`
  - `Softwareschmiede.Tests.E2E.E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung`

  Grund: bestätigte Sandbox-Limitation (ConPTY-Kindprozess wird in diesem Kontext nicht an die Pseudokonsole isoliert); dokumentierte Projektvorgabe, kein Code-Defekt. Die Tests sind für den Ausgangszustand als „nicht ausgeführt" zu werten — weder Erfolg noch nachgewiesener Fehler.
- Keine Build-, Setup- oder Infrastrukturfehler aufgetreten. Keine abgebrochenen Läufe.

## Testklassen

Bestehende Testklassen im betroffenen fachlichen Bereich (SCM-Automatisierung / Einstellungen):

### `Softwareschmiede.Tests.Application.Services.AppEinstellungServiceTests_ScmMonitoring`
Datei: `src/Softwareschmiede.Tests/Application/Services/AppEinstellungServiceTests_ScmMonitoring.cs`

- `GetScmMonitoringEnabledAsync_WithoutEntry_DefaultsToTrue` — Standard `true` ohne gespeicherten Schalter.
- `GetScmMonitoringEnabledAsync_PersistedValue_IsHonored` — persistierter Schalter gilt in beide Richtungen.
- `GetScmTriggerKeywordAsync_WithoutEntry_DefaultsToSoftwareschmiede` — Standard-Trigger `$Softwareschmiede`.
- `GetScmTriggerKeywordAsync_InvalidStoredValue_FallsBackToDefault` (Theory: `""`, `"   "`) — leere Werte fallen auf Standard zurück.
- `GetScmTriggerKeywordAsync_StoredValue_ReturnsTrimmed` — gespeicherter Wert wird getrimmt.
- `GetScmPromptAppendicesAsync_WithoutEntries_ReturnsEmpty` — gebündelte Anhang-Abfrage leer ohne Einträge.
- `GetScmPromptAppendicesAsync_PersistedValues_AreReturned` — nur gespeicherte Schlüssel erscheinen.

### `Softwareschmiede.Tests.Application.Services.AppEinstellungServiceTests_Backmerge`
Datei: `src/Softwareschmiede.Tests/Application/Services/AppEinstellungServiceTests_Backmerge.cs`

- `GetScmBackmergeModusAsync_WithoutEntry_DefaultsToDeaktiviert`
- `GetScmBackmergeModusAsync_InvalidStoredValue_FallsBackToDeaktiviert` (Theory: `"kein-wert"`, `"99"`)
- `SetScmBackmergeModusAsync_PersistedValue_IsHonored` (Theory: `Global`, `Projektbezogen`)
- `SetScmBackmergeModusAsync_UndefinedValue_Throws`
- `BackmergeProjektFlag_WithoutEntry_DefaultsFalseAndIsPerProject`

### `Softwareschmiede.Tests.Application.Services.AppEinstellungServiceTests_KontoModus`
Datei: `src/Softwareschmiede.Tests/Application/Services/AppEinstellungServiceTests_KontoModus.cs`

- `GetScmKontoModusAsync_WithoutEntry_DefaultsToBenutzerkonto`
- `GetScmKontoModusAsync_InvalidStoredValue_FallsBackToBenutzerkonto` (Theory)
- `SetScmKontoModusAsync_PersistedValue_IsHonored` (Theory: beide Modi)
- `SetScmKontoModusAsync_UndefinedValue_Throws`

### `Softwareschmiede.Tests.Application.Services.ScmBackmergeServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmBackmergeServiceTests.cs`

- `Evaluate_ModusDeaktiviert_KeinMerge`, `Evaluate_ModusGlobal_BackmergeWirdGemergt`, `Evaluate_ModusProjektbezogenOhneFlag_KeinMerge`, `Evaluate_ModusProjektbezogenMitFlag_WirdGemergt`
- `Evaluate_KeinBackmergeMuster_KeinMerge`, `Evaluate_FalschesZiel_KeinMerge`, `Evaluate_IssueReferenz_WirdIgnoriert`
- `Evaluate_GeschlossenerPr_KeinMerge` (Theory), `Evaluate_NichtMergebar_WartetUndPrueftErneut` (Theory)
- `Evaluate_OhneSchreibrecht_KeinMerge`, `Evaluate_InaktivesRepository_KeinMerge`, `Evaluate_OhneBasisbranch_KeinMerge`
- `Evaluate_MergeFehlgeschlagen_BackoffWiederholung`, `Evaluate_PermanenterProviderfehler_EndgueltigGesperrt`, `Evaluate_CheckpointInWartezeit_KeineErneuteAbfrage`

### `Softwareschmiede.Tests.App.ViewModels.SettingsViewModelTests` (SCM-relevante Tests)
Datei: `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`

- `LadenCommand_LoadsScmMonitoringDefaults`, `SpeichernCommand_PersistsScmMonitoringSettings`, `SpeichernCommand_RejectsEmptyScmTriggerKeyword` (Theory)
- `LadenCommand_LoadsBackmergeModusDefaults`, `SpeichernCommand_PersistsBackmergeModus`
- `LadenCommand_LoadsScmPromptAppendixDefaults`, `SpeichernCommand_PersistsAndReloadsScmPromptAppendices`, `SpeichernCommand_BlankScmPromptAppendix_StoresNoValue` (Theory)
- `LadenCommand_LoadsKontoModusDefaults`, `SpeichernCommand_PersistsKontoModus`, `SpeichernCommand_RejectsMissingKontoModus`
- `SpeichernCommand_SeparatesKontoWithoutAgentToken_ShowsFehlerMeldung`, `SpeichernCommand_SeparatesKonto_FormValueSatisfiesAgentTokenRequirement`
- (`SettingsViewModelTests_IdePlugin.cs` enthält ergänzend IDE-Plugin-Tests; nicht SCM-betroffen.)

### `Softwareschmiede.IntegrationTests.Services.ScmTaskCompletionMonitoringServiceTests`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmTaskCompletionMonitoringServiceTests.cs`

- `Complete_ReportedPullRequestNumber_LinksReferenceToTask` — im Abschluss gemeldeter PR wird als `PullRequestReferenz` verknüpft.
- `Monitor_MergedPullRequest_StopsCliAndCompletesTaskAfterWait` — gemergter PR startet Countdown; nach Wartezeit CLI-Stopp + `Beendet`.
- `Monitor_MergedPullRequest_CancelsScheduledPromptOnComplete` — geplanter Prompt wird beim Abschluss zurückgezogen.
- `Monitor_ReopenedPullRequest_ResetsCountdown` — wieder geöffneter PR setzt `ScmAbschlussErkanntUtc` zurück.
- `Monitor_ClosedIssue_WithoutPullRequest_CompletesTaskAfterWait` — Issue-Pfad ohne PR-Verknüpfung.
- `CompletionService_PassesActiveKonto_ToRepositoryContext` — Issue-Prüfung läuft im aktiven Konto-Modus (SeparatesKonto → Agent-Konto).
- `Monitor_DisabledAndManualTask_SkipsObservation` — deaktivierter Schalter und manuelle Aufgaben (ohne `ScmIncomingEvent`) bleiben unberührt.
- `Monitor_OpenPullRequestOrPendingEvent_KeepTaskRunning` — offener PR bzw. laufendes Ereignis blockiert Abschluss.

### `Softwareschmiede.IntegrationTests.Services.ScmEventMonitoringServiceTests_Backmerge`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_Backmerge.cs`

- `Monitor_BackmergePullRequest_IsMergedAutomatically` — global aktivierter Backmerge wird per Admin-Merge bestätigt.
- `Monitor_BackmergeDisabled_NoStatusQueryNoMerge` — deaktivierter Modus: keine Statusabfrage, kein Merge.
- `Monitor_ProjectScopedModeWithFlag_Merges` — projektbezogener Modus mit Projekt-Freigabe mergt.
- `Monitor_NonBackmergePullRequest_IsNotMerged` — Feature-PR (feature → staging) wird nicht gemergt.
- `Monitor_UnmergeableBackmerge_RetriesNextPoll` — nicht mergebarer Backmerge wird im nächsten Intervall erneut geprüft.

### `Softwareschmiede.IntegrationTests.Services.ScmEventMonitoringServiceTests_Settings`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_Settings.cs`

- `Monitor_DisabledMonitoring_SkipsObservationAndDispatch_UntilReEnabled` — deaktivierter Schalter unterbindet Beobachtung, Enumeration und Zustellung (inkl. Reaktivierung).
- `ExchangeService_DisabledMonitoring_SkipsPendingAttempts` — Antwortkanal hält ebenfalls an.
- `Monitor_CustomTriggerKeyword_AcceptsNewAndRejectsOld` — konfiguriertes Keyword ersetzt Standard.
- `ManualRetry_DispatchesWhileMonitoringDisabled` — manuelles „Erneut prüfen" bleibt bei deaktivierter Überwachung funktionsfähig.

### `Softwareschmiede.IntegrationTests.Services.ScmEventMonitoringServiceTests_Notifications`
Datei: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_Notifications.cs`

- `Monitor_SeparatesKonto_MentionNotification_EntersInboxViaResolver` — im Modus `SeparatesKonto` ersetzt die Notifications-Inbox die repository-weite Enumeration (`EnumerateReferencesAsync` wird nie aufgerufen — relevant für die Backmerge-Kopplung).
- `Monitor_SeparatesKonto_ForeignNotification_DiscardedWithDiagnostic` — Fremd-Notification wird verworfen.
- Weitere Tests im selben Stil zu Verwerfung/Diagnose und Inbox-Watermark (Datei enthält 296 Zeilen; hier nur die für die Backmerge-Kopplung relevanten aufgeführt).

### `Softwareschmiede.Tests.E2E.End2EndTest` (konsolidierter FlaUI-Runner)
Dateien: `src/Softwareschmiede.Tests/E2E/MainTest.cs`, `src/Softwareschmiede.Tests/E2E/E2E_SettingsFeatureFlags.cs`, `src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs`

- `General_BasisUndEinstellungen_E2E` — enthält u. a. `Einstellungen_AllgemeinEnthaeltNurVorhandeneAbschnitte_E2E`: prüft heute explizit, dass im „Allgemein"-Tab die SCM-Ereignis-Checkbox `IsScmMonitoringEnabled` existiert und **kein** Abschnitt/Control namens „Automatisierung" vorhanden ist (`Assert.Null(...ByName("Automatisierung"))`). Dieser Test wird durch die Anforderung (neues Register „Automatisierung") voraussichtlich angepasst werden müssen.
- `E2E_ScmEvents.cs` — großes konsolidiertes SCM-E2E-Szenario (Konto-Modus-Umschaltung über die Einstellungs-UI, `GetKontoModus`/`SetKontoModus`/`WaitForKontoModus` im Page Object `E2E/Views/SettingsView.cs`, Selektor `ByName("Konto-Modus")`).

## Hilfsmethoden

### `ScmDispatchFixture` (`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`, per `<Compile Include>` auch in `Softwareschmiede.IntegrationTests` verlinkt)
- Baut den vollständigen DI-Container mit gemocktem `IGitPlugin`/`IScmEventSource` (`fixture.Git`), In-Memory-/Temp-SQLite-DB und Prozessfakes auf; stellt `InitializeAsync`, `AddAsync(configured)`, `DispatchAsync`, `EventAsync`, `AddNotification`, `SetKontoModus`, `Sessions`, `Prompts`, `Responses`, `Repository` (ScmEventRepository) bereit. Grundlage aller SCM-Monitoring-/Dispatch-Integrationstests.

### `TestTimeProvider` (lokal in `ScmTaskCompletionMonitoringServiceTests.cs`)
- Deterministische `TimeProvider`-Ableitung mit `Advance(TimeSpan)` für die Abschluss-Wartezeit.

### `TestDbContextFactory` (`src/Softwareschmiede.Tests/Helpers/`)
- Erzeugt In-Memory-`SoftwareschmiededDbContext` für `AppEinstellungService`-Unit-Tests.

### Hilfsmethoden in `ScmTaskCompletionMonitoringServiceTests.cs`
- `SetupPullRequestStatus`, `ProcessEventWithReportAsync` (simuliert accepted/complete-Exchange-Dateien), `SetWaitAsync`, `AddIssueReferenceAsync`, `AddManualTaskWithPullRequestAsync`, `TaskAsync`.

### Hilfsmethoden in `ScmEventMonitoringServiceTests_Backmerge.cs` / `_Settings.cs`
- `AddRepositoryWithBasisbranchAsync` (legt `GitRepository` mit `DefaultSourceBranchName = "staging"` an), `SetModusAsync` (`SetScmBackmergeModusAsync`), `SetProjektFlagAsync` (`SetBackmergeProjektAktivAsync`), `ConfigureBackmerge`/`ConfigureStatus`/`ConfigureMerge` (Mock-Setup für Enumeration, Default-Branch, PR-Status und Merge), `Monitor` (Service-Auflösung), `AddTaskWithIssueAsync`, `ConfigureObservations`.

### E2E-Page-Object `Softwareschmiede.Tests.E2E.Views.SettingsView` (`src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs`)
- `SwitchTab(tabName)`, `GetActiveTab`, `IsOnPluginsTab`, `SaveSettings`, `DiscardChanges`
- `GetKontoModus`/`SetKontoModus`/`WaitForKontoModus` (Selektor `ByName("Konto-Modus")`, patternbasierte ComboBox-Auswahl)
- `GetUpdateMode`/`SetUpdateMode`/`GetUpdatePfad`, `GetDesignMode`/`SetDesignMode`, `SetPluginSecretField` (PasswordBox-Tastatureingabe), `ConfigureLocalDirectoryPlugin`, `SelectDefaultKiPlugin`, `IsPluginEnabled`/`SetPluginEnabled`, `IsIdePluginEnabled`/`SetIdePluginEnabled`, `MoveIdePluginUp`, `IsFirstIdePlugin`, `OpenKonsolenTestDialog`, `OpenCliHelp`.
