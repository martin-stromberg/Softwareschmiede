# Tests — Replay-Geometrie

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-10-01, ca. 19:12–19:31 Uhr MESZ (UTC+2; Zeitzone „W. Europe Standard Time")
- **Branch und Commit-ID:** `task/konsolentestfenster-replay-geometrie`, Commit `6d43f596965e84d152025e8a995918e90fdbeb11` (2026-09-28 05:02:38 +0200, „feat: Konsolentestfenster-Schrittmodus — Rest-Race im CAS-Fail-Pfad von WiedergabeStarten behoben")
- **Uncommittete Änderungen im getesteten Stand:** keine Änderungen an getrackten Dateien (`git status`: clean); untracked: `.agents/` (Lifecycle-Skills), `Screenshot-Doppelung-Bei-Ausgabe.png`, `Screenshot-Zeilenumbruch-Terminal.png`, `Screenshot-Zeilenumbruch.png`, `cli-replay-a255276dd4e848039bab82ed51400729.clireplay` (14 MB Referenz-Aufzeichnung, Header verifiziert: **220×50, 486051 Chunks, `IstVollstaendig=false`, „Devin CLI"**), `docs/features/task/konsolentestfenster-replay-geometrie/` (Anforderungs-/Inventardateien inkl. der hier abgelegten TRX-Reports). Getesteter Stand entspricht damit dem Commit-Inhalt.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows (WPF-Desktop), .NET SDK `10.0.401`, Test-Runtime `.NET 10.0.12`, xUnit.net VSTest-Adapter 3.1.5. Alle Testläufe mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (Sandbox-Regel laut `CLAUDE.md`: ConPTY-Kindprozesse werden unter `dotnet test` in dieser Sandbox nicht isoliert — betroffene Tests werden sauber übersprungen statt mit Timeout zu scheitern).
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Zwei Testprojekte, je zwei Kategorie-Lanes — Quelle: `.github/workflows/pr-staging-ci.yml` und `CLAUDE.md` (stabile Lane = `Category!=OsInterface`; `Category=OsInterface` separat, enthält E2E/FlaUI, ConPTY, Clipboard u. a.):
  - `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` (`net10.0-windows10.0.17763.0`, xUnit + FlaUI; referenziert `Softwareschmiede.App.csproj`)
  - `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` (`net10.0`; referenziert nur `Softwareschmiede` + `Softwareschmiede.Plugin.LocalDirectory`)

### Build-Situation (wichtig)

- `dotnet build Softwareschmiede.slnx -c Debug` **schlägt fehl** mit MSB3026-Wiederholungen → MSB3027/MSB3021: `Softwareschmiede.dll` und `Softwareschmiede.Plugin.Contracts.dll` können nicht nach `src\Softwareschmiede.App\bin\Debug\net10.0-windows10.0.17763.0\` kopiert werden — gesperrt durch **„Microsoft Visual Studio (PID 18668), Softwareschmiede.App (PID 36992)"** (die laufende App wurde um 06:26 Uhr aus dem Repo-`bin\Debug` gestartet; nach Self-Hosting-Regel wurde sie **nicht** beendet). Reiner Infrastruktur-/Sperrfehler, kein Codefehler.
- Teil-Build `dotnet build src/Softwareschmiede/Softwareschmiede.csproj -c Debug` **erfolgreich** (0 Warnungen, 0 Fehler).
- `dotnet build Softwareschmiede.slnx -c Release` **erfolgreich** (0 Fehler, 1 bekannte Warnung `CS8602` in `src/Softwareschmiede.Tests/Application/Services/CliOutputProtokollWriterTests.cs` Z. 151, ~13 s).
- **Konsequenz:** Alle vier Testlanes wurden in `-c Release` ausgeführt (statt des üblichen Debug). Da `Softwareschmiede.Tests` das App-Projekt referenziert, wäre ein Debug-`dotnet test` am selben Lock gescheitert.
- **E2E-Besonderheit:** `WpfTestBase.ResolveAppExePath` (`WpfTestBase.cs` Z. 941–985) bevorzugt die **Debug**-App.exe vor Release — die OsInterface-E2E-Tests des Release-Laufs fuhren daher gegen die vorhandene Debug-App (Binaries vom 01.10. 06:26 = HEAD-Stand, da Arbeitsbaum clean). Für gesperrte-bin-Szenarien existiert zusätzlich der Override `SOFTWARESCHMIEDE_E2E_APP_PATH` (Z. 943–954).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build Debug | `dotnet build Softwareschmiede.slnx -c Debug` | Repo-Root | 1 | — | **Build-Fehler** (MSB3027×2, MSB3021×2 — Dateisperre, siehe oben) | — | Konsolenlog (kein TRX) |
| Build Release | `dotnet build Softwareschmiede.slnx -c Release` | Repo-Root | 0 | — | — | — | Konsolenlog (kein TRX) |
| 1 Regulär | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Release --filter "Category!=OsInterface" --results-directory docs/features/task/konsolentestfenster-replay-geometrie/inventory/test-results --logger "trx;LogFileName=test-results-regular-tests.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 1876 | 0 | 1 | [TRX](test-results/test-results-regular-tests.trx) |
| 2 Regulär Integration | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Release --filter "Category!=OsInterface" --results-directory docs/features/task/konsolentestfenster-replay-geometrie/inventory/test-results --logger "trx;LogFileName=test-results-regular-integration.trx" --logger "console;verbosity=minimal"` | Repo-Root | 0 | 69 | 0 | 0 | [TRX](test-results/test-results-regular-integration.trx) |
| 3 OsInterface | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Release --filter "Category=OsInterface" --results-directory docs/features/task/konsolentestfenster-replay-geometrie/inventory/test-results --logger "trx;LogFileName=test-results-os-interface-tests.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 53 | 0 | 2 | [TRX](test-results/test-results-os-interface-tests.trx) |
| 4 OsInterface Integration | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Release --filter "Category=OsInterface" --results-directory docs/features/task/konsolentestfenster-replay-geometrie/inventory/test-results --logger "trx;LogFileName=test-results-os-interface-integration.trx" --logger "console;verbosity=minimal"` | Repo-Root | 0 | 9 | 0 | 0 | [TRX](test-results/test-results-os-interface-integration.trx) |

Hinweis zu Lauf 3: `End2EndTest.RunGeneralTests` lief **8 m 37 s** und bestand — darin ist das konsolidierte Konsolentestfenster-Szenario `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` enthalten (`MainTest.cs` Z. 36), das den kompletten Wiedergabe-Pfad (Laden → Abspielen → Schrittmodus → Pausieren/Fortsetzen → Neustart → Ende → Quell-Liste) gegen die echte App verifiziert.

### Nachgewiesene bestehende Testfehler

**Keine** — alle vier Lanes liefen ohne Fehlschlag. Der Debug-Build-Fehler ist ein Infrastruktur-/Sperrproblem (laufende App-Instanz + Visual Studio), kein Testfehler; die Release-Läufe decken denselben Quellstand ab.

### Testlücken und Ausführungsprobleme

Übersprungene Tests (alle absichtlich/mechanisch begründet, keine Infrastrukturfehler):

| Test | Lane | Grund |
|------|------|-------|
| `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` | 1 (regulär) | `SkippableFact` mit `Skip.If(OperatingSystem.IsWindows())` — prüft den Nicht-Windows-Pfad, auf diesem Windows-Host per Design übersprungen |
| `Softwareschmiede.Tests.E2E.End2EndTest.RunConPtyTests` | 3 (OsInterface) | `SkipWennConPtyNichtVerfuegbar()` via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (`WpfTestBase.cs` Z. 922–925 → `ConPtyEnvironmentProbe.IsAvailable`, `ConPtyEnvironmentProbe.cs` Z. 30) — bündelt 21 ConPTY-abhängige E2E-Szenarien, darunter `ConPtyLifecycle_*` in `E2E_ConPtyLifecycle.cs`, das den Export einer echten `.clireplay` und deren Wiedergabe im Konsolentestfenster Ende-zu-Ende verifiziert; in dieser Sandbox nicht ausführbar, in CI/interaktiver Session regulär grün |
| `Softwareschmiede.Tests.E2E.E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` | 3 (OsInterface) | Ebenfalls `SkipWennConPtyNichtVerfuegbar()` — startet echte CLI-Prozesse |

Damit: **1877 + 69 + 55 + 9 = 2010 entdeckte Tests**; davon 2007 bestanden, 0 fehlgeschlagen, 3 übersprungen.

**Konfigurationsabweichung:** Alle Läufe in Release statt Debug (siehe Build-Situation) — kein Hinweis auf konfigurationsabhängiges Testverhalten; die Ergebnisse sind als Ausgangsbasis verwertbar, ein Debug-Nachweis steht bis zur Auflösung der Dateisperre aus.

**Für die Anforderung fehlende Abdeckung:** Es existiert kein Test, der die Geometrie der Wiedergabe gegen die Control-Größe prüft — weder dass `OnSessionChanged`/`OnRenderSizeChanged` den Buffer einer Replay-Session verkleinern, noch `ExtentWidth`/`HorizontalOffset`/horizontale Scroll-Methoden (alle heute Stubs), noch die Sichtbarkeit der horizontalen Scrollbar im Dialog.

## Testklassen (bestehend, anforderungsrelevant)

### `TerminalReplaySessionTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/TerminalReplaySessionTests.cs` (841 Zeilen, 22 Facts) — `FakeTimeProvider`-Muster, `CreateAufzeichnung`-Helfer (Z. 791–803: `Cols = 80, Rows = 24`-Fixture), `WarteBisAsync`-Polling, `BufferAlsText`-Nachweis.

- `Wiedergabe_ErzeugtGleichenBufferWieLiveSession` (Z. 17) — dieselbe Chunk-Sequenz durch `PseudoConsoleSession` und Replay-Session ergibt denselben Buffer (Renderpfad-Parität).
- `Wiedergabe_ZeitrafferVerkuerztRealePausen` (Z. 56), `Pausieren_StopptChunkAnwendung_FortsetzenSetztFort` (Z. 90), `RebuildBufferFromReplay_BautBufferAusGespieltemPraefix` (Z. 129), `Wiedergabe_AmEnde_FeuertExitedUndWechseltAufInaktiv` (Z. 152), `Wiedergabe_OutputChunkEvent_LiefertRohbytes` (Z. 173).
- **`Stubs_SindUnschaedlich` (Z. 197–224)** — dokumentiert den heutigen No-Op-Vertrag: `Resize(10,10)` und `Resize(0,-3)` liefern beide `true` („Resize ist ein Stub und darf nie fehlschlagen", Z. 220–221) — **bei geänderter Resize-/Geometrie-Semantik anzupassen**.
- `WiedergabeStarten_ZweiterAufruf_IstNoOp` (Z. 229), `Dispose_WaehrendWiedergabe_BrichtAb` (Z. 253), `Dispose_MitZeitrafferNull_BrichtAbStattChunksZuDraenen` (Z. 278).
- Schrittmodus-Block (Z. 329–723): `SchrittVor_*` (329/352/376), `SchrittZurueck_*` (407/437/462), `Pausiert_Schritte_Fortsetzen_SetztAnSchrittpositionFort` (480), `SchrittVor_BisEndeBeiPausierterSchleife_TerminiertSauber` (536), `SchrittZurueck_ImExitedHandler_TerminiertAufgeweckteSchleife` (587), `WiedergabeStarten_NachEndeUndSchrittZurueck_*` (633), `WiedergabeStarten_GegenVerurteilteSchleife_*` (677), `Schritte_NachDispose_SindNoOp` (723). `SchrittZurueck_*`-Tests belegen den Präfix-Rebuild — relevant, weil `Buffer.Reset()` die Geometrie beibehält.

### `TerminalControlTests` (+ `.ClipboardPaste`)
Dateien: `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs` (616 Zeilen), `TerminalControlTests.ClipboardPaste.cs` (364 Zeilen) — **WPF-Controls werden echt gehostet** via `WpfUnitTestHelpers.RunOnSta`; Sessions sind echte `PseudoConsoleSession`-Instanzen via `TestPseudoConsoleSessionFactory` auf selbstgebauten Streams (`ImmediateEofStream`, `FixedContentStream`, `ControllableStream`, `WriteThrowingStream`).

- `OnSessionChanged_RegistersBufferChangedHandler` (Z. 69), `_ToNewSession_DeregistersOldHandler` (Z. 91), `_ToNull_DeregistersAllHandlers` (Z. 119), `ParallelSessions_NoBufferInterference` (Z. 141), `SessionSwitch_BackToPreviousSession_PreservesBuffer` (Z. 170), `OnSessionChanged_ReattachedSession_KeineDoppelteAusgabe` (Z. 199).
- **IScrollInfo-Block (nur vertikal):** `ScrollInfo_AlternateScreen_KeinScrollbackKeinOffset` (Z. 237), `ScrollInfo_LangerVerlauf_MeldetExtentGroesserAlsViewport` (Z. 276), `ScrollInfo_SetVerticalOffsetUndNavigation_KlemmenOffset` (Z. 295), `ScrollInfo_NeueAusgabeAmEnde_FolgtNeuemEnde` (Z. 327), `ScrollInfo_ManuellNachOben_NeueAusgabeErhaeltOffset` (Z. 350), `OnSessionChanged_SetztScrollzustandAufEnde` (Z. 372).
- **`ScrollViewerLayout_CanContentScroll_BegrenztViewportAufSichtbareHoehe` (Z. 396–427)** — Referenzmuster für Geometrie-Tests: `TerminalControl` in echtem `ScrollViewer` (`CanContentScroll`, `VerticalScrollBarVisibility=Auto`, `HorizontalScrollBarVisibility=Disabled`) mit `Measure`/`Arrange`/`UpdateLayout` arrangiert; prüft `ScrollOwner`-Zuordnung und dass `session.Buffer.Rows` dem sichtbaren Bereich folgt (3–5 bei 160×64) — belegt indirekt das heutige Resize-auf-Control-Verhalten.
- Hilfsmethoden: `CreateArrangedControl` (160×48, Z. 469), `InvokeUpdateScrollInfo` (Reflection, Z. 478), `WriteLines` (Z. 484), `RaiseOutputAndCountDispatcherOperations` (Z. 438), `SetLogger` (Z. 463), `BufferText` (Z. 265).
- `.ClipboardPaste`: 10 Facts zum Ctrl+V-/Clipboard-Pfad — unabhängig von der Geometrie.

### `KonsolenTestViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/KonsolenTestViewModelTests.cs` (696 Zeilen) — `Mock<IDialogService>`, echter `CliReplayAufzeichnungStore` auf Temp-Dateien, `FakeTimeProvider`, synchroner `dispatcherInvoke: action => action()`.

- Laden: `AufzeichnungOeffnen_LaadtSessionUndQuellEintraege` (Z. 87), `_UnvollstaendigeAufzeichnung_ZeigtHinweis` (Z. 114), `_KorrupteDatei_ZeigtFehlerMeldung` (Z. 127), `_DialogAbgebrochen_KeinZustandswechsel` (Z. 143).
- Wiedergabe: `Wiedergabe_LaeuftBisEnde_UndSynchronisiertQuellAnsicht` (Z. 158), `Wiedergabe_PausierenUndFortsetzen` (Z. 184), `Wiedergabe_NachEnde_StartetErneutAbPosition0` (Z. 239), `Wiedergabe_NeustartAusPausiertemLauf_SpieltErneutAbPosition0` (Z. 341).
- Zeitraffer: `ZeitrafferSchwelleText_ValidiertUndWirktLive` (Z. 269), Theory `..._WerdenAbgewiesen` (Z. 299), `..._UebernimmtLetzteGueltigeSchwelleAufFrischeSession` (Z. 317).
- Schrittmodus: `SchrittVor_SchrittZurueck_SynchronisierenPositionUndQuellAuswahl` (Z. 439), `SchrittCommands_CanExecute_NachZustand` (Z. 475), `SchrittCommands_Execute_WaehrendLaufenderWiedergabe_SindNoOp` (Z. 532), `WiedergabeNeustarten_AusSchrittmodus_SpieltAbPosition0` (Z. 574), `SchrittVor_BisEnde_ZeigtStatusBeendet` (Z. 611), `Pausiert_Schritt_Fortsetzen_ViewModelEbene` (Z. 644).
- `Schliessen_LoestCloseRequestedAus` (Z. 397), `Commands_OhneAufzeichnung_SindInert` (Z. 416).

### `CliReplayAufzeichnungStoreTests` / `CliOutputRecorderTests` / `KiAusfuehrungsServiceCliAufzeichnungTests` / `CliChunkQuelltextFormatterTests`
- `CliReplayAufzeichnungStoreTests` (`src/Softwareschmiede.Tests/Infrastructure/Terminal/`): Roundtrip + Formatvalidierung inkl. Geometrie-Header (`cols <= 0` → `InvalidDataException` — Pfad der `> 0`-Garantie).
- `CliOutputRecorderTests`: Budget/Präfix/`IstVollstaendig`-Semantik.
- `KiAusfuehrungsServiceCliAufzeichnungTests` (`src/Softwareschmiede.Tests/Application/Services/`): Recorder-Verdrahtung (`DefaultCols`/`DefaultRows` → Header).
- `CliChunkQuelltextFormatterTests` (`src/Softwareschmiede.Tests/App/Services/`): Steuerzeichen-Sichtbarmachung der Quell-Liste.

### `TerminalBufferTests` / `AnsiSequenceParserTests`
- `TerminalBufferTests` (`src/Softwareschmiede.Tests/Domain/Terminal/`): `Apply`-Semantik, `Reset`, **`Resize`** (Inhaltserhalt, Scrollback-Verhalten), Alternate Screen — der `Resize`-Vertrag bleibt laut Anforderung unverändert.
- `AnsiSequenceParserTests` (`src/Softwareschmiede.Tests/Infrastructure/Terminal/`): CSI/SGR/UTF-8/`Reset()`.

### `End2EndTest` (E2E, FlaUI)
Dateien: `src/Softwareschmiede.Tests/E2E/MainTest.cs` + partielle Szenario-Dateien — `[Trait("Category", "E2E")]`, `[OsInterface]`, `[Collection("E2E")]`, Basisklasse `WpfTestBase`.

- `RunGeneralTests` (Z. 18–67): ohne ConPTY; enthält `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` (Z. 36) — in Lauf 3 bestanden (8 m 37 s Gesamtlauf).
- `RunConPtyTests` (Z. 73–103): `[SkippableFact]` + `SkipWennConPtyNichtVerfuegbar()` — in dieser Sandbox übersprungen; enthält `ConPtyLifecycle_...` mit echtem `.clireplay`-Export + Wiedergabe.

`KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` (`src/Softwareschmiede.Tests/E2E/E2E_KonsolenTestfenster.cs` Z. 30–285): konsolidiertes Szenario — erzeugt vier synthetische `.clireplay`-Dateien via `CliReplayAufzeichnungStore` (alle **`Cols = 80, Rows = 24`** — Z. 53–54, 74–75, 96–97; eine Aufzeichnung mit deutlich mehr Cols als Dialogbreite existiert **nicht**), öffnet den Dialog über `SettingsView.OpenKonsolenTestDialog`, deckt Dialog-Abbruch, Formatfehler, Schrittmodus (drei Phasen), Pausieren→Fortsetzen an einer 3-s-Inter-Chunk-Pause, erneutes Abspielen, Neustart aus Pausiert und Zeitraffer 0 ab. `TryCloseKonsolenTestfenster` (Z. 289–307) schließt fehlertolerant im `finally`. **Expliziter Code-Kommentar (Z. 127–129): der gerenderte Terminalinhalt ist über UI-Automation nicht lesbar** (kein TextPattern am custom gerenderten `TerminalControl`) — ein Geometrie-Nachweis muss über andere Beobachtbare laufen (z. B. horizontale Scrollbar/Sichtbarkeit oder ein TextElement mit `AutomationProperties.Name`); `KonsolenTestDialogView` hat derzeit **keinen** Scrollbar-/ScrollPattern-Leser.

### Sonstige Befunde
- `TempReplayDiagnoseTests.cs` (in der Anforderung als untracked erwähnt): **nicht vorhanden** im Arbeitsbaum (`find src -name "*TempReplay*"` leer, `git status` zeigt es nicht) — nichts zu entfernen.
- `E2E_TerminalFallbackDiagnose.cs` existiert (`TerminalFallbackDiagnose_PipeFallbackUndRequiresPtyFehler_E2E` in `RunGeneralTests` Z. 34) — unabhängig von der Geometrie.

## Hilfsmethoden / Test-Doubles

### `KonsolenTestDialogView` (E2E-Wrapper)
Datei: `src/Softwareschmiede.Tests/E2E/Views/Dialogs/KonsolenTestDialogView.cs` (375 Zeilen), Basisklasse `DialogView` (Titel „Konsolentest").

| Methode | Zweck |
|---------|-------|
| `OeffneAufzeichnung(pfad)` / `OeffneAufzeichnungAbbrechen()` | Nativen `OpenFileDialog` deterministisch bedienen (ESC/RETURN-Handling der Autocomplete-Liste, Z. 34–81) |
| `IstFehlerSichtbar()` / `GetFehlerMeldung()` / `WarteAufFehlerSichtbar()` | Fehlerbanner via `IsOffscreen`/`HelpText` (Z. 86–128) |
| `SetZeitrafferSchwelle(text)` | Zeitraffer-Textbox (Z. 133–138) |
| `StartWiedergabe()` / `NeustartWiedergabe()` / `PausierenToggle()` / `SchrittVor()` / `SchrittZurueck()` | Button-Klicks per `WaitForEnabledElement` + `ClickInForeground` (Z. 144–187) |
| `IstSchaltflaecheAktiviert(name)` | Generischer Enabled-Check per Automation-Name (Z. 193–197) |
| `GetStatusText()` / `GetPositionsText()` / `WarteAufStatus` / `WarteAufPosition` | Status-/Positionstext über `HelpText` mit Polling (Z. 218–268) |
| `GetSelektierterQuellEintragIndex()` / `GetQuellEintraegeCount()` / `GetQuellEintragText(index)` / `WarteAufQuellEintraege(min)` | Quell-Chunk-Liste (Z. 201–315) |
| `Schliessen()` | Invoke-Pattern + `WaitUntilGone` (Z. 321–326) |
| `WaitForEnabledElement(parent, name, timeout)` | privat statisch — Element suchen UND `IsEnabled` abwarten (Z. 355–368) |

Basisklasse `DialogView` (`src/Softwareschmiede.Tests/E2E/Views/DialogView.cs`): `GetDialogWindow`, `DialogWindowCondition` (Name + ControlType.Window), `ForceShow`/`ForceClose`, `MatchesOpenWindow`.

### `WpfTestBase` / `ConPtyEnvironmentProbe` (E2E-Infrastruktur)
- `LaunchApp` (`WpfTestBase.cs` Z. ~170–207): startet `Softwareschmiede.App.exe` mit `SOFTWARESCHMIEDE_TEST_DB_PATH` (Test-SQLite; erzwingt Pipe-Backend via `SimulatedPseudoConsoleProcessLauncher`).
- `ResolveAppExePath` (Z. 941–985): Debug-App.exe zuerst, Release als Fallback; Override `SOFTWARESCHMIEDE_E2E_APP_PATH` (Z. 943–954) — dokumentierter Escape-Hatch genau für das gesperrte-bin-Szenario dieser Inventory.
- `SkipWennConPtyNichtVerfuegbar` (Z. 922–925) → `ConPtyEnvironmentProbe.IsAvailable` (nur Env-Variable, keine Auto-Erkennung).
- `WarteAufEchtesHauptfenster`, `GetLatestAppLogContent`/`CheckAppStartupException` (Z. 995–1001 — App-Startup-Log-Auswertung bei E2E-Timeouts).

### Weitere Unit-Test-Helfer
- `FakeTimeProvider` (`Microsoft.Extensions.Time.Testing`): steuert `Task.Delay`-Pausen der `WiedergabeLoopAsync` deterministisch.
- `TestPseudoConsoleSessionFactory` (`src/Softwareschmiede.Tests/Helpers/`): echte `PseudoConsoleSession` auf MemoryStreams (`NullPseudoConsoleHandle`, `Process.GetCurrentProcess()`), optional `TerminalSessionOptions` (Default-Geometrie 220×50).
- `TestTerminalSessionFactory` (`src/Softwareschmiede.Tests/Helpers/`): `ITerminalSessionFactory`-Double mit `DeterministicPseudoConsoleProcessLauncher`/`ChunkEmittingPseudoConsoleProcessLauncher`.
- `WpfUnitTestHelpers.RunOnSta` (`src/Softwareschmiede.Tests/Helpers/WpfUnitTestHelpers.cs` Z. 14–32): STA-Thread + `DispatcherSynchronizationContext` für WPF-Unit-Tests; `TestKeyboardDevice` für synthetische `KeyEventArgs`.
- `ChunkedStream` (privat in `TerminalReplaySessionTests`): liefert pro Lesevorgang genau einen Chunk.
- `OsInterfaceAttribute`/`OsInterfaceFactAttribute` (`src/Softwareschmiede.Tests/Infrastructure/Testing/`): markieren `Category=OsInterface`.
