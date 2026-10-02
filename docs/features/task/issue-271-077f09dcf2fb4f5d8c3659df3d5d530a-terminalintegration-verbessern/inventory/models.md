# Bestandsaufnahme: Datenmodell (Terminalintegration)

Datenklassen, Records und Structs im Bereich Terminal/PTY/CLI-Ausführung.

## `TerminalCell`
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalCell.cs`

Record-Struct für eine einzelne Terminal-Zelle; `TerminalCell.Default` liefert Leerzeichen mit hellgrauer Vordergrund-/schwarzer Hintergrundfarbe.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Character` | `char` | Das angezeigte Zeichen |
| `Foreground` | `Color` | Vordergrundfarbe (System.Drawing) |
| `Background` | `Color` | Hintergrundfarbe |
| `Bold` | `bool` | Fettdarstellung |
| `Underline` | `bool` | Unterstrichen |
| `Dim` | `bool` | Gedimmt |
| `Default` | `static TerminalCell` | Standardzelle (Leerzeichen, `Color.FromArgb(229,229,229)` auf Schwarz) |

## `TerminalEvent`-Hierarchie
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalEvents.cs`

Abstrakter Basistyp `TerminalEvent` (record) plus abgeleitete Records — Ergebnistypen des `AnsiSequenceParser`, werden von `TerminalBuffer.Apply` konsumiert:

| Record | Parameter | Beschreibung / Zweck |
|--------|-----------|----------------------|
| `TextWrittenEvent` | `string Text` | Klartext für den Buffer (bereits UTF-8-dekodiert) |
| `CursorMovedEvent` | `int Row, int Col, bool IsAbsolute` | Absolute/relative Cursor-Positionierung (aus CSI H/f) |
| `CursorMovedRelativeEvent` | `int DeltaRow, int DeltaCol` | Relative Cursor-Bewegung (aus CSI A/B/C/D) |
| `ColorChangedEvent` | `Color? Foreground, Color? Background, bool? Bold, bool? Dim, bool? Underline, bool Reset` | SGR-Attributänderung; `null` = unverändert |
| `ScreenClearedEvent` | `int Mode` | CSI J: 0 = Cursor bis Ende, 1 = Anfang bis Cursor, 2 = alles (inkl. Scrollback-Löschung) |
| `LineErasedEvent` | `int Mode` | CSI K: 0 = Cursor bis Zeilenende, 1 = Anfang bis Cursor, 2 = ganze Zeile |
| `CursorVisibilityChangedEvent` | `bool Visible` | CSI ?25 h/l |

Nicht abgebildet (Parser erzeugt dafür keine Events): Alternate Screen (`?1049h/l`), Insert/Delete Line/Char, Scroll-Regionen (DECSTBM), weitere `?`-Modi außer `?25`, OSC-Inhalte (werden nur bis zum Terminator überlesen), ESC-Sequenzen außer `ESC[`/`ESC]`.

## `TerminalBufferSnapshot`
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs` (Zeilen 359–372)

Record, erzeugt von `TerminalBuffer.GetSnapshot()` unter einem Lock — konsistenter Lesezustand für den Renderer (`TerminalControl.OnRender`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Grid` | `TerminalCell[,]` | Kopie des Zellen-Grids |
| `Rows` | `int` | Zeilenanzahl |
| `Cols` | `int` | Spaltenanzahl |
| `CursorRow` | `int` | Cursor-Zeile |
| `CursorCol` | `int` | Cursor-Spalte |
| `ScrollbackRows` | `TerminalCell[][]` | Scrollback-Zeilen (älteste zuerst) |
| `ScrollbackCount` | `int` | Anzahl Scrollback-Zeilen (abgeleitet) |
| `TotalRows` | `int` | Scrollback + sichtbares Grid (abgeleitet) |

## `CliProcessHandle`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (Zeilen 675–728)

Handle auf einen laufenden CLI-Prozess; wird von `KiAusfuehrungsService` pro Aufgabe in `_handles` (ConcurrentDictionary) verwaltet.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `AufgabeId` | `Guid` | Zugehörige Aufgabe |
| `Process` | `Process` | Verwalteter Prozess (bei ConPTY aus `Process.GetProcessById`) |
| `LastHeartbeat` | `DateTimeOffset` | Letzter Heartbeat (setzbar) |
| `AbsichtlichGestoppt` | `bool` (volatile) | Markierung für `StopCliAsync` — beeinflusst Exit-Status-Mapping |
| `PseudoConsoleSession` | `PseudoConsoleSession?` | Session-Referenz, nur beim ConPTY-/Simulationspfad gesetzt |
| `NativeProcessHandle` | `IntPtr` | Natives Win32-Prozess-Handle aus `CreateProcess` (nur ConPTY); bleibt offen für `GetExitCodeProcess`, wird in `CancelAndDisposeConPtyResourcesAsync` geschlossen |
| `SendCts` | `CancellationTokenSource?` | Storniert den verzögerten Plugin-Befehlsversand (`SendCommandDelayedAsync`) beim Prozessende |
| `OutputSink` | `ITerminalOutputSink?` | Senke für Terminal-Rohausgabe (`CliOutputProtokollWriter`) |

## `CliRuntimeStatusChangedEventArgs`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` (Zeilen 411–422)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Status` | `CliRuntimeStatus` | Neuer Laufzeitstatus |

## `ProcessStartResult`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleProcessStarter.cs` (Zeilen 146–161)

Interner readonly struct; Rückgabe von `PseudoConsoleProcessStarter.Start`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProcessHandle` | `IntPtr` | Win32-Prozess-Handle (bleibt offen → `CliProcessHandle.NativeProcessHandle`) |
| `Pid` | `int` | Prozess-ID |

## `ScheduledPromptInfo`
Datei: `src/Softwareschmiede/Application/Services/ScheduledPromptInfo.cs`

`record ScheduledPromptInfo(Guid AufgabeId, string PromptText, DateTimeOffset TargetTime)` — Beschreibung eines geplanten zeitgesteuerten Prompts für `PromptZeitVersandService`.

## `ProzessStartAnfrage`
Datei: `src/Softwareschmiede/Domain/ValueObjects/ProzessStartAnfrage.cs`

`record ProzessStartAnfrage(string DateiName, string? Argumente, bool ShellAusfuehren)` — prozessstart-Beschreibung für `IProzessStarter` (wird für Explorer-/IDE-Öffnen verwendet, **nicht** für den CLI-/Terminalpfad).

## `EntwicklungsprozessServiceOptions`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` (Zeilen 20–26)

Record mit optionalen Dienst-Abhängigkeiten; für die Terminalintegration relevant:

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `KiAusfuehrungsService` | `KiAusfuehrungsService?` | Dienst zum Starten der KI-CLI (`StartWithPseudoConsoleAsync`) |

Weitere Optionen: `ProjektService`, `RepositoryStartskriptService`, `RepositoryInitialisierungService`, `GitOrchestrationService`.

## `Aufgabe` (nur terminalrelevante Felder)
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `KiPluginPrefix` | `string?` | Prefix des verwendeten KI-Plugins |
| `AktiveRunId` | `string?` | Aktive Lauf-ID einer KI-Ausführung |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Letzter Heartbeat (durch `CliProcessManager`) |
| `LetzterCliStartUtc` | `DateTimeOffset?` | Zeitpunkt des letzten echten CLI-Starts |
| `LaufStatus` | `AufgabeLaufStatus?` | Laufzeit-Substatus, gepflegt aus `PseudoConsoleSession.RuntimeStatusChanged` über `CliProcessManager` |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Grober Ausführungszustand (steuert u. a. CLI-Panel-Sichtbarkeit via `SollCliAnzeigen`) |
| `PausiertBisUtc` | `DateTimeOffset?` | Blockiert Starts/Neustarts/Prompt-Versand; laufender Prozess wird nicht unterbrochen |
| `LokalerKlonPfad` | `string?` | Lokales Arbeitsverzeichnis für den CLI-Start |

## Nicht vorhanden
- `ITerminalSession`, `ITerminalSessionFactory`/`TerminalSessionService`, `TerminalSessionStartSpec`, `TerminalProviderCapabilities`, `TerminalSessionDiagnostics`, `TerminalReplayBuffer` — keine dieser in der Anforderung vorgeschlagenen Abstraktionen existiert im Code (Repo-weite Suche ohne Treffer).
- Es gibt **keinen** Typ, der eine reine Startbeschreibung (Executable/Args/Cwd/Env) ohne `System.Diagnostics.ProcessStartInfo` abbildet — die Plugins liefern `ProcessStartInfo` direkt.
