# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TerminalControl.cs (TerminalControl)

- **Fehlerbehandlung** — In `OnPreviewKeyDown` wird für den Ctrl+V-Zweig `e.Handled = true` gesetzt und `ReadClipboardAndInsertAsync()` unbedingt aufgerufen, ohne vorher wie im direkt darunterliegenden Zweig (`bytes != null && Session?.InputStream != null`) zu prüfen, ob überhaupt eine aktive `Session` existiert. Ist `Session` `null` (z. B. Control noch keiner Sitzung zugeordnet), wirft `Session!.InputStream!.WriteAsync(...)` in `ReadClipboardAndInsertAsync` eine `NullReferenceException`, die zwar abgefangen, aber als irreführende Warnung „Fehler beim Einfügen aus der Zwischenablage in den Terminal-Input-Stream" geloggt wird — obwohl kein echter Fehler vorliegt, sondern schlicht keine Sitzung aktiv ist. Zusätzlich wird `e.Handled` unbedingt gesetzt, obwohl in diesem Fall nichts passiert (Inkonsistenz zum Verhalten des normalen Tasten-Zweigs, der `Handled` nur bei tatsächlichem Schreiben setzt).

  Empfehlung: Vor dem Aufruf von `ReadClipboardAndInsertAsync()` prüfen, ob `Session?.InputStream != null`, analog zum bestehenden Muster im Zweig für `KeyToVt100Encoder.Encode(e)`, z. B.:
  ```csharp
  if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
  {
      if (Session?.InputStream != null)
      {
          e.Handled = true;
          _ = ReadClipboardAndInsertAsync();
      }
      return;
  }
  ```

- **Doppelter Code** — `WriteToInputStream(byte[] bytes)` und `ReadClipboardAndInsertAsync()` implementieren nahezu identische Logik (Bytes in `Session.InputStream` schreiben, danach `Session.MarkInputActivity()` aufrufen, `catch (Exception ex)` mit `_logger.LogWarning(ex, ...)`), unterscheiden sich nur durch synchrones `Write` vs. asynchrones `WriteAsync` und den Log-Text.

  Empfehlung: Gemeinsame Hilfsmethode extrahieren, z. B. `private async Task WriteToInputStreamAsync(byte[] bytes, string errorContext)`, die von beiden Aufrufstellen (Tastatur-Encoding und Clipboard-Paste) genutzt wird, um Schreiben, `MarkInputActivity()` und Fehlerbehandlung/Logging an einer Stelle zu bündeln.

- **Fehlerbehandlung** — `GetClipboardText()` fängt in `catch (Exception)` jeden Zwischenablage-Zugriffsfehler ab und gibt `string.Empty` zurück, ohne den Fehler zu loggen. Das weicht vom sonstigen Muster in derselben Klasse ab (`WriteToInputStream` und `ReadClipboardAndInsertAsync` loggen beide über `_logger.LogWarning`) und verschluckt diagnostisch relevante Informationen (z. B. Zwischenablage durch anderen Prozess gesperrt, Zugriff verweigert) vollständig.

  Empfehlung: Vor dem `return string.Empty;` eine Warnung loggen, z. B. `_logger.LogWarning(ex, "Fehler beim Lesen aus der Zwischenablage");`, damit das Fehlerverhalten konsistent mit den übrigen catch-Blöcken der Klasse ist.

### KeyToVt100EncoderTests.cs (KeyToVt100EncoderTests)

- **Namenskonventionen und Einheitlichkeit** — Die neue Testdatei liegt unter `src/Softwareschmiede.Tests/Infrastructure/Terminal/KeyToVt100EncoderTests.cs`, obwohl die getestete Klasse `KeyToVt100Encoder` im Namespace `Softwareschmiede.App.Controls` liegt (`src/Softwareschmiede.App/Controls/KeyToVt100Encoder.cs`). Die bestehende Projektkonvention spiegelt den Quell-Namespace/-Ordner im Testordner (siehe `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs`, `TerminalControlTests_ClipboardPaste.cs`, `StatusIndicatorControlTests.cs` — alle unter `App/Controls`), wird hier durchbrochen: Die Datei liegt fälschlich im `Infrastructure/Terminal`-Ordner und trägt den Namespace `Softwareschmiede.Tests.Infrastructure.Terminal` statt `Softwareschmiede.Tests.App.Controls`.

  Empfehlung: Datei nach `src/Softwareschmiede.Tests/App/Controls/KeyToVt100EncoderTests.cs` verschieben und den Namespace auf `Softwareschmiede.Tests.App.Controls` ändern.

## Geprüfte Dateien

- `src/Softwareschmiede.App/Controls/KeyToVt100Encoder.cs`
- `src/Softwareschmiede.App/Controls/TerminalControl.cs`
- `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs`
- `src/Softwareschmiede.Tests/Domain/Terminal/TerminalBufferTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs`
- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests_ClipboardPaste.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/KeyToVt100EncoderTests.cs`
