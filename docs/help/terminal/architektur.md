← [Zurück zur Übersicht](index.md)

# Terminal-Integration — Architektur

## Beteiligte Komponenten

| Komponente | Typ | Lage | Rolle |
|------------|-----|------|-------|
| `ITerminalSession` | Interface | `Softwareschmiede.Infrastructure.Terminal` | Einheitliche Session-Abstraktion: `Buffer`, `InputStream`, `WriteInputAsync`/`WritePromptAsync`, `Resize`, `IsPseudoTerminal`, `ExitCode`, `DrainOutputAsync`, `RebuildBufferFromReplay`, `Mark*Activity`, Events `OutputChunk`/`Exited`/`Failed`/`BufferChanged`/`RuntimeStatusChanged` |
| `PseudoConsoleSession` | Klasse | `Softwareschmiede.Infrastructure.Terminal` | Einzige `ITerminalSession`-Implementierung: koordiniert Prozess, ConPTY oder Pipes; betreibt die Leseschleife (`ReadLoopAsync`) ab Konstruktion bis `Dispose()` unabhängig vom UI-Lebenszyklus (Issue-86); feuert `BufferChanged`, `OutputChunk`, `Exited`, `Failed` |
| `TerminalSessionStartSpec` | Record | `Softwareschmiede.Plugin.Contracts` | Plugin-gelieferte Startbeschreibung: `FileName`, `Arguments`, `WorkingDirectory`, `EnvironmentVariables`, `Capabilities`, `PluginName`, `OptionalParameters` |
| `TerminalProviderCapabilities` | Flags-Enum | `Softwareschmiede.Plugin.Contracts` | `None`, `SupportsPty`, `RequiresPty` — steuert die Backend-Wahl |
| `TerminalExecutableResolver` | Statische Klasse | `Softwareschmiede.Infrastructure.Terminal` | PATHEXT-/PATH-Auflösung von `FileName`: `.exe`/endungslos → `Direct` (absoluter Pfad), `.cmd`/`.bat` → `CmdWrapped` (`cmd.exe /d /s /c "<pfad>"`), sonstiger Treffer → `NotExecutable`, kein Treffer → `NotFound` |
| `TerminalSessionDiagnostics` | Klasse | `Softwareschmiede.Infrastructure.Terminal` | Preflight-Einzelchecks (`ConPTY-Verfügbarkeit`, `Executable`, `CLI-Health`, `Encoding`, `Terminalgröße`, `Pluginparameter`) → `TerminalPreflightResult` mit `BackendEmpfehlung` |
| `ITerminalSessionFactory` / `TerminalSessionService` | Interface / Klasse | `Softwareschmiede.Infrastructure.Terminal` | Zentrale Session-Erzeugung: Spec-Validierung, Executable-Auflösung, Preflight, Backend-Wahl (PTY vs. Pipe), `[Terminal-Diagnose]`-Fallback-Marker |
| `IPseudoConsoleProcessLauncher` | Interface | `Softwareschmiede.Infrastructure.Terminal` | Backend-Start: `Start(Guid, TerminalSessionStartSpec, ITerminalOutputSink?)` → `TerminalSessionStartResult`; `IsPseudoTerminal` |
| `Win32PseudoConsoleProcessLauncher` | Klasse | `Softwareschmiede.Infrastructure.Terminal` | ConPTY-Backend: startet die normalisierte Spec direkt via `CreateProcess` + `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE` — ohne `cmd.exe`-Hülle |
| `SimulatedPseudoConsoleProcessLauncher` | Klasse | `Softwareschmiede.Infrastructure.Terminal` | Pipe-Backend: startet die Spec über umgeleitete Stdin/Stdout/Stderr; `CrSubmittingInputStream` übersetzt nacktes `\r` nach `\r\n` für Pipe-basierte Shells |
| `TerminalReplayBuffer` | Klasse | `Softwareschmiede.Infrastructure.Terminal` | Begrenzter Byte-Ringpuffer mit den rohen Ausgabe-Chunks — Basis für `RebuildBufferFromReplay` beim UI-Reattach |
| `TerminalSessionOptions` | Klasse | `Softwareschmiede.Infrastructure.Terminal` | `Terminal`-Konfiguration: `ReplayBufferByteBudget` (Default 512 KiB), `DefaultCols`/`DefaultRows` (220/50) |
| `ITerminalOutputSink` | Interface | `Softwareschmiede.Infrastructure.Terminal` | Optionale Senke für rohe Terminal-Output-Bytes; erlaubt UI-unabhängige Weiterverarbeitung der gelesenen Chunks |
| `PseudoConsole` | Klasse | `Softwareschmiede.Infrastructure.Terminal` | HPCON-Wrapper; Größenänderungen |
| `PseudoConsoleProcessStarter` | Statische Klasse | `Softwareschmiede.Infrastructure.Terminal` | Win32-Prozessstart mit ConPTY |
| `PseudoConsoleNativeMethods` | Statische Klasse | `Softwareschmiede.Infrastructure.Terminal` | P/Invoke-Deklarationen |
| `TerminalBuffer` | Klasse | `Softwareschmiede.Domain.Terminal` | 2D-Grid, Cursor, Farben, Scrollback, Scroll-Region (DECSTBM), Insert/Delete-Line/Char, Erase-in-Line/Display, Alternate Screen, Save/Restore-Cursor, `Reset()` |
| `TerminalCell` | Record Struct | `Softwareschmiede.Domain.Terminal` | Einzelne Zelle (Zeichen, Farben, Attribute) |
| `TerminalEvent` + Subklassen | Record Klassen | `Softwareschmiede.Domain.Terminal` | Parser-Ergebnis-Hierarchie (Text, Cursor, Farben, Erase, Alt-Screen, Scroll, Insert/Delete, Save/Restore, Reset) |
| `AnsiSequenceParser` | Klasse | `Softwareschmiede.Infrastructure.Terminal` | VT100/ANSI-Zustandsmaschine mit chunk-übergreifendem UTF-8-Decoding |
| `TerminalControl` | FrameworkElement | `Softwareschmiede.App.Controls` | Reiner Renderer: WPF-Rendering und Tastaturhandling; bindet `ITerminalSession`, ruft beim Reattach `RebuildBufferFromReplay`, unterdrückt Scrollback im Alternate Screen |
| `KeyToVt100Encoder` | Statische Klasse | `Softwareschmiede.App.Controls` | WPF Key → VT100-Byte-Konversion |
| `KiAusfuehrungsService` | Service | `Softwareschmiede.Application.Services` | Prozess-Lifecycle-Management; delegiert Session-Erzeugung an `ITerminalSessionFactory` |
| `CliOutputLineAccumulator` | Klasse | `Softwareschmiede.Application.Services` | Dekodiert UTF-8 über Chunk-Grenzen und segmentiert Terminal-Output in Protokollzeilen |
| `CliOutputProtokollWriter` | Klasse | `Softwareschmiede.Application.Services` | Implementiert `ITerminalOutputSink`; schreibt Ausgabezeilen über `ProtokollService.AddCliOutputAsync` in das Aufgabenprotokoll |
| `ProtokollService` | Service | `Softwareschmiede.Application.Services` | Persistiert `ProtokollTyp.CliOutput` und erkennt Rate-Limit-Marker |
| `TaskDetailViewModel` | ViewModel | `Softwareschmiede.App.ViewModels` | Event-Propagation (`TerminalSessionGestartet`) |
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
    ↓ delegiert Session-Erzeugung an
  TerminalSessionService (ITerminalSessionFactory)

Session-Erzeugung:
  TerminalSessionService.StartAsync
    ├─ TerminalExecutableResolver.Resolve(spec)      → normalisierte Spec
    ├─ TerminalSessionDiagnostics.RunPreflightAsync  → Checks + PtyVerfuegbar
    └─ Backend-Wahl:
         ├─ PTY  → Win32PseudoConsoleProcessLauncher  → ConPTY-Prozess
         └─ Pipe → SimulatedPseudoConsoleProcessLauncher → Stdin/Stdout-Pipes

Protokollierungs-Pfad:
  PseudoConsoleSession (ITerminalSession)
    ↓ meldet Output-Chunks an
  ITerminalOutputSink / CliOutputProtokollWriter
    ↓ schreibt via ProtokollService
  Protokolleintrag(Typ = CliOutput, AufgabeId)

Rendering-Pfad:
  ITerminalSession (PseudoConsoleSession)
    ↓ propagiert Event zu
  TaskDetailView → setzt Session auf
  TerminalControl
    ↓ rendert
  TerminalBuffer
    ↓ wird gefüllt durch
  AnsiSequenceParser
    ↓ parst Bytes aus
  ITerminalSession.OutputStream
    ↓ die kommt von
  PseudoConsole (PTY) bzw. Prozess-Stdout (Pipe)
    ↓ wurde erstellt durch
  PseudoConsoleProcessStarter / SimulatedPseudoConsoleProcessLauncher
    ↓ mit Hilfe von
  PseudoConsoleNativeMethods (P/Invoke)
    ↓
  Windows ConPTY API

Tastatureingaben (Reverse-Pfad):
  TerminalControl.PreviewKeyDown/TextInput
    ↓ konvertiert via
  KeyToVt100Encoder
    ↓ schreibt in
  ITerminalSession.InputStream
    ↓ (Pipe-Backend: CrSubmittingInputStream übersetzt \r → \r\n)
    ↓
  Laufender Prozess
```

## Datenfluss

### 1. Session-Start (Direct-Start, Issue #271)

```
KiAusfuehrungsService.StartTerminalSessionAsync(aufgabeId, plugin, repoPath, optionalParameters)
  ├─ Ruft IKiPlugin.GetTerminalStartSpecAsync() auf → TerminalSessionStartSpec
  ├─ Erzeugt CliOutputProtokollWriter (ITerminalOutputSink) für die Aufgabe
  ├─ Ruft ITerminalSessionFactory.StartAsync(aufgabeId, spec, sink, plugin.CheckHealthAsync, ct) auf
  │    ├─ TerminalExecutableResolver.Resolve(spec)
  │    │    ├─ absoluter/relativer Pfad oder nackter Name
  │    │    ├─ Suche: WorkingDirectory → PATH (Spec-Env, sonst Prozess-Env)
  │    │    ├─ Kandidaten: literal / fileName + PATHEXT (Spec-Env → Prozess-Env → .COM;.EXE;.BAT;.CMD)
  │    │    ├─ .exe/.com/endungslos     → Direct (absoluter Pfad)
  │    │    ├─ .cmd/.bat               → CmdWrapped: cmd.exe /d /s /c "<pfad>" <args>
  │    │    ├─ Treffer ohne Executable → NotExecutable
  │    │    └─ kein Treffer           → NotFound
  │    ├─ TerminalSessionDiagnostics.RunPreflightAsync(spec, resolution, healthCheck, forcePtyUnavailable)
  │    │    ├─ ConPTY-Verfügbarkeit (OS-Build ≥ 17763; forcePtyUnavailable via AppSetting "Terminal.ForcePtyUnavailable")
  │    │    ├─ Executable-Check (aus Resolution: nicht gefunden / nicht ausführbar)
  │    │    ├─ CLI-Health (healthCheck-Delegate — nur bei erfolgreicher Auflösung; nicht fatal)
  │    │    ├─ Encoding (UTF-8), Terminalgröße (DefaultCols/DefaultRows), Pluginparameter
  │    │    └─ → TerminalPreflightResult { Checks, PtyVerfuegbar, BackendEmpfehlung }
  │    └─ Backend-Wahl (feste Reihenfolge):
  │         ├─ Executable NotFound/NotExecutable → InvalidOperationException + Marker
  │         ├─ RequiresPty && !PtyVerfuegbar     → InvalidOperationException + Marker
  │         ├─ E2E-Modus (SOFTWARESCHMIEDE_TEST_DB_PATH) → Pipe
  │         ├─ !SupportsPty                      → Pipe + Diagnose-Marker
  │         ├─ PtyVerfuegbar                     → PTY (Win32PseudoConsoleProcessLauncher)
  │         └─ sonst                             → Pipe + Diagnose-Marker
  │         (Fallback-Diagnose: LogWarning + "[Terminal-Diagnose]"-Zeile via outputSink.OnOutputChunk
  │          — landet nur im CliOutput-Protokoll, nicht im Terminal/Replay. Für die Anwenderin
  │          wird der eingeschränkte Modus zusätzlich im CliStatusText sichtbar gemacht:
  │          "… (eingeschränkter Modus – kein Pseudo-Terminal)", sobald die Session
  │          IsPseudoTerminal == false meldet)
  ├─ Erzeugt CliProcessHandle mit ITerminalSession-Referenz und OutputSink
  ├─ Verdrahtet session.Exited/Failed → HandleProcessExitedAsync / Status Fehler
  ├─ Fired CliProcessStatusChanged(Gestartet)
  └─ Fired TaskDetailViewModel.TerminalSessionGestartet(session)
```

Der Plugin-Prozess wird direkt aus der Spec gestartet — es gibt keine `cmd.exe`-Zwischenschale mehr,
in die der Befehl als Tastatureingabe getippt wird. Für `IKiPlugin`-Implementierungen auf
`CliKiPluginBase`-Basis mappt `BuildTerminalStartSpec` die bestehende `ProcessStartInfo` auf die
Spec (inkl. Environment-Variablen, `OptionalParameters`).

### 2. Output-Rendering und Replay

Die Leseschleife (`ReadLoopAsync`) läuft in `PseudoConsoleSession` selbst — gestartet im Konstruktor der
Session und beendet erst in `Dispose()`. Sie läuft unabhängig davon, ob ein `TerminalControl` gebunden
ist (Issue-86). Jeder gelesene Chunk:

1. geht an die optionale `ITerminalOutputSink` (Protokollierung, rohe Bytes),
2. wird im `TerminalReplayBuffer` (rohe Bytes, Budget aus `Terminal:ReplayBufferByteBudget`) abgelegt,
3. wird vom `AnsiSequenceParser` in `TerminalEvent`s zerlegt (chunk-übergreifendes UTF-8),
4. wird auf den `TerminalBuffer` angewendet (unter dem Render-Lock),
5. löst `OutputChunk` (rohe Bytes) und `BufferChanged` aus.

```
PseudoConsoleSession.OutputStream (ConPTY-Pipe bzw. Prozess-Stdout)
  ↓ async bytes gelesen in ReadLoopAsync (läuft ab Session-Konstruktion)
ITerminalOutputSink.OnOutputChunk(bytes)
  ↓ CliOutputProtokollWriter kopiert Bytes und rekonstruiert Zeilen im Hintergrund
ProtokollService.AddCliOutputAsync(aufgabeId, line)
TerminalReplayBuffer.Append(bytes)
AnsiSequenceParser.Parse(bytes) → TerminalEvents
  ↓ unter Render-Lock angewendet auf
Buffer.Apply(event) → Grid/Cursor/Attribute/Alt-Screen/Scroll-Region
OutputChunk-Event (Rohbytes) → BufferChanged-Event
  ↓ (falls ein TerminalControl gebunden ist)
TerminalControl.OnBufferChanged → Dispatcher.InvokeAsync(InvalidateVisual) → OnRender
```

**Reattach:** Beim erneuten Binden einer Session ruft `TerminalControl.OnSessionChanged`
`session.RebuildBufferFromReplay()` auf — der Buffer wird zurückgesetzt und aus den Replay-Rohbytes
mit einem frischen Parser deterministisch neu aufgebaut. So sieht das Control denselben Endzustand,
ohne dass Ausgabe doppelt erscheint oder verloren geht. Im Alternate Screen meldet das Control keinen
Scrollback (Extent = sichtbares Grid, Offset geklemmt auf 0, Scroll-Navigation No-Op).

Die Protokollierung ist bewusst nicht an `TerminalControl` gekoppelt. Eine Aufgabe schreibt ihre
CLI-Ausgaben weiter in das Protokoll, auch wenn die CLI-Ansicht nicht geöffnet ist.

### 3. Input-Handling

```
TerminalControl.PreviewKeyDown / TextInput
  ↓ fängt WPF Key-Event ab
KeyToVt100Encoder.Encode(keyEventArgs)
  ↓ konvertiert Key zu VT100 byte[]
ITerminalSession.InputStream.WriteAsync(bytes)
  ├─ PTY-Backend:  schreibt in die ConPTY-Input-Pipe
  └─ Pipe-Backend: CrSubmittingInputStream übersetzt nacktes \r → \r\n
    ↓ Prozess liest stdin
```

### 4. Größenänderung

```
TerminalControl.SizeChanged
  ↓ berechnet newCols/newRows aus Zellgröße
  ↓ ruft auf
TerminalBuffer.Resize(newCols, newRows)     — Grid anpassen (alt-screen-aware)
ITerminalSession.Resize(newCols, newRows)   — dedupliziert, serialisiert
  ↓ (nur PTY-Backend, unter Lock)
PseudoConsole.Resize(cols, rows)
  ↓ ruft Win32-API auf
ResizePseudoConsole(hpcon, newSize)
  ↓ Prozess erhält SIGWINCH-Äquivalent
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
                          ↑↓ Session Property (ITerminalSession)
┌─────────────────────────────────────────────────────────────┐
│ Application Layer                                            │
│ ┌────────────────────────────────────────────────────────┐ │
│ │ TaskDetailViewModel                                    │ │
│ │ └─ Event: TerminalSessionGestartet                     │ │
│ │ KiAusfuehrungsService (Lifecycle, CliProcessHandle)    │ │
│ └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                          ↑↓ StartTerminalSessionAsync / ITerminalSessionFactory
┌─────────────────────────────────────────────────────────────┐
│ Domain Layer                                                 │
│ ┌────────────────────────────────────────────────────────┐ │
│ │ TerminalBuffer (2D-Grid, Cursor, Scrollback,           │ │
│ │   Alt-Screen, Scroll-Region, Insert/Delete, Reset)     │ │
│ │ TerminalCell (record struct)                           │ │
│ │ TerminalEvent + Subklassen (Events vom Parser)         │ │
│ └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                          ↑↓ Apply(event)
┌─────────────────────────────────────────────────────────────┐
│ Infrastructure Layer                                         │
│ ┌────────────────────────────────────────────────────────┐ │
│ │ TerminalSessionService (ITerminalSessionFactory)       │ │
│ │ TerminalExecutableResolver (PATHEXT/PATH-Auflösung)    │ │
│ │ TerminalSessionDiagnostics (Preflight-Checks)          │ │
│ │ AnsiSequenceParser (VT100-Zustandsmaschine, UTF-8)     │ │
│ │ PseudoConsoleSession (ITerminalSession, Replay, Locks) │ │
│ │ TerminalReplayBuffer (Byte-Ringpuffer)                 │ │
│ │ PseudoConsole (HPCON-Wrapper)                          │ │
│ │ PseudoConsoleProcessStarter (Win32-ConPTY-Start)       │ │
│ │ SimulatedPseudoConsoleProcessLauncher (Pipe-Backend)   │ │
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
│ │ Plugin-CLI-Prozess (z. B. devin.exe, codex.cmd)        │ │
│ └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

## Konfiguration

Sektion `Terminal` in `appsettings.json` (gebunden an `TerminalSessionOptions`):

| Schlüssel | Default | Bedeutung |
|---|---|---|
| `Terminal:ReplayBufferByteBudget` | 524288 (512 KiB) | Byte-Budget des Replay-Puffers pro Session — begrenzt, wie viel Rohoutput für den Reattach-Neuaufbau vorgehalten wird |
| `Terminal:DefaultCols` | 220 | Anfangsspalten der Session (ConPTY-Erstellung, Preflight-Check) |
| `Terminal:DefaultRows` | 50 | Anfangszeilen der Session |
| `Terminal.ForcePtyUnavailable` (App-Einstellung, nicht JSON) | — | Test-/Debug-Hook: erzwingt `PtyVerfuegbar=false` im Preflight → Pipe-Fallback mit Diagnose-Marker; `RequiresPty`-Plugins schlagen fehl |

## Executable-Auflösung

`TerminalExecutableResolver` normalisiert `spec.FileName` vor dem Prozessstart:

- **Suchreihenfolge:** `WorkingDirectory` → `PATH` aus den Spec-Umgebungsvariablen → `PATH` des eigenen Prozesses.
- **PATHEXT:** aus den Spec-Umgebungsvariablen, sonst Prozess-`PATHEXT`, sonst `.COM;.EXE;.BAT;.CMD`.
- **Namen ohne Erweiterung:** literaler Treffer zuerst, dann `name` + jedes PATHEXT-Element.
- **`.exe`/`.com` oder endungslose Dateien:** `Direct` — `FileName` wird zum absoluten Pfad.
- **`.cmd`/`.bat`:** `CmdWrapped` — die Spec wird zu `cmd.exe /d /s /c "<pfad>" <original-args>` normalisiert (npm-Shims u. ä.).
- **Treffer mit anderer Erweiterung** (z. B. `.ps1`): `NotExecutable` — harter Fehler, kein Pipe-Fallback.
- **Kein Treffer:** `NotFound` — harter Fehler (`InvalidOperationException`) mit `[Terminal-Diagnose]`-Marker.

## Service-Integration

### KiAusfuehrungsService

Zentrale Lifecycle-Klasse:
- Erzeugt `CliProcessHandle` mit `ITerminalSession`-Referenz und OutputSink
- Verwaltet aktive Prozesse in Dictionary `_handles`, solange der zugehörige Prozess läuft — unabhängig davon, ob die Aufgabenseite angezeigt wird (parallele CLI-Ausführungen, Issue-86)
- Delegiert die Session-Erzeugung an `ITerminalSessionFactory` (`TerminalSessionService`) und reicht `plugin.CheckHealthAsync` als Preflight-Probe durch
- Propagiert Status-Änderungen via `CliProcessStatusChanged`-Event
- `GetTerminalSession(aufgabeId)` ermöglicht Zugriff auf die Session unabhängig vom View-Lebenszyklus (z. B. für Resize/Stop oder erneutes Binden an ein `TerminalControl`)
- Das `Exited`-Ereignis der Session (nicht mehr `Process.Exited` auf Service-Ebene) löst `HandleSessionEndedAsync` aus (→ `HandleExitedCoreAsync`) — die Session meldet ihren eigenen Exit-Code (`GetExitCodeProcess` auf dem nativen Handle bzw. `Process.ExitCode` im Pipe-Fallback); `Failed` geht über `HandleSessionFailedAsync` in denselben Pfad
- `Dispose()` muss beim App-Shutdown aufgerufen werden, damit alle noch laufenden Sessions (und deren Leseschleifen) sauber beendet werden; dies geschieht automatisch, da `KiAusfuehrungsService` als Singleton im DI-Container registriert ist und beim Beenden des Hosts (`App.OnExit` → `_host.Dispose()`) disposed wird
- Beim Aufräumen ruft `DisposeSessionResourcesAsync` zuerst einen kurzen `DrainOutputAsync` der Session und danach `OutputSink.CompleteAsync(...)` auf, damit bereits angenommene Protokollzeilen begrenzt persistiert werden können

### TaskDetailViewModel

Holt die laufende Session vom Service (nach dem Start über `CliProcessStatusChanged` bzw. beim Laden via `GetTerminalSession`) und propagiert sie über das eigene `TerminalSessionGestartet`-Event an die View:
```csharp
var session = _kiService.GetTerminalSession(_aufgabeId);
if (session is not null)
    TerminalSessionGestartet?.Invoke(session); // Action<ITerminalSession>
```

### TaskDetailView.xaml.cs

Abonniert das ViewModel-Event und setzt die Session auf das Control — beim erneuten Binden baut `TerminalControl` den Buffer über `RebuildBufferFromReplay()` neu auf:
```csharp
ViewModel.TerminalSessionGestartet += session =>
{
    TerminalConsole.Session = session;
};
```

## Fehlertoleranzen

| Fehlerszenario | Behandlung |
|---|---|
| Executable nicht auffindbar / nicht ausführbar | `InvalidOperationException` nach `[Terminal-Diagnose]`-Marker im CliOutput-Protokoll → UI-Fehler-Banner |
| `RequiresPty`-Plugin ohne verfügbare PTY | `InvalidOperationException` nach Diagnose-Marker — kein stiller Pipe-Fallback |
| `CreatePseudoConsole` schlägt fehl | `InvalidOperationException` mit HRESULT → UI-Fehler-Banner |
| PTY nicht verfügbar / `SupportsPty` fehlt | Pipe-Backend mit `[Terminal-Diagnose]`-Marker (Log + Protokollzeile) |
| Prozess startet nicht | Win32-Fehler wird geloggt; Status `Fehler` |
| Ausgabe-Pipe schließt vorzeitig | `ReadLoopAsync` endet; Buffer bleibt im letzten Zustand erhalten; `Exited` nur bei realem Prozessende |
| Resize-API schlägt fehl | Rückgabewert ignoriert; Konsole läuft mit alter Größe weiter |
| `InputStream.WriteAsync` schlägt fehl (`OnPreviewKeyDown`/`OnTextInput`) | Fehler wird per `LogWarning` protokolliert statt verschluckt; Tastatureingabe geht verloren, Steuerung bleibt bedienbar |
| ANSI-Parser-Fehler | Fehlerhafte Sequenzen werden ignoriert; Zustand bleibt konsistent; `Failed`-Event signalisiert Lesefehler |
| Unerwartete Exception in `ReadLoopAsync` | `catch (Exception)` protokolliert (`LogError`) und feuert `Failed`; Leseschleife endet geordnet |
| `TerminalControl` nicht (mehr) gebunden, während Prozess Ausgabe produziert | Die Leseschleife der Session läuft unabhängig weiter und puffert Ausgabe in `Buffer` + `TerminalReplayBuffer` — kein Datenverlust (Issue-86); Reattach baut den Buffer aus dem Replay neu auf |
| `CliOutputProtokollWriter` kann eine Zeile nicht persistieren | Fehler wird geloggt; die Terminal-Session, der Parser und das Rendering laufen weiter |
| Output-Persistenz fällt hinter schnelle CLI-Ausgabe zurück | Bounded Queue erzeugt Backpressure und protokolliert Warnungen ab definierten Schwellen; der Terminal-Output-Reader wartet, bis wieder Queue-Kapazität verfügbar ist |
| Plugin-Health-Check schlägt fehl / wirft | Nicht-fataler Preflight-Check-Eintrag `CLI-Health` (Ok=false) — Start läuft weiter |

## Skalierung und Zuverlässigkeit

- **Speicherverbrauch:** 1000-Zeilen-Scrollback × Spaltenanzahl × `TerminalCell`-Größe (ca. 30 Bytes). Bei 120 Spalten: ~3.6 MB. Zusätzlich `TerminalReplayBuffer` ≤ `Terminal:ReplayBufferByteBudget` (Default 512 KiB) Rohbytes pro Session.
- **CPU-Last:** Rendering per `DrawingContext` ist effizient; Parser läuft on-demand (Byte-basiert).
- **Hängende Prozesse:** Keine speziellen Timeouts; das `Exited`-Ereignis der Session ist Source of Truth.
- **CLI-Protokollierung:** `CliOutputProtokollWriter.QueueCapacity` begrenzt die ausstehenden Ausgabezeilen auf 4096. Der Abschluss ist idempotent, synchronisiert sich mit einer aktiven Producer-Queue-Phase und kann über `CompleteAsync(timeout)` begrenzt auf die Persistenz bereits angenommener Zeilen warten.
- **Windows-Versionen:** ConPTY erfordert Windows 10 Build 17763+; auf älteren Builds läuft das Pipe-Backend mit Diagnose-Marker — `RequiresPty`-Plugins schlagen dort mit Fehler fehl.
