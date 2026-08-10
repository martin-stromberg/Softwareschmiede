# Anforderung

## Fachliche Zusammenfassung

Der CLI-Konsolen-Input in der `TerminalControl`-Komponente unterstützt derzeit nicht die Eingabe von Sonderzeichen, die über die Alt Gr-Tastenkombination erreichbar sind (z. B. "@", "{", "}", "|", "~", "`" auf deutschem Tastaturlayout). Zusätzlich fehlt die Unterstützung für die wortweise Navigation des Eingabefokus mit Strg+Pfeiltaste (links/rechts). Diese Eingabeeinschränkungen betreffen alle KI-Plugins (Claude CLI, GitHub Copilot, Devin, Codex, BitBucket, etc.), die CLI-Prozesse über die Pseudo-Console starten und mit Benutzereingaben interagieren. Eine Behebung muss sicherstellen, dass die Eingabemöglichkeiten einheitlich über alle Plugins funktionieren.

## Betroffene Klassen und Komponenten

### Tastaturcodierung (Encoder)
- `KeyToVt100Encoder` (Softwareschmiede.App/Controls/KeyToVt100Encoder.cs): VT100-Kodierung von Tastaturereignissen
  - Methode `Encode(KeyEventArgs e)`: Muss Alt Gr und Modifier-Kombinationen korrekt erkennen und kodieren
  - Behandlung von Strg+Links und Strg+Rechts (wortweise Navigation)
  - Korrekte Differenzierung zwischen Alt, AltGr und deren Kombination mit anderen Tasten

### Terminal-Input-Verarbeitung
- `TerminalControl` (Softwareschmiede.App/Controls/TerminalControl.cs): WPF-Control für Terminal-Rendering
  - Methode `OnPreviewKeyDown(KeyEventArgs e)`: Koordiniert `KeyToVt100Encoder.Encode()` und leitet Bytes an Input-Stream
  - Mögliches Handling von Modifier-Flags bei der Tastatur-Event-Verarbeitung

### Pseudo-Console-Infrastruktur
- `PseudoConsoleSession` (Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs): Verwaltet Input-Stream
  - Methode `InputStream`: Nimmt bereits VT100-kodierte Bytes an; keine Änderung erforderlich

### Tests
- Neue oder erweiterte Unit-Tests in `KeyToVt100EncoderTests.cs` für:
  - Alt Gr + alphanumerische Tasten
  - Alt Gr + Sonderzeichen (je Tastaturlayout)
  - Strg+Links und Strg+Rechts
  - Grenzfälle bei Modifier-Kombinationen
- Integrationstests zur Validierung mit echten CLI-Prozessen (ggf. bestehende E2E-Tests)

## Funktionale Anforderungen

1. Die `TerminalControl` muss Sonderzeichen akzeptieren, die über Alt Gr auf dem System-Tastaturlayout erreichbar sind.
2. Die Alt Gr-Eingabe muss durch `KeyToVt100Encoder.Encode()` korrekt erkannt und – falls nötig – als VT100-Sequenz kodiert werden.
3. Ist Alt Gr + Zeichen über das WPF `TextInput`-Event verfügbar, muss dieses (wie bei normalen Zeichen) verwendet werden.
4. Die Tasten-Kombinationen Strg+Links und Strg+Rechts müssen als VT100-Sequenzen kodiert werden, die wortweise Cursor-Navigation ermöglichen.
5. Alle Eingaben (Alt Gr, Strg+Pfeiltaste, normale Zeichen) müssen korrekt an den Input-Stream der `PseudoConsoleSession` geschrieben werden.
6. Die Implementierung muss sicherstellen, dass KI-Plugins (Claude CLI, GitHub Copilot, Devin, Codex, BitBucket, etc.) diese Eingabemöglichkeiten konsistent nutzen können.
7. Es darf keine Regression bei bestehenden Tastatureingaben geben (Buchstaben, Zahlen, Enter, Pfeiltasten, Funktionstasten, Strg+A bis Strg+Z).

## Implementierungsansatz

### Alt Gr-Unterstützung

Alt Gr ist auf verschiedenen Tastaturlayouts unterschiedlich implementiert:
- Auf deutschen Tastaturen: Oft als Ctrl+Alt erkannt (wird als Rechts-Alt weitergeleitet)
- Auf anderen Layouts (z. B. französisch, spanisch): Ähnliches Verhalten

Strategie:
- In `KeyToVt100Encoder.Encode()` prüfen, ob `ModifierKeys.Alt` gesetzt ist (ggf. auch `ModifierKeys.RightAlt` wenn verfügbar)
- Wenn Alt + normaler Buchstabe/Zahl: Normalerweise wird dies über `OnTextInput` verarbeitet (WPF verarbeitet die Komposition), daher keine Änderung nötig
- Wenn Alt + Sonderzeichen (z. B. auf deutschem Layout: Alt Gr + 5 = "{"), wird dies typischerweise auch über `OnTextInput` kommen, muss aber sichergestellt werden
- **Annahme:** Die Hauptbeschränkung liegt darin, dass `OnPreviewKeyDown` versucht, alle Sonderzeichen zu kodieren, bevor `OnTextInput` aufgerufen wird. Eine Überprüfung der Alt/AltGr-Logik in `OnPreviewKeyDown` könnte notwendig sein.

### Strg+Pfeiltaste-Unterstützung

Strg+Links und Strg+Rechts sind Standard-VT100-Sequenzen für wortweise Navigation:
- `Strg+Rechts`: `\x1b[1;5C` (CSI 1 ; 5 C)
- `Strg+Links`: `\x1b[1;5D` (CSI 1 ; 5 D)

Diese Sequenzen sind in gängigen CLI-Tools (bash, vim, nano, …) standardisiert.

Erweiterung von `KeyToVt100Encoder.Encode()`:
- Prüfen, ob `ModifierKeys.Control` gesetzt ist
- Bei `Key.Left` mit Ctrl: Rückgabe von `\x1b[1;5D`
- Bei `Key.Right` mit Ctrl: Rückgabe von `\x1b[1;5C`
- Optional: Auch andere Ctrl+Arrow-Kombinationen (Up, Down) für zeilenweise Navigation hinzufügen

### Implementierungsdetails

1. **In `KeyToVt100Encoder.Encode()`:**
   - Aktuell wird nur Ctrl + A–Z behandelt. Dies muss ausgebaut werden zu einem allgemeinen Modifier-Handling.
   - Struktur: `var ctrl = (e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0;` prüft Ctrl
   - Analog: Prüfung für `ModifierKeys.Alt` hinzufügen
   - Switch-Statement erweitern um Einträge für `Key.Left` und `Key.Right` mit Ctrl-Prüfung
   - Bei Alt-gesetzen Tasten: Normalerweise via `OnTextInput` behandeln (null aus `Encode()` zurückgeben)

2. **In `TerminalControl.OnPreviewKeyDown()`:**
   - Aktuell unverändert; die Logik bleibt bei `KeyToVt100Encoder.Encode()`.
   - Möglicherweise: Explizite Handhabung von Alt/AltGr vor der `Encode()`-Prüfung, um sicherzustellen, dass diese das `TextInput`-Event korrekt empfangen.

3. **Tests:**
   - Unit-Tests für `KeyToVt100Encoder.Encode()`:
     - Alt + Zeichen (u. U. null, da via TextInput verarbeitet)
     - Strg+Links und Strg+Rechts VT100-Sequenzen
     - Grenzfälle: Strg+Alt+Zeichen, Shift+Strg+Zeichen, etc.
   - Integrationstests: Starten eines CLI-Prozesses in `PseudoConsoleSession`, Senden von Alt Gr + "@" und Strg+Links, Validieren der Ausgabe

## Konfiguration

Keine zusätzliche Konfiguration erforderlich. Die Tastaturunterstützung wird zentral in `KeyToVt100Encoder` gesteuert und gilt automatisch für alle KI-Plugins.

## Nicht-Ziele

- Änderung der Tastatur-Layouts oder Unterstützung exotischer Tastaturen
- Umdefinition von bestehenden Strg+Kombinationen (z. B. Strg+C, Strg+V)
- Änderung der `OnTextInput`-Verarbeitung außer zur Sicherstellung korrekter Alt Gr-Komposition
- Umgestaltung der `PseudoConsoleSession` oder VT100-Ausgabe-Verarbeitung

## Offene Fragen

1. Auf welchen Tastaturlayouts soll die Alt Gr-Unterstützung primär validiert werden (deutsch, englisch, andere)?
2. Sollten auch Varianten wie Shift+Strg+Pfeiltaste (Markierung + wortweise Navigation) unterstützt werden?
3. Gibt es bekannte Einschränkungen der WPF `KeyEventArgs` beim Unterscheiden von Alt und AltGr, die beachtet werden müssen?
4. Sollen alternative VT100-Sequenzen für Strg+Pfeiltaste unterstützt werden (z. B. `\x1b[1;5C` vs. `\x1b[5C`)? Welche erwarten die aktuellen KI-Plugins?
5. Wie soll mit Tastaturlayouts umgegangen werden, auf denen Alt Gr nicht existiert oder anders implementiert ist?
6. Soll eine Validierung gegen eine Whitelist von bekannten KI-Plugins durchgeführt werden, oder ist eine generische Lösung ausreichend?
