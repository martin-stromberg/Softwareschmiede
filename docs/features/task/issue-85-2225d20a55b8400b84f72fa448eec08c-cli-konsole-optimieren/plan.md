# Umsetzungsplan: CLI Konsole optimieren

## Übersicht

Der Plan adressiert zwei zentrale Verbesserungen der Terminal-Funktionalität:

1. **Stabilitäts-Fix:** Behebung von Race Conditions und Rendering-Problemen durch Verbesserung der Synchronisierung zwischen `PseudoConsoleSession` (Leseschleife), `TerminalBuffer.Apply()` und `TerminalControl.OnRender()`.
2. **Feature-Addition:** Implementierung der Clipboard-Paste-Funktionalität (`Ctrl+V`) mit Fehlerbehandlung und ggfs. Konfigurationsunterstützung.

Betroffene Schichten: Domain Layer (TerminalBuffer, TerminalEvent), Infrastructure Layer (PseudoConsoleSession, KeyToVt100Encoder), Presentation Layer (TerminalControl).

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Buffer-Synchronisierung (Race-Condition-Fix)** | Verbesserung der bestehenden Lock-basierten Synchronisierung durch atomares Event-Firing nach Lock-Release und ggfs. Einführung einer `GetSnapshot()`-Methode für Render-Operationen. Kein radikaler Wechsel zu Copy-on-Write-Semantik in dieser Iteration. | Die aktuelle Lock-Granularität ist vorhanden und funktioniert teilweise. Eine minimale, bewährte Verbesserung (Double-Check, Event-Timing) ist risikoärmer als ein kompletter Ansatz-Wechsel. Copy-on-Read kann später evaluiert werden, wenn sich Contention-Probleme zeigen. |
| **Clipboard-Paste-Mechanik** | Raw-Text-Eingabe (UTF-8) über `InputStream.WriteAsync()`, nicht über VT100-Escape-Sequenzen. Newlines werden konsistent als `\r` (CR) behandelt (Standard für CLI-Input). | Rohtext ist einfacher, performanter und kompatibel mit Windows-Clipboards. VT100-Escape für Raw-Text wäre unnötig komplex. CR ist Windows-Standard für CLI-Eingaben. |
| **Clipboard-Paste-Ereignis** | Keine neue `ClipboardPasteEvent`-Klasse. Paste wird direkt in `InputStream` geschrieben, nicht über Event-System verarbeitet. | Das Event-System ist für Parser-Events konzipiert (Ausgabe-Parsing). Eingabe-Events müssen direkt in den Input-Stream. Eine neue Event-Klasse würde zusätzliche Komplexität ohne Nutzen hinzufügen. |
| **Fehlerbehandlung Clipboard** | Strukturierte Fehlerbehandlung in `TerminalControl.ReadClipboardAndInsertAsync()` mit Try-Catch, Logger-Warnung, kein UI-Fehler-Popup. | Clipboard-Zugriff kann fehlschlagen (keine Berechtigung, Clipboard leer, I/O-Fehler). Das Control sollte robust weiterlaufen; ein Dialog wäre aufdringlich für einen Input-Fehler. |
| **Konfiguration** | Optionale Konfigurationseinträge für Clipboard-Paste sind vorgesehen, aber nicht verpflichtend in dieser Iteration. Standard-Verhalten ist: Paste aktiviert, `\r` als Zeilenseparator, kein Rate-Limiting. | Konfigurierbarkeit ist nice-to-have. Zunächst wird ein vernünftiger Default implementiert; Rate-Limiting und Aktivierung/Deaktivierung können später hinzugefügt werden, falls nötig. |

---

## Programmabläufe

### Ablauf 1: Buffer-Rendering mit verbesserter Synchronisierung

1. `PseudoConsoleSession.ReadLoopAsync()` liest Bytes aus dem Output-Stream.
2. `AnsiSequenceParser.Parse()` verarbeitet Bytes zu `TerminalEvent`-Instanzen.
3. Für jeden Event: `TerminalBuffer.Apply(event)` wird aufgerufen.
4. Innerhalb von `TerminalBuffer.Apply()`:
   - Lock auf `_lock` wird erworben.
   - Interne Zustandsänderungen werden angewendet (Grid-Update, Cursor-Bewegung, Farben).
   - **Neu:** Eine Versions-Markierung (`_bufferVersion`) wird inkrementiert, um konsistente Snapshot-Lesevorgänge zu ermöglichen.
   - Lock wird freigegeben.
5. **Kritisch (neu):** `BufferChanged`-Event wird **nach** Lock-Release gefeuert (war möglicherweise Quelle der Race Condition).
6. `TerminalControl.OnBufferChanged()` wird aufgerufen → `Dispatcher.InvokeAsync(InvalidateVisual)`.
7. `TerminalControl.OnRender(DrawingContext dc)` wird zur Render-Zeit aufgerufen.
8. Innerhalb von `OnRender()`:
   - Optionale neue Methode: `TerminalBuffer.GetSnapshot()` wird aufgerufen, die unter Lock eine Kopie des aktuellen Zustandssnapshots zurückgibt (Grid, Cursor, Farben).
   - Oder: Grid wird direkt unter Lock gelesen (bestehender Ansatz, aber mit verbesserter Synchronisierung bestätigt).
   - Render-Operationen sind Lock-frei.

**Beteiligte Klassen/Komponenten:** `PseudoConsoleSession`, `AnsiSequenceParser`, `TerminalBuffer`, `TerminalControl`

### Ablauf 2: Clipboard-Paste-Eingabe (Ctrl+V)

1. Benutzer drückt `Ctrl+V` auf dem fokussierten `TerminalControl`.
2. `TerminalControl.OnPreviewKeyDown(KeyEventArgs e)` wird aufgerufen.
3. **Neu:** Check auf `e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0`.
4. Wenn Bedingung erfüllt: `e.Handled = true` und `ReadClipboardAndInsertAsync()` wird aufgerufen (feuer-und-vergessen via `_ =`).
5. Innerhalb von `ReadClipboardAndInsertAsync()`:
   - `GetClipboardText()` wird aufgerufen (liest `System.Windows.Clipboard.GetText()` oder wertet Fehler ab).
   - Wenn Text nicht leer: `KeyToVt100Encoder.EncodeClipboardText(text)` wird aufgerufen.
   - Resultierende Bytes werden via `await Session.InputStream.WriteAsync(bytes)` geschrieben.
   - `Session.MarkInputActivity()` wird aufgerufen, um Runtime-Status zu aktualisieren.
   - Bei Fehler: Exception wird abgefangen, Logger-Warnung wird geschrieben.

**Beteiligte Klassen/Komponenten:** `TerminalControl`, `KeyToVt100Encoder`, `PseudoConsoleSession`, `System.Windows.Clipboard`

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| — | — | Keine neuen Klassen erforderlich. |

---

## Änderungen an bestehenden Klassen

### `TerminalBuffer` (Domain/Terminal)

- **Neue Eigenschaft:** `_bufferVersion` (`int`, private) — Versions-Zähler zur Unterstützung von konsistenten Snapshot-Lesevorgängen. Wird inkrementiert jedesmal, wenn `Apply()` den Buffer modifiziert (optional, nur falls `GetSnapshot()` implementiert wird).
- **Neue Methode (optional):** `GetSnapshot()` → `TerminalBufferSnapshot` (oder Tuple/Record mit Grid-Kopie, Cursor, Farben) — Gibt einen konsistenten Snapshot des aktuellen Buffer-Zustands unter Lock zurück. Wird in `TerminalControl.OnRender()` verwendet, um Race Conditions zu vermeiden. *(Alternative: Lock-Schutz in `GetRow()` und anderen Lesemethoden verstärken)*
- **Geänderte Methode:** `Apply(TerminalEvent evt)` — Synchronisierungsverbesserung:
  - `BufferChanged`-Event wird **nach** Lock-Release gefeuert (Critical Fix).
  - Optional: `_bufferVersion` wird inkrementiert (nur falls `GetSnapshot()` implementiert).

### `KeyToVt100Encoder` (Infrastructure/Terminal)

- **Neue Methode:** `EncodeClipboardText(string text)` → `byte[]` — Kodiert Clipboard-Text als UTF-8-Byte-Array mit Newline-Handling:
  - Input: Text mit möglicherweise unterschiedlichen Zeilenseparatoren (`\n`, `\r\n`, `\r`).
  - Verarbeitung: Alle Newlines werden zu `\r` (CR) normalisiert.
  - Output: UTF-8-kodierte Bytes.
  - Behandlung von Sonderfällen: Null, Leerstring (gibt leeres Array zurück).

### `TerminalControl` (Presentation/Controls)

- **Neue private Methode:** `ReadClipboardAndInsertAsync()` → `Task` — Asynchrone Methode zum Lesen der Zwischenablage und Schreiben in `InputStream`:
  - Ruft `GetClipboardText()` auf.
  - Ruft `KeyToVt100Encoder.EncodeClipboardText(text)` auf.
  - Schreibt Bytes via `await Session.InputStream.WriteAsync(bytes)`.
  - Ruft `Session.MarkInputActivity()` auf.
  - Fehlerbehandlung: Try-Catch um gesamte Operation, Logger-Warnung bei Fehler.

- **Neue private Methode:** `GetClipboardText()` → `string` — Hilfsmethod zum Lesen der Zwischenablage:
  - Liest `System.Windows.Clipboard.GetText()`.
  - Fehlerbehandlung: Try-Catch, gibt `string.Empty` bei Fehler zurück.

- **Geänderte Methode:** `OnPreviewKeyDown(KeyEventArgs e)` — Zusätzliche Prüfung auf `Ctrl+V`:
  - Vor dem bestehenden Handler: Check auf `e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0`.
  - Wenn erfüllt: `e.Handled = true` und `_ = ReadClipboardAndInsertAsync()`.
  - Sonst: Bestehender Handler-Code wird ausgeführt.

---

## Datenbankmigrationen

Keine.

---

## Validierungsregeln

Keine neuen Validierungsregeln erforderlich. Clipboard-Text wird als Raw-UTF-8 ohne spezifische Validierung verarbeitet (CLI-Prozess legt Einschränkungen fest).

---

## Konfigurationsänderungen

Keine **verpflichtenden** Konfigurationsänderungen in dieser Iteration. Die folgenden Einträge sind optional für zukünftige Erweiterungen dokumentiert:

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `TerminalClipboardPasteEnabled` (optional) | `bool` | `true` | Aktiviert/Deaktiviert Clipboard-Paste-Funktionalität |
| `TerminalPasteLineSeparator` (optional) | `string` | `"\r"` | Zeichen-Sequenz für Zeilenumbrüche in Clipboard-Paste |
| `TerminalPasteMaxBytesPerSecond` (optional) | `int` | `0` (unlimited) | Rate-Limiting für Clipboard-Paste (0 = unlimitiert) |

**Anmerkung:** Diese Einträge werden **nicht** implementiert, sondern nur dokumentiert für potenzielle zukünftige Versionen. Die aktuelle Implementierung nutzt Defaults.

---

## Seiteneffekte und Risiken

- **Synchronisierungs-Verbesse­rung im `Apply()`-Verhalten:** Änderung des Timings von `BufferChanged`-Event könnte theoretisch bestehende Test-Timing-Annahmen beeinflussen. **Mitigation:** `TerminalControlTests` müssen überprüft werden; ggfs. Tests auf neue Event-Timing-Annahmen angepasst werden.
  
- **Neue Abhängigkeit zu `System.Windows.Clipboard`:** `TerminalControl` wird abhängig von WPF-Clipboard-API. **Mitigation:** Fehlerbehandlung ist implementiert; sollte bei Clipboard-Zugriff-Fehlern robust weiterlaufen.

- **Performance-Risiko bei sehr großen Clipboard-Texten:** Sehr große Paste-Operationen könnten CLI-Input-Buffer überladen oder UI-Thread blockieren. **Mitigation:** Implementierung asynchron via `WriteAsync()`. Rate-Limiting kann in zukünftigen Versionen hinzugefügt werden, falls nötig.

- **Keine bekannten kritischen Seiteneffekte auf bestehende Features.** Änderungen sind isoliert auf Buffer-Synchronisierung und Keyboard-Input-Handling.

---

## Umsetzungsreihenfolge

### Phase 1: Buffer-Synchronisierungs-Fix (Stabilitäts-Fix)

1. **Analyse und Verbesserung von `TerminalBuffer.Apply()`-Synchronisierung**
   - Voraussetzungen: Keine.
   - Beschreibung: Überprüfung des aktuellen Lock-Verhaltens in `TerminalBuffer.Apply()`. Verbesserung durch:
     - Sicherstellen, dass `BufferChanged`-Event **nach** Lock-Release gefeuert wird (kritischer Fix).
     - Optional: Einführung einer `_bufferVersion`-Markierung für zukünftige Snapshot-Unterstützung.
     - Code-Review: Überprüfung, dass alle Grid-Zugriffe innerhalb des Locks erfolgen.
   - Zielcode: `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs`

2. **Implementierung von `TerminalBuffer.GetSnapshot()` (optional, aber empfohlen)**
   - Voraussetzungen: Schritt 1 abgeschlossen.
   - Beschreibung: Neue Methode, die einen konsistenten Snapshot des Buffer-Zustands unter Lock zurückgibt. Dies reduziert Lock-Contention bei häufigen Render-Aufrufen. Snapshot enthält: Grid-Kopie (oder Referenz zu unveränderlichem Grid), CursorRow, CursorCol, aktuelle Farb-Attribute.
   - Zielcode: `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs`

3. **Anpassung von `TerminalControl.OnRender()` (optional, abhängig von Schritt 2)**
   - Voraussetzungen: Schritt 2 abgeschlossen (oder übersprungen, falls `GetSnapshot()` nicht implementiert).
   - Beschreibung: Falls `GetSnapshot()` implementiert: Render-Methode nutzt `Buffer.GetSnapshot()` statt direkter Grid-Zugriffe. Dies garantiert konsistente Render-Operations ohne intermediate Lock-Streitigkeiten.
   - Zielcode: `src/Softwareschmiede.App/Controls/TerminalControl.cs`

4. **Tests für Buffer-Synchronisierung erweitern**
   - Voraussetzungen: Schritte 1-3 abgeschlossen.
   - Beschreibung: Neue Thread-Sicherheits-Tests in `TerminalBufferTests`:
     - Parallele `Apply()` + `GetRow()` Zugriffe (Stress-Test).
     - Verifikation, dass `BufferChanged`-Event zuverlässig nach Lock-Release gefeuert wird.
     - Optional: Tests für `GetSnapshot()`-Korrektheit bei parallelen Updates.
   - Zielcode: `src/Softwareschmiede.Tests/Domain/Terminal/TerminalBufferTests.cs`

### Phase 2: Clipboard-Paste-Funktionalität (Feature-Addition)

5. **Implementierung von `KeyToVt100Encoder.EncodeClipboardText()`**
   - Voraussetzungen: Keine (unabhängig von Phase 1).
   - Beschreibung: Neue statische Methode zur Konvertierung von Clipboard-Text zu UTF-8-Bytes mit Newline-Normalisierung (`\n`, `\r\n`, `\r` → `\r`).
   - Zielcode: `src/Softwareschmiede.App/Controls/KeyToVt100Encoder.cs`

6. **Implementierung von Clipboard-Lese-Methoden in `TerminalControl`**
   - Voraussetzungen: Schritt 5 abgeschlossen.
   - Beschreibung: Neue private Methoden:
     - `GetClipboardText()` — Liest `System.Windows.Clipboard.GetText()` mit Fehlerbehandlung.
     - `ReadClipboardAndInsertAsync()` — Liest Clipboard, kodiert via `KeyToVt100Encoder.EncodeClipboardText()`, schreibt in `InputStream`.
   - Zielcode: `src/Softwareschmiede.App/Controls/TerminalControl.cs`

7. **Erweiterung von `TerminalControl.OnPreviewKeyDown()` für `Ctrl+V`-Handling**
   - Voraussetzungen: Schritt 6 abgeschlossen.
   - Beschreibung: Ergänzung des bestehenden Handlers:
     - Prüfung auf `e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0`.
     - Aufruf von `_ = ReadClipboardAndInsertAsync()`.
     - `e.Handled = true` setzen (verhindern, dass Event weiter propagiert).
   - Zielcode: `src/Softwareschmiede.App/Controls/TerminalControl.cs`

8. **Tests für Clipboard-Paste-Funktionalität**
   - Voraussetzungen: Schritte 5-7 abgeschlossen.
   - Beschreibung: Neue Tests in `TerminalControlTests` und/oder neue Testklasse `KeyToVt100EncoderClipboardTests`:
     - `KeyToVt100EncoderTests.EncodeClipboardText_*` — Tests für Single-line, Multi-line (LF/CRLF/CR), Unicode, Leerstring, Null.
     - `TerminalControlTests.OnPreviewKeyDown_CtrlV_*` — Tests für Ctrl+V Handling, Clipboard-Fehler-Szenarien.
   - Zielcode: `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs`, `src/Softwareschmiede.Tests/Infrastructure/Terminal/KeyToVt100EncoderTests.cs`

### Phase 3: Integration und Validierung

9. **Integrations-Tests: Parallele CLI-Ausgabe + Clipboard-Paste**
   - Voraussetzungen: Phasen 1 und 2 abgeschlossen.
   - Beschreibung: E2E-Tests zur Verifikation, dass Clipboard-Paste während laufender CLI-Ausgabe nicht blockiert und nicht zu UI-Freezes führt. Szenarien: 
     - Schnelle CLI-Ausgabe + Ctrl+V (sollte nicht flackern oder Text vermischen).
     - Große Paste-Operationen (sollten asynchron ablaufen, nicht blockieren).
   - Zielcode: Neue E2E-Tests oder Erweiterung bestehender Integrations-Tests.

10. **Überprüfung und Anpassung bestehender Tests**
    - Voraussetzungen: Phasen 1-2 abgeschlossen, Schritt 9 abgeschlossen.
    - Beschreibung: Überprüfung bestehender Tests auf Kompatibilität mit neuer Buffer-Synchronisierung und neuem Keyboard-Handler:
      - `TerminalControlTests` auf geänderte Event-Timing-Annahmen überprüfen.
      - Ggfs. Anpassungen an Test-Timing-Assertions vornehmen.
    - Zielcode: `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs`, andere betroffene Tests.

11. **Vollständiger Build und Test-Durchlauf**
    - Voraussetzungen: Schritte 1-10 abgeschlossen.
    - Beschreibung: Kompletter Build (`dotnet build`) und Unit + Integrations-Tests (`dotnet test`). Verifizierung, dass keine Regressions entstanden sind.
    - Zielcode: Gesamtes Projekt.

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `EncodeClipboardText_SingleLineText_ReturnsUtf8Bytes` | `KeyToVt100EncoderTests` (neu oder erweitert) | Single-line Text wird korrekt zu UTF-8 kodiert. |
| `EncodeClipboardText_MultiLineTextWithLF_ConvertsToCarriageReturn` | `KeyToVt100EncoderTests` | Multi-line Text mit `\n` wird zu `\r` normalisiert. |
| `EncodeClipboardText_MultiLineTextWithCRLF_ConvertsToCarriageReturn` | `KeyToVt100EncoderTests` | Text mit `\r\n` wird zu `\r` normalisiert. |
| `EncodeClipboardText_UnicodeCharacters_ReturnsValidUtf8` | `KeyToVt100EncoderTests` | Unicode-Zeichen (z.B. Emojis, Umlaute) werden korrekt kodiert. |
| `EncodeClipboardText_EmptyString_ReturnsEmptyArray` | `KeyToVt100EncoderTests` | Leerer String gibt leeres Byte-Array zurück. |
| `EncodeClipboardText_SpecialCharactersAndTabs_PreservedInUtf8` | `KeyToVt100EncoderTests` | Tabs und Sonderzeichen bleiben erhalten. |
| `OnPreviewKeyDown_CtrlV_CallsReadClipboardAndInsertAsync` | `TerminalControlTests` (erweitert) | `Ctrl+V` triggert Clipboard-Lesen. |
| `OnPreviewKeyDown_CtrlV_SetsHandledTrue` | `TerminalControlTests` | `Ctrl+V` setzt `e.Handled = true`. |
| `ReadClipboardAndInsertAsync_Success_WritesEncodedBytesToInputStream` | `TerminalControlTests` | Erfolgreicher Clipboard-Read schreibt Bytes in `InputStream`. |
| `ReadClipboardAndInsertAsync_ClipboardEmpty_DoesNothing` | `TerminalControlTests` | Leiche Clipboard führt zu No-Op. |
| `ReadClipboardAndInsertAsync_ClipboardAccessThrows_LogsWarningAndContinues` | `TerminalControlTests` | Clipboard-Zugriff-Fehler wird geloggt, Control läuft weiter. |
| `ReadClipboardAndInsertAsync_CallsMarkInputActivity` | `TerminalControlTests` | Nach erfolgreichem Write wird `MarkInputActivity()` aufgerufen. |
| `Buffer_ParallelApplyAndRead_NoRaceCondition` | `TerminalBufferTests` (erweitert) | Parallele `Apply()` + `GetRow()` Zugriffe führen zu konsistent lesbarem Buffer. |
| `Buffer_BufferChangedEventFiredAfterLockRelease` | `TerminalBufferTests` | `BufferChanged`-Event wird nach Lock-Release gefeuert (Timing-Test). |
| `Buffer_GetSnapshot_ReturnsConsistentState` | `TerminalBufferTests` (optional, falls GetSnapshot implementiert) | `GetSnapshot()` gibt konsistenten Buffer-Zustand zurück. |
| `GetClipboardText_ClipboardContainsText_ReturnsText` | `TerminalControlTests` (privater Test via Reflection) | Clipboard-Text wird korrekt gelesen. |
| `GetClipboardText_ClipboardAccessThrows_ReturnsEmptyString` | `TerminalControlTests` | Fehler bei Clipboard-Zugriff gibt Leerstring zurück. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TerminalControlTests.OnSessionChanged_*` | Keine Anpassung (SessionChanged ist unabhängig). |
| `TerminalControlTests.OnTextInput_*` | Ggfs. Überprüfung auf Timing-Abhängigkeiten mit `BufferChanged`-Event. |
| `TerminalBufferTests.Buffer_*` | Keine Anpassung (Semantik unverändert, nur Lock-Timing verbessert). |
| Sonstige Tests | Vollständiger Test-Lauf erforderlich, um potenzielle Timing-Flakiness zu erkennen. |

Falls Lock-Timing-Änderungen Test-Assertions beeinflussen → Anpassung nötig (z.B. Spin-Wait statt fester Delays).

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Benutzer drückt `Ctrl+V` mit Text in Clipboard | E2E-Test (neu oder erweitert) | Text aus Clipboard wird korrekt in CLI eingefügt. |
| Benutzer drückt `Ctrl+V` mit Multi-line-Text | E2E-Test | Multi-line-Text wird korrekt eingefügt (Zeilenumbrüche als `\r`). |
| Benutzer drückt `Ctrl+V` mit leerem Clipboard | E2E-Test | Keine Aktion, kein Fehler. |
| Benutzer drückt `Ctrl+V` während schneller CLI-Ausgabe | E2E-Test | Paste findet statt, ohne UI zu frieren oder Ausgabe zu vermischen. |
| Benutzer drückt `Ctrl+V` mit sehr großem Clipboard-Text | E2E-Test (optional, abhängig von Rate-Limiting-Entscheidung) | Paste läuft asynchron, UI bleibt responsive. |
| CLI-Ausgabe wird stabil angezeigt bei parallelen Updates | E2E-Test (neu oder erweitert) | Keine Vermischung oder Überschreibung von Text (Stabilitäts-Verifikation). |

### Bestehende E2E-Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Existierende CLI-Output-Tests | Ggfs. Überprüfung auf stabile Ausgabe (kein Flackern/Vermischung nach Buffer-Fix). |
| Existierende Keyboard-Input-Tests | Keine Anpassung (nur neue `Ctrl+V` Event hinzugefügt, bestehende Tasten unverändert). |

---

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Sollte `GetSnapshot()` als neue Methode implementiert werden, oder reicht eine Verbesserung der Lock-Granularität in bestehenden Lesemethoden? | **Empfehlung:** Implementierung von `GetSnapshot()`. Dies ist eine bewährte Praxis (Snapshot Pattern) und bietet explizite Kontrolle über konsistente Lesevorgänge. Alternativ: Zunächst Lock-Granularität verbessern, dann `GetSnapshot()` evaluieren, falls Performance-Probleme auftreten. |
| 2 | Soll es Rate-Limiting für Clipboard-Paste geben, um CLI-Buffer-Überlauf zu vermeiden? | **Empfehlung:** Kein Rate-Limiting in dieser Iteration. CLI-Prozesse sind üblicherweise robust gegenüber schnellen Input-Sequenzen. Falls in Praxis Probleme auftreten, kann Rate-Limiting über Konfiguration (z.B. `TerminalPasteMaxBytesPerSecond`) hinzugefügt werden. |
| 3 | Sollte Clipboard-Paste nur möglich sein, wenn `RuntimeStatus == WartetAufEingabe` ist? | **Empfehlung:** Nein, Paste sollte immer möglich sein. Die CLI kann Input jederzeit verarbeiten, unabhängig vom Status. Eine Einschränkung würde UX verschlechtern und ist nicht nötig. |
| 4 | Sollte ein visueller Indikator anzeigen, dass Clipboard-Text eingefügt wird? | **Empfehlung:** Nein, nicht in dieser Iteration. Das Control ist bereits fokussiert (da `Ctrl+V` verarbeitet wird), und CLI-Echo zeigt die Eingabe. Ein zusätzlicher visueller Indikator ist nicht nötig und könnte ablenken. |
| 5 | Sollte die aktuelle Implementierung WPF `System.Windows.Clipboard` oder `System.Windows.Forms.Clipboard` verwenden? | **Empfehlung:** `System.Windows.Clipboard` (WPF-native API). `System.Windows.Forms.Clipboard` würde WinForms-Abhängigkeit hinzufügen, was nicht nötig ist. WPF ist bereits Abhängigkeit des Controls. |

---

## Zusammenfassung der Implementierungs-Schritte

**Phase 1 (Stabilitäts-Fix):** 4-5 Schritte (Buffer-Synchronisierung, ggfs. Snapshot, Tests)  
**Phase 2 (Feature-Addition):** 3-4 Schritte (Clipboard-Encoder, Lese-Methoden, Keyboard-Handler, Tests)  
**Phase 3 (Integration):** 2-3 Schritte (E2E-Tests, Integrations-Tests, vollständiger Test-Durchlauf)

**Geschätzte Komplexität:** Mittel (Stabilitäts-Fix benötigt sorgfältige Synchronisierungs-Analyse; Clipboard-Paste ist unkompliziert).  
**Geschätzte Testabdeckung:** Hoch (umfangreiche Unit- und E2E-Tests erforderlich).
