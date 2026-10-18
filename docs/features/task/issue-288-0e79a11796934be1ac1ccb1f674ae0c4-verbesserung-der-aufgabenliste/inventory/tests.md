# Tests

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-10-18, Testläufe zwischen 18:36 und 18:54 MESZ (UTC+02:00, Windows, Rechner `DESKTOP-CM8OBSG`)
- **Branch und Commit-ID:** `task/issue-288-0e79a11796934be1ac1ccb1f674ae0c4-verbesserung-der-aufgabenliste`, HEAD `7b909e51a1f9ad21ec092f4c88a89ddbf42ee280` ("fix: Unassigned-SCM-Quellen nicht als Überwachungsfehler melden (#290)")
- **Uncommittete Änderungen im getesteten Stand:** keine Änderungen an getrackten Dateien (`git status --short` zeigt nur zwei untracked Einträge: `docs/features/task/issue-288-…/` — die Feature-Dokumentation inkl. `requirement.md` — sowie die Datei `nul` (163 Bytes; Rest einer fehlgeschlagenen `dir`-Ausgabeumleitung, vorhandenes Artefakt ohne Bezug zur Anforderung))
- **Testumgebung und Runtime-/SDK-Versionen:** .NET SDK `10.0.402`; Test-Assembly `Softwareschmiede.Tests.dll` / `Softwareschmiede.IntegrationTests.dll` für `net10.0-windows10.0.17763.0`, Debug-Konfiguration; Windows-Desktop-Session (WPF-E2E über FlaUI/UIA3 lauffähig); Umgebungsvariable `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` bei allen `dotnet test`-Läufen gesetzt (Sandbox-Limitation, siehe CLAUDE.md)
- **Ermittelte Testsuiten und Quellen der Testbefehle:** `CLAUDE.md` (Testing-Abschnitt: stabile Lane `Category!=OsInterface`, OS-Interface-Lane separat) sowie `.github/workflows/pr-staging-ci.yml` (Z. 128/132 regulär, Z. 222/226 OsInterface; analog `staging-ci.yml`, `release.yml`). CI nutzt `--no-build` nach separatem Build — hier wurde zuvor ein vollständiger Solution-Build ausgeführt und `dotnet test` mit implizitem Build aufgerufen (identische Testmenge). Suites: `src/Softwareschmiede.Tests` (Unit + E2E) und `src/Softwareschmiede.IntegrationTests`.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build Softwareschmiede.slnx -c Debug` | Repo-Root | 0 | — (0 Warnungen, 0 Fehler, ~12 s) | — | — | Konsolenausgabe im Sitzungslog |
| 1 | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` | Repo-Root | 0 | 2022 | 0 | 1 | [Log](test-results/dotnet-test-regular.log), [TRX](test-results/test-results-regular.trx) |
| 2 | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface"` | Repo-Root | 0 | 66 | 0 | 5 | [Log](test-results/dotnet-test-osinterface.log), [TRX](test-results/test-results-osinterface.trx) |
| 3 | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface"` | Repo-Root | 0 | 194 | 0 | 0 | [Log](test-results/dotnet-test-integration-regular.log), [TRX](test-results/test-results-integration-regular.trx) |
| 4 | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category=OsInterface"` | Repo-Root | 0 | 9 | 0 | 0 | [Log](test-results/dotnet-test-integration-osinterface.log), [TRX](test-results/test-results-integration-osinterface.trx) |

Laufzeiten (TRX `startTime`→`endTime`, UTC+02:00): Lauf 1: 18:36:18–18:38:58 (2 m 39 s); Lauf 2: 18:39:09–18:52:53 (13 m 44 s); Lauf 3: 18:53:28–18:53:54 (25 s); Lauf 4: 18:54:00–18:54:03 (3 s).

### Nachgewiesene bestehende Testfehler

**Keine.** Alle 2291 ausgeführten Tests sind bestanden; es gab weder Testfehlschläge noch Build-, Setup- oder Infrastrukturfehler. Kein Fehler darf daher in späteren Arbeiten als "preexisting" ausgewiesen werden — jeder dann auftretende Fehlschlag ist neu zu untersuchen (Ausnahme: die unten dokumentierten, absichtlich übersprungenen Tests).

### Testlücken und Ausführungsprobleme

Übersprungene Tests (6 insgesamt, alle dokumentierte Sandbox-/Plattform-Einschränkungen, keine Fehlschläge):

| Test | Grund | Lauf |
|------|-------|------|
| `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` | `[SkippableFact]` mit `Skip.If(OperatingSystem.IsWindows(), …)` — Test gilt nur für Nicht-Windows (`ArbeitsverzeichnisOeffnenServiceTests.cs:36`) | 1 |
| `Softwareschmiede.Tests.E2E.End2EndTest.ConPty_LifecycleUndReplay_E2E` | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (bestätigte Sandbox-Limitation: ConPTY-Kindprozess wird nicht an die Pseudokonsole gebunden) | 2 |
| `Softwareschmiede.Tests.E2E.End2EndTest.ConPty_WeitereLebenszyklusSzenarien_E2E` | dto. | 2 |
| `Softwareschmiede.Tests.E2E.End2EndTest.ConPty_SessionReattach_KeineDoppelausgabe_E2E` | dto. | 2 |
| `Softwareschmiede.Tests.E2E.End2EndTest.TerminalText_LiveMarkCopyAndKeepSelection_E2E` | dto. | 2 |
| `Softwareschmiede.Tests.E2E.E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` | dto. (startet CLI via ConPTY) | 2 |

Anmerkung zum Umfang: Die 5 ConPTY-Skips sind der tatsächlich getestete Umfang in dieser Sandbox; in einer interaktiven Session (ohne die Env-Variable) würden diese Tests ausgeführt. Alle übrigen E2E-Tests (61 Stück in Lauf 2) liefen inkl. echter WPF-Fenster erfolgreich.

## Testklassen (für den Bereich relevant)

### `MainWindowViewModelTests`

Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`

- `AktiveAufgabenAktualisierenAsync_ShouldFillObservableCollection_WhenCalled` — Liste wird befüllt
- `AktiveAufgabenAktualisierenAsync_ShouldSetOpenTodoCountPerTask` — `OffeneTodoCount`/`OffeneTodoLabelText` ("2 Todos"/"0 Todos")
- `AktiveAufgabenAktualisierenAsync_ShouldMapPausiertBisUtc_WhenTaskIsPaused` — `PausiertBisUtc`/`IstPausiert`-Mapping
- `OffeneTodosAnzeigenCommand_ShouldOpenDialogForSelectedTask` — To-do-Dialog öffnet sich mit Aufgabe/Todos
- `IsDashboardVisible_*` (2 Tests) — Sichtbarkeitssteuerung Seitenleisten-Sektion
- `NavigateZuAufgabeCommand_*` (2 Tests) — Detail-VM-Erzeugung, Fenstertitel
- `ProjectTaskNavigation_ShouldSetTitleToLoadedTaskTitle`, `NavigateToDashboardCommand_*` (3 Tests) — Titel-/Callback-Verhalten
- `CurrentView_Setter_UsesFireAndForgetSafely` — Refresh-Trigger über `CurrentView`
- `NavigateToDashboard_ShouldShareAktiveAufgabenListeWithDashboardViewModel_WhenCalled` — `BeSameAs`-Prüfung der geteilten Liste
- `NavigateToDashboard_ShouldWireNavigateZuAufgabeActionOnDashboardViewModel_WhenCalled` — Navigationsdelegat
- `AktiveAufgabenAktualisierenAsync_ShouldMarkCurrentTaskAsActive_*`, `…ShouldMoveActiveMarker_*`, `…ShouldKeepActiveMarker_*`, `…ShouldNotMarkAnyTaskActive_*` (4 Tests) — `IsAktiv`-Markierung
- `AktiveAufgabenAktualisierenAsync_ShouldResolvePluginNamesPerTask_WhenPrefixesAreKnown` — `ScmPluginName="GitHub"`, `KiPluginName="Codex"` per `IPluginManager`-Mock
- `AktiveAufgabenAktualisierenAsync_ShouldFallbackToStoredPrefix_WhenPluginIsUnknown` — Prefix-Fallback ("Unbekannt.KI")
- `AktiveAufgabenAktualisierenAsync_ShouldNotUseGlobalDefaults_WhenPluginPrefixesAreMissing` — `null` bei fehlendem Prefix
- `RunningCountChanged_ShouldReloadAktiveAufgabenListe_WhenRaised` — Event-Trigger
- `LaufdatenChanged_ShouldReloadVisiblePanelItem_WhenRunDataWasPersisted` — Refresh via `AufgabeLaufdatenChangedNotifier` + Converter-Ausgabe ("✓ Bereit"→"▶ Läuft")
- `AktiveAufgabenAktualisierenAsync_ShouldSkip_WhenAlreadyRunning` — Re-Entrancy-Semaphore
- `Dispose_ShouldUnsubscribeFromRunningCountChanged`, `Dispose_ShouldUnsubscribeFromLaufdatenChanged` — Abmeldung
- `CurrentVersion_*` (2 Tests) — Versionsanzeige
- Enthält private Fake-Klassen `VerzoegernderAktiveAufgabenService`, `ZaehlenderAktiveAufgabenService` (`IAktiveAufgabenService`)

### `MainWindowViewModelTests_ZeitgesteuerterPrompt`

Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests_ZeitgesteuerterPrompt.cs`

- `AktiveAufgabenAktualisierenAsync_MitGeplantemPrompt_SetztHasScheduledPrompt` / `…OhneGeplantenPrompt_HasScheduledPromptBleibtFalse` — `HasScheduledPrompt`-Mapping

### `MainWindowViewModelTests_UpdateStartup` / `MainWindowViewModelTests_UpdateSettingsReadFailure` / `MainWindowViewModelUpdateTestBase`

Dateien: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests_UpdateStartup.cs`, `…_UpdateSettingsReadFailure.cs`, `MainWindowViewModelUpdateTestBase.cs` — Update-Ablauf, für die Anforderung nicht direkt relevant.

### `DashboardViewModelTests`

Datei: `src/Softwareschmiede.Tests/App/ViewModels/DashboardViewModelTests.cs`

- `Initialize_ShouldSetAktiveAufgabenListeToProvidedInstance_WhenCalled` — `BeSameAs`
- `LadenAsync_ShouldNotModifyAktiveAufgabenListe_WhenCalled` — Dashboard verändert die Liste nicht
- `NavigateZuAufgabeCommand_ShouldInvokeNavigateZuAufgabeAction_WhenExecuted`, `…ShouldNotThrow_WhenNavigateZuAufgabeActionNotSet` — Delegat-Verhalten

### `KiAusfuehrungsStatusConverterTests` / `…_ZeitgesteuerterPrompt`

Dateien: `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusConverterTests.cs`, `…_ZeitgesteuerterPrompt.cs`

- 15 Tests in der Hauptdatei: "▶ Läuft"/"⏸ Wartet"/"✓ Bereit" für `Aufgabe` und `AktiveAufgabePanelItem`, Pausiert-Countdown (inkl. >24 h und abgelaufene Pause), `null`-Eingabe → `string.Empty`, `ConvertBack` → `NotSupportedException`
- 3 Tests Scheduled-Prompt: "⏳ Prompt in Wartestellung" auch bei laufender CLI, "⏸ Wartet" ohne Prompt

### `AufgabeServiceTests` (relevante Auswahl)

Datei: `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs` (Z. 681–803)

- `GetAktiveAufgabenAsync_ShouldReturnAufgabenWithStatusGestartetOrWartend_WhenCalled`
- `GetAktiveAufgabenAsync_ShouldSortByLetzterCliStartDescThenByErstellungsDatum_WhenCalled`
- `GetAktiveAufgabenAsync_ShouldIgnoreLastHeartbeatForSorting_WhenLetzterCliStartExists`
- `GetAktiveAufgabenAsync_ShouldLimitTo20Results_WhenMoreThan20Exist`
- `GetAktiveAufgabenAsync_ShouldIncludeProjekt_WhenCalled`
- `GetAktiveAufgabenAsync_ShouldIncludeGitRepository_WhenCalled`

### E2E-Tests (FlaUI, `Category=OsInterface`), die die Aufgabenliste nutzen

| Test | Datei | Relevanter Anker |
|------|-------|------------------|
| `End2EndTest.AufgabeWechselUeberSeitenleiste_*` | `src/Softwareschmiede.Tests/E2E/E2E_TaskWechselUeberMenue.cs` | `MenuView.NavigateToTask` → Klick auf `AufgabeNavigieren:{Titel}` (2×) |
| `End2EndTest.AufgabePausieren_*` | `src/Softwareschmiede.Tests/E2E/E2E_AufgabePausieren.cs` | Wartet auf `AufgabeNavigieren:{Titel}` als Seitenleisten-Nachweis (Z. 60–63); `AufgabeStatus:{Titel}`-HelpText; `AufgabeKachel:{Titel}`-HelpText "Pausiert:True/False" |
| `End2EndTest.*` (Plugin-Auswahl/Wechsel) | `src/Softwareschmiede.Tests/E2E/E2E_PluginAuswahlUndWechsel.cs` | `MenuView.GetActiveTaskKiPluginName` liest `AutomationId AktiveAufgabeKiPlugin_{Titel}` (Z. 74, 121) |
| `End2EndTest.*` (Arbeitsstatus) | `src/Softwareschmiede.Tests/E2E/E2E_ArbeitsstatusAktualisierung.cs` | `MenuView.WaitForTaskStatus` erwartet Status-Volltext im `HelpText` von `AufgabeStatus:{Titel}` (Z. 56, 62) |

## Hilfsmethoden

### `MenuView` (E2E-View für die Navigationsseitenleiste)

Datei: `src/Softwareschmiede.Tests/E2E/Views/MenuView.cs` (erbt `BaseWindowView`/`WpfTestBase`-Helfer)

- `NavigateToDashboard()` / `NavigateToProjects()` / `NavigateToSettings()` — Navigations-Buttons via `NavigiereBisSichtbar` (Klick-Retry + "FehlerMeldung"-Fail-Fast)
- `NavigateToTask(taskTitle)` — klickt `AufgabeNavigieren:{Titel}`-Button (`Z. 257–261`) — **Anker entfällt laut Anforderung mit dem Pfeil-Button**
- `GetActiveTaskKiPluginName(taskTitle)` — liest `ByAutomationId("AktiveAufgabeKiPlugin_{Titel}")` und strippt `"KI: "` (`Z. 269–279`)
- `WaitForTaskStatus(taskTitle, expectedStatus, timeout)` — pollt `ByName("AufgabeStatus:{Titel}")` auf `HelpText == expectedStatus` (`Z. 290–306`) — Vertrag "Volltext im HelpText" ist beizubehalten
- `GetVersionText`, `ClickUpdatePruefen`, `ClickUpdateStarten`, `IsUpdateStartButton*`, `GetOfferedUpdateVersion`, `WaitForOfferedUpdateVersion`, `GetUpdateHinweis`, `WaitForUpdateHinweis` — Update-Bereich

### `WpfTestBase`

Datei: `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`

Startet `Softwareschmiede.App.exe` (Debug/Release, `net10.0-windows10.0.17763.0`) als eigenen Prozess, wartet aufs Hauptfenster, stellt Timeouts `Short`/`Medium`/`Long` (20 s/15 s/30 s via `ElementWaitHelper`), Credential-Store-Snapshot und App-Log-Snapshot (`_appLogDirectory`, `LogSnapshot`) bereit. E2E-Methoden tragen `[Trait("Category","E2E")]` + `[OsInterface]`/`Scenario`-Traits (`MainTest.cs`).

### `ElementWaitHelper` / `BaseWindowView`

`src/Softwareschmiede.Tests/E2E/ElementWaitHelper.cs`, `E2E/Views/` — `WaitForElement(window, conditionFactory, timeout)` und View-Pattern-Basis (`ForceShow`, `ForceClose`, `IsVisible`), die von `MenuView` und den anderen Views genutzt werden.

### `TestDbContextFactory`, `TaskDetailViewModelTestFactory`, `TestKiAusfuehrungsServiceFactory`

`src/Softwareschmiede.Tests/Helpers/` — In-Memory-DbContext bzw. fertige ViewModels/Services für die Unit-Tests.
