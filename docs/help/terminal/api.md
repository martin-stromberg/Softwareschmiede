← [Zurück zur Übersicht](index.md)

# Terminal-Integration — API

## Übersicht

Das Terminal-System exponiert die `PseudoConsoleSession` zum Starten und Steuern von Prozessen, die `TerminalControl` als WPF-Rendering-Component, das `PseudoConsoleSessionGestartet`-Event zum Lifecycle-Management und optionale Output-Senken für die UI-unabhängige Weiterverarbeitung gelesener Terminalausgaben.

## PseudoConsoleSession

Koordiniert einen Pseudo Console-Prozess, Input-Pipe und Output-Pipe.

### Eigenschaften

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Process` | `System.Diagnostics.Process` | Der laufende Prozess (read-only) |
| `InputStream` | `System.IO.Stream` | Pipe zum Schreiben von Tastatureingaben (read-only) |
| `OutputStream` | `System.IO.Stream` | Pipe zum Lesen der Prozess-Ausgabe (read-only) |
| `Buffer` | `TerminalBuffer` | Der Terminal-Buffer der Sitzung; wird bereits bei Konstruktion angelegt und von der internen Leseschleife befüllt, unabhängig davon, ob ein `TerminalControl` gebunden ist (read-only) |
| `RuntimeStatus` | `CliRuntimeStatus` | Der aktuelle Betriebszustand der CLI (`Inaktiv`, `Laeuft`, `WartetAufEingabe`). Wird alle 1 Sekunde neu bewertet basierend auf Prozess-Zustand und I/O-Aktivität (read-only) |

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

### Methoden

#### `Resize(int cols, int rows)`

Ändert die Größe der Pseudo Console und des zugrunde liegenden Terminal-Puffers.

**Parameter:**
- `cols`: Neue Spaltenanzahl (muss > 0 sein)
- `rows`: Neue Zeilenanzahl (muss > 0 sein)

**Rückgabe:** `bool` — `true` wenn erfolgreich, `false` bei Fehler (z.B. ungültige Parameter)

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

#### `Dispose()`

Schließt alle Ressourcen: HPCON-Handle, Input-Pipe, Output-Pipe. Bricht die interne Leseschleife (`ReadLoopAsync`) ab und schließt danach die Streams. Der Prozess wird **nicht** beendet (muss über `Process.Kill()` manuell beendet werden, falls erforderlich). Eine angebundene Output-Senke wird durch die Leseschleife idempotent abgeschlossen; der Service-Cleanup kann zusätzlich `CompleteAsync(...)` aufrufen, um begrenzt auf Persistenz zu warten.

**Beispiel:**
```csharp
session.Dispose();
```

## TerminalControl

WPF-`FrameworkElement` zum Rendern einer `PseudoConsoleSession`.

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

Die aktive `PseudoConsoleSession` zum Rendern.

**Typ:** `PseudoConsoleSession?`

**Standard:** `null`

**Beschreibung:** Wenn gesetzt, abonniert `TerminalControl` das `BufferChanged`-Event der Session und rendert deren `Buffer`. Die Leseschleife selbst läuft unabhängig vom Control in `PseudoConsoleSession` — sie startet bei der Konstruktion der Session und läuft weiter, auch wenn keine oder eine andere Session gebunden ist (parallele CLI-Ausführungen, Issue-86). Beim Wechsel zu einer neuen Session wird nur der `BufferChanged`-Handler der alten Session deregistriert, nicht deren Leseschleife.

**Beispiel (Code-Behind):**
```csharp
TerminalConsole.Session = pseudoConsoleSession;
```

**Beispiel (Data-Binding):**
```xml
<controls:TerminalControl Session="{Binding CurrentTerminalSession}" />
```

### Ereignisse

Das Control erbt von `FrameworkElement`. Terminal-spezifische Events sind nicht exposiert; das Control abonniert intern lediglich `PseudoConsoleSession.BufferChanged`, dessen Leseschleife im Hintergrund der Session läuft.

### Rendering

Das Control rendert den Buffer mit:
- **Schrift:** Monospace Consolas 13pt
- **Zellenbreite:** Ca. 7.5 px (hängt vom System DPI ab)
- **Zellenhöhe:** Ca. 13 px
- **Hintergrundfarbe:** Schwarz (standard Terminal-Farbe)
- **Vordergrundfarbe:** Hellgrau (ANSI Standard)
- **Attribute:** Bold, Dim, Underline (soweit unterstützt)
- **Cursor:** Halbtransparentes weißes Rechteck

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

Das Control triggert automatisch `Session.ResizeAsync()` bei Layout-Änderungen. Die neuen Spalten- und Zeilenzahlen werden aus verfügbaren Pixeln und Zellengröße berechnet.

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

#### `StartWithPseudoConsoleAsync(Guid aufgabeId, IKiPlugin kiPlugin, string localRepoPath, string? parameters, CancellationToken ct)`

Startet einen KI-CLI-Prozess über die Pseudo Console API.

**Parameter:**
- `aufgabeId`: Eindeutige Aufgaben-ID
- `kiPlugin`: Plugin-Instanz (muss `IKiPlugin.StartCliAsync` implementieren)
- `localRepoPath`: Arbeitsverzeichnis des Prozesses
- `parameters`: Optionale CLI-Argumente
- `ct`: Cancellation Token

**Rückgabe:** `Task<CliProcessHandle>`

**Output-Protokollierung:** Für ConPTY-Starts erzeugt der Service einen `CliOutputProtokollWriter`, reicht ihn als `ITerminalOutputSink` an den Launcher weiter und hält ihn im `CliProcessHandle.OutputSink`. Der Writer speichert Ausgabezeilen über `ProtokollService.AddCliOutputAsync` als `ProtokollTyp.CliOutput`.

**Exceptions:**
- `InvalidOperationException`: `CreatePseudoConsole` fehlgeschlagen oder Plugin-Fehler

**Beispiel:**
```csharp
var handle = await _kiService.StartWithPseudoConsoleAsync(
    taskId, 
    codexPlugin, 
    "C:\\repos\\my-project",
    "--verbose",
    cancellationToken
);
```

#### `GetPseudoConsoleSession(Guid aufgabeId)`

Gibt die aktive `PseudoConsoleSession` für eine Aufgabe zurück, falls eine läuft.

**Parameter:**
- `aufgabeId`: Aufgaben-ID

**Rückgabe:** `PseudoConsoleSession?` (null, falls kein Prozess läuft oder kein ConPTY gestartet wurde)

**Beispiel:**
```csharp
var session = _kiService.GetPseudoConsoleSession(taskId);
if (session != null)
{
    await session.ResizeAsync(100, 25);
}
```

#### `StopAsync(Guid aufgabeId)`

Beendet den laufenden Prozess für eine Aufgabe.

**Parameter:**
- `aufgabeId`: Aufgaben-ID

**Rückgabe:** `Task`

**Exceptions:**
- `KeyNotFoundException`: Keine aktive Aufgabe mit dieser ID

## Events

### TaskDetailViewModel.PseudoConsoleSessionGestartet

Wird gefeuert, nachdem `KiAusfuehrungsService.StartWithPseudoConsoleAsync` erfolgreich abgeschlossen wurde.

**Typ:** `Action<PseudoConsoleSession>?`

**Parameter:** Die neu erstellte `PseudoConsoleSession`

**Verwendung:**
```csharp
taskViewModel.PseudoConsoleSessionGestartet += session =>
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

## CliOutputProtokollWriter

Implementiert `ITerminalOutputSink` für Aufgabenläufe.

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

**Zustand:** Der Parser ist zustandsbehaftet; unvollständige Sequenzen über Paket-Grenzen werden korrekt zusammengesetzt.

**Unterstützte Sequenzen:**
- **Plaintext:** Klartext → `TextWrittenEvent`
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
- **Cursor-Bewegung:** `\x1b[{row};{col}H` → `CursorMovedEvent` (1-basiert → 0-basiert)
  - `\x1b[A`, `\x1b[B`, `\x1b[C`, `\x1b[D`: Relative Bewegung
- **Clear/Erase:** `\x1b[2J`, `\x1b[K` → `ScreenClearedEvent`, `LineErasedEvent`
- **Cursor-Sichtbarkeit:** `\x1b[?25h`, `\x1b[?25l` → `CursorVisibilityChangedEvent`

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

Ändert Grid-Größe. Erhält sichtbare Zeilen; neue Zeilen werden initialisiert.

**Parameter:**
- `cols`: Neue Spaltenanzahl
- `rows`: Neue Zeilenanzahl

**Thread-Sicherheit:** Intern synchronisiert

#### `GetSnapshot()`

Erstellt einen konsistenten Snapshot des aktuellen Buffer-Zustands unter einem einzigen Lock. Wird von Render-Operationen genutzt, um Race Conditions zwischen paralleler Buffer-Aktualisierung und Lesezugriffen zu vermeiden.

**Rückgabe:** `TerminalBufferSnapshot` (Record mit Grid-Kopie, Rows, Cols, CursorRow, CursorCol)

**Thread-Sicherheit:** Intern synchronisiert; der Snapshot ist konsistent unter dem Lock erstellt

**Beispiel:**
```csharp
var snapshot = buffer.GetSnapshot();
var gridCopy = snapshot.Grid;
var cursorRow = snapshot.CursorRow;
// Render-Operationen ohne Lock-Contention
for (var r = 0; r < snapshot.Rows; r++)
{
    for (var c = 0; c < snapshot.Cols; c++)
    {
        var cell = gridCopy[r, c];
        // Zeichne Zelle
    }
}
```

#### Eigenschaften (read-only)

- `Rows`: Aktuelle Zeilenanzahl
- `Cols`: Aktuelle Spaltenanzahl
- `CursorRow`: Cursor-Zeile (0-basiert)
- `CursorCol`: Cursor-Spalte (0-basiert)
- `ScrollbackCount` (internal): Anzahl der aktuell im Scrollback-Ringpuffer gehaltenen Zeilen. Diese Eigenschaft ist nur für Tests sichtbar (interne API).

## Enums

### `CliRuntimeStatus`

Betriebszustand einer aktiven CLI-Sitzung:

| Wert | Beschreibung |
|------|--------------|
| `Inaktiv` | Kein laufender CLI-Prozess ist aktiv. |
| `Laeuft` | Die CLI läuft und hat kürzlich Ausgabe oder Eingabe verarbeitet. |
| `WartetAufEingabe` | Die CLI läuft, erzeugt aber seit längerer Zeit (Standard: 4 Sekunden) keine Ausgabe und wartet vermutlich auf Benutzereingabe. |

Der Status wird automatisch alle 1 Sekunde neu bewertet und das `RuntimeStatusChanged`-Event wird ausgelöst, falls sich der Status geändert hat.

## Konstanten

| Konstante | Wert | Beschreibung |
|-----------|------|--------------|
| `TerminalBuffer.MaxScrollbackLines` | 1000 | Maximale Scrollback-Puffer-Größe in Zeilen |
| `TerminalControl.FontSize` | 13.0 | Schriftgröße (Punkt) für Rendering |
| `PseudoConsoleSession.ReadLoopShutdownTimeout` | 5 Sekunden | Maximale Wartezeit beim Beenden der Leseschleife in `Dispose()` |
| `AnsiSequenceParser` | — | Kein Schwellenwert; alle Standard-Sequenzen werden geparst |
