# Logik — Konsolentestfenster Schrittmodus

## `TerminalReplaySession` — Kern der Erweiterung
Datei: `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs` (344 Zeilen)

`sealed`-Implementierung von `ITerminalSession` über einer `CliOutputAufzeichnung`. Erzeugt im Konstruktor `Buffer` aus `aufzeichnung.Cols`/`Rows` (Z. 53); `TimeProvider` ist injizierbarer Test-Hook.

### Felder / Zustand (Schrittmodus-relevant)

| Feld | Zeile | Beschreibung |
|------|-------|--------------|
| `_aufzeichnung` | 17 | `readonly CliOutputAufzeichnung` — Quell der `Chunks`-Sequenz |
| `_timeProvider` | 18 | `readonly TimeProvider` — für `Task.Delay` der Pausen |
| `_parser` | 20 | **`readonly AnsiSequenceParser`** — kumuliert Zustand über Chunks (State-Maschine, `_paramBuffer`, `_textBuffer`, `_utf8Decoder`); ein Rückwärtsschritt muss diesen Zustand zurücksetzen (Reset oder Neuinstanzierung → Feld müsste nicht-`readonly` werden) |
| `_abgespielteChunks` | 21 | `List<byte[]>` — Prefix-Liste der bereits angewendeten Chunk-Daten; wird aktuell **nur per `Add` befüllt** (Z. 270), kein Kürzungspfad vorhanden |
| `_renderLock` | 22 | Serialisiert `OutputChunk`-Feuern + `Add` + `Parse`/`Apply` (Schleife) sowie `RebuildBufferFromReplay`; `Pausieren()` nutzt es als Fence (Z. 147–149) |
| `_statusLock` | 23 | Schützt `_runtimeStatus` |
| `_playbackCts` | 24 | Abbruch der Wiedergabe-Schleife (Dispose) |
| `_pauseLock` / `_pauseGate` | 25/30 | Async-Pause-Gate (`TaskCompletionSource` mit `RunContinuationsAsynchronously`); Invariante „`_istPausiert` ⇒ Gate geschlossen" nur unter `_pauseLock` gültig |
| `_process` | 31 | Nicht gestarteter `Process`-Stub (Interface-Pflicht) |
| `_playbackTask` | 32 | Task der `WiedergabeLoopAsync` |
| `_zeitrafferTicks` | 33 | `long` (Interlocked) — `ZeitrafferSchwelle`, Default `TimeSpan.MaxValue` (Echtzeit) |
| `_istPausiert` | 34 | `volatile bool` |
| `_aktuellerChunkIndex` | 35 | `int` — Anzahl angewendeter Chunks; von der Schleife per `Interlocked.Exchange(ref _aktuellerChunkIndex, i + 1)` gesetzt (Z. 277); gelesen via `Volatile.Read` |
| `_wiedergabeGestartet` | 36 | `int` — macht `WiedergabeStarten()` idempotent |
| `_exitedSignaled` | 37 | `int` — `Exited` maximal einmal |
| `_disposed` | 38 | `int` |

### Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `TerminalReplaySession(aufzeichnung, timeProvider, logger?)` | public | Erzeugt Session + `Buffer` (Z. 45–54) |
| `WiedergabeStarten()` | public | Idempotent (`CompareExchange` auf `_wiedergabeGestartet`); setzt `RuntimeStatus = Laeuft` und startet `_playbackTask = Task.Run(() => WiedergabeLoopAsync(_playbackCts.Token))` (Z. 121–130) |
| `Pausieren()` | public | Setzt `_istPausiert` + schließt `_pauseGate` unter `_pauseLock`, danach `_renderLock`-Fence — nach Rückkehr wird kein Chunk mehr angewendet (Z. 133–150) |
| `Fortsetzen()` | public | Öffnet das Gate unter `_pauseLock` (Z. 153–160) |
| `RebuildBufferFromReplay()` | public | **Präfix-Rebuild**: `Buffer.Reset()` + frischer lokaler `AnsiSequenceParser` + `Parse`/`Apply` über alle `_abgespielteChunks` — unter `_renderLock` (Z. 187–197). Feuert **kein** `BufferChanged` (Aufrufer `TerminalControl` invalidiert selbst) |
| `Resize(cols, rows)` | public | Stub, immer `true` (Z. 163) — `TerminalControl` resized den Buffer selbst |
| `WriteInputAsync` / `WritePromptAsync` / `MarkInputActivity` / `MarkOutputActivity` / `DrainOutputAsync` | public | Unschädliche Stubs (Z. 166–183) |
| `Dispose()` | public | `_disposed`-CAS, `Cancel` + `Fortsetzen`, kein synchrones Warten auf die Schleife, `RuntimeStatus → Inaktiv` (Z. 200–222) |
| `WiedergabeLoopAsync(ct)` | private | **Positionsproblem für Schrittmodus**: iteriert mit lokalem `for (var i = 0; i < chunks.Count; i++)` (Z. 233); pro Iteration: `ct.ThrowIfCancellationRequested` → `WartePauseGateAsync` → `delay = min(chunk.Offset - vorherigerOffset, ZeitrafferSchwelle)` via `Task.Delay(delay, _timeProvider, ct)` → erneutes Gate-Wait unter `_renderLock` mit `_istPausiert`-Recheck → `OutputChunk`-Event → `_abgespielteChunks.Add` → `_parser.Parse` + `Buffer.Apply` → `_aktuellerChunkIndex = i + 1` → `BufferChanged` (Z. 227–293). `vorherigerOffset` (Z. 229) ist ebenfalls schleifenlokal — ein Fortsetzen nach Schritten bräuchte den Offset des zuletzt angewendeten Chunks als Basis |
| `WartePauseGateAsync(ct)` | private | `_pauseGate.Task.WaitAsync(ct)` unter `_pauseLock` gelesen (Z. 296–302) |
| `RaiseExited()` | private | Guard `_disposed`/`_exitedSignaled`; `RuntimeStatus → Inaktiv`, `Exited` mit `ExitCode = null` (Z. 311–327) — wird im `finally` der Schleife nur bei **nicht** abgebrochenem Durchlauf gefeuert (Z. 290–291) |
| `SetRuntimeStatus(status)` | private | Unter `_statusLock`; feuert `RuntimeStatusChanged` bei Änderung (Z. 329–343) |

### Properties / Events

| Member | Zeile | Semantik |
|--------|-------|----------|
| `AktuellerChunkIndex` | 88 | Anzahl abgespielter Chunks (0..`Chunks.Count`) — einzige öffentliche Positionsquelle |
| `IstPausiert` | 91 | Pausiert-Flag |
| `ZeitrafferSchwelle` | 96–100 | Deckel der Inter-Chunk-Pausen (`TimeSpan.Zero` = maximale Geschwindigkeit) |
| `RuntimeStatus` | 73–76 | `Inaktiv` ↔ `Laeuft` (kein `WartetAufEingabe`-Pfad) |
| `OutputChunk` | 103 | Pro angewendetem Chunk (Rohbytes, vor dem Parsen) |
| `Exited` | 106 | Einmalig am Schleifenende (ohne Abbruch) |
| `Failed` | 110 | Nie ausgelöst (`#pragma CS0067`) |
| `BufferChanged` | 114 | Nach jedem angewendeten Chunk (nicht nach `RebuildBufferFromReplay`) |
| `RuntimeStatusChanged` | 117 | Bei Statuswechsel |

### Schrittmodus-Befunde (aus der Anforderung verifiziert)

- Der Schleifenindex `i` ist die **tatsächliche Positionsquelle** — `_aktuellerChunkIndex` folgt nur nach (`i + 1`). Für „Fortsetzen an der Schrittposition" muss die Schleife ihre Position aus `_abgespielteChunks.Count`/`_aktuellerChunkIndex` ableiten statt aus `i`.
- `_parser` ist `readonly` und zustandskumulierend — beim Rückwärtsschritt steht er nach dem Präfix-Rebuild auf dem alten Endzustand; der Schleifenpfad parst mit `_parser`, der Rebuild-Pfad mit einem **frischen** lokalen Parser (Z. 192). Beide Pfade sind damit aktuell nicht zustandskohärent.
- `RebuildBufferFromReplay` ist der bestehende deterministische Neuaufbau — ein Rückwärtsschritt kann `RemoveRange` auf `_abgespielteChunks` + denselben Rebuild + `_parser.Reset()`/Ersatz nutzen.
- `BufferChanged` feuert die Schleife pro Chunk **außerhalb** des `_renderLock` (Z. 278); `OnReplayBufferChanged` im ViewModel aktualisiert darüber Position und Quell-Selektion.
- `Pausieren()` garantiert „kein Chunk nach Rückkehr" — Schritt-Methoden müssen mit dieser Garantie koexistieren (z. B. Schritt nur bei geschlossenem Gate oder nach `Pausieren`-Fence unter `_renderLock`).

## `KonsolenTestViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs` (405 Zeilen)

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `KonsolenTestViewModel(dialogService, store, timeProvider, logger, dispatcherInvoke?)` | public | DI-Konstruktor; `dispatcherInvoke` als Test-Hook (Default über `DispatcherInvokeFactory.Create`) (Z. 43–61) |
| `AufzeichnungOeffnenCommand` | public `ICommand` | `AsyncRelayCommand` → `OeffneAufzeichnungAsync` (Z. 56, 182–210): `ShowOpenFileDialogAsync` → `store.LadeAsync` → Quell-Einträge via `Task.Run` → `LadeAufzeichnung` |
| `WiedergabeStartenCommand` | public `ICommand` | `RelayCommand`, CanExecute `_replaySession is not null && !IstWiedergabeAktiv` (Z. 57); bei `_wiedergabeBeendet` frische Session via `ErsetzeReplaySessionDurchFrische` (Z. 285–300) |
| `WiedergabeNeustartenCommand` | public `ICommand` | CanExecute `_replaySession is not null && IstWiedergabeAktiv` (Z. 58); verwirft die laufende Session und startet frisch ab Position 0 (Z. 302–313) |
| `WiedergabePausierenCommand` | public `ICommand` | CanExecute `IstWiedergabeAktiv` (Z. 59); Toggle `Pausieren`/`Fortsetzen` + `IstPausiert`/`StatusText` (Z. 325–342) |
| `SchliessenCommand` | public `ICommand` | `EntsorgeReplaySession` + `CloseRequested` (Z. 60, 344–348) |
| `Session` | public | An `TerminalControl.Session` gebunden (`ITerminalSession?`, privat setzbar) |
| `QuellEintraege` / `AktuellerQuellEintrag` | public | `ObservableCollection<CliChunkAnzeigeEintrag>` / selektierter Eintrag (Two-Way an `QuellChunkListe` gebunden) |
| `DateiPfad`, `StatusText`, `PositionsText`, `IstWiedergabeAktiv`, `IstPausiert`, `ZeitrafferSchwelleText`, `FehlerMeldung`, `UnvollstaendigHinweis` | public | Anzeige-Zustand; `IstWiedergabeAktiv`-Setter ruft `RelayCommand.Refresh()` (Z. 127) |
| `CloseRequested` | public event | `EventHandler<bool>` — führt zu `Window.Close()` |
| `LadeAufzeichnung(...)` | private | Entsorgt alte Session, erzeugt Replay-Session (`ErzeugeReplaySession`), befüllt `QuellEintraege`, setzt Position/Status, `Session = _replaySession` (Z. 212–237) |
| `ErzeugeReplaySession(...)` | private | `new TerminalReplaySession(aufzeichnung, _timeProvider)` + Schwelle übernehmen + `BufferChanged`/`Exited` abonnieren (Z. 256–266) |
| `EntsorgeReplaySession()` | private | Events abmelden → `Dispose` → `_replaySession = null`, `Session = null` (Z. 268–283) |
| `ErsetzeReplaySessionDurchFrische()` | private | Entsorgen + neu erzeugen + `AktuellerQuellEintrag = null`, `PositionsText = "Chunk 0/n"`, `_wiedergabeBeendet = false` (Z. 315–323) |
| `OnReplayBufferChanged` | private | Sender-Identitätsprüfung (ReferenceEquals, doppelt), dann `_dispatcherInvoke`: `PositionsText = $"Chunk {index}/{QuellEintraege.Count}"`, `AktuellerQuellEintrag = QuellEintraege[index - 1]` (Z. 350–367) — **der bestehende Synchronisationspfad, den Schritte ebenfalls auslösen können** |
| `OnReplayExited` | private | `IstWiedergabeAktiv = false`, `IstPausiert = false`, `_wiedergabeBeendet = true`, `StatusText = "Wiedergabe beendet."` (Z. 369–384) |
| `TryParseZeitrafferSchwelle` | private static | Validiert Sekunden-Eingabe (Z. 386–404) |

Abonnierte Events: `TerminalReplaySession.BufferChanged`, `TerminalReplaySession.Exited` (je aktiver Session; `RuntimeStatusChanged`/`OutputChunk`/`Failed` werden **nicht** abonniert).
Publizierte Events: `CloseRequested`.

Befund: `_wiedergabeBeendet` steuert nur den „erneut abspielen"-Pfad von `WiedergabeStarten`; ein reines Schritte-Modell (ohne Start) hat aktuell kein eigenes Zustands-Flag — `IstWiedergabeAktiv` ist nur über den Start/Exited-Pfad wahr.

## `KonsolenTestDialog`
Dateien: `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml` + `.xaml.cs`

- Nicht-modales `Window` (Titel „Konsolentest"), `DataContext = KonsolenTestViewModel`; `Closed` dispost das ViewModel (xaml.cs Z. 20–24).
- Werkzeugleiste (Grid.Row 0, XAML Z. 18–84): Buttons `AufzeichnungOeffnen` (Z. 25–28), `WiedergabeStarten` „Abspielen" (Z. 39–42), `WiedergabeNeustarten` „Neu starten" (Z. 43–48), `WiedergabePausieren` „Pausieren/Fortsetzen" (Z. 49–53), `ZeitrafferSchwelle`-TextBox (Z. 59–62), Status `WiedergabeStatus` (Z. 63–69), Position `WiedergabePosition` (Z. 70–76), `KonsolenTestSchliessen` (Z. 77–82) — alle mit `AutomationProperties.Name` (E2E-Adressierbarkeit), `Padding="10,4"`, `Margin="8,0,0,0"`-Kaskade.
- `ReplayTerminal` = `TerminalControl` mit `Session="{Binding Session}"` (Z. 126–128).
- `QuellChunkListe` = `ListView` mit `SelectedItem="{Binding AktuellerQuellEintrag, Mode=TwoWay}"` (Z. 135–158); `SelectionChanged` → `OnQuellListeSelectionChanged` ruft `ScrollIntoView` (xaml.cs Z. 29–33) — folgt damit auch Schritt-Selektionen.
- Hinweis-Banner `UnvollstaendigHinweis` (Z. 87–99) und Fehlerbanner `FehlerMeldung` (Z. 102–112).

## `TerminalControl` (Session-Bindung)
Datei: `src/Softwareschmiede.App/Controls/TerminalControl.cs`

- `OnSessionChanged` (Z. 94–132): alte Session abmelden → `session.BufferChanged += OnBufferChanged` → **`session.RebuildBufferFromReplay()`** (Z. 124) → `_buffer = session.Buffer` + Resize → sofort `InvalidateVisual`. Beim Session-Tausch (Neustart mit frischer Session) wird der Buffer dadurch aus `_abgespielteChunks` der neuen Session (leer) neu aufgebaut.
- `OnBufferChanged` (Z. 134–141): Dispatcher-`InvalidateVisual` + Scroll-Follow.
- `OnRenderSizeChanged` (Z. 301–316): resizet `_buffer` **und** ruft `session.Resize` (bei Replay ein Stub).
- `OnCreateAutomationPeer`-Override stellt UIA-Zugänglichkeit sicher (Z. 143 ff.).

## `CliReplayAufzeichnungStore`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/CliReplayAufzeichnungStore.cs` (177 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SpeichernAsync(stream, aufzeichnung, ct)` | public | `.clireplay`-Binärformat: Magic `SWCLRPLY`, Version 1, Header (AufgabeId, StartUtc, EndeUtc, Cols, Rows, IstVollstaendig, PluginName) + Records `[Int64 OffsetTicks][Int32 Length][Bytes]` (Z. 17–52) |
| `LadeAsync(stream, ct)` | public | Deserialisiert mit strenger Validierung (`InvalidDataException` bei Magic/Version/Geometrie/Längen) (Z. 59–69, `Lese` Z. 94–176) |
| `SpeichernAsync(pfad, ...)` / `LadeAsync(pfad, ...)` | public | Datei-Varianten (Z. 75–92) |

DI: `AddSingleton<CliReplayAufzeichnungStore>` (`App.xaml.cs` Z. 325). Für den Schrittmodus unverändert nutzbar — liefert die `Chunks`-Sequenz.

## `CliOutputRecorder`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/CliOutputRecorder.cs`

`ITerminalOutputSink`-Implementierung: zeichnet Rohbytes mit `TimeProvider`-Zeitstempel chunkweise auf; bei Budget-Überschreitung stoppt die Aufnahme und `IstVollstaendig = false` (intaktes Präfix bleibt). `GetAufzeichnung()` liefert die `CliOutputAufzeichnung`-Momentaufnahme. Verdrahtet in `KiAusfuehrungsService` (Z. 26, 239–247, `RegistriereAufzeichnung` Z. 352); `TerminalSessionOptions.AufzeichnungByteBudget` steuert das Budget. Export erfolgt über `CliReplayExportService` aus dem Task-Detail.

## `CliChunkQuelltextFormatter`
Datei: `src/Softwareschmiede.App/Services/CliChunkQuelltextFormatter.cs`

`static`, `Formatiere(ReadOnlySpan<byte>)`: UTF-8-Dekodierung + sichtbare Steuerzeichen (ESC→`␛`, CR→`\r`, LF→`\n`, TAB→`\t`, übrige→`\xNN`). Erzeugt `CliChunkAnzeigeEintrag.Quelltext`.

## `AnsiSequenceParser`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs` (459 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Parse(ReadOnlySpan<byte>)` | public | Zustandsbehafteter VT100/ANSI-Parser → `IEnumerable<TerminalEvent>`; interner Zustand: `_state` (Normal/Escape/Csi/CsiQuestion/Osc/EscapeCharset), `_paramBuffer`, `_textBuffer`, `_utf8Decoder` (chunk-übergreifende UTF-8-Sequenzen via `flush:false`, Z. 196–210) |
| `Reset()` | public | Setzt State-Maschine, beide Puffer und den UTF-8-Decoder-Übertrag zurück (Z. 182–188) — **vollständiger Zustands-Reset, für den Schritt-/Rebuild-Pfad nutzbar** |

## `PseudoConsoleSession` (Referenzpfad)
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

- `ReadLoopAsync` (Z. 275–336): Live-Referenz für die Chunk-Anwendungsreihenfolge — `_replayBuffer.Append` (Z. 316, `TerminalReplayBuffer`), `OutputChunk`-Event, `Parse`/`Apply` unter Render-Lock.
- `RebuildBufferFromReplay` (Z. 341 ff.): dasselbe Rebuild-Muster (Reset + frischer Parser + Prefix), `GetReplayChunks()` (Z. 355).
- `CliRuntimeStatus`-Enum + `CliRuntimeStatusChangedEventArgs` + `CliRuntimeStatusEvaluator` am Dateiende (Z. 577 ff.).

## Einstiegspunkt / Dialog-Erzeugung

- `SettingsViewModel.KonsolenTestOeffnenCommand` (`SettingsViewModel.cs` Z. 248/298–306): `AsyncRelayCommand` → `GetRequiredService<KonsolenTestViewModel>()` → `IDialogService.ShowKonsolenTestDialogAsync`. Einstiegs-Button `KonsolenTestOeffnen` im Tab „Allgemein" der Einstellungen.
- `WpfDialogService.ShowKonsolenTestDialogAsync` (`WpfDialogService.cs` Z. 208–225): **nicht-modales** `KonsolenTestDialog`-Fenster (`Show()`, `Owner = MainWindow`); `AktivesDialogOwnerFenster()` (Z. 231–235) sorgt bei aus dem nicht-modalen Fenster geöffneten Dialogen für korrektes Ownership.
- `IDialogService.ShowOpenFileDialogAsync` (`IDialogService.cs` Z. 64–68): nativer `OpenFileDialog` (WpfDialogService Z. 190–205).

## `RelayCommand` / `AsyncRelayCommand` / `ViewModelBase`
Datei: `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs`

- `RelayCommand` (Z. 49–76): `Action` + optionale `Func<bool>`-CanExecute; `CanExecuteChanged` an `CommandManager.RequerySuggested` gekoppelt; statisches `RelayCommand.Refresh()` = `InvalidateRequerySuggested` — von `IstWiedergabeAktiv`-Setter genutzt.
- `AsyncRelayCommand` (Z. 106–175): `Func<CancellationToken, Task>` + `_isExecuting`-Guard; `ExecuteAsync` für direktes Awaiten in Tests.
- `ViewModelBase.SetProperty` (Z. 20–38): INotifyPropertyChanged, optionale `onChanged`-Aktion.

## `DispatcherInvokeFactory`
Datei: `src/Softwareschmiede.App/Services/DispatcherInvokeFactory.cs`

`Create(Action<Action>?)`: übergebener Hook (Tests: synchron `action => action()`) oder `Application.Current.Dispatcher`-Invoke — entkoppelt ViewModel vom UI-Thread.
