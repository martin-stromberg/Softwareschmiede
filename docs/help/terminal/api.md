← [Zurück zur Übersicht](index.md)

# Terminal-Integration — API

## Übersicht

Das Terminal-System exponiert drei öffentliche Schnittstellen: die `PseudoConsoleSession` zum Starten und Steuern von Prozessen, die `TerminalControl` als WPF-Rendering-Component und das `PseudoConsoleSessionGestartet`-Event zum Lifecycle-Management.

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

Schließt alle Ressourcen: HPCON-Handle, Input-Pipe, Output-Pipe. Bricht die interne Leseschleife (`ReadLoopAsync`) ab, wartet mit 5-Sekunden-Timeout auf deren Beendigung und schließt danach die Streams. Der Prozess wird **nicht** beendet (muss über `Process.Kill()` manuell beendet werden, falls erforderlich).

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

#### Eigenschaften (read-only)

- `Rows`: Aktuelle Zeilenanzahl
- `Cols`: Aktuelle Spaltenanzahl
- `CursorRow`: Cursor-Zeile (0-basiert)
- `CursorCol`: Cursor-Spalte (0-basiert)

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
