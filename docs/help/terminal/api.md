← [Zurück zur Übersicht](index.md)

# Terminal-Integration — API

## Übersicht

Das Terminal-System exponiert die `ITerminalSession`-Abstraktion zum Steuern von Prozessen (`PseudoConsoleSession` für beide Backends; `TerminalReplaySession` für die Wiedergabe von `.clireplay`-Aufzeichnungen), die `ITerminalSessionFactory` (`TerminalSessionService`) zur zentralen Session-Erzeugung mit Executable-Auflösung, Preflight-Diagnose und Backend-Wahl, das `TerminalControl` als WPF-Rendering-Component, das `TerminalSessionGestartet`-Event zum Lifecycle-Management und optionale Output-Senken für die UI-unabhängige Weiterverarbeitung gelesener Terminalausgaben (Aufgabenprotokoll und Rohbyte-Mitschnitt).

## ITerminalSession

Gemeinsame Abstraktion einer Terminal-Session; implementiert `IDisposable`. Zwei Implementierungen: `PseudoConsoleSession` für die Live-Ausführung über beide Backends (ConPTY und Pipe-Fallback — koordiniert einen Prozess mit Input-Pipe und Output-Pipe) und `TerminalReplaySession` für die zeitgesteuerte Wiedergabe einer `.clireplay`-Aufzeichnung im Konsolentestfenster (kein echter Prozess; siehe eigener Abschnitt).

### Eigenschaften

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Process` | `System.Diagnostics.Process` | Der laufende Prozess (read-only) |
| `InputStream` | `System.IO.Stream` | Pipe zum Schreiben von Tastatureingaben (read-only) |
| `OutputStream` | `System.IO.Stream` | Pipe zum Lesen der Prozess-Ausgabe (read-only) |
| `Buffer` | `TerminalBuffer` | Der Terminal-Buffer der Sitzung; wird bereits bei Konstruktion angelegt und von der internen Leseschleife befüllt, unabhängig davon, ob ein `TerminalControl` gebunden ist (read-only) |
| `RuntimeStatus` | `CliRuntimeStatus` | Der aktuelle Betriebszustand der CLI (`Inaktiv`, `Laeuft`, `WartetAufEingabe`). Wird alle 1 Sekunde neu bewertet basierend auf Prozess-Zustand und I/O-Aktivität (read-only) |
| `IsPseudoTerminal` | `bool` | `true`, wenn die Session über ein echtes Pseudo-Terminal (ConPTY) läuft; `false` auf dem Pipe-Fallback-Backend — steuert u. a. den „eingeschränkter Modus"-Hinweis in der Statuszeile |
| `SupportsResize` | `bool` | `true`, wenn die Session-Geometrie zur Laufzeit geändert werden darf (Live-Sessions); `false` bei fixierter Geometrie (`TerminalReplaySession`) — dann darf ein Aufrufer weder `Resize` noch `Buffer.Resize` aufrufen |
| `ExitCode` | `int?` | Der Exit-Code des Prozesses nach dessen Beendigung (`null` vor Beendigung oder wenn nicht ermittelbar) |

### Events

#### `BufferChanged`

Wird nach jeder erfolgreichen Verarbeitung eines Ausgabe-Chunks durch die interne Leseschleife (`ReadLoopAsync`) ausgelöst. Die Leseschleife läuft ab Konstruktion der Session bis zu ihrem `Dispose()` unabhängig vom Lebenszyklus eines gebundenen `TerminalControl` — mehrere CLI-Prozesse können dadurch parallel weiterlaufen und puffern, auch wenn ihre Aufgabenseite gerade nicht angezeigt wird (Issue-86).

Wenn die Session mit einer `ITerminalOutputSink` konstruiert wurde, meldet die Leseschleife denselben Chunk vor der ANSI-Parser-Verarbeitung an die Senke. Das `BufferChanged`-Event bleibt ausschließlich das Rendering-Signal.

**Typ:** `EventHandler?`

**Beispiel:**
```csharp
session.BufferChanged += (_, _) => Dispatcher.InvokeAsync(InvalidateVisual);
```

#### `RuntimeStatusChanged`

Wird ausgelöst, wenn sich der Betriebszustand der CLI ändert (z.B. von `Laeuft` zu `WartetAufEingabe` oder `Inaktiv`). Dies ermöglicht der UI, visuelle Indikatoren wie "CLI wird ausgeführt" oder "CLI wartet auf Eingabe" anzuzeigen.

**Typ:** `EventHandler<CliRuntimeStatusChangedEventArgs>`

**EventArgs:** `Status` (vom Typ `CliRuntimeStatus`)

**Beispiel:**
```csharp
session.RuntimeStatusChanged += (_, args) =>
{
    if (args.Status == CliRuntimeStatus.WartetAufEingabe)
        StatusBar.Text = "Warte auf Eingabe...";
};
```

#### `OutputChunk`

Wird pro gelesenem Roh-Chunk ausgelöst — **vor** der ANSI-Parser-Verarbeitung und nach der Meldung an die `ITerminalOutputSink`. Trägt die unveränderten Ausgabebytes.

**Typ:** `EventHandler<TerminalOutputChunkEventArgs>`

**EventArgs:** `Data` (`ReadOnlyMemory<byte>` — unveränderter Roh-Chunk; der Lesepuffer wird wiederverwendet, Inhalte ggf. kopieren)

#### `Exited`

Wird bei Beendigung des Prozesses genau einmal ausgelöst. Der Exit-Code wird PID-wiederverwendungs-sicher über `GetExitCodeProcess` auf dem nativen Prozess-Handle (ConPTY-Backend) bzw. `Process.ExitCode` (Pipe-Backend) ermittelt. Ein Ende des Output-Streams bei noch laufendem Prozess löst das Event nicht aus.

**Typ:** `EventHandler<TerminalSessionExitedEventArgs>`

**EventArgs:** `ExitCode` (`int?` — `null`, wenn nicht ermittelbar)

#### `Failed`

Wird bei einem fatalen Laufzeitfehler der Session ausgelöst (z. B. Leseschleifen-Exception, defekte Pipe). `KiAusfuehrungsService` behandelt `Failed` wie einen Exit ohne Code → Status `Fehler`.

**Typ:** `EventHandler<TerminalSessionFailedEventArgs>`

**EventArgs:** `Error` (`Exception`), `Phase` (`string`, z. B. `ReadLoop`, `Write`)

### Methoden

#### `Resize(int cols, int rows)`

Ändert die Größe der Pseudo Console und des zugrunde liegenden Terminal-Puffers. Identische Dimensionsaufrufe werden dedupliziert und parallele Aufrufe serialisiert — kein doppelter oder konkurrierender PTY-Call. Auf dem Pipe-Backend ist die Größenänderung eine No-Op (`NullPseudoConsoleHandle`).

**Parameter:**
- `cols`: Neue Spaltenanzahl (muss > 0 sein)
- `rows`: Neue Zeilenanzahl (muss > 0 sein)

**Rückgabe:** `bool` — `true` wenn erfolgreich oder bereits dem aktuellen Zustand entsprechend, `false` bei Fehler (z.B. ungültige Parameter)

**Beispiel:**
```csharp
if (session.Resize(120, 30))
    Console.WriteLine("Resized to 120x30");
```

#### `MarkOutputActivity()`

Meldet, dass die CLI Ausgabe produziert hat. Setzt intern `RuntimeStatus` auf `Laeuft` (falls nicht bereits). Wird automatisch von der Leseschleife aufgerufen, kann aber auch manuell aufgerufen werden, um Aktivität zu signalisieren.

**Beispiel:**
```csharp
session.MarkOutputActivity();
```

#### `MarkInputActivity()`

Meldet, dass eine Benutzereingabe versendet wurde. Setzt intern `RuntimeStatus` auf `Laeuft` (falls nicht bereits). Kann manuell aufgerufen werden, wenn Eingaben außerhalb der Standard-Keyboard-Handler versendet werden.

**Beispiel:**
```csharp
session.MarkInputActivity();
```

#### `WriteInputAsync(ReadOnlyMemory<byte> bytes, CancellationToken ct = default)`

Schreibt bereits kodierte Eingabebytes serialisiert in den Input-Stream der Sitzung. Längere Bytefolgen werden pro Session über `_inputWriteLock` serialisiert, in 4096-Byte-Chunks geschrieben, jeder Chunk abgewartet und abschließend geflusht — Reihenfolge und Vollständigkeit bleiben erhalten, auch wenn Paste, Prompt-Versand und Tastatureingaben zeitnah liegen. Auf dem Pipe-Backend übersetzt `CrSubmittingInputStream` nackte `\r` zu `\r\n`.

**Parameter:**
- `bytes`: Die zu schreibenden, bereits kodierten Eingabebytes
- `ct`: Abbruch-Token

#### `WritePromptAsync(string prompt, CancellationToken ct)`

Schreibt einen Prompt als Texteingabe inkl. abschließendem Submit (`\r`) in den Input-Stream, flusht und meldet die Eingabe über `MarkInputActivity`. Zeilenumbrüche werden einheitlich als `\r` übertragen (entspricht der Tastaturkodierung von Enter). Wird u. a. von `PromptZeitVersandService` und `ProjektleiterAgentService` (Initial-Prompt) verwendet.

**Parameter:**
- `prompt`: Der zu sendende Prompt-Text
- `ct`: Abbruch-Token

#### `DrainOutputAsync(TimeSpan timeout, CancellationToken ct = default)`

Wartet begrenzt darauf, dass die Leseschleife den Output-Stream bis zum Ende verarbeitet hat (Tail-Output landet noch in der Protokoll-Senke). Wird vor dem `Dispose` der Session im Service-Cleanup aufgerufen.

**Parameter:**
- `timeout`: Maximale Wartezeit (`<= TimeSpan.Zero` = unbegrenzt bis zum Ende der Schleife bzw. `ct`)
- `ct`: Abbruch-Token

**Rückgabe:** `Task<bool>` — `true`, wenn die Leseschleife abgeschlossen wurde; `false` bei Timeout/Abbruch

#### `RebuildBufferFromReplay()`

Baut `Buffer` synchron aus den gespeicherten `TerminalReplayBuffer`-Roh-Chunks neu auf: `TerminalBuffer.Reset()` + Neu-Parsen aller Chunks mit einem frischen `AnsiSequenceParser` unter demselben Render-Lock wie die Leseschleife. Wird von `TerminalControl.OnSessionChanged` bei der UI-Neuanbindung aufgerufen — erzeugt deterministisch denselben Endzustand ohne doppelte Ausgaben.

#### `Dispose()`

Schließt alle Ressourcen: HPCON-Handle, Input-Pipe, Output-Pipe und das native Win32-Prozess-Handle (ConPTY-Pfad). Bricht die interne Leseschleife (`ReadLoopAsync`) ab und schließt danach die Streams. Der Prozess wird **nicht** beendet (muss über `Process.Kill()` manuell beendet werden, falls erforderlich). Eine angebundene Output-Senke wird durch die Leseschleife idempotent abgeschlossen; der Service-Cleanup kann zusätzlich `CompleteAsync(...)` aufrufen, um begrenzt auf Persistenz zu warten.

**Beispiel:**
```csharp
session.Dispose();
```

## TerminalControl

WPF-`FrameworkElement` zum Rendern einer `ITerminalSession`.

### Abhängigkeiten

```xml
xmlns:controls="clr-namespace:Softwareschmiede.App.Controls"
```

### XAML-Verwendung

```xml
<controls:TerminalControl x:Name="TerminalConsole" />
```

### Dependency Properties

#### `Session`

Die aktive `ITerminalSession` zum Rendern.

**Typ:** `ITerminalSession?`

**Standard:** `null`

**Beschreibung:** Wenn gesetzt, ruft das Control zuerst `session.RebuildBufferFromReplay()` auf (deterministischer Neuaufbau des Buffers aus dem `TerminalReplayBuffer` — kein Datenverlust, keine Doppelausgaben beim Reattach), abonniert dann das `BufferChanged`-Event der Session und rendert deren `Buffer`. Die Leseschleife selbst läuft unabhängig vom Control in `PseudoConsoleSession` — sie startet bei der Konstruktion der Session und läuft weiter, auch wenn keine oder eine andere Session gebunden ist (parallele CLI-Ausführungen, Issue-86). Beim Wechsel zu einer neuen Session wird nur der `BufferChanged`-Handler der alten Session deregistriert, nicht deren Leseschleife.

**Beispiel (Code-Behind):**
```csharp
TerminalConsole.Session = terminalSession; // ITerminalSession
```

**Beispiel (Data-Binding):**
```xml
<controls:TerminalControl Session="{Binding CurrentTerminalSession}" />
```

### Ereignisse

Das Control erbt von `FrameworkElement`. Terminal-spezifische Events sind nicht exposiert; das Control abonniert intern lediglich `ITerminalSession.BufferChanged`, dessen Leseschleife im Hintergrund der Session läuft.

### Rendering

Das Control rendert den Buffer mit:
- **Schrift:** Monospace Consolas 13pt
- **Zellenbreite:** Ca. 7.5 px (hängt vom System DPI ab)
- **Zellenhöhe:** Ca. 13 px
- **Hintergrundfarbe:** Schwarz (standard Terminal-Farbe)
- **Vordergrundfarbe:** Hellgrau (ANSI Standard)
- **Attribute:** Bold, Dim, Underline (soweit unterstützt)
- **Cursor:** Halbtransparentes weißes Rechteck

Das Control implementiert `IScrollInfo` und wird in der Aufgabendetailansicht in einem vertikalen `ScrollViewer` gehostet. Dadurch meldet es dem `ScrollViewer` die logische Verlaufshöhe (`ExtentHeight`), die sichtbare Zeilenhöhe (`ViewportHeight`) und den aktuellen zeilenbasierten `VerticalOffset`.

Unterstütztes Scrollverhalten:
- Vertikale Scrollbar bei mehr Ausgabe als sichtbarer Höhe
- Mausrad-Scroll um mehrere Terminalzeilen
- Line-Scroll um eine Terminalzeile
- Page Up/Page Down um ungefähr eine sichtbare Seite
- Horizontaler UI-Scroll nur bei Sessions mit fixierter Geometrie (`SupportsResize == false`, z. B. `TerminalReplaySession`): `ExtentWidth` beträgt dann `Buffer.Cols × Zellbreite` (Pixel-Einheiten), `SetHorizontalOffset`/`Line*`/`Page*`/`MouseWheel*` verschieben den horizontalen Offset und `OnRender` zeichnet via Clip+TranslateTransform nur den sichtbaren Spaltenbereich. Bei Live-Sessions liegt `Cols × Zellbreite` per `floor(ActualWidth/Zellbreite)`-Arithmetik nie über dem Viewport — dort bleibt der Extent deckungsgleich und kein horizontaler Balken entsteht (in `TaskDetailView` ist die horizontale Scrollbar zusätzlich `Disabled`).
- **Alternate Screen:** Solange `buffer.IsAlternateScreenActive` gesetzt ist (Vollbild-TUI via CSI `?1049h`), meldet `IScrollInfo` keinen Scrollback-Bereich (`ExtentHeight == ViewportHeight`), der Offset wird auf 0 geklemmt und alle Scroll-Operationen sind No-Ops

Wenn der Offset am Ende des Verlaufs steht, folgt das Control neuer Ausgabe automatisch. Nach manuellem Hochscrollen bleibt die Position stabil, bis wieder ans Ende gescrollt wird. Eingabefokus und Tastaturweitergabe bleiben erhalten; Klicks in die Terminalfläche fokussieren `TerminalControl`, Scrollbar-Klicks werden nicht abgefangen.

### Tastatureingaben

Das Control fängt `PreviewKeyDown`- und `TextInput`-Events ab und konvertiert sie via `KeyToVt100Encoder` zu VT100-Sequenzen, die in `Session.InputStream` geschrieben werden.

Unterstützte Tasten:
- ASCII-Zeichen (a-z, A-Z, 0-9, Sonderzeichen)
- Pfeiltasten (↑↓←→) → `\x1b[A`, `\x1b[B`, `\x1b[C`, `\x1b[D`
- F1–F12 → `\x1b[11~` bis `\x1b[24~`
- Pos1, Ende, PgUp, PgDn → entsprechende Escape-Sequenzen
- Enter → `\r`
- Backspace → `\x08`
- Delete → `\x1b[3~`
- Tab → `\t`
- Escape → `\x1b`
- Ctrl+C → `\x03` (SIGINT)
- Ctrl+Z → `\x1a` (SIGTSTP)

### Größenänderungen

Das Control triggert automatisch `Session.Resize(cols, rows)` bei Layout-Änderungen — nur bei `Session.SupportsResize == true`. Bei fixierter Geometrie (`false`) bleiben Buffer und Session in ihrer Geometrie; das Control aktualisiert dann lediglich Scrollinfo (Viewport/Extent, Klemmung des horizontalen Offsets) und Darstellung. Die neuen Spalten- und Zeilenzahlen werden aus verfügbaren Pixeln und Zellengröße berechnet; die Session dedupliziert identische Dimensionen selbst.

### Clipboard-Paste-Support

Das Control fängt `Ctrl+V`-Eingaben ab und verarbeitet sie über die neuen privaten Methoden `GetClipboardText()` und `ReadClipboardAndInsertAsync()`. Dies ermöglicht die direkte Einfügung von Zwischenablage-Text in die CLI.

**Verhalten:**
- `Ctrl+V` wird von `OnPreviewKeyDown` abgefangen
- Text wird aus `System.Windows.Clipboard.GetText()` gelesen
- Text wird via `KeyToVt100Encoder.EncodeClipboardText(text)` normalisiert und kodiert
- Normalisierte Bytes werden asynchron in `Session.InputStream` geschrieben
- `Session.MarkInputActivity()` wird aufgerufen
- Fehler werden per `ILogger` protokolliert, blockieren nicht

## KeyToVt100Encoder

Statische Klasse zur Konvertierung von Tastaturereignissen und Text in VT100-Byte-Sequenzen.

### Methoden (öffentlich)

#### `Encode(KeyEventArgs e)`

Konvertiert ein WPF-Tastaturereignis in eine VT100-Byte-Sequenz.

**Parameter:**
- `e`: Das WPF-Tastaturereignis

**Rückgabe:** `byte[]?` — VT100-Byte-Sequenz, oder `null` wenn das Zeichen über `TextInput` übermittelt werden soll

**Unterstützte Tasten:**
- Ctrl+A bis Ctrl+Z: ASCII-Kontrollcodes (0x01–0x1A)
- Enter: `0x0D` (CR)
- Backspace: `0x7F` (DEL)
- Tab: `0x09`
- Escape: `0x1B`
- Pfeiltasten: `\x1b[A`, `\x1b[B`, `\x1b[C`, `\x1b[D`
- F1–F12: `\x1b[11~` bis `\x1b[24~`
- Delete: `\x1b[3~`
- Pos1/Ende/PgUp/PgDn: entsprechende Escape-Sequenzen

**Beispiel:**
```csharp
var bytes = KeyToVt100Encoder.Encode(keyEventArgs);
if (bytes != null)
    await session.InputStream.WriteAsync(bytes);
```

#### `EncodeText(string text)`

Kodiert normalen Text als UTF-8-Byte-Array.

**Parameter:**
- `text`: Der zu kodierende Text

**Rückgabe:** `byte[]` — UTF-8-kodierte Bytes

**Beispiel:**
```csharp
var bytes = KeyToVt100Encoder.EncodeText("Hello");
```

#### `EncodeClipboardText(string? text)`

Kodiert Zwischenablage-Text für die CLI-Eingabe: Zeilenumbrüche werden einheitlich normalisiert, das Ergebnis wird als UTF-8 kodiert.

**Parameter:**
- `text`: Der zu kodierende Zwischenablage-Text (oder `null`)

**Rückgabe:** `byte[]` — UTF-8-kodierte, newline-normalisierte Bytes, oder leeres Array bei `null`/leerem Text

**Newline-Normalisierung:**
- `\n` (LF) → `\r` (CR)
- `\r\n` (CRLF) → `\r` (CR) — das `\n` wird übersprungen
- `\r` (CR) → `\r` (CR) — bleibt unverändert

**Hintergrund:** Windows-CLIs erwarten `\r` (Carriage Return) als Zeilenumbruch-Zeichen; Multi-line-Text aus der Zwischenablage kann aber verschiedene Newline-Formate haben (LF von Unix, CRLF von Windows). Diese Methode normalisiert alle Varianten zu `\r`, um Kompatibilität zu gewährleisten.

**Beispiel:**
```csharp
var textWithUnixNewlines = "line1\nline2\nline3";
var bytes = KeyToVt100Encoder.EncodeClipboardText(textWithUnixNewlines);
// Ergebnis: UTF-8-Bytes von "line1\rline2\rline3"
await session.InputStream.WriteAsync(bytes);
```

## KiAusfuehrungsService

Zentrale Service-Klasse für Prozess-Lifecycle.

### Methoden (öffentlich)

#### `StartTerminalSessionAsync(Guid aufgabeId, IKiPlugin kiPlugin, string localRepoPath, string? optionalParameters = null, CancellationToken ct = default, RepositoryStartKonfiguration? startConfig = null, IGitPlugin? gitPlugin = null)`

Startet einen KI-CLI-Prozess als interaktive Terminal-Session — über die Pseudo Console API (ConPTY) oder, diagnostiziert, über das Pipe-Fallback-Backend.

**Parameter:**
- `aufgabeId`: Eindeutige Aufgaben-ID
- `kiPlugin`: Plugin-Instanz (liefert die Spec über `IKiPlugin.GetTerminalStartSpecAsync`; `TerminalCapabilities` steuert die Backend-Eignung)
- `localRepoPath`: Arbeitsverzeichnis des Prozesses
- `optionalParameters`: Optionale CLI-Argumente
- `ct`: Cancellation Token
- `startConfig`: Optionale Startkonfiguration des Repositories (Arbeitsverzeichnis-Auflösung)
- `gitPlugin`: Optionales Git-Plugin zur Auflösung des tatsächlichen Repository-Pfads

**Rückgabe:** `Task<CliProcessHandle>` — `CliProcessHandle.Session` trägt die `ITerminalSession`, `OutputSink` den Protokoll-Writer

**Interner Ablauf:** Die Spec wird über `kiPlugin.GetTerminalStartSpecAsync` geholt und zusammen mit `kiPlugin.CheckHealthAsync` (als `healthCheck`-Delegate) an `ITerminalSessionFactory.StartAsync` gereicht. `TerminalSessionService` löst die Executable auf (`TerminalExecutableResolver`), führt den Preflight aus (`TerminalSessionDiagnostics`), wählt das Backend und startet den Prozess direkt über den jeweiligen `IPseudoConsoleProcessLauncher` — ohne `cmd.exe`-Zwischenschale. `session.Exited`/`session.Failed` sind auf `HandleSessionEndedAsync`/`HandleSessionFailedAsync` verdrahtet.

**Output-Protokollierung:** Für jeden Session-Start erzeugt der Service einen `CliOutputProtokollWriter`, reicht ihn als `ITerminalOutputSink` an die Factory/den Launcher weiter und hält ihn im `CliProcessHandle.OutputSink`. Der Writer speichert Ausgabezeilen über `ProtokollService.AddCliOutputAsync` als `ProtokollTyp.CliOutput`. Fehler- und Fallback-Fälle schreiben zusätzlich eine `[Terminal-Diagnose]`-Markerzeile mit den Preflight-Ergebnissen in dasselbe Protokoll.

**Rohbyte-Mitschnitt:** Ist `TerminalSessionOptions.AufzeichnungByteBudget > 0` (Default 8 MB), erzeugt der Service zusätzlich einen `CliOutputRecorder` und übergibt beide Senken als `CompositeTerminalOutputSink`; der Recorder wird in `_aufzeichnungen` registriert (Registry auf die letzten `MaxAufzeichnungenAnzahl = 8` Aufgaben begrenzt — siehe `GetCliAufzeichnung`). Bei `AufzeichnungByteBudget <= 0` bleibt es beim Protokoll-Writer allein.

**Exceptions:**
- `ArgumentException`: `TerminalSessionStartSpec.FileName` leer
- `InvalidOperationException`: Executable nicht auffindbar/nicht ausführbar (`NotFound`/`NotExecutable`), `RequiresPty`-Plugin ohne verfügbare PTY, `CreatePseudoConsole` fehlgeschlagen oder Plugin-Fehler

**Beispiel:**
```csharp
var handle = await _kiService.StartTerminalSessionAsync(
    taskId, 
    codexPlugin, 
    "C:\\repos\\my-project",
    "--verbose",
    cancellationToken
);
```

#### `GetTerminalSession(Guid aufgabeId)`

Gibt die aktive `ITerminalSession` für eine Aufgabe zurück, falls eine läuft.

**Parameter:**
- `aufgabeId`: Aufgaben-ID

**Rückgabe:** `ITerminalSession?` (null, falls kein Prozess läuft oder der klassische Pipe-Start ohne Session verwendet wurde)

**Beispiel:**
```csharp
var session = _kiService.GetTerminalSession(taskId);
if (session != null)
{
    session.Resize(100, 25);
}
```

#### `GetCliAufzeichnung(Guid aufgabeId)`

Gibt den Rohbyte-Mitschnitt der letzten Terminal-Session einer Aufgabe zurück — auch nach dem Session-Ende abrufbar, da der Recorder-Eintrag das `CliProcessHandle` überlebt. Die Registry ist auf die letzten `MaxAufzeichnungenAnzahl = 8` Aufgaben begrenzt (ältere Mitschnitte werden verworfen; ein Neustart derselben Aufgabe zählt als jüngster Eintrag).

**Parameter:**
- `aufgabeId`: Aufgaben-ID

**Rückgabe:** `CliOutputAufzeichnung?` — Snapshot mit Header-Metadaten und den aufgezeichneten Chunks; `null`, wenn keine Aufzeichnung existiert (kein Session-Start oder Mitschnitt via `AufzeichnungByteBudget <= 0` deaktiviert)

#### `StopAsync(Guid aufgabeId)`

Beendet den laufenden Prozess für eine Aufgabe.

**Parameter:**
- `aufgabeId`: Aufgaben-ID

**Rückgabe:** `Task`

**Exceptions:**
- `KeyNotFoundException`: Keine aktive Aufgabe mit dieser ID

## Events

### TaskDetailViewModel.TerminalSessionGestartet

Wird gefeuert, nachdem `KiAusfuehrungsService.StartTerminalSessionAsync` erfolgreich abgeschlossen wurde (oder bei erneutem Binden einer bereits laufenden Session).

**Typ:** `Action<ITerminalSession>?`

**Parameter:** Die neu erstellte bzw. laufende `ITerminalSession`

**Verwendung:**
```csharp
taskViewModel.TerminalSessionGestartet += session =>
{
    TerminalConsole.Session = session;
};
```

### KiAusfuehrungsService.CliProcessStatusChanged

Wird gefeuert, wenn der Prozess seine Zustand ändert (z.B. startet, stoppt, fehlgeschlagen).

**Typ:** `event EventHandler<CliProcessStatusChangedEventArgs>`

**EventArgs:**
- `AufgabeId`: Betroffene Aufgaben-ID
- `Status`: Neuer Status (Gestartet, Gestoppt, Fehler)

## ITerminalOutputSink

Optionale Schnittstelle für rohe Terminal-Ausgaben einer `PseudoConsoleSession`.

### Methoden

#### `OnOutputChunk(ReadOnlySpan<byte> bytes)`

Wird durch `PseudoConsoleSession.ReadLoopAsync` für jeden gelesenen Output-Chunk aufgerufen. Implementierungen müssen die benötigten Bytes sofort kopieren, weil der Lesepuffer wiederverwendet wird.

#### `Complete()`

Schließt die Senke idempotent ab und flusht ausstehende Restdaten. Diese Methode darf mehrfach aufgerufen werden.

#### `CompleteAsync(TimeSpan timeout, CancellationToken ct = default)`

Schließt die Senke ab und wartet begrenzt auf die Persistenz bereits angenommener Daten.

## ITerminalDiagnoseSink

Zusätzlicher, optionaler Routing-Kanal einer `ITerminalOutputSink` für `[Terminal-Diagnose]`-Markerzeilen (keine echte CLI-Ausgabe).

### Methoden

#### `OnDiagnoseChunk(ReadOnlySpan<byte> bytes)`

Wird von `TerminalSessionService.WriteDiagnosis` aufgerufen, wenn die übergebene Senke das Interface implementiert — sonst erhalten Senken Marker weiter über `OnOutputChunk` (Rückwärtskompatibilität). Senken, die byte-exakte Mitschnitte der CLI-Ausgabe erstellen (`CliOutputRecorder`), implementieren das Interface bewusst **nicht**, damit die artefaktischen Markerzeilen nicht im Mitschnitt landen.

## CompositeTerminalOutputSink

`ITerminalOutputSink` + `ITerminalDiagnoseSink`, die eine Senke auf mehrere innere Senken auffächert.

|| Member | Verhalten |
||--------|-----------|
|| Konstruktor | `CompositeTerminalOutputSink(params ITerminalOutputSink[] inner)` — innere Senken in Aufrufreihenfolge |
|| `OnOutputChunk` | Ruft `OnOutputChunk` aller inneren Senken |
|| `OnDiagnoseChunk` | Ruft `OnDiagnoseChunk` nur der inneren Senken auf, die `ITerminalDiagnoseSink` implementieren |
|| `Complete`/`CompleteAsync` | Schließt alle inneren Senken der Reihe nach ab |

Wird in `KiAusfuehrungsService.StartTerminalSessionAsync` eingesetzt, um `CliOutputProtokollWriter` + `CliOutputRecorder` parallel zu betreiben (bei `AufzeichnungByteBudget <= 0` entfällt sie — der Protokoll-Writer wird direkt übergeben).

## CliOutputProtokollWriter

Implementiert `ITerminalOutputSink` und `ITerminalDiagnoseSink` für Aufgabenläufe (`OnDiagnoseChunk` leitet auf denselben Zeilen-Accumulator-Pfad wie `OnOutputChunk` — `[Terminal-Diagnose]`-Marker erscheinen weiterhin im Aufgabenprotokoll).

| Merkmal | Verhalten |
|---------|-----------|
| Zuordnung | Ein Writer gehört genau zu einer `aufgabeId` |
| Zeilenbildung | `CliOutputLineAccumulator` dekodiert UTF-8 über Chunk-Grenzen und trennt auf LF, CRLF und einzelnes CR |
| Queue | Bounded Channel mit `QueueCapacity = 4096`; bei voller Queue wartet der Output-Reader auf freie Kapazität |
| Persistenz | Hintergrund-Worker schreibt sequenziell über `ProtokollService.AddCliOutputAsync` |
| Fehler | Persistenzfehler werden geloggt und nicht in die Terminal-Session zurückgeworfen |

## AnsiSequenceParser

Zustandsbehafteter ANSI-Escape-Sequenz-Parser.

### Methoden (öffentlich)

#### `Parse(ReadOnlySpan<byte> data)`

Zerlegt einen Byte-Block in `TerminalEvent`-Instanzen.

**Parameter:**
- `data`: Rohe Bytes aus Prozess-Output

**Rückgabe:** `IEnumerable<TerminalEvent>`

**Zustand:** Der Parser ist zustandsbehaftet; unvollständige Sequenzen über Paket-Grenzen werden korrekt zusammengesetzt. Die UTF-8-Dekodierung läuft über einen persistenten `System.Text.Decoder` (`flush: false`) — Mehrbyte-Zeichen, die eine Chunk-Grenze überschreiten, bleiben im Decoder-Zustand und werden mit den Restbytes des nächsten Chunks dekodiert (kein U+FFFD-Ersatzzeichen-Zerfall). `Reset()` setzt die Zustandsmaschine inklusive Decoder-Übertrag zurück (wird beim Replay-Neuaufbau über einen frischen Parser erreicht).

**Unterstützte Sequenzen:**
- **Plaintext:** Klartext → `TextWrittenEvent`; Steuerzeichen `\r` (Spalte 0), `\n`/`\r\n` (Zeilenvorschub + Spalte 0), `\x08`/`\b` (Cursor links), `\t` (nächster Tab-Stopp à 8 Spalten), BEL (überlesen)
- **SGR (Select Graphic Rendition):** `\x1b[{codes}m`
  - `0`: Reset (Standard-Farben)
  - `1`: Bold
  - `2`: Dim
  - `4`: Underline
  - `22`: Normal (kein Bold/Dim)
  - `24`: Underline aus
  - `30-37`: 3-bit Vordergrund-Farben
  - `38;5;{n}`: 8-bit Vordergrund-Farbe
  - `38;2;{r};{g};{b}`: 24-bit Vordergrund-Farbe
  - `40-47`: 3-bit Hintergrund-Farben
  - `48;5;{n}`: 8-bit Hintergrund-Farbe
  - `48;2;{r};{g};{b}`: 24-bit Hintergrund-Farbe
- **Cursor-Bewegung:** `\x1b[{row};{col}H` / `\x1b[f` → `CursorMovedEvent` (1-basiert → 0-basiert)
  - `\x1b[A`, `\x1b[B`, `\x1b[C`, `\x1b[D`, `\x1b[e`, `\x1b[a`: Relative Bewegung
  - `\x1b[E` / `\x1b[F`: CNL/CPL (n Zeilen runter/rauf + Spalte 0)
  - `\x1b[G` / ``\x1b[` ``: CHA/HPA (absolute Spalte), `\x1b[d`: VPA (absolute Zeile)
- **Clear/Erase:** `\x1b[{n}J`, `\x1b[{n}K` → `ScreenClearedEvent`, `LineErasedEvent`
- **Insert/Delete:** `\x1b[{n}L` → `LinesInsertedEvent`, `\x1b[{n}M` → `LinesDeletedEvent`, `\x1b[{n}@` → `CharsInsertedEvent`, `\x1b[{n}P` → `CharsDeletedEvent`, `\x1b[{n}X` → `CharsErasedEvent`
- **Scrolling:** `\x1b[{n}S`/`T` → `ScreenScrolledEvent` (DeltaRows positiv/negativ); `\x1b[{t};{b}r` → `ScrollRegionChangedEvent` (DECSTBM)
- **Alternate Screen / private Modi:** `\x1b[?1049h`/`l` → `AlternateScreenChangedEvent` (inkl. Save/Restore-Cursor-Anteil), `\x1b[?1047h`/`l` → `AlternateScreenChangedEvent`, `\x1b[?1048h`/`l` → `CursorSavedEvent`, `\x1b[?25h`/`l` → `CursorVisibilityChangedEvent`
- **Cursor Save/Restore:** `ESC 7`/`ESC 8`, `\x1b[s`/`\x1b[u` → `CursorSavedEvent(Restored: false/true)`
- **Reset:** `ESC c` (RIS) → `TerminalResetEvent`
- **String-Sequenzen:** OSC (`ESC ]`) sowie DCS (`ESC P`), SOS (`ESC X`), PM (`ESC ^`), APC (`ESC _`) werden bis BEL oder ST (`ESC \`) still übersprungen — ihre Payloads erscheinen nicht als Text
- **Abbruch unvollständiger Sequenzen:** Trifft ein `ESC` mitten in einer CSI- oder String-Sequenz ein, wird die begonnene Sequenz verworfen und das `ESC` beginnt regulär eine neue Sequenz (robust gegenüber Sequenz-Abbrüchen des Senders und Chunk-Grenzen)
- **Zeichensatz-Sequenzen** (`ESC (` …) und unbekannte Sequenzen: werden still überlesen

**Beispiel:**
```csharp
var parser = new AnsiSequenceParser();
var bytes = Encoding.UTF8.GetBytes("Hello \x1b[31mRed\x1b[0m");
foreach (var evt in parser.Parse(bytes))
{
    buffer.Apply(evt);
}
```

## TerminalBuffer

Zustandsbehafteter Terminal-Zustand (Grid, Cursor, Farben).

### Methoden (öffentlich)

#### `Apply(TerminalEvent evt)`

Wendet ein Terminal-Event auf den Buffer an.

**Parameter:**
- `evt`: Event-Instanz (`TextWrittenEvent`, `CursorMovedEvent`, etc.)

**Nebeneffekte:** Ändert interner Zustand (Grid, Cursor, Attribute)

**Thread-Sicherheit:** Methode ist intern synchronisiert via `lock`

**Beispiel:**
```csharp
buffer.Apply(new TextWrittenEvent("Hello "));
buffer.Apply(new ColorChangedEvent { Foreground = Color.Red });
buffer.Apply(new TextWrittenEvent("World"));
```

#### `Resize(int cols, int rows)`

Ändert Grid-Größe. Erhält sichtbare Zeilen; neue Zeilen werden initialisiert. Berücksichtigt ein aktives Alternate-Screen-Grid.

**Parameter:**
- `cols`: Neue Spaltenanzahl
- `rows`: Neue Zeilenanzahl

**Thread-Sicherheit:** Intern synchronisiert

#### `Reset()`

Setzt Grid, Scrollback, Cursor und den Alternate-Screen-Zustand vollständig zurück. Wird von `PseudoConsoleSession.RebuildBufferFromReplay()` vor dem Neu-Parsen der Replay-Chunks aufgerufen.

#### `GetSnapshot()`

Erstellt einen konsistenten Snapshot des aktuellen Buffer-Zustands unter einem einzigen Lock. Wird von Render- und Scroll-Operationen genutzt, um Race Conditions zwischen paralleler Buffer-Aktualisierung und Lesezugriffen zu vermeiden. Bei aktivem Alternate Screen liefert der Snapshot nur das Alt-Grid — ohne Scrollback-Präfix.

**Rückgabe:** `TerminalBufferSnapshot` (Record mit Grid-Kopie, Scrollback-Zeilen, Rows, Cols, CursorRow, CursorCol, ScrollbackCount und TotalRows)

**Thread-Sicherheit:** Intern synchronisiert; der Snapshot ist konsistent unter dem Lock erstellt

**Beispiel:**
```csharp
var snapshot = buffer.GetSnapshot();
var gridCopy = snapshot.Grid;
var scrollbackRows = snapshot.ScrollbackRows;
var cursorRow = snapshot.CursorRow;
// Render-Operationen ohne Lock-Contention
for (var logicalRow = 0; logicalRow < snapshot.TotalRows; logicalRow++)
{
    // Zeilen 0..ScrollbackCount-1 stammen aus dem Scrollback,
    // danach folgt das sichtbare Grid.
}
```

#### Eigenschaften (read-only)

- `Rows`: Aktuelle Zeilenanzahl
- `Cols`: Aktuelle Spaltenanzahl
- `CursorRow`: Cursor-Zeile (0-basiert)
- `CursorCol`: Cursor-Spalte (0-basiert)
- `IsAlternateScreenActive`: `true`, solange der Alternate Screen aktiv ist (zwischen CSI `?1049h`/`?1047h` und `?1049l`/`?1047l`); das Alt-Grid hat keinen Scrollback
- `ScrollbackCount` (internal): Anzahl der aktuell im Scrollback-Ringpuffer gehaltenen Zeilen. Diese Eigenschaft ist nur für Tests sichtbar (interne API).

#### Verarbeitete `TerminalEvent`-Records

`TextWrittenEvent`, `CursorMovedEvent`, `CursorMovedRelativeEvent`, `ColorChangedEvent`, `ScreenClearedEvent`, `LineErasedEvent`, `CursorVisibilityChangedEvent`, `AlternateScreenChangedEvent(bool Enabled)`, `LinesInsertedEvent`, `LinesDeletedEvent`, `CharsInsertedEvent`, `CharsDeletedEvent`, `CharsErasedEvent`, `ScrollRegionChangedEvent(int Top, int Bottom)`, `ScreenScrolledEvent(int DeltaRows)`, `CursorSavedEvent(bool Restored)`, `TerminalResetEvent`.

## ITerminalSessionFactory / TerminalSessionService

Zentrale Erzeugung von Terminal-Sessions: löst die vom Plugin gelieferte `TerminalSessionStartSpec` auf (`TerminalExecutableResolver`), führt die Preflight-Diagnose aus (`TerminalSessionDiagnostics`), wählt das Backend (PTY oder diagnostizierter Pipe-Fallback) und delegiert den eigentlichen Start an den jeweiligen `IPseudoConsoleProcessLauncher`. In der DI als `ITerminalSessionFactory` (Singleton) registriert; die Implementierung erhält beide Launcher als konkrete Typen (`Win32PseudoConsoleProcessLauncher` = `ptyLauncher`, `SimulatedPseudoConsoleProcessLauncher` = `pipeLauncher`).

### Methoden

#### `StartAsync(Guid aufgabeId, TerminalSessionStartSpec spec, ITerminalOutputSink? outputSink, Func<CancellationToken, Task<bool>>? healthCheck, CancellationToken ct)`

Startet eine Terminal-Session aus der gelieferten Spec.

**Parameter:**
- `aufgabeId`: Aufgaben-ID (Logging/Protokoll)
- `spec`: Die vom Plugin gelieferte Startbeschreibung (`IKiPlugin.GetTerminalStartSpecAsync`)
- `outputSink`: Optionale Senke für Terminal-Ausgabe (Protokoll-Pfad); empfängt auch die `[Terminal-Diagnose]`-Markerzeilen
- `healthCheck`: Optionale Plugin-Health-Probe (`IKiPlugin.CheckHealthAsync` als Delegate); wird im Preflight nur bei erfolgreicher Executable-Auflösung aufgerufen und ist nicht fatal
- `ct`: Abbruch-Token

**Rückgabe:** `Task<TerminalSessionStartResult>` — Record mit `Process`, `ITerminalSession Session`, `bool IsPseudoTerminal`

**Exceptions:**
- `ArgumentException`: `spec.FileName` ist leer
- `ArgumentOutOfRangeException`: `TerminalSessionOptions.ReplayBufferByteBudget <= 0` oder `DefaultCols`/`DefaultRows` außerhalb `1..short.MaxValue`
- `InvalidOperationException`: Executable `NotFound`/`NotExecutable` bzw. `RequiresPty` ohne verfügbare PTY — jeweils nach dem Schreiben der `[Terminal-Diagnose]`-Markerzeile

## IPseudoConsoleProcessLauncher

Interne Backend-Naht für den eigentlichen Terminal-Prozessstart innerhalb von `TerminalSessionService`. Beide Implementierungen erhalten die bereits normalisierte Spec (keine eigene Auflösungslogik) und einen `IOptions<TerminalSessionOptions>`-Konstruktorparameter.

### Member

| Member | Typ | Beschreibung |
|--------|-----|--------------|
| `IsPseudoTerminal` | `bool` | `true` beim ConPTY-Backend (`Win32PseudoConsoleProcessLauncher`), `false` beim Pipe-Backend (`SimulatedPseudoConsoleProcessLauncher`) |
| `Start(Guid aufgabeId, TerminalSessionStartSpec spec, ITerminalOutputSink? outputSink)` | Methode | Startet die Spec direkt (`CreateProcess` mit `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE` bzw. `Process.Start` mit Stdin/Stdout/Stderr-Redirects) → `TerminalSessionStartResult` |

## TerminalExecutableResolver

Interne statische Klasse (`Softwareschmiede.Infrastructure.Terminal`): `Resolve(TerminalSessionStartSpec)` → `TerminalExecutableResolution` (`NormalizedSpec`, `TerminalExecutableStatus Status`, `ResolvedPath`, `Detail`).

- **Suchreihenfolge:** `spec.WorkingDirectory` → `PATH`-Einträge (aus `spec.EnvironmentVariables`, sonst Prozess-Umgebung); `FileName` mit Verzeichnisanteil → nur dieses Verzeichnis.
- **PATHEXT:** `spec.EnvironmentVariables["PATHEXT"]` → Prozess-`PATHEXT` → Default `.COM;.EXE;.BAT;.CMD`; `FileName` mit eigener Erweiterung wird nur literal gesucht.
- **Status `Direct`:** `.exe`/`.com`- oder endungsloser PE-Image-Treffer → `FileName` wird zum absoluten Pfad.
- **Status `CmdWrapped`:** `.cmd`/`.bat`-Treffer → `FileName = "cmd.exe"`, `Arguments = "/d /s /c \"<pfad>\" <original-args>"` (`/d` unterdrückt AutoRun).
- **Status `NotExecutable`:** Treffer mit anderer Erweiterung (z. B. `.ps1`) oder endungsloser Nicht-PE-Treffer (z. B. POSIX-Shell-Shim neben `name.cmd` — wird zugunsten der PATHEXT-Kandidaten übersprungen und nur gemeldet, wenn er der einzige Treffer bleibt).
- **Status `NotFound`:** kein Treffer.

## TerminalSessionDiagnostics

Führt den Preflight vor einem Session-Start aus (DB-frei — der `Terminal.ForcePtyUnavailable`-Test-Override wird vom `TerminalSessionService` gelesen und als `forcePtyUnavailable`-Parameter hereingereicht).

### `RunPreflightAsync(TerminalSessionStartSpec spec, TerminalExecutableResolution resolution, Func<CancellationToken, Task<bool>>? healthCheck, bool forcePtyUnavailable, CancellationToken ct)` → `TerminalPreflightResult`

Einzelchecks (als `TerminalPreflightCheck(Name, Ok, Detail)` protokolliert):

| Check | Inhalt |
|-------|--------|
| `ConPTY-Verfügbarkeit` | Windows + OS-Build ≥ 10.0.17763; bei `forcePtyUnavailable` erzwungen `Ok=false` |
| `Executable` | Aus dem Auflösungsergebnis: `Direct`/`CmdWrapped` → `Ok` mit `ResolvedPath`; `NotFound`/`NotExecutable` → `Ok=false` mit Detail |
| `CLI-Health` | `healthCheck`-Delegate, **nur** bei erfolgreicher Executable-Auflösung aufgerufen; `false`/Exception → `Ok=false` (nicht fatal — ohne Einfluss auf `BackendEmpfehlung`); sonst „übersprungen" |
| `Encoding` | Immer `Ok` (UTF-8) |
| `Terminalgröße` | `DefaultCols`/`DefaultRows` aus `TerminalSessionOptions` im Bereich `1..short.MaxValue` |
| `Pluginparameter` | `Ok` mit `spec.OptionalParameters`/`spec.Arguments` als Detail |

`TerminalPreflightResult` trägt `Checks`, `PtyVerfuegbar` und `BackendEmpfehlung` (`TerminalBackendEmpfehlung.Pty`/`Pipe`/`Fehler`): `Fehler` bei nicht auflösbarer Executable oder `RequiresPty` ohne PTY; `Pipe` bei fehlendem `SupportsPty` oder nicht verfügbarer PTY; sonst `Pty`.

## TerminalSessionStartSpec

Record im Contracts-Projekt (`Softwareschmiede.Plugin.Contracts`, `Domain/ValueObjects`) — die Plugin-gelieferte Startbeschreibung für den interaktiven Terminal-Pfad (einziger vertraglicher Spec-Weg: `IKiPlugin.GetTerminalStartSpecAsync`; bei `CliKiPluginBase`-Plugins über `BuildTerminalStartSpec` aus `BuildProcessStartInfo` gemappt).

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `FileName` | `string` | Executable-Name oder -Pfad (nackter Befehlsname, `.cmd`/`.bat`-Shim oder absoluter Pfad); Pflichtfeld |
| `Arguments` | `string` | Argumente für den Start |
| `WorkingDirectory` | `string` | Arbeitsverzeichnis des CLI-Prozesses |
| `EnvironmentVariables` | `IReadOnlyDictionary<string, string?>` | Zusätzliche Umgebungsvariablen (überschreiben die geerbte Umgebung; u. a. `PATH`/`PATHEXT` für die Auflösung) |
| `Capabilities` | `TerminalProviderCapabilities` | Terminal-Fähigkeiten der CLI (Default `SupportsPty`) |
| `PluginName` | `string` | Anzeigename des liefernden Plugins (Diagnose/Protokoll) |
| `OptionalParameters` | `string?` | Die beim Spec-Abruf übergebenen optionalen Parameter (Diagnosezwecke) |

## TerminalReplayBuffer

Begrenzter Ringpuffer roher Ausgabe-Chunks pro Session — Basis für `ITerminalSession.RebuildBufferFromReplay` bei der UI-Neuanbindung. Bewusst vom dauerhaften Sitzungsprotokoll (`CliOutputProtokollWriter`) getrennt.

### Methoden

- `Append(ReadOnlySpan<byte> chunk)` — kopiert den Chunk; bei Budget-Überschreitung werden die ältesten Chunks verworfen (ein Chunk größer als das Budget wird auf die letzten `byteBudget` Bytes gekürzt)
- `GetChunks()` → `IReadOnlyList<byte[]>` — Momentaufnahme der gehaltenen Chunks in Eingangsreihenfolge

**Konstruktor:** `TerminalReplayBuffer(int byteBudget)` — `byteBudget <= 0` → `ArgumentOutOfRangeException`.

## TerminalSessionOptions

Laufzeitparameter der Terminal-Integration; gebunden aus der `appsettings.json`-Sektion `Terminal` (`TerminalSessionOptions.SectionName`) via `services.Configure<TerminalSessionOptions>(...)` in `App.xaml.cs`; konsumiert über `IOptions<TerminalSessionOptions>` in `TerminalSessionService` und beiden Launcher-Typen.

| Eigenschaft | Typ | Standard | Beschreibung |
|-------------|-----|----------|--------------|
| `ReplayBufferByteBudget` | `int` | `524288` (512 KiB) | Byte-Budget des `TerminalReplayBuffer` pro Session |
| `DefaultCols` | `int` | `220` | Initiale Spaltenanzahl (ConPTY-Erstellung, Preflight-Check) |
| `DefaultRows` | `int` | `50` | Initiale Zeilenanzahl |
| `AufzeichnungByteBudget` | `int` | `8388608` (8 MB) | Byte-Budget des `CliOutputRecorder`-Mitschnitts pro Session; `<= 0` deaktiviert die Aufzeichnung (kein Recorder, keine Composite-Senke) |

## Enums

### `CliRuntimeStatus`

Betriebszustand einer aktiven CLI-Sitzung:

| Wert | Beschreibung |
|------|--------------|
| `Inaktiv` | Kein laufender CLI-Prozess ist aktiv. |
| `Laeuft` | Die CLI läuft und hat kürzlich Ausgabe oder Eingabe verarbeitet. |
| `WartetAufEingabe` | Die CLI läuft, erzeugt aber seit längerer Zeit (Standard: 4 Sekunden) keine Ausgabe und wartet vermutlich auf Benutzereingabe. |

Der Status wird automatisch alle 1 Sekunde neu bewertet und das `RuntimeStatusChanged`-Event wird ausgelöst, falls sich der Status geändert hat.

### `TerminalProviderCapabilities`

`[Flags]`-Enum im Contracts-Projekt (`Softwareschmiede.Domain.Enums`) — deklarativ pro Plugin über `IKiPlugin.TerminalCapabilities` (virtuell in `CliKiPluginBase`, Default `SupportsPty`); steuert die Backend-Wahl im `TerminalSessionService`.

| Wert | Beschreibung |
|------|--------------|
| `None = 0` | Keine besonderen Terminal-Fähigkeiten deklariert |
| `SupportsPty = 1` | Die CLI kann in einer Pseudo Console (ConPTY/PTY) betrieben werden |
| `RequiresPty = 2` | Die CLI benötigt eine echte Pseudo Console und verweigert/versagt ohne TTY → kein Pipe-Fallback erlaubt (harter Fehler mit Diagnose stattdessen) |

Festgelegte Werte: `ClaudeCliPlugin`, `CodexPlugin`, `GitHubCopilotPlugin`, `DevinPlugin` → `RequiresPty | SupportsPty`; `KiSimulatorPlugin` (E2E-/Test-Anbieter) → Default `SupportsPty`.

### `TerminalExecutableStatus`

Status der Executable-Auflösung (`TerminalExecutableResolution.Status`):

| Wert | Beschreibung |
|------|--------------|
| `Direct` | `.exe`/endungsloser PE-Image-Treffer — `FileName` ist der aufgelöste absolute Pfad |
| `CmdWrapped` | `.cmd`/`.bat`-Ziel — Spec wurde zu `cmd.exe /d /s /c "<pfad>"` normalisiert |
| `NotFound` | Kein Treffer in `WorkingDirectory`/`PATH` |
| `NotExecutable` | Treffer gefunden, aber nicht per `CreateProcess` ausführbar (z. B. `.ps1`, endungsloses Shell-Shim) |

### `TerminalBackendEmpfehlung`

Backend-Empfehlung aus dem Preflight: `Pty`, `Pipe`, `Fehler`.

## Konstanten

| Konstante | Wert | Beschreibung |
|-----------|------|--------------|
| `TerminalBuffer.MaxScrollbackLines` | 1000 | Maximale Scrollback-Puffer-Größe in Zeilen |
| `TerminalControl.FontSize` | 13.0 | Schriftgröße (Punkt) für Rendering |
| `TerminalSessionOptions.ReplayBufferByteBudget` | 524288 (512 KiB) | Byte-Budget des Replay-Puffers pro Session (`appsettings.json`-Sektion `Terminal`) |
| `TerminalSessionOptions.DefaultCols`/`DefaultRows` | 220 / 50 | Initiale Terminalgröße beim Session-Start |
| `TerminalSessionOptions.AufzeichnungByteBudget` | 8388608 (8 MB) | Byte-Budget der Rohbyte-Aufzeichnung pro Session; `<= 0` deaktiviert den Mitschnitt |
| `KiAusfuehrungsService.MaxAufzeichnungenAnzahl` | 8 | Maximale Anzahl vorgehaltener CLI-Aufzeichnungen (ältere werden verworfen) |
| `TerminalSessionService.ForcePtyUnavailableKey` | `"Terminal.ForcePtyUnavailable"` | `AppEinstellungen`-Schlüssel (Debug-/Test-Hook): `"true"` erzwingt `PtyVerfuegbar=false` im Preflight |
| `TerminalSessionService.TestDatenbankPfadVariable` | `"SOFTWARESCHMIEDE_TEST_DB_PATH"` | Umgebungsvariable, die den E2E-Testmodus kennzeichnet (erzwingt Pipe-Backend) |
| `KiAusfuehrungsService.ConPtyOutputDrainTimeout` | 2 Sekunden | Wartezeit auf `DrainOutputAsync` der Session im Cleanup |
| `KiAusfuehrungsService.CliOutputWriterDrainTimeout` | 2 Sekunden | Wartezeit auf `OutputSink.CompleteAsync` im Cleanup |
| `AnsiSequenceParser` | — | Kein Schwellenwert; alle Standard-Sequenzen werden geparst |

## CliOutputRecorder

`ITerminalOutputSink`, die die Rohbytes einer Terminal-Session mit Zeitstempel pro Chunk im Speicher aufzeichnet (Diagnose-Mitschnitt für das Konsolentestfenster). Implementiert `ITerminalDiagnoseSink` bewusst **nicht** — `[Terminal-Diagnose]`-Markerzeilen bleiben aus dem byte-exakten Mitschnitt ausgeschlossen.

**Konstruktor:** `CliOutputRecorder(Guid aufgabeId, string pluginName, int cols, int rows, int byteBudget, TimeProvider timeProvider, ILogger? logger = null)` — `cols`/`rows` werden als initiale Session-Geometrie in den Aufzeichnungs-Header übernommen.

|| Member | Verhalten |
||--------|-----------|
|| `OnOutputChunk` | Kopiert die Bytes unverändert und speichert sie als `CliOutputChunkRecord` mit Offset `timeProvider.GetUtcNow() - StartUtc`; leere Chunks werden übersprungen. Bei `buffered + bytes.Length > byteBudget` stoppt die Aufnahme dauerhaft und `IstVollstaendig` wird `false` — das intakte Präfix bleibt erhalten (kein Verwerfen ältester Chunks) |
|| `Complete`/`CompleteAsync` | Setzt `EndeUtc` idempotent (erstes `Complete` gewinnt); `CompleteAsync` ist synchron abgeschlossen |
|| `GetAufzeichnung()` | Liefert einen `CliOutputAufzeichnung`-Snapshot der bis dahin aufgezeichneten Chunks — auch nach dem Session-Ende abrufbar |

## CliOutputAufzeichnung / CliOutputChunkRecord

Datenmodell der Rohbyte-Aufzeichnung (`Softwareschmiede.Infrastructure.Terminal`).

`CliOutputChunkRecord` — Record `(TimeSpan Offset, byte[] Data)`: `Offset` ist der zeitliche Abstand des Chunks zum Aufzeichnungsbeginn, `Data` die unveränderten Rohbytes.

`CliOutputAufzeichnung` — Header + Chunk-Liste:

|| Eigenschaft | Typ | Beschreibung |
||-------------|-----|--------------|
|| `AufgabeId` | `Guid` | ID der aufgezeichneten Aufgabe |
|| `PluginName` | `string` | Anzeigename des aufgezeichneten KI-Plugins |
|| `StartUtc` | `DateTimeOffset` | Absoluter Aufzeichnungsbeginn (UTC); Anker der relativen Chunk-Offsets |
|| `Cols` / `Rows` | `int` | Initiale Terminal-Geometrie der aufgezeichneten Session |
|| `IstVollstaendig` | `bool` | `false`, wenn das Byte-Budget überschritten wurde (nur das Präfix ist enthalten) |
|| `EndeUtc` | `DateTimeOffset?` | Aufzeichnungsende; `null` solange die Aufzeichnung läuft |
|| `Chunks` | `IReadOnlyList<CliOutputChunkRecord>` | Chunks in Eingangsreihenfolge |

## CliReplayAufzeichnungStore — `.clireplay`-Dateiformat

Serialisiert/Deserialisiert `CliOutputAufzeichnung` im `.clireplay`-Binärformat. In der DI als Singleton registriert.

**Dateiformat (Version 1):**

|| Bereich | Inhalt |
||---------|--------|
|| Magic | 8 Bytes `SWCLRPLY` (ASCII) |
|| Header | `Int32 Version` (= 1), `Guid AufgabeId` (16 Bytes), `Int64 StartUtcTicks`, `Int64 EndeUtcTicks` (`0` = nicht gesetzt), `Int32 Cols`, `Int32 Rows`, `Boolean IstVollstaendig`, `Int32 PluginNameLength` + UTF-8-Bytes |
|| Records | Wiederholt bis EOF: `Int64 OffsetTicks`, `Int32 Length`, `Length` Bytes Rohdaten |

### Methoden

- `SpeichernAsync(Stream, CliOutputAufzeichnung, ct)` / `SpeichernAsync(string pfad, CliOutputAufzeichnung, ct)` — serialisiert gepuffert und schreibt async (UI-Thread bleibt frei)
- `LadeAsync(Stream, ct)` / `LadeAsync(string pfad, ct)` → `CliOutputAufzeichnung` — liest async in einen gepufferten Stream und validiert: Magic, `Version == 1`, `Cols`/`Rows > 0` (sonst wirft die Buffer-Anlage beim Replay), `PluginName`-Länge und Record-Längen gegen die Restlänge — Verletzungen → `InvalidDataException`

## TerminalReplaySession

Zweite `ITerminalSession`-Implementierung (`Softwareschmiede.Infrastructure.Terminal`): spielt eine `CliOutputAufzeichnung` zeitgesteuert durch denselben Renderpfad wie `PseudoConsoleSession` ab (`AnsiSequenceParser` → `TerminalBuffer` → `BufferChanged`). Zusätzlich zur Schnittstelle steuert sie die Wiedergabe — zeitgesteuert (`WiedergabeStarten`/`Pausieren`/`Fortsetzen`/`ZeitrafferSchwelle`) und als Einzelschritte (`SchrittVor`/`SchrittZurueck`).

**Konstruktor:** `TerminalReplaySession(CliOutputAufzeichnung aufzeichnung, TimeProvider timeProvider, ILogger<TerminalReplaySession>? logger = null)` — legt `Buffer` mit `Math.Max(1, Cols)`/`Rows` aus dem Aufzeichnungs-Header an.

### Zusätzliche Abspiel-Member

|| Member | Typ | Beschreibung |
||--------|-----|--------------|
|| `WiedergabeStarten()` | Methode | Startet die zeitgesteuerte Wiedergabe ab der aktuellen Position (idempotent, solange eine Wiedergabe-Schleife lebt; nach einem beendeten Durchlauf re-armierbar über das Lauf-Flag `_wiedergabeLoopAktiv` — z. B. für „beendet → `SchrittZurueck` → ab Position fortsetzen"); setzt `RuntimeStatus = Laeuft` |
|| `Pausieren()` | Methode | Hält die Wiedergabe an; ein kurzer Render-Lock dient als Fence — nach Rückkehr wird garantiert kein Chunk mehr angewendet |
|| `Fortsetzen()` | Methode | Setzt eine pausierte Wiedergabe exakt an der Position fort |
|| `SchrittVor()` | `bool` | Wendet genau den nächsten aufgezeichneten Chunk an — zeitstempel-unabhängig (keine Inter-Chunk-Pause, keine `ZeitrafferSchwelle`-Wirkung); feuert `OutputChunk` und `BufferChanged`. Am Ende der Aufzeichnung: `Exited` (wie am Schleifenende) plus Termination einer evtl. pausiert parkenden Schleife. `false` = No-Op am Ende oder nach `Dispose` |
|| `SchrittZurueck()` | `bool` | Stellt den Zustand vor dem zuletzt angewendeten Chunk wieder her — deterministischer Neuaufbau aus dem verbleibenden Präfix (`Buffer.Reset()` + `_parser.Reset()` + Re-Parse über `BaueBufferUndParserAusPraefixNeuAuf`); feuert nur `BufferChanged`, kein `OutputChunk`. `false` = No-Op an Position 0 oder nach `Dispose` |
|| `IstPausiert` | `bool` | `true` solange pausiert |
|| `ZeitrafferSchwelle` | `TimeSpan` | Obere Grenze der Wartezeit vor jedem Chunk: `min(realePause, ZeitrafferSchwelle)`; `Zero` = maximale Geschwindigkeit; jederzeit änderbar, wirkt auf folgende Pausen |
|| `AktuellerChunkIndex` | `int` | Anzahl bereits abgespielter Chunks — einzige Positionsquelle ist `_abgespielteChunks.Count` (unter `_renderLock`); Schleife und Einzelschritte mutieren denselben Zustand |

### Stub-Member der Schnittstelle

|| Member | Wert |
||--------|------|
|| `Process` | Nicht gestartetes `new Process()` — `Id`/`HasExited` werfen `InvalidOperationException` (von `TaskDetailView.TryGetProcessId` bereits abgefangen) |
|| `InputStream`/`OutputStream` | `Stream.Null` |
|| `IsPseudoTerminal` | `false` |
|| `SupportsResize` | `false` — die Geometrie ist auf die Aufzeichnungs-Header-Werte fixiert; das `TerminalControl` resized Replay-Sessions nicht (überschüssige Breite wird horizontal scrollbar) |
|| `Resize` | `true` — harmloser No-Op; mit `SupportsResize == false` vom Control nicht mehr aufgerufen |
|| `WriteInputAsync`/`WritePromptAsync`/`MarkInputActivity`/`MarkOutputActivity` | No-Op |
|| `Failure` | `null` (`Failed` wird nie ausgelöst) |
|| `ExitCode` | `null` |
|| `DrainOutputAsync` | `true` |
|| `RuntimeStatus` | `Laeuft` solange eine Wiedergabe-Schleife lebt (inkl. Pause), `Inaktiv` vor dem Start, nach dem Ende und im reinen Schrittmodus ohne gestartete Schleife |
|| `RebuildBufferFromReplay` | Baut `Buffer` synchron aus den **bis dahin abgespielten** Chunks neu auf (gleiches `_renderLock` wie die Wiedergabe-Schleife); setzt dabei auch den `_parser`-Zustand zurück — Buffer und Parser bleiben kohärent |
|| `Exited` | Feuert nach dem letzten Chunk mit `ExitCode = null` — Flanken-Ereignis („Ende wurde erreicht", kein Positions-Snapshot); kann über die Session-Lebensdauer mehrfach feuern (z. B. beendet → `SchrittZurueck` → neuer Durchlauf) |

**Pause-Gate:** Die Pausierung läuft über ein asynchrones `TaskCompletionSource`-Gate (`RunContinuationsAsynchronously`) statt eines synchronen Waits — `Task.Delay`-Fortsetzungen können zeitprovider-bedingt synchron auf fremden Threads laufen (z. B. `FakeTimeProvider.Advance`), ein blockierendes Wait würde dort deadlocked parken.

**Schrittmodus / Positionsführung:** `WiedergabeLoopAsync` liest die Position pro Iteration unter `_renderLock` aus `_abgespielteChunks.Count` (statt schleifenlokalem Index) und prüft sie vor dem Anwenden erneut — Einzelschritte verändern damit auch die Fortsetzposition der Schleife, und ein `Fortsetzen` nach `SchrittZurueck` wartet die aufgezeichnete Pause des zurückgenommenen Chunks regulär erneut ab. Die Chunk-Anwendung läuft in Schleife und `SchrittVor` über denselben Helper `WendeChunkAnUnterLock(position)` (`OutputChunk` → Add → Parse/Apply → Index-Write). Setzt `SchrittVor` die Position ans Ende, wird zusätzlich `_schleifeBeendenAngefordert` gesetzt — die aufgeweckte Schleife terminiert dann deterministisch, selbst wenn die Position zwischenzeitlich wieder unter dem Ende liegt. `RaiseExited(nurAmEnde: true)` verbindet die Positions-Prüfung mit dem Signal-Flag atomar unter `_renderLock`; das Event selbst feuert nach Lock-Freigabe.

## ICliReplayExportService / CliReplayExportService

Export-Service für den `.clireplay`-Mitschnitt (`Softwareschmiede.App.Services`; in der DI als Singleton registriert). Wird von `TaskDetailViewModel.ExportCliReplayAsync` verwendet.

### `ExportCliReplayAsync(Guid aufgabeId, string zielPfad, CancellationToken ct)` → `Task`

Holt die Aufzeichnung über `KiAusfuehrungsService.GetCliAufzeichnung(aufgabeId)` und schreibt sie über `CliReplayAufzeichnungStore.SpeichernAsync`.

**Exceptions:**
- `ArgumentException`: `zielPfad` leer
- `InvalidOperationException`: „Für diese Aufgabe liegt keine Aufzeichnung vor."

### `HatAufzeichnung(Guid aufgabeId)` → `bool`

Vorab-Prüfung, ob für die Aufgabe ein Mitschnitt vorliegt — das ViewModel prüft dies, bevor der Speicherdialog geöffnet wird.

## IDialogService — neue Dialog-Methoden

### `ShowOpenFileDialogAsync(string title, string filter, string? initialDirectory = null, CancellationToken ct)` → `Task<string?>`

Zeigt einen nativen `Microsoft.Win32.OpenFileDialog` auf dem UI-Dispatcher; liefert den gewählten Dateipfad oder `null` bei Abbruch. Der Dialog wird als Owner das **aktivste Fenster** zugeordnet (`AktivesDialogOwnerFenster`) — wichtig bei Aufruf aus dem nicht-modalen Konsolentestfenster, damit der Dialog nicht hinter dem Owned-Window versinkt.

### `ShowKonsolenTestDialogAsync(KonsolenTestViewModel viewModel, CancellationToken ct)` → `Task`

Zeigt `KonsolenTestDialog` **nicht-modal** an (`dialog.Show()`, `Owner = MainWindow`) und kehrt nach dem Anzeigen zurück — das Diagnosefenster bleibt parallel zur laufenden Arbeit nutzbar. Der Lebenszyklus des ViewModels wird über den `Closed`-Handler des Fensters disponiert.

## KonsolenTestViewModel / KonsolenTestDialog

ViewModel und Fenster des Konsolentestfensters (`Softwareschmiede.App.ViewModels` / `Softwareschmiede.App.Views`, `Title="Konsolentest"`). `KonsolenTestViewModel` ist per `AddTransient` registriert und wird aus `SettingsViewModel.KonsolenTestOeffnenCommand` per `IServiceProvider.GetRequiredService` aufgelöst.

**Konstruktor-Abhängigkeiten:** `IDialogService`, `CliReplayAufzeichnungStore`, `TimeProvider` (weitergereicht an `TerminalReplaySession`), `ILogger<KonsolenTestViewModel>`; optionaler Test-Hook `Action<Action>? dispatcherInvoke`.

|| Member | Beschreibung |
||--------|--------------|
|| `AufzeichnungOeffnenCommand` | Öffnen-Dialog → `LadeAsync` → Session erzeugen; Formatfehler → `FehlerMeldung` |
|| `WiedergabeStartenCommand` | Startet die Wiedergabe ab der aktuellen Position; bei beendetem Durchlauf (`_wiedergabeBeendet`) wird eine frische `TerminalReplaySession` erzeugt (Rebind über `Session` → `RebuildBufferFromReplay`) — nach einem `SchrittZurueck` vom Ende wird stattdessen dieselbe Session re-armiert und läuft an der Position weiter |
|| `WiedergabeNeustartenCommand` | Bricht eine laufende/pausierte Wiedergabe ab und spielt sofort wieder ab Position 0 (frische Session); zusätzlich im reinen Schrittmodus aktiv (`AktuellerChunkIndex > 0` ohne laufende Wiedergabe) als direkter Rückweg zum Anfang |
|| `WiedergabePausierenCommand` | Toggle `Pausieren`/`Fortsetzen` |
|| `SchrittVorCommand` / `SchrittZurueckCommand` | Einzelschritt vorwärts/rückwärts über `TerminalReplaySession.SchrittVor`/`SchrittZurueck`; CanExecute: Session geladen, nicht unpausiert laufende Wiedergabe (`!IstWiedergabeAktiv \|\| IstPausiert`) und Positionsgrenze (`AktuellerChunkIndex < QuellEintraege.Count` bzw. `> 0`). Die Handler ziehen den Wiedergabe-Zustandsteil der Sperre intern nach (RelayCommand wertet CanExecute bei `Execute` nicht aus) und setzen eigene `StatusText`-Meldungen; `SchrittZurueck` löscht `_wiedergabeBeendet` |
|| `SchliessenCommand` | `CloseRequested`-Event → Fenster schließt |
|| `Session` | `ITerminalSession?` — Bindung ans `TerminalControl` (`AutomationName="ReplayTerminal"`) |
|| `QuellEintraege` / `AktuellerQuellEintrag` | `ObservableCollection<CliChunkAnzeigeEintrag>` (`Index` 1-basiert — die „#"-Spalte zählt wie der PositionsText die angewendeten Chunks, `Offset`, `Laenge`, `Quelltext`) für die `ListView` `QuellChunkListe`; der aktuelle Eintrag folgt `AktuellerChunkIndex` über `BufferChanged` (`null` an Position 0) und wird per `ScrollIntoView` sichtbar gehalten |
|| `ZeitrafferSchwelleText` | Sekunden als Dezimalzahl ≥ 0 (`0` = maximale Geschwindigkeit); validiert — ungültige Eingabe → `FehlerMeldung`, letzte gültige Schwelle bleibt aktiv |
|| `StatusText` / `PositionsText` | Statuszeile („Wiedergabe läuft.", „Pausiert.", „Wiedergabe beendet.", „Einzelschritt — Chunk n/y angewendet.", „Schritt zurück — Chunk n/y zurückgenommen.") und Position („Chunk x/y") |
|| `GeometrieText` | Anzeigetext der aufgezeichneten Terminal-Geometrie („Aufzeichnung: {Cols}×{Rows}") — wird beim Laden gesetzt und beim Entsorgen der Session geleert |
|| `UnvollstaendigHinweis` | Hinweistext bei `IstVollstaendig = false` der geladenen Aufzeichnung |
|| `FehlerMeldung` | Fehlertext des Dialogs |

**Hilfsklassen der Quell-Ansicht:** `CliChunkQuelltextFormatter.Formatiere(ReadOnlySpan<byte>)` (statisch, `Softwareschmiede.App.Services`) dekodiert UTF-8 und macht Steuerzeichen sichtbar (`ESC` → `␛`, `CR` → `\r`, `LF` → `\n`, `TAB` → `\t`, übrige Steuerbytes/DEL → `\xNN`); `CliChunkAnzeigeEintrag` (`Softwareschmiede.App.ViewModels`) ist das Zeilenmodell.
