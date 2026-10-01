# Datenmodell — Replay-Geometrie

## `CliOutputAufzeichnung`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/CliOutputAufzeichnung.cs` (31 Zeilen)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `AufgabeId` | `Guid` (`required init`) | ID der aufgezeichneten Aufgabe |
| `PluginName` | `string` (`required init`) | Anzeigename des aufgezeichneten KI-Plugins |
| `StartUtc` | `DateTimeOffset` (`required init`) | Aufzeichnungsbeginn, Anker der Chunk-Offsets |
| `Cols` | `int` (`required init`) | **Initiale Spaltenanzahl der aufgezeichneten Session** (Z. 16–17) — die für diese Anforderung maßgebliche Geometrie-Quelle; `> 0` durch Store-Validierung garantiert |
| `Rows` | `int` (`required init`) | **Initiale Zeilenanzahl der aufgezeichneten Session** (Z. 19–20) |
| `IstVollstaendig` | `bool` (`required init`) | `false` bei Budget-Überschreitung (nur Präfix vorhanden) → treibt `UnvollstaendigHinweis` im ViewModel |
| `EndeUtc` | `DateTimeOffset?` (`init`) | Aufzeichnungsende, `null` bei laufender Aufzeichnung |
| `Chunks` | `IReadOnlyList<CliOutputChunkRecord>` (`required init`) | Rohbyte-Chunks in Eingangsreihenfolge |

Befund: Resize-Ereignisse werden **nicht** aufgezeichnet — der Header trägt nur die **initiale** Geometrie (`TerminalSessionOptions.DefaultCols`/`DefaultRows` zum Session-Start, siehe `KiAusfuehrungsService` Z. 238–248). Keine Formatänderung nötig.

## `CliOutputChunkRecord`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/CliOutputChunkRecord.cs`

`sealed record CliOutputChunkRecord(TimeSpan Offset, byte[] Data)` — Rohbytes eines Chunks + Offset relativ zu `StartUtc`.

## `CliChunkAnzeigeEintrag`
Datei: `src/Softwareschmiede.App/ViewModels/CliChunkAnzeigeEintrag.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Index` | `int` (`required init`) | 1-basierte Chunk-Nummer (Spalte „#"), synchron zu `PositionsText` „Chunk n/y" |
| `Offset` | `TimeSpan` (`required init`) | Chunk-Offset seit Aufzeichnungsbeginn |
| `Laenge` | `int` (`required init`) | Chunk-Länge in Bytes |
| `Quelltext` | `string` (`required init`) | Quelltext mit sichtbaren Steuersequenzen (`CliChunkQuelltextFormatter`) |

## `TerminalBuffer`
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs` (594 Zeilen)

| Member | Sichtbarkeit / Typ | Beschreibung / Zweck |
|--------|--------------------|----------------------|
| `TerminalBuffer(cols, rows)` | public ctor | Legt `_grid`, `_cols`, `_rows` an (je `Math.Max(1, …)`), `_scrollBottom = _rows - 1` (Z. 34–41) |
| `Cols` / `Rows` | public `int` | Aktuelle Buffer-Geometrie (Z. 50–59) — **lesbar für das Control** |
| `CursorRow` / `CursorCol` | public `int` | Cursorposition (Z. 62–71) |
| `IsAlternateScreenActive` | public `bool` | Vollbild-TUI-Flag; klemmt vertikales Scrollen (Z. 44–47) |
| `ScrollbackCount` | internal `int` | Scrollback-Füllstand (nur Tests, Z. 74–77) |
| `Apply(TerminalEvent)` | public | Wendet Parser-Events an; Textschreiben bricht bei `CursorCol >= _cols` auf die nächste Zeile um (Z. 271–272) — **Umbruchsschwelle ist die Buffer-Geometrie** |
| `Reset()` | public | Setzt Inhalt/Cursor/SGR/Scroll-Region zurück, **behält `_cols`/`_rows` bei** (Z. 159–178) — ein vom Control verkleinerter Buffer bleibt nach Rebuild verkleinert |
| `Resize(cols, rows)` | public | Reallokiert das Grid mit Inhaltserhalt; bei Zeilenverkleinerung wandern abgeschnittene Zeilen in den Scrollback (außer Alt-Screen) (Z. 183–231). Laut Anforderung unverändert zu lassen — für Replay-Sessions soll er schlicht nicht mehr aufgerufen werden |
| `GetRow(int)` / `GetSnapshot()` | public | Zeilenkopie bzw. konsistenter Gesamt-Snapshot (Z. 236–248, 538–563) |
| `MaxScrollbackLines` | private const | 1000 Zeilen Scrollback-Kapazität (Z. 16) |

## `TerminalBufferSnapshot`
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs` (Z. 581–593)

`sealed record TerminalBufferSnapshot(TerminalCell[,] Grid, int Rows, int Cols, int CursorRow, int CursorCol, TerminalCell[][] ScrollbackRows)` — abgeleitet: `ScrollbackCount`, `TotalRows` (= Scrollback + Rows). Wird von `TerminalControl.OnRender`/`UpdateScrollInfo` gelesen.

## `TerminalSessionOptions`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionOptions.cs`

| Eigenschaft | Typ / Default | Beschreibung |
|-------------|---------------|--------------|
| `ReplayBufferByteBudget` | `int` = 512 KiB | Budget des Live-`TerminalReplayBuffer` |
| `DefaultCols` | `int` = **220** | Initiale Spaltenanzahl beim Session-Start — fließt in `PseudoConsoleSession.Buffer` (Z. 124) und als `Cols` in die Aufzeichnung |
| `DefaultRows` | `int` = **50** | Initiale Zeilenanzahl analog |
| `AufzeichnungByteBudget` | `int` = 8 MiB | Budget des `CliOutputRecorder`; `<= 0` deaktiviert den Mitschnitt |

Befund: Die 220×50-Defaults erklären die Geometrie der realen Referenz-Aufzeichnung (`cli-replay-a255276dd4e848039bab82ed51400729.clireplay`, Header verifiziert: 220×50, 486051 Chunks, `IstVollstaendig=false`, „Devin CLI").
