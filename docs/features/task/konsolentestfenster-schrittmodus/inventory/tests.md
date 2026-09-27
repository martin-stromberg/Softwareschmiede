# Tests — Konsolentestfenster Schrittmodus

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-09-27, ca. 23:36–23:52 Uhr MESZ (UTC+2; Rechner `DESKTOP-CM8OBSG`)
- **Branch und Commit-ID:** `task/konsolentestfenster-schrittmodus`, Commit `e67e1fbb77958e0c4d6709c7da6bfee93054fda9` (2026-09-26 21:21:26 +0200, „fix: Konsolentestfenster-Replay Nacharbeiten (Review-/Usability-Befunde)")
- **Uncommittete Änderungen im getesteten Stand:** keine Änderungen an getrackten Dateien (`git status`: clean); untracked: `.agents/` (Lifecycle-Skills) und `docs/features/task/konsolentestfenster-schrittmodus/` (Anforderungs-/Inventardateien inkl. der hier abgelegten TRX-Reports). Getesteter Stand entspricht damit dem Commit-Inhalt.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows (WPF-Desktop), .NET SDK `10.0.401`, Test-Runtime `.NET 10.0.12`, xUnit.net VSTest-Adapter 3.1.5. Alle Testläufe mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (Sandbox-Regel laut `CLAUDE.md`: ConPTY-Kindprozesse werden unter `dotnet test` in dieser Sandbox nicht isoliert — betroffene Tests werden sauber übersprungen statt mit Timeout zu scheitern).
- **Ermittelte Testsuiten und Quellen der Testbefehle:** Zwei Testprojekte, je zwei Kategorie-Lanes — Quelle: `.github/workflows/pr-staging-ci.yml` (Z. 121–146) und `CLAUDE.md` (stabile Lane = `Category!=OsInterface`; `Category=OsInterface` separat, enthält E2E/FlaUI, ConPTY, Clipboard u. a.):
  - `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` (`net10.0-windows10.0.17763.0`, xUnit + FlaUI)
  - `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` (`net10.0`)
  - Vor allen Testläufen: `dotnet build Softwareschmiede.slnx -c Debug` — **erfolgreich** (0 Fehler, 1 Warnung `CS8602` „Dereferenzierung eines möglichen Nullverweises" in `src/Softwareschmiede.Tests/Application/Services/CliOutputProtokollWriterTests.cs` Z. 151, Dauer ~15 s — bereits in der Vorgänger-Bestandsaufnahme dokumentiert).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build Softwareschmiede.slnx -c Debug` | Repo-Root | 0 | — | — | — | Konsolenlog (kein TRX) |
| 1 Regulär | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category!=OsInterface" --results-directory docs/features/task/konsolentestfenster-schrittmodus/inventory/test-results --logger "trx;LogFileName=test-results-regular-tests.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 1858 | 0 | 1 | [TRX](test-results/test-results-regular-tests.trx) |
| 2 Regulär Integration | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category!=OsInterface" --results-directory docs/features/task/konsolentestfenster-schrittmodus/inventory/test-results --logger "trx;LogFileName=test-results-regular-integration.trx" --logger "console;verbosity=minimal"` | Repo-Root | 0 | 69 | 0 | 0 | [TRX](test-results/test-results-regular-integration.trx) |
| 3 OsInterface | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category=OsInterface" --results-directory docs/features/task/konsolentestfenster-schrittmodus/inventory/test-results --logger "trx;LogFileName=test-results-os-interface-tests.trx" --logger "console;verbosity=normal"` | Repo-Root | 0 | 53 | 0 | 2 | [TRX](test-results/test-results-os-interface-tests.trx) |
| 4 OsInterface Integration | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category=OsInterface" --results-directory docs/features/task/konsolentestfenster-schrittmodus/inventory/test-results --logger "trx;LogFileName=test-results-os-interface-integration.trx" --logger "console;verbosity=minimal"` | Repo-Root | 0 | 9 | 0 | 0 | [TRX](test-results/test-results-os-interface-integration.trx) |

Hinweis zu Lauf 3: `End2EndTest.RunGeneralTests` lief **8 m 30 s** und bestand — darin ist das konsolidierte Konsolentestfenster-Szenario `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` enthalten (`MainTest.cs` Z. 36), das den kompletten Wiedergabe-Pfad (Laden → Abspielen → Pausieren → Fortsetzen → Neustart → Ende → Quell-Liste) gegen die echte App verifiziert.

### Nachgewiesene bestehende Testfehler

**Keine** — alle vier Lanes liefen ohne Fehlschlag (im Unterschied zur Vorgänger-Bestandsaufnahme trat das dort dokumentierte instabile Clipboard-Verhalten `TerminalControlTests.ReadClipboardAndInsertAsync_SessionWechseltWaerendPaste_SchreibtInSnapshotSession` in diesem Lauf nicht auf; der Test bestand in Lauf 3).

### Testlücken und Ausführungsprobleme

Übersprungene Tests (alle absichtlich/mechanisch begründet, keine Infrastrukturfehler):

| Test | Lane | Grund |
|------|------|-------|
| `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` | 1 (regulär) | `SkippableFact` mit `Skip.If(OperatingSystem.IsWindows())` — prüft den Nicht-Windows-Pfad, auf diesem Windows-Host per Design übersprungen |
| `Softwareschmiede.Tests.E2E.End2EndTest.RunConPtyTests` | 3 (OsInterface) | `SkipWennConPtyNichtVerfuegbar()` via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (`WpfTestBase.cs` Z. 922–925) — bündelt 21 ConPTY-abhängige E2E-Szenarien, darunter `ConPtyLifecycle_*` in `E2E_ConPtyLifecycle.cs` (Z. 80–110), das den Export einer echten `.clireplay` und deren Wiedergabe im Konsolentestfenster Ende-zu-Ende verifiziert; in dieser Sandbox nicht ausführbar, in CI/interaktiver Session regulär grün |
| `Softwareschmiede.Tests.E2E.E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` | 3 (OsInterface) | Ebenfalls `SkipWennConPtyNichtVerfuegbar()` — startet echte CLI-Prozesse |

Damit: **1859 + 69 + 55 + 9 = 1992 entdeckte Tests**; davon 1989 bestanden, 0 fehlgeschlagen, 3 übersprungen. Der OsInterface-Lauf deckt den echten `Win32PseudoConsoleProcessLauncher`/ConPTY-Pfad mangels Sandbox-Isolierung nicht ab (E2E fährt bewusst auf dem simulierten Pipe-Backend über `SimulatedPseudoConsoleProcessLauncher`, erzwungen durch `SOFTWARESCHMIEDE_TEST_DB_PATH`).

Für den Schrittmodus existieren noch keine Tests (keine `SchrittVor`/`SchrittZurueck`-Member, keine Schritt-Commands/Buttons — siehe logic.md).

## Testklassen (bestehend, anforderungsrelevant)

### `TerminalReplaySessionTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/TerminalReplaySessionTests.cs` (404 Zeilen) — **direkt erweiterbar für Schritt-Unit-Tests**; `FakeTimeProvider`-Muster, `CreateAufzeichnung`-Helfer, `WarteBisAsync`-Polling, `ZeilenText`/`BufferAlsText`-Nachweise.

- `Wiedergabe_ErzeugtGleichenBufferWieLiveSession` (Z. 17) — dieselbe Chunk-Sequenz durch `PseudoConsoleSession` (via `TestPseudoConsoleSessionFactory` + `ChunkedStream`) und Replay-Session ergibt denselben Buffer.
- `Wiedergabe_ZeitrafferVerkuerztRealePausen` (Z. 56) — `FakeTimeProvider.Advance` steuert die Inter-Chunk-Pausen.
- `Pausieren_StopptChunkAnwendung_FortsetzenSetztFort` (Z. 90) — Pausieren/Fortsetzen-Semantik inkl. `RuntimeStatus = Laeuft` im Pausiert-Zustand.
- `RebuildBufferFromReplay_BautBufferAusGespieltemPraefix` (Z. 126) — **direkter Vorbild-Test für den Rückwärtsschritt**: Buffer wird explizit zerstört (`Buffer.Reset()`) und nur aus dem gespielten Präfix neu aufgebaut.
- `Wiedergabe_AmEnde_FeuertExitedUndWechseltAufInaktiv` (Z. 149) — `Exited` mit `ExitCode = null`, `RuntimeStatus → Inaktiv`.
- `Wiedergabe_OutputChunkEvent_LiefertRohbytes` (Z. 170) — `OutputChunk`-Vertrag (ungeparste Rohbytes, chunkweise, in Reihenfolge).
- `Stubs_SindUnschaedlich` (Z. 194) — `ITerminalSession`-Stub-Vertrag (Prozess-Stub, Null-Streams, No-Op-Eingaben, `Resize`/`DrainOutputAsync` immer erfolgreich, `RebuildBufferFromReplay` auf leerem Präfix wirft nicht).
- `WiedergabeStarten_ZweiterAufruf_IstNoOp` (Z. 226) — Idempotenz.
- `Dispose_WaehrendWiedergabe_BrichtAb` (Z. 250) — Abbruch via CTS, kein `Exited` nach Dispose.
- `Dispose_MitZeitrafferNull_BrichtAbStattChunksZuDraenen` (Z. 275) — blockiert die Schleife mitten im `OutputChunk`-Handler (`ManualResetEventSlim`), beweist den Abbruch-Check zwischen Chunks.

### `KonsolenTestViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/KonsolenTestViewModelTests.cs` (415 Zeilen) — **direkt erweiterbar für Command-/CanExecute-Tests**; `Mock<IDialogService>`, echter `CliReplayAufzeichnungStore` auf Temp-Dateien, `FakeTimeProvider`, synchroner `dispatcherInvoke: action => action()`.

- `AufzeichnungOeffnen_LaadtSessionUndQuellEintraege` (Z. 87) — Session-Typ `TerminalReplaySession`, Quell-Einträge, `PositionsText = "Chunk 0/2"`, Status.
- `AufzeichnungOeffnen_UnvollstaendigeAufzeichnung_ZeigtHinweis` (Z. 113), `AufzeichnungOeffnen_KorrupteDatei_ZeigtFehlermeldung` (Z. 126), `AufzeichnungOeffnen_DialogAbgebrochen_KeinZustandswechsel` (Z. 142).
- `Wiedergabe_LaeuftBisEnde_UndSynchronisiertQuellAnsicht` (Z. 157) — Endstatus „Wiedergabe beendet.", `PositionsText = "Chunk 2/2"`, `AktuellerQuellEintrag = QuellEintraege[1]`.
- `Wiedergabe_PausierenUndFortsetzen` (Z. 183) — Status „Pausiert.", kein Chunk während Pause trotz `Advance(10 min)`, Fortsetzen bis Ende.
- `Wiedergabe_NachEnde_StartetErneutAbPosition0` (Z. 229) — CanExecute nach Ende, frische Session (`NotBeSameAs`).
- `ZeitrafferSchwelleText_ValidiertUndWirktLive` (Z. 259), `ZeitrafferSchwelleText_UngueltigeWerte_WerdenAbgewiesen` (Theory, Z. 289), `ZeitrafferSchwelleText_Ungueltig_UebernimmtLetzteGueltigeSchwelleAufFrischeSession` (Z. 307).
- `Wiedergabe_NeustartAusPausiertemLauf_SpieltErneutAbPosition0` (Z. 331) — Neustart-CanExecute aus Pausiert-Zustand, Session-Ersatz.
- `Schliessen_LoestCloseRequestedAus` (Z. 381), `Commands_OhneAufzeichnung_SindInert` (Z. 400) — CanExecute-Guards ohne geladene Aufzeichnung.

### `CliReplayAufzeichnungStoreTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/CliReplayAufzeichnungStoreTests.cs` — 11 Facts: Roundtrip (Header + Chunks, `EndeUtc null`, UTC-Normalisierung, Datei-Wrapper), Formatvalidierung (Magic, Version, abgeschnittene Header/Records, Geometrie, PluginName-Länge).

### `CliOutputRecorderTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Terminal/CliOutputRecorderTests.cs` — 6 Facts: Chunk-Offsets/Byte-Kopien, Budget-Überschreitung behält intaktes Präfix + `IstVollstaendig = false`, `Complete` idempotent, Snapshot-Unabhängigkeit, leere Chunks ignoriert, kein `ITerminalDiagnoseSink`.

### `KiAusfuehrungsServiceCliAufzeichnungTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceCliAufzeichnungTests.cs` — 7 Tests: Recorder-Verdrahtung (`GetCliAufzeichnung`, Composite-Senke, Budget, Max-Aufzeichnungen, `TimeProvider`-Zeitstempel).

### `CliChunkQuelltextFormatterTests`
Datei: `src/Softwareschmiede.Tests/App/Services/CliChunkQuelltextFormatterTests.cs` — 4 Facts: Steuerzeichen-Sichtbarmachung (`␛`, `\r`, `\n`, `\xNN`), UTF-8-Mehrbyte.

### `AnsiSequenceParserTests` / `TerminalBufferTests` / `TerminalControlTests`
- `AnsiSequenceParserTests` (27 Facts/Theories): CSI-Befehle, SGR, chunk-übergreifende UTF-8-Dekodierung, OSC-Überlesen, `Reset()`.
- `TerminalBufferTests` (35 Facts/Theories): `Apply`-Semantik aller `TerminalEvent`-Typen, `Reset`, `Resize`, Scrollback, Alternate Screen.
- `TerminalControlTests` (+ `.ClipboardPaste`): `OnSessionChanged` ruft `RebuildBufferFromReplay`, BufferChanged-De/Registrierung, Session-Wechsel ohne Doppelausgabe (`OnSessionChanged_ReattachedSession_KeineDoppelteAusgabe`).

### `End2EndTest` (E2E, FlaUI)
Dateien: `src/Softwareschmiede.Tests/E2E/MainTest.cs` + partielle Szenario-Dateien — `[Trait("Category", "E2E")]`, `[OsInterface]`, `[Collection("E2E")]`, Basisklasse `WpfTestBase`.

- `RunGeneralTests` (Z. 18–67): ohne ConPTY; enthält `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` (Z. 36) — in Lauf 3 bestanden (8 m 30 s Gesamtlauf).
- `RunConPtyTests` (Z. 73–103): `[SkippableFact]` + `SkipWennConPtyNichtVerfuegbar()` — in dieser Sandbox übersprungen; enthält `ConPtyLifecycle_...`-Szenario mit echtem `.clireplay`-Export + Wiedergabe (`E2E_ConPtyLifecycle.cs` Z. 80–110).

`KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` (`E2E_KonsolenTestfenster.cs` Z. 29–178): konsolidiertes Szenario — erzeugt drei synthetische `.clireplay`-Dateien (`CliReplayAufzeichnungStore`), öffnet den Dialog über `SettingsView.OpenKonsolenTestDialog`, deckt Dialog-Abbruch, Formatfehler, Pausieren→Fortsetzen an einer Aufzeichnung mit 3-s-Inter-Chunk-Pause, erneutes Abspielen nach Ende, Neustart aus Pausiert, Zeitraffer 0 sowie die Quell-Chunk-Liste ab; `TryCloseKonsolenTestfenster` (Z. 182–200) schließt Dialog + Einstellungen fehlertolerant im `finally`.

## Hilfsmethoden / Test-Doubles

### `KonsolenTestDialogView` (E2E-Wrapper)
Datei: `src/Softwareschmiede.Tests/E2E/Views/Dialogs/KonsolenTestDialogView.cs` (328 Zeilen), Basisklasse `DialogView` (Titel „Konsolentest").

| Methode | Zweck |
|---------|-------|
| `OeffneAufzeichnung(pfad)` | Klickt „Aufzeichnung öffnen…" und bedient den nativen `OpenFileDialog` deterministisch (ValuePattern + ESC/RETURN-Handling der Autocomplete-Liste) |
| `OeffneAufzeichnungAbbrechen()` | Öffnen-Dialog per ESC abbrechen |
| `IstFehlerSichtbar()` / `GetFehlerMeldung()` / `WarteAufFehlerSichtbar()` | Fehlerbanner-Prüfung (Sichtbarkeit via `IsOffscreen`, Text via `HelpText`) |
| `SetZeitrafferSchwelle(text)` | Setzt die Zeitraffer-Textbox |
| `StartWiedergabe()` / `NeustartWiedergabe()` / `PausierenToggle()` | Button-Klicks per `WaitForEnabledElement` + `ClickInForeground` (wartet CanExecute-Neuauswertung) — **Muster für die neuen Schritt-Buttons** |
| `GetStatusText()` / `GetPositionsText()` | Liest HelpText von `WiedergabeStatus`/`WiedergabePosition` |
| `WarteAufStatus(erwartet)` / `WarteAufPosition(erwartet)` | Polling-Warten mit `Medium`-Timeout |
| `WarteAufQuellEintraege(mindest)` / `GetQuellEintraegeCount()` / `GetQuellEintragText(index)` | Quell-Chunk-Liste (GridView-Zeilen; letzte Zelle = Quelltext) |
| `Schliessen()` | Invoke-Pattern + `WaitUntilGone` |
| `WaitForEnabledElement(parent, name, timeout)` | private statisch — Element suchen UND `IsEnabled` abwarten |

### `SettingsView` (E2E)
Datei: `src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs`

- `OpenKonsolenTestDialog()` (Z. 64–69): Tab „Allgemein" → Button `KonsolenTestOeffnen` → `new KonsolenTestDialogView(Window).ForceShow()`.

### `WpfTestBase` (E2E-Infrastruktur)
Datei: `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`

- `LaunchApp(...)` (Z. 170 ff.): startet `Softwareschmiede.App.exe` mit `SOFTWARESCHMIEDE_TEST_DB_PATH` (Test-SQLite, erzwingt Pipe-Backend via `SimulatedPseudoConsoleProcessLauncher`).
- `SkipWennConPtyNichtVerfuegbar()` (Z. 922–925): `Skip.If(!ConPtyEnvironmentProbe.IsAvailable, ...)` — Environment-Variable steuert den Skip in dieser Sandbox.
- `WarteAufEchtesHauptfenster`, Timeouts `Short`/`Medium`/`Long` (aus `BaseWindowView`/`ElementWaitHelper`).

### Weitere Unit-Test-Helfer

- `FakeTimeProvider` (`Microsoft.Extensions.Time.Testing`): `Advance(TimeSpan)` steuert die `Task.Delay`-Pausen der `WiedergabeLoopAsync` deterministisch — in `TerminalReplaySessionTests` und `KonsolenTestViewModelTests` im Einsatz.
- `TestPseudoConsoleSessionFactory` (`src/Softwareschmiede.Tests/Helpers/`): echte `PseudoConsoleSession` auf MemoryStreams mit optionalem `TimeProvider` — Vergleichsreferenz für den Live-Renderpfad.
- `ChunkedStream` (privat in `TerminalReplaySessionTests` Z. 372–403): liefert pro Lesevorgang genau einen Chunk.
- `ErstelleAufzeichnungsDateiAsync`/`SetupOpenDialog`/`WarteBisAsync`/`CreateSut` (privat in `KonsolenTestViewModelTests`): echte `.clireplay`-Temp-Dateien via `CliReplayAufzeichnungStore.SpeichernAsync`, `Mock<IDialogService>`-Setup, synchroner Dispatcher.
- `WpfUnitTestHelpers.RunOnSta` (`src/Softwareschmiede.Tests/Helpers/`): STA-Thread für WPF-Unit-Tests.
- `OsInterfaceAttribute`/`OsInterfaceFactAttribute` (`src/Softwareschmiede.Tests/Infrastructure/Testing/`): markieren `Category=OsInterface`.
