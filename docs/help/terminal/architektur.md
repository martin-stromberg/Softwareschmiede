← [Zurück zur Übersicht](index.md)

# Terminal-Integration — Architektur

## Beteiligte Komponenten

| Komponente | Typ | Lage | Rolle |
|------------|-----|------|-------|
| `PseudoConsoleSession` | Klasse | `Softwareschmiede.Infrastructure.Terminal` | Koordiniert Prozess, ConPTY und Pipes; betreibt die Leseschleife (`ReadLoopAsync`) ab Konstruktion bis `Dispose()` unabhängig vom UI-Lebenszyklus (Issue-86) und feuert `BufferChanged` |
| `ITerminalOutputSink` | Interface | `Softwareschmiede.Infrastructure.Terminal` | Optionale Senke für rohe Terminal-Output-Bytes; erlaubt UI-unabhängige Weiterverarbeitung der gelesenen Chunks |
| `PseudoConsole` | Klasse | `Softwareschmiede.Infrastructure.Terminal` | HPCON-Wrapper; Größenänderungen |
| `PseudoConsoleProcessStarter` | Statische Klasse | `Softwareschmiede.Infrastructure.Terminal` | Win32-Prozessstart mit ConPTY |
| `PseudoConsoleNativeMethods` | Statische Klasse | `Softwareschmiede.Infrastructure.Terminal` | P/Invoke-Deklarationen |
| `TerminalBuffer` | Klasse | `Softwareschmiede.Domain.Terminal` | 2D-Grid, Cursor, Farben, Scrollback |
| `TerminalCell` | Record Struct | `Softwareschmiede.Domain.Terminal` | Einzelne Zelle (Zeichen, Farben, Attribute) |
| `TerminalEvent` + Subklassen | Record Klassen | `Softwareschmiede.Domain.Terminal` | Parser-Ergebnis-Hierarchie |
| `AnsiSequenceParser` | Klasse | `Softwareschmiede.Infrastructure.Terminal` | VT100/ANSI-Zustandsmaschine |
| `TerminalControl` | FrameworkElement | `Softwareschmiede.App.Controls` | Reiner Renderer: WPF-Rendering und Tastaturhandling; abonniert `PseudoConsoleSession.BufferChanged`, besitzt keine eigene Leseschleife |
| `KeyToVt100Encoder` | Statische Klasse | `Softwareschmiede.App.Controls` | WPF Key → VT100-Byte-Konversion |
| `KiAusfuehrungsService` | Service | `Softwareschmiede.Application.Services` | Prozess-Lifecycle-Management |
| `CliOutputLineAccumulator` | Klasse | `Softwareschmiede.Application.Services` | Dekodiert UTF-8 über Chunk-Grenzen und segmentiert Terminal-Output in Protokollzeilen |
| `CliOutputProtokollWriter` | Klasse | `Softwareschmiede.Application.Services` | Implementiert `ITerminalOutputSink`; schreibt Ausgabezeilen über `ProtokollService.AddCliOutputAsync` in das Aufgabenprotokoll |
| `ProtokollService` | Service | `Softwareschmiede.Application.Services` | Persistiert `ProtokollTyp.CliOutput` und erkennt Rate-Limit-Marker |
| `TaskDetailViewModel` | ViewModel | `Softwareschmiede.App.ViewModels` | Event-Propagation |
| `TaskDetailView` | Ansicht | `Softwareschmiede.App.Views` | XAML-Hosting für `TerminalControl` |
| Windows ConPTY API | Win32 | Windows Kernel | `CreatePseudoConsole`, `ResizePseudoConsole`, `ClosePseudoConsole` |

## Abhängigkeiten

```
WPF (Presentation Layer):
  TaskDetailView (XAML)
    ↓ bindet an
  TaskDetailViewModel
    ↓ abonniert Event von
  KiAusfuehrungsService
    ↓ erstellt

Protokollierungs-Pfad:
  PseudoConsoleSession
    ↓ meldet Output-Chunks an
  ITerminalOutputSink / CliOutputProtokollWriter
    ↓ schreibt via ProtokollService
  Protokolleintrag(Typ = CliOutput, AufgabeId)

Rendering-Pfad:
  PseudoConsoleSession
    ↓ propagiert Event zu
  TaskDetailView → setzt Session auf
  TerminalControl
    ↓ rendert
  TerminalBuffer
    ↓ wird gefüllt durch
  AnsiSequenceParser
    ↓ parst Bytes aus
  PseudoConsoleSession.OutputStream
    ↓ die kommt von
  PseudoConsole
    ↓ wurde erstellt durch
  PseudoConsoleProcessStarter
    ↓ mit Hilfe von
  PseudoConsoleNativeMethods (P/Invoke)
    ↓
  Windows ConPTY API

Tastatureingaben (Reverse-Pfad):
  TerminalControl.PreviewKeyDown/TextInput
    ↓ konvertiert via
  KeyToVt100Encoder
    ↓ schreibt in
  PseudoConsoleSession.InputStream
    ↓
  PseudoConsole.InputWritePipe
    ↓
  Laufender Prozess
```

## Datenfluss

### 1. Prozessstart

```
KiAusfuehrungsService.StartWithPseudoConsoleAsync()
  ├─ Ruft IKiPlugin.StartCliAsync() auf → ProcessStartInfo
  ├─ Erstellt anonyme Pipes via CreatePipe
  ├─ Erstellt PseudoConsole via CreatePseudoConsole
  ├─ Startet Prozess via CreateProcess mit PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE
  ├─ Erzeugt CliOutputProtokollWriter für die Aufgabe
  ├─ Erzeugt PseudoConsoleSession (bindet Prozess, Pipes, PseudoConsole, Output-Senke)
  ├─ Erzeugt CliProcessHandle mit PseudoConsoleSession-Referenz und OutputSink
  ├─ Fired CliProcessStatusChanged(Gestartet)
  └─ Fired TaskDetailViewModel.PseudoConsoleSessionGestartet(session)
```

### 2. Output-Rendering

Die Leseschleife (`ReadLoopAsync`) läuft in `PseudoConsoleSession` selbst — gestartet im Konstruktor der
Session und beendet erst in `PseudoConsoleSession.Dispose()`. Sie läuft unabhängig davon, ob ein
`TerminalControl` gebunden ist, damit mehrere CLI-Prozesse parallel weiterlaufen können, auch wenn ihre
Aufgabenseite gerade nicht angezeigt wird (Issue-86). `TerminalControl` ist reiner Renderer und abonniert
lediglich das `BufferChanged`-Event der Session.

```
PseudoConsoleSession.OutputStream (Pipe)
  ↓ async bytes gelesen in PseudoConsoleSession.ReadLoopAsync (läuft ab Session-Konstruktion)
ITerminalOutputSink.OnOutputChunk(bytes) (optional, vor Parser-Verarbeitung)
  ↓ CliOutputProtokollWriter kopiert Bytes und rekonstruiert Zeilen im Hintergrund
ProtokollService.AddCliOutputAsync(aufgabeId, line)
  ↓ speichert Protokolleintrag Typ CliOutput
AnsiSequenceParser.Parse(bytes)
  ↓ zerlegt bytes in Events
[TextWrittenEvent, CursorMovedEvent, ColorChangedEvent, ...]
  ↓ jedes Event wird angewendet auf
PseudoConsoleSession.Buffer.Apply(event)
  ↓ aktualisiert Grid[row,col], CursorRow, CursorCol, Attribute
  ↓ nach jedem Chunk
PseudoConsoleSession.BufferChanged-Event
  ↓ (falls ein TerminalControl gebunden ist)
TerminalControl.OnBufferChanged()
  ↓ Dispatcher.InvokeAsync(InvalidateVisual)
  ↓ triggert WPF-Render-Cycle
TerminalControl.OnRender(DrawingContext)
  ↓ liest aktuelle Grid-Zellen + Cursor
  ↓ zeichnet Rechtecke (Hintergrund) und FormattedText (Vordergrund)
  ↓ rendert Cursor-Rechteck
```

Die Protokollierung ist bewusst nicht an `TerminalControl` gekoppelt. Eine Aufgabe schreibt ihre CLI-Ausgaben weiter in das Protokoll, auch wenn die CLI-Ansicht nicht geöffnet oder gerade keine View an die Session gebunden ist.

### 3. Input-Handling

```
TerminalControl.PreviewKeyDown / TextInput
  ↓ fängt WPF Key-Event ab
KeyToVt100Encoder.Encode(keyEventArgs)
  ↓ konvertiert Key zu VT100 byte[]
  ↓ (z.B. Key.Up → [0x1b, 0x5b, 0x41])
PseudoConsoleSession.InputStream.WriteAsync(bytes)
  ↓ schreibt in Input-Pipe
  ↓ Prozess liest aus HPCON Input
ConPTY interner Buffer
  ↓ Prozess macht stdout/stderr mit Konsequenz
```

### 4. Größenänderung

```
TerminalControl.SizeChanged
  ↓ berechnet newCols = AvailableWidth / CellWidth
  ↓ berechnet newRows = AvailableHeight / CellHeight
  ↓ ruft auf
PseudoConsoleSession.ResizeAsync(newCols, newRows)
  ↓ ruft auf
PseudoConsole.Resize(cols, rows)
  ↓ ruft Win32-API auf
ResizePseudoConsole(hpcon, newSize)
  ↓ Kernel aktualisiert ConPTY interne Größe
  ↓ Prozess erhält SIGWINCH-Signal (Windows-Äquivalent)
TerminalBuffer.Resize(newCols, newRows)
  ↓ passt internes Grid an
```

## Schichtenmodell

```
┌─────────────────────────────────────────────────────────────┐
│ Presentation Layer (WPF)                                    │
│ ┌────────────────────────────────────────────────────────┐ │
│ │ TaskDetailView.xaml (XAML)                             │ │
│ │ └─ TerminalControl (WPF FrameworkElement)              │ │
│ │    ├─ Input: PreviewKeyDown, TextInput                 │ │
│ │    └─ Output: OnRender(DrawingContext)                 │ │
│ └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                          ↑↓ Session Property
┌─────────────────────────────────────────────────────────────┐
│ Application Layer                                            │
│ ┌────────────────────────────────────────────────────────┐ │
│ │ TaskDetailViewModel                                    │ │
│ │ └─ Event: PseudoConsoleSessionGestartet                │ │
│ └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                          ↑↓ StartWithPseudoConsoleAsync
┌─────────────────────────────────────────────────────────────┐
│ Domain Layer                                                 │
│ ┌────────────────────────────────────────────────────────┐ │
│ │ TerminalBuffer (verwaltet 2D-Grid, Cursor, Attribute)  │ │
│ │ TerminalCell (record struct)                           │ │
│ │ TerminalEvent + Subklassen (Events vom Parser)         │ │
│ └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                          ↑↓ Apply(event)
┌─────────────────────────────────────────────────────────────┐
│ Infrastructure Layer                                         │
│ ┌────────────────────────────────────────────────────────┐ │
│ │ AnsiSequenceParser (VT100-Zustandsmaschine)            │ │
│ │ PseudoConsoleSession (Koordination)                    │ │
│ │ PseudoConsole (HPCON-Wrapper)                          │ │
│ │ PseudoConsoleProcessStarter (Win32-Start)              │ │
│ │ PseudoConsoleNativeMethods (P/Invoke)                  │ │
│ │ KeyToVt100Encoder (Key→VT100)                          │ │
│ └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                          ↑↓ Prozess I/O
┌─────────────────────────────────────────────────────────────┐
│ Windows API / OS                                             │
│ ┌────────────────────────────────────────────────────────┐ │
│ │ ConPTY (CreatePseudoConsole, ResizePseudoConsole)      │ │
│ │ CreateProcess, CreatePipe, CloseHandle                 │ │
│ │ Laufender KI-CLI-Prozess (z.B. codex.exe)              │ │
│ └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

## Service-Integration

### KiAusfuehrungsService

Zentrale Lifecycle-Klasse:
- Erstellt `PseudoConsoleSession` und `CliProcessHandle`
- Verwaltet aktive Prozesse in Dictionary `_handles`, solange der zugehörige Prozess läuft — unabhängig davon, ob die Aufgabenseite angezeigt wird (parallele CLI-Ausführungen, Issue-86)
- Erzeugt für ConPTY-Starts einen `CliOutputProtokollWriter` pro `aufgabeId`, reicht ihn über `IPseudoConsoleProcessLauncher.Start(..., outputSink)` an die `PseudoConsoleSession` weiter und hält ihn im `CliProcessHandle.OutputSink`
- Propagiert Status-Änderungen via `CliProcessStatusChanged`-Event
- `GetPseudoConsoleSession(aufgabeId)` ermöglicht Zugriff auf die Session unabhängig vom View-Lebenszyklus (z. B. für Resize/Stop oder erneutes Binden an ein `TerminalControl`)
- `HandleProcessExited()` muss zuverlässig aufgerufen werden (über `Process.Exited`), damit `PseudoConsoleSession.Dispose()` die Leseschleife der Sitzung beendet und das Handle aus `_handles` entfernt wird
- `Dispose()` muss beim App-Shutdown aufgerufen werden, damit alle noch laufenden Sessions (und deren Leseschleifen) sauber beendet werden; dies geschieht automatisch, da `KiAusfuehrungsService` als Singleton im DI-Container registriert ist und beim Beenden des Hosts (`App.OnExit` → `_host.Dispose()`) disposed wird
- Beim Aufräumen ruft `CancelAndDisposeConPtyResourcesAsync` zuerst einen kurzen Output-Drain der Session und danach `OutputSink.CompleteAsync(...)` auf, damit bereits angenommene Protokollzeilen begrenzt persistiert werden können

### TaskDetailViewModel

Erhält Event `PseudoConsoleSessionGestartet` von `KiAusfuehrungsService`:
```csharp
_kiService.PseudoConsoleSessionGestartet += session =>
{
    _currentSession = session;
    PseudoConsoleSessionGestartet?.Invoke(session);
};
```

### TaskDetailView.xaml.cs

Abonniert ViewModel-Event und setzt Session auf Control:
```csharp
ViewModel.PseudoConsoleSessionGestartet += session =>
{
    TerminalConsole.Session = session;
};
```

## Fehlertoleranzen

| Fehlerszenario | Behandlung |
|---|---|
| `CreatePseudoConsole` schlägt fehl | `InvalidOperationException` mit HRESULT → UI-Fehler-Banner |
| Prozess startet nicht | Win32-Fehler wird geloggt; Status `Fehler` |
| Ausgabe-Pipe schließt vorzeitig | `ReadLoopAsync` (in `PseudoConsoleSession`) endet; Buffer bleibt im letzten Zustand erhalten |
| Resize-API schlägt fehl | Rückgabewert ignoriert; Konsole läuft mit alter Größe weiter |
| `InputStream.WriteAsync` schlägt fehl (`OnPreviewKeyDown`/`OnTextInput`) | Fehler wird per `LogWarning` protokolliert statt verschluckt; Tastatureingabe geht verloren, Steuerung bleibt bedienbar |
| ANSI-Parser-Fehler | Fehlerhafte Sequenzen werden ignoriert; Zustand bleibt konsistent |
| Unerwartete Exception in `PseudoConsoleSession.ReadLoopAsync` (außerhalb `ReadAsync`, z. B. in `_parser.Parse`/`Buffer.Apply`) | Generisches `catch (Exception)` protokolliert den Fehler (`LogError "Unerwarteter Fehler im Terminal-Lesevorgang der Sitzung."`); Leseschleife endet geordnet statt die Anwendung zu gefährden |
| `TerminalControl` nicht (mehr) gebunden, während Prozess Ausgabe produziert | Die Leseschleife der Session läuft unabhängig weiter und puffert die Ausgabe in `Buffer`; `BufferChanged` wird gefeuert, hat aber keinen Abonnenten — kein Datenverlust (parallele CLI-Ausführungen, Issue-86) |
| `CliOutputProtokollWriter` kann eine Zeile nicht persistieren | Fehler wird geloggt; die Terminal-Session, der Parser und das Rendering laufen weiter |
| Output-Persistenz fällt hinter schnelle CLI-Ausgabe zurück | Bounded Queue erzeugt Backpressure und protokolliert Warnungen ab definierten Schwellen; der Terminal-Output-Reader wartet, bis wieder Queue-Kapazität verfügbar ist |

## Skalierung und Zuverlässigkeit

- **Speicherverbrauch:** 1000-Zeilen-Scrollback × Spaltenanzahl × `TerminalCell`-Größe (ca. 30 Bytes). Bei 120 Spalten: ~3.6 MB.
- **CPU-Last:** Rendering per `DrawingContext` ist effizient; Parser läuft on-demand (Byte-basiert).
- **Hängende Prozesse:** Keine speziellen Timeouts; `Process.Exited`-Event ist Source of Truth.
- **CLI-Protokollierung:** `CliOutputProtokollWriter.QueueCapacity` begrenzt die ausstehenden Ausgabezeilen auf 4096. Der Abschluss ist idempotent und kann über `CompleteAsync(timeout)` begrenzt auf die Persistenz bereits angenommener Zeilen warten.
- **Windows-Versionen:** Erfordert Windows 10 Build 17763+; kein Fallback auf ältere Versionen.
