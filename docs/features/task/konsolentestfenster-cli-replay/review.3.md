# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

## Umgesetzte Planelemente

### Neue Klassen / Dateien

- [x] `CliOutputChunkRecord` (Record) — `src/Softwareschmiede/Infrastructure/Terminal/CliOutputChunkRecord.cs`, `Offset` (TimeSpan) + `Data` (byte[]) vorhanden
- [x] `CliOutputAufzeichnung` (Datenmodell) — `src/Softwareschmiede/Infrastructure/Terminal/CliOutputAufzeichnung.cs`, alle Felder vorhanden: `AufgabeId`, `PluginName`, `StartUtc`, `Cols`, `Rows`, `IstVollstaendig`, `EndeUtc?`, `Chunks`
- [x] `CliOutputRecorder` (`ITerminalOutputSink`) — `src/Softwareschmiede/Infrastructure/Terminal/CliOutputRecorder.cs`: `TimeProvider`-Offsets, Byte-Budget mit `IstVollstaendig=false` + Aufnahmestopp bei Überschreitung, idempotentes `Complete`/`CompleteAsync`, `GetAufzeichnung()`-Snapshot; implementiert bewusst nicht `ITerminalDiagnoseSink`
- [x] `ITerminalDiagnoseSink` (Interface) — `src/Softwareschmiede/Infrastructure/Terminal/ITerminalDiagnoseSink.cs`, `OnDiagnoseChunk(ReadOnlySpan<byte>)`
- [x] `CompositeTerminalOutputSink` (`ITerminalOutputSink` + `ITerminalDiagnoseSink`) — `src/Softwareschmiede/Infrastructure/Terminal/CompositeTerminalOutputSink.cs`: Fanout, Diagnose-Routing nur an `ITerminalDiagnoseSink`-Senken, `Complete`/`CompleteAsync`-Drain
- [x] `CliReplayAufzeichnungStore` — `src/Softwareschmiede/Infrastructure/Terminal/CliReplayAufzeichnungStore.cs`: Magic `SWCLRPLY`, Version, Header inkl. `EndeUtc`/`Cols`/`Rows`-Validierung `> 0`, Records `[OffsetTicks][Length][Bytes]`, `InvalidDataException` bei Formatfehlern, `UtcTicks`-Normalisierung, echt async, `SpeichernAsync`/`LadeAsync` (Stream + Datei)
- [x] `TerminalReplaySession` (`ITerminalSession`) — `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs`: Ctor (`CliOutputAufzeichnung`, `TimeProvider`, optionaler `ILogger<TerminalReplaySession>`), eigener `TerminalBuffer`/`AnsiSequenceParser`/`_renderLock`, Wiedergabe-Task mit async-TCS-Pause-Gate (`RunContinuationsAsynchronously`, Z. 26–30/296–301) und `Task.Delay(delay, _timeProvider, ct)` + `min(realePause, ZeitrafferSchwelle)` (Z. 240–245), `OutputChunk`→Parse/Apply→`BufferChanged`-Sequenz wie `PseudoConsoleSession.ReadLoopAsync` (Z. 256–270)
- [x] `TerminalReplaySession`-Abspiel-Member — `WiedergabeStarten` (idempotent via Interlocked, Z. 121–130), `Pausieren`/`Fortsetzen` mit `_pauseLock`-Invariante „pausiert ⇒ Gate geschlossen" (Z. 133–160, Iteration 3), `IstPausiert`, `ZeitrafferSchwelle`, `AktuellerChunkIndex`; In-Flight-Chunk bei `Pausieren` unter `_renderLock` zurückgehalten (Re-Check Z. 252–266)
- [x] `TerminalReplaySession`-Stub-Member — `Process` = nicht-gestartetes `new Process()` (dokumentiert, Z. 63–67), `InputStream`/`OutputStream` = `Stream.Null`, `IsPseudoTerminal` = `false`, `Resize` = `true`, `WriteInputAsync`/`WritePromptAsync`/`MarkInputActivity`/`MarkOutputActivity` = No-Op, `Failure` = `null`, `ExitCode` = `null`, `DrainOutputAsync` = `true`, `RuntimeStatus` `Laeuft`/`Inaktiv` via `RuntimeStatusChanged`, `RebuildBufferFromReplay` unter `_renderLock` (Z. 187–197), `Exited` am Ende mit `ExitCode = null`, `Dispose` bricht Wiedergabe ab
- [x] `ICliReplayExportService` / `CliReplayExportService` — `src/Softwareschmiede.App/Services/CliReplayExportService.cs`: `ExportCliReplayAsync` (`GetCliAufzeichnung` → `InvalidOperationException` bei `null`, sonst `SpeichernAsync`); `HatAufzeichnung` (Iteration 2)
- [x] `CliChunkQuelltextFormatter` (statisch) — `src/Softwareschmiede.App/Services/CliChunkQuelltextFormatter.cs`: `ESC` → `␛`, `CR`/`LF`/`TAB` markiert, übrige Steuerbytes → `\xNN`, UTF-8-Dekodierung
- [x] `CliChunkAnzeigeEintrag` — `src/Softwareschmiede.App/ViewModels/CliChunkAnzeigeEintrag.cs`: `Index`, `Offset`, `Laenge`, `Quelltext`
- [x] `KonsolenTestViewModel` — `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs`: Commands `AufzeichnungOeffnen`, `WiedergabeStarten`, `WiedergabeNeustarten` (Iteration 3, Z. 53/67–69/294–315), `WiedergabePausieren`-Toggle, `Schliessen`; Properties `Session`, `QuellEintraege`, `AktuellerQuellEintrag`, `DateiPfad`, `StatusText`, `PositionsText`, `IstWiedergabeAktiv` (mit `RelayCommand.Refresh`, Z. 122), `IstPausiert`, `ZeitrafferSchwelleText` (Validierung inkl. `TimeSpan.MaxValue`-Obergrenze Z. 388–394), `FehlerMeldung`, `UnvollstaendigHinweis` (Z. 225–227), `CloseRequested`; `EntsorgeReplaySession` mit Event-Abmeldung + `_replaySession`/`Session`-Nullung (Z. 260–275), Neustart-/`_wiedergabeBeendet`-Pfad über `ErsetzeReplaySessionDurchFrische` (Z. 307–315), Quell-Formatierung via `Task.Run` (Z. 192)
- [x] `KonsolenTestDialog` (Window) — `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml(.cs)`: `Title="Konsolentest"`, Werkzeugleiste inkl. „Neu starten" (`WiedergabeNeustarten`, Z. 39–44), `TerminalControl` (`ReplayTerminal`), `ListView QuellChunkListe` mit `SelectedItem`-Sync + `ScrollIntoView`, Fehlerbanner + `UnvollstaendigHinweis` mit `NullOrEmptyToVisibilityConverter` und `HelpText`-Bindings, Ctor `KonsolenTestDialog(KonsolenTestViewModel)` + `CloseRequested`-/`Closed`-Verdrahtung
- [x] `KonsolenTestDialogView` (E2E-Wrapper) — `src/Softwareschmiede.Tests/E2E/Views/Dialogs/KonsolenTestDialogView.cs`: `DialogTitle = "Konsolentest"`, eigene `OpenDialogCondition` (Z. 323–328), `OeffneAufzeichnung` (Autocomplete-tolerant), `OeffneAufzeichnungAbbrechen`, `IstFehlerSichtbar`/`GetFehlerMeldung`/`WarteAufFehlerSichtbar` auf `GetDialogWindow()`, `SetZeitrafferSchwelle`, `StartWiedergabe`/`NeustartWiedergabe`/`PausierenToggle` über `WaitForEnabledElement`, `GetStatusText`/`GetPositionsText` via HelpText, `WarteAufStatus`/`WarteAufPosition`/`WarteAufQuellEintraege`, `GetQuellEintraegeCount`/`GetQuellEintragText`, `Schliessen`

### Änderungen an bestehenden Klassen

- [x] `TerminalSessionOptions.AufzeichnungByteBudget` (`int`, Default 8 MB) — `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionOptions.cs:20`
- [x] `KiAusfuehrungsService` — Ctor +`IOptions<TerminalSessionOptions>`/`TimeProvider`; `_aufzeichnungen` (`ConcurrentDictionary<Guid, CliOutputRecorder>`) mit `MaxAufzeichnungenAnzahl`-Retention (8, `LinkedList`-Reihenfolge); `GetCliAufzeichnung(Guid)`; `StartTerminalSessionAsync` erzeugt Recorder nur bei Budget > 0 (Composite) sonst `outputWriter` direkt, `CliProcessHandle.OutputSink` = dieselbe Senke, `catch` drainet die `outputSink`-Variable
- [x] `CliOutputProtokollWriter` implementiert `ITerminalDiagnoseSink` — `OnDiagnoseChunk` → Accumulator-Pfad
- [x] `TerminalSessionService.WriteDiagnosis` — `ITerminalDiagnoseSink`-Routing mit `OnOutputChunk`-Fallback
- [x] `IDialogService.ShowOpenFileDialogAsync` + `ShowKonsolenTestDialogAsync` — deklariert und in `WpfDialogService` implementiert: `OpenFileDialog` auf UI-Dispatcher (Z. 202); `ShowKonsolenTestDialogAsync` **nicht-modal** via `dialog.Show()` mit `Owner = MainWindow` (Z. 208–225); `AktivesDialogOwnerFenster()` (Z. 231–235)
- [x] `TaskDetailViewModel` — optionaler Ctor-Parameter `ICliReplayExportService?` mit Fallback, `KannCliReplayExportieren`, `ExportCliReplayCommand`, `ExportCliReplayAsync` (`HatAufzeichnung`-Vorab-Prüfung, Standardname `cli-replay-{aufgabeId:N}.clireplay`, Filter „CLI-Replay-Dateien", `.clireplay`-Endungsprüfung, Fehler → `FehlerMeldung`)
- [x] `TaskDetailView.xaml` — `RibbonLargeButton` „Aufzeichnung exportieren" (`CliReplayExport`) neben `CliRawExport`, Tooltips auf beiden
- [x] `SettingsViewModel` — `IDialogService` + `IServiceProvider` erforderliche Ctor-Parameter, `KonsolenTestOeffnenCommand`; XML-Doc auf „nicht-modale Konsolentestfenster" korrigiert (Z. 247, Iteration 3)
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
- [x] `MainWindowViewModelUpdateTestBase` — `services.AddSingleton(_dialogServiceMock.Object)` (Z. 79)
- [x] `SettingsView`-E2E-Wrapper — `OpenKonsolenTestDialog()`
- [x] `TaskDetailView`-E2E-Wrapper — `ExportCliReplay(zielPfad)` inkl. `null`-Abbruchpfad via `HandleSaveFileDialog` (ESC, Z. 345–349/359–362)

### Neue Tests (Code-seitig verifiziert)

- [x] `CliOutputRecorderTests` — 6 Tests
- [x] `CompositeTerminalOutputSinkTests` — 3 Tests
- [x] `CliReplayAufzeichnungStoreTests` — 11 Tests
- [x] `TerminalReplaySessionTests` — 9 Tests inkl. `Wiedergabe_ErzeugtGleichenBufferWieLiveSession`
- [x] `CliChunkQuelltextFormatterTests` — 4 Tests
- [x] `KonsolenTestViewModelTests` — 12 Testmethoden/13 Fälle (Laden, Formatfehler, Dialog-Abbruch, `UnvollstaendigHinweis`, Wiedergabe-Sync, Pause/Fortsetzen, Neustart nach Ende, Neustart aus Pausiert `Wiedergabe_NeustartAusPausiertemLauf_SpieltErneutAbPosition0`, Zeitraffer gültig + Theory `-2`/`1e13`, `CloseRequested`, Commands inert); racy Asserts via `BeOneOf` + `WarteBisAsync` (Z. 166–172)
- [x] `TaskDetailViewModelTests_CliReplayExport` — 6 Tests
- [x] `CliReplayExportServiceTests` — 3 Tests
- [x] `KiAusfuehrungsServiceCliAufzeichnungTests` — 7 Tests
- [x] `TerminalSessionServiceTests.StartAsync_DiagnoseFaehigeSenke_ErhaeltMarkerUeberDiagnoseKanal`
- [x] `CliOutputProtokollWriterTests.OnDiagnoseChunk_SchreibtMarkerAlsProtokollzeilen`
- [x] Regressionstests — `PseudoConsoleSessionTests.ReadLoopAsync_RebuildAusOutputSink_WendetChunkNichtDoppeltAn`, `AnsiSequenceParserTests` (`Parse_EscInCsi_*`, `Parse_EscInCsiQuestion_*`, `Parse_StringSequenzen_WerdenUebersprungen`, `Parse_OscStAnChunkGrenze_*`, `Parse_OscMitStImSelbenChunk_*`, `Parse_EscInOsc_*`)

### E2E (Pflicht + Empfohlen — Runde-2-Restabweichungen geschlossen)

- [x] Task 44 / `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` in `RunGeneralTests` (`MainTest.cs:36`) — **beide Runde-2-Restabweichungen geschlossen:** (a) Chunk 1 der Diagnose-Aufzeichnung enthält SGR-Farbsequenzen `\x1b[31m…\x1b[0m` (`E2E_KonsolenTestfenster.cs:56`), die Quell-Listen-Assertion prüft die sichtbar gemachten Steuersequenzen (`␛[31m`, `E2E-Replay-Chunk-1`, `␛[0m` — Z. 148–151); (b) Dialog-/Settings-Cleanup steht im `finally` (`TryCloseKonsolenTestfenster`, Z. 163–177 + Helper Z. 182–200, TryClose-Muster analog `TryCloseTaskDetail`). Im selben konsolidierten Szenario: OpenFileDialog-Abbruch, Formatfehler-Phase (`WarteAufFehlerSichtbar` + „geladen"), Pause-/Fortsetzen-Phase (3-s-Pause, `WarteAufStatus("Pausiert.")`, `"Chunk 1/2"` während Pause), Neustart-Phasen (nach Ende + aus Pausiert via `NeustartWiedergabe`), Zeitraffer-Phase (`"0"`, Quell-Liste 2 Einträge), Endstatus „Wiedergabe beendet."
- [x] Task 45 / `ConPtyCliReplayExport_ExportOeffnetKonsolenTestUndSpieltAb_E2E` als Phase in `ConPtyLifecycle_*` (`E2E_ConPtyLifecycle.cs:53/70`) — **Abbruch-Subphase ergänzt:** `ExportCliReplay(null)` → Save-Dialog per ESC (Z. 76–77), `Assert.False(File.Exists(pfad))` + `Assert.False(new ErrorView(mainWindow).IsVisible)` (Z. 78–79), danach Bestätigungs-Pfad (Export → Magic-Check `SWCLRPLY` → Laden + Abspielen im Konsolentestfenster). Lane in Sandbox via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` übersprungen

## Offene Aufgaben

- [ ] Task 38 / Plan-Schritt 16 „Devin-CLI-Ursachenanalyse" — **begründet offen:** Die Live-Aufzeichnung einer echten Devin-CLI-Session und die Reproduktion im Konsolentestfenster ist in dieser Sandbox nicht möglich (kein konfigurierbares Devin-Plugin/keine CLI). Stattdessen wurden per statischer Analyse drei reale Defekte gefunden und behoben (Rebuild-Race in `PseudoConsoleSession`, ESC-State-/String-Sequenz-Defekte in `AnsiSequenceParser`, Pause-Deadlock in `TerminalReplaySession`), jeweils mit Regressionstests. Verbleibende Lücke: der geforderte **End-to-End-Nachweis an einer echten Devin-CLI-Aufzeichnung** außerhalb der Sandbox.

## Hinweise

- **Eigenständige Verifikation in Review-Runde 3:** `dotnet build Softwareschmiede.slnx` — 0 Fehler, 0 Warnungen. `dotnet test ... --filter "FullyQualifiedName~KonsolenTestViewModelTests|FullyQualifiedName~TerminalReplaySessionTests"` (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`) — **22/22 grün** (13 ViewModel-Fälle + 9 ReplaySession-Tests). Die E2E-Lanes wurden in dieser Runde nicht erneut ausgeführt; der dokumentierte Iteration-3-Lauf (`RunGeneralTests` grün, OsInterface 53/0 Fehler) bleibt die Referenz.
- **Dokumentierte Planabweichung (Iteration 3, vertretbar):** `IOptions<TerminalSessionOptions>` ist kein Ctor-Parameter von `KonsolenTestViewModel` mehr — plan.md Z. 85 listet die Dep noch für den Cols/Rows-Fallback. Der Fallback wurde als toter Code entfernt, weil `CliReplayAufzeichnungStore.LadeAsync` `Cols`/`Rows > 0` bereits validiert (Kommentar `KonsolenTestViewModel.cs:210–211`); die Abweichung ist in der Tasks-Datei dokumentiert (Task 64). Gleiches Muster wie die bereits bewertete Iteration-2-Abweichung „nicht-modal" — plan.md wurde auch hier nicht nachgezogen; die Ctor-Spezifikation in plan.md Z. 85 ist damit überholt.
- **Dokumentierte Planabweichung (Iteration 2, unverändert):** Konsolentestfenster nicht-modal (`WpfDialogService.cs:223`, `Show()` statt `ShowDialog()`) — per Usability-Review begründet; plan.md Z. 18/113 nicht nachgezogen.
- Testzahlen in der Tasks-Datei an den tatsächlichen Stand angeglichen (`KonsolenTestViewModelTests`: 11 → 12 Testmethoden/13 Fälle).
- Kleinigkeit: `KonsolenTestViewModel` erzeugt die `TerminalReplaySession` ohne `ILogger<TerminalReplaySession>` (`KonsolenTestViewModel.cs:252` — optionaler Parameter, `NullLogger`-Default); plankonform.
