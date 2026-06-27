# Tasks: ConPTY-basiertes Terminal-Control

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Infrastruktur | `PseudoConsoleNativeMethods` anlegen (P/Invoke: `CreatePseudoConsole`, `ResizePseudoConsole`, `ClosePseudoConsole`, `CreateProcess`, `InitializeProcThreadAttributeList`, `UpdateProcThreadAttribute`, `DeleteProcThreadAttributeList`, `CreatePipe`, `CloseHandle`) | Offen | — |
| 2 | Infrastruktur | `PseudoConsole` anlegen (IDisposable-Wrapper für `HPCON`, Pipe-Handles; `Create`-Fabrikmethode; `Resize(short cols, short rows)`; `Dispose`) | Offen | — |
| 3 | Infrastruktur | `PseudoConsoleProcessStarter` anlegen (`Start(ProcessStartInfo, PseudoConsole)` → Win32-Prozess via `STARTUPINFOEX` + `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE`) | Offen | — |
| 4 | Infrastruktur | `PseudoConsoleSession` anlegen (koordiniert `PseudoConsole`, `Process`, `InputStream`, `OutputStream`; `ResizeAsync`; `Dispose`) | Offen | — |
| 5 | Datenmodell | `TerminalCell` als `record struct` anlegen (`char Character`, `Color Foreground`, `Color Background`, `bool Bold`, `bool Underline`, `bool Dim`) | Offen | — |
| 6 | Datenmodell | `TerminalEvent`-Hierarchie anlegen (Basis + `TextWrittenEvent`, `CursorMovedEvent`, `ColorChangedEvent`, `ScreenClearedEvent`, `LineErasedEvent`, `CursorVisibilityChangedEvent`) | Offen | — |
| 7 | Datenmodell | `TerminalBuffer` anlegen (2D-Grid, Cursor-Position, SGR-Attributzustand, Scrollback-Ringpuffer 1 000 Zeilen; `Apply(TerminalEvent)`, `Resize(int cols, int rows)`) | Offen | — |
| 8 | Logik | `AnsiSequenceParser` anlegen (zustandsbehafteter VT100-Parser; `Parse(ReadOnlySpan<byte>)` → `IEnumerable<TerminalEvent>`; deckt Plaintext, CSI-Sequenzen, SGR 3-bit/8-bit/24-bit, Cursor-Move, Clear/Erase ab) | Offen | — |
| 9 | Logik | `KeyToVt10Encoder` anlegen (WPF `Key` → `byte[]`: Pfeiltasten, Enter, Backspace, Delete, Tab, Escape, F1–F12, Ctrl+Buchstabe) | Offen | — |
| 10 | Datenmodell | `CliProcessHandle` erweitern: `PseudoConsoleSession?`-Property hinzufügen; `FensterHandle`-Property entfernen | Offen | — |
| 11 | Logik | `KiAusfuehrungsService` erweitern: `StartWithPseudoConsoleAsync` hinzufügen; `GetPseudoConsoleSession` hinzufügen; `SetFensterHandle`/`GetFensterHandle` entfernen; `Dispose` um Session-Dispose erweitern | Offen | — |
| 12 | UI | `TerminalControl` anlegen (`FrameworkElement`-Subklasse; `Session`-DependencyProperty; `ReadLoopAsync`; `OnRender` mit `DrawingContext`; Keyboard-Handling; `SizeChanged`-Resize) | Offen | — |
| 13 | UI | `TaskDetailViewModel` umbauen: `PseudoConsoleSessionGestartet`-Event hinzufügen; `EmbeddedWindowHandle`, `CliProzessGestartet`, `GetRunningProcess`, `SetCliWindowHandle`, `GetCliWindowHandle` entfernen; `StartCliAndUpdateStateAsync` und `CliAutomatischNeustartenAsync` auf `StartWithPseudoConsoleAsync` umstellen | Offen | — |
| 14 | UI | `TaskDetailView.xaml` umbauen: `ProcessWindowHost` durch `<controls:TerminalControl x:Name="TerminalConsole"/>` ersetzen; `EmbeddedHandle`-Binding entfernen | Offen | — |
| 15 | UI | `TaskDetailView.xaml.cs` umbauen: `_pollCts`, `WaitForWindowHandleAsync`, `OnCliProzessGestartet` entfernen; `OnPseudoConsoleSessionGestartet` ergänzen; `Loaded`/`Unloaded`-Handler anpassen | Offen | — |
| 16 | UI | `ProcessWindowHost.cs` entfernen | Offen | — |
| 17 | Tests | `AnsiSequenceParserTests`-Klasse anlegen mit Tests: Plaintext, SGR-Farbe, SGR-Reset, 24-Bit-Farbe, Cursor-Move, Clear-Screen, Erase-Line, mehrteilige Pakete | Offen | — |
| 18 | Tests | `TerminalBufferTests`-Klasse anlegen mit Tests: Text schreiben, Cursor-Move, Newline-Scroll, Resize, Clear-Screen | Offen | — |
| 19 | Tests | `KiAusfuehrungsServiceTests` anpassen: `SetFensterHandle`/`GetFensterHandle`-Tests entfernen; `GetPseudoConsoleSession`-Test für null-Fall hinzufügen | Offen | — |
| 20 | Tests | `TaskDetailViewModelTests` anpassen: `EmbeddedWindowHandle`-, `CliProzessGestartet`-, `GetRunningProcess`-, `SetCliWindowHandle`-, `GetCliWindowHandle`-Assertions durch `PseudoConsoleSessionGestartet`-Assertion ersetzen | Offen | — |
| 21 | Tests | `CliEmbeddingServiceIntegrationTests` anpassen: API-Änderungen an `KiAusfuehrungsService` nachziehen | Offen | — |
| 22 | E2E-Tests | `E2E_AufgabeStarten` anpassen: `CliProzessGestartet`-Registrierung durch `PseudoConsoleSessionGestartet` ersetzen | Offen | — |
| 23 | E2E-Tests | `E2E_AutoStartCli` anpassen: analog zu `E2E_AufgabeStarten` | Offen | — |
| 24 | E2E-Tests | `E2E_PluginWechsel` anpassen: Fenster-Handle-Assertions entfernen; Session-basierte Assertions ergänzen | Offen | — |
| 25 | E2E-Tests | `E2E_ConPtyTerminalStart` anlegen: ConPTY-Session wird gestartet; `PseudoConsoleSessionGestartet` gefeuert; Session nicht null | Offen | — |
| 26 | E2E-Tests | `E2E_ConPtyKeyboardInput` anlegen: simulierter Tastendruck → kodiertes Byte im InputStream | Offen | — |
| 27 | E2E-Tests | `E2E_ConPtyResize` anlegen: `SizeChanged` → `ResizePseudoConsole` mit neuen Maßen aufgerufen | Offen | — |
| 28 | E2E-Tests | `E2E_ConPtyProcessEnd` anlegen: Prozessende → `IsCliRunning == false`, ReadLoop terminiert | Offen | — |
