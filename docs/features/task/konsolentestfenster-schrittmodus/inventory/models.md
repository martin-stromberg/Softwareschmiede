# Datenmodell — Konsolentestfenster Schrittmodus

## `CliOutputAufzeichnung`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/CliOutputAufzeichnung.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `AufgabeId` | `Guid` (required, init) | ID der aufgezeichneten Aufgabe |
| `PluginName` | `string` (required, init) | Anzeigename des KI-Plugins |
| `StartUtc` | `DateTimeOffset` (required, init) | Aufzeichnungsbeginn (UTC), Anker der Chunk-Offsets |
| `Cols` | `int` (required, init) | Initiale Spaltenanzahl (beim Laden validiert `> 0`) |
| `Rows` | `int` (required, init) | Initiale Zeilenanzahl (beim Laden validiert `> 0`) |
| `IstVollstaendig` | `bool` (required, init) | `false`, wenn das Byte-Budget überschritten wurde (nur Präfix enthalten) |
| `EndeUtc` | `DateTimeOffset?` (init) | Aufzeichnungsende oder `null` |
| `Chunks` | `IReadOnlyList<CliOutputChunkRecord>` (required, init) | Aufgezeichnete Chunks in Eingangsreihenfolge — **die Sequenz, über die der Schrittmodus iteriert** |

## `CliOutputChunkRecord`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/CliOutputChunkRecord.cs`

Positional Record: `record CliOutputChunkRecord(TimeSpan Offset, byte[] Data)`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Offset` | `TimeSpan` | Zeitlicher Abstand zum Aufzeichnungsbeginn — Basis der Inter-Chunk-Pausen in `WiedergabeLoopAsync` (`realePause = chunk.Offset - vorherigerOffset`); im Schrittmodus ohne Wirkung |
| `Data` | `byte[]` | Unveränderte Rohbytes des Chunks — die Einheit, die ein Einzelschritt anwendet |

## `CliChunkAnzeigeEintrag`
Datei: `src/Softwareschmiede.App/ViewModels/CliChunkAnzeigeEintrag.cs`

Zeilenmodell der Quell-Chunk-Liste im Dialog (ein Eintrag pro aufgezeichnetem Chunk).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Index` | `int` (required, init) | 0-basierter Chunk-Index |
| `Offset` | `TimeSpan` (required, init) | Zeitoffset seit Aufzeichnungsbeginn |
| `Laenge` | `int` (required, init) | Länge in Bytes |
| `Quelltext` | `string` (required, init) | Quelltext mit sichtbar gemachten Steuersequenzen (ESC → ␛ usw.) |

`KonsolenTestViewModel.AktuellerQuellEintrag` zeigt auf `QuellEintraege[AktuellerChunkIndex - 1]` — für den Schrittmodus damit automatisch „der zuletzt angewendete Chunk".

## `TerminalBuffer`
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs`

| Member | Typ | Beschreibung / Zweck |
|--------|-----|----------------------|
| `TerminalBuffer(cols, rows)` | Ktor | Erzeugt Buffer mit gegebener Geometrie |
| `Apply(TerminalEvent)` | Methode | Wendet ein Parser-Ereignis auf den Buffer an (alle Zustandswechsel laufen ausschließlich hierüber — daher ist der Bufferzustand eine reine Funktion der Chunk-Sequenz) |
| `Reset()` | Methode | Setzt den Buffer vollständig zurück: leeres Grid, leerer Scrollback, Cursor an Home, SGR-Attribute auf Standard, volle Scroll-Region, Alternate Screen deaktiviert — Basis des Präfix-Rebuilds |
| `Resize(cols, rows)` | Methode | Größenanpassung unter Erhalt des Inhalts (Scrollback-Transfer beim Verkleinern) |
| `GetRow(rowIndex)` | Methode | Kopie der Zellen einer Zeile (Test-Nachweis des Bufferinhalts) |
| `GetSnapshot()` | Methode | `TerminalBufferSnapshot` mit Grid + Scrollback (Vergleichs-/Test-Nachweis) |
| `IsAlternateScreenActive`, `Rows`, `Cols`, `CursorRow`, `CursorCol` | Properties | Zustandsabfragen |

Wichtig: interner `_lock` in `TerminalBuffer` — `Apply`/`Reset` sind selbst gelockt; die Session-seitige Serialisierung läuft zusätzlich über `_renderLock` der Session.

## `TerminalBufferSnapshot`
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs` (Z. 581 ff.)

Record mit `Grid` (`TerminalCell[,]`), `ScrollbackRows`, `CursorRow`, `CursorCol` u. a.; `ScrollbackCount`/`TotalRows` abgeleitet. In `TerminalReplaySessionTests` genutzt (`BufferAlsText`), um Bufferzustände zu vergleichen — geeignetes Mittel, um „Zustand vor dem letzten Chunk" beim Rückwärtsschritt zu beweisen.

## `TerminalReplayBuffer`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplayBuffer.cs`

Budgetbegrenzter Rohchunk-Speicher der **Live**-Session (`PseudoConsoleSession._replayBuffer`):

| Member | Typ | Beschreibung / Zweck |
|--------|-----|----------------------|
| `TerminalReplayBuffer(byteBudget)` | Ktor | Byte-Budget; bei Überschreitung werden älteste Chunks verworfen |
| `Append(ReadOnlySpan<byte>)` | Methode | Hängt einen Rohchunk an |
| `GetChunks()` | Methode | `IReadOnlyList<byte[]>` der gehaltenen Chunks |

Nicht identisch mit `TerminalReplaySession._abgespielteChunks` (`List<byte[]>`, unbegrenzt, aus der Datei-Aufzeichnung). Für den Schrittmodus nur als Konzept-Referenz relevant.

## Event-Args (Schnittstellen-Ereignisse)
Dateien: `src/Softwareschmiede/Infrastructure/Terminal/`

| Typ | Inhalt | Relevanz |
|-----|--------|----------|
| `TerminalOutputChunkEventArgs` | `Data` (`ReadOnlyMemory<byte>` Rohbytes) | `OutputChunk`-Event — feuert pro angewendetem Chunk (Schleifenpfad Z. 269) |
| `TerminalSessionExitedEventArgs` | `ExitCode` (`int?`; bei Replay `null`) | `Exited`-Event am Ende der Wiedergabe-Schleife |
| `TerminalSessionFailedEventArgs` | Fehlerdetails | `Failed` — bei Replay nie ausgelöst |
| `CliRuntimeStatusChangedEventArgs` | `Status` (`CliRuntimeStatus`) | `RuntimeStatusChanged` — feuert bei `Inaktiv`→`Laeuft`→`Inaktiv` |
