# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TerminalControlTests_ClipboardPaste.cs (TerminalControlTests_ClipboardPaste)

- **Doppelter Code** — Die neue Testklasse dupliziert mehrere Helper-Methoden und private Stream-Stub-Klassen 1:1 aus der bereits existierenden `TerminalControlTests.cs` im selben Verzeichnis/Namespace (`Softwareschmiede.Tests.App.Controls`):
  - `SetLogger` (Zeilen 235–239) ist identisch zu `TerminalControlTests.cs` Zeilen 224–228.
  - `CreateSession(Stream)` / `CreateSession(Stream, Stream)` (Zeilen 241–248) sind identisch zu `TerminalControlTests.cs` Zeilen 230–237.
  - `RunOnSta` (Zeilen 250–268) ist identisch zu `TerminalControlTests.cs` Zeilen 239–257.
  - Die private Klasse `ImmediateEofStream` (Zeilen 272–288) ist Zeichen für Zeichen identisch zu `TerminalControlTests.cs` Zeilen 260–276.
  - Die private Klasse `WriteThrowingStream` (Zeilen 290–307) ist Zeichen für Zeichen identisch zu `TerminalControlTests.cs` Zeilen 313–328.

  Empfehlung: `TerminalControlTests_ClipboardPaste` als zusätzliche Datei derselben Klasse mittels `partial class TerminalControlTests` anlegen (z. B. `TerminalControlTests.ClipboardPaste.cs`), sodass Helper-Methoden (`SetLogger`, `CreateSession`, `RunOnSta`) und die Stream-Stubs (`ImmediateEofStream`, `WriteThrowingStream`) nur einmal existieren und aus beiden Dateien genutzt werden. Alternativ die duplizierten Member aus der neuen Datei entfernen und stattdessen die vorhandenen aus `TerminalControlTests.cs` referenzieren (z. B. durch Zusammenführen beider Dateien zu einer Klasse).

### TerminalControlTests_ClipboardPaste.cs (TerminalControlTests_ClipboardPaste)

- **Namenskonvention** — Der Klassen-/Dateiname `TerminalControlTests_ClipboardPaste` (Zeile 16) weicht vom Namensschema ab, das in allen anderen ca. 80 Testklassen des Projekts durchgehend verwendet wird (`<Subject>Tests.cs`, reines PascalCase ohne Unterstrich), inklusive der thematisch benachbarten `TerminalControlTests.cs`, `TerminalBufferTests.cs` und `PseudoConsoleSessionTests.cs`.

  Empfehlung: Bei Umsetzung als `partial class` (siehe vorheriger Befund) den Klassennamen auf `TerminalControlTests` vereinheitlichen und nur den Dateinamen thematisch kennzeichnen (z. B. `TerminalControlTests.ClipboardPaste.cs`). Falls eine eigenständige Klasse gewünscht ist, den Namen ohne Unterstrich im Stil der Codebasis wählen (z. B. `TerminalControlClipboardPasteTests`).

### TerminalControl.cs (TerminalControl)

- **Doppelter Code** — `WriteToInputStream` (Zeilen 212–225, synchroner Tastatur-Pfad) und die neu hinzugefügte `WriteToInputStreamAsync` (Zeilen 294–310, asynchroner Zwischenablage-Pfad) implementieren nahezu identische Fehlerbehandlung: Schreiben in `Session.InputStream`, anschließend `Session.MarkInputActivity()`, bei Exception `_logger.LogWarning(ex, ...)`. Einziger Unterschied sind `Write` vs. `WriteAsync` und die Log-Nachricht.

  Empfehlung: Gemeinsame Fehlerbehandlungslogik extrahieren, z. B. eine private Methode `TryHandleWriteResultAsync(Func<ValueTask> write, string errorMessage)`, die von beiden Aufrufstellen nur noch die passende Schreiboperation (synchron per `ValueTask.CompletedTask`-Wrapper oder direkt per `WriteAsync`) übergeben bekommt, um die Duplizierung von Try/Catch/MarkInputActivity-Block zu vermeiden.

## Geprüfte Dateien

- `src/Softwareschmiede.App/Controls/KeyToVt100Encoder.cs`
- `src/Softwareschmiede.App/Controls/TerminalControl.cs`
- `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs`
- `src/Softwareschmiede.Tests/Domain/Terminal/TerminalBufferTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs`
- `src/Softwareschmiede.Tests/App/Controls/KeyToVt100EncoderTests.cs`
- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests_ClipboardPaste.cs`
