# Tests — Bestandsaufnahme und Test-Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 16.09.2026, ca. 10:17–10:26 Uhr lokale Zeit (UTC+2, CEST)
- Branch: `task/issue-151-4e13c673297741e8b0f7a2ad19f7d7e3-erkennen-von-limits`
- Commit-ID: `eae4caba421afee65c6185de30470a98ac669b03` („Backmerge from main to staging (#261)", 2026-09-06 18:27:32 +0200)
- Uncommittete Änderungen im getesteten Stand: nur untracked Dateien — `.agents/` (Lifecycle-Skills) sowie `docs/features/task/issue-151-4e13c673297741e8b0f7a2ad19f7d7e3-erkennen-von-limits/` (zu Testzeitpunkt: `requirement.md`, `todo.md`; die `inventory/`-Artefakte entstanden durch diese Bestandsaufnahme). Keine Änderungen an getrackten Dateien.
- Testumgebung und Runtime-/SDK-Versionen: Windows; .NET SDK `10.0.401`; Testziel-Framework `net10.0` (`Softwareschmiede.IntegrationTests`) bzw. `net10.0-windows10.0.17763.0` (`Softwareschmiede.Tests`, WPF). `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` bei allen `dotnet test`-Aufrufen gesetzt (Sandbox-Einschränkung: ConPTY-Isolation funktioniert hier nicht, siehe CLAUDE.md).
- Ermittelte Testsuiten und Quellen der Testbefehle: `CLAUDE.md` (Abschnitt „Testing") — stabile Lane `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`, separate OS-Interface-Lane `--filter "Category=OsInterface"` (enthält E2E/ConPTY/Clipboard/echte Prozessstarts); zusätzlich das Projekt `src/Softwareschmiede.IntegrationTests` mit denselben beiden Filtern. Vorab: vollständiger Build `dotnet build Softwareschmiede.slnx` (erfolgreich, keine MSB3026/MSB3027-Locks).
- Vollständiger Build: `dotnet build Softwareschmiede.slnx` — erfolgreich (alle Projekte inkl. App und Plugins, Debug). Kein Locked-Exe-Fallback nötig.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Lane 1: Unit/App | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=inventory-lane1-regular-tests.trx"` | Repo-Root | 0 | 1528 | 0 | 1 | [Log](test-results/lane1-tests-regular.log) / [TRX](test-results/inventory-lane1-regular-tests.trx) |
| Lane 2: OS-Interface | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface" --logger "trx;LogFileName=inventory-lane2-osinterface-tests.trx"` | Repo-Root | 0 | 48 | 0 | 2 | [Log](test-results/lane2-tests-osinterface.log) / [TRX](test-results/inventory-lane2-osinterface-tests.trx) |
| Lane 3: Integration | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=inventory-lane3-regular-integration.trx"` | Repo-Root | 0 | 69 | 0 | 0 | [Log](test-results/lane3-integration-regular.log) / [TRX](test-results/inventory-lane3-regular-integration.trx) |
| Lane 4: Integration OS-Interface | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category=OsInterface" --logger "trx;LogFileName=inventory-lane4-osinterface-integration.trx"` | Repo-Root | 0 | 9 | 0 | 0 | [Log](test-results/lane4-integration-osinterface.log) / [TRX](test-results/inventory-lane4-osinterface-integration.trx) |

Dauern: Lane 1 ≈ 1 min 20 s, Lane 2 ≈ 3 min 5 s, Lane 3 ≈ 9 s, Lane 4 ≈ 3 s. Alle Läufe synchron im Vordergrund ausgeführt; kein `--no-build` verwendet.

### Nachgewiesene bestehende Testfehler

Keine. In allen vier Lanes wurden **0 Fehlschläge** gemeldet.

### Testlücken und Ausführungsprobleme

Übersprungene Tests (alle begründet, keine Fehler):

| Test-ID | Suite / Dateipfad | Grund | Lauf / Nachweis |
|---------|-------------------|-------|-----------------|
| `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` | `src/Softwareschmiede.Tests/Application/Services/ArbeitsverzeichnisOeffnenServiceTests.cs` (Z. 34) | `[SkippableFact]` mit `Skip.If(OperatingSystem.IsWindows(), …)` — Test prüft das Nicht-Windows-Verhalten und läuft auf Windows absichtlich nicht. | Lane 1, [Log](test-results/lane1-tests-regular.log) |
| `Softwareschmiede.Tests.E2E.E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` | `src/Softwareschmiede.Tests/E2E/E2E_RepositoryInitialisierungAusfuehrungTests.cs` (Z. 40: `SkipWennConPtyNichtVerfuegbar()`) | ConPTY in dieser Sandbox nicht nutzbar → `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (CLAUDE.md). | Lane 2, [Log](test-results/lane2-tests-osinterface.log) |
| `Softwareschmiede.Tests.E2E.End2EndTest.RunConPtyTests` | `src/Softwareschmiede.Tests/E2E/MainTest.cs` (Z. 51/53) | Gebündelter ConPTY-E2E-Lauf (14+ UI-Szenarien inkl. `ZeitgesteuerterPrompt_..._ZeigtWartestellungStatus_E2E`, `SeitenleistenKachel_AktualisiertStatusAutomatisch_...`), geskippt via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`. | Lane 2, [Log](test-results/lane2-tests-osinterface.log) |

Sonstige Einschränkungen: keine Build-/Setup-/Infrastrukturfehler aufgetreten. Kein `Softwareschmiede.App.exe`-Lock beim Build. Die übersprungenen ConPTY-Szenarien gelten in dieser Umgebung weder als Erfolg noch als Fehlschlag — sie sind auf einer interaktiven Windows-Session auszuführen, wenn ein verändertes Verhalten daran verifiziert werden soll.

## Testklassen (anforderungsrelevant)

### `ProtokollServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ProtokollServiceTests.cs`

- `TestRateLimitMarkerParsing` — Marker wird erkannt, `DateTimeOffset` extrahiert (Z. 190).
- `TryParseRateLimitMarker_WithoutTimestamp_ReturnsTrueButNullResetUtc` — Marker ohne Zeitstempel → `true`, `resetUtc = null` (Z. 207).
- `TryParseRateLimitMarker_WithInvalidTimestamp_ReturnsTrueButNullResetUtc` — Ungültiger Zeitstempel → `true`, `resetUtc = null` (Z. 218).
- `TryParseRateLimitMarker_WithNoMarker_ReturnsFalse` — Kein Marker → `false` (Z. 229).
- `TryParseRateLimitMarker_WithEmptyLine_ReturnsFalse` — Leere Zeile → `false` (Z. 239).
- `AddCliOutputAsync_ShouldCreateRateLimitEntry_WhenMarkerDetected` — Bei Marker: `CliOutput` + zusätzlicher `ProtokollTyp.RateLimit`-Eintrag (Z. 249).
- `AddCliOutputAsync_ShouldCreateSingleEntry_WhenNoMarker` — Ohne Marker nur ein Eintrag (Z. 263).
- Weitere: `AddEintragAsync`/`AddTestErgebnisseAsync`/`AddStatusUebergangAsync`/`SuchenAsync`/`GetByAufgabeAsync`-Abdeckung.

### `RateLimitDetectionServiceIntegrationTests`
Datei: `src/Softwareschmiede.Tests/ServiceIntegration/RateLimitDetectionServiceIntegrationTests.cs`

- `MarkerInAusgabe_WirdErkannt_UndRateLimitEintragErstellt` — Markerzeile `[[SOFTWARESCHMIEDE_RATE_LIMIT:2026-06-15T10:00:00Z]]` über `AddCliOutputAsync` → 2 Einträge, davon einer `RateLimit` (Z. 31).
- `ParseRateLimitMarker_ExtrahiertZeitstempel_Korrekt` — Zeitstempel-Extraktion (Z. 46).
- `VorschlagPrompt_WirdGespeichert_UndStatusWirdWartend` — `StartenAsync` → `SavePromptVorschlagAsync` → `SetStatusAsync(Wartend)` persistiert Status + Vorschlag (Z. 59).
- `ClearPromptVorschlag_EntferntVorschlagUndZeitstempel` — Leeren des Vorschlags (Z. 81).

**Anforderungsrelevanz:** Belegt den bestehenden Marker→Protokoll-Pfad und die manuelle `Gestartet → Wartend`-Transition. Nicht abgedeckt: Plugin-weite Persistenz des Reset-Zeitpunkts, automatisches Pausieren weiterer Aufgaben mit gleichem `KiPluginPrefix`, automatische Reaktivierung nach Ablauf.

### `AufgabeServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`

- `TestStatusValidation` (Z. 653), `SetStatusAsync_ShouldSetStatus_WhenTransitionIsAllowed` (Z. 668) — Transitions-Validierung.
- `StatusSetzenAsync_ShouldSetStatusGestartet_WhenAufgabeExists` (Z. 416) / `..._ShouldSetGivenStatus_WhenAufgabeExists` (Z. 597) — validierungsfreies Setzen.
- `GetAktiveAufgabenAsync_ShouldReturnAufgabenWithStatusGestartetOrWartend_WhenCalled` (Z. 683) — Aktiv/Wartend-Filter.
- `GetAktiveAufgabenAsync_ShouldSortByLetzterCliStartDescThenByErstellungsDatum_WhenCalled` (Z. 704), `..._ShouldIgnoreLastHeartbeatForSorting_...` (Z. 724), `..._ShouldLimitTo20Results_...` (Z. 753), `..._ShouldIncludeProjekt/GitRepository_...` (Z. 769/786) — Sortierung, Limit, Includes.
- `TestHeartbeatUpdate` (Z. 612), `GetHeartbeatAgeMinutesAsync_ShouldReturnNull_WhenNoHeartbeatSet` (Z. 639) — Heartbeat-Persistenz.
- `SavePromptVorschlagAsync_*` (Z. 480/497), `ClearPromptVorschlagAsync_*` (Z. 517) — Vorschlagsprompt.
- `StartenAsync_*` (Z. 349/367/583), `AbschliessenAsync_*` (Z. 431), `VerwerfenAsync_*` (Z. 537–567) — Lebenszyklus.

### `AufgabeRecoveryServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/AufgabeRecoveryServiceTests.cs`

- `IstRecoveryStatus_ShouldMatchAllowedStates` — Theory über `AufgabeStatus` × `AufgabeAusfuehrungsStatus` × `istAutonom` (Z. 78).
- `RecoverManuellAsync_ShouldSetStatusAndCreateAudit_WhenTaskIsInArbeitAndNotRunning` / `..._WhenTaskInWartendAndNotRunning` (Z. 33/49) — Recovery aus `Gestartet` und `Wartend`.
- `RecoverManuellAsync_ShouldThrow_WhenTaskIsStillRunning` / `..._WhenStatusIsNotRecoverable` / `..._WhenRunningCheckFails` (Z. 85/99/113).
- `TestRecoveryCandidates` (Z. 127), `ScanForRecoveryCandidates_ShouldExcludeAutonomeAufgaben` (Z. 207), `..._ShouldExcludeRunningTasks` (Z. 274), `RecoverManuellAsync_ShouldThrow_WhenAufgabeIstAutonom` (Z. 241).

### `KiAusfuehrungsServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`

- Prozess-Lebenszyklus: `IsRunning_...` (Z. 33), `GetRunningCount_...` (Z. 40), `GetLastExitCode_...` (Z. 47), `StopCliAsync_ShouldNotThrow_WhenNoProcessStarted` (Z. 54), `TestCliStartAsync` (Z. 70), `StartCliAsync_ShouldReturnHandle_...` (Z. 96).
- Exit-Verhalten: `ProcessExited_ExitCodeZero/NonZero_PersistiertAusfuehrungBeendetOhneGesamtstatusZuBeenden` (Z. 171/201), `StopCliAsync_PersistiertAusfuehrungBeendetOhneGesamtstatusZuBeenden` (Z. 228) — Prozessende ändert `AusfuehrungsStatus`, nicht `Status`.
- Event-Robustheit: `ProcessExited_SubscriberThrows_LogsAndDoesNotCrash` (Z. 267), `ConPtyProcessExited_SubscriberThrows_...` (Z. 288).
- ConPTY-Pfad: `StartWithPseudoConsoleAsync_*` (Z. 450–676), darunter **`AddCliOutputAsync_RateLimitMarkerAusConPtyOutput_ErzeugtRateLimitEintrag` (Z. 632)** — Marker aus echter ConPTY-Ausgabe erzeugt `RateLimit`-Eintrag — sowie `..._PersistiertSessionOutputAlsCliOutput` (Z. 560) und `..._ParalleleAufgaben_TrenntProtokolleNachAufgabeId` (Z. 599).

### `AufgabeLaufAktivitaetTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/AufgabeLaufAktivitaetTests.cs`

- `IstAktiv_ShouldReturnTrue_WhenRunIdSetAndHeartbeatFresh` (Z. 11), `..._WhenHeartbeatOlderThanTimeout` (Z. 23), `..._WhenHeartbeatExactlyAtTimeout` (Z. 35), `..._WhenHeartbeatNull` (Z. 47), `..._WhenRunIdNull` (Z. 56) — vollständige Grenzfallabdeckung des 5-Minuten-Fensters.

### `SessionManagementServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/SessionManagementServiceTests.cs`

- `PauseAufgabeBeiBudgetLimit_SetztSessionPauseUtc` (Z. 60), `..._AktualisieertStateJson` (Z. 73) — persistenter Pausen-Zeitstempel (Autonome Aufgaben).
- `SetzeFort_SendetWeitermachenPrompt` (Z. 86) — Resume-Pfad.
- `PruefeAusfuehrung_ErkenntUnterbruch` (Z. 105), `..._KeineUnterbrechung_WennHeartbeatAktuell` (Z. 121), `..._GibtTrueZurueck_WennSessionPausiertIst` (Z. 135), `..._WennNochKeinHeartbeatVorliegt` (Z. 151), Fehlerfälle (Z. 162–184).

### `KiAusfuehrungsStatusConverterTests` (+ `_ZeitgesteuerterPrompt`)
Dateien: `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusConverterTests.cs`, `..._ZeitgesteuerterPrompt.cs`

- „▶ Läuft"/„⏸ Wartet"/„✓ Bereit"-Pfade für Entity und `AktiveAufgabePanelItem` (Z. 17–206), darunter `Convert_ShouldReturnWartetString_WhenStatusIstWartend` (Z. 36) und `..._WhenPanelItemHasActiveRunAndLaufStatusIstWartetAufEingabe` (Z. 152).
- `Convert_ShouldReturnBereitString_WhenWartendAufgabeHasBeendeteAusfuehrung` (Z. 172) / `..._WhenPanelItemWartendHasBeendeteAusfuehrung` (Z. 189) — `Wartend` ohne aktive Ausführung → „Bereit".
- Prompt-Wartestellung dominiert Laufstatus (Z. 17–55 der zweiten Datei).

### `MainWindowViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`

- `AktiveAufgabenAktualisierenAsync_*` — Listenbefüllung, To-Do-Zählung, Aktiv-Markierung, Plugin-Namensauflösung inkl. Prefix-Fallback (Z. 132–503).
- `RunningCountChanged_ShouldReloadAktiveAufgabenListe_WhenRaised` (Z. 549), `LaufdatenChanged_ShouldReloadVisiblePanelItem_WhenRunDataWasPersisted` (Z. 570) — eventgetriebene Refresh-Pfade.
- `UpdateStartenCommand_ShouldStop_WhenSafetyDialogIsDeclined` (Z. 645) u. a. — `CliUpdateSafetyService`-Integration im Update-Flow.

### `TaskDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

- Panel-Sichtbarkeiten nach Status: `ShowEditPanel_IsTrue_WhenStatusNeu` (Z. 363), `ShowCliPanel_IsTrue_WhenStatusGestartet`/`..._Wartend`/`..._AusfuehrungBeendetIst` (Z. 377/391/403), `ShowDiffPanel_IsTrue_WhenStatusBeendet` (Z. 421) — `Wartend` zeigt bereits das CLI-Panel.
- `KannSpeichern`/`KannLoeschen`-CanExecute-Matrix (Z. 647–739), `KannCliStoppen_ShouldBeFalse_WhenAufgabeIstAutonom...` (Z. 556).
- Keine Pausieren-Tests vorhanden.

### E2E — `End2EndTest` (partial, gebündelt)
Dateien: `src/Softwareschmiede.Tests/E2E/MainTest.cs` + `E2E_*.cs` (partial-Klassen)

- `RunGeneralTests` (`[Fact]`, `[OsInterface]`, `[Collection("E2E")]`) — gebündelte UI-Szenarien ohne ConPTY: Autonome-Aufgaben-Initialisierung, **AutonomAufgabeAgentExecution_StartUnteragentUndSessionPause_E2E** (Session-Pause der Autonomen Aufgaben), Settings/Persistenz, Repository, To-Dos, TaskDetail, ViewPattern, Fehleransicht.
- `RunConPtyTests` (`[SkippableFact]`, in dieser Sandbox übersprungen) — ConPTY-Szenarien u. a. `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E`, `AufgabeStarten_...`, `PluginAuswahlAbbrechenOkUndWechsel_E2E`, `PluginAktivierung_...`, `SeitenleistenKachel_AktualisiertStatusAutomatisch_OhneManuellesNeuladen_E2E`, `CliPanel_BleibtSichtbarNachBeendigung_E2E`.
- Weitere `E2E_*`-Dateien (u. a. `E2E_ArbeitsstatusAktualisierung.cs`, `E2E_ZeitgesteuerterPrompt.cs`, `E2E_PluginAuswahlUndWechsel.cs`, `E2E_PluginAktivierung.cs`, `E2E_SettingsKiPluginPersistence.cs`, `E2E_TaskDetailNavigation.cs`) sind `partial`-Methoden der Klasse `End2EndTest` und werden innerhalb der beiden gebündelten Läufe aufgerufen.

## Hilfsmethoden

### `WpfTestBase`
Datei: `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs` (Basisklasse aller WPF-E2E-Tests, FlaUI/UIA3)

- `LaunchApp` / `LaunchAppAndGetMainWindow` — startet `Softwareschmiede.App.exe` aus `bin/<Config>/net10.0-windows10.0.17763.0` mit eigener SQLite-Test-DB (`SOFTWARESCHMIEDE_TEST_DB_PATH`, Temp-Datei pro Test), wartet auf Hauptfenster, prüft App-Log auf `[ERR]`/`[FTL]` bei Startfehlschlag (`CheckAppStartupException`, `AppStartupLogInspector`).
- `OpenTestDbContext` / `TestDbPath` — direkter EF-Core-Zugriff auf die Test-DB für Vorbedingungen, die über die UI nicht abbildbar sind (relevant für das Setzen von Aufgabenstatus/Zeitstempeln in E2E-Tests).
- `WaitForElement`, `WaitUntilGone`, `WaitForWindow`, `WaitForProzessStartEintragAsync` — Timeout-Wartehilfen (`Short`/`Medium`/`Long` via `ElementWaitHelper`); Fehlerbanner-`FehlerMeldung` als Fail-Fast-Diagnose.
- `SkipWennConPtyNichtVerfuegbar` — `Skip.If(!ConPtyEnvironmentProbe.IsAvailable, ...)`; reagiert auf `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`.
- Setup-Hilfen: `CreateProject`/`OpenProject`/`CreateAndOpenProject`, `SetupProjectMitNeuerAufgabe(ForStartedApp)`, `NeueAufgabeAnlegen`, `AufgabeTitelSetzen`, `AufgabeDetailSpeichern`, `StartenUndPluginWaehlen` (Plugin-Auswahldialog inkl. „FuerProjektVerwenden"), `ConfigureLocalDirectoryPlugin`, `CreateLocalSourceDirectory` (git init), `AssignLocalDirectoryRepository`, `NavigateToSettings`, `OffeneAufgabenItems`, `AufgabeAusListeOeffnen`.
- `CredentialStoreSnapshot` + `ManagedCredentialKeys` — sichert/stellt OS-weite Windows-Credential-Store-Einträge um den Test herum wieder her; `DeleteTestDatabase`/`DeleteProzessStartLog` in `Dispose`.
- `AufzeichnenderProzessStarter`-Log (`ResolveProzessStartLogPfad`) — zeichnet gestartete Prozesse im Testmodus auf; `WaitForProzessStartEintragAsync` verifiziert echte Prozessstarts.

### `ElementWaitHelper`, `AppStartupLogInspector`, `ConPtyEnvironmentProbe`, `CredentialStoreSnapshot`, `LogSnapshot`
Dateien: `src/Softwareschmiede.Tests/E2E/` — Timeout-Konstanten und Polling (`ElementWaitHelper`), App-Log-Snapshot/Startup-Fehlerdiagnose (`AppStartupLogInspector`), ConPTY-Verfügbarkeitsprüfung inkl. Skip-Begründung (`ConPtyEnvironmentProbe`, Env-Var `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS`).

### `TestDbContextFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs` — erzeugt isolierte `SoftwareschmiededDbContext`-Instanzen (SQLite) für Unit-/Integrationstests; verwendet u. a. in `RateLimitDetectionServiceIntegrationTests`.
