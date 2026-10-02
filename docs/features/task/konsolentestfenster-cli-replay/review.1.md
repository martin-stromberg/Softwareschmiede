# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

## Umgesetzte Planelemente

### Neue Klassen / Dateien

- [x] `CliOutputChunkRecord` (Record) — `src/Softwareschmiede/Infrastructure/Terminal/CliOutputChunkRecord.cs`, `Offset` (TimeSpan) + `Data` (byte[]) vorhanden
- [x] `CliOutputAufzeichnung` (Datenmodell) — `src/Softwareschmiede/Infrastructure/Terminal/CliOutputAufzeichnung.cs`, alle Felder vorhanden: `AufgabeId`, `PluginName`, `StartUtc`, `Cols`, `Rows`, `IstVollstaendig`, `EndeUtc?`, `Chunks`
- [x] `CliOutputRecorder` (`ITerminalOutputSink`) — `src/Softwareschmiede/Infrastructure/Terminal/CliOutputRecorder.cs`: `TimeProvider`-Offsets, Byte-Budget mit `IstVollstaendig=false` + Aufnahmestopp bei Überschreitung (intaktes Präfix bleibt), idempotentes `Complete`/`CompleteAsync` (`EndeUtc` gesetzt), `GetAufzeichnung()`-Snapshot; implementiert bewusst **nicht** `ITerminalDiagnoseSink`
- [x] `ITerminalDiagnoseSink` (Interface) — `src/Softwareschmiede/Infrastructure/Terminal/ITerminalDiagnoseSink.cs`, `OnDiagnoseChunk(ReadOnlySpan<byte>)`
- [x] `CompositeTerminalOutputSink` (`ITerminalOutputSink` + `ITerminalDiagnoseSink`) — `src/Softwareschmiede/Infrastructure/Terminal/CompositeTerminalOutputSink.cs`: Fanout für `OnOutputChunk`, `OnDiagnoseChunk` nur an innere `ITerminalDiagnoseSink`-Senken, `Complete`/`CompleteAsync` drainen alle Senken
- [x] `CliReplayAufzeichnungStore` — `src/Softwareschmiede/Infrastructure/Terminal/CliReplayAufzeichnungStore.cs`: Magic `SWCLRPLY`, Version Int32, Header inkl. `EndeUtc` (0 = nicht gesetzt), `Cols`/`Rows`-Validierung `> 0`, Records `[OffsetTicks][Length][Bytes]` bis EOF, `InvalidDataException` bei allen Formatfehlern (inkl. abgeschnittener Records), Stream-API (`SchreibeAsync`/`LadeAsync`) + Datei-Wrapper (`SpeichernAsync`/`LadeAsync(pfad)`)
- [x] `TerminalReplaySession` (`ITerminalSession`) — `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs`: Ctor (`CliOutputAufzeichnung`, `TimeProvider`, `ILogger<TerminalReplaySession>`), eigener `TerminalBuffer`/`AnsiSequenceParser`/`_renderLock`, Wiedergabe-Task mit async-TCS-Pause-Gate und `Task.Delay(delay, _timeProvider, ct)` + `min(realePause, ZeitrafferSchwelle)`, `OutputChunk`→Parse/Apply→`BufferChanged`-Sequenz wie `PseudoConsoleSession.ReadLoopAsync`
- [x] `TerminalReplaySession`-Abspiel-Member — `WiedergabeStarten` (idempotent via Interlocked), `Pausieren`, `Fortsetzen`, `IstPausiert`, `ZeitrafferSchwelle`, `AktuellerChunkIndex`, `ChunkAnzahl`
- [x] `TerminalReplaySession`-Stub-Member — `Process` = nicht-gestartetes `new Process()`, `InputStream`/`OutputStream` = `Stream.Null`, `IsPseudoTerminal` = `false`, `Resize` = `true`, `WriteInputAsync`/`WritePromptAsync`/`MarkInputActivity`/`MarkOutputActivity` = No-Op, `Failure` = `null` (`Failed` nie ausgelöst), `ExitCode` = `null`, `DrainOutputAsync` = `true`, `RuntimeStatus` `Laeuft`/`Inaktiv` via `RuntimeStatusChanged`, `RebuildBufferFromReplay` unter `_renderLock` aus abgespielten Chunks, `Exited` am Ende mit `ExitCode = null`, `Dispose` bricht Wiedergabe ab
- [x] `ICliReplayExportService` / `CliReplayExportService` — `src/Softwareschmiede.App/Services/CliReplayExportService.cs`: `GetCliAufzeichnung` → `InvalidOperationException` („Für diese Aufgabe liegt keine Aufzeichnung vor.") bei `null`, sonst `CliReplayAufzeichnungStore.SpeichernAsync`
- [x] `CliChunkQuelltextFormatter` (statisch) — `src/Softwareschmiede.App/Services/CliChunkQuelltextFormatter.cs`: `ESC` → `␛`, `CR` → `\r`, `LF` → `\n`, `TAB` → `\t`, übrige Steuerbytes/DEL → `\xNN`, UTF-8-Dekodierung
- [x] `CliChunkAnzeigeEintrag` — `src/Softwareschmiede.App/ViewModels/CliChunkAnzeigeEintrag.cs`: `Index`, `Offset`, `Laenge`, `Quelltext`
- [x] `KonsolenTestViewModel` — `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs`: alle Ctor-Deps (`IDialogService`, `CliReplayAufzeichnungStore`, `IOptions<TerminalSessionOptions>`, `TimeProvider`, `ILogger`, optionaler `dispatcherInvoke`-Test-Hook), Commands (`AufzeichnungOeffnen`, `WiedergabeStarten`, `WiedergabePausieren`-Toggle, `Schliessen`), Properties (`Session`, `QuellEintraege`, `AktuellerQuellEintrag`, `DateiPfad`, `StatusText`, `PositionsText`, `IstWiedergabeAktiv`, `IstPausiert`, `ZeitrafferSchwelleText` mit Validierung → `FehlerMeldung`, `FehlerMeldung`, `UnvollstaendigHinweis`), `CloseRequested`-Event, Cols/Rows-Fallback auf Options-Defaults, `BufferChanged`/`Exited`-Sync über Dispatcher
- [x] `KonsolenTestDialog` (Window) — `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml(.cs)`: `Title="Konsolentest"`, Werkzeugleiste, `TerminalControl` (`AutomationName="ReplayTerminal"`, `Session`-Bindung), `ListView QuellChunkListe` mit `SelectedItem`-Sync + `ScrollIntoView` im Code-behind, Fehlerbanner (`AutomationProperties.Name="FehlerMeldung"` + `HelpText`-Binding + `NullOrEmptyToVisibilityConverter`), `UnvollstaendigHinweis`-Element, alle geplanten `AutomationProperties.Name`-Werte (`AufzeichnungOeffnen`, `AufzeichnungPfad`, `WiedergabeStarten`, `WiedergabePausieren`, `ZeitrafferSchwelle`, `WiedergabeStatus`, `WiedergabePosition`, `KonsolenTestSchliessen`), Ctor `KonsolenTestDialog(KonsolenTestViewModel)` + `CloseRequested`-Verdrahtung
- [x] `KonsolenTestDialogView` (E2E-Wrapper) — `src/Softwareschmiede.Tests/E2E/Views/Dialogs/KonsolenTestDialogView.cs`: `DialogTitle = "Konsolentest"`, eigene `OpenDialogCondition` („Öffnen"/„Open"/„CLI-Aufzeichnung öffnen"), `OeffneAufzeichnung`, `OeffneAufzeichnungAbbrechen`, `IstFehlerSichtbar`, `GetFehlerMeldung` (beide auf `GetDialogWindow()`), `SetZeitrafferSchwelle`, `StartWiedergabe`, `PausierenToggle`, `GetStatusText`, `GetPositionsText`, `WarteAufStatus`, `GetQuellEintraegeCount`, `GetQuellEintragText`, `Schliessen`

### Änderungen an bestehenden Klassen

- [x] `TerminalSessionOptions.AufzeichnungByteBudget` (`int`, Default `8 * 1024 * 1024`) — `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionOptions.cs:21`
- [x] `KiAusfuehrungsService`-Konstruktor — +`IOptions<TerminalSessionOptions>`, +`TimeProvider` (`KiAusfuehrungsService.cs:38-48`)
- [x] `KiAusfuehrungsService._aufzeichnungen` (`ConcurrentDictionary<Guid, CliOutputRecorder>`) — vorhanden
- [x] `KiAusfuehrungsService.GetCliAufzeichnung(Guid)` → `CliOutputAufzeichnung?` — vorhanden (Z. 341), liefert Snapshot auch nach Session-Ende
- [x] `KiAusfuehrungsService.StartTerminalSessionAsync` — Recorder nur bei `AufzeichnungByteBudget > 0` (mit `spec.PluginName`, `DefaultCols`/`DefaultRows`, `_timeProvider`), Composite-Senke bei aktivem Recorder sonst `outputWriter` direkt (keine einelementige Composite), Senke an `StartAsync` übergeben, `CliProcessHandle.OutputSink` = dieselbe Senke, `catch` drainet die `outputSink`-Variable, `_aufzeichnungen[aufgabeId]`-Eintrag nach erfolgreichem Start
- [x] `CliOutputProtokollWriter` implementiert `ITerminalDiagnoseSink` — `OnDiagnoseChunk` leitet auf `OnOutputChunk` weiter (`CliOutputProtokollWriter.cs:59`)
- [x] `TerminalSessionService.WriteDiagnosis` — `ITerminalDiagnoseSink`-Routing mit `OnOutputChunk`-Fallback (`TerminalSessionService.cs:143-149`)
- [x] `IDialogService.ShowOpenFileDialogAsync` + `ShowKonsolenTestDialogAsync` — deklariert und in `WpfDialogService` implementiert (`Microsoft.Win32.OpenFileDialog` auf UI-Dispatcher; modal via `ShowDialog`, `Owner = MainWindow`)
- [x] `TaskDetailViewModel` — optionaler Ctor-Parameter `ICliReplayExportService? cliReplayExportService = null` mit Fallback-Instanziierung, `KannCliReplayExportieren`, `ExportCliReplayCommand`, `ExportCliReplayAsync` (Standardname `cli-replay-{aufgabeId:N}.clireplay`, `.clireplay`-Endungsprüfung → `FehlerMeldung`, Fehler → `FehlerMeldung`)
- [x] `TaskDetailView.xaml` — `RibbonLargeButton` „Aufzeichnung exportieren" (`AutomationName="CliReplayExport"`) in Ribbon-Gruppe „CLI" neben `CliRawExport`
- [x] `SettingsViewModel` — `IDialogService` + `IServiceProvider` als erforderliche Ctor-Parameter, `KonsolenTestOeffnenCommand` (`GetRequiredService<KonsolenTestViewModel>()` + `ShowKonsolenTestDialogAsync`, Fehler → `FehlerMeldung`)
- [x] `SettingsView.xaml` — Abschnitt „Diagnose" am Ende des Tabs „Allgemein" mit Button `KonsolenTestOeffnen` + Beschreibungstext
- [x] `App.xaml.cs` — `AddSingleton<CliReplayAufzeichnungStore>()`, `AddSingleton<ICliReplayExportService, CliReplayExportService>()`, `AddTransient<KonsolenTestViewModel>()`
- [x] `appsettings.json` — `"AufzeichnungByteBudget": 8388608` in Sektion `Terminal`
- [x] `WindowExtensions.DialogFactories` — `w => new KonsolenTestDialogView(w)` eingetragen
- [x] Bugfix `PseudoConsoleSession` (Rebuild-Race/Doppelausgabe) — `_replayBuffer.Append` liegt nun **innerhalb** `_renderLock` zusammen mit Parse/Apply (`PseudoConsoleSession.cs:313-318`)
- [x] Bugfix `AnsiSequenceParser` (ESC-State) — ESC bricht `State.Csi` und `State.CsiQuestion` ab (`AnsiSequenceParser.cs:114-122, 140-144`); DCS/SOS/PM/APC (`P`/`X`/`^`/`_`) laufen in den String-Überspring-Zustand (`:75-81`); OSC-ESC geht in `State.Escape` ohne Folge-Byte-Vorwegnahme → ST-Rest über Chunk-Grenze korrekt verworfen (`:163-171`)
- [x] Bugfix `TerminalReplaySession` (Pause-Deadlock) — async `TaskCompletionSource`-Gate mit `RunContinuationsAsynchronously` statt blockierendem Wait (`TerminalReplaySession.cs:22-27, 265-271`)

### Testinfrastruktur-Anpassungen

- [x] `TestKiAusfuehrungsServiceFactory` — optionale Parameter `terminalOptions`, `timeProvider` (`IOptions.Create`-Wrapping)
- [x] `TestTerminalSessionFactory.CreateChunkEmittingLauncher` — Launcher-Double füttert `outputSink.OnOutputChunk` mit Test-Chunks beim Start
- [x] `SettingsViewModelTests.CreateSut` + `SettingsViewModelTests_IdePlugin.CreateSut` — um `Mock<IDialogService>` + `Mock<IServiceProvider>` erweitert
- [x] `MainWindowViewModelUpdateTestBase` — `services.AddSingleton(_dialogServiceMock.Object)` registriert (Z. 79)
- [x] `SettingsView`-E2E-Wrapper — `OpenKonsolenTestDialog()` (Tab „Allgemein" → `KonsolenTestOeffnen`)
- [x] `TaskDetailView`-E2E-Wrapper — `ExportCliReplay(zielPfad)` (klickt `CliReplayExport` + `HandleSaveFileDialog`)

### Neue Tests (Code-seitig verifiziert)

- [x] `CliOutputRecorderTests` — 6 Tests (Chunk-Grenzen/Bytekopie + Offsets, Budget-Präfix, idempotentes Complete, Snapshot-Unabhängigkeit, leere Chunks, kein `ITerminalDiagnoseSink`)
- [x] `CompositeTerminalOutputSinkTests` — Fanout, Diagnose-Routing nur an Diagnose-Senken, `Complete`-Drain
- [x] `CliReplayAufzeichnungStoreTests` — 9 Tests (Roundtrip inkl. `EndeUtc`/`IstVollstaendig`, Magic-/Versions-/Geometrie-/Längen-Fehler, abgeschnittene Header/Records, Datei-Wrapper)
- [x] `TerminalReplaySessionTests` — 9 Tests inkl. `Wiedergabe_ErzeugtGleichenBufferWieLiveSession` (Vergleich gegen `PseudoConsoleSession`), Zeitraffer, Pause via `FakeTimeProvider` (Inline-Continuation-Fall abgedeckt), Rebuild-Präfix, Exited/RuntimeStatus, Stubs, Dispose
- [x] `CliChunkQuelltextFormatterTests` — Steuerzeichen-Sichtbarmachung, `\xNN`, UTF-8-Mehrbyte
- [x] `KonsolenTestViewModelTests` — 9 Tests (Laden, Formatfehler, Dialog-Abbruch, `UnvollstaendigHinweis`, Wiedergabe-Sync, Pause/Fortsetzen, `ZeitrafferSchwelleText` gültig + ungültig/negativ mit Schwellen-Erhalt, `CloseRequested`, Commands inert)
- [x] `TaskDetailViewModelTests_CliReplayExport` — CanExecute, Dialog-Abbruch, Service-Aufruf mit Pfad, falsche Endung, Service-Fehler → `FehlerMeldung`
- [x] `CliReplayExportServiceTests` — `InvalidOperationException` ohne Aufzeichnung; Happy Path über `TestKiAusfuehrungsServiceFactory` + `CreateChunkEmittingLauncher` → echte ladbare `.clireplay`-Datei
- [x] `KiAusfuehrungsServiceCliAufzeichnungTests` — 6 Tests (Chunks in `GetCliAufzeichnung` inkl. Metadaten, `null` ohne Session, Budget-Opt-out ohne Composite, Composite-Typ bei aktivem Budget, Budget-Überschreitung mit Präfix, `TimeProvider`-Zeitstempel)
- [x] `TerminalSessionServiceTests.StartAsync_DiagnoseFaehigeSenke_ErhaeltMarkerUeberDiagnoseKanal` — Diagnose-Kanal-Routing (Fallback über `OnOutputChunk` weiterhin durch die Bestandstests `*_PipeMitDiagnose*` abgesichert)
- [x] `CliOutputProtokollWriterTests.OnDiagnoseChunk_SchreibtMarkerAlsProtokollzeilen`
- [x] Regressionstest Rebuild-Race — `PseudoConsoleSessionTests.ReadLoopAsync_RebuildAusOutputSink_WendetChunkNichtDoppeltAn`
- [x] Regressionstests Parser — `Parse_EscInCsi_*`, `Parse_EscInCsiQuestion_*`, `Parse_StringSequenzen_WerdenUebersprungen` (DCS/SOS/PM/APC, BEL + ST), `Parse_OscStAnChunkGrenze_*`, `Parse_OscMitStImSelbenChunk_*`, `Parse_EscInOsc_*`

## Offene Aufgaben

- [ ] Task 38 / Plan-Schritt 16 „Devin-CLI-Ursachenanalyse" — **teilweise umgesetzt:** Die Live-Aufzeichnung einer echten Devin-CLI-Session und die Reproduktion im Konsolentestfenster ist nicht erfolgt (in dieser Sandbox nicht möglich — kein konfigurierbares Devin-Plugin/keine CLI). Stattdessen wurden per statischer Analyse drei reale Defekte gefunden und behoben (Rebuild-Race in `PseudoConsoleSession`, ESC-State-/String-Sequenz-Defekte in `AnsiSequenceParser`, Pause-Deadlock in `TerminalReplaySession`), jeweils mit Regressionstests. Die Begründung liegt im Plan-Rahmen (Ziel des Schritts: Defekte finden und beheben — erreicht), die verbleibende Lücke ist der geforderte **End-to-End-Nachweis an einer echten Devin-CLI-Aufzeichnung** außerhalb der Sandbox.
- [ ] Task 44 / Pflicht-E2E `KonsolenTestfenster_..._E2E` — **teilweise umgesetzt:** `E2E_KonsolenTestfenster.cs` ist angelegt, in `RunGeneralTests` eingehängt und deckt Öffnen, OpenFileDialog-Abbruch, Formatfehler-Banner, Zeitraffer-Setzen, Laden, Abspielen bis „Wiedergabe beendet." sowie Quell-Liste ab. Die im Plan-Pflicht-Szenario (plan.md Z. 309) geforderte Phase **„Pausieren (keine neuen Chunks) → Fortsetzen"** fehlt im E2E-Ablauf — der Wrapper `KonsolenTestDialogView.PausierenToggle()` existiert, wird aber in keinem E2E-Szenario aufgerufen (Pause ist nur auf Unit-Ebene via `TerminalReplaySessionTests`/`KonsolenTestViewModelTests` abgesichert). Hinweis: mit `ZeitrafferSchwelle = "0"` und nur zwei Chunks ist ein Pause-Klick schwer deterministisch — ggf. zweite Aufzeichnung mit längerer Inter-Chunk-Pause im selben Szenario verwenden.

## Hinweise

- Build-Verifikation: `dotnet build src/Softwareschmiede/Softwareschmiede.csproj` und `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` — beide erfolgreich, 0 Warnungen/0 Fehler (inkrementell).
- Kleinigkeit ohne Planverstoß: `KonsolenTestViewModel` übergibt keinen `ILogger<TerminalReplaySession>` an die Session (Parameter ist optional, Default `NullLogger`) — plankonform, nur erwähnenswert falls Diagnose-Logs der Replay-Session gewünscht sind.
- Die Tasks-Datei nannte „8 Tests" für `CliOutputRecorderTests` (tatsächlich 6 Methoden) und „2 neue Tests" für `TerminalSessionServiceTests.WriteDiagnosis_*` (tatsächlich 1 neuer Test; der `OnOutputChunk`-Fallback ist durch die unverändert weiterlaufenden Bestandstests `*_PipeMitDiagnose*` gedeckt) — Zahlen in der Tasks-Datei entsprechend korrigiert.
