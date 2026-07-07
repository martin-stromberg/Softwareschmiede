# Datenmodell: Bestandsaufnahme

## `TerminalBuffer`
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs`

**Zweck:** Zustandsbehafteter Terminal-Buffer; verwaltet ein 2D-Grid aus `TerminalCell`-Werten, Cursor-Position, SGR-Attribut-Zustand und einen Scrollback-Ringpuffer.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Rows` | `int` (Property) | Aktuelle Zeilenanzahl des Buffers (lesend mit Lock synchronisiert) |
| `Cols` | `int` (Property) | Aktuelle Spaltenanzahl des Buffers (lesend mit Lock synchronisiert) |
| `CursorRow` | `int` (Property) | Aktuelle Cursor-Zeile, 0-basiert (lesend mit Lock synchronisiert) |
| `CursorCol` | `int` (Property) | Aktuelle Cursor-Spalte, 0-basiert (lesend mit Lock synchronisiert) |
| `_grid` | `TerminalCell[,]` | 2D-Grid, das Terminal-Zellen speichert (privat, Lock-geschützt) |
| `_cols` | `int` | Interne Spaltenanzahl (privat) |
| `_rows` | `int` | Interne Zeilenanzahl (privat) |
| `_cursorRow` | `int` | Interne Cursor-Zeile (privat) |
| `_cursorCol` | `int` | Interne Cursor-Spalte (privat) |
| `_scrollback` | `Queue<TerminalCell[]>` | Ringpuffer für Scrollback-Inhalt, max. 1000 Zeilen (privat) |
| `_lock` | `object` | Lock-Objekt für Synchronisierung von `Apply()` und `Resize()` (privat) |
| `_currentForeground` | `Color` | Aktuelle Vordergrundfarbe für neue Zeichen (privat) |
| `_currentBackground` | `Color` | Aktuelle Hintergrundfarbe für neue Zeichen (privat) |
| `_currentBold` | `bool` | Aktueller Bold-Status (privat) |
| `_currentDim` | `bool` | Aktueller Dim-Status (privat) |
| `_currentUnderline` | `bool` | Aktueller Underline-Status (privat) |

**Synchronisierung:** Interne Lock-basierte Synchronisierung auf `_lock`. Alle schreibenden Operationen (`Apply()`, `Resize()`) und lesenden Zugriffe auf Properties erfolgen unter Lock. Methode `GetRow()` gibt eine Kopie zurück (Copy-on-Read).

**Bestandteil der Konstruktion:**
- `TerminalBuffer(int cols, int rows)` – Erstellt Buffer mit Größe, initialisiert Grid mit `TerminalCell.Default`

---

## `TerminalCell`
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalCell.cs`

**Zweck:** Record Struct, das eine einzelne Zelle im Terminal-Grid darstellt.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Character` | `char` | Das angezeigte Zeichen |
| `Foreground` | `Color` (System.Drawing) | Vordergrundfarbe der Zelle |
| `Background` | `Color` (System.Drawing) | Hintergrundfarbe der Zelle |
| `Bold` | `bool` | Gibt an, ob der Text fett dargestellt wird |
| `Underline` | `bool` | Gibt an, ob der Text unterstrichen wird |
| `Dim` | `bool` | Gibt an, ob der Text gedimmt dargestellt wird |
| `Default` | `TerminalCell` (static) | Standardzelle mit Leerzeichen, heller grauer Vordergrundfarbe (229,229,229) und schwarzem Hintergrund |

**Besonderheiten:** `TerminalCell` ist ein `record struct`, daher Copy-Semantik (keine Referenzen). Immutable über `init`-Accessoren.
