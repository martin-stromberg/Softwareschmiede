# Logik-Analyse

## `KeyToVt100Encoder`

Datei: `src/Softwareschmiede.App/Controls/KeyToVt100Encoder.cs`

Statische Utility-Klasse zur Kodierung von WPF-Tastaturereignissen in VT100-Byte-Sequenzen.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Encode(KeyEventArgs e)` | internal static | Kodiert ein Tastaturereignis zu VT100-Bytes oder null; aktuell: Ctrl+A-Z, Funktionstasten, Navigationstasten, Enter, Backspace, Tab, Escape, Delete |
| `EncodeText(string text)` | internal static | Kodiert einen Text als UTF-8-Bytes |
| `EncodeClipboardText(string? text)` | internal static | Kodiert Zwischenablage-Text mit Newline-Normalisierung zu CR (\r) |

### Aktuelles Verhalten von `Encode(KeyEventArgs e)`

- **Ctrl+A bis Ctrl+Z:** Rückgabe von einzelnen Bytes (ASCII 1-26)
- **Funktionstasten F1-F12:** VT100-Escape-Sequenzen (z. B. F1 → `\x1bOP`)
- **Navigationstasten:** 
  - Up, Down, Left, Right → `\x1b[A`, `\x1b[B`, `\x1b[D`, `\x1b[C`
  - Home, End → `\x1b[H`, `\x1b[F`
  - Page Up, Page Down → `\x1b[5~`, `\x1b[6~`
- **Sonstige:** Enter → `\x0D`, Back → `\x7F`, Tab → `\x09`, Escape → `\x1B`, Delete → `\x1b[3~`
- **Nicht unterstützt:** Alt Gr-Sonderzeichen, Ctrl+Links/Rechts für wortweise Navigation

### Fehlende Features

1. **Alt Gr-Unterstützung:** Keine Prüfung auf `ModifierKeys.Alt` oder `ModifierKeys.RightAlt`
2. **Ctrl+Links/Rechts:** Kein Switch-Eintrag für `Key.Left` oder `Key.Right` mit Ctrl-Modifier
3. **Shift+Ctrl-Kombinationen:** Nicht in der Anforderung genannt, aber auch nicht vorhanden

---

## `TerminalControl`

Datei: `src/Softwareschmiede.App/Controls/TerminalControl.cs`

WPF-Control zur Renderung einer `PseudoConsoleSession` und Weiterleitung von Tastatureingaben. Erbt von `FrameworkElement` und implementiert `IScrollInfo`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnPreviewKeyDown(KeyEventArgs e)` | protected override | Ruft `KeyToVt100Encoder.Encode()` auf; schreibt zurückgegebene Bytes in `InputStream`; sondert Ctrl+V (Zwischenablage) aus |
| `OnTextInput(TextCompositionEventArgs e)` | protected override | Ruft `KeyToVt100Encoder.EncodeText()` auf; wird für reguläre Texteingaben (inkl. via Komposition) verwendet |
| `WriteToInputStream(byte[] bytes)` | private | Schreibt Bytes in `Session.InputStream` und ruft `MarkInputActivity()` auf |
| `ReadClipboardAndInsertAsync()` | private async | Liest Zwischenablage-Text, kodiert ihn mit `EncodeClipboardText()` und schreibt ihn asynchron in den Input-Stream |

### Ablauf der Tastatureingabe

1. **OnPreviewKeyDown** wird aufgerufen
2. Spezialfall: Ctrl+V → `ReadClipboardAndInsertAsync()`
3. Allgemein: `KeyToVt100Encoder.Encode(e)` wird aufgerufen
   - Wenn Rückgabe nicht null: Bytes werden in `InputStream` geschrieben, Event wird als behandelt markiert
   - Wenn Rückgabe null: Falls `OnTextInput` folgt, wird dort die Zeicheneingabe verarbeitet
4. **OnTextInput** wird aufgerufen (nur wenn nicht in OnPreviewKeyDown behandelt)
   - Text wird via `EncodeText()` zu UTF-8 kodiert und in `InputStream` geschrieben

### Abhängigkeiten

- Ruft `KeyToVt100Encoder.Encode()`, `EncodeText()` und `EncodeClipboardText()` auf
- Schreibt in `Session.InputStream` (vom Typ `Stream`, bereitgestellt durch `PseudoConsoleSession`)
- Ruft `Session.MarkInputActivity()` auf nach erfolgreicher Eingabe

---

## `PseudoConsoleSession`

Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

Verwaltete Sitzung einer laufenden Pseudo-Console mit Eingabe- und Ausgabe-Stream-Verwaltung.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `MarkInputActivity()` | public | Meldet eine Benutzereingabe an die Status-Erkennung |
| `MarkOutputActivity()` | public | Meldet gelesene Ausgabe an die Status-Erkennung |
| `Resize(int cols, int rows)` | public | Ändert die Größe der Pseudo Console |
| `Dispose()` | public | Beendet die Sitzung und gibt Ressourcen frei |
| `DrainOutputAsync(TimeSpan timeout, CancellationToken ct)` | public async | Wartet auf das Ende der Leseschleife |
| `ReadLoopAsync(CancellationToken ct)` | private async | Kontinuierliche Schleife: liest Bytes aus `OutputStream`, parsed sie, wendet sie auf `Buffer` an |
| `WritePromptAsync(string prompt, CancellationToken ct)` | public async | Schreibt einen Prompt mit Newline-Normalisierung in `InputStream` |
| `NormalizeToCarriageReturn(string text)` | public static | Konvertiert alle Zeilenenden (\r\n und \n) zu einzelnem \r |

### Properties

- **`InputStream`** (public, readonly): `Stream` zum Schreiben von Tastatureingaben an den Prozess
- **`OutputStream`** (public, readonly): `Stream` zum Lesen der Prozessausgabe
- **`Process`** (public, readonly): Verwalteter `Process`
- **`RuntimeStatus`** (public): Laufzeitstatus (Inaktiv, Laeuft, WartetAufEingabe)
- **`Buffer`** (public, readonly): `TerminalBuffer` zur Speicherung des Terminal-Inhalts

### Integration mit TerminalControl

- `InputStream` ist das Ziel für `TerminalControl.WriteToInputStream()`
- `InputStream` akzeptiert beliebige UTF-8- oder VT100-kodierte Bytes (keine Verarbeitung)
- Keine Änderungen an `InputStream` erforderlich für Alt Gr oder Ctrl+Pfeiltasten
