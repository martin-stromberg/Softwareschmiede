# Tests — Symbol für Auftragsart auf den Aufgabenkacheln (Issue #462)

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-11-23, ca. 00:11–00:33 Uhr MEZ (UTC+1; Rechner `DESKTOP-CM8OBSG`, TRX-Zeitstempel `+01:00`)
- **Branch und Commit-ID:** `task/issue-462-688f2045750f4637af8344f303856c57-symbol-fuer-ursprungstyp`, Commit `e480710803689e47f7de20f2d5a2f1acad949a8a` („Backmerge from main to staging (#460)")
- **Uncommittete Änderungen im getesteten Stand:** keine Änderungen an getrackten Dateien; untracked nur `docs/features/task/issue-462-688f2045750f4637af8344f303856c57-symbol-fuer-ursprungstyp/` (Anforderungs-/Inventardateien inkl. der hier abgelegten TRX-Reports). Getesteter Stand entspricht damit dem Commit-Inhalt.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows (WPF-Desktop, `net11.0-windows10.0.17763.0`), .NET SDK `11.0.100`, Test-Runtime `.NET 11.0.0`, xUnit.net VSTest-Adapter 4.0.1. Alle Testläufe mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (Sandbox-Regel aus `CLAUDE.md`: ConPTY-Kindprozesse werden unter `dotnet test` in dieser Sandbox nicht isoliert — betroffene Tests werden sauber übersprungen statt mit Timeout zu scheitern).
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Zwei Testprojekte, je zwei Kategorie-Lanes — Quelle: `.github/workflows/pr-staging-ci.yml` (Z. 121–146, 217–226) und `CLAUDE.md` (stabile Lane = `Category!=OsInterface`; `Category=OsInterface` separat, enthält E2E/FlaUI, ConPTY, Clipboard, echte Prozessstarts u. a.):
  - `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` (`net11.0-windows10.0.17763.0`, xUnit + FlaUI)
  - `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` (`net11.0`)
  - Vor allen Testläufen: `dotnet build Softwareschmiede.slnx -c Debug` — **erfolgreich** (0 Warnungen, 0 Fehler, ~7 s).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build Softwareschmiede.slnx -c Debug` | Repo-Root | 0 | — | — | — | Konsolenlog (kein TRX) |
| 1 Regulär | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category!=OsInterface" --results-directory docs/features/task/issue-462-688f2045750f4637af8344f303856c57-symbol-fuer-ursprungstyp/inventory/test-results --logger "trx;LogFileName=test-results-regular-tests.trx"` | Repo-Root | 0 | 2782 | 0 | 3 | [TRX](test-results/test-results-regular-tests.trx) |
| 2 Regulär Integration | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category!=OsInterface" --results-directory docs/features/task/issue-462-688f2045750f4637af8344f303856c57-symbol-fuer-ursprungstyp/inventory/test-results --logger "trx;LogFileName=test-results-regular-integration.trx"` | Repo-Root | 0 | 406 | 0 | 0 | [TRX](test-results/test-results-regular-integration.trx) |
| 3 OsInterface | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category=OsInterface" --results-directory docs/features/task/issue-462-688f2045750f4637af8344f303856c57-symbol-fuer-ursprungstyp/inventory/test-results --logger "trx;LogFileName=test-results-os-interface-tests.trx"` | Repo-Root | 0 | 70 | 0 | 5 | [TRX](test-results/test-results-os-interface-tests.trx) |
| 4 OsInterface Integration | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category=OsInterface" --results-directory docs/features/task/issue-462-688f2045750f4637af8344f303856c57-symbol-fuer-ursprungstyp/inventory/test-results --logger "trx;LogFileName=test-results-os-interface-integration.trx"` | Repo-Root | 0 | 19 | 0 | 0 | [TRX](test-results/test-results-os-interface-integration.trx) |

Gesamt: **3285 entdeckte Tests** (2785 + 406 + 75 + 19); davon 3277 bestanden, 0 fehlgeschlagen, 8 übersprungen.

### Nachgewiesene bestehende Testfehler

Keine — alle vier Lanes liefen ohne Fehlschlag durch (Exit-Code jeweils 0).

### Testlücken und Ausführungsprobleme

Übersprungene Tests (alle absichtlich/mechanisch begründet, keine Infrastrukturfehler):

| Test | Lane | Grund |
|------|------|-------|
| `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_Linux_RemainsUnchanged` | 1 (regulär) | Plattformbedingter Skip — prüft den Linux-Pfad, auf diesem Windows-Host per Design übersprungen |
| `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_RemainsUnchanged` | 1 (regulär) | Plattformbedingter Skip — macOS-Pfad |
| `Softwareschmiede.Tests.Application.Services.WorkingDirectoryOpenServiceTests.Open_OnNotWindows_ThrowsPlatformNotSupportedException` | 1 (regulär) | `SkippableFact` mit `Skip.If(OperatingSystem.IsWindows())` — prüft den Nicht-Windows-Pfad |
| `Softwareschmiede.Tests.E2E.End2EndTest.ConPty_SessionReattach_NoDoubleoutput_E2E` | 3 (OsInterface) | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` — ConPTY-Kindprozess wird in dieser Sandbox nicht isoliert (dokumentierte Sandbox-Limitation, in CI/interaktiver Session grün) |
| `Softwareschmiede.Tests.E2E.End2EndTest.ConPty_FurtherLifecycleScenarios_E2E` | 3 (OsInterface) | ebenso |
| `Softwareschmiede.Tests.E2E.End2EndTest.ConPty_LifecycleAndReplay_E2E` | 3 (OsInterface) | ebenso |
| `Softwareschmiede.Tests.E2E.End2EndTest.TerminalText_LiveMarkCopyAndKeepSelection_E2E` | 3 (OsInterface) | ebenso |
| `Softwareschmiede.Tests.E2E.E2E_RepositoryInitializationExecutionTests.InitializationscriptExecution` | 3 (OsInterface) | ebenso — startet echte CLI-Prozesse über ConPTY |

**Hinweis (kein Testfehler):** Im Konsolenlog von Lauf 1 erschien nach Abschluss der Assertions ein unbehandelter `System.Threading.Tasks.TaskCanceledException`-Stacktrace aus einem Hintergrund-Timer (`PseudoConsoleSession.<.ctor>b__70_0` → `RefreshRuntimeStatus` → `TaskDetailViewModel.OnCliRuntimeStatusChanged` → `Dispatcher.Invoke`, `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:1813` bzw. `DispatcherInvokeFactory.cs:16`). Der Lauf endete dennoch mit „Der Testlauf war erfolgreich" und 0 Fehlschlägen — der Fehler trat beim Testhost-Teardown auf und wurde keinem Test zugeordnet.

**Anforderungsbezogene Testlücke:** Es existiert noch **kein Test** für `KindLabel`/das Auftragsart-Badge-Mapping in `MapActiveWorkItemPanelItem` (`MainWindowViewModelTests` enthält keine `Kind`-Assertions) und kein Test für ein Symbol-Mapping (existiert noch nicht). Die Badge-Sichtbarkeit wurde in der Vorarbeit (Issue #451 / Schritt 4, `docs/features/task/issue-456-3cb8391f3eed40858c230db71a140e58-issue-aufbereitung-repository-schritt-4-auftragsart/test-results.md` Z. 24) bewusst ohne FlaUI-E2E über Unit-Tests abgedeckt („Badge-Sichtbarkeit folgt trivial aus `KindLabel`-Binding").

## Testklassen (bestehend, anforderungsrelevant)

### `MainWindowViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`

- `RefreshActiveWorkItemsAsync_ShouldFillObservableCollection_WhenCalled` (Z. 129) — Basistest: `ActiveWorkItemsList` wird aus `WorkItemService` befüllt.
- `RefreshActiveWorkItemsAsync_ShouldResolvePluginNamePerTask_WhenPrefixesAreKnown` (Z. 482) — Plugin-Namen-Auflösung via `IPluginManager`-Mocks; Vorbild für `Kind`-Mapping-Tests.
- `RefreshActiveWorkItemsAsync_ShouldFallbackToStoredPrefix_WhenPluginIsUnknown` (Z. 522) — Fallback auf rohen Prefix.
- `RefreshActiveWorkItemsAsync_ShouldNotUseGlobalDefaults_WhenPluginPrefixesAreMissing` (Z. 540) — `ScmPluginName`/`AiPluginName` bleiben `null` ohne Prefix.
- `RefreshActiveWorkItemsAsync_ShouldMapPluginSymbol_WithFallbackGlyphTolerance` (Z. 833) — `ScmPluginSymbol`/`AiPluginSymbol`-Mapping inkl. `IPlugin.FallbackSymbol`-Toleranz bei `null` aus Moq-Mocks.
- `RefreshActiveWorkItemsAsync_ShouldMapAiGroupsName` (Z. 882) — `AiGroupName`/`AiGroupIsFallback` (PluginName / roher Prefix / `WorkItemListLayout.NotExecuted`).
- `RefreshActiveWorkItemsAsync_ShouldMapRepositoryGroupsName` (Z. 918) — `RepositoryGroupName`/`RepositoryGroupIsFallback`.
- `RefreshActiveWorkItemsAsync_ShouldMapRepositoryName_WithRepoAndProjectFallback` (Z. 802) — Untertitel-Mapping.
- `RefreshActiveWorkItemsAsync_ShouldSetOpenTodoCountPerTask` (Z. 146), `…_ShouldMapLiveRunStatus` (Z. 171), `…_ShouldMapPausedUntilUtc_WhenTaskIsPaused` (Z. 191), `…_ShouldMarkCurrentTaskAsActive_WhenTaskDetailIsCurrentView` (Z. 404) u. a. — weitere Panel-Item-Mappings.
- **Kein `Kind`-/`KindLabel`-Test vorhanden** (grep nach `Kind` in der Datei: keine Treffer).

### `WorkItemsListsViewTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/WorkItemsListsViewTests.cs`

- `Configure_ShouldApplyGroupingAndSorting_WhenModeIsDate` (Z. 12) — flache Liste, kein Gruppendeskriptor.
- `Configure_ShouldApplyGroupingAndSorting_WhenModeIsRepository` (Z. 23) — `RepositoryGroupName`-Gruppierung, Fallback-Primärsortierung.
- `Configure_ShouldApplyGroupingAndSorting_WhenModeIsAi` (Z. 37) — `AiGroupName`-Gruppierung.
- `Configure_ShouldApplyGroupingAndSorting_WhenModeIsKind` (Z. 51) — `KindGroupName`-Gruppierung + Primärsortierung `KindSortOrder` asc.
- `Configure_ShouldPlaceFallbackGroupLast_WhenGrouped` (Z. 63) — Fallback-Gruppe ans Ende (`ListCollectionView` über `ActiveWorkItemPanelItem`-Liste).
- `Configure_ShouldSortWithinGroupBySortTimestampDesc` (Z. 81) — gruppeninterne Sortierung.
- **Kein Test von `WorkItemListLayout.KindGroupName` selbst** (Namen-Mapping je Enum-Wert ungetestet; nur der Deskriptor-Verweis wird geprüft).

### `WorkItemServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/WorkItemServiceTests.cs`

- `CreateAsync_ShouldCreateWorkItemWithStatusOpen_WhenCalledWithValidData` (Z. 46, Assert Z. 58) — `Kind == WorkItemKind.Manual` bei manueller Anlage.
- `CreateFromIssueAsync_…` (Z. 63, Assert Z. 82) — `Kind == Manual` bei manueller Issue-Anlage.
- `CreateFromIssueEventAsync_ShouldAssignKind_WhenCreatedFromEvent` (Z. 90, Theory) — `purpose=null` → `Instruction`, `purpose="IssueAufbereitung"` → `IssuePreparation`.
- PR-/Alert-Anlagepfade (Z. 180, 226) — `Kind == PullRequest` bzw. `Alert`.
- `GetActiveWorkItemsAsync_*` (Z. 705–825) — Datenquelle der Liste (Filter auf Started/Waiting, Sortierung, Top-20-Limit, Includes `Project`/`GitRepository`).

### `PluginSymbolTests`
Datei: `src/Softwareschmiede.Tests/PluginSymbolTests.cs`

- `PluginSymbol_ShouldBeNonEmptyAndDistinctFromDefault_ForAllRepoPlugins` (Z. 75) — alle Repo-Plugins liefern einen nicht-leeren, vom `FallbackSymbol` verschiedenen Glyph.
- `PluginSymbol_ShouldReturnInterfaceDefault_WhenNotOverridden` (Z. 88) — Default-Interface-Member liefert `FallbackSymbol`.

### `End2EndTest` (E2E, FlaUI) — relevante Ausschnitte
Dateien: `src/Softwareschmiede.Tests/E2E/MainTest.cs` + partielle Szenario-Dateien

- `WorkItemsList_CompactTileSymbolAndModeswitch_E2E` (`E2E_WorkItemsListModes.cs` Z. 23) — liest KI-/SCM-/Status-Glyph über `MenuView`-Accessoren (Z. 85–88), prüft To-do-Button und Moduswechsel (Datum/Repository/KI) in Seitenleiste + Dashboard inkl. Persistenz. **Kein Zugriff auf das Auftragsart-Badge und kein „Art"-Moduswechsel** — es existiert kein E2E-Accessor für `ActiveWorkItemKind_{Title}` (Vorbilder: `MenuView.GetActiveTaskScmPluginSymbol` Z. 481, `GetActiveTaskAiPluginSymbol` Z. 472, `GetActiveTaskAiPluginName` Z. 454, `GetActiveTaskStatusGlyph` Z. 493; `DashboardView.cs` hat keine Symbol-Accessoren).

### Integrationstests (anforderungsrelevant)
- `Softwareschmiede.IntegrationTests` — SCM-Ereignis-Pipeline-Tests (u. a. `ScmTaskCompletionMonitoringServiceTests`, Issue-Aufbereitung), decken den `Kind`-Wechsel `IssuePreparation` → `Instruction` auf dem echten Service-Pfad ab (vgl. Vorarbeit test-results.md Schritt 4).

## Hilfsmethoden / Test-Doubles

### `MainWindowViewModelTests`-Setup
Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`

- `CreateSut(...)` (Z. 80) — baut `MainWindowViewModel` mit echtem `WorkItemService` auf In-Memory-DB (`TestDbContextFactory`), `Mock<IPluginManager>`, `Mock<IRunningAutomationStatusSource>`, `Mock<IDialogService>`, `MainWindowOptionalServices(DispatcherInvoke: action => action(), …)`.
- `_workItemService.CreateAsync/UpdateAsync/StartAsync` — Standard-Sequenz zum Anlegen aktiver Aufgaben (`Kind` ist bei `CreateAsync` immer `Manual`; andere Arten müssten für Symbol-Tests direkt über die Test-DB gesetzt oder über die jeweiligen Create*-Pfade erzeugt werden).

### `TestDbContextFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/` — In-Memory-`SoftwareschmiedeDbContext`; `WorkItem.Kind` kann direkt im Test gesetzt werden (vgl. `E2E_WorkItemsListModes.cs` Z. 62–72: Direktzugriff auf `db.WorkItems`).

### `WpfTestBase` / View-Wrapper (E2E)
Dateien: `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`, `src/Softwareschmiede.Tests/E2E/Views/`

- `LaunchApp`, `WaitForElement` (Timeouts `Short`/`Medium`/`Long`), `OpenTestDbContext`, `RestartAppPreservingDatabase`.
- `MenuView` (Z. 454–520): Accessoren `GetActiveTaskAiPluginName`, `GetActiveTaskAiPluginSymbol`, `GetActiveTaskScmPluginSymbol`, `GetActiveTaskStatusGlyph`, `ClickOpenTodosButton` — Vorbild für einen optionalen Badge-Accessor; Scoping über `AutomationId "ActiveWorkItemsListMenu"`.
- `DashboardView` (Z. 10, 47–72): `WorkItemsListAutomationId = "ActiveWorkItemsListDashboard"`, `SetWorkItemsListsMode`, `GetWorkItemsGroupsNames`, `WaitForWorkItemsGroup`.

### Testinfrastruktur-Attribute
Dateien: `src/Softwareschmiede.Tests/Infrastructure/Testing/`

- `OsInterfaceAttribute`/`OsInterfaceFactAttribute`/`OsInterfaceTheoryAttribute` — markieren `Category=OsInterface`.
- `FlakyAttribute`/`FlakyConventionTests` — Flaky-Markierung mit Konventionstest.
- `WpfUnitTestHelpers.RunOnSta` (`src/Softwareschmiede.Tests/Helpers/WpfUnitTestHelpers.cs`) — STA-Ausführung für WPF-Unit-Tests.
