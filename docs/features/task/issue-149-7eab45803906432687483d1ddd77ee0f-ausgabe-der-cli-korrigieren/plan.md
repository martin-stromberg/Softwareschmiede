# Umsetzungsplan: Korrektur der CLI-Ausgabeanzeige

## Übersicht

Behebung der Darstellungsfehler in der Terminal-Rendering-Pipeline (ausgelassene/verschobene Zeilen,
doppelt wirkende Ausgabe, stehenbleibende Altzeilen, ungewollte Zeilenumbrüche). Betroffen sind die
Domänenklasse `TerminalBuffer` (Zeilenvorschub-Semantik, Resize- und Clear-Verhalten), die WPF-`TerminalControl`
(Neuzeichnung nach Resize) sowie die zugehörigen Unit- und E2E-Tests. Der ANSI-Parser bleibt unverändert;
die Zeilen- und Cursor-Semantik verbleibt bewusst im `TerminalBuffer` (Domain Model).

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Zeilenvorschub-Normalisierung | Behandlung im `TerminalBuffer.ApplyText` (Domain Model) statt neuer Parser-Events (`LineBreakEvent`) | Cursor- und Zeilensemantik gehört in den Buffer; der `AnsiSequenceParser` bleibt reiner Byte-zu-Event-Transport. Vermeidet neue Event-Typen und hält die Änderung lokal und testbar. Es besteht keine Abhängigkeit von einer ConPTY-seitigen Normalisierung — CR, LF und CRLF werden im Buffer defensiv behandelt. |
| Semantik von `\n` (LF) | LF wird wie CRLF behandelt: nächste Zeile **und** Spalte 0 (Newline-Mode-Semantik). `\r` allein bleibt: Spalte 0 ohne Zeilenvorschub. | Die Anzeige spiegelt Windows-Konsolenausgabe (CRLF) wider; Programme, die nur `\n` senden, dürfen keinen Treppeneffekt erzeugen. Entspricht der in der Anforderung (Abschnitt 3) festgelegten Zielsemantik. |
| Resize beim Verkleinern der **Spalten**zahl | Abschneiden am rechten Rand, **kein** Reflow (Umbruch) des Zeileninhalts. Aktuelles Kopierverhalten (`copyCols = Min(_cols, cols)`) bleibt erhalten. | Vom Anwender bestätigt. Reflow ist deutlich komplexer und wird von der Anforderung nicht verlangt; der Fokus liegt auf Zeilen- und Zeilenvorschub-Integrität. |
| Resize beim Verkleinern der **Zeilen**zahl | Untere (aktuelle) Zeilen erhalten statt oberer Zeilen; Cursor-Zeile wird um den Versatz mitgezogen; herausgeschobene obere Zeilen wandern in den Scrollback | Ein Terminal zeigt beim Verkleinern den aktuellen Prompt/Cursor am unteren Rand, nicht veralteten oberen Inhalt. Behebt „alte Zeilen bleiben sichtbar" nach Verkleinerung. |
| Vollständige Grid-Bereinigung bei Clear Screen | Extraktion einer privaten `ClearAllCells()`-Methode, genutzt von `ApplyClearScreen(mode 2)`; sie füllt das gesamte Grid mit `TerminalCell.Default` **und leert zusätzlich den `_scrollback`-Ringpuffer** | Vom Anwender bestätigt: Clear Screen (`ESC[2J`) soll auch den Scrollback leeren (einfacheres Modell). Der Scrollback wird derzeit nicht in der UI angezeigt (kein Scrollbalken/Mausrad-Handler), daher entstehen keine sichtbaren Nebenwirkungen. Kapselt die Clear-Semantik explizit und wiederverwendbar. |
| Render nach Resize | Explizites `InvalidateVisual()` am Ende von `OnRenderSizeChanged` | Garantiert Neuzeichnung des gesamten Controls nach Größenänderung, statt sich allein auf WPFs implizites Re-Render zu verlassen. |

## Programmabläufe

### Zeilenvorschub bei Textausgabe

1. `PseudoConsoleSession.ReadLoopAsync` liest einen Ausgabe-Chunk und ruft `AnsiSequenceParser.Parse` auf.
2. Der Parser liefert u. a. ein `TextWrittenEvent` mit Rohtext (kann `\r`, `\n`, `\r\n`, `\x08` enthalten).
3. `TerminalBuffer.Apply` ruft `ApplyText` auf.
4. `ApplyText` verarbeitet zeichenweise:
   - `\r` → `_cursorCol = 0` (kein Zeilenvorschub).
   - `\n` → `AdvanceLine()` **und** `_cursorCol = 0` (nächste Zeile, Spalte 0).
   - `\x08` → Spalte um 1 zurück (min. 0).
   - druckbares Zeichen → ggf. Zeilenumbruch bei Spaltenüberlauf, dann Zelle schreiben, Spalte++.
5. Ein reines `\r\n` erzeugt genau einen Zeilenvorschub in Spalte 0 (kein doppelter Umbruch, keine Verschiebung).

Beteiligte Klassen/Komponenten: `TerminalBuffer`, `AnsiSequenceParser`, `PseudoConsoleSession`, `TextWrittenEvent`

### Buffer-Verkleinerung (Resize) mit Beibehaltung der aktuellen Zeilen

1. `TerminalControl.OnRenderSizeChanged` berechnet neue `cols`/`rows` und ruft `buffer.Resize(cols, rows)` sowie `session.Resize(cols, rows)` auf.
2. `TerminalBuffer.Resize` erstellt ein neues, vollständig mit `TerminalCell.Default` gefülltes Grid.
3. Bei Verkleinerung der Zeilen (`rows < _rows`): Es werden die **untersten** `rows` Zeilen des alten Grids in das neue Grid kopiert; der Zeilenversatz `offset = _rows - rows` bestimmt die Quellzeilen (`offset .. _rows-1`). Pro Zeile werden die Spalten am rechten Rand abgeschnitten (`copyCols = Min(_cols, cols)`), **kein Reflow**.
4. Die herausgeschobenen oberen Zeilen (`0 .. offset-1`) werden in den `_scrollback`-Ringpuffer übernommen (unter Beachtung von `MaxScrollbackLines`).
5. Bei Vergrößerung oder gleicher Zeilenzahl (`rows >= _rows`): Kopie ab Zeile 0 wie bisher (top-aligned), Rest bleibt `Default`.
6. Die Cursor-Zeile wird um denselben `offset` reduziert und anschließend auf `[0, rows-1]` geklemmt; die Cursor-Spalte wird auf `[0, cols-1]` geklemmt.
7. `TerminalControl` ruft `InvalidateVisual()` auf, wodurch `OnRender` das gesamte Control neu zeichnet.

Beteiligte Klassen/Komponenten: `TerminalControl`, `TerminalBuffer`, `PseudoConsoleSession`

### Bildschirm löschen (Clear Screen Mode 2)

1. Parser erzeugt `ScreenClearedEvent(Mode: 2)`.
2. `TerminalBuffer.Apply` → `ApplyClearScreen(2)`.
3. `ApplyClearScreen(2)` ruft `ClearAllCells()` auf: das gesamte Grid wird mit `TerminalCell.Default` gefüllt **und der `_scrollback`-Ringpuffer geleert**; anschließend wird der Cursor auf (0,0) gesetzt.
4. `PseudoConsoleSession.BufferChanged` löst `TerminalControl.OnRender` aus, das den schwarzen Hintergrund über die gesamte Fläche zeichnet — keine Restinhalte sichtbar.

Beteiligte Klassen/Komponenten: `TerminalBuffer`, `ScreenClearedEvent`, `TerminalControl`

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `TerminalBuffer` (Domänenklasse)

- **Neue Methoden:**
  - `ClearAllCells` (`private void`) — füllt `_grid` vollständig mit `TerminalCell.Default` (kapselt die bestehende `FillGrid(_grid, _rows, _cols)`-Logik) und leert zusätzlich `_scrollback`.
- **Geänderte Methoden:**
  - `ApplyText` — Fall `'\n'`: zusätzlich zu `AdvanceLine()` wird `_cursorCol = 0` gesetzt (LF wirkt wie CRLF). Fall `'\r'` bleibt unverändert (nur Spalte 0).
  - `Resize` — beim Verkleinern der Zeilenzahl werden die untersten `rows` Zeilen kopiert (Versatz `offset = _rows - rows`), die herausgeschobenen oberen Zeilen wandern in `_scrollback`, und `_cursorRow` wird um `offset` reduziert (dann geklemmt). Spaltenverkleinerung schneidet weiterhin rechts ab (kein Reflow). Vergrößerung/Gleichstand bleiben top-aligned wie bisher.
  - `ApplyClearScreen` — Fall Mode 2 (`default`) ruft `ClearAllCells()` statt `FillGrid(_grid, _rows, _cols)` auf und leert dadurch auch den Scrollback.
- **Neue Events / Event-Handler:** Keine.

### `TerminalControl` (WPF-Control)

- **Geänderte Methoden:**
  - `OnRenderSizeChanged` — nach `buffer.Resize`/`session.Resize` wird `InvalidateVisual()` aufgerufen, um die vollständige Neuzeichnung nach Größenänderung sicherzustellen.
- **Neue Eigenschaften / Methoden / Events:** Keine.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **`ApplyText` / bare `\n`:** Bestehender Unit-Test `Buffer_Newline_ScrolltBeiLetzterZeile` schreibt Text gefolgt von Newline. Da `\n` nun zusätzlich die Spalte auf 0 setzt, muss die Cursor-Spalten-Erwartung des Tests geprüft und ggf. angepasst werden. Die Scroll-Erwartung selbst ändert sich nicht.
- **`Resize`:** Programme/Tests, die nach einer Verkleinerung Inhalt in den oberen Zeilen erwarten, sehen jetzt die unteren Zeilen. Bestehender Test `Buffer_Resize_ErhaeltSichtbarenInhalt` (Vergrößerung/Gleichstand, top-aligned) bleibt unverändert; `Buffer_Resize_KleinerAlsInhalt_WirftNicht` prüft nur Ausnahmefreiheit und bleibt gültig.
- **Scrollback bei Clear Screen:** `ClearAllCells()` leert nun den Scrollback. Da der Scrollback nicht gerendert wird und es keinen Scroll-Zugriff in der UI gibt, entsteht keine sichtbare Nebenwirkung; er stellt lediglich das erwartete „leerer Bildschirm nach Clear"-Modell sicher.
- **Scrollback beim Verkleinern:** Das Einreihen herausgeschobener Zeilen erhöht die Scrollback-Nutzung; durch `MaxScrollbackLines`-Deckelung besteht kein unbegrenztes Wachstum.
- **`InvalidateVisual()` nach Resize:** Zusätzliche Neuzeichnung pro Größenänderung; vernachlässigbar, da Größenänderungen selten sind.
- **Thread-Sicherheit:** Alle Buffer-Änderungen laufen weiterhin unter `_lock`; `Resize`, `Apply` und der Zugriff auf `_scrollback` bleiben serialisiert. Kein neues Race-Potenzial.

## Umsetzungsreihenfolge

1. **`ClearAllCells()` in `TerminalBuffer` extrahieren**
   - Voraussetzungen: Keine (baut auf vorhandener `FillGrid`-Logik und `_scrollback` auf).
   - Beschreibung: Private Methode anlegen, die `_grid` vollständig mit `TerminalCell.Default` füllt und `_scrollback.Clear()` aufruft; `ApplyClearScreen(2)` darauf umstellen (Cursor-Reset auf (0,0) verbleibt in `ApplyClearScreen`).

2. **Zeilenvorschub-Semantik in `ApplyText` korrigieren**
   - Voraussetzungen: Keine.
   - Beschreibung: Im Fall `'\n'` zusätzlich `_cursorCol = 0` setzen; `'\r'` unverändert lassen.

3. **`Resize` beim Verkleinern auf untere Zeilen umstellen**
   - Voraussetzungen: Keine.
   - Beschreibung: Bei `rows < _rows` untere Zeilen kopieren (Spalten rechts abschneiden, kein Reflow), Zeilenversatz auf Cursor anwenden, herausgeschobene obere Zeilen in `_scrollback` einreihen. Vergrößerung/Gleichstand unverändert.

4. **`InvalidateVisual()` in `TerminalControl.OnRenderSizeChanged` ergänzen**
   - Voraussetzungen: Keine.
   - Beschreibung: Nach den Resize-Aufrufen die Neuzeichnung des Controls auslösen.

5. **Unit-Tests für Buffer-Verhalten ergänzen/anpassen**
   - Voraussetzungen: Schritte 1–3 umgesetzt.
   - Beschreibung: Neue Tests für LF/CRLF-Normalisierung, Verkleinerungs-Resize (untere Zeilen erhalten, Cursor korrekt, Spalten-Abschnitt), vollständige Clear-Bereinigung inkl. Scrollback; betroffenen `Buffer_Newline_*`-Test anpassen.

6. **E2E-Test für Anzeige-Integrität ergänzen**
   - Voraussetzungen: Schritte 1–4 umgesetzt.
   - Beschreibung: E2E-Szenario, das mehrzeilige CLI-Ausgabe erzeugt und die korrekte Darstellung im `TerminalControl` (keine zusätzlichen/fehlenden Zeilenumbrüche) prüft.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Buffer_LineFeed_SetztSpalteAufNull` | `TerminalBufferTests` | Nach `TextWrittenEvent("A\nB")` steht `B` in Zeile 1, Spalte 0 (kein Treppeneffekt). |
| `Buffer_CarriageReturnLineFeed_ErgibtEinenUmbruch` | `TerminalBufferTests` | `"A\r\nB"` erzeugt genau einen Zeilenvorschub, `B` in Zeile 1 Spalte 0. |
| `Buffer_CarriageReturnAllein_BleibtInZeile` | `TerminalBufferTests` | `"AAA\rB"` überschreibt Spalte 0 der aktuellen Zeile ohne Zeilenvorschub. |
| `Buffer_ResizeKleiner_ErhaeltUntereZeilen` | `TerminalBufferTests` | Nach Verkleinerung der Zeilenzahl sind die zuletzt geschriebenen (unteren) Zeilen sichtbar, obere entfernt. |
| `Buffer_ResizeKleiner_CursorFolgtUnterenZeilen` | `TerminalBufferTests` | Cursor-Zeile wird nach Verkleinerung korrekt um den Versatz reduziert und geklemmt. |
| `Buffer_ResizeSchmaler_SchneidetRechtsAb` | `TerminalBufferTests` | Bei Verkleinerung der Spaltenzahl wird der Zeileninhalt rechts abgeschnitten (kein Reflow in die nächste Zeile). |
| `Buffer_ClearScreenMode2_AlleZellenLeer` | `TerminalBufferTests` | Nach `ScreenClearedEvent(2)` sind alle Zellen `TerminalCell.Default` und Cursor bei (0,0). |
| `Buffer_ClearScreenMode2_LeertScrollback` | `TerminalBufferTests` | Nach genügend Zeilenvorschüben (Scrollback gefüllt) und anschließendem `ScreenClearedEvent(2)` ist der Scrollback leer. |
| `Parse_CrLfText_ErgibtTextMitCrLf` | `AnsiSequenceParserTests` | Verifiziert, dass der Parser `\r\n` unverändert im `TextWrittenEvent`-Text belässt (Semantik liegt im Buffer). |

Hinweis: Für die Scrollback-Prüfung (`Buffer_ClearScreenMode2_LeertScrollback`) ist ein testseitiger Lesezugriff
auf den Scrollback-Zustand erforderlich. Da `_scrollback` privat ist und keine öffentliche Zähl-Eigenschaft
existiert, wird dafür eine minimale test-sichtbare Zugriffsmöglichkeit ergänzt (z. B. eine `internal`-Eigenschaft
`ScrollbackCount` mit `InternalsVisibleTo` für das Testprojekt, sofern noch nicht vorhanden). Falls das Testprojekt
bereits `InternalsVisibleTo`-Zugriff besitzt, genügt die `internal`-Eigenschaft.

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `Buffer_Newline_ScrolltBeiLetzterZeile` (`TerminalBufferTests`) | `\n` setzt jetzt zusätzlich die Cursor-Spalte auf 0; Spalten-Erwartung ggf. anpassen (Scroll-Erwartung bleibt). |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Mehrzeilige CLI-Ausgabe wird ohne Treppeneffekt/ausgelassene Zeilen korrekt untereinander dargestellt | E2E-Terminal-Testklasse (`src/Softwareschmiede.Tests/E2E/`, Category `OsInterface`) | Zeilenumbrüche der Windows-Konsole werden 1:1 dargestellt (keine zusätzlichen/fehlenden Umbrüche). |

Hinweis: E2E-Tests dieser Kategorie laufen im Agent-Sandbox nur mit gesetztem `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS`
sauber (siehe CLAUDE.md); die vollständige Verifikation erfolgt in einer interaktiven Sitzung.

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine.

## Offene Punkte

Keine.
