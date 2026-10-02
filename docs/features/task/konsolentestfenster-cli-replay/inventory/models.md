# Datenmodell — Konsolentestfenster für CLI-Ausgabe-Replay

Datenmodellklassen und Records, die für die Anforderung (Rohbyte-Mitschnitt mit Zeitstempeln, Dateiformat, Replay-Session, Vergleichsansicht) relevant sind. Es existiert **noch kein** Datenmodell für einen zeitgestempelten Chunk-Mitschnitt — die nächstliegenden vorhandenen Strukturen sind unten dokumentiert.

## `TerminalCell`
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalCell.cs`

`record struct`; eine Zelle des Terminal-Grids.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Character` | `char` | Angezeigtes Zeichen |
| `Foreground` | `Color` (`System.Drawing`) | Vordergrundfarbe |
| `Background` | `Color` | Hintergrundfarbe |
| `Bold` | `bool` | Fettdarstellung |
| `Underline` | `bool` | Unterstrichen |
| `Dim` | `bool` | Gedimmt |
| `Default` | `static TerminalCell` | Standardzelle (Leerzeichen, Hellgrau/Schwarz) |

## `TerminalEvent`-Hierarchie
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalEvents.cs`

Abstrakter Basistyp `TerminalEvent` (`abstract record`, Zeile 6). Konkrete Ereignisse — Ergebnis von `AnsiSequenceParser.Parse`, werden via `TerminalBuffer.Apply` angewendet:

| Record | Parameter | Zweck |
|--------|-----------|-------|
| `TextWrittenEvent` | `string Text` | Dekodierter Textlauf |
| `CursorMovedEvent` | `int Row, int Col, bool IsAbsolute` | Absolute Cursorposition (`-1` = unverändert) |
| `CursorMovedRelativeEvent` | `int DeltaRow, int DeltaCol` | Relative Cursorbewegung |
| `ColorChangedEvent` | `Color? Foreground/Background, bool? Bold/Dim/Underline, bool Reset` | SGR-Attributwechsel |
| `ScreenClearedEvent` | `int Mode` | CSI J (0/1/2/3) |
| `LineErasedEvent` | `int Mode` | CSI K |
| `CursorVisibilityChangedEvent` | `bool Visible` | `?25h/l` |
| `AlternateScreenChangedEvent` | `bool Enabled` | `?47/1047/1049h/l` |
| `LinesInsertedEvent` / `LinesDeletedEvent` | `int Count` | CSI L / M |
| `CharsInsertedEvent` / `CharsDeletedEvent` / `CharsErasedEvent` | `int Count` | CSI @ / P / X |
| `ScrollRegionChangedEvent` | `int Top, int Bottom` | CSI r |
| `ScreenScrolledEvent` | `int DeltaRows` | CSI S / T |
| `CursorSavedEvent` | `bool Restored` | ESC 7/8, CSI s/u, `?1048` |
| `TerminalResetEvent` | — | ESC c |

## `TerminalBuffer` / `TerminalBufferSnapshot`
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs`

Zustandsbehafteter 2D-Grid-Buffer mit Scrollback (max. 1000 Zeilen, `MaxScrollbackLines`, Zeile 16), Alternate-Screen-Unterstützung (`_mainScreenGrid`), Scroll-Region und SGR-Zustand. Alle öffentlichen Zugriffe laufen unter `_lock`.

| Member | Typ | Beschreibung |
|--------|-----|--------------|
| `TerminalBuffer(int cols, int rows)` | ctor (Z. 34) | Erzeugt Grid, `ScrollBottom = rows-1` |
| `IsAlternateScreenActive` | `bool` Property (Z. 44) | Alternate Screen aktiv |
| `Rows` / `Cols` | `int` Property (Z. 50/56) | Aktuelle Dimension |
| `CursorRow` / `CursorCol` | `int` Property (Z. 62/68) | Cursorposition |
| `ScrollbackCount` | `int` (internal, Z. 74) | Scrollback-Zeilen |
| `Apply(TerminalEvent)` | `void` (Z. 81) | Wendet ein Terminalereignis an |
| `Reset()` | `void` (Z. 159) | Buffer zurücksetzen |
| `Resize(int cols, int rows)` | `void` (Z. 183) | Buffer-Geometrie ändern |
| `GetRow(int rowIndex)` | `TerminalCell[]` (Z. 236) | Zeilenkopie |
| `GetSnapshot()` | `TerminalBufferSnapshot` (Z. 538) | Konsistenter Gesamtsnapshot unter einem Lock |

`TerminalBufferSnapshot` (Z. 581): `record(TerminalCell[,] Grid, int Rows, int Cols, int CursorRow, int CursorCol, TerminalCell[][] ScrollbackRows)` mit `ScrollbackCount`/`TotalRows`.

## `TerminalSessionOptions`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionOptions.cs`

Konfigurationssektion `"Terminal"` (`SectionName`, Z. 7); gebunden in `App.xaml.cs` Z. 233.

| Eigenschaft | Typ | Default | Zweck |
|-------------|-----|---------|-------|
| `ReplayBufferByteBudget` | `int` | `512 * 1024` | Byte-Budget des `TerminalReplayBuffer` pro Session |
| `DefaultCols` | `int` | `220` | Initiale Spaltenanzahl |
| `DefaultRows` | `int` | `50` | Initiale Zeilenanzahl |

## `TerminalReplayBuffer`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplayBuffer.cs`

`sealed class`; Budget-begrenzter Ringpuffer für **rohe Ausgabe-Bytes ohne Zeitstempel** — dient heute nur dem `RebuildBufferFromReplay`-Neuaufbau bei Control-Neuanbindung, nicht einem zeitgesteuerten Replay. Für den geforderten Mitschnitt fehlt: Zeitstempel pro Chunk, unbegrenzte/vollständige Aufzeichnung, Persistenz.

| Member | Typ | Beschreibung |
|--------|-----|--------------|
| `TerminalReplayBuffer(int byteBudget)` | ctor (Z. 16) | Wirft bei `byteBudget <= 0` |
| `Append(ReadOnlySpan<byte> chunk)` | `void` (Z. 26) | Kopiert Chunk; bei Budget-Überschreitung werden die ältesten Chunks verworfen; leere Chunks ignoriert |
| `GetChunks()` | `IReadOnlyList<byte[]>` (Z. 43) | Snapshot in Eingangsreihenfolge |

## `TerminalSessionStartSpec`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/TerminalSessionStartSpec.cs`

`sealed record`; Startbeschreibung, die Plugins über `IKiPlugin.GetTerminalStartSpecAsync` liefern.

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `FileName` | `string` | Executable-Name/-Pfad |
| `Arguments` | `string` | Startargumente |
| `WorkingDirectory` | `string` | Arbeitsverzeichnis |
| `EnvironmentVariables` | `IReadOnlyDictionary<string,string?>` | Zusätzliche Umgebungsvariablen |
| `Capabilities` | `TerminalProviderCapabilities` | PTY-Fähigkeiten (Default `SupportsPty`) |
| `PluginName` | `string` | Anzeigename des Plugins |
| `OptionalParameters` | `string?` | Übergebene optionale Parameter |

## `TerminalSessionStartResult`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionStartResult.cs`

`sealed record(Process Process, ITerminalSession Session, bool IsPseudoTerminal)` — Rückgabe von `ITerminalSessionFactory.StartAsync` und `IPseudoConsoleProcessLauncher.Start`.

## `CliProcessHandle`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (ab Z. 638)

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `AufgabeId` | `Guid` | Zugehörige Aufgabe |
| `Process` | `Process` | Verwalteter Prozess |
| `LastHeartbeat` | `DateTimeOffset` | Letzter Heartbeat |
| `AbsichtlichGestoppt` | `bool` | Durch `StopCliAsync` beendet |
| `Session` | `ITerminalSession?` | Terminal-Session (null bei klassischem Start) |
| `OutputSink` | `ITerminalOutputSink?` | Senke, die beim Aufräumen via `CompleteAsync` abgeschlossen wird (Z. 631 f.) |

## `Protokolleintrag`
Datei: `src/Softwareschmiede/Domain/Entities/Protokolleintrag.cs`

Persistierter Protokolleintrag — Basis des bestehenden `.raw`-Exports (zeilennormalisiert, `ProtokollTyp.CliOutput`).

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `Id` | `Guid` | Eindeutige ID |
| `AufgabeId` | `Guid` | Zugehörige Aufgabe |
| `Typ` | `ProtokollTyp` | Eintragstyp (`CliOutput` für CLI-Zeilen) |
| `Inhalt` | `string` | Zeileninhalt (bereits zeilennormalisiert — `\r`-Semantik/Chunk-Grenzen gehen verloren) |
| `AgentName` | `string?` | Beteiligter Agent |
| `Zeitstempel` | `DateTimeOffset` | Zeitpunkt des Eintrags |
| `Aufgabe` | `Aufgabe` | Navigation |
| `TestErgebnisse` | `List<TestErgebnis>` | Bei Typ `TestErgebnis` |
| `DiffResult` | `DiffResult?` | Optionales Diff |

## EventArgs-Records

| Typ | Datei | Inhalt |
|-----|-------|--------|
| `TerminalOutputChunkEventArgs` | `src/Softwareschmiede/Infrastructure/Terminal/TerminalOutputChunkEventArgs.cs` | `ReadOnlyMemory<byte> Data` — roher Chunk vor dem Parsen |
| `TerminalSessionExitedEventArgs` | `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionExitedEventArgs.cs` | `int? ExitCode` |
| `TerminalSessionFailedEventArgs` | `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionFailedEventArgs.cs` | `Exception Error`, `string Phase` |
| `CliRuntimeStatusChangedEventArgs` | `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` (Z. 589) | `CliRuntimeStatus Status` |
