# Umsetzungsplan: Konsolentestfenster für CLI-Ausgabe-Replay

## Übersicht

Die Terminal-Integration wird um ein Diagnose-Werkzeug erweitert: Ein neuer `CliOutputRecorder` (als `ITerminalOutputSink` neben `CliOutputProtokollWriter` über eine Composite-Senke) zeichnet die Rohbytes jeder Terminal-Session mit Zeitstempel pro Chunk mit; exportierbar wird die Aufzeichnung als `.clireplay`-Binärdatei aus der `TaskDetailView`. Ein neues modales Konsolentestfenster (Einstieg über `SettingsView`, Abschnitt „Diagnose") lädt eine solche Datei und spielt sie über eine neue `TerminalReplaySession` (Implementierung von `ITerminalSession`) zeitreal durch den echten Renderpfad `AnsiSequenceParser` → `TerminalBuffer` → `TerminalControl` ab — mit Pausieren/Fortsetzen, einstellbarer Zeitraffer-Pausenverkürzung und einer synchronen Quell-Ansicht der Chunks. Mit dem Werkzeug wird die Ursache der bei der Devin-CLI beobachteten Auslassungen/Dopplungen untersucht und gefundene Defekte mit Regressionstests behoben.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Mitschnitt-Hook | `CompositeTerminalOutputSink` (`ITerminalOutputSink`-Fanout), verdrahtet in `KiAusfuehrungsService.StartTerminalSessionAsync` beim Session-Start | Die Factory-/Launcher-Signaturen (`ITerminalSessionFactory.StartAsync`, `IPseudoConsoleProcessLauncher.Start`, `PseudoConsoleSessionContext`) bleiben unverändert; die Senke existiert ab Session-Erzeugung und erfasst damit auch frühe Chunks ohne Race-Bedingung (im Gegensatz zur nachträglichen `OutputChunk`-Event-Registrierung). |
| `[Terminal-Diagnose]`-Marker | Neues Interface `ITerminalDiagnoseSink` (`OnDiagnoseChunk`); `WriteDiagnosis` in `TerminalSessionService` routet Markerzeilen nur an Senken, die das Interface implementieren (Fallback: `OnOutputChunk`) | `TerminalSessionService.WriteDiagnosis` (`TerminalSessionService.cs` Z. 131–149) schreibt Markerzeilen über `outputSink.OnOutputChunk` — mit einer Composite-Senke würden diese Artefakt-Zeilen in den Rohbyte-Mitschnitt gelangen und im Replay als gefälschte Ausgabe erscheinen. Das Interface trennt Diagnose-Routing von Ausgabe-Routing ohne Signaturbruch; nicht-aware Senken erhalten Marker weiter über `OnOutputChunk` (Verhalten für vorhandene Mocks/Tests unverändert). `CliOutputProtokollWriter` implementiert es (leitet auf denselben Accumulator-Pfad), `CompositeTerminalOutputSink` reicht nur an innere Diagnose-Senken weiter, `CliOutputRecorder` implementiert es nicht → keine Marker im Mitschnitt. |
| Mitschnitt-Umfang | Immer-an pro Terminal-Session, im Speicher, budgetbegrenzt über `TerminalSessionOptions.AufzeichnungByteBudget` (Default 8 MB); bei Überschreitung stoppt die Aufnahme und `IstVollstaendig=false` wird gesetzt; `<= 0` deaktiviert den Recorder | Retrospektive Diagnose erfordert, dass der Mitschnitt bereits lief, bevor ein Fehler auftrat (opt-in käme zu spät). Bei Budget-Überschreitung wird das intakte Präfix behalten statt älteste Chunks zu verwerfen — ein Replay ab Position 0 braucht den Anfang (Parser-Zustand), das `TerminalReplayBuffer`-Verwerfungsmodell würde den Neuaufbau korrumpieren. Der Recorder ist getrennt vom 512-KB-`TerminalReplayBuffer`, dessen Zweck (Control-Neuaufbau) unverändert bleibt. |
| Dateiformat `.clireplay` | Binärformat über `CliReplayAufzeichnungStore`: Header (Magic `SWCLRPLY`, Version `Int32`, `AufgabeId`, `StartUtc` als Ticks, `EndeUtc` als Ticks mit `0` = nicht gesetzt, `Cols`, `Rows`, `PluginName` längenpräfixiert UTF-8, `IstVollstaendig`), danach Records `[Int64 OffsetTicks][Int32 Length][Bytes]` bis EOF | Kompakt, verlustfrei, streambar und deterministisch roundtrip-testbar; die menschenlesbare Inspektion übernimmt die Quell-Ansicht im Werkzeug — ein Textformat ist dafür nicht erforderlich. Die Version ermöglicht spätere Formaterweiterungen. `EndeUtc` gehört als Header-Feld dazu, da `CliOutputAufzeichnung.EndeUtc` im Modell enthalten ist. |
| Zeitstempel-Basis | Relative Offsets (`TimeSpan` seit Aufzeichnungsbeginn) pro Chunk + absoluter Anker `StartUtc` im Header | Wiedergabe- und Zeitraffer-Logik rechnet ausschließlich mit Deltas; absolute Rekonstruktion bleibt über den Anker möglich. Der Recorder nutzt den per DI bereits registrierten `TimeProvider` (App.xaml.cs Z. 311) — testbar via `FakeTimeProvider`. |
| `ITerminalSession.Process` im Replay | `TerminalReplaySession` liefert ein nicht-gestartetes `new Process()` als Stub | Die Schnittstelle bleibt unverändert (kein nullbarer Member, kein neues Control-Interface). Zugriffe wie `Process.Id`/`HasExited` werfen `InvalidOperationException` — die vorhandenen Aufrufer fangen genau diesen Fehlermodus bereits ab (`TaskDetailView.TryGetProcessId`, `TaskDetailView.xaml.cs` Z. 161–174; `KiAusfuehrungsService`-Pfade greifen ohnehin nur bei echten Sessions zu). Ehrlicher Stub statt irreführendem `Process.GetCurrentProcess()`. |
| Wiedergabesteuerung | Auf der konkreten Klasse `TerminalReplaySession` (zusätzliche Member neben `ITerminalSession`: `WiedergabeStarten`, `Pausieren`, `Fortsetzen`, `ZeitrafferSchwelle`, `AktuellerChunkIndex`), nicht auf dem Interface | `PseudoConsoleSession` müsste sonst sinnlose Abspiel-Member implementieren. Das ViewModel kennt die konkrete Klasse und bindet nur `Session` (`ITerminalSession`) ans `TerminalControl`. |
| Einstiegspunkt & Fensterart | Neuer Abschnitt „Diagnose" im `SettingsView`-Tab „Allgemein" mit Button „Konsolentestfenster öffnen"; das Fenster ist ein **modaler** Dialog (`ShowDialog`, `Owner = MainWindow`) über `IDialogService` | Unaufdringlich (kein Navigations-Eintrag), folgt der einheitlichen Dialog-Konvention aller bestehenden Dialoge und der FlaUI-`DialogView`-Erkennung über den Fenstertitel. Der Export-Einstiegspunkt bleibt dagegen aufgabenbezogen im `TaskDetailView`-Ribbon (Gruppe „CLI"). |
| Zeitraffer-Semantik | `ZeitrafferSchwelle` (`TimeSpan`, jederzeit änderbar): die Wartezeit vor jedem Chunk ist `min(realePause, ZeitrafferSchwelle)`; `0` = maximale Geschwindigkeit | Entspricht der freigegebenen Entscheidung „Pausenverkürzung ab einstellbarer Mindestlänge" — kürzere Pausen bleiben zeitreal, längere werden gedeckelt. |
| Resize-Ereignisse | Werden nicht aufgezeichnet; der Header speichert die initialen `Cols`/`Rows` der Session | Replay nutzt die aktuelle Fenstergröße (`TerminalControl` resized den Buffer ohnehin bei Bindung und Größenänderung); die Header-Werte dienen nur der initialen Buffer-Geometrie. |
| Verhältnis zu `.raw`-Export | Beide Exporte bestehen parallel („Rohausgabe exportieren" + „Aufzeichnung exportieren") | Unterschiedliche Zwecke: zeilennormalisierte Sicht vs. byte-exaktes, zeitreales Replay. |
| `SettingsViewModel`-Konstruktor | `IDialogService` + `IServiceProvider` als erforderliche Parameter | Folgt der dominanten Konvention (`TaskDetailViewModel` Z. 695/697, `ProjectDetailViewModel` Z. 331–332); erforderliche Parameter lassen DI-Fehlkonfigurationen sofort scheitern statt einen still defekten `KonsolenTestOeffnenCommand` zu erzeugen. Konsequenz: die direkten Test-Instanziierungen (`SettingsViewModelTests`, `SettingsViewModelTests_IdePlugin`) und der Test-DI-Container in `MainWindowViewModelUpdateTestBase` werden angepasst — siehe „Betroffene bestehende Tests". |
| Opt-out `AufzeichnungByteBudget <= 0` | `CliOutputProtokollWriter` wird **direkt** als `outputSink` übergeben — keine Composite-Senke | Einfachste Variante: der `catch`-Pfad (Z. 221–224) drainet die übergebene Senke unverändert; eine einelementige Composite hätte keinen Mehrwert. |
| Aufzeichnungs-Ablage | `ConcurrentDictionary<Guid, CliOutputRecorder>` in `KiAusfuehrungsService`; Eintrag überlebt das Session-Ende, wird beim nächsten Start derselben Aufgabe ersetzt | Export muss auch nach Prozessende funktionieren — das `CliProcessHandle` wird bei `Exited` entfernt und eignet sich daher nicht als alleinige Ablage. Speicher bleibt durch das Budget je Aufgabe begrenzt; Bereinigung erfolgt bei Neustart der Aufgabe bzw. App-Ende. |

## Programmabläufe

### Mitschnitt der CLI-Ausgabe beim Session-Start

1. `KiAusfuehrungsService.StartTerminalSessionAsync` erzeugt nach `GetTerminalStartSpecAsync` den bestehenden `CliOutputProtokollWriter` sowie — nur wenn `AufzeichnungByteBudget > 0` — einen neuen `CliOutputRecorder` (Parameter: `aufgabeId`, `spec.PluginName`, `options.DefaultCols`/`DefaultRows`, `AufzeichnungByteBudget`, `TimeProvider`, Logger) und verpackt beide in `CompositeTerminalOutputSink`. Bei `AufzeichnungByteBudget <= 0` wird **kein** Recorder und **keine** Composite erzeugt; `outputWriter` bleibt die Senke.
2. Die ermittelte Senke (Composite bei aktivem Recorder, sonst `outputWriter` direkt) wird als `outputSink` an `_sessionFactory.StartAsync` übergeben (durchgereicht über `TerminalSessionService` → `IPseudoConsoleProcessLauncher.Start` → `PseudoConsoleSessionContext.OutputSink`).
3. `PseudoConsoleSession.ReadLoopAsync` ruft pro gelesenem Chunk `_outputSink.OnOutputChunk` (`PseudoConsoleSession.cs` Z. 309) → die Composite ruft nacheinander `CliOutputProtokollWriter.OnOutputChunk` (Zeilenprotokoll, unverändert) und `CliOutputRecorder.OnOutputChunk` (kopiert Bytes, Zeitstempel `TimeProvider.GetUtcNow() - StartUtc`) auf.
4. `[Terminal-Diagnose]`-Markerzeilen aus `TerminalSessionService.WriteDiagnosis` werden über `ITerminalDiagnoseSink.OnDiagnoseChunk` geroutet und erreichen nur den Protokoll-Writer, nicht den Recorder.
5. Am Ende der Leseschleife ruft `ReadLoopAsync` `Complete()` (`PseudoConsoleSession.cs` Z. 334) → die übergebene Senke wird beendet (bei aktivem Recorder: die Composite beendet beide innere Senken); `CliProcessHandle.OutputSink` (= dieselbe Senke) wird in `DisposeSessionResourcesAsync` via `CompleteAsync` gedraint.
6. Schlägt `_sessionFactory.StartAsync` fehl, drainet der bestehende `catch`-Block (`KiAusfuehrungsService.cs` Z. 221–224) künftig die tatsächlich übergebene Senke (`CompleteAsync` auf der `outputSink`-Variable: Composite bei aktivem Recorder, `outputWriter` direkt bei deaktiviertem) statt fix `outputWriter`; der Recorder wird verworfen (kein `_aufzeichnungen`-Eintrag).
7. `KiAusfuehrungsService` legt den Recorder in `_aufzeichnungen[aufgabeId]` ab; `GetCliAufzeichnung(aufgabeId)` liefert jederzeit (auch nach Session-Ende) ein `CliOutputAufzeichnung`-Snapshot.

Beteiligte Klassen/Komponenten: `KiAusfuehrungsService`, `CliOutputProtokollWriter`, `CliOutputRecorder`, `CompositeTerminalOutputSink`, `ITerminalDiagnoseSink`, `TerminalSessionService`, `PseudoConsoleSession`, `TerminalSessionOptions`.

### Export der Aufzeichnung als `.clireplay`

1. Anwender klickt in `TaskDetailView` (Ribbon-Gruppe „CLI", nur sichtbar wenn `IsCliViewSelected`) die neue Schaltfläche „Aufzeichnung exportieren" (`AutomationName="CliReplayExport"`, `ExportCliReplayCommand`).
2. `TaskDetailViewModel.ExportCliReplayAsync` öffnet `IDialogService.ShowSaveFileDialogAsync` mit Standardname `cli-replay-{aufgabeId:N}.clireplay`, erzwingt die `.clireplay`-Endung, ruft `ICliReplayExportService.ExportCliReplayAsync(aufgabeId, zielPfad, ct)`, Fehler → `FehlerMeldung` (identischer Ablauf wie `ExportCliRawAsync`, Z. 2309–2346).
3. `CliReplayExportService` holt die Aufzeichnung über `KiAusfuehrungsService.GetCliAufzeichnung(aufgabeId)`; bei `null` wirft eine verständliche `InvalidOperationException` („Keine Aufzeichnung vorhanden"), sonst serialisiert `CliReplayAufzeichnungStore.SpeichernAsync` Header + Chunk-Records in die Datei.

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `TaskDetailView.xaml`, `IDialogService`, `ICliReplayExportService`/`CliReplayExportService`, `KiAusfuehrungsService`, `CliReplayAufzeichnungStore`.

### Konsolentestfenster: Öffnen, Laden, Abspielen, Vergleichen

1. Anwender öffnet die Einstellungen; im Tab „Allgemein" klickt er im neuen Abschnitt „Diagnose" auf „Konsolentestfenster öffnen" (`AutomationName="KonsolenTestOeffnen"`).
2. `SettingsViewModel.KonsolenTestOeffnenCommand` löst `KonsolenTestViewModel` per `_serviceProvider.GetRequiredService<...>()` auf (Konvention wie `AufgabePausierenDialogViewModel`) und ruft `IDialogService.ShowKonsolenTestDialogAsync(viewModel, ct)`; `WpfDialogService` zeigt `KonsolenTestDialog` modal (`Owner = MainWindow`, UI-Dispatcher).
3. Im Dialog: „Aufzeichnung öffnen…" (`AutomationName="AufzeichnungOeffnen"`) → `IDialogService.ShowOpenFileDialogAsync` (Filter `*.clireplay`) → `CliReplayAufzeichnungStore.LadeAsync` → bei Formatfehler `FehlerMeldung`.
4. Das ViewModel erzeugt `TerminalReplaySession` aus der geladenen `CliOutputAufzeichnung` (Buffer-Initialgröße aus Header-`Cols`/`Rows`, Fallback `TerminalSessionOptions`-Defaults), setzt die gebundene `Session`-Property → `TerminalControl.OnSessionChanged` subscribiert `BufferChanged`, ruft `RebuildBufferFromReplay()` (noch keine Chunks abgespielt → leerer Buffer) und resized den Buffer auf die Fenstergröße. Ist die geladene Aufzeichnung unvollständig (`IstVollstaendig = false`), zeigt das ViewModel einen sichtbaren Hinweis („Aufzeichnung unvollständig — Budget überschritten") in einem eigenen Hinweis-Element (`UnvollstaendigHinweis`), damit ein abgeschnittenes Replay im Diagnosewerkzeug nicht als vollständig fehlinterpretiert wird.
5. Die Quell-Ansicht (`ListView`, `AutomationName="QuellChunkListe"`) wird mit `CliChunkAnzeigeEintrag`-Zeilen befüllt (Index, Offset, Länge, Quelltext via `CliChunkQuelltextFormatter` — Steuerzeichen sichtbar: `ESC` → `␛`, `CR` → `\r`, `LF` → `\n`, übrige Steuerbytes → `\xNN`).
6. „Abspielen" (`WiedergabeStarten`) → `session.WiedergabeStarten()` startet den Wiedergabe-Task: pro Chunk wartet die Schleife `min(realePause, ZeitrafferSchwelle)` (`Task.Delay(delay, _timeProvider, ct)`), respektiert das Pause-Gate (`Pausieren`/`Fortsetzen`), löst `OutputChunk` aus, wendet unter `_renderLock` `_parser.Parse(chunk)` + `Buffer.Apply` an (identisch zum Referenzpfad `PseudoConsoleSession.ReadLoopAsync` Z. 315–319) und feuert `BufferChanged`.
7. Das ViewModel subscribed `BufferChanged`, aktualisiert über `DispatcherInvokeFactory` `PositionsText` („Chunk x/y"), `StatusText` und `AktuellerQuellEintrag`; die Quell-Liste selektiert den aktuellen Chunk und scrollt ihn ins Sichtfeld (`ScrollIntoView` im Code-behind auf `SelectionChanged`).
8. `ZeitrafferSchwelle`-TextBox (`AutomationName="ZeitrafferSchwelle"`) bindet validierend auf die Session-Property und wirkt live auf alle folgenden Pausen; „Pausieren/Fortsetzen" (`WiedergabePausieren`) toggelt das Gate.
9. Nach dem letzten Chunk setzt die Session `RuntimeStatus = Inaktiv` (`RuntimeStatusChanged`) und feuert `Exited` (`ExitCode = null`); Statusanzeige „Wiedergabe beendet". Fenster schließen → `Dispose` der Session (Cancellation des Wiedergabe-Tasks).

Beteiligte Klassen/Komponenten: `SettingsViewModel`, `SettingsView.xaml`, `IDialogService`/`WpfDialogService`, `KonsolenTestDialog`, `KonsolenTestViewModel`, `TerminalReplaySession`, `CliReplayAufzeichnungStore`, `CliChunkQuelltextFormatter`, `CliChunkAnzeigeEintrag`, `TerminalControl`.

### Ursachenanalyse und Behebung der Devin-CLI-Defekte

1. Mit dem fertigen Werkzeug wird eine echte Devin-CLI-Session aufgezeichnet und als `.clireplay` exportiert.
2. Die Wiedergabe im Konsolentestfenster wird chunkweise mit der Quell-Ansicht verglichen, bis der Punkt isoliert ist, an dem Auslassungen/Dopplungen entstehen (Verdachtsbereiche laut Anforderung: chunk-übergreifende Sequenzen, `\r`-Semantik, schnelle Output-Bursts).
3. Der identifizierte Defekt wird in `AnsiSequenceParser`, `TerminalBuffer` oder `TerminalControl` behoben.
4. Der minimale problematische Chunk-Ausschnitt wird als Regressionstest (Unit-Test auf Parser-/Buffer-Ebene bzw. `TerminalReplaySession`-Vergleichstest gegen `PseudoConsoleSession`-Verhalten) abgesichert.

Beteiligte Klassen/Komponenten: `CliReplayAufzeichnungStore`, `TerminalReplaySession`, `AnsiSequenceParser`, `TerminalBuffer`, `TerminalControl`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `CliOutputChunkRecord` | Record (`src/Softwareschmiede/Infrastructure/Terminal/`) | Ein Mitschnitt-Eintrag: `Offset` (`TimeSpan` relativ zur Aufzeichnung), `Data` (`byte[]`, unveränderte Rohbytes). |
| `CliOutputAufzeichnung` | Datenmodellklasse (`src/Softwareschmiede/Infrastructure/Terminal/`) | Vollständige Aufzeichnung: `AufgabeId`, `PluginName`, `StartUtc`, `Cols`, `Rows`, `IstVollstaendig`, `EndeUtc?`, `Chunks` (`IReadOnlyList<CliOutputChunkRecord>`). |
| `CliOutputRecorder` | Klasse, `ITerminalOutputSink` (`src/Softwareschmiede/Infrastructure/Terminal/`) | Zeichnet `OnOutputChunk`-Bytes mit `TimeProvider`-Offset in eine Liste auf; budgetbegrenzt (`AufzeichnungByteBudget`), bei Überschreitung `IstVollstaendig=false` und Aufnahmestopp; `Complete`/`CompleteAsync` setzen `EndeUtc` (idempotent); `GetAufzeichnung()` liefert Snapshot. |
| `ITerminalDiagnoseSink` | Interface (`src/Softwareschmiede/Infrastructure/Terminal/`) | `OnDiagnoseChunk(ReadOnlySpan<byte>)` — Routing-Kanal für `[Terminal-Diagnose]`-Markerzeilen, damit sie nicht in den Rohbyte-Mitschnitt gelangen. |
| `CompositeTerminalOutputSink` | Klasse, `ITerminalOutputSink` + `ITerminalDiagnoseSink` (`src/Softwareschmiede/Infrastructure/Terminal/`) | Fächert `OnOutputChunk` an alle inneren Senken; `OnDiagnoseChunk` nur an innere `ITerminalDiagnoseSink`-Implementierungen; `Complete`/`CompleteAsync` schließen alle inneren Senken ab. |
| `CliReplayAufzeichnungStore` | Klasse (`src/Softwareschmiede/Infrastructure/Terminal/`) | Serialisiert/Deserialisiert `CliOutputAufzeichnung` im `.clireplay`-Binärformat; streambasierte Kernmethoden (`SchreibeAsync(Stream, …)`, `LadeAsync(Stream, …)`) plus Datei-Wrapper (`SpeichernAsync(pfad, …)`, `LadeAsync(pfad, …)`); Magic-/Versionsprüfung sowie Header-Validierung (`Cols`/`Rows` müssen > 0 sein — sonst wirft die Buffer-Anlage beim Replay) → `InvalidDataException`. |
| `TerminalReplaySession` | Klasse, `ITerminalSession` (`src/Softwareschmiede/Infrastructure/Terminal/`) | Spielt eine `CliOutputAufzeichnung` durch `AnsiSequenceParser` → `TerminalBuffer` ab; **Konstruktor-Parameter:** `CliOutputAufzeichnung`, `TimeProvider` (produktiv die per DI registrierte Instanz `TimeProvider.System`, `App.xaml.cs` Z. 311 — durchgereicht vom `KonsolenTestViewModel`; Tests instanziieren die Session direkt mit `FakeTimeProvider`), `ILogger<TerminalReplaySession>`; zusätzliche Abspiel-Member `WiedergabeStarten()`, `Pausieren()`, `Fortsetzen()`, `IstPausiert`, `ZeitrafferSchwelle`, `AktuellerChunkIndex`, `ChunkAnzahl`; vollständige Stub-Member-Liste: `Process` = nicht-gestartetes `new Process()`, `InputStream`/`OutputStream` = `Stream.Null`, `IsPseudoTerminal` = `false`, `Resize` = `true`, `WriteInputAsync`/`WritePromptAsync`/`MarkInputActivity`/`MarkOutputActivity` = No-Op, `Failure` = `null` (`Failed` wird nicht ausgelöst), `ExitCode` = `null`, `DrainOutputAsync` = `true` (keine Live-Ausgabe abzuwarten); `RuntimeStatus` = `CliRuntimeStatus.Laeuft` während laufender Wiedergabe (inkl. Pause), `Inaktiv` vor dem Start und nach dem Ende — Übergänge über `RuntimeStatusChanged` signalisiert; `RebuildBufferFromReplay` baut den Buffer aus den bis dahin abgespielten Chunks neu auf; `Exited` am Ende mit `ExitCode = null`. |
| `ICliReplayExportService` / `CliReplayExportService` | Interface + Klasse (`src/Softwareschmiede.App/Services/`) | `ExportCliReplayAsync(aufgabeId, zielPfad, ct)`: holt `CliOutputAufzeichnung` via `KiAusfuehrungsService.GetCliAufzeichnung`, wirft bei fehlender Aufzeichnung, schreibt via `CliReplayAufzeichnungStore`. |
| `CliChunkQuelltextFormatter` | Statische Klasse (`src/Softwareschmiede.App/Services/`) | Formatierer für die Quell-Ansicht: UTF-8-Dekodierung mit Escape-Darstellung (`ESC` → `␛`, `CR` → `\r`, `LF` → `\n`, `TAB` → `\t`, andere Steuerbytes → `\xNN`). |
| `CliChunkAnzeigeEintrag` | Datenmodellklasse (`src/Softwareschmiede.App/ViewModels/`) | Zeilenmodell der Quell-Ansicht: `Index`, `Offset`, `Laenge`, `Quelltext`. |
| `KonsolenTestViewModel` | ViewModel (`src/Softwareschmiede.App/ViewModels/`) | Dialog-Logik: `AufzeichnungOeffnenCommand`, `WiedergabeStartenCommand`, `WiedergabePausierenCommand` (Toggle), `SchliessenCommand`; Properties `Session` (`ITerminalSession?`), `QuellEintraege` (`ObservableCollection<CliChunkAnzeigeEintrag>`), `AktuellerQuellEintrag`, `DateiPfad`, `StatusText`, `PositionsText`, `IstWiedergabeAktiv`, `IstPausiert`, `ZeitrafferSchwelleText` (validiert), `FehlerMeldung`, `UnvollstaendigHinweis` (sichtbarer Text bei `IstVollstaendig = false`); `CloseRequested`-Event (Dialog-Konvention); Dispatcher-Marshal via `DispatcherInvokeFactory.Create`. **Konstruktor-Abhängigkeiten** (per DI-Registrierung `AddTransient` und `GetRequiredService`-Auflösung aus `SettingsViewModel` — alle Parameter müssen im Container vorhanden sein): `IDialogService`, `CliReplayAufzeichnungStore`, `IOptions<TerminalSessionOptions>` (Cols/Rows-Fallback bei der Buffer-Initialgröße), `TimeProvider` (wird an den `TerminalReplaySession`-Konstruktor weitergereicht; DI-registriert als `TimeProvider.System`, `App.xaml.cs` Z. 311), `ILogger<KonsolenTestViewModel>`; optionaler Test-Hook `Action<Action>? dispatcherInvoke = null` (Konvention aus `TaskDetailViewModel` Z. 731). |
| `KonsolenTestDialog` | Window (`src/Softwareschmiede.App/Views/`, XAML + Code-behind) | `Title="Konsolentest"`; Werkzeugleiste (Öffnen-Button, Pfad-Anzeige, Abspielen, Pausieren/Fortsetzen, Zeitraffer-TextBox, Status-/Positionsanzeige, Hinweis-Element `UnvollstaendigHinweis` für `IstVollstaendig = false`) + zweigeteilter Bereich: `TerminalControl` (`AutomationName="ReplayTerminal"`, `Session`-Bindung) neben `ListView` der Quell-Chunks (`AutomationName="QuellChunkListe"`, `SelectedItem` ↔ `AktuellerQuellEintrag`, `ScrollIntoView` im Code-behind); Fehlerbanner als `TextBlock` mit `AutomationProperties.Name="FehlerMeldung"` + `AutomationProperties.HelpText="{Binding FehlerMeldung}"` in einem `Border` mit `NullOrEmptyToVisibilityConverter` (Konvention `SettingsView.xaml` Z. 193–201); Ctor `KonsolenTestDialog(KonsolenTestViewModel)` + `CloseRequested`-Verdrahtung. |
| `KonsolenTestDialogView` | E2E-View-Wrapper (`src/Softwareschmiede.Tests/E2E/Views/Dialogs/`) | `DialogView`-Subklasse (`DialogTitle = "Konsolentest"`) mit `OeffneAufzeichnung(pfad)` (bedient den nativen OpenFileDialog analog `TaskDetailView.HandleSaveFileDialog`, Z. 345–362 — mit **eigener Titel-Bedingung** `OpenDialogCondition` auf „Öffnen"/„Open" bzw. den in `ShowOpenFileDialogAsync` gesetzten Dialog-Titel, da `SaveDialogCondition` Z. 488–493 nur „Speichern unter"/„Save As" abdeckt), `Starten`, `PausierenFortsetzen`, `SetzeZeitraffer`, `GetQuellEintraege`, `GetStatusText`, `GetPositionsText`, `Schliessen` sowie die Fehlerbanner-Accessoren `IstFehlerSichtbar` (`ElementExists` auf `ByName("FehlerMeldung")`) und `GetFehlerMeldung()` (Bannertext via `GetHelpTextOrName`, Konvention `ErrorView.GetErrorMessage`, `ErrorView.cs` Z. 23) — beide suchen auf dem **Dialogfenster** (`GetDialogWindow()`), nicht auf `Window`: `ErrorView` durchsucht nur das Hauptfenster (`ErrorView.cs` Z. 14/23) und kann das Banner im modalen Dialog nicht finden. Muss in `WindowExtensions.DialogFactories` (`src/Softwareschmiede.Tests/E2E/Views/WindowExtensions.cs` Z. 10–23) registriert werden, damit `CurrentView()` den modalen Dialog erkennt. |

## Änderungen an bestehenden Klassen

### `TerminalSessionOptions` (Konfigurationsklasse)

- **Neue Eigenschaften:** `AufzeichnungByteBudget` (`int`, Default `8 * 1024 * 1024`) — Byte-Budget des `CliOutputRecorder` pro Session; `<= 0` deaktiviert den Mitschnitt.

### `KiAusfuehrungsService` (Singleton-Service)

- **Geänderter Konstruktor:** zusätzlich `IOptions<TerminalSessionOptions>` und `TimeProvider` (beide bereits im DI-Container, `App.xaml.cs` Z. 233/311).
- **Neue Felder:** `_aufzeichnungen` (`ConcurrentDictionary<Guid, CliOutputRecorder>`).
- **Neue Methoden:** `GetCliAufzeichnung(Guid aufgabeId)` → `CliOutputAufzeichnung?` (Snapshot des Recorders, auch nach Session-Ende verfügbar).
- **Geänderte Methoden:** `StartTerminalSessionAsync` (Z. 211–233) — erzeugt zusätzlich `CliOutputRecorder` (bei Budget > 0), verpackt `CliOutputProtokollWriter` + Recorder in `CompositeTerminalOutputSink`, übergibt die ermittelte Senke (Composite bzw. bei deaktiviertem Recorder `outputWriter` direkt — keine einelementige Composite) an `_sessionFactory.StartAsync` und setzt `CliProcessHandle.OutputSink` auf dieselbe Senke; trägt den Recorder in `_aufzeichnungen` ein (nach erfolgreichem Start; bei Startfehler wird er verworfen). Der `catch`-Block (Z. 221–224) ruft künftig `CompleteAsync` auf der übergebenen `outputSink`-Variable statt fix auf `outputWriter` auf, damit im Startfehler-Pfad bei aktivem Recorder beide innere Senken gedraint werden (bei deaktiviertem Recorder bleibt es unverändert der Protokoll-Writer allein).

### `CliOutputProtokollWriter` (ITerminalOutputSink)

- **Neue Schnittstelle:** implementiert zusätzlich `ITerminalDiagnoseSink` — `OnDiagnoseChunk` leitet auf denselben Accumulator-Pfad wie `OnOutputChunk` weiter.

### `TerminalSessionService` (ITerminalSessionFactory)

- **Geänderte Methoden:** `WriteDiagnosis` (Z. 131–149) — sendet die Markerzeile an `outputSink.OnDiagnoseChunk`, wenn die Senke `ITerminalDiagnoseSink` implementiert, sonst weiterhin `OnOutputChunk` (Rückwärtskompatibilität für einfache Senken/Mocks).

### `IDialogService` / `WpfDialogService`

- **Neue Methoden:** `ShowOpenFileDialogAsync(string title, string filter, string? initialDirectory = null, CancellationToken ct = default)` → `Task<string?>` (`Microsoft.Win32.OpenFileDialog` auf dem UI-Dispatcher, analog `ShowSaveFileDialogAsync` Z. 152–178).
- **Neue Methoden:** `ShowKonsolenTestDialogAsync(KonsolenTestViewModel viewModel, CancellationToken ct = default)` → `Task` (modales `KonsolenTestDialog`-Fenster, `Owner = MainWindow`, über das `ShowDialogAsync`-Hilfsmuster Z. 181–190 bzw. Dispatcher-Pendant ohne Ergebnisrückgabe).

### `TaskDetailViewModel`

- **Geänderter Konstruktor:** neuer optionaler Parameter `ICliReplayExportService? cliReplayExportService = null` mit Fallback-Instanziierung (Konvention von `cliRawExportService`, Z. 707/729).
- **Neue Eigenschaften:** `KannCliReplayExportieren` (`bool` — Aufgabe geladen, analog `KannCliRawExportieren` Z. 557).
- **Neue Commands:** `ExportCliReplayCommand` (`AsyncRelayCommand`, `KannCliReplayExportieren`).
- **Neue Methoden:** `ExportCliReplayAsync(CancellationToken)` — `ShowSaveFileDialogAsync` (`cli-replay-{aufgabeId:N}.clireplay`, Filter `*.clireplay`), Endungsprüfung, `_cliReplayExportService.ExportCliReplayAsync`, Fehler → `FehlerMeldung`.

### `TaskDetailView.xaml`

- **Neues Element:** `RibbonLargeButton` „Aufzeichnung exportieren" (`AutomationName="CliReplayExport"`, `ButtonCommand="{Binding ExportCliReplayCommand}"`) in der Ribbon-Gruppe „CLI" direkt neben `CliRawExport` (Z. 104–107).

### `SettingsViewModel`

- **Geänderter Konstruktor:** zusätzlich `IDialogService` und `IServiceProvider` als **erforderliche** Parameter (keine optionalen `= null`-Parameter — `KonsolenTestOeffnenCommand` ist eine Kernfunktion der Seite und muss nicht null-tolerant sein; beide Typen sind in `TaskDetailViewModel` Z. 695/697 und `ProjectDetailViewModel` Z. 331–332 ebenfalls erforderliche Parameter — die optionale-Parameter-Konvention ist dort für austauschbare Feature-Services wie `cliRawExportService` Z. 707/729 reserviert). `IServiceProvider` löst MS.DI automatisch aus dem Container auf (keine Registrierung nötig); `IDialogService` muss in Test-Containern explizit registriert werden (siehe „Betroffene bestehende Tests"). Der `IServiceProvider` dient der Auflösung von `KonsolenTestViewModel` per `GetRequiredService` (Konvention aus `TaskDetailViewModel` Z. 2357).
- **Neue Commands:** `KonsolenTestOeffnenCommand` (`AsyncRelayCommand` — erzeugt `KonsolenTestViewModel` per `_serviceProvider.GetRequiredService<KonsolenTestViewModel>()`, ruft `ShowKonsolenTestDialogAsync`, Fehler → `FehlerMeldung`).

### `SettingsView.xaml`

- **Neuer Abschnitt:** „Diagnose" am Ende des Tabs „Allgemein" (nach dem Abschnitt „Updates", Z. 290–319) mit Button „Konsolentestfenster öffnen" (`AutomationName="KonsolenTestOeffnen"`, `Command="{Binding KonsolenTestOeffnenCommand}"`) und kurzem Beschreibungstext.

### `App.xaml.cs`

- **Neue DI-Registrierungen:** `services.AddSingleton<CliReplayAufzeichnungStore>()`, `services.AddSingleton<ICliReplayExportService, CliReplayExportService>()`, `services.AddTransient<KonsolenTestViewModel>()` (Konventionen Z. 323–324 bzw. 360–374).

### `appsettings.json` (`src/Softwareschmiede/appsettings.json`)

- **Neuer Eintrag:** `"AufzeichnungByteBudget": 8388608` in der Sektion `Terminal` (Z. 27–31).

### `AnsiSequenceParser` / `TerminalBuffer` / `TerminalControl` (Bugfix-Umfang)

- Änderungen gemäß dem Ergebnis der Ursachenanalyse (Auslassungen/Dopplungen bei der Devin-CLI); jeder Fix wird von einem Regressionstest begleitet.

### `WindowExtensions` (E2E-Testinfrastruktur, `src/Softwareschmiede.Tests/E2E/Views/WindowExtensions.cs`)

- **Neuer Eintrag:** `w => new KonsolenTestDialogView(w)` in `DialogFactories` (Z. 10–23) — ohne Registrierung erkennt `CurrentView()` den geöffneten modalen Konsolentest-Dialog nicht (fällt auf `ErrorView`/Marker-Suche zurück), was Aufräum-/`ForceClose`-Pfade und Fehlerdiagnosen im E2E-Lauf beeinträchtigt und der etablierten Dialog-Wrapper-Konvention widerspricht.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| Export-Zielpfad (`TaskDetailViewModel.ExportCliReplayAsync`) | Muss auf `.clireplay` enden | `FehlerMeldung` = „Export-Zielpfad muss auf .clireplay enden." (analog `.raw`-Regel Z. 2325–2329) |
| Aufzeichnung vorhanden (`CliReplayExportService`) | `GetCliAufzeichnung` darf nicht `null` liefern | `InvalidOperationException` → `FehlerMeldung` „Für diese Aufgabe liegt keine Aufzeichnung vor." |
| Zu ladende Datei (`CliReplayAufzeichnungStore`) | Magic `SWCLRPLY` + unterstützte Version, konsistente Record-Längen | `InvalidDataException` → `FehlerMeldung` im Konsolentestfenster |
| Header `Cols`/`Rows` (`CliReplayAufzeichnungStore.LadeAsync`) | Beide Werte müssen > 0 sein (0/korrupte Werte lassen die Buffer-Anlage beim Replay werfen) | `InvalidDataException` → `FehlerMeldung` im Konsolentestfenster |
| `ZeitrafferSchwelleText` (KonsolenTestViewModel) | Dezimalzahl ≥ 0 (Sekunden; `0` = alle Pausen auf 0 verkürzt) | Ungültige Eingabe → `FehlerMeldung`/Feldvalidierung, letzte gültige Schwelle bleibt aktiv |
| `TerminalSessionOptions.AufzeichnungByteBudget` | `<= 0` deaktiviert den Mitschnitt (kein Recorder erzeugt) | Kein Fehler — bewusster Opt-out-Schalter; Recorder und Composite entfallen, `CliOutputProtokollWriter` wird direkt als `outputSink` übergeben |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Terminal.AufzeichnungByteBudget` | `int` (`TerminalSessionOptions`, `appsettings.json`) | `8388608` (8 MB) | Byte-Budget der Rohbyte-Aufzeichnung pro Session; `<= 0` deaktiviert den Mitschnitt |

## Seiteneffekte und Risiken

- **`CliProcessHandle.OutputSink` wird bei aktivem Recorder zur Composite-Senke:** `DisposeSessionResourcesAsync` (Z. 621–633) ruft `CompleteAsync` weiter auf der einen übergebenen Senke — bei aktivem Recorder drainet die Composite beide innere Senken, bei `AufzeichnungByteBudget <= 0` bleibt es der `CliOutputProtokollWriter` direkt; Verhalten für `CliOutputProtokollWriter` unverändert.
- **`TerminalSessionService.WriteDiagnosis`-Routing:** Senken ohne `ITerminalDiagnoseSink` erhalten Marker weiter über `OnOutputChunk` → bestehende Tests/Mocks unverändert. Der Mitschnitt enthält absichtlich nur echte CLI-Bytes (keine Marker) — das ist die geforderte Byte-Treue.
- **Speicherbedarf:** pro aufgezeichneter Aufgabe bis zu `AufzeichnungByteBudget` im RAM bis zum Neustart der Aufgabe/App-Ende; bei Budget-Überschreitung gekennzeichnete Teilaufzeichnung (`IstVollstaendig=false`), die den Anfang der Session verlustfrei enthält.
- **Konstruktor-Erweiterungen:** `KiAusfuehrungsService` (+`IOptions<TerminalSessionOptions>`, +`TimeProvider`) → `TestKiAusfuehrungsServiceFactory` erhält optionale Parameter; `SettingsViewModel` (+`IDialogService`, +`IServiceProvider` als **erforderliche** Parameter) → Kompilierbruch in den direkten Test-Instanziierungen `SettingsViewModelTests.CreateSut` (`SettingsViewModelTests.cs` Z. 60–70) und `SettingsViewModelTests_IdePlugin.CreateSut` (`SettingsViewModelTests_IdePlugin.cs` Z. 55–65) — beide übergeben 9 positionale Argumente und müssen um `IDialogService`-/`IServiceProvider`-Mocks erweitert werden. Zusätzlich registriert `MainWindowViewModelUpdateTestBase` (`MainWindowViewModelUpdateTestBase.cs` Z. 77) `AddTransient<SettingsViewModel>` in einem Test-DI-Container **ohne** `IDialogService`; `MainWindowViewModel.NavigateToSettings` (`MainWindowViewModel.cs` Z. 234) löst per `GetRequiredService<SettingsViewModel>()` auf und wird über `SpeichereUpdateEinstellungenUeberUiAsync` (Z. 144–155) von den Update-Tests tatsächlich durchlaufen → `InvalidOperationException` zur Laufzeit, bis `services.AddSingleton(_dialogServiceMock.Object)` ergänzt ist (Feld existiert bereits, Z. 50; `IServiceProvider` löst MS.DI automatisch aus dem Container auf). `TaskDetailViewModel` nur optionaler Parameter → `TaskDetailViewModelTestFactory` unverändert.
- **`TerminalReplaySession.Process`-Stub:** Nicht-gestartetes `Process`-Objekt — `Id`/`HasExited` werfen `InvalidOperationException`; bereits abgefangen in `TaskDetailView.TryGetProcessId` (Z. 161–174). `KiAusfuehrungsService`-Aufrufer sind nicht betroffen, da die Replay-Session niemals über den Service läuft.
- **Eingabe in der Replay-Ansicht:** `TerminalControl` leitet Tastatur/Paste an `Session.WriteInputAsync`, weil `InputStream = Stream.Null` nicht `null` ist — die Replay-Session implementiert `WriteInputAsync`/`WritePromptAsync` als No-Op (kein Fehler, keine Wirkung).
- **`RebuildBufferFromReplay` während laufender Wiedergabe:** Der Neuaufbau nutzt nur bereits abgespielte Chunks und denselben `_renderLock` wie die Wiedergabe-Schleife — kein Vermischen mit Live-Chunks (gleiche Invariante wie `PseudoConsoleSession` Z. 341–351).
- **OpenFileDialog-Automatisierung (E2E):** Nativer Dialog wird wie der Save-Dialog über Fenstertitel + Texteingabe + Return bedient (`TaskDetailView.HandleSaveFileDialog`-Muster, `E2E/Views/TaskDetailView.cs` Z. 345–362, 474–493). Die bestehende `SaveDialogCondition` (Z. 488–493) prüft nur „Speichern unter"/„Save As" — für den OpenFileDialog ist eine eigene Bedingung nötig: `ControlType.Window` mit Name „Öffnen"/„Open" bzw. dem via `ShowOpenFileDialogAsync(title, …)` gesetzten Dialog-Titel (z. B. „CLI-Aufzeichnung öffnen").
- **Baseline-Risiko:** Der dokumentierte instabile Clipboard-Test (`TerminalControlTests.ReadClipboardAndInsertAsync_...`) ist unabhängig von diesen Änderungen.

## Umsetzungsreihenfolge

1. **Aufzeichnungs-Datenmodell anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: `CliOutputChunkRecord` und `CliOutputAufzeichnung` in `src/Softwareschmiede/Infrastructure/Terminal/`.

2. **`CliOutputRecorder` implementieren**
   - Voraussetzungen: Schritt 1; `ITerminalOutputSink` (vorhanden).
   - Beschreibung: Zeitgestempelte, budgetbegrenzte Aufzeichnung mit `TimeProvider`, `IstVollstaendig`-Flag, `GetAufzeichnung()`-Snapshot, idempotentes `Complete`/`CompleteAsync`.

3. **Diagnose-Routing + Composite-Senke**
   - Voraussetzungen: Keine.
   - Beschreibung: `ITerminalDiagnoseSink` anlegen; `CliOutputProtokollWriter` implementiert es; `CompositeTerminalOutputSink` anlegen; `TerminalSessionService.WriteDiagnosis` auf Interface-Routing umstellen.

4. **`CliReplayAufzeichnungStore` (`.clireplay`-Binärformat)**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Header + Record-Serialisierung, Magic-/Versionsprüfung, `Cols`/`Rows`-Header-Validierung, Stream- und Datei-API.

5. **`TerminalReplaySession` implementieren**
   - Voraussetzungen: Schritt 1; `ITerminalSession`, `TerminalBuffer`, `AnsiSequenceParser`, `TerminalOutputChunkEventArgs` (vorhanden); `FakeTimeProvider`-Paket im Testprojekt (vorhanden, `Microsoft.Extensions.TimeProvider.Testing`).
   - Beschreibung: `ITerminalSession`-Implementierung mit eigenem `TerminalBuffer`/`AnsiSequenceParser`/`_renderLock`; Wiedergabe-Task mit Pause-Gate und `Task.Delay(delay, _timeProvider, ct)`-Zeitraffer; `RebuildBufferFromReplay` aus abgespielten Chunks; `Exited` am Ende; vollständige Stub-Member-Liste gemäß Klassenspezifikation (`Process`-Stub, `Stream.Null`-Streams, `Resize` = `true`, `WriteInputAsync`/`WritePromptAsync`/`MarkInputActivity`/`MarkOutputActivity` = No-Op, `Failure` = `null`, `ExitCode` = `null`, `DrainOutputAsync` = `true`, `RuntimeStatus` = `Laeuft` während Wiedergabe/`Inaktiv` sonst).

6. **Konfiguration `AufzeichnungByteBudget`**
   - Voraussetzungen: Keine.
   - Beschreibung: `TerminalSessionOptions.AufzeichnungByteBudget` + Eintrag in `appsettings.json`.

7. **`KiAusfuehrungsService`-Verdrahtung**
   - Voraussetzungen: Schritte 2, 3, 6.
   - Beschreibung: Konstruktor-Deps, Recorder-Erzeugung + Composite in `StartTerminalSessionAsync` (bei `AufzeichnungByteBudget <= 0` direkte Übergabe von `outputWriter`, keine Composite), `_aufzeichnungen`-Registry, `GetCliAufzeichnung`; `catch`-Block (Z. 221–224) auf `CompleteAsync` der übergebenen `outputSink`-Variable umstellen; `TestKiAusfuehrungsServiceFactory` um optionale Parameter erweitern.

8. **`CliReplayExportService` + Export-Fluss**
   - Voraussetzungen: Schritte 4, 7.
   - Beschreibung: `ICliReplayExportService`/`CliReplayExportService` in `Softwareschmiede.App/Services`; `TaskDetailViewModel` (`ExportCliReplayCommand`, `KannCliReplayExportieren`, `ExportCliReplayAsync`, optionaler Ctor-Parameter); `RibbonLargeButton` in `TaskDetailView.xaml` (Gruppe „CLI").

9. **`IDialogService`-Erweiterungen**
   - Voraussetzungen: Keine (für `ShowOpenFileDialogAsync`); `KonsolenTestViewModel` aus Schritt 11 für die Dialog-Methode — die Interface-Signatur kann parallel angelegt werden, die Wpf-Implementierung folgt mit Schritt 12.
   - Beschreibung: `ShowOpenFileDialogAsync` (`Microsoft.Win32.OpenFileDialog`) und `ShowKonsolenTestDialogAsync` in `IDialogService` + `WpfDialogService`.

10. **Quell-Ansicht-Hilfen**
    - Voraussetzungen: Keine.
    - Beschreibung: `CliChunkQuelltextFormatter` und `CliChunkAnzeigeEintrag`.

11. **`KonsolenTestViewModel`**
    - Voraussetzungen: Schritte 4, 5, 9 (`ShowOpenFileDialogAsync`), 10.
    - Beschreibung: Laden der Datei, Session-Erzeugung, Abspiel-Commands, Status-/Positionsanzeige, Quell-Liste, `CloseRequested`, `FehlerMeldung`.

12. **`KonsolenTestDialog` (XAML + Code-behind)**
    - Voraussetzungen: Schritt 11; `TerminalControl` (vorhanden).
    - Beschreibung: Fenster `Title="Konsolentest"`, Werkzeugleiste, `TerminalControl` + `ListView` mit `SelectedItem`-Sync und `ScrollIntoView`, Fehlerbanner; `AutomationProperties.Name` an allen interaktiven Elementen (`AufzeichnungOeffnen`, `ReplayTerminal`, `QuellChunkListe`, `WiedergabeStarten`, `WiedergabePausieren`, `ZeitrafferSchwelle`, `WiedergabeStatus`, `WiedergabePosition`, `UnvollstaendigHinweis`, `FehlerMeldung`, `KonsolenTestSchliessen`).

13. **Einstiegspunkt in den Einstellungen**
    - Voraussetzungen: Schritte 9, 11.
    - Beschreibung: `SettingsViewModel` (erforderliche Ctor-Deps `IDialogService` + `IServiceProvider` + `KonsolenTestOeffnenCommand`); Abschnitt „Diagnose" im `SettingsView`-Tab „Allgemein". Im selben Schritt die betroffenen Test-Konstruktionsstellen anpassen: `SettingsViewModelTests.CreateSut` (Z. 60–70) und `SettingsViewModelTests_IdePlugin.CreateSut` (Z. 55–65) um `IDialogService`-/`IServiceProvider`-Mocks erweitern; in `MainWindowViewModelUpdateTestBase` (Z. 59–84) `services.AddSingleton(_dialogServiceMock.Object)` registrieren (sonst `InvalidOperationException` bei `GetRequiredService<SettingsViewModel>` in den Update-Tests).

14. **DI-Registrierungen**
    - Voraussetzungen: Schritte 4, 8, 11.
    - Beschreibung: `CliReplayAufzeichnungStore`, `ICliReplayExportService`, `KonsolenTestViewModel` in `App.xaml.cs`.

15. **Unit-Tests für alle neuen Komponenten**
    - Voraussetzungen: Schritte 2–14.
    - Beschreibung: Testklassen gemäß Abschnitt „Neue Tests"; Bestätigung, dass `TerminalReplaySession` für identische Chunk-Folgen denselben Buffer-Endzustand wie `PseudoConsoleSession` liefert (Vergleichs-Test).

16. **Devin-CLI-Ursachenanalyse + Bugfix + Regressionstests**
    - Voraussetzungen: Schritte 1–15 (Werkzeug nutzbar); Devin-CLI als konfiguriertes KI-Plugin zum Aufzeichnen einer echten Session.
    - Beschreibung: Reale Aufzeichnung erzeugen, im Konsolentestfenster reproduzieren, Defekt in `AnsiSequenceParser`/`TerminalBuffer`/`TerminalControl` isolieren und beheben; pro Defekt ein Regressionstest mit dem problematischen Chunk-Muster.

17. **E2E-Test für den Fenster-Fluss**
    - Voraussetzungen: Schritte 9–14.
    - Beschreibung: `KonsolenTestDialogView`-Wrapper mit eigener OpenFileDialog-Titel-Bedingung („Öffnen"/„Open" bzw. gesetzter Dialog-Titel), Registrierung des Wrappers in `WindowExtensions.DialogFactories` (Z. 10–23), `SettingsView`-Wrapper-Erweiterung, Szenario-Methode eingehängt in `RunGeneralTests` (kein ConPTY nötig — die `.clireplay`-Datei wird direkt per `CliReplayAufzeichnungStore` erzeugt); das konsolidierte Szenario deckt neben dem Happy Path auch den UI-ausgelösten Formatfehler (`FehlerMeldung`-Banner) und den OpenFileDialog-Abbruch ab und schließt den Dialog im `finally` (TryClose-Muster analog `TryCloseTaskDetail`); optionaler Export-E2E in `RunConPtyTests` mit `TaskDetailView`-Wrapper-Methode `ExportCliReplay(zielPfad)` (klickt `CliReplayExport` + bedient den Save-Dialog über die vorhandene `HandleSaveFileDialog`/`SaveDialogCondition`-Infrastruktur) — in dieser Sandbox via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` immer übersprungen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `OnOutputChunk_ErhaeltChunkGrenzenUndBytes` | `CliOutputRecorderTests` | Bytes werden kopiert, Chunk-Grenzen bleiben exakt erhalten (keine Zeilen-Normalisierung, `\r` erhalten). |
| `OnOutputChunk_ZeitstempelViaTimeProvider` | `CliOutputRecorderTests` | Offsets relativ zum Start über `FakeTimeProvider`. |
| `OnOutputChunk_BudgetUeberschreitung_MarkiertUnvollstaendig` | `CliOutputRecorderTests` | Bei Überschreitung bleibt das Präfix intakt, `IstVollstaendig=false`, keine weiteren Chunks. |
| `Complete_Idempotent_SetztEndeUtc` | `CliOutputRecorderTests` | `Complete`/`CompleteAsync` mehrfach aufrufbar. |
| `OnOutputChunk_FaechertAnAlleInnerenSenken` | `CompositeTerminalOutputSinkTests` | Reihenfolge/Vollständigkeit des Fanout. |
| `OnDiagnoseChunk_NurAnDiagnoseSenken` | `CompositeTerminalOutputSinkTests` | Diagnose-Bytes erreichen nur `ITerminalDiagnoseSink`-Implementierungen, nicht den Recorder. |
| `CompleteAsync_DraintAlleSenken` | `CompositeTerminalOutputSinkTests` | Beide inneren `CompleteAsync` werden aufgerufen. |
| `WriteDiagnosis_DiagnoseSink_ErhältMarkerUeberDiagnoseKanal` | `TerminalSessionServiceTests` (Erweiterung) | Marker-Bytes gehen an `OnDiagnoseSink`, nicht an `OnOutputChunk`. |
| `SpeichernLaden_Roundtrip` | `CliReplayAufzeichnungStoreTests` | Bytes, Offsets, Header-Metadaten (`AufgabeId`, `StartUtc`, `EndeUtc`, `Cols`, `Rows`, `PluginName`, `IstVollstaendig`) identisch nach Roundtrip. |
| `LadeAsync_FalschesMagic_WirftInvalidData` | `CliReplayAufzeichnungStoreTests` | Formatfehler-Erkennung. |
| `LadeAsync_UngueltigeColsRows_WirftInvalidData` | `CliReplayAufzeichnungStoreTests` | Header-Validierung: `Cols`/`Rows` = 0 oder negativ → `InvalidDataException`. |
| `Wiedergabe_ErzeugtGleichenBufferWieLiveSession` | `TerminalReplaySessionTests` | Dieselbe Chunk-Folge durch `TerminalReplaySession` und `PseudoConsoleSession` (`TestPseudoConsoleSessionFactory`) liefert identischen `TerminalBufferSnapshot`. |
| `Wiedergabe_WartetZeitrealZwischenChunks` | `TerminalReplaySessionTests` | Chunks erscheinen erst nach ihrem Offset (`FakeTimeProvider.Advance`). |
| `Wiedergabe_ZeitrafferVerkuerztLangePausen` | `TerminalReplaySessionTests` | Pause > `ZeitrafferSchwelle` wird auf die Schwelle gekürzt; Pause < Schwelle bleibt zeitreal; Schwelle 0 = keine Verzögerung. |
| `PausierenFortsetzen_BlockiertUndSetztFort` | `TerminalReplaySessionTests` | Kein Chunk-Apply während Pause; Fortsetzen setzt exakt fort. |
| `RebuildBufferFromReplay_WaehrendWiedergabe` | `TerminalReplaySessionTests` | Neuaufbau enthält nur bereits abgespielte Chunks; nach Vollendung identisch zum Endzustand. |
| `Ende_FeuertExitedUndSetztInaktiv` | `TerminalReplaySessionTests` | `Exited` einmalig, `ExitCode=null`, `RuntimeStatus=Inaktiv`. |
| `StubMember_SindSicher` | `TerminalReplaySessionTests` | `Resize` → `true`, `WriteInputAsync`/`WritePromptAsync`/`MarkInputActivity`/`MarkOutputActivity` No-Op, `Failure` = `null`, `ExitCode` = `null`, `DrainOutputAsync` begrenzt/`true`, `RuntimeStatus` = `Laeuft` während Wiedergabe und `Inaktiv` vor/nach, `Dispose` beendet Wiedergabe. |
| `Formatiere_MachtSteuersequenzenSichtbar` | `CliChunkQuelltextFormatterTests` | `ESC` → `␛`, `CR`/`LF`/`TAB` markiert, Binärbytes als `\xNN`, UTF-8 korrekt dekodiert. |
| `AufzeichnungOeffnen_LaadtUndErzeugtReplaySession` | `KonsolenTestViewModelTests` | Datei laden → `Session` gesetzt, `QuellEintraege` befüllt, `ShowOpenFileDialogAsync`-Mock. |
| `AufzeichnungOeffnen_Formatfehler_ZeigtFehlermeldung` | `KonsolenTestViewModelTests` | `InvalidDataException` → `FehlerMeldung`, keine Session. |
| `AufzeichnungOeffnen_DialogAbbruch_KeinZustandswechsel` | `KonsolenTestViewModelTests` | `ShowOpenFileDialogAsync` liefert `null` → keine Session, keine `FehlerMeldung`. |
| `AufzeichnungOeffnen_UnvollstaendigeAufzeichnung_ZeigtHinweis` | `KonsolenTestViewModelTests` | Geladene Datei mit `IstVollstaendig = false` → `UnvollstaendigHinweis` gesetzt/sichtbar. |
| `WiedergabeStartenPausierenFortsetzen_SteuernSession` | `KonsolenTestViewModelTests` | Commands steuern `IstWiedergabeAktiv`/`IstPausiert`; gültige `ZeitrafferSchwelleText`-Eingabe aktualisiert `ZeitrafferSchwelle` live. |
| `ZeitrafferSchwelleText_UngueltigeEingabe_ZeigtFehlerUndBehältSchwelle` | `KonsolenTestViewModelTests` | Negativfall der Validierungsregel: ungültige Eingabe (nicht-numerisch oder negativ) → `FehlerMeldung`/Feldvalidierung gesetzt; die zuletzt gültige `ZeitrafferSchwelle` der Session bleibt wirksam (kein Zustandswechsel auf einen ungültigen Wert). |
| `ExportCliReplay_SchreibtClireplayDatei` | `TaskDetailViewModelTests_CliReplayExport` | Happy Path analog `TaskDetailViewModelTests_CliRawExport` (Save-Dialog-Mock, echte Session über `TestKiAusfuehrungsServiceFactory` mit Sink-injizierendem Launcher-Double, das `OnOutputChunk` mit Test-Chunks füttert). |
| `ExportCliReplay_KeineAufzeichnung_ZeigtFehlermeldung` | `TaskDetailViewModelTests_CliReplayExport` | Aufgabe ohne/ungestartete Session → `FehlerMeldung`, keine Datei. |
| `ExportCliReplayAsync_ShouldSetFehlerMeldung_WhenTargetPathIsNotClireplay` | `TaskDetailViewModelTests_CliReplayExport` | Zielpfad ohne `.clireplay`-Endung → `FehlerMeldung`, keine Datei (Pendant zu `ExportCliRawAsync_ShouldSetFehlerMeldung_WhenTargetPathIsNotRaw`). |
| `ExportCliReplayAsync_ShouldSetFehlerMeldung_WhenFileWriteFails` | `TaskDetailViewModelTests_CliReplayExport` | Schreibfehler (z. B. unleserliches Zielverzeichnis) → `FehlerMeldung` (Pendant zu `..._WhenFileWriteFails`). |
| `ExportCliReplayAsync_ShouldAbort_WhenDialogCancelled` | `TaskDetailViewModelTests_CliReplayExport` | Save-Dialog liefert `null` → keine Datei, keine `FehlerMeldung` (Pendant zu `ExportCliRawAsync_ShouldAbort_WhenDialogCancelled`). |
| `StartTerminalSession_VerdrahtetRecorder` | `KiAusfuehrungsServiceTests` (Erweiterung) | Chunks aus der Sink landen in `GetCliAufzeichnung` (inkl. PluginName/Cols/Rows), auch nach Session-Ende abrufbar. |
| `StartTerminalSession_BudgetNullOderNegativ_KeinRecorder` | `KiAusfuehrungsServiceTests` (Erweiterung) | `TerminalSessionOptions.AufzeichnungByteBudget <= 0` → kein `CliOutputRecorder` erzeugt, `GetCliAufzeichnung(aufgabeId)` liefert `null`, `CliOutputProtokollWriter` wird direkt (ohne Composite) als `outputSink` übergeben. |
| *(Platzhalter je Fund)* | `AnsiSequenceParserTests` / `TerminalBufferTests` / `TerminalControlTests` | Regressionstest pro identifiziertem Devin-CLI-Defekt mit dem realen Chunk-Muster. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TestKiAusfuehrungsServiceFactory` (`src/Softwareschmiede.Tests/Helpers/`) | `KiAusfuehrungsService`-Konstruktor erhält `IOptions<TerminalSessionOptions>` + `TimeProvider` — als optionale Parameter mit Defaults nachrüsten. |
| `TestTerminalSessionFactory` / `DeterministicPseudoConsoleProcessLauncher` (`src/Softwareschmiede.Tests/Helpers/`) | Der deterministische Launcher erzeugt die Session auf leeren MemoryStreams und füttert keine Chunks — für `StartTerminalSession_VerdrahtetRecorder` und `ExportCliReplay_SchreibtClireplayDatei` ist ein Launcher-Double mit beschreibbarem/kontrollierbarem Output-Stream nötig, das `OnOutputChunk` der Senke mit Test-Chunks aufruft (z. B. Erweiterung von `TestTerminalSessionFactory` um ein `ChunkEmitting`-Launcher-Double). |
| `TerminalSessionServiceTests` (Diagnose-Marker-Tests) | `WriteDiagnosis` routet über `ITerminalDiagnoseSink` — Tests mit einfachen `ITerminalOutputSink`-Mocks laufen weiter über den `OnOutputChunk`-Fallback; nur Tests, die das Diagnose-Routing explizit prüfen, ergänzen eine Diagnose-Sink. |
| `SettingsViewModelTests` (`src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs`, `CreateSut` Z. 60–70) | `SettingsViewModel`-Konstruktor erhält `IDialogService` + `IServiceProvider` als erforderliche Parameter → `CreateSut` um `Mock<IDialogService>` und `Mock<IServiceProvider>` (bzw. den vorhandenen Provider-Mock) erweitern; sonst Kompilierbruch. |
| `SettingsViewModelTests_IdePlugin` (`src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests_IdePlugin.cs`, `CreateSut` Z. 55–65) | Gleicher Grund: `CreateSut` um die beiden neuen erforderlichen Parameter erweitern. |
| `MainWindowViewModelUpdateTestBase` (`src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelUpdateTestBase.cs`, Container-Aufbau Z. 59–84) | Registriert `AddTransient<SettingsViewModel>` (Z. 77) ohne `IDialogService` im Container; `NavigateToSettings` → `GetRequiredService<SettingsViewModel>` (`MainWindowViewModel.cs` Z. 234) wird von `SpeichereUpdateEinstellungenUeberUiAsync` (Z. 144–155) durchlaufen → `services.AddSingleton(_dialogServiceMock.Object)` ergänzen (Feld Z. 50 existiert bereits; `IServiceProvider` löst MS.DI automatisch auf). |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | `KonsolenTestfenster_AufzeichnungLadenAbspielenPausierenUndQuellAnsicht_E2E` — Einstellungen → „Diagnose" → Fenster öffnen → `.clireplay`-Datei (vom Test via `CliReplayAufzeichnungStore` geschrieben, mit ANSI-Sequenzen + langen Pausen) über nativen OpenFileDialog laden → Quell-Liste zeigt Chunks mit sichtbaren Steuersequenzen → Abspielen → Status/Position aktualisiert → Pausieren (keine neuen Chunks) → Fortsetzen → Zeitraffer-Schwelle setzen → Wiedergabe läuft bis „Beendet". Anschließend **im selben Dialog/selben Szenario** (Konsolidierungsregel, keine neue Testmethode): korrupte/inkompatible Datei laden → `FehlerMeldung`-Banner sichtbar (Assertion über `KonsolenTestDialogView.IstFehlerSichtbar`/`GetFehlerMeldung` auf dem Dialogfenster); OpenFileDialog-Abbruch (ESC) → kein Zustandswechsel, kein Fehlerbanner. Der modale Dialog wird im `finally` geschlossen (TryClose-Muster analog `TryCloseTaskDetail` in `E2E_CliRawExport.cs` Z. 99–108), damit Folgeszenarien in `RunGeneralTests` nicht blockiert werden. | `src/Softwareschmiede.Tests/E2E/E2E_KonsolenTestfenster.cs` (Aufruf in `RunGeneralTests`, `End2EndTest`) | Fenster-Fluss komplett über die UI erreichbar; Vergleichsansicht, Steuerung und UI-ausgelöste Fehler-/Abbruchfälle funktionieren gegen den echten `TerminalControl`-Renderpfad | Benutzerfluss (Fenster öffnen, Datei wählen, abspielen, pausieren, Quell-Vergleich, Fehleranzeige) ist nur über UI erreichbar; Rendern geschieht in `OnRender` und ist per FlaUI nicht textuell assertbar — Status-, Positions-, Quell-Listen- und `FehlerMeldung`-Elemente sind deshalb die verbindlichen Assertions. Kein ConPTY nötig (kein echter CLI-Prozess). |
| Empfohlen | `CliReplayExport_ErstelltClireplayDateiAusLaufenderSession_E2E` — Aufgabe mit gestarteter CLI (KiSimulator-Muster aus `E2E_CliRawExport`) → im selben Szenario: (a) „Aufzeichnung exportieren" → Save-Dialog per ESC abbrechen → keine Datei, kein `ErrorView`-Banner (Pendant zu `CliRawExport_AbbruchErzeugtKeineDateiUndKeinenFehlerbanner_E2E`); (b) erneut exportieren → Save-Dialog bestätigen → `.clireplay`-Datei existiert und lässt sich via `CliReplayAufzeichnungStore.LadeAsync` lesen | `src/Softwareschmiede.Tests/E2E/E2E_CliReplayExport.cs` (Aufruf in `RunConPtyTests`, neben den `CliRawExport`-Szenarien) | Export-Pfad aus echter Session verdrahtet den Recorder korrekt; Abbruch erzeugt weder Datei noch Fehleranzeige | End-to-End-Nachweis der Recorder-Verdrahtung über eine echte (Pipe-Backend-)Session; benötigt laufende CLI → ConPTY-Lane analog `CliRawExport`. **Hinweis:** `RunConPtyTests` wird in dieser Sandbox via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` immer übersprungen — die Recorder-Verdrahtung ist hier nur durch `KiAusfuehrungsServiceTests`/`TaskDetailViewModelTests_CliReplayExport` (In-Memory-Launcher) nachgewiesen. |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine — die Änderungen sind additiv (neuer Ribbon-Button, neuer Einstellungen-Abschnitt, neuer Dialog); bestehende Szenarien berühren diese Elemente nicht.

## Offene Punkte

Keine.
