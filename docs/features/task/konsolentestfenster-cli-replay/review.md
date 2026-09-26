# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Nacharbeit-Lauf (`continue.md`): Die dort gemeldeten Review-/Usability-Befunde wurden umgesetzt — keine Planelemente entfernt oder verändert, alle zuvor als umgesetzt verifizierten Punkte gelten weiterhin. Verifiziert am geänderten Code:

## Umgesetzte Planelemente

### Neue Klassen / Dateien

- [x] `CliOutputChunkRecord` (Record) — `src/Softwareschmiede/Infrastructure/Terminal/CliOutputChunkRecord.cs`, `Offset` (TimeSpan) + `Data` (byte[]) vorhanden
- [x] `CliOutputAufzeichnung` (Datenmodell) — `src/Softwareschmiede/Infrastructure/Terminal/CliOutputAufzeichnung.cs`, alle Felder vorhanden: `AufgabeId`, `PluginName`, `StartUtc`, `Cols`, `Rows`, `IstVollstaendig`, `EndeUtc?`, `Chunks`
- [x] `CliOutputRecorder` (`ITerminalOutputSink`) — `src/Softwareschmiede/Infrastructure/Terminal/CliOutputRecorder.cs`: `TimeProvider`-Offsets, Byte-Budget mit `IstVollstaendig=false` + Aufnahmestopp bei Überschreitung, idempotentes `Complete`/`CompleteAsync`, `GetAufzeichnung()`-Snapshot; implementiert bewusst nicht `ITerminalDiagnoseSink`
- [x] `ITerminalDiagnoseSink` (Interface) — `src/Softwareschmiede/Infrastructure/Terminal/ITerminalDiagnoseSink.cs`, `OnDiagnoseChunk(ReadOnlySpan<byte>)`
- [x] `CompositeTerminalOutputSink` (`ITerminalOutputSink` + `ITerminalDiagnoseSink`) — `src/Softwareschmiede/Infrastructure/Terminal/CompositeTerminalOutputSink.cs`: Fanout, Diagnose-Routing nur an `ITerminalDiagnoseSink`-Senken, `Complete`/`CompleteAsync`-Drain
- [x] `CliReplayAufzeichnungStore` — `src/Softwareschmiede/Infrastructure/Terminal/CliReplayAufzeichnungStore.cs`: Magic `SWCLRPLY`, Version, Header inkl. `EndeUtc`/`Cols`/`Rows`-Validierung `> 0`, Records `[OffsetTicks][Length][Bytes]`, `InvalidDataException` bei Formatfehlern, `UtcTicks`-Normalisierung, echt async, `SpeichernAsync`/`LadeAsync` (Stream + Datei)
- [x] `TerminalReplaySession` (`ITerminalSession`) — `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs`: Ctor (`CliOutputAufzeichnung`, `TimeProvider`, optionaler `ILogger<TerminalReplaySession>`), eigener `TerminalBuffer`/`AnsiSequenceParser`/`_renderLock`, Wiedergabe-Task mit async-TCS-Pause-Gate (`RunContinuationsAsynchronously`) und `Task.Delay(delay, _timeProvider, ct)` + `min(realePause, ZeitrafferSchwelle)`; `OutputChunk`→Parse/Apply→`BufferChanged`-Sequenz wie `PseudoConsoleSession.ReadLoopAsync`
- [x] `TerminalReplaySession`-Abspiel-Member — `WiedergabeStarten` (idempotent via Interlocked), `Pausieren`/`Fortsetzen` mit `_pauseLock`-Invariante „pausiert ⇒ Gate geschlossen", `IstPausiert`, `ZeitrafferSchwelle`, `AktuellerChunkIndex`; In-Flight-Chunk bei `Pausieren` unter `_renderLock` zurückgehalten. **Nacharbeit:** `ct.ThrowIfCancellationRequested()` am Schleifenanfang (Z. 239) und nach dem Gate-Wait im In-Flight-Retry (Z. 263) — die Schleife drain-t nach `Dispose` bei `ZeitrafferSchwelle = 0` (delay == 0, bereits erfüllter Gate-Task) keine Chunks mehr auf der disposed Session.
- [x] `TerminalReplaySession`-Stub-Member — `Process` = nicht-gestartetes `new Process()` (dokumentiert), `InputStream`/`OutputStream` = `Stream.Null`, `IsPseudoTerminal` = `false`, `Resize` = `true`, `WriteInputAsync`/`WritePromptAsync`/`MarkInputActivity`/`MarkOutputActivity` = No-Op, `Failure` = `null`, `ExitCode` = `null`, `DrainOutputAsync` = `true`, `RuntimeStatus` `Laeuft`/`Inaktiv` via `RuntimeStatusChanged`, `RebuildBufferFromReplay` unter `_renderLock`, `Exited` am Ende mit `ExitCode = null`, `Dispose` bricht Wiedergabe ab
- [x] `ICliReplayExportService` / `CliReplayExportService` — `src/Softwareschmiede.App/Services/CliReplayExportService.cs`: `ExportCliReplayAsync` (`GetCliAufzeichnung` → `InvalidOperationException` bei `null`, sonst `SpeichernAsync`); `HatAufzeichnung`
- [x] `CliChunkQuelltextFormatter` (statisch) — `src/Softwareschmiede.App/Services/CliChunkQuelltextFormatter.cs`: `ESC` → `␛`, `CR`/`LF`/`TAB` markiert, übrige Steuerbytes → `\xNN`, UTF-8-Dekodierung
- [x] `CliChunkAnzeigeEintrag` — `src/Softwareschmiede.App/ViewModels/CliChunkAnzeigeEintrag.cs`: `Index`, `Offset`, `Laenge`, `Quelltext`
- [x] `KonsolenTestViewModel` — `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs`: Commands `AufzeichnungOeffnen`, `WiedergabeStarten`, `WiedergabeNeustarten`, `WiedergabePausieren`-Toggle, `Schliessen`; Properties `Session`, `QuellEintraege`, `AktuellerQuellEintrag`, `DateiPfad`, `StatusText`, `PositionsText`, `IstWiedergabeAktiv` (mit `RelayCommand.Refresh`), `IstPausiert`, `ZeitrafferSchwelleText` (Validierung inkl. `TimeSpan.MaxValue`-Obergrenze), `FehlerMeldung`, `UnvollstaendigHinweis`, `CloseRequested`; `EntsorgeReplaySession` mit Event-Abmeldung + `_replaySession`/`Session`-Nullung, Neustart-/`_wiedergabeBeendet`-Pfad über `ErsetzeReplaySessionDurchFrische`, Quell-Formatierung via `Task.Run`. **Nacharbeit:** `_zeitrafferSchwelle`-Feld (Z. 33–37) hält die zuletzt gültige Schwelle; `ErzeugeReplaySession` (Z. 256–266) wendet sie immer an — eine frische Session fällt bei stehengelassenem Fehlertext nicht mehr still auf `TimeSpan.MaxValue` (Echtzeit) zurück.
- [x] `KonsolenTestDialog` (Window) — `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml(.cs)`: `Title="Konsolentest"`, Werkzeugleiste inkl. „Neu starten" (`WiedergabeNeustarten`), `TerminalControl` (`ReplayTerminal`), `ListView QuellChunkListe` mit `SelectedItem`-Sync + `ScrollIntoView`, Fehlerbanner + `UnvollstaendigHinweis` mit `NullOrEmptyToVisibilityConverter` und `HelpText`-Bindings, Ctor + `CloseRequested`-/`Closed`-Verdrahtung. **Nacharbeit:** Pfad-`TextBlock` (Z. 29–38) mit `MaxWidth="320"`, `TextTrimming="CharacterEllipsis"`, `ToolTip`/`HelpText` auf `DateiPfad` — Überlauf der Werkzeugleiste behoben.
- [x] `KonsolenTestDialogView` (E2E-Wrapper) — `src/Softwareschmiede.Tests/E2E/Views/Dialogs/KonsolenTestDialogView.cs`: `DialogTitle = "Konsolentest"`, eigene `OpenDialogCondition`, `OeffneAufzeichnung` (Autocomplete-tolerant), `OeffneAufzeichnungAbbrechen`, `IstFehlerSichtbar`/`GetFehlerMeldung`/`WarteAufFehlerSichtbar` auf `GetDialogWindow()`, `SetZeitrafferSchwelle`, `StartWiedergabe`/`NeustartWiedergabe`/`PausierenToggle` über `WaitForEnabledElement`, `GetStatusText`/`GetPositionsText` via HelpText, `WarteAufStatus`/`WarteAufPosition`/`WarteAufQuellEintraege`, `GetQuellEintraegeCount`/`GetQuellEintragText`, `Schliessen`

### Änderungen an bestehenden Klassen

- [x] `TerminalSessionOptions.AufzeichnungByteBudget` (`int`, Default 8 MB) — `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionOptions.cs:20`
- [x] `KiAusfuehrungsService` — Ctor +`IOptions<TerminalSessionOptions>`/`TimeProvider`; `_aufzeichnungen` (`ConcurrentDictionary<Guid, CliOutputRecorder>`) mit `MaxAufzeichnungenAnzahl`-Retention (8, `LinkedList`-Reihenfolge); `GetCliAufzeichnung(Guid)`; `StartTerminalSessionAsync` erzeugt Recorder nur bei Budget > 0 (Composite) sonst `outputWriter` direkt, `CliProcessHandle.OutputSink` = dieselbe Senke, `catch` drainet die `outputSink`-Variable
- [x] `CliOutputProtokollWriter` implementiert `ITerminalDiagnoseSink` — `OnDiagnoseChunk` → Accumulator-Pfad
- [x] `TerminalSessionService.WriteDiagnosis` — `ITerminalDiagnoseSink`-Routing mit `OnOutputChunk`-Fallback
- [x] `IDialogService.ShowOpenFileDialogAsync` + `ShowKonsolenTestDialogAsync` — deklariert und in `WpfDialogService` implementiert: `OpenFileDialog` auf UI-Dispatcher; `ShowKonsolenTestDialogAsync` **nicht-modal** via `dialog.Show()` mit `Owner = MainWindow`; `AktivesDialogOwnerFenster()`
- [x] `TaskDetailViewModel` — optionaler Ctor-Parameter `ICliReplayExportService?` mit Fallback, `KannCliReplayExportieren`, `ExportCliReplayCommand`, `ExportCliReplayAsync` (`HatAufzeichnung`-Vorab-Prüfung, Standardname `cli-replay-{aufgabeId:N}.clireplay`, Filter „CLI-Replay-Dateien", `.clireplay`-Endungsprüfung, Fehler → `FehlerMeldung`). **Nacharbeit:** „keine Aufzeichnung"-Meldung (Z. 2367) erwähnt jetzt die Flüchtigkeit des Mitschnitts (nur Arbeitsspeicher, letzte 8 Aufgaben, Verlust bei Neustart/durch neuere Ausführungen).
- [x] `TaskDetailView.xaml` — `RibbonLargeButton` „Aufzeichnung exportieren" (`CliReplayExport`) neben `CliRawExport`, Tooltips auf beiden
- [x] `SettingsViewModel` — `IDialogService` + `IServiceProvider` erforderliche Ctor-Parameter, `KonsolenTestOeffnenCommand`
- [x] `SettingsView.xaml` — Abschnitt „Diagnose" im Tab „Allgemein" mit Button `KonsolenTestOeffnen`
- [x] `App.xaml.cs` — `AddSingleton<CliReplayAufzeichnungStore>()`, `AddSingleton<ICliReplayExportService, CliReplayExportService>()`, `AddTransient<KonsolenTestViewModel>()`
- [x] `appsettings.json` — `"AufzeichnungByteBudget": 8388608` in Sektion `Terminal`
- [x] `WindowExtensions.DialogFactories` — `w => new KonsolenTestDialogView(w)` eingetragen
- [x] Bugfix `PseudoConsoleSession` — `_replayBuffer.Append` + Parse/Apply unter gemeinsamem `_renderLock`
- [x] Bugfix `AnsiSequenceParser` — ESC-Abbruch in CSI/CsiQuestion, DCS/SOS/PM/APC-Überspringen, OSC-ESC ohne Byte-Vorwegnahme
- [x] Bugfix `TerminalReplaySession` — async TCS-Pause-Gate statt blockierendem Wait

### Testinfrastruktur-Anpassungen

- [x] `TestKiAusfuehrungsServiceFactory` — optionale Parameter `terminalOptions`, `timeProvider`
- [x] `TestTerminalSessionFactory.CreateChunkEmittingLauncher` — Launcher-Double füttert `outputSink.OnOutputChunk`
- [x] `SettingsViewModelTests.CreateSut` + `SettingsViewModelTests_IdePlugin.CreateSut` — um `Mock<IDialogService>` + `Mock<IServiceProvider>` erweitert
- [x] `MainWindowViewModelUpdateTestBase` — `services.AddSingleton(_dialogServiceMock.Object)`
- [x] `SettingsView`-E2E-Wrapper — `OpenKonsolenTestDialog()`
- [x] `TaskDetailView`-E2E-Wrapper — `ExportCliReplay(zielPfad)` inkl. `null`-Abbruchpfad via `HandleSaveFileDialog` (ESC)

### Neue Tests (Code-seitig verifiziert)

- [x] `CliOutputRecorderTests` — 6 Tests
- [x] `CompositeTerminalOutputSinkTests` — 3 Tests
- [x] `CliReplayAufzeichnungStoreTests` — 11 Tests
- [x] `TerminalReplaySessionTests` — 10 Tests inkl. `Wiedergabe_ErzeugtGleichenBufferWieLiveSession`; **Nacharbeit:** `Dispose_MitZeitrafferNull_BrichtAbStattChunksZuDraenen` (deterministisch über blockierenden `OutputChunk`-Handler — belegt den Abbruch zwischen Chunks bei `delay == 0`)
- [x] `CliChunkQuelltextFormatterTests` — 4 Tests
- [x] `KonsolenTestViewModelTests` — 13 Testmethoden/14 Fälle; **Nacharbeit:** `ZeitrafferSchwelleText_Ungueltig_UebernimmtLetzteGueltigeSchwelleAufFrischeSession` (frische Session nach erneutem Laden behält die zuletzt gültige Schwelle statt Echtzeit-Default)
- [x] `TaskDetailViewModelTests_CliReplayExport` — 6 Tests
- [x] `CliReplayExportServiceTests` — 3 Tests
- [x] `KiAusfuehrungsServiceCliAufzeichnungTests` — 7 Tests
- [x] `TerminalSessionServiceTests.StartAsync_DiagnoseFaehigeSenke_ErhaeltMarkerUeberDiagnoseKanal`
- [x] `CliOutputProtokollWriterTests.OnDiagnoseChunk_SchreibtMarkerAlsProtokollzeilen`
- [x] Regressionstests — `PseudoConsoleSessionTests.ReadLoopAsync_RebuildAusOutputSink_WendetChunkNichtDoppeltAn`, `AnsiSequenceParserTests` (`Parse_EscInCsi_*`, `Parse_EscInCsiQuestion_*`, `Parse_StringSequenzen_WerdenUebersprungen`, `Parse_OscStAnChunkGrenze_*`, `Parse_OscMitStImSelbenChunk_*`, `Parse_EscInOsc_*`)

### E2E (Pflicht + Empfohlen)

- [x] Task 44 / `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` in `RunGeneralTests` (`MainTest.cs:36`) — konsolidiertes Szenario inkl. ANSI-Quelltext-Assertions, Pause-/Neustart-Phasen, Formatfehler-Banner, OpenFileDialog-Abbruch und `finally`-Cleanup (`TryCloseKonsolenTestfenster`)
- [x] Task 45 / `ConPtyCliReplayExport_ExportOeffnetKonsolenTestUndSpieltAb_E2E` als Phase in `ConPtyLifecycle_*` (`E2E_ConPtyLifecycle.cs:53/70`) — Abbruch-Subphase + Magic-Check + Laden/Abspielen. **Nacharbeit:** `settings`/`dialog` außerhalb des `try` deklariert (Z. 75–76), `TryCloseKonsolenTestfenster(dialog, settings)` im `finally` (Z. 116) — konsistent zum Schwester-Test; Lane in Sandbox via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` übersprungen

## Offene Aufgaben

- [ ] Task 38 / Plan-Schritt 16 „Devin-CLI-Ursachenanalyse" — **begründet offen:** Die Live-Aufzeichnung einer echten Devin-CLI-Session und die Reproduktion im Konsolentestfenster ist in dieser Sandbox nicht möglich (kein konfigurierbares Devin-Plugin/keine CLI). Stattdessen wurden per statischer Analyse drei reale Defekte gefunden und behoben (Rebuild-Race in `PseudoConsoleSession`, ESC-State-/String-Sequenz-Defekte in `AnsiSequenceParser`, Pause-Deadlock in `TerminalReplaySession`), jeweils mit Regressionstests. Verbleibende Lücke: der geforderte **End-to-End-Nachweis an einer echten Devin-CLI-Aufzeichnung** außerhalb der Sandbox.

## Hinweise

- **Eigenständige Verifikation im Nacharbeit-Lauf:** `dotnet build Softwareschmiede.slnx` — 0 Fehler (1 pre-existing Warnung CS8602 in `CliOutputProtokollWriterTests.cs:151`, unverändert). `dotnet test ... --filter "FullyQualifiedName~TerminalReplaySessionTests|FullyQualifiedName~KonsolenTestViewModelTests|FullyQualifiedName~TaskDetailViewModelTests_CliReplayExport"` (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`) — **30/30 grün** inkl. beider neuer Regressionstests.
- **Dokumentierte Planabweichung (Iteration 3, unverändert vertretbar):** `IOptions<TerminalSessionOptions>` ist kein Ctor-Parameter von `KonsolenTestViewModel` mehr — plan.md Z. 85 listet die Dep noch für den Cols/Rows-Fallback. Der Fallback wurde als toter Code entfernt, weil `CliReplayAufzeichnungStore.LadeAsync` `Cols`/`Rows > 0` bereits validiert.
- **Dokumentierte Planabweichung (Iteration 2, unverändert):** Konsolentestfenster nicht-modal (`WpfDialogService` `Show()` statt `ShowDialog()`) — per Usability-Review begründet; plan.md nicht nachgezogen.
