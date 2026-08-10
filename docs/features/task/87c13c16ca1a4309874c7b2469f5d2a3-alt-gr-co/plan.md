# Umsetzungsplan: Alt Gr-Unterstützung und Ctrl+Pfeiltaste-Navigation

## Übersicht

Der Plan umfasst die Implementierung von Alt Gr-Unterstützung für Sonderzeichen und Ctrl+Pfeiltaste-Navigation zur wortweisen Cursor-Bewegung in der TerminalControl-Komponente. Dies ermöglicht Benutzern, Sonderzeichen (z. B. "@", "{", "}", "|", "~", "`" auf deutschem Tastaturlayout) sowie wortweise Navigation (Ctrl+Links/Rechts) in CLI-Prozessen zu nutzen. Die Änderungen sind auf den `KeyToVt100Encoder` beschränkt, dessen erweiterte VT100-Kodierung automatisch über alle KI-Plugins (Claude CLI, GitHub Copilot, Devin, Codex, BitBucket, etc.) konsistent verfügbar wird.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Alt Gr-Erkennung | Prüfung auf `ModifierKeys.Alt` oder `ModifierKeys.RightAlt` in `Encode()` mit frühem Return (null), wenn Alt ohne Sonderzeichen erkannt wird | WPF verarbeitet Alt Gr-Komposition normalerweise via `OnTextInput`-Event; ein Early Return verhindert doppelte Kodierung und überlässt die Verarbeitung dem normalen TextInput-Flow. Alt Gr-Sonderzeichen kommen typischerweise über `OnTextInput`, nicht über `OnPreviewKeyDown`, daher ist null-Rückgabe das richtige Signal. |
| Ctrl+Pfeiltaste-Sequenzen | VT100-Standard-Sequenzen (CSI 1;5C für Ctrl+Right, CSI 1;5D für Ctrl+Left) in `Encode()` als neue Switch-Cases | Gängige Standard-VT100-Sequenzen; in bash, vim, nano und anderen CLI-Tools standardisiert und erwartet. |
| Modifier-Handling | Erwerbung von `ModifierKeys` über `e.KeyboardDevice.Modifiers` (wie bereits für Ctrl) | Folgt bestehendem Muster in der Codebase; zentralisierte Prüfung an einer Stelle. |
| Auswirkung auf `OnPreviewKeyDown()` und `OnTextInput()` | Keine Änderungen nötig; beide Methoden arbeiten unverändert mit dem erweiterten `Encode()` zusammen | `Encode()` gibt null zurück wenn Alt erkannt wird → `OnTextInput` wird aufgerufen und verarbeitet das resultierende Zeichen. Bei Ctrl+Pfeiltasten gibt `Encode()` die Sequenz zurück → `OnPreviewKeyDown` schreibt sie direkt in den Input-Stream. |

## Programmabläufe

### Alt Gr + Sonderzeichen (z. B. "@" auf deutschem Layout)

1. Benutzer drückt Alt Gr + Taste
2. WPF `TerminalControl.OnPreviewKeyDown()` wird aufgerufen mit dem `KeyEventArgs`
3. `KeyToVt100Encoder.Encode(KeyEventArgs e)` wird aufgerufen
4. `Encode()` prüft `(e.KeyboardDevice.Modifiers & ModifierKeys.Alt) != 0`
5. Wenn Alt erkannt und es sich nicht um eine vordefinierte Tastenkombination handelt → `Encode()` gibt `null` zurück
6. `OnPreviewKeyDown()` beendet sich, Event wird nicht als behandelt markiert
7. WPF ruft `OnTextInput()` auf (Win32-Komposition erfolgt hier)
8. `OnTextInput()` ruft `EncodeText()` auf und schreibt UTF-8-kodierte Bytes in `InputStream`
9. `PseudoConsoleSession` leitet die Bytes an den laufenden CLI-Prozess weiter

**Beteiligte Klassen/Komponenten:** `TerminalControl`, `KeyToVt100Encoder`, `PseudoConsoleSession`

### Ctrl+Pfeiltaste (wortweise Navigation)

1. Benutzer drückt Ctrl+Links oder Ctrl+Rechts
2. WPF `TerminalControl.OnPreviewKeyDown()` wird aufgerufen
3. `KeyToVt100Encoder.Encode(KeyEventArgs e)` wird aufgerufen
4. `Encode()` prüft `(e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0`
5. Bei Ctrl+Left prüft `Encode()` auf `Key.Left` → gibt `byte[] { 0x1b, 0x5b, 0x31, 0x3b, 0x35, 0x44 }` (Escape-Sequenz `\x1b[1;5D`) zurück
6. Bei Ctrl+Right prüft `Encode()` auf `Key.Right` → gibt `byte[] { 0x1b, 0x5b, 0x31, 0x3b, 0x35, 0x43 }` (Escape-Sequenz `\x1b[1;5C`) zurück
7. `OnPreviewKeyDown()` schreibt die Bytes in `InputStream` und markiert Event als behandelt
8. `PseudoConsoleSession` leitet die VT100-Sequenzen an den CLI-Prozess weiter, der sie als wortweise Navigation interpretiert

**Beteiligte Klassen/Komponenten:** `TerminalControl`, `KeyToVt100Encoder`, `PseudoConsoleSession`

## Neue Klassen

Keine neuen Klassen erforderlich. Die Funktionalität wird durch Erweiterung der existierenden `KeyToVt100Encoder`-Klasse implementiert.

## Änderungen an bestehenden Klassen

### `KeyToVt100Encoder` (Utility-Klasse)

- **Geänderte Methoden:**
  - `Encode(KeyEventArgs e)` — Muss erweitert werden um:
    1. Prüfung auf `ModifierKeys.Alt` (oder `ModifierKeys.RightAlt` falls verfügbar)
    2. Early Return mit `null` wenn Alt ohne spezifische Handhabung erkannt wird (damit `OnTextInput` die Komposition übernimmt)
    3. Neue Switch-Cases für `Key.Left` und `Key.Right` mit Ctrl-Modifier-Prüfung
    4. Rückgabe der korrekten VT100-Sequenzen:
       - Ctrl+Left: `\x1b[1;5D` (CSI-Sequenz für Ctrl+Left)
       - Ctrl+Right: `\x1b[1;5C` (CSI-Sequenz für Ctrl+Right)

Die Logik sollte wie folgt aussehen (pseudocode):
- Vor den Switch-Blöcken für spezifische Keys: `var ctrl = (e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0;` und `var alt = (e.KeyboardDevice.Modifiers & ModifierKeys.Alt) != 0;` extrahieren
- Switch über `e.Key`
- Für `Key.Left` und `Key.Right`: Falls `ctrl` === true, die entsprechende Sequenz zurückgeben
- Für alle anderen Tasten mit `alt` === true: `null` zurückgeben (damit `OnTextInput` die Komposition verarbeitet)

### `TerminalControl` (WPF-Komponente)

Keine Änderungen erforderlich. Die Klasse arbeitet unverändert mit dem erweiterten `KeyToVt100Encoder.Encode()` zusammen:
- `OnPreviewKeyDown()` ruft weiterhin `Encode()` auf und schreibt bei nicht-null-Rückgabe die Bytes in den Stream
- `OnTextInput()` wird weiterhin für reguläre Texteingaben aufgerufen

### `PseudoConsoleSession` (Infrastructure-Klasse)

Keine Änderungen erforderlich. Die `InputStream`-Property akzeptiert bereits VT100-kodierte Bytes ohne weitere Verarbeitung.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Keine Regression bei bestehenden Eingaben:** Die neuen Ctrl+Pfeiltasten-Sequenzen werden als zusätzliche Switch-Cases eingefügt; bestehende Tastenbehandlung bleibt unverändert.
- **Alt-Handling könnte bestehende Alt-Kombinationen beeinflussen:** Wenn Alt+Zeichen-Kombinationen zuvor nicht über `OnTextInput` verarbeitet wurden, könnte die neue Logik dieses Verhalten ändern. Dies ist jedoch gewünscht: Alt Gr-Sonderzeichen sollen über `OnTextInput` kommen, und für nicht-Sonderzeichen (z. B. Alt+F, Alt+E für Menüs) sollte `OnTextInput` sowieso nicht relevant sein.
- **Tastaturlayout-abhängigkeit:** Ctrl+Pfeiltasten sind universell; Alt Gr funktioniert nur auf Layouts, die Alt Gr haben (deutsch, französisch, spanisch, etc.). Auf englischen Layouts ohne Alt Gr wird dieses Feature nicht relevant.
- **Keine bekannten Seiteneffekte auf bestehende Tests:** Die Änderungen sind lokal in `Encode()` beschränkt; bestehende Tests für andere Methoden sollten unbeeinträchtigt bleiben.

## Umsetzungsreihenfolge

1. **Erwerbung von Modifier-Keys in `KeyToVt100Encoder.Encode()` vorbereiten**
   - Voraussetzungen: Keine
   - Beschreibung: Referenzen auf `e.KeyboardDevice.Modifiers` werden in der bestehenden Methode bereits verwendet (für Ctrl); keine neuen NuGet-Abhängigkeiten nötig.

2. **Ctrl+Pfeiltaste-VT100-Sequenzen in `KeyToVt100Encoder.Encode()` implementieren**
   - Voraussetzungen: Keine (nur Code-Addition)
   - Beschreibung: Neue Switch-Cases für `Key.Left` und `Key.Right` mit Ctrl-Modifier-Prüfung hinzufügen. Die Sequenzen `\x1b[1;5D` und `\x1b[1;5C` als Byte-Arrays oder Escape-String-Konstanten definieren und zurückgeben.

3. **Alt Gr-Handling (Early Return) in `KeyToVt100Encoder.Encode()` implementieren**
   - Voraussetzungen: Keine
   - Beschreibung: Vor den bestehenden Switch-Blöcken: Prüfung auf `ModifierKeys.Alt`, und falls erkannt und die Taste nicht in einer vordefinierten Liste spezifischer Alt-Kombinationen liegt (z. B. Alt+Shift für Tastaturlayout-Wechsel), `null` zurückgeben, damit `OnTextInput` aufgerufen wird.

4. **Unit-Tests für Ctrl+Pfeiltasten schreiben**
   - Voraussetzungen: Keine (Test-Framework xUnit und FluentAssertions bereits vorhanden)
   - Beschreibung: Tests in `KeyToVt100EncoderTests` für:
     - `Encode()` bei Ctrl+Left → erwartet `\x1b[1;5D`
     - `Encode()` bei Ctrl+Right → erwartet `\x1b[1;5C`
     - Grenzfälle: Ctrl+Up, Ctrl+Down (sollen nicht zu neuen Sequenzen führen, oder falls gewünscht, implementiert werden), Shift+Ctrl+Left/Right (optional, nicht in Anforderung)

5. **Unit-Tests für Alt-Handling schreiben**
   - Voraussetzungen: Keine
   - Beschreibung: Tests in `KeyToVt100EncoderTests` für:
     - Alt + beliebige Taste → erwartet `null` (damit `OnTextInput` aufgerufen wird)
     - Alt + Shift + Taste → erwartet `null`
     - Prüfung, dass andere Modifier nicht betroffen sind

6. **E2E-Test für Alt Gr + Sonderzeichen (deutsches Layout)**
   - Voraussetzungen: Test-Infrastruktur `WpfTestBase` für FlaUI (vorhanden)
   - Beschreibung: Laufe die App, starte einen CLI-Prozess (z. B. `cmd.exe` oder `bash`), sende Alt Gr + 5 (sollte "{" auf deutschem Layout produzieren), validiere, dass das Zeichen in der CLI-Ausgabe erscheint.

7. **E2E-Test für Ctrl+Links und Ctrl+Rechts (wortweise Navigation)**
   - Voraussetzungen: Test-Infrastruktur `WpfTestBase` für FlaUI (vorhanden), CLI-Prozess, der VT100-Sequenzen interpretiert (z. B. bash)
   - Beschreibung: Laufe die App, starte einen bash-Prozess mit aktivem History-Mode, gebe ein Wort ein, positioniere den Cursor mit Ctrl+Links/Rechts, validiere, dass der Cursor an den erwarteten Positionen ist (über Cursor-Ausgabe in bash oder ähnliches).

8. **Bestehende Tests verifizieren**
   - Voraussetzungen: Alle vorherigen Unit-Tests erfolgreich
   - Beschreibung: Full build und vollständige Test-Suite ausführen, um sicherzustellen, dass keine Regression in bestehenden Tests vorhanden ist.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Encode_CtrlLeftKey_ReturnsVt100ControlLeftSequence` | `KeyToVt100EncoderTests` | `Encode(KeyEventArgs)` mit Ctrl+Left gibt `\x1b[1;5D` zurück |
| `Encode_CtrlRightKey_ReturnsVt100ControlRightSequence` | `KeyToVt100EncoderTests` | `Encode(KeyEventArgs)` mit Ctrl+Right gibt `\x1b[1;5C` zurück |
| `Encode_AltModifierWithoutSpecificKey_ReturnsNull` | `KeyToVt100EncoderTests` | `Encode(KeyEventArgs)` mit Alt (z. B. Alt+A) gibt `null` zurück, damit `OnTextInput` die Verarbeitung übernimmt |
| `Encode_AltShiftCombination_ReturnsNull` | `KeyToVt100EncoderTests` | `Encode(KeyEventArgs)` mit Alt+Shift gibt `null` zurück |
| `Encode_CtrlShiftLeftKey_BehaviorToBeDetermined` | `KeyToVt100EncoderTests` | (Optional) Behavior für Shift+Ctrl+Left: entweder spezifische Sequenz oder `null` — abhängig von Klärung offener Punkte |
| E2E Test: Alt Gr + Sonderzeichen (Deutsch) | `TerminalControlE2ETests` oder neuer Test-Klasse | Benutzer gibt Alt Gr + 5 ein (deutsches Layout, "{"), validiert, dass das Zeichen im CLI-Prozess ankommt |
| E2E Test: Ctrl+Links für wortweise Navigation | `TerminalControlE2ETests` oder neuer Test-Klasse | Benutzer gibt Text ein, positioniert Cursor mit Ctrl+Links/Rechts, validiert Cursor-Position in bash oder ähnliches |

### Betroffene bestehende Tests

Keine. Die Änderungen an `Encode()` sind Erweiterungen (neue Switch-Cases, neue Modifier-Prüfung), die bestehende Testfälle für andere Tasten nicht beeinflussen.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Alt Gr + Sonderzeichen können in CLI eingegeben werden | `TerminalControlE2ETests` (neu oder erweitert) | Anforderung 1: "Die TerminalControl muss Sonderzeichen akzeptieren, die über Alt Gr auf dem System-Tastaturlayout erreichbar sind." |
| Ctrl+Links bewegt Cursor wortweise nach links | `TerminalControlE2ETests` (neu oder erweitert) | Anforderung 4: "Die Tasten-Kombinationen Strg+Links und Strg+Rechts müssen als VT100-Sequenzen kodiert werden, die wortweise Cursor-Navigation ermöglichen." |
| Ctrl+Rechts bewegt Cursor wortweise nach rechts | `TerminalControlE2ETests` (neu oder erweitert) | Anforderung 4: "Die Tasten-Kombinationen Strg+Links und Strg+Rechts müssen als VT100-Sequenzen kodiert werden, die wortweise Cursor-Navigation ermöglichen." |
| Keine Regression: Bestehende Tastatureingaben (Buchstaben, Zahlen, Enter, Pfeiltasten) funktionieren weiterhin | `TerminalControlE2ETests` (bestehender Test erweitern oder neuer Smoke-Test) | Anforderung 7: "Es darf keine Regression bei bestehenden Tastatureingaben geben." |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine. Die Umsetzung ändert das bestehende Verhalten bestehender Tasten nicht; nur neue Tasten/Modifier-Kombinationen werden hinzugefügt.

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Auf welchen Tastaturlayouts soll die Alt Gr-Unterstützung primär validiert werden (deutsch, englisch, andere)? | **Empfehlung:** Deutsches Layout als primäres Validierungs-Target (es ist der Standard-Kontext des Projekts, siehe Repository-Sprache). Englisches Layout als sekundäres Fallback-Target, da dort Alt Gr meist nicht existiert und die Eingabe über `OnTextInput` weiterhin funktioniert. Tests müssen layoutabhängig sein oder über generische Sonderzeichen ausgelöst werden (z. B. über TextInput-Event statt DirectX-Tastaturlayout-Abfrage). |
| 2 | Sollten auch Varianten wie Shift+Strg+Pfeiltaste (Markierung + wortweise Navigation) unterstützt werden? | **Empfehlung:** Nein, nicht in dieser Implementierung. Die Anforderung nennt nur Strg+Pfeiltaste. Shift+Strg+Pfeiltaste ist ein Feature für Markierungs-Verhalten, das terminal-seitig (bash, vim, etc.) gehandhabt wird und nicht vom Terminal-Control gesteuert werden sollte. Falls einzelne CLI-Tools dies brauchen, können sie ihre Eingabe-Remapping selbst verwalten. |
| 3 | Gibt es bekannte Einschränkungen der WPF `KeyEventArgs` beim Unterscheiden von Alt und AltGr, die beachtet werden müssen? | **Empfehlung:** WPF unterscheidet nicht explizit zwischen Alt und AltGr; AltGr wird typischerweise als `ModifierKeys.Alt` erkannt. Dies ist das erwartete Verhalten in `OnPreviewKeyDown`. Die resultierenden Sonderzeichen kommen über `OnTextInput` (Win32-Komposition), nicht über `OnPreviewKeyDown` — daher ist die Early-Return-Strategie (Rückgabe von `null` bei Alt) korrekt. **Implementierungstest erforderlich:** Auf deutschem Layout mit echtem Alt Gr verfügbar, um zu validieren, dass "{", "}", etc. tatsächlich über `OnTextInput` kommen. |
| 4 | Sollen alternative VT100-Sequenzen für Strg+Pfeiltaste unterstützt werden (z. B. `\x1b[1;5C` vs. `\x1b[5C`)? Welche erwarten die aktuellen KI-Plugins? | **Empfehlung:** Standard-Sequenzen `\x1b[1;5C` (Ctrl+Right) und `\x1b[1;5D` (Ctrl+Left) verwenden — diese sind in bash, vim, nano und den meisten POSIX-Tools standardisiert und erwartet. Alternative `\x1b[5C` ist weniger weit verbreitet. KI-Plugins (Claude CLI, GitHub Copilot, etc.) nutzen keine eigenen VT100-Varianten; sie leiten an bash oder ähnliche Shells weiter, die diese Standard-Sequenzen verarbeiten. |
| 5 | Wie soll mit Tastaturlayouts umgegangen werden, auf denen Alt Gr nicht existiert (z. B. englisches US-Layout)? | **Empfehlung:** Keine spezielle Behandlung nötig. Auf Layouts ohne Alt Gr wird `ModifierKeys.Alt` nicht gesetzt (es sei denn, der Benutzer drückt tatsächlich Alt), daher funktioniert die Logik korrekt: Alt+Zeichen-Kombinationen geben `null` zurück, und normale Eingaben (ohne Alt) werden wie üblich verarbeitet. Die Feature ist layout-agnostisch implementiert und funktioniert überall. |
| 6 | Soll eine Validierung gegen eine Whitelist von bekannten KI-Plugins durchgeführt werden, oder ist eine generische Lösung ausreichend? | **Empfehlung:** Generische Lösung ist ausreichend. Die Implementierung ist im Terminal-Control, nicht im Plugin-System; alle CLI-Prozesse (unabhängig von ihrer Herkunft als KI-Plugin oder anderes) erhalten automatisch die korrekt kodierten Eingaben. Eine Plugin-Whitelist ist unnötig und würde zukünftige Erweiterungen hemmen. |

