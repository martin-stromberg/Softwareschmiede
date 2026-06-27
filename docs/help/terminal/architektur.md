← [Zurück zur Übersicht](index.md)

# Terminal-Integration — Architektur

## Beteiligte Komponenten

| Komponente | Typ | Lage | Rolle |
|------------|-----|------|-------|
| `PseudoConsoleSession` | Klasse | `Softwareschmiede.Infrastructure.Terminal` | Koordiniert Prozess, ConPTY und Pipes |
| `PseudoConsole` | Klasse | `Softwareschmiede.Infrastructure.Terminal` | HPCON-Wrapper; Größenänderungen |
| `PseudoConsoleProcessStarter` | Statische Klasse | `Softwareschmiede.Infrastructure.Terminal` | Win32-Prozessstart mit ConPTY |
| `PseudoConsoleNativeMethods` | Statische Klasse | `Softwareschmiede.Infrastructure.Terminal` | P/Invoke-Deklarationen |
| `TerminalBuffer` | Klasse | `Softwareschmiede.Domain.Terminal` | 2D-Grid, Cursor, Farben, Scrollback |
| `TerminalCell` | Record Struct | `Softwareschmiede.Domain.Terminal` | Einzelne Zelle (Zeichen, Farben, Attribute) |
| `TerminalEvent` + Subklassen | Record Klassen | `Softwareschmiede.Domain.Terminal` | Parser-Ergebnis-Hierarchie |
| `AnsiSequenceParser` | Klasse | `Softwareschmiede.Infrastructure.Terminal` | VT100/ANSI-Zustandsmaschine |
| `TerminalControl` | FrameworkElement | `Softwareschmiede.App.Controls` | WPF-Rendering und Tastaturhandling |
| `KeyToVt100Encoder` | Statische Klasse | `Softwareschmiede.App.Controls` | WPF Key → VT100-Byte-Konversion |
| `KiAusfuehrungsService` | Service | `Softwareschmiede.Application.Services` | Prozess-Lifecycle-Management |
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
  ├─ Erzeugt PseudoConsoleSession (bindet Prozess, Pipes, PseudoConsole)
  ├─ Erzeugt CliProcessHandle mit PseudoConsoleSession-Referenz
  ├─ Fired CliProcessStatusChanged(Gestartet)
  └─ Fired TaskDetailViewModel.PseudoConsoleSessionGestartet(session)
```

### 2. Output-Rendering

```
PseudoConsoleSession.OutputStream (Pipe)
  ↓ async bytes gelesen in ReadLoopAsync
AnsiSequenceParser.Parse(bytes)
  ↓ zerlegt bytes in Events
[TextWrittenEvent, CursorMovedEvent, ColorChangedEvent, ...]
  ↓ jedes Event wird angewendet auf
TerminalBuffer.Apply(event)
  ↓ aktualisiert Grid[row,col], CursorRow, CursorCol, Attribute
  ↓ Schleife nach jedem Batch
TerminalControl.InvalidateVisual()
  ↓ triggert WPF-Render-Cycle
TerminalControl.OnRender(DrawingContext)
  ↓ liest aktuelle Grid-Zellen + Cursor
  ↓ zeichnet Rechtecke (Hintergrund) und FormattedText (Vordergrund)
  ↓ rendert Cursor-Rechteck
```

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
- Verwaltet aktive Prozesse in Dictionary `_handles`
- Propagiert Status-Änderungen via `CliProcessStatusChanged`-Event
- `GetPseudoConsoleSession(aufgabeId)` ermöglicht später Zugriff auf Session für Resize/Stop

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
| Ausgabe-Pipe schließt vorzeitig | `ReadLoopAsync` endet; Buffer bleibt sichtbar |
| Resize-API schlägt fehl | Rückgabewert ignoriert; Konsole läuft mit alter Größe weiter |
| `InputStream.WriteAsync` schlägt fehl | Fehler wird geloggt; Tastatureingabe verloren |
| ANSI-Parser-Fehler | Fehlerhafte Sequenzen werden ignoriert; Zustand bleibt konsistent |

## Skalierung und Zuverlässigkeit

- **Speicherverbrauch:** 1000-Zeilen-Scrollback × Spaltenanzahl × `TerminalCell`-Größe (ca. 30 Bytes). Bei 120 Spalten: ~3.6 MB.
- **CPU-Last:** Rendering per `DrawingContext` ist effizient; Parser läuft on-demand (Byte-basiert).
- **Hängende Prozesse:** Keine speziellen Timeouts; `Process.Exited`-Event ist Source of Truth.
- **Windows-Versionen:** Erfordert Windows 10 Build 17763+; kein Fallback auf ältere Versionen.
