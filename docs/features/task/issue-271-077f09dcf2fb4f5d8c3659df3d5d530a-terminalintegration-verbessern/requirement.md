# Übersetzte Anforderung: Terminalintegration verbessern — gemeinsame PTY-Session und sauberes VT-Rendering

**Quelle:** Issue #271 / Aufgaben-ID `077f09dc-f2fb-4f5d-8c36-59df3d5d530a`
**Branch:** `task/issue-271-077f09dcf2fb4f5d8c3659df3d5d530a-terminalintegration-verbessern`

## Fachliche Zusammenfassung

Die interaktive KI-CLI-Einbettung soll auf eine gemeinsame Terminal-Session-Abstraktion `ITerminalSession` umgestellt werden, die Prozessstart, PTY-/ConPTY-Erzeugung, `Resize`, Ein-/Ausgabe und den Session-Lebenszyklus zentral kapselt — statt wie heute über `KiAusfuehrungsService` → `IPseudoConsoleProcessLauncher` → `cmd.exe`-Hülle mit verzögertem Tastatur-Inject des Plugin-Befehls (`SendCommandDelayedAsync`). Die Anbieter-Plugins (`CliKiPluginBase`-Ableitungen) werden auf eine reine Startbeschreibung (Executable, Argumente, Arbeitsverzeichnis, Umgebungsvariablen, Anbieterfähigkeiten) reduziert. Die interaktive Ausgabe wird als unveränderte Byte-Chunks gestreamt und von einem VT-kompatiblen, zustandsbasierten Renderer verarbeitet; der bisherige Pipe-/Nicht-PTY-Pfad bleibt als explizit diagnostizierbarer Fallback erhalten. Ergänzend sind eine Pre-Flight-Diagnose (PTY-Verfügbarkeit, CLI-Version, Encoding, Terminalgröße, Pluginparameter) sowie ein begrenzter Rohdaten-Replay-Puffer für UI-Neuanbindungen vorgesehen.

## Betroffene Klassen und Komponenten

### Neu zu erstellende Artefakte (voraussichtlich, Namen sind Vorschläge)

- **`ITerminalSession`** (Interface, voraussichtlich `Softwareschmiede.Infrastructure.Terminal`): gemeinsame Session-Abstraktion mit `WriteInput`, `Resize`, `OutputChunk`, `Exited`, `Failed` und `Dispose` — bildet die in der Anforderung geforderte Contract-Elemente ab.
- **Zentrale Session-Erzeugung** (z. B. `ITerminalSessionFactory` / `TerminalSessionService`): zentralisiert Prozessstart, PTY-/ConPTY-Erzeugung, Resize-Verdrahtung und Lebenszyklus, die heute auf `Win32PseudoConsoleProcessLauncher`, `PseudoConsoleProcessStarter`, `PseudoConsole` und `KiAusfuehrungsService` verteilt sind.
- **Start-Spezifikation für Anbieter** (Value Object, z. B. `TerminalSessionStartSpec`): Executable, Argumente, Arbeitsverzeichnis, Umgebungsvariablen; löst die Rückgabe von `ProcessStartInfo` aus `IKiPlugin.StartCliAsync` für den interaktiven Pfad ab (Annahme).
- **Anbieterfähigkeiten** (Enum/Flags oder Deskriptor, z. B. `TerminalProviderCapabilities`): beschreibt, ob eine CLI PTY/ConPTY unterstützt bzw. benötigt (Annahme; die Anforderung nennt "Anbieterfähigkeiten" ohne deren Form festzulegen).
- **Pre-Flight-Diagnose** (z. B. `TerminalSessionDiagnostics`): prüft und protokolliert vor dem Start PTY-Verfügbarkeit (ConPTY erfordert laut `docs/help/terminal/architektur.md` Windows 10 Build 17763+), CLI-Version, Encoding, Terminalgröße und Pluginparameter.
- **Replay-Puffer** (z. B. `TerminalReplayBuffer`): begrenzter Ringpuffer roher Ausgabe-Chunks für UI-Neuanbindungen; klar getrennt vom dauerhaften Sitzungsprotokoll (`CliOutputProtokollWriter` / `ProtokollService`).

### Bestehende, voraussichtlich zu ändernde Artefakte

- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` — konkrete Session; wird voraussichtlich `ITerminalSession` implementieren bzw. hinter die Abstraktion gesetzt (heute: `InputStream`, `OutputStream`, `Buffer`, `Resize`, `WriteInputAsync`, `WritePromptAsync`, `BufferChanged`, `RuntimeStatusChanged`, `ReadLoopAsync`).
- `src/Softwareschmiede/Infrastructure/Terminal/IPseudoConsoleProcessLauncher.cs`, `Win32PseudoConsoleProcessLauncher.cs`, `SimulatedPseudoConsoleProcessLauncher.cs`, `PseudoConsoleProcessStarter.cs`, `PseudoConsole.cs`, `PseudoConsoleNativeMethods.cs`, `IPseudoConsoleHandle.cs`, `NullPseudoConsoleHandle.cs` — bisheriger Startpfad; die cmd.exe-Verschachtelung in `Win32PseudoConsoleProcessLauncher.Start` (cmd.exe starten, Plugin-Befehl per `SendCommandDelayedAsync` als Tastatureingabe nach ~300 ms injizieren) wird durch den direkten Prozessstart in der Session-Schicht ersetzt.
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` — `StartWithPseudoConsoleAsync`, `StartCliAsync` (klassischer Pipe-Start = Fallback-Kandidat), `SendCommandDelayedAsync`, `CliProcessHandle` (inkl. `NativeProcessHandle`, `SendCts`, `OutputSink`), `TryGetExitCode`, `CancelAndDisposeConPtyResourcesAsync`.
- `src/Softwareschmiede/Infrastructure/Services/CliSessionService.cs` + `ICliSessionService.cs` — älterer, zeilenbasierter (`ReadLineAsync`) Session-Pfad; laut Anforderung darf kein `ReadLine` im interaktiven Pfad verwendet werden → Abbau oder eindeutige Fallback-Rolle.
- `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs`, `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs`, `TerminalEvents.cs`, `TerminalCell.cs` — eigener VT-Renderer; aktuell ohne Alternate-Screen (`?1049`), Insert/Delete-Line, Scroll-Regionen u. a. — falls der Eigenbau als "vorhandene gleichwertige Komponente" weitergeführt wird (siehe Offene Fragen), sind diese Klassen entsprechend zu erweitern.
- `src/Softwareschmiede.App/Controls/TerminalControl.cs` + `KeyToVt100Encoder.cs` — WPF-Renderer und Tastaturkodierung; `OnRenderSizeChanged` ist das WPF-Äquivalent zum geforderten `ResizeObserver` und bereits mit `session.Resize` verdrahtet; Eingabe/Enter/Backspace/Ctrl-C/Paste/Fokus sind vorhanden und zentral zu führen.
- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`, `Domain/Interfaces/IKiPlugin.cs`, `IAiCliProvider.cs` sowie die Plugin-Implementierungen unter `plugins/` (`Softwareschmiede.Plugin.ClaudeCli`, `.Codex`, `.GitHubCopilot`, `.Devin`, `.KiSimulator`) — Reduktion auf CLI/Argumente/Arbeitsverzeichnis/Umgebung/Fähigkeiten.
- `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs` + `CliOutputLineAccumulator.cs` (`ITerminalOutputSink`) — Rohdatensenke für das Aufgabenprotokoll; die zeilenweise Segmentierung bleibt auf die Protokollierung beschränkt und darf nicht in den interaktiven Renderpfad wirken.
- `src/Softwareschmiede/Application/Services/CliProcessManager.cs`, `EntwicklungsprozessService.cs`, `ProjektleiterAgentService.cs`, `PromptZeitVersandService.cs`, `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` (`PseudoConsoleSessionGestartet`), `AutonomAufgabeDetailViewModel.cs`, `src/Softwareschmiede.App/Views/TaskDetailView.xaml(.cs)` — Aufrufer/Bindung der Session; auf `ITerminalSession` umzustellen bzw. anzupassen.

### Tests

- Bestehende: `PseudoConsoleSessionTests*`, `TerminalBufferTests`, `AnsiSequenceParserTests`, `CliRuntimeStatusEvaluatorTests`, `TerminalControlTests*`, `CliProcessManagerTests*`, `CliOutputLineAccumulatorTests`, `CliOutputProtokollWriterTests`, `CliEmbeddingServiceIntegrationTests`, `ProjektleiterAgentServiceTests_CliIntegration`, `TestPseudoConsoleSessionFactory`, E2E: `E2E_ConPtyLifecycle`, `E2E_AutoStartCli`, `E2E_CliPanelVisibility`, `E2E_CliRawExport`, `ConPtyEnvironmentProbe` (Kategorie `OsInterface`).
- Neu: Unit-/Integrationstests für `ITerminalSession`-Lebenszyklus, `WriteInput`, `Resize`, `Exited`/`Failed`, Exit-Code; Renderer-Tests für CR/LF, Backspace, ANSI-Farben, Cursorbewegungen, alternative Screens und schnelle Output-Bursts; E2E-Tests (FlaUI, konsolidiert) für den vollständigen Benutzerfluss inkl. Fokus, Eingabe, Paste, Resize, Session-Neuanbindung und kontrolliertem Fallback; Diagnose-Tests für fehlende/inkompatible PTY-Abhängigkeit.

## Implementierungsansatz

- **P0 — gemeinsame Session:** `ITerminalSession` mit `WriteInput`, `Resize`, `OutputChunk`, `Exited`, `Failed`, `Dispose` einführen; `PseudoConsoleSession` dahintersetzen bzw. darauf abbilden. Die Session-Erzeugung zentralisieren: Der heutige Umweg "cmd.exe in ConPTY starten → Plugin-Befehl als Tastaturbytes injizieren" (`Win32PseudoConsoleProcessLauncher.Start` + `KiAusfuehrungsService.SendCommandDelayedAsync`) wird durch direkten Start der Anbieter-CLI in der PTY ersetzt; die Exit-Code-Ermittlung über `NativeProcessHandle`/`GetExitCodeProcess` ist in `Exited` abzubilden.
- **Plugin-Reduktion:** `IKiPlugin`/`CliKiPluginBase` liefern für den interaktiven Pfad nur noch die Startbeschreibung (Executable, Argumente, Arbeitsverzeichnis, Umgebung — bereits weitgehend über `BuildProcessStartInfo` vorhanden) plus Anbieterfähigkeiten. Migration schrittweise: zunächst eine problematische CLI über den neuen Adapter, bisheriger Pfad bleibt als expliziter, diagnostizierbarer Fallback.
- **P0 — Rohdaten-Streaming:** `OutputChunk` transportiert unveränderte Bytes; die vorhandene Leseschleife (`ReadLoopAsync` → `ITerminalOutputSink.OnOutputChunk`) ist bereits chunk-basiert und als Ausgangspunkt geeignet. Kein `ReadLine` im interaktiven Pfad (betrifft `CliSessionService`; `CliOutputLineAccumulator` bleibt rein der Protokollierung vorbehalten). Encoding (UTF-8-Dekodierung in `AnsiSequenceParser`/`CliOutputLineAccumulator`) und Behandlung ungültiger Sequenzen (Parser ignoriert derzeit still) dokumentieren.
- **P1 — zustandsbasiertes Rendering:** VT-kompatiblen Renderer verwenden — entweder `xterm.js` (würde Browser-Hosting, z. B. WebView2, in der WPF-App erfordern) oder den bestehenden Stack `AnsiSequenceParser` + `TerminalBuffer` + `TerminalControl` als gleichwertige Komponente um Alternate Screen, weitere CSI-Befehle etc. erweitern (offene Frage). Eingabe/Enter/Backspace/Ctrl-C/Paste/Fokus zentral behandeln (`KeyToVt100Encoder`, `TerminalControl`); `OnRenderSizeChanged` ↔ PTY-`Resize` mit tatsächlichen Spalten/Zeilen an den Prozess senden.
- **P1 — Diagnose/Fallback:** Vor dem Start PTY-Verfügbarkeit, CLI-Version (`CliKiPluginBase.CheckHealthWithVersionCommandAsync`/`CheckHealthAsync` existieren bereits), Encoding, Terminalgröße und Pluginparameter prüfen und protokollieren. Fehlende native PTY-Abhängigkeiten und nicht unterstützte CLIs verständlich melden; Pipe-Fallback (`SimulatedPseudoConsoleProcessLauncher`, `StartCliAsync`) darf nicht stillschweigend als gleichwertiger interaktiver Modus gelten.
- **P2 — Replay/Sessionzustand:** Begrenzten Rohdaten-Replay-Puffer für UI-Neuanbindung vorsehen (heute erfolgt die Wiederanbindung über den gerenderten `TerminalBuffer`-Zustand — kein Rohdaten-Replay); doppelte Subscriber (`BufferChanged`, `RuntimeStatusChanged` — bereits teilweise defensiv bereinigt) und konkurrierende Resize-Aufrufe verhindern; Sessions deterministisch beenden (bestehende `Dispose`-/`DrainOutputAsync`-/`CompleteAsync`-Kaskade auf die neue Abstraktion übertragen); Replay-Puffer klar vom dauerhaften Protokoll (`ProtokollService`, `CliOutputProtokollWriter`) abgrenzen.
- **Einführungsstrategie (aus der Anforderung):** 1. Session-Abstraktion + Diagnose, 2. eine problematische CLI migrieren, 3. Renderer + vollständige E2E-Abdeckung, 4. Vergleich mit bisherigem Pfad, 5. weitere Anbieter schrittweise migrieren.

## Konfiguration

- **Anbieterfähigkeiten:** voraussichtlich deklarativ pro Plugin (Eigenschaft/Deskriptor am `IKiPlugin` bzw. `CliKiPluginBase`), nicht als Benutzereinstellung (Annahme).
- **Fallback-Verhalten:** ob der Nicht-PTY-Pfad pro Plugin, pro Aufgabe oder global (Anwendungseinstellung) aktivierbar sein soll, ist zu klären; auf jeden Fall explizit und diagnostizierbar, nicht stillschweigend.
- **Replay-Puffergröße:** Begrenzung (Bytes oder Chunks) festlegen — keine Persistenzkonfiguration, sondern Laufzeitparameter (Annahme).

## Akzeptanzkriterien (aus der Anforderung übernommen)

- Eine CLI startet mit definiertem Arbeitsverzeichnis und erwarteter Umgebung.
- Eingabe, Enter, Backspace, Ctrl-C, Paste und Exit-Code funktionieren.
- Fortschrittsanzeigen mit `\r`, ANSI-Farben, Cursorbewegungen und mehrzeilige Ausgabe werden korrekt dargestellt.
- Resize wird vom Prozess beobachtbar übernommen.
- Schnelle Output-Bursts verlieren keine Chunks und blockieren die UI nicht.
- Eine Session-Neuanbindung erzeugt keine doppelten Ausgaben.
- Fehlende oder inkompatible PTY-Abhängigkeiten führen zu einer verständlichen Diagnose.
- E2E-Tests decken den vollständigen Benutzerfluss ab.

## Offene Fragen

1. **Welche Anbieter-CLI ist die "problematische" für die erste Migration?** Die Anforderung nennt keinen konkreten Anbieter; Kandidaten sind `CodexPlugin` oder `GitHubCopilotPlugin` (CLIs, die ConPTY ggf. als nicht unterstützt erkennen).
2. **Renderer-Strategie:** `xterm.js` erfordert Browser-Hosting (z. B. WebView2) in der WPF-App — ist das gewollt, oder soll der bestehende `AnsiSequenceParser`/`TerminalBuffer`/`TerminalControl`-Stack als "vorhandene gleichwertige Komponente" erweitert werden? (Annahme: Erweiterung des Bestehenden, u. a. Alternate Screen `?1049h/l`, Insert/Delete-Line, Scroll-Regionen.)
3. **Lage von `ITerminalSession`:** `Softwareschmiede.Infrastructure.Terminal` (neben `PseudoConsoleSession`) oder in `Softwareschmiede.Plugin.Contracts`/`Domain`, falls Plugins die Session-Schnittstelle direkt kennen sollen?
4. **cmd.exe-Hülle:** Wird `SendCommandDelayedAsync`/die cmd.exe-Verschachtelung vollständig durch den direkten PTY-Prozessstart ersetzt, oder bleibt sie als Wrapper für CLIs ohne PTY-Fähigkeit erhalten?
5. **Fallback-Definition:** Ist mit "bisheriger Integrationspfad" der klassische `StartCliAsync`-Pfad (Prozess ohne PTY), der `SimulatedPseudoConsoleProcessLauncher` (Pipe-Simulation im E2E-Modus) oder der zeilenbasierte `CliSessionService` gemeint? (Annahme: Alle Nicht-PTY-Pfade werden zu einem expliziten, diagnostizierbaren Fallback konsolidiert.)
6. **Replay-Puffer-Form:** Rohe Byte-Chunks mit erneutem Parsen bei Reattach vs. Weiternutzung des gerenderten `TerminalBuffer`-Zustands; Größe/Abbruchkriterium des Puffers.
7. **Plattformscope:** Die Referenz auf `node-pty` und "native PTY-Abhängigkeiten je Plattform" deutet auf mögliche Portierbarkeit hin — bleibt die Umsetzung Windows/ConPTY-only (aktueller Stand) oder ist eine Abstraktion für weitere PTY-Backends gefordert?
8. **Session-Neuanbindung:** Bezieht sich das Akzeptanzkriterium auf die UI-Neuanbindung (`TerminalControl.Session`-Wechsel, bereits möglich) oder zusätzlich auf App-Neustart/Detach-Szenarien?
