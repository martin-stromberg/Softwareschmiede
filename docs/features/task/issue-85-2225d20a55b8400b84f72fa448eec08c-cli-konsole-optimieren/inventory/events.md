# TerminalEvent-Hierarchie: Bestandsaufnahme

Datei: `src/Softwareschmiede/Domain/Terminal/TerminalEvents.cs`

**Zweck:** Event-basierte Kommunikation zwischen `AnsiSequenceParser` (erzeugt Events) und `TerminalBuffer` (wendet Events an).

## Basistyp

### `TerminalEvent`
Abstrakte Record-Klasse, Basistyp für alle Terminal-Ereignisse.

---

## Abgeleitete Event-Typen

| Event-Klasse | Parameter | Beschreibung |
|--------------|-----------|-------------|
| `TextWrittenEvent` | `Text: string` | Klartext, der in den Terminal-Buffer geschrieben werden soll |
| `CursorMovedEvent` | `Row: int`, `Col: int`, `IsAbsolute: bool` | Absolute oder relative Cursor-Positionierung (1-basiert im Parser, wird zu 0-basiert konvertiert) |
| `CursorMovedRelativeEvent` | `DeltaRow: int`, `DeltaCol: int` | Relative Cursor-Bewegung (positiv = nach unten/rechts) |
| `ColorChangedEvent` | `Foreground: Color?`, `Background: Color?`, `Bold: bool?`, `Dim: bool?`, `Underline: bool?`, `Reset: bool` | SGR-Farbänderung (Select Graphic Rendition); `Reset=true` setzt alle Attribute auf Standard zurück |
| `ScreenClearedEvent` | `Mode: int` | Bildschirminhalt löschen (0 = Cursor bis Ende, 1 = Anfang bis Cursor, 2 = Alles) |
| `LineErasedEvent` | `Mode: int` | Zeile löschen (0 = Cursor bis Zeilenende, 1 = Zeilenanfang bis Cursor, 2 = Ganze Zeile) |
| `CursorVisibilityChangedEvent` | `Visible: bool` | Cursor-Sichtbarkeit ändern |

---

## Verwendung

- **Erzeugung:** `AnsiSequenceParser.Parse()` gibt `IEnumerable<TerminalEvent>` zurück
- **Verarbeitung:** `TerminalBuffer.Apply(TerminalEvent evt)` wendet Events auf den internen Zustand an
- **Synchronisierung:** Alle Events werden innerhalb des `lock(_lock)` in `Apply()` verarbeitet

---

## Fehlende Events (Implementierungsbedarf)

- **`ClipboardPasteEvent`** – Optional, falls Clipboard-Text zentral über Event-System verarbeitet werden soll statt direkt in `InputStream` zu schreiben
