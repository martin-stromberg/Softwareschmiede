# Datenmodelle

## `TerminalBuffer`
Datei: `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs`

Zustandsbehafteter Terminal-Buffer: verwaltet ein 2D-Grid aus TerminalCell-Werten, Cursor-Position, SGR-Attribut-Zustand und einen Scrollback-Ringpuffer.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Rows` | int | Aktuelle Zeilenanzahl des Buffers (Thread-safe) |
| `Cols` | int | Aktuelle Spaltenanzahl des Buffers (Thread-safe) |
| `CursorRow` | int | Aktuelle Cursor-Zeile (0-basiert, Thread-safe) |
| `CursorCol` | int | Aktuelle Cursor-Spalte (0-basiert, Thread-safe) |

**Wichtige Methoden:**
- `Apply(TerminalEvent evt)` — Wendet ein Terminal-Ereignis auf den Buffer an (TextWritten, CursorMoved, ColorChanged, ScreenCleared, LineErased, etc.). Thread-sicher mit interner Lock.
- `Resize(int cols, int rows)` — Ändert die Größe des Buffers
- `GetRow(int r)` — Gibt das Zellen-Array einer Zeile zurück

**Verwendung in TerminalControl:** Der Buffer wird von TerminalControl verwaltet (Feld `_buffer`). Bei Session-Wechsel wird der neue Buffer aus `session.Buffer` geladen oder neu erstellt. Der Buffer wird bei `OnRender()` gelesen, um Terminal-Ausgabe zu zeichnen. Bei `OnRenderSizeChanged` wird der Buffer resized.

---

## `CliRuntimeStatusChangedEventArgs`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

Ereignisargumente für Änderungen des CLI-Laufzeitstatus.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Status` | CliRuntimeStatus | Der neue Laufzeitstatus (Laeuft, WartetAufEingabe, Inaktiv) |

**Verwendung:** Wird von `PseudoConsoleSession.RuntimeStatusChanged`-Event gefeuert. Wird von `TaskDetailViewModel.AttachCliStatusSession()` per Event-Handler abonniert und aktualisiert die CLI-Statuszeile in der UI.

---

## `CliProcessHandle`
Datei: (vermutet in `src/Softwareschmiede/Application/Services/` oder verwandtem Namespace)

Handles für laufende CLI-Prozesse. Wird von `KiAusfuehrungsService._handles` (ConcurrentDictionary<Guid, CliProcessHandle>) verwaltet.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Process` | System.Diagnostics.Process | Der verwaltete CLI-Prozess |
| `PseudoConsoleSession` | PseudoConsoleSession | Die zugehörige Terminal-Sitzung |
| (weitere) | ... | (gemäß Implementierung) |

**Verwendung:** Wird von `KiAusfuehrungsService.StartCliAsync()` erstellt und in `_handles` eingelagert. Der `Exited`-Event des Prozesses ist an `HandleProcessExited()` gebunden.
