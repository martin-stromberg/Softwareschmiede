# Tests: GitHub-Notifications als Eingangskanal

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-31, ca. 13:29–13:47 +0100 (lokale Zeit)
- Branch und Commit-ID: `task/769daac011ce412dbde61b999b53ca1b-notifications-als-eingangskana`, Commit `b0de86d64849ab2c07faeb2b13218920cdb26165` (== `origin/staging`/`staging`)
- Uncommittete Änderungen im getesteten Stand: nur das untracked Feature-Verzeichnis `docs/features/task/769daac011ce412dbde61b999b53ca1b-notifications-als-eingangskana/` (Anforderungs-/Arbeitsdokumente inkl. dieser Bestandsaufnahme). Keine Änderungen an Quell- oder Testcode.
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK `10.0.403`, Testlaufzeit .NET 10.0.14 (xUnit.net VSTest Adapter v3.1.5), xUnit + Moq + FluentAssertions; für alle Läufe `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` gesetzt (Sandbox-Regel aus `CLAUDE.md`); `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS` **nicht** gesetzt — die OsInterface-Lane fuhr echte WPF-Fenster (FlaUI) mit echter App-Instanz.
- Ermittelte Testsuiten und Quellen der Testbefehle: Zwei Testprojekte (`src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`, `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`), je zwei Lanes `Category!=OsInterface` (regulär) und `Category=OsInterface` (E2E/ConPTY/Clipboard/echte Prozessstarts). Quellen: `CLAUDE.md` (stabile Lane + OsInterface getrennt), `.github/workflows/pr-staging-ci.yml` und `staging-ci.yml` (CI-Kommandos). Build vorab: `dotnet build Softwareschmiede.slnx -c Debug` — erfolgreich, 0 Warnungen, 0 Fehler (17,7 s).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build Softwareschmiede.slnx -c Debug` | Repo-Wurzel | 0 | — | — | — | Konsolenausgabe (0 Warnungen, 0 Fehler) |
| Tests regulär | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category!=OsInterface"` | Repo-Wurzel | 0 | 2049 | 0 | 1 | [regular-tests.trx](test-results/regular-tests.trx) |
| Integration regulär | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category!=OsInterface"` | Repo-Wurzel | 0 | 243 | 0 | 0 | [regular-integration.trx](test-results/regular-integration.trx) |
| Tests OsInterface | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category=OsInterface"` | Repo-Wurzel | 1 | 62 | 3 | 5 | [osinterface-tests.trx](test-results/osinterface-tests.trx) |
| Integration OsInterface | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category=OsInterface"` | Repo-Wurzel | 0 | 9 | 0 | 0 | [osinterface-integration.trx](test-results/osinterface-integration.trx) |

### Nachgewiesene bestehende Testfehler

Alle drei Fehlschläge liegen in der OsInterface-Lane (WPF-E2E/STA-Umgebungstests); die regulären Lanes sind fehlerfrei.

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|-------------------|----------------------------|-------------------|
| `Softwareschmiede.Tests.E2E.ProjectDetailE2ETests.ProjektDetailSzenarien` | `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs` | `System.TimeoutException : Nach Klick auf 'Zurück' wurde innerhalb von 15s keine neue Ansicht sichtbar.` — `TaskDetailView.GoBack()` (TaskDetailView.cs:130) ← `AufgabenInProjektdetail_NeuAnlegenUndFiltern_E2E` (ProjectDetailE2ETests.cs:201) ← `ProjektDetailSzenarien` (Z. 46). FlaUI-Navigation-Timeout; die App lief (vorherige Teilschritte desselben Tests liefen), kein Startabsturz | OsInterface-Lauf, [osinterface-tests.trx](test-results/osinterface-tests.trx) |
| `Softwareschmiede.Tests.E2E.ScmEventsE2ETests.ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` | `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs` | `System.TimeoutException : SCM Aufgabe 2: erwartet Verarbeitet, tatsächlich Wartend: Eingabebereitschaft unbekannt. Für die angezeigte CLI-Sitzung bestätigen.` — `ScmE2EFixture.WaitEventCoreAsync` (ScmE2EFixture.cs:146) ← `MissingVariantsAsync` (E2E_ScmEvents.cs:507) ← Testmethode (Z. 246). Alle dokumentierten Teilszenarien (E1–E6 inkl. Empfang, Replies, Restart, Reconcile) liefen zuvor grün; der Timeout tritt in der letzten Phase beim Warten auf Ereignisstatus `Verarbeitet` auf — das Ereignis blieb `Wartend`, weil die CLI-Sitzung eine Bereitschaftsbestätigung erforderte. Direkt der von dieser Anforderung betroffene SCM-Ereignispfad | OsInterface-Lauf, [osinterface-tests.trx](test-results/osinterface-tests.trx); Fixture-Nachweise: `C:\Users\Martin\AppData\Local\Temp\softwareschmiede_scm_e2e_92ac6eb8ac214d2da3d2ff78f4f53ea9` |
| `Softwareschmiede.Tests.App.Controls.TerminalControlTests.ReadClipboardAndInsertAsync_CallsMarkInputActivity` | `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.ClipboardPaste.cs` (Z. 238) | `Expected GetLastInputActivity(session) to have a value because ReadClipboardAndInsertAsync muss nach erfolgreichem Schreiben MarkInputActivity() aufrufen, but found <null>` — STA-Clipboard-Test über `WpfUnitTestHelpers.RunOnSta`; umgebungssensitiver Clipboard-Zugriff | OsInterface-Lauf, [osinterface-tests.trx](test-results/osinterface-tests.trx) |

### Testlücken und Ausführungsprobleme

- **Übersprungen in der regulären Lane (1):** `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — beabsichtigter Plattform-Skip (Test gilt nur für Nicht-Windows-Systeme).
- **Übersprungen in der OsInterface-Lane (5, alle via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, bestätigte Sandbox-Limitation lt. `CLAUDE.md`):**
  - `E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung`
  - `End2EndTest.ConPty_LifecycleUndReplay_E2E`
  - `End2EndTest.TerminalText_LiveMarkCopyAndKeepSelection_E2E`
  - `End2EndTest.ConPty_WeitereLebenszyklusSzenarien_E2E`
  - `End2EndTest.ConPty_SessionReattach_KeineDoppelausgabe_E2E`
- **Keine Notifications-Tests:** Es existiert keinerlei Testabdeckung für eine GitHub-Notifications-Inbox oder einen Kontomodus — der Mechanismus ist noch nicht implementiert.
- Keine Build-, Setup- oder Infrastrukturfehler; alle vier Läufe wurden vollständig ausgeführt.

## Testklassen

### `ScmEventValidationServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ScmEventValidationServiceTests.cs`
- `ActualAuthorOrEditorMustBeOwner` — Owner-Regel: nur tatsächlicher Bearbeiter (Autor/Editor = Viewer = Owner) + Trigger legitimieren
- `TriggerMustBeFreeStanding` — Zitatzeichen-flankierte Vorkommen lösen nicht aus
- `ConfiguredTriggerKeyword_ReplacesDefaultAndStaysFreeStanding` — konfiguriertes Keyword ersetzt Standard
- `MissingTriggerKeyword_FallsBackToDefault` / `TriggerKeyword_IsTrimmedBeforeMatching` — Fallback/Normalisierung
- `LoginCannotOverrideDifferentIds` — ID schlägt Login
- `MetadataDoesNotCreateAnotherVersionAndContentNamespacesDo` — `EventKey`/`Revision`-Semantik

### `AppEinstellungServiceTests_ScmMonitoring` / `AppEinstellungServiceTests_Backmerge`
Dateien: `src/Softwareschmiede.Tests/Application/Services/AppEinstellungServiceTests_ScmMonitoring.cs`, `AppEinstellungServiceTests_Backmerge.cs`
- Defaults (`GetScmMonitoringEnabledAsync` → `true`, `GetScmTriggerKeywordAsync` → `$Softwareschmiede`, `GetScmBackmergeModusAsync` → `Deaktiviert`)
- Persistierte Werte werden gelesen; ungültige Werte fallen auf Defaults zurück
- `SetScmBackmergeModusAsync_UndefinedValue_Throws` — Validierungsmuster für Enum-Setter
- `BackmergeProjektFlag_WithoutEntry_DefaultsFalseAndIsPerProject` — projektbezogener Key
- `GetScmPromptAppendicesAsync_*` — gebündelte Mehrfach-Lesung

### `GitHubEventSourceTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs`

~30 Methoden gegen gemockten `IGitHubEventApiClient`-Transport: `RepositoryContext_UsesStableIdsAndPermissions`, `Comments_ReadAllPages_AndBodyEditorFromSameVersion`, `FailedSecondPage_ThrowsWithoutGraphOrPartialBatch`, `Pagination_RejectsForeignTargets`, `Description_VersionsDistinguishTextAndEdits_NotMetadata`, `MissingEditor_RemainsMissing_AndSameVersionCanResolveLater`, `Watermark_ExcludesNewerVersion`, `Reviews_PaginateAndExcludePending`, `PullRequestCommentKinds_UseSeparateNamespaces`, `OwnMarkedComment_IsExcluded_ButForeignMarkerIsNotTrusted`, `EventKey_SeparatesEveryOriginDimension`, `Failures_AreTypedAndSanitized`, `AccessFailures_ExplainTokenSettingsAndRecovery`, `GraphErrors_RejectPartialData`, `ExistingReaction_IsReconciledAcrossAllPages`, `CommentTimeout_ReportsUncertain_AndReconcilesMarker`, `ReplyPermissionFailure_IsNotUncertain`, `ChangedViewer_BlocksResponseBeforePost`, `PullRequestResponse_PreservesOriginAndUsesJsonBody`, `Enumeration_ReturnsOpenReferencesAcrossPages`, `Enumeration_SinceWindow_AddsOnlyRecentlyUpdatedClosedReferences`, `Enumeration_Failure_RemainsTypedProviderError`, `GhTransport_UsesArgumentListAndJsonFile_NotBodyOrTokenArguments`, `ReadIssue_ReturnsCreationFields`, `ReadIssue_PullRequestResponse_ReturnsNull`, `ReadPullRequest_ReturnsCheckoutFields`, `ReadIssue_NotFound_RemainsTypedProviderError`.

### `GitHubPluginTests` (Auszug)
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`
- `GetAgentEnvironmentAsync_WithToken_ProvidesGhAndGitAuth` — `GH_TOKEN` + `http.extraheader` + `GIT_TERMINAL_PROMPT=0` aus Credential Store
- `GetAgentEnvironmentAsync_WithoutToken_OnlyDisablesGitPrompt` — ohne Token nur `GIT_TERMINAL_PROMPT=0`
- Weitere ~100 Plugin-Methodentests (Clone/Push/PR/Settings) über gemockten `ICliRunner`

### `ScmEventMonitoringServiceTests_*` (Integration, reale SQLite + `ScmDispatchFixture`)
Dateien: `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_{Settings,RepoWide,TaskAssignment,AmbiguityRecovery,AutoStartDelivery,Backmerge}.cs`
- `_Settings`: `Monitor_DisabledMonitoring_SkipsObservationAndDispatch_UntilReEnabled`, `ExchangeService_DisabledMonitoring_SkipsPendingAttempts`, `Monitor_CustomTriggerKeyword_AcceptsNewAndRejectsOld`, `ManualRetry_DispatchesWhileMonitoringDisabled`
- `_RepoWide`: Enumeration aktiver Repos ohne/mit Aufgabe, Dedup über Polls, inaktive/Nicht-GitHub-Repos werden nicht enumeriert, Enumerationsfehler-Backoff blockiert verknüpfte Referenzen nicht
- `_TaskAssignment`: qualifizierter Kommentar legt Aufgabe + Inbox an, PR-Review-Quelle, Einzelaufgabe bei wiederholten Polls, unqualifiziertes Ereignis bleibt unzugeordnet, verschwundene Referenz/Auflösungsfehler, Wiederverwendung
- `_AmbiguityRecovery`: Alias-Wechsel nach Restart akzeptiert unveränderten Kommentar genau einmal
- `_AutoStartDelivery`: CLI-Start/Zustellung je Konfigurationslage, Queue-Auswahl, blockierte Aufgaben bleiben wartend
- `_Backmerge`: Backmerge-PR wird modusabhängig gemergt bzw. nicht

### `ScmEventDispatchTests*` / `ScmEventCliExchangeAccessTests` / `ScmTaskCompletionMonitoringServiceTests` / `ScmEventPersistenceTests` (Integration)
Dateien: `src/Softwareschmiede.IntegrationTests/Services/`
- Dispatch-Reihenfolge, >100 wartende Ereignisse, Alias-Zuordnung, Retry/Hostaktionen, Agenten-Aktionen (`GetAgentEnvironmentAsync`-Mock), Cancel, Devin-Profil, Prompt-Appendix, Exchange-Probe/Restricted-Process, Abschlussüberwachung, Persistenz.

### `SettingsViewModelTests` (Auszug SCM)
Datei: `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`
- `LadenCommand_LoadsScmMonitoringDefaults`, `SpeichernCommand_PersistsScmMonitoringSettings` (u. a. Backmerge-Modus Z. 644–704), Plugin-Einstellungsgruppen/-werte, Pflichtfeld-Validierung, Update-Einstellungen.

### `ScmEventsE2ETests` / `End2EndTest` (E2E, OsInterface)
Dateien: `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs`, `E2E_ScmRegressionSimulator.cs`
- `ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` — konsolidierter E2E über die echte SCM-Pipeline (Empfang→Queue→Dispatch→Replies→Restart→Reconcile); im Ausgangslauf fehlgeschlagen (s. oben)
- `Simulator_PluginwechselProjektstandardAufgabenstartUndNavigation_E2E` — Simulator-Szenario

## Hilfsmethoden

### `ScmDispatchFixture`
Datei: `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`
- Baut echten `ServiceProvider` mit SQLite-DB, `ScmEventMonitoringService`, `ScmEventStore`, `ScmEventOrchestrator`, `ScmEventTaskResolver`, `ScmBackmergeService`, `KiAusfuehrungsService` etc. (Z. 120–153)
- `Mock<IGitPlugin>` mit `As<IScmEventSource>`/`As<IScmEventResponder>`/`As<IScmAgentAccess>`-Defaults (Z. 60–76) — direkter Ansatzpunkt für einen gemockten Notification-Feed
- `AddAsync` (Z. 162–181): Projekt+Repo+Aufgabe+akzeptiertes Ereignis in einem Schritt
- `DispatchAsync`, `EventAsync`, `CompleteProbeAsync` (Dateitool-Kanalprobe), `SetPrompt`, `ScmKiProfile` (`Simulator`/`Devin`)

### `ScmE2EFixture`
Datei: `src/Softwareschmiede.Tests/E2E/ScmE2EFixture.cs`
- Dateibasiertes SCM-E2E-Fixture: `AddText` (Z. 99), `WaitEventAsync`/`WaitEventForTaskAsync`/`WaitEventCoreAsync` (Z. 128–147), `WaitForPollAsync` (Z. 149), `Responses`, `Prompts`, `BeginRuntimeCommand`, `Hold`/`Control` (Fehlerinjektion)

### Weitere Helfer
- `TestDbContextFactory` (`src/Softwareschmiede.Tests/Helpers/`): InMemory-DbContext für Service-Tests
- `InMemoryCredentialStoreForSettings` (`SettingsViewModelTests.cs`): InMemory-`ICredentialStore`
- `WpfTestBase` (`src/Softwareschmiede.Tests/E2E/`): FlaUI-Basis (LaunchApp, WaitForElement, Fenster-Wrapper `Views/*.cs`)
- `TestCliPlugin` (`SettingsViewModelTests.cs`): minimales `CliKiPluginBase`-Testplugin
