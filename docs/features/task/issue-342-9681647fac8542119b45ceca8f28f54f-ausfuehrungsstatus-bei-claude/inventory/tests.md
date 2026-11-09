# Tests — Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-09, ca. 06:25–06:40 Uhr CET (UTC+01:00)
- Branch und Commit-ID: `task/issue-342-9681647fac8542119b45ceca8f28f54f-ausfuehrungsstatus-bei-claude` @ `a5b969531b8d644013c21d13eec6b9f0c70490a8` ("Backmerge from main to staging (#340)")
- Uncommittete Änderungen im getesteten Stand: nur untracked Feature-Dokumente unter `docs/features/task/issue-342-9681647fac8542119b45ceca8f28f54f-ausfuehrungsstatus-bei-claude/` (`requirement.md`, `todo.md`, `inventory/`); **keine Änderungen an Produktivcode oder Tests**
- Testumgebung und Runtime-/SDK-Versionen: Windows (DESKTOP-CM8OBSG), .NET SDK 10.0.403, xUnit.net VSTest Adapter 3.1.5, .NET 10.0.14; Agenten-Sandbox mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` und `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1` (siehe CLAUDE.md — bestätigte Sandbox-Limitationen, kein Code-Bug)
- Ermittelte Testsuiten und Quellen der Testbefehle: CLAUDE.md (stabile Lane `Category!=OsInterface`; OS-interface-Tests separat) und `.github/workflows/pr-staging-ci.yml` (beide Testprojekte, beide Kategorie-Filter). Vorab: `dotnet build Softwareschmiede.slnx -c Debug` erfolgreich (0 Warnungen, 0 Fehler, 8 s, [build-debug.log](test-results/build-debug.log)).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build Softwareschmiede.slnx -c Debug` | Repo-Root | 0 | — | — | — | [build-debug.log](test-results/build-debug.log) |
| Regular Tests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category!=OsInterface"` | Repo-Root | 0 | 2200 | 0 | 1 | [TRX](test-results/regular-tests/test-results-regular-tests.trx), [Console](test-results/regular-tests-console.log) |
| Regular IntegrationTests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category!=OsInterface"` | Repo-Root | 0 | 257 | 0 | 0 | [TRX](test-results/regular-integration/test-results-regular-integration.trx), [Console](test-results/regular-integration-console.log) |
| OsInterface Tests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category=OsInterface"` | Repo-Root | 0 | 51 | 0 | 19 | [TRX](test-results/os-interface-tests/test-results-os-interface-tests.trx), [Console](test-results/os-interface-tests-console.log) |
| OsInterface IntegrationTests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category=OsInterface"` | Repo-Root | 0 | 9 | 0 | 0 | [TRX](test-results/os-interface-integration/test-results-os-interface-integration.trx), [Console](test-results/os-interface-integration-console.log) |

### Nachgewiesene bestehende Testfehler

**Keine.** Alle 4 Testläufe endeten mit Exit-Code 0 ohne fehlgeschlagene Tests. Jeder spätere Fehlschlag ist daher als neu einzuordnen, sofern er nicht in dieser Baseline als übersprungen dokumentiert ist.

### Testlücken und Ausführungsprobleme

- **1 beabsichtigt übersprungener Test** (Regular Lane): `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — outcome `NotExecuted`, Grund: „Test gilt nur für Nicht-Windows-Betriebssysteme" (plattformbedingt, kein Defekt).
- **19 umgebungsbedingt übersprungene Tests** (OsInterface Lane, beide Skip-Mechanismen greifen wie vorgesehen):
  - 13× `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1` — sämtliche FlaUI-Fenster-Tests (`WpfE2ETests`, `ProjectDetailE2ETests`, `ScmEventsE2ETests`, `E2E_RepositoryManagementTests`, `E2E_RepositoryInitialisierungConfigTests`, `E2E_RepositoryInitialisierungAusfuehrungTests`, `End2EndTest.General_*`, `Simulator_Pluginwechsel*`, `ReplayText_*`).
  - 6× `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` — `End2EndTest.ConPty_LifecycleUndReplay_E2E`, `ConPty_WeitereLebenszyklusSzenarien_E2E`, `ConPty_SessionReattach_KeineDoppelausgabe_E2E`, `TerminalText_LiveMarkCopyAndKeepSelection_E2E` u. a.
  - **Für die Anforderung relevant:** Der einzige E2E zum gemeldeten Symptom (`SeitenleistenKachel_AktualisiertStatusAutomatisch_OhneManuellesNeuladen_E2E`, siehe unten) steckt im ConPTY-gebündelten `ConPty_WeitereLebenszyklusSzenarien_E2E` und ist in dieser Umgebung **nicht gelaufen** — ein eventueller E2E-Nachweis der Korrektur kann in dieser Sandbox nur als „Skipped" dokumentiert werden; der Lauf muss interaktiv (Visual Studio/lokale Session) erfolgen.
- **Keine Infrastruktur-/Build-Fehler** in den dokumentierten Läufen.

## Testklassen (betroffener Bereich)

### `CliProcessManagerTests_AktiverLauf` — `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests_AktiverLauf.cs`
- `OnCliProcessStatusChanged_WithGestartet_SetztAktiveRunIdSofort` — `Gestartet` → `AktiveRunId`/`LastHeartbeatUtc` sofort persistiert (Issue-108-Regression).
- `OnCliProcessStatusChanged_WithGestoppt_EntferntAktiveRunId` — `Gestoppt` → `AktiveRunId` entfernt.
- `OnCliProcessStatusChanged_WithFehler_EntferntAktiveRunId` — `Fehler` → `AktiveRunId` entfernt.

### `CliProcessManagerTests_LaufStatus` — `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests_LaufStatus.cs`
- `VollerZyklus_GestartetWartetGestoppt_PersistiertLaufStatusUeberDieSitzung` — voller Zyklus inkl. Fake-`PseudoConsoleSession` und `RuntimeStatusChanged` → `LaufStatus`.
- `RaisesRuntimeStatusChanged_AfterGestoppt_AktualisiertLaufStatusNichtMehr` — verspätete Events nach Stopp wirkungslos.

### `CliProcessManagerTests` — `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests.cs`
- `AktualisierungAsync_WithConcurrentTimerTicks_Serializes` / `..._DoesNotSerializeAcrossTasks` — Semaphore-Isolation des Heartbeats.
- `AktivenLaufSetzenAsync_WithDisposedScopeFactory_Completes`, `AktivenLaufBeendenAsync_WithDisposedScopeFactory_Completes`, `AktualisiereLaufStatusAsync_WithDisposedScopeFactory_Completes` — Shutdown-Race: disposed ScopeFactory wird geschluckt (deckt genau den „Fehler wird nur geloggt"-Pfad ab).

### `AufgabeServiceTests_AktiverLauf` — `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests_AktiverLauf.cs`
- `AktivenLaufSetzenAsync_ShouldSetAktiveRunIdAndHeartbeat_WhenCalled` — Setzt alle Laufdaten + `LaufStatus=Laeuft` + `AusfuehrungsStatus=Aktiv`.
- `AktivenLaufBeendenAsync_ShouldClearAktiveRunId_WhenPreviouslySet` / `..._ShouldClearLaufStatus_WhenPreviouslySetToWartetAufEingabe` — Rücksetzung aller Laufdaten.
- `AktualisiereLaufStatusAsync_ShouldSetWartetAufEingabe_WhenAktiveRunIdIsSet` — Substatus-Update.
- `AktualisiereLaufStatusAsync_ShouldNotSetLaufStatus_WhenAktiveRunIdIsNull` — **dokumentiert die No-Op-Semantik bei fehlender `AktiveRunId`** (relevanter Divergenz-Verstärker).
- `VollerZyklus_BereitLaeuftWartetBereit_PersistiertJedenSchrittKorrekt` — Service-Sicht des Vollzyklus.

### `AufgabeLaufAktivitaetTests` — `src/Softwareschmiede.Tests/Application/Services/AufgabeLaufAktivitaetTests.cs`
- `IstAktiv_ShouldReturn{True|False}_*` (5 Tests) — RunId+frischer Heartbeat → true; abgelaufener/exakt am Timeout/fehlender Heartbeat/fehlende RunId → false.

### `KiAusfuehrungsStatusConverterTests` — `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusConverterTests.cs`
- „▶ Läuft" bei `AktiveRunId`+frischem Heartbeat (Aufgabe und `AktiveAufgabePanelItem`).
- „⏸ Wartet" bei `Status=Wartend` bzw. `LaufStatus=WartetAufEingabe`.
- **„✓ Bereit" bei `AktiveRunId` gesetzt + Heartbeat > 5 min** (`..._WhenNoActiveRunOrHeartbeatExpired`) — exakt das H4-Bild.
- „✓ Bereit" bei `AusfuehrungsStatus=Beendet` trotz `Status=Wartend`; Pausiert-Countdown vor allen Zuständen; `ConvertBack` wirft.
- Hinweis: **Kein** Test für `AusfuehrungsStatus=Aktiv` + `AktiveRunId=null` (das „Aktiv ohne Run"-Fenster aus H1/F2/H5) — Lücke.

### `KiAusfuehrungsStatusSymbolConverterTests` / `KiAusfuehrungsStatusConverterTests_ZeitgesteuerterPrompt` — `src/Softwareschmiede.Tests/App/Converters/`
- Spiegelnde Glyph-Tests und `PromptInWartestellung`-Priorisierung.

### `MainWindowViewModelTests` — `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`
- `RunningCountChanged_ShouldReloadAktiveAufgabenListe_WhenRaised`, `LaufdatenChanged_ShouldReloadVisiblePanelItem_WhenRunDataWasPersisted`, `Dispose_ShouldUnsubscribeFromLaufdatenChanged` — Refresh-Verdrahtung.
- Diverse `AktiveAufgabenAktualisierenAsync_*`-Tests (Mapping inkl. `PausiertBisUtc`, `IsAktiv`-Marker, Plugin-Namen, Sortierung, `_refreshGate`-Skip).

### `TaskDetailViewModelTests` (+ `*_Pause`, `*_ZeitgesteuerterPrompt` u. a.) — `src/Softwareschmiede.Tests/App/ViewModels/`
- `ShowCliPanel_IsTrue_WhenStatusGestartet/Wartend/AusfuehrungBeendet` u. a. (~106 Tests) — Panel-/Command-Sichtbarkeit; `AusfuehrungAktivSetzenAsync` in Arrange (Z. 205). Kein Test, der Fußzeilen-Live-Status gegen Kachel-Persistenz prüft.

### `KiAusfuehrungsServiceTests` (OsInterface) — `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`
- `StartTerminalSessionAsync_*` inkl. `_SessionFailed_MeldetFehlerStattGestoppt`, `_FailedVorVerdrahtung_WirdErkanntUndAlsFehlerGemeldet`, `_ProzessVorVerdrahtungBeendet_MapptExitCodeAufStatus` — deckt die Pre-Wiring-/Failed-Pfade von H3 ab.
- `ProcessExited_ExitCode{Zero|NonZero}_PersistiertAusfuehrungBeendetOhneGesamtstatusZuBeenden`, `StopCliAsync_PersistiertAusfuehrungBeendetOhneGesamtstatusZuBeenden`.

### `End2EndTest` (FlaUI, `[OsInterface]`) — `src/Softwareschmiede.Tests/E2E/E2E_ArbeitsstatusAktualisierung.cs` + `MainTest.cs`
- `SeitenleistenKachel_AktualisiertStatusAutomatisch_OhneManuellesNeuladen_E2E` (aufgerufen in `ConPty_WeitereLebenszyklusSzenarien_E2E`, MainTest.cs Z. 250) — **einziger E2E zum selben Symptom (Issue #108)**: echter KiSimulator-CLI-Start → Kachel „▶ Läuft", Stopp → „✓ Bereit". Deckt **nur den erfolgreichen `Gestartet`-Pfad** ab — kein Session-Reuse, kein Persist-Fehler, kein Heartbeat-Abbruch (H1–H5 alle ungetestet auf E2E-Ebene). Historischer Hinweis im Dateikopf: Eine frühere Fassung simulierte den Heartbeat per SQL und war grün, obwohl `AktiveRunId` nirgends gesetzt wurde — selbes Missverhältnis wie jetzt auf Metaebene.
- `AufgabeOeffnen_NachStoppen_StartetCliNichtAutomatischErstExplizit_E2E` — Wiederanbindung an beendete CLI (benachbarter Bereich).

## Hilfsmethoden

### `TestKiAusfuehrungsServiceFactory` — `src/Softwareschmiede.Tests/Helpers/`
- `Create(scopeFactory?)` — erzeugt `KiAusfuehrungsService` mit Test-`ITerminalSessionFactory` für Session-Starts ohne echtes ConPTY.

### `TestPseudoConsoleSessionFactory` — `src/Softwareschmiede.Tests/Helpers/`
- `Create(input, output, timeProvider, waitingThreshold, logger)` — `PseudoConsoleSession` mit gesteuerten Streams und kurzer Warte-Schwelle für deterministische `RuntimeStatus`-Tests.

### `TestDbContextFactory` — `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs`
- `Create()` — EF-Core-InMemory-`SoftwareschmiededDbContext` für `AufgabeService`-Tests.

### `ScmDispatchFixture` — `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` (gelinkt in IntegrationTests)
- SQLite-basierte Integrationstest-Fixture für SCM-/Aufgaben-Pfade.

### `WpfTestBase` / `TaskDetailView` (E2E-Views) — `src/Softwareschmiede.Tests/E2E/`
- `LaunchApp` (zentral `SkipWennWpfE2ENichtVerfuegbar`), `SkipWennConPtyNichtVerfuegbar`, `SetupProjectMitNeuerAufgabe`, `StartenUndPluginWaehlen`, `WaitForTaskStatus(titel, status, timeout)` — E2E-Helfer für Aufgaben-Kachel-Assertions.

## Zusammenfassung der Abdeckungslücken für diese Anforderung

1. Kein Unit-/Integrations-Test simuliert den Fall „Session/Prozess lebt, `Gestartet`-Ereignis ausgeblieben (Early-Return)" → keine Assertion „Kachel darf nicht `Bereit` zeigen".
2. Kein Test deckt `ExecuteWithAufgabeServiceAsync`-Fehlschlag außer `ObjectDisposedException` ab (Retry-/Selbstheilungs-Erwartung nicht existent).
3. Kein Test für `StartCliAndUpdateStateAsync`/`CliNeustartenCoordinatedAsync`-Early-Return („laufende Session wiederverwendet → persistierte Laufdaten unverändert").
4. Kein Test für `CliStoppenAsync` bei nicht beendbarem Prozess (F1).
5. Kein Test für `Klassifiziere` mit `AusfuehrungsStatus=Aktiv` + `AktiveRunId=null` (explizites „Aktiv ohne Run"-Bild).
6. E2E-Abdeckung nur für den Happy-Path-Start/Stopp (KiSimulator), keine Re-Attach-/Wiederverwendungsszenarien; in dieser Sandbox zudem nicht ausführbar.
