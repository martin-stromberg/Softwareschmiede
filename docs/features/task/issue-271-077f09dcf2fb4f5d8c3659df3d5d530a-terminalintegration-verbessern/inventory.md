# Bestandsaufnahme: Terminalintegration verbessern — gemeinsame PTY-Session und sauberes VT-Rendering

Bestandsaufnahme des bestehenden Terminal-/PTY-/CLI-Integrationscodes bezogen auf die Anforderung in `requirement.md` (Issue #271): gemeinsame Session-Abstraktion `ITerminalSession`, Rohdaten-Streaming, VT-kompatibles Rendering, Diagnose/Fallback und Replay-Puffer.

## Zusammenfassung

**Vorhanden:**

- Ein vollständiger, funktionierender ConPTY-Stack: `Win32PseudoConsoleProcessLauncher` → `PseudoConsole` (HPCON) + `PseudoConsoleProcessStarter` (`CreateProcess` mit `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE`) → `PseudoConsoleSession` mit eigener, UI-unabhängiger Leseschleife (`ReadLoopAsync`), chunk-basiertem Output (`ITerminalOutputSink.OnOutputChunk`), `Resize`, serialisiertem `WriteInputAsync`, `WritePromptAsync` und `DrainOutputAsync`/`Dispose`-Kaskade — Details in [Logik](inventory/logic.md).
- Ein eigener VT-Renderer-Stack: `AnsiSequenceParser` (CSI A–D/H/f/J/K/m, SGR inkl. 256er/TrueColor, `?25`) → `TerminalBuffer` (2D-Grid, Scrollback 1000 Zeilen, `GetSnapshot`) → `TerminalControl` (WPF-`FrameworkElement` mit `IScrollInfo`, `OnRenderSizeChanged` ↔ `session.Resize` bereits verdrahtet) + `KeyToVt100Encoder` (Tasten, Paste, Ctrl-Kombis).
- Eine Prozess-Lifecycle-Schicht: `KiAusfuehrungsService` (zwei Startpfade: `StartWithPseudoConsoleAsync` und klassischer Pipe-`StartCliAsync`), `CliProcessHandle` inkl. `NativeProcessHandle`-basiertem Exit-Code (`GetExitCodeProcess`), `CliProcessManager` (Heartbeat + `RuntimeStatusChanged` → persistierter `Aufgabe.LaufStatus`), `PromptZeitVersandService` und `ProjektleiterAgentService` (verzögerte Prompt-Injektionen), `CliOutputProtokollWriter`/`CliOutputLineAccumulator` (zeilenbasiertes Sitzungsprotokoll).
- Plugin-Schicht: `CliKiPluginBase` mit `BuildProcessStartInfo` als faktischer Startbeschreibung (Executable, Args, Cwd, Env), `CheckHealthWithVersionCommandAsync`/`CheckHealthAsync` als vorhandene Health-/Versions-Basis; fünf Ableitungen (`ClaudeCliPlugin`, `CodexPlugin`, `GitHubCopilotPlugin`, `DevinPlugin`, `KiSimulatorPlugin`).
- E2E-/Test-Infrastruktur: `SimulatedPseudoConsoleProcessLauncher` (Pipe-Simulation im E2E-Modus via `SOFTWARESCHMIEDE_TEST_DB_PATH`), `TestPseudoConsoleSessionFactory`, `TestKiAusfuehrungsServiceFactory`, `WpfTestBase` mit `SkipWennConPtyNichtVerfuegbar`/`ConPtyEnvironmentProbe`, `OsInterface*`-Testkategorien, `AppStartupLogInspector`.

**Nicht vorhanden (zentral für die Anforderung):**

- Keine Session-Abstraktion `ITerminalSession` — `PseudoConsoleSession` ist eine `sealed` Klasse ohne Interface; `TerminalControl.Session`, `CliProcessHandle.PseudoConsoleSession`, `IPseudoConsoleProcessLauncher.Start` und alle Aufrufer sind hart auf den konkreten Typ verdrahtet. Es gibt auch kein `Exited`/`Failed`-Event an der Session und keine `OutputChunk`-Abstraktion — Rohchunks gehen nur an `ITerminalOutputSink`, Events `BufferChanged`/`RuntimeStatusChanged` sind geparste/aggregierte Formen.
- Keine zentrale Session-Erzeugung — verteilt auf `IPseudoConsoleProcessLauncher` + `PseudoConsoleSession`-Konstruktor + `KiAusfuehrungsService`; der ConPTY-Pfad startet `cmd.exe` als Hülle und injiziert den Plugin-Befehl ~300 ms später als Tastaturbytes (`SendCommandDelayedAsync`) statt die Anbieter-CLI direkt in der PTY zu starten.
- Keine Anbieterfähigkeiten (`TerminalProviderCapabilities` o. ä.) — kein Plugin deklariert PTY-Bedarf/-Unterstützung.
- Keine Pre-Flight-Diagnose (PTY-Verfügbarkeit, Encoding, Terminalgröße) — es existieren nur `CheckHealthAsync` (`--version`-Probe) und Compile-Zeit-Voraussetzung `net10.0-windows10.0.17763.0`; kein Laufzeit-Check vor `CreatePseudoConsole`.
- Kein Rohdaten-Replay-Puffer — Session-Neuanbindung erfolgt ausschließlich über den gerenderten `TerminalBuffer`-Zustand (`TerminalControl.OnSessionChanged` übernimmt `session.Buffer` direkt).
- VT-Lücken: kein Alternate Screen (`?1049h/l`), keine Insert/Delete-Line/Char, keine Scroll-Regionen, OSC wird nur überlesen; UTF-8-Mehrbyte-Sequenzen über Chunk-Grenzen zerfallen zu U+FFFD (`AnsiSequenceParser` flusht `_textBuffer` am Ende jedes `Parse`-Aufrufs — anders als `CliOutputLineAccumulator`, der chunk-übergreifend korrekt dekodiert).
- `CliSessionService`/`ICliSessionService` (zeilenbasierter `ReadLineAsync`-Pfad) existiert noch, ist aber **ohne DI-Registrierung und ohne Aufrufer** — de facto toter Code.
- `src/Softwareschmiede/terminal-backend/` enthält einen nicht referenzierten Node.js-Prototyp (`node-pty` + `ws`), der schon einmal denselben "Shell in PTY starten + Befehl per `write()` injizieren"-Ansatz zeigt.

**Test-Ausgangszustand:** Alle vier CI-Testlanes ausgeführt (Build + 4 `dotnet test`-Läufe) — **1812 Tests bestanden, 0 fehlgeschlagen, 3 übersprungen** (alle absichtlich: 2 ConPTY-E2E-Runner via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, 1 Nicht-Windows-Test). Nachweis inkl. TRX-Reports in [Tests](inventory/tests.md). Auffällig: Der echte `Win32PseudoConsoleProcessLauncher`-Pfad hat keine automatisierte Abdeckung — E2E fährt auf dem simulierten Pipe-Pfad.

## Details

- [Datenmodell](inventory/models.md) — `TerminalCell`, `TerminalEvent`-Hierarchie, `TerminalBufferSnapshot`, `CliProcessHandle`, `ProcessStartResult`, `ScheduledPromptInfo`, `Aufgabe` (relevante Felder)
- [Logik](inventory/logic.md) — `PseudoConsoleSession`, Launcher, `PseudoConsole(ProcessStarter|NativeMethods)`, `KiAusfuehrungsService`, `CliProcessManager`, `CliOutputProtokollWriter`/`CliOutputLineAccumulator`, `PromptZeitVersandService`, `EntwicklungsprozessService`, `ProjektleiterAgentService`, `CliSessionService` (toter Code), `TerminalControl`, `KeyToVt100Encoder`, `TaskDetailView(Model)`, `CliKiPluginBase` + fünf Plugins, `terminal-backend`
- [Enums](inventory/enums.md) — `CliRuntimeStatus`, `CliProcessStatus`, `AufgabeLaufStatus`, `AnsiSequenceParser.State`, `PluginType`
- [Interfaces](inventory/interfaces.md) — `IPseudoConsoleProcessLauncher`, `IPseudoConsoleHandle`, `ITerminalOutputSink`, `IKiPlugin`, `IAiCliProvider`, `ICliSessionService`, `IRunningAutomationStatusSource`, `ICliRawExportService`, `ICliRunner`, `IProzessStarter`
- [Tests](inventory/tests.md) — Test-Ausgangszustand (Zeitpunkt, Commit, Umgebung, Läufe mit TRX-Nachweisen), Testklassen und Hilfsmethoden
