# Umsetzungsplan: ConPTY-basiertes Terminal-Control

## Übersicht

Der bestehende `ProcessWindowHost` (Win32 `SetParent` auf `conhost.exe`-Fenster) wird durch ein vollständiges ConPTY-basiertes Terminal ersetzt. Ein neues `PseudoConsoleSession`-Subsystem im Core-Projekt startet KI-CLI-Prozesse über die Windows Pseudo Console API (`CreatePseudoConsole`); ein neues `TerminalControl` im WPF-Projekt rendert den VT100-Ausgabestrom und leitet Tastatureingaben als Escape-Sequenzen an die ConPTY-Eingabe-Pipe weiter. Betroffen sind: `Softwareschmiede` (Core/Infrastruktur), `Softwareschmiede.App` (WPF), `Softwareschmiede.Tests`.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| VT100-Parser | In-House-Implementierung (`AnsiSequenceParser`) | Die tatsächlich emittierten Sequenzen moderner KI-CLIs (SGR-Farben, Cursor-Bewegung, Erase) sind eine handhabbare Teilmenge des Standards; keine externe Bibliothek nötig, volle Kontrolle für schrittweise Erweiterung |
| Terminal-Rendering | Eigene `FrameworkElement`-Subklasse mit `OnRender`/`DrawingContext` | Pixel-genaues Rendering, feste Monospace-Gitterzellen, hohe Frameraten bei häufigen Ausgabe-Updates — besser als `RichTextBox` oder `StackPanel` |
| Ort der ConPTY-Infrastruktur | Im Core-Projekt `Softwareschmiede` (Infrastruktur-Namespace) | `KiAusfuehrungsService` ist dort beheimatet; P/Invoke ist Windows-only, aber das Projekt zielt bereits auf `net10.0-windows10.0.17763.0`; verhindert Circular Dependency App → Core |
| Einstiegspunkt für ConPTY-Start | Neue Methode `StartWithPseudoConsoleAsync` in `KiAusfuehrungsService` | Lifecycle-Management (Heartbeat, `CliProcessStatusChanged`, Kill) bleibt in einer Klasse; `CliProcessHandle` wird um `PseudoConsoleSession?` erweitert |
| Tastatureingabe | `PreviewKeyDown`/`TextInput` in `TerminalControl` → `KeyToVt100Encoder` → `PseudoConsoleSession.InputStream` | Direkte Pipe-Schreibung ist der korrekte ConPTY-Weg; keine SendInput-Umwege |
| Scrollback-Puffer | Einfacher Ringpuffer (1 000 Zeilen) in `TerminalBuffer` | Ausreichend für KI-CLI-Ausgaben; kein komplexes virtuelles Scrolling in der ersten Version |
| `TerminalCell`-Darstellung | `record struct` mit Zeichen, Vorder-/Hintergrundfarbe, Attributen (Bold, Dim, Underline, Cursor) | Werttyp vermeidet Heap-Allokationen im Buffer-Grid; pattern-matchbar |
| Schriftart `TerminalControl` | Consolas 13pt, fest kodiert | Für die erste Version ausreichend; Konfigurierbarkeit als separates Feature |
| `CreatePseudoConsole`-Fehler | `InvalidOperationException` propagieren; `FehlerMeldung` im `TaskDetailViewModel` setzen | Fehler tritt bei korrektem TFM (Windows 10 Build 17763+) praktisch nicht auf; UI-Feedback genügt |
| OSC-Sequenzen (Fenstertitel u. a.) | Verwerfen (ignorieren, nicht rendern) | Claude CLI und Codex CLI benötigen Fenstertitel-Setzung nicht; kein Handlungsbedarf in V1 |
| Mouse-Tracking-Sequenzen | Nicht unterstützen | Weder Claude CLI noch Codex CLI erfordern Mouse-Tracking in ihrer Standard-Nutzung |

## Programmabläufe

### 1. Prozessstart mit ConPTY

1. `TaskDetailViewModel.StartenAsync` löst `StartCliAndUpdateStateAsync(pluginPrefix, lokalerKlonPfad, optionalParameters)` aus.
2. `StartCliAndUpdateStateAsync` ruft `_kiService.StartWithPseudoConsoleAsync(aufgabeId, kiPlugin, lokalerKlonPfad, optionalParameters, ct)`.
3. `KiAusfuehrungsService.StartWithPseudoConsoleAsync` führt folgende Schritte durch:
   a. Ruft `kiPlugin.StartCliAsync(localRepoPath, parameters)` → `ProcessStartInfo`.
   b. Erstellt anonyme `Pipe`-Paare für Input und Output (je `CreatePipe`).
   c. Ruft `PseudoConsole.Create(inputReadHandle, outputWriteHandle, initialCols, initialRows)` → `PseudoConsole`-Instanz.
   d. Ruft `PseudoConsoleProcessStarter.Start(psi, pseudoConsole)` → liefert Win32-Prozess-Handle + PID.
   e. Erzeugt verwaltetes `Process`-Objekt via `Process.GetProcessById(pid)`.
   f. Erzeugt `PseudoConsoleSession` mit `InputStream` (aus Input-Write-Pipe), `OutputStream` (aus Output-Read-Pipe), `PseudoConsole`, `Process`.
   g. Erzeugt `CliProcessHandle(aufgabeId, process) { PseudoConsoleSession = session }`.
   h. Trägt Handle in `_handles` ein, registriert `process.Exited`, feuert `CliProcessStatusChanged(aufgabeId, Gestartet)`.
4. `StartCliAndUpdateStateAsync` feuert `PseudoConsoleSessionGestartet(session)` statt bisherigem `CliProzessGestartet`.

Beteiligte Klassen/Komponenten: `KiAusfuehrungsService`, `PseudoConsole`, `PseudoConsoleProcessStarter`, `PseudoConsoleSession`, `CliProcessHandle`, `TaskDetailViewModel`.

### 2. Terminal-Rendering-Loop

1. `TaskDetailView.xaml.cs` empfängt `OnPseudoConsoleSessionGestartet(session)`.
2. Setzt `TerminalConsole.Session = session`.
3. `TerminalControl` startet `ReadLoopAsync(ct)` auf einem Hintergrund-Task:
   - Liest bytes in Schleife aus `session.OutputStream` (asynchroner `PipeStream.ReadAsync`).
   - Übergibt Byte-Block an `AnsiSequenceParser.Parse(bytes)` → `IEnumerable<TerminalEvent>`.
   - Übergibt jedes `TerminalEvent` an `TerminalBuffer.Apply(event)` (aktualisiert Zustand, Cursor, Farben).
   - Ruft nach jedem Batch `Dispatcher.InvokeAsync(InvalidateVisual)` auf.
4. `TerminalControl.OnRender(DrawingContext dc)`:
   - Liest aktuelle Schriftgröße, berechnet Zellenbreite/-höhe (monospace).
   - Iteriert über sichtbare Zeilen im `TerminalBuffer`.
   - Zeichnet Hintergrundrechtecke (`dc.DrawRectangle`) und `FormattedText` für jede Zelle.
   - Rendert Cursor-Rechteck an `TerminalBuffer.CursorRow`/`CursorCol`.

Beteiligte Klassen/Komponenten: `TaskDetailView`, `TerminalControl`, `AnsiSequenceParser`, `TerminalBuffer`, `TerminalEvent`-Hierarchie, `PseudoConsoleSession`.

### 3. Tastatureingabe

1. `TerminalControl` fängt `PreviewKeyDown` und `TextInput`-Events ab (Fokus vorausgesetzt).
2. `KeyToVt100Encoder.Encode(keyEventArgs)` liefert `byte[]` (z. B. Pfeiltasten → `\x1b[A`, Enter → `\r`, Ctrl+C → `\x03`).
3. Byte-Array wird asynchron in `session.InputStream` geschrieben.
4. Fensterfokus: `TerminalControl.MouseDown` setzt `Keyboard.Focus(this)`.

Beteiligte Klassen/Komponenten: `TerminalControl`, `KeyToVt100Encoder`, `PseudoConsoleSession`.

### 4. ConPTY-Resize

1. `TerminalControl.SizeChanged`-Event ermittelt neue Pixel-Größe.
2. Berechnet neue Spalten- und Zeilenanzahl anhand Zellenbreite/-höhe.
3. Ruft `session.ResizeAsync(cols, rows)` auf.
4. `PseudoConsoleSession.ResizeAsync` delegiert an `PseudoConsole.Resize(cols, rows)` (`ResizePseudoConsole` P/Invoke).
5. Passt `TerminalBuffer.Resize(cols, rows)` an (erhält Scrollback, passt aktuelle Zeilen an).

Beteiligte Klassen/Komponenten: `TerminalControl`, `PseudoConsoleSession`, `PseudoConsole`, `TerminalBuffer`.

### 5. Prozessende

1. `process.Exited` wird ausgelöst → `KiAusfuehrungsService.Exited`-Handler entfernt Handle aus `_handles`.
2. `PseudoConsoleSession` schließt Output-Pipe → `ReadLoopAsync` liest EOF → terminiert.
3. `CliProcessStatusChanged(aufgabeId, Gestoppt|Fehler)` → `TaskDetailViewModel.OnCliProcessStatusChanged` → `IsCliRunning = false`.
4. `KiAusfuehrungsService.Dispose` und `CliProcessHandle.Dispose` rufen `PseudoConsoleSession.Dispose()` auf → `ClosePseudoConsole`, Pipes schließen.

Beteiligte Klassen/Komponenten: `KiAusfuehrungsService`, `CliProcessHandle`, `PseudoConsoleSession`, `PseudoConsole`, `TaskDetailViewModel`.

## Neue Klassen

### Core (`Softwareschmiede`)

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `PseudoConsoleNativeMethods` | `static class` | P/Invoke-Deklarationen: `CreatePseudoConsole`, `ResizePseudoConsole`, `ClosePseudoConsole`, `CreateProcess`, `UpdateProcThreadAttribute`, `InitializeProcThreadAttributeList`, `DeleteProcThreadAttributeList`, `CreatePipe`, `CloseHandle` |
| `PseudoConsole` | `sealed class`, `IDisposable` | Kapselt `HPCON`-Handle + Pipe-Handles; `Resize(short cols, short rows)`; `Dispose` ruft `ClosePseudoConsole` und schließt Pipes |
| `PseudoConsoleProcessStarter` | `static class` | `Start(ProcessStartInfo psi, PseudoConsole pc)` → erstellt Win32-Prozess mit `STARTUPINFOEX`; gibt `(IntPtr processHandle, int pid)` zurück |
| `PseudoConsoleSession` | `sealed class`, `IDisposable` | Koordiniert `PseudoConsole`, `Process`, Input-`Stream`, Output-`Stream`; `ResizeAsync(int cols, int rows)` |
| `TerminalCell` | `record struct` | `char Character`, `Color Foreground`, `Color Background`, `bool Bold`, `bool Underline`, `bool Dim` |
| `TerminalBuffer` | `sealed class` | 2D-Grid aus `TerminalCell`, Cursor-Position (`CursorRow`/`CursorCol`), Attribut-Zustand, Scrollback-Ringpuffer (1 000 Zeilen); `Apply(TerminalEvent)`, `Resize(int cols, int rows)` |
| `TerminalEvent` | `abstract record` | Basistyp der Parser-Ergebnis-Hierarchie |
| `TextWrittenEvent` | `sealed record : TerminalEvent` | Auszugebender Klartext |
| `CursorMovedEvent` | `sealed record : TerminalEvent` | Absolute oder relative Cursor-Positionierung |
| `ColorChangedEvent` | `sealed record : TerminalEvent` | SGR-Farbänderung (Vordergrund, Hintergrund, Bold, Dim, Underline, Reset) |
| `ScreenClearedEvent` | `sealed record : TerminalEvent` | `\x1b[2J` oder `\x1b[H\x1b[2J` |
| `LineErasedEvent` | `sealed record : TerminalEvent` | `\x1b[K` (Erase to end of line) und Varianten |
| `CursorVisibilityChangedEvent` | `sealed record : TerminalEvent` | `\x1b[?25h`/`l` |
| `AnsiSequenceParser` | `sealed class` | Zustandsbehafteter VT100-Parser; `IEnumerable<TerminalEvent> Parse(ReadOnlySpan<byte> data)` |

### App (`Softwareschmiede.App`)

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `TerminalControl` | `FrameworkElement`-Subklasse | WPF-Control: `Session`-Property, `ReadLoopAsync`, `OnRender` via `DrawingContext`, Fokus- und Tastatur-Handling |
| `KeyToVt100Encoder` | `static class` | Konvertiert WPF `KeyEventArgs` → VT100-Byte-Sequenz |

## Änderungen an bestehenden Klassen

### `CliProcessHandle`

- **Neue Eigenschaften:** `PseudoConsoleSession? PseudoConsoleSession` — wird von `StartWithPseudoConsoleAsync` gesetzt; `null` bei klassischem Start
- **Geänderte Methoden:** Keine (Dispose-Aufruf erfolgt im `KiAusfuehrungsService`)
- **Entfernte Eigenschaften:** `FensterHandle` (IntPtr) — wird mit ConPTY nicht mehr benötigt

### `KiAusfuehrungsService`

- **Neue Methoden:**
  - `StartWithPseudoConsoleAsync(Guid aufgabeId, IKiPlugin kiPlugin, string localRepoPath, string? parameters, CancellationToken ct)` → `Task<CliProcessHandle>` — ConPTY-Einstiegspunkt; übernimmt Sperr- und Lifecycle-Logik analog zu `StartCliAsync`
  - `GetPseudoConsoleSession(Guid aufgabeId)` → `PseudoConsoleSession?` — gibt die Session aus dem `CliProcessHandle` zurück
- **Entfernte Methoden:** `SetFensterHandle(Guid, IntPtr)`, `GetFensterHandle(Guid)` — nicht mehr benötigt
- **Geänderte Methoden:** `Dispose` — ruft zusätzlich `handle.PseudoConsoleSession?.Dispose()` für alle Handles auf

### `TaskDetailViewModel`

- **Neue Events:** `PseudoConsoleSessionGestartet: Action<PseudoConsoleSession>?` — wird nach erfolgreichem `StartWithPseudoConsoleAsync` gefeuert
- **Entfernte Properties:** `EmbeddedWindowHandle` (`IntPtr`), `CliProzessGestartet`-Event
- **Entfernte Methoden:** `SetCliWindowHandle(IntPtr)`, `GetCliWindowHandle()`, `GetRunningProcess()`
- **Geänderte Methoden:** `StartCliAndUpdateStateAsync` — ruft `_kiService.StartWithPseudoConsoleAsync` statt `StartCliAsync`; feuert `PseudoConsoleSessionGestartet` statt `CliProzessGestartet`; `CliAutomatischNeustartenAsync` analog

### `TaskDetailView.xaml.cs`

- **Entfernte Felder:** `_pollCts`
- **Entfernte Methoden:** `WaitForWindowHandleAsync`, `OnCliProzessGestartet`
- **Neue Methoden:** `OnPseudoConsoleSessionGestartet(PseudoConsoleSession session)` — setzt `TerminalConsole.Session = session`
- **Geänderte Methoden:** `Loaded`-Handler — abonniert `PseudoConsoleSessionGestartet` statt `CliProzessGestartet`; entfernt Fenster-Handle-Fallback-Logik

### `TaskDetailView.xaml`

- **Geändert:** `<controls:ProcessWindowHost ... EmbeddedHandle="{Binding EmbeddedWindowHandle}"/>` wird durch `<controls:TerminalControl x:Name="TerminalConsole"/>` ersetzt

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **`ProcessWindowHost.cs` entfernt:** Alle XAML-Referenzen auf `controls:ProcessWindowHost` müssen vor dem Entfernen der Klasse entfernt sein.
- **`KiAusfuehrungsService` API-Änderung:** `SetFensterHandle`/`GetFensterHandle` werden entfernt — betrifft `KiAusfuehrungsServiceTests` und alle Aufrufer.
- **`TaskDetailViewModel` API-Änderung:** `EmbeddedWindowHandle`, `CliProzessGestartet`, `GetRunningProcess`, `SetCliWindowHandle`, `GetCliWindowHandle` werden entfernt — betrifft `TaskDetailViewModelTests` und E2E-Tests.
- **Kein Fallback für alte Windows-Versionen:** `CreatePseudoConsole` ist erst ab Windows 10 Build 17763 verfügbar. Das Projekt zielt bereits auf `net10.0-windows10.0.17763.0`, daher ist das kein praktisches Risiko, aber der Fehlerfall sollte im UI kommuniziert werden (Fehlertext im `FehlerMeldung`-Banner).
- **ConPTY-Prozessstart anders als `Process.Start`:** `Process.GetProcessById(pid)` nach `CreateProcess` liefert kein `HasExited`-tracking bis der Prozess beendet wird; der `Exited`-Handler auf dem so gewonnenen `Process`-Objekt muss vor Aufruf von `EnableRaisingEvents = true` gesetzt werden.

## Umsetzungsreihenfolge

1. **`PseudoConsoleNativeMethods` anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: Statische Klasse mit allen P/Invoke-Deklarationen für ConPTY (`CreatePseudoConsole`, `ResizePseudoConsole`, `ClosePseudoConsole`) und Prozessstart-Hilfsfunktionen (`InitializeProcThreadAttributeList`, `UpdateProcThreadAttribute`, `DeleteProcThreadAttributeList`, `CreateProcess`, `CreatePipe`, `CloseHandle`) in `Softwareschmiede/Infrastructure/Terminal/`.

2. **`PseudoConsole` anlegen**
   - Voraussetzungen: `PseudoConsoleNativeMethods`
   - Beschreibung: `IDisposable`-Wrapper für `HPCON`-Handle und Pipe-Handles; statische `Create`-Fabrikmethode; `Resize(short cols, short rows)`; `Dispose` schließt Handle und Pipes sicher.

3. **`PseudoConsoleProcessStarter` anlegen**
   - Voraussetzungen: `PseudoConsoleNativeMethods`, `PseudoConsole`
   - Beschreibung: Statische Klasse mit `Start(ProcessStartInfo psi, PseudoConsole pc)`: erzeugt `STARTUPINFOEX`, initialisiert Attributliste, setzt `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE`, ruft `CreateProcess`, gibt PID und Win32-Prozess-Handle zurück.

4. **`TerminalCell` und `TerminalBuffer` anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: `TerminalCell` als `record struct`; `TerminalBuffer` mit 2D-Array aus `TerminalCell`-Zeilen, Cursor-Position, aktuellen SGR-Attributen (Vorder-/Hintergrundfarbe, Bold/Dim/Underline), Scrollback-Ringpuffer (1 000 Zeilen); Methoden `Apply(TerminalEvent)`, `Resize(int cols, int rows)` in `Softwareschmiede/Domain/Terminal/`.

5. **`TerminalEvent`-Hierarchie anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: Abstrakte Basis `TerminalEvent` + versiegelte Records `TextWrittenEvent`, `CursorMovedEvent`, `ColorChangedEvent`, `ScreenClearedEvent`, `LineErasedEvent`, `CursorVisibilityChangedEvent` in `Softwareschmiede/Domain/Terminal/`.

6. **`AnsiSequenceParser` anlegen**
   - Voraussetzungen: `TerminalEvent`-Hierarchie
   - Beschreibung: Zustandsbehafteter Parser; `IEnumerable<TerminalEvent> Parse(ReadOnlySpan<byte> data)` verarbeitet plaintext, CSI-Sequenzen (`\x1b[...`), OSC-Sequenzen (verworfen), DEC-Private-Modes; deckt SGR-Farben (3-bit, 8-bit `38;5;n`, 24-bit `38;2;r;g;b`), Cursor-Bewegungen, Clear/Erase ab.

7. **`PseudoConsoleSession` anlegen**
   - Voraussetzungen: `PseudoConsole`, `PseudoConsoleProcessStarter`
   - Beschreibung: Koordiniert `PseudoConsole`, `Process`, `Stream InputStream` (Input-Write-Pipe), `Stream OutputStream` (Output-Read-Pipe); `ResizeAsync(int cols, int rows)`; `Dispose` schließt alle Handles geordnet.

8. **`CliProcessHandle` erweitern — `PseudoConsoleSession?`**
   - Voraussetzungen: `PseudoConsoleSession`
   - Beschreibung: Neue Eigenschaft `PseudoConsoleSession?`; `FensterHandle`-Eigenschaft entfernen.

9. **`KiAusfuehrungsService` erweitern**
   - Voraussetzungen: `CliProcessHandle` mit `PseudoConsoleSession?`, `PseudoConsoleSession`, `PseudoConsole`, `PseudoConsoleProcessStarter`
   - Beschreibung: Neue Methode `StartWithPseudoConsoleAsync`; neue Methode `GetPseudoConsoleSession`; `SetFensterHandle`/`GetFensterHandle` entfernen; `Dispose` um Session-Dispose erweitern.

10. **`KeyToVt100Encoder` anlegen**
    - Voraussetzungen: Keine.
    - Beschreibung: Statische Klasse in `Softwareschmiede.App/Controls/`; Mapping WPF `Key` → `byte[]`: Pfeiltasten, Pos1/Ende, PgUp/PgDown, Enter, Backspace, Delete, Tab, Escape, Funktionstasten F1–F12, Ctrl+Buchstabe.

11. **`TerminalControl` anlegen**
    - Voraussetzungen: `PseudoConsoleSession`, `AnsiSequenceParser`, `TerminalBuffer`, `KeyToVt100Encoder`
    - Beschreibung: `FrameworkElement`-Subklasse in `Softwareschmiede.App/Controls/`; `DependencyProperty Session` vom Typ `PseudoConsoleSession?`; `OnSessionChanged` startet `ReadLoopAsync`; `OnRender(DrawingContext)` rendert `TerminalBuffer` mit fester Monospace-Schrift (Consolas 13pt); `PreviewKeyDown`/`TextInput` schreiben Bytes via `KeyToVt100Encoder` in `session.InputStream`; `MouseDown` setzt Fokus; `SizeChanged` ruft `session.ResizeAsync`.

12. **`TaskDetailViewModel` umbauen**
    - Voraussetzungen: `KiAusfuehrungsService` mit `StartWithPseudoConsoleAsync`
    - Beschreibung: `EmbeddedWindowHandle`, `CliProzessGestartet`, `SetCliWindowHandle`, `GetCliWindowHandle`, `GetRunningProcess` entfernen; `PseudoConsoleSessionGestartet`-Event hinzufügen; `StartCliAndUpdateStateAsync` und `CliAutomatischNeustartenAsync` auf `StartWithPseudoConsoleAsync` umstellen.

13. **`TaskDetailView.xaml` umbauen**
    - Voraussetzungen: `TerminalControl`
    - Beschreibung: `ProcessWindowHost`-Element durch `<controls:TerminalControl x:Name="TerminalConsole"/>` ersetzen; `EmbeddedHandle`-Binding entfernen.

14. **`TaskDetailView.xaml.cs` umbauen**
    - Voraussetzungen: `TaskDetailViewModel` (Schritt 12), `TerminalControl` (Schritt 11)
    - Beschreibung: `_pollCts`, `WaitForWindowHandleAsync`, `OnCliProzessGestartet` entfernen; `OnPseudoConsoleSessionGestartet` ergänzen; `Loaded`-Handler anpassen.

15. **`ProcessWindowHost.cs` entfernen**
    - Voraussetzungen: Schritte 13 und 14 abgeschlossen (keine Referenzen mehr)
    - Beschreibung: Datei löschen; XAML-Namespace-Referenz `controls:ProcessWindowHost` ist bereits in Schritt 13 entfernt.

16. **Tests anlegen**
    - Voraussetzungen: Alle Implementierungsschritte abgeschlossen
    - Beschreibung: Neue Testklassen `AnsiSequenceParserTests`, `TerminalBufferTests`; bestehende `TaskDetailViewModelTests`, `KiAusfuehrungsServiceTests`, `CliEmbeddingServiceIntegrationTests` und betroffene E2E-Tests anpassen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Parse_PlainText_ErgibtTextWrittenEvent` | `AnsiSequenceParserTests` | Klartext ohne Escapes → `TextWrittenEvent` mit korrektem Text |
| `Parse_SgrFarbe_ErgibtColorChangedEvent` | `AnsiSequenceParserTests` | `\x1b[31m` → `ColorChangedEvent` mit roter Vordergrundfarbe |
| `Parse_SgrReset_ErgibtColorChangedEventMitStandardfarben` | `AnsiSequenceParserTests` | `\x1b[0m` → Reset auf Standardfarben |
| `Parse_Sgr24BitFarbe_WirdKorrektParsiert` | `AnsiSequenceParserTests` | `\x1b[38;2;100;200;50m` → 24-Bit-Vordergrundfarbe |
| `Parse_CursorMove_ErgibtCursorMovedEvent` | `AnsiSequenceParserTests` | `\x1b[5;10H` → `CursorMovedEvent(Row=4, Col=9)` (0-basiert) |
| `Parse_ClearScreen_ErgibtScreenClearedEvent` | `AnsiSequenceParserTests` | `\x1b[2J` → `ScreenClearedEvent` |
| `Parse_EraseLine_ErgibtLineErasedEvent` | `AnsiSequenceParserTests` | `\x1b[K` → `LineErasedEvent` |
| `Parse_MehrteiligePakete_WerdenZusammengesetzt` | `AnsiSequenceParserTests` | Escape-Sequenz über zwei `Parse`-Aufrufe aufgeteilt → vollständig verarbeitet |
| `Buffer_SchreibtText_AktualisiertZellen` | `TerminalBufferTests` | `Apply(TextWrittenEvent)` → korrekte Zeichen in Grid |
| `Buffer_CursorMove_AktualisiertPosition` | `TerminalBufferTests` | `Apply(CursorMovedEvent)` → `CursorRow`/`CursorCol` aktualisiert |
| `Buffer_Newline_ScrolltBeiLetzterZeile` | `TerminalBufferTests` | Text in letzter Zeile + Newline → Scroll um eine Zeile |
| `Buffer_Resize_ErhaeltSichtbarenInhalt` | `TerminalBufferTests` | `Resize(cols, rows)` kürzt oder erweitert Buffer ohne Datenverlust im sichtbaren Bereich |
| `Buffer_ClearScreen_Setzt AllesZurueck` | `TerminalBufferTests` | `Apply(ScreenClearedEvent)` → alle Zellen leer, Cursor auf (0,0) |
| `KiAusfuehrungsService_GetPseudoConsoleSession_GibtNull_OhneSession` | `KiAusfuehrungsServiceTests` | Kein ConPTY gestartet → `GetPseudoConsoleSession` gibt `null` zurück |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TaskDetailViewModelTests` | `EmbeddedWindowHandle`, `CliProzessGestartet`, `GetRunningProcess`, `SetCliWindowHandle`, `GetCliWindowHandle` entfernt; `PseudoConsoleSessionGestartet`-Event stattdessen prüfen |
| `KiAusfuehrungsServiceTests` | `SetFensterHandle`/`GetFensterHandle`-Tests entfernen |
| `CliEmbeddingServiceIntegrationTests` | `FakeKiPlugin.StartCliAsync` unverändert; `StartWithPseudoConsoleAsync` benötigt Windows-Build 17763 — Test als `[Trait("Category", "ConPTY")]` markieren oder auf Windows-Only beschränken |
| `E2E_AufgabeStarten` | `CliProzessGestartet`-Event-Registrierung durch `PseudoConsoleSessionGestartet` ersetzen |
| `E2E_AutoStartCli` | Analog zu `E2E_AufgabeStarten` |
| `E2E_PluginWechsel` | Analog; Fenster-Handle-Assertions entfernen |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| ConPTY-Session wird gestartet; `TerminalControl` erhält Session | `E2E_ConPtyTerminalStart` | `PseudoConsoleSessionGestartet` wird gefeuert; Session ist nicht null |
| Tastatureingabe landet im InputStream der Session | `E2E_ConPtyKeyboardInput` | Nach simuliertem Tastendruck enthält InputStream-Pipe das kodierte Byte |
| Resize aktualisiert ConPTY-Dimensionen | `E2E_ConPtyResize` | `ResizePseudoConsole` wird mit geänderten Spalten/Zeilen aufgerufen |
| Prozessende beendet ReadLoop und setzt IsCliRunning=false | `E2E_ConPtyProcessEnd` | Nach Prozessende: `IsCliRunning == false`, ReadLoop terminiert |

Bestehende E2E-Tests mit Anpassungsbedarf:

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_AufgabeStarten` | `CliProzessGestartet`-Prüfung durch `PseudoConsoleSessionGestartet`-Prüfung ersetzen |
| `E2E_AutoStartCli` | Analog |
| `E2E_PluginWechsel` | Fenster-Handle-Assertions entfernen; Session-basierte Assertions ergänzen |

## Offene Punkte

Keine.
