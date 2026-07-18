# Datenmodelle

## `TerminalBuffer`
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs`

Zustandsbehafteter Terminal-Buffer mit Thread-Safety.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `_grid` | `TerminalCell[,]` | 2D-Array der Terminal-Zellen (Zeile × Spalte) |
| `_cols` | `int` | Aktuelle Spaltenanzahl |
| `_rows` | `int` | Aktuelle Zeilenanzahl |
| `_cursorRow` | `int` | Aktuelle Cursor-Zeile (0-basiert) |
| `_cursorCol` | `int` | Aktuelle Cursor-Spalte (0-basiert) |
| `_scrollback` | `Queue<TerminalCell[]>` | Ringpuffer für Scroll-History (maximal 1000 Zeilen) |
| `_currentForeground` | `Color` | Aktuelle Vordergrundfarbe (Standard: helles Grau #E5E5E5) |
| `_currentBackground` | `Color` | Aktuelle Hintergrundfarbe (Standard: Schwarz) |
| `_currentBold` | `bool` | Aktueller Bold-Status für nachfolgende Zeichen |
| `_currentDim` | `bool` | Aktueller Dim-Status für nachfolgende Zeichen |
| `_currentUnderline` | `bool` | Aktueller Underline-Status für nachfolgende Zeichen |
| `Rows` (Property) | `int` | Getter für _rows |
| `Cols` (Property) | `int` | Getter für _cols |
| `CursorRow` (Property) | `int` | Getter für _cursorRow |
| `CursorCol` (Property) | `int` | Getter für _cursorCol |
| `Buffer` (Property in PseudoConsoleSession) | `TerminalBuffer` | Der Buffer dieser Sitzung (Default: 220 Spalten × 50 Zeilen) |

**Beobachtungen:**
- Thread-sicher durch `_lock`-Objekt
- SGR-Attribute sind Zustandsvariablen, gelten für alle nachfolgenden Zeichen bis zur nächsten Änderung
- `GetSnapshot()` erstellt eine konsistente Kopie für sichere Render-Operationen

---

## `TerminalCell`
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalCell.cs`

Ein Record Struct, das eine einzelne Zelle im Terminal-Grid repräsentiert.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Character` | `char` | Das angezeigte Zeichen |
| `Foreground` | `Color` | Vordergrundfarbe der Zelle |
| `Background` | `Color` | Hintergrundfarbe der Zelle |
| `Bold` | `bool` | Fett-Darstellung |
| `Underline` | `bool` | Unterstrichen-Darstellung |
| `Dim` | `bool` | Gedimmte Darstellung |
| `Default` (Static) | `TerminalCell` | Standardzelle: Leerzeichen mit Standardfarben (grau #E5E5E5, schwarzer Hintergrund) |

**Beobachtungen:**
- Immutable (Record Struct mit `init`-Properties)
- `Default` dient als Initialisierungswert für alle leeren Zellen

---

## `TerminalEvent` (Basistyp und Untertypen)
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalEvents.cs`

Abstrakte Record-Basisklasse für alle Terminal-Ereignisse.

| Typ | Eigenschaften | Zweck |
|-----|--------------|-------|
| `TerminalEvent` | — | Abstrakte Basisklasse |
| `TextWrittenEvent` | `Text: string` | Klartext ohne Escape-Sequenzen (enthält möglicherweise `\r`, `\n`, `\x08`) |
| `CursorMovedEvent` | `Row: int`, `Col: int`, `IsAbsolute: bool` | Cursor-Positionierung (absolute oder relative Deltas) |
| `CursorMovedRelativeEvent` | `DeltaRow: int`, `DeltaCol: int` | Relative Cursor-Verschiebung |
| `ColorChangedEvent` | `Foreground: Color?`, `Background: Color?`, `Bold: bool?`, `Dim: bool?`, `Underline: bool?`, `Reset: bool` | SGR-Attributänderung (Select Graphic Rendition) |
| `ScreenClearedEvent` | `Mode: int` (0, 1, oder 2) | Bildschirmlösch-Befehl (0=Cursor bis Ende, 1=Anfang bis Cursor, 2=Ganzer Screen) |
| `LineErasedEvent` | `Mode: int` (0, 1, oder 2) | Zeilenlösch-Befehl (0=Cursor bis Zeilenende, 1=Zeilenanfang bis Cursor, 2=Ganze Zeile) |
| `CursorVisibilityChangedEvent` | `Visible: bool` | Cursor-Sichtbarkeit setzen |

**Beobachtungen:**
- Alle Events sind immutable Records
- `TextWrittenEvent.Text` wird direkt vom `AnsiSequenceParser` als UTF8-decodiert bereitgestellt
- Die Zeilenumbruch-Verarbeitung (`\r`, `\n`) findet erst in `TerminalBuffer.ApplyText()` statt, nicht im Parser

---

## `TerminalBufferSnapshot`
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs`

Record, das einen konsistenten Snapshot des Buffer-Zustands unter einem einzigen Lock erfasst.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Grid` | `TerminalCell[,]` | Kopie des Zellen-Grids zum Snapshot-Zeitpunkt |
| `Rows` | `int` | Zeilenanzahl des Buffers zum Snapshot-Zeitpunkt |
| `Cols` | `int` | Spaltenanzahl des Buffers zum Snapshot-Zeitpunkt |
| `CursorRow` | `int` | Cursor-Zeile zum Snapshot-Zeitpunkt |
| `CursorCol` | `int` | Cursor-Spalte zum Snapshot-Zeitpunkt |

**Beobachtungen:**
- Wird von `TerminalBuffer.GetSnapshot()` erzeugt und von `TerminalControl.OnRender()` zum Rendern verwendet
- Ermöglicht race-condition-freie Render-Operationen
