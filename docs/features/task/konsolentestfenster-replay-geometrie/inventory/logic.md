# Logik — Replay-Geometrie

## `TerminalControl` — Kern der Anforderung
Datei: `src/Softwareschmiede.App/Controls/TerminalControl.cs` (535 Zeilen)

`sealed`-Klasse, `FrameworkElement` + `IScrollInfo`. Reiner Renderer: abonniert `ITerminalSession.BufferChanged`, rendert `session.Buffer` per `GetSnapshot()` + `FormattedText`. Wird in **zwei** Views verwendet: `KonsolenTestDialog.xaml` (`ReplayTerminal`) und `TaskDetailView.xaml` (`TerminalConsole` — Live-Pfad, darf nicht beeinträchtigt werden).

### Geometrie-relevante Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Session` (DependencyProperty) | public | `ITerminalSession`-Bindung, PropertyChanged-Callback `OnSessionChanged` (Z. 41–52) |
| `OnSessionChanged(session)` | private | Alte Session abmelden → `session.BufferChanged += OnBufferChanged` → **`session.RebuildBufferFromReplay()`** (Z. 124) → `_buffer = session.Buffer` → **`_buffer.Resize(cols, rows)`** (Z. 126, mit `CalculateCols()`/`CalculateRows()` aus `ActualWidth`/`ActualHeight`) → `UpdateScrollInfo` → `InvalidateVisual`. Bei `session == null`: State-Reset (Z. 101–111). **Erzwingt Control-Geometrie beim Binden — Haupt-Eingriffspunkt der Anforderung** |
| `OnRenderSizeChanged(sizeInfo)` | protected override | `buffer.Resize(cols, rows)` **und** `session.Resize(cols, rows)` bei jeder Control-Größenänderung (Z. 301–317) — **zweiter Eingriffspunkt** |
| `CalculateCols()` | private | `ActualWidth > 0 ? ActualWidth : 220 * 8` → `Max(1, w / _cellWidth)` (Z. 337–341). Der 220×8-Fallback greift, solange das Control noch nicht arrangiert ist (z. B. bei `MeasureOverride` mit unendlicher Breite oder beim Binden vor dem ersten Layout) |
| `CalculateRows()` | private | Analog: `ActualHeight > 0 ? ActualHeight : 50 * 16` → `Max(1, h / _cellHeight)` (Z. 343–347) |
| `MeasureCellSize()` | private | Misst „W" in Consolas 13 per `FormattedText` → `_cellWidth`/`_cellHeight` (Fallbacks 8/16) (Z. 319–335) |
| `MeasureOverride(availableSize)` | protected override | Bei `double.IsInfinity(availableSize.Width)` (horizontal scrollender ScrollViewer!) → `CalculateCols() * _cellWidth` — kommt aktuell aus `ActualWidth`-Fallback, **nicht aus dem Buffer** (Z. 350–356) |
| `ArrangeOverride(finalSize)` | protected override | `base` + `UpdateScrollInfo(true)` (Z. 359–364) |
| `OnRender(dc)` | protected override | Schwarzer Hintergrund `ActualWidth`×`ActualHeight` (Z. 154); Snapshot-basiertes Zeichnen: Hintergrund-Farb-Runs `bgStart * _cellWidth` (Z. 187), Glyphen `c * _cellWidth` (Z. 210), Cursor `cursorCol * _cellWidth` (Z. 218–220). Iteriert `visibleRows` × alle `snapshot.Cols` — **kein horizontaler Offset, kein Spalten-Clipping**; `visibleStart` (Zeilen) aus `_isFollowingEnd`/`_verticalOffset` (Z. 167–169) |
| `OnBufferChanged` | private | Dispatcher-`UpdateScrollInfo(true)` + `InvalidateVisual` (Z. 134–141) |
| `OnCreateAutomationPeer` | protected override | `FrameworkElementAutomationPeer` — macht `AutomationProperties.Name`/`HelpText` UIA-lesbar (Z. 143–149); **relevant für E2E-Beobachtbarkeit** |

### `IScrollInfo`-Stand

| Member | Zeile | Stand |
|--------|-------|-------|
| `CanVerticallyScroll` | 55 | `{ get; set; } = true` |
| `CanHorizontallyScroll` | 58 | `{ get; set; }` — default `false`, wird vom ScrollViewer gesetzt |
| `ExtentWidth` | 61 | `=> ViewportWidth` — **Stub** (kein Extent > Viewport möglich → ScrollViewer blendet die HScrollBar immer aus) |
| `ExtentHeight` | 64 | `=> _extentHeight` (echt) |
| `ViewportWidth` | 67 | `=> Math.Max(0, ActualWidth)` |
| `ViewportHeight` | 70 | `=> _viewportHeight` |
| `HorizontalOffset` | 73 | `=> 0` — **Stub** |
| `VerticalOffset` | 76 | `=> _verticalOffset` |
| `ScrollOwner` | 79 | `{ get; set; }` — vom `ScrollViewer` bei `CanContentScroll="True"` gesetzt |
| `LineUp`/`LineDown` | 367–370 | `SetVerticalOffset(±1)` |
| `LineLeft`/`LineRight` | 373–380 | **leer** |
| `PageUp`/`PageDown` | 383–386 | `SetVerticalOffset(±GetPageScrollRows())` |
| `PageLeft`/`PageRight` | 389–396 | **leer** |
| `MouseWheelUp`/`MouseWheelDown` | 399–402 | `SetVerticalOffset(±3)` (`MouseWheelScrollLines`) |
| `MouseWheelLeft`/`MouseWheelRight` | 405–412 | **leer** |
| `SetHorizontalOffset` | 415–417 | **leer** |
| `SetVerticalOffset` | 420–437 | echt: Alt-Screen-Klemmung auf 0, `ClampOffset` auf `ScrollableHeight`, `_isFollowingEnd`-Pflege, `InvalidateScrollInfo` + `InvalidateVisual` |
| `MakeVisible` | 440 | `=> rectangle` (no-op) |
| `UpdateScrollInfo(followEndIfNeeded)` | 446–466 | berechnet `_viewportHeight`/`_extentHeight` aus `snapshot.TotalRows` (Alt-Screen: nur Grid), klemmt `_verticalOffset`, pflegt `_isFollowingEnd`, ruft `ScrollOwner?.InvalidateScrollInfo()` — **vertikales Vorbild** |
| `ScrollableHeight` | 442 | `Max(0, _extentHeight - _viewportHeight)` |
| `ClampOffset` | 482–487 | NaN/negativ → 0, sonst auf `max` geklemmt |

**Einheiten-Asymmetrie (verifiziert, relevant für die Planung):** Vertikal sind Extent/Viewport/Offsets in **Zeilen** (`ExtentHeight` in Zeilen, `SetVerticalOffset` in Zeilen); `ViewportWidth`/`ActualWidth` sind dagegen **Pixel**. Für horizontales Scrollen muss `ExtentWidth` in Pixeln aus `buffer.Cols * _cellWidth` kommen — `IScrollInfo` verlangt konsistente Einheiten pro Achse (der ScrollViewer rechnet Extent↔Viewport in derselben Einheit; gemischte Zeilen-/Pixelmaße über Achsen hinweg sind zulässig, da `LineLeft` etc. vom Control selbst interpretiert werden).

### Weitere Member (ungekürzt geprüft)

- `OnPreviewKeyDown`/`OnTextInput`/`OnMouseDown` (Z. 243–298): Tastatur→`KeyToVt100Encoder`→`session.WriteInputAsync`; Ctrl+V→Clipboard-Paste-Pfad (`ReadClipboardAndInsertAsync`, `GetClipboardText`, `WriteToInputStreamAsync` Z. 493–534).
- `_isFollowingEnd` (Z. 33): Follow-End-Flag für vertikales Auto-Scrollen.
- `_brushCache`/`BlackBrush`/`CursorBrush`: Brush-Verwaltung (Z. 35–37, 224–240).

## `TerminalReplaySession` — fixe Geometrie-Quelle
Datei: `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs` (575 Zeilen)

`sealed`-Implementierung von `ITerminalSession` über einer `CliOutputAufzeichnung`. Klassendoku (Z. 8–14) beschreibt explizit das heutige Verhalten: *„Die im Header der Aufzeichnung gespeicherte Geometrie dient nur der initialen Buffer-Größe — die Wiedergabe nutzt die aktuelle Control-Geometrie (`TerminalControl` resized den Buffer beim Binden); Resize-Ereignisse werden nicht aufgezeichnet und nicht reproduziert."* — **bei Umsetzung zu aktualisieren.**

### Geometrie-relevante Befunde

| Member | Zeile | Befund |
|--------|-------|--------|
| `Buffer` | 84 (`get`), init Z. 67 | `new TerminalBuffer(Math.Max(1, aufzeichnung.Cols), Math.Max(1, aufzeichnung.Rows))` — **Aufzeichnungs-Geometrie bereits beim Erzeugen** |
| `Resize(int, int)` | 314 | `=> true` — No-Op-Stub, ändert den Buffer nicht (das Control resized `session.Buffer` selbst!) |
| `BaueBufferUndParserAusPraefixNeuAuf()` | 494–501 | `Buffer.Reset()` + `_parser.Reset()` + Re-Parse aller `_abgespielteChunks` — `Buffer.Reset()` **behält die aktuellen (ggf. vom Control verkleinerten) Dimensionen**: ein einmal verkleinerter Buffer bleibt nach Rebuild/`SchrittZurueck` falsch |
| `RebuildBufferFromReplay()` | 338–344 | public, unter `_renderLock`; wird von `TerminalControl.OnSessionChanged` aufgerufen (Z. 124) |

Kein öffentliches Geometrie-Member über `Buffer.Cols`/`Buffer.Rows` hinaus; keine Kennzeichnung „fixe Geometrie".

### Schrittmodus-/Wiedergabe-Stand (aus Vorgänger-Inventory übernommen, am Code verifiziert)

- `WiedergabeStarten()` (Z. 140–216): CAS auf `_wiedergabeLoopAktiv`; CAS-Fail-Pfad entscheidet unter `_renderLock` zwischen „gesunde Wiedergabe" / „Termination widerrufen" / „Neustart an sterbende Schleife hängen" (`_schleifeBeendenAngefordert`/`_schleifeBeendenKonsumiert`, HEAD-Commit `6d43f59`).
- `SchrittVor()` (Z. 253–285): wendet `Chunks[_abgespielteChunks.Count]` unter `_renderLock` an (`WendeChunkAnUnterLock`), feuert `BufferChanged` außerhalb des Locks; am Ende `_schleifeBeendenAngefordert = true` + `RaiseExited(nurAmEnde: true)` + `Fortsetzen()`.
- `SchrittZurueck()` (Z. 292–311): `RemoveAt` auf `_abgespielteChunks`, `_exitedSignaled`-Reset, `BaueBufferUndParserAusPraefixNeuAuf()`, `BufferChanged`.
- `WiedergabeLoopAsync` (Z. 374–476): Position = `_abgespielteChunks.Count` (kein lokaler Index mehr — seit Schrittmodus-Feature); Pause-Gate + Zeitraffer-`Task.Delay(delay, _timeProvider, ct)` + Positions-/Beenden-Re-Check unter `_renderLock`.
- `WendeChunkAnUnterLock(position)` (Z. 481–489): `OutputChunk` → `_abgespielteChunks.Add` → `_parser.Parse` + `Buffer.Apply` → `_aktuellerChunkIndex`-Write.
- `RaiseExited(nurAmEnde)` (Z. 530–558): Flanken-Ereignis, CAS `_exitedSignaled`, `RuntimeStatus → Inaktiv`.
- Properties: `AktuellerChunkIndex` (Z. 102), `IstPausiert` (Z. 105), `ZeitrafferSchwelle` (Z. 110–114), `RuntimeStatus` (Z. 87–90, nur `Inaktiv`↔`Laeuft`), `IsPseudoTerminal => false` (Z. 93), `ExitCode`/`Failure` (Z. 96/99).
- Stubs: `InputStream`/`OutputStream` = `Stream.Null` (Z. 71/74), `Process` = nicht gestarteter Stub (Z. 81), `WriteInputAsync`/`WritePromptAsync`/`MarkInputActivity`/`MarkOutputActivity`/`DrainOutputAsync` (Z. 317–334).
- `Dispose()` (Z. 347–369): CAS `_disposed`, CTS-Cancel + `Fortsetzen`, kein synchrones Warten.

## `PseudoConsoleSession` — Referenzpfad (unverändert zu lassen)
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` (634 Zeilen)

- `Buffer = new TerminalBuffer(effectiveOptions.DefaultCols, effectiveOptions.DefaultRows)` (Z. 124) — Live-Sessions starten in Options-Geometrie (220×50 default) und folgen danach Control-Resizes.
- `Resize(cols, rows)` (Z. 167–188): `false` bei `cols <= 0 || rows <= 0` und bei fehlgeschlagenem PTY-Call; Deduplizierung identischer Aufrufe unter `_resizeLock`. **`false` ist kein „fixiert"-Signal** (Semantik-Kollision aus der Anforderung verifiziert).
- `ReadLoopAsync` (Z. 275–336): `_replayBuffer.Append` + `Parse`/`Apply` unter `_renderLock`, `BufferChanged`.
- `RebuildBufferFromReplay()` (Z. 341–351): `Buffer.Reset()` + frischer lokaler `AnsiSequenceParser` + Re-Parse aus `TerminalReplayBuffer`.
- `CliRuntimeStatus`-Enum + `CliRuntimeStatusChangedEventArgs` + `CliRuntimeStatusEvaluator` am Dateiende (Z. 577 ff.).

## `KonsolenTestViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs` (468 Zeilen)

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `Session` | public `ITerminalSession?` | An `TerminalControl.Session` gebunden; gesetzt in `LadeAufzeichnung` (Z. 255) und `ErsetzeReplaySessionDurchFrische` (Z. 345), `null` in `EntsorgeReplaySession` (Z. 303) — **einziger Eintragspunkt der Replay-Session ins Control** |
| Commands | public `ICommand` | `AufzeichnungOeffnen` (AsyncRelayCommand → `OeffneAufzeichnungAsync` Z. 201–229), `WiedergabeStarten` (Z. 306–321), `WiedergabeNeustarten` (Z. 323–337), `WiedergabePausieren` (Z. 349–366), `SchrittVor`/`SchrittZurueck` (Z. 368–401, inkl. interner Guards), `Schliessen` (Z. 403–407) |
| `LadeAufzeichnung` | private | Store-Validierung stellt `Cols`/`Rows > 0` sicher (Kommentar Z. 235–236); erzeugt `TerminalReplaySession` via `ErzeugeReplaySession`, setzt `Session` (Z. 231–256) |
| `ErzeugeReplaySession` | private | `new TerminalReplaySession(aufzeichnung, _timeProvider)` + `ZeitrafferSchwelle`-Übernahme + `BufferChanged`/`Exited`-Abos (Z. 277–287) |
| `ErsetzeReplaySessionDurchFrische` | private | Entsorgen + neu erzeugen + `Session = _replaySession` → Rebind triggert `OnSessionChanged` (Z. 339–347) |
| `OnReplayBufferChanged` / `OnReplayExited` | private | Sender-Identitätsprüfung + `_dispatcherInvoke`; Positions-/Status-Texte, Quell-Selektion, `RelayCommand.Refresh()` (Z. 409–447) |
| Anzeige-Properties | public | `QuellEintraege`, `AktuellerQuellEintrag`, `DateiPfad`, `StatusText`, `PositionsText`, `IstWiedergabeAktiv`, `IstPausiert`, `ZeitrafferSchwelleText`, `FehlerMeldung`, `UnvollstaendigHinweis` — **kein Geometrie-Hinweis vorhanden** |

Abonnierte Events: `TerminalReplaySession.BufferChanged`, `.Exited`. Publizierte Events: `CloseRequested` (`EventHandler<bool>`).

## `KonsolenTestDialog`
Dateien: `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml` (175 Zeilen) + `.xaml.cs` (34 Zeilen)

- Nicht-modales `Window`, `Width="1200" Height="720"`, `Closed` dispost das ViewModel (xaml.cs Z. 20–24); `OnQuellListeSelectionChanged` → `ScrollIntoView` (Z. 29–33).
- **Zentral:** `ScrollViewer` um `ReplayTerminal` (Z. 136–143): `VerticalScrollBarVisibility="Auto"`, **`HorizontalScrollBarVisibility="Disabled"`**, `CanContentScroll="True"`. `ReplayTerminal` = `TerminalControl` mit `Session="{Binding Session}"` und `AutomationProperties.Name="ReplayTerminal"` — der ScrollViewer selbst hat **keinen** `x:Name`/`AutomationProperties.Name` (E2E-Adressierung müsste über Klassenname/Tree-Reihenfolge laufen oder ergänzt werden).
- Werkzeugleiste (WrapPanel, Z. 26–97) mit `AutomationProperties.Name` auf allen Elementen; `WiedergabeStatus`/`WiedergabePosition` tragen dynamischen Text über `HelpText` (E2E-Lesepfad).
- Spaltenaufteilung Terminal↔Quell-Liste: `2*` / `5` (GridSplitter) / `*` (Z. 130–134).

## `TaskDetailView` — Live-Pfad (darf nicht beeinträchtigt werden)
Dateien: `src/Softwareschmiede.App/Views/TaskDetailView.xaml` + `.xaml.cs`

- `TerminalScrollViewer` (xaml Z. 487–496): `VerticalScrollBarVisibility="Auto"`, `HorizontalScrollBarVisibility="Disabled"`, `CanContentScroll="True"`, enthält `TerminalConsole` = `TerminalControl`; `PreviewMouseDown` → Fokus-Helfer.
- `xaml.cs`: Session-Bindung nicht via XAML-Binding, sondern Code — `TerminalSessionGestartet`-Event des ViewModels → `SetTerminalSession` setzt `TerminalConsole.Session` (Z. 53–91); Fokus-Handling `FocusTerminalConsole` (Z. 109 ff.). Der Live-Pfad bindet `PseudoConsoleSession` — Resize-auf-Control bleibt dort korrekt.

## `CliReplayAufzeichnungStore`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/CliReplayAufzeichnungStore.cs` (177 Zeilen)

- `.clireplay`-Binärformat: Magic `SWCLRPLY`, Version 1, Header (AufgabeId, StartUtc, EndeUtc, **Cols, Rows**, IstVollstaendig, PluginName) + `[Int64 OffsetTicks][Int32 Length][Bytes]`-Records (Z. 17–52).
- `LadeAsync` validiert streng: Magic/Version, **`cols <= 0 || rows <= 0` → `InvalidDataException`** (Z. 131–132), Längen gegen Restlänge (Z. 133–160). Damit ist die Header-Geometrie für jede erfolgreich geladene Datei positiv.
- DI: `AddSingleton<CliReplayAufzeichnungStore>` (App-Startup).

## `CliOutputRecorder`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/CliOutputRecorder.cs`

`ITerminalOutputSink`: zeichnet Rohbytes chunkweise mit `TimeProvider`-Offsets auf; `_cols`/`_rows` kommen per Konstruktor-Parameter (Z. 33–47) aus `TerminalSessionOptions.DefaultCols`/`DefaultRows` (Aufrufer `KiAusfuehrungsService` Z. 240–247); `GetAufzeichnung()` setzt `Cols = _cols, Rows = _rows` (Z. 107–108). Bei Budget-Überschreitung: `IstVollstaendig = false`, intaktes Präfix bleibt.

## `AnsiSequenceParser`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs` (459 Zeilen)

- `Parse(ReadOnlySpan<byte>)`: zustandsbehafteter VT100/ANSI-Parser → `IEnumerable<TerminalEvent>` (chunk-übergreifender Zustand: `_state`, `_paramBuffer`, `_textBuffer`, `_utf8Decoder`).
- `Reset()` (Z. 182–188): vollständiger Zustands-Reset — vom Replay-Präfix-Rebuild genutzt.
- Geometrie-agnostisch: der Parser kennt keine Cols/Rows — Umbruch/Cursor-Klemmung entsteht erst im `TerminalBuffer` (`ApplyText` Z. 271, `CursorMoved`-Clamp Z. 93–104). **Das Layout der Wiedergabe ist damit vollständig eine Funktion der Buffer-Geometrie** — die Ursache des Divergenz-Problems liegt ausschließlich im Control-seitigen `Resize`.

## Einstiegspunkt / Dialog-Erzeugung

- `SettingsViewModel.KonsolenTestOeffnenCommand` (Z. 248, Initialisierung Z. 298, `KonsolenTestOeffnenAsync` Z. 301–306): `AsyncRelayCommand` → `ShowKonsolenTestDialogAsync`.
- `WpfDialogService.ShowKonsolenTestDialogAsync` (Z. 208–225): nicht-modales `KonsolenTestDialog`-Fenster (`Show()`, `Owner = MainWindow`).
- `IDialogService.ShowOpenFileDialogAsync` (`IDialogService.cs` Z. 64–68) → `WpfDialogService` Z. 181 ff. (nativer `OpenFileDialog`).

## `RelayCommand` / `AsyncRelayCommand` / `ViewModelBase` / `DispatcherInvokeFactory`

- `RelayCommand` (`ViewModelBase.cs` Z. 49–76): `CanExecuteChanged` an `CommandManager.RequerySuggested`; `RelayCommand.Refresh()` (Z. 75) = manueller Requery-Impuls.
- `AsyncRelayCommand` (Z. 106–175): `Func<CancellationToken, Task>` + `_isExecuting`-Guard.
- `DispatcherInvokeFactory.Create(Action<Action>?)` (`src/Softwareschmiede.App/Services/DispatcherInvokeFactory.cs`): injizierbarer Dispatcher-Hook (Tests: synchron).
