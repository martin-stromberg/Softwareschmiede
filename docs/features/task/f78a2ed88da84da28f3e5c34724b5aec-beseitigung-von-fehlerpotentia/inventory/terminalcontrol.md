# TerminalControl

Datei: `src/Softwareschmiede.App/Controls/TerminalControl.cs`

WPF-`FrameworkElement`, rendert eine `PseudoConsoleSession` und leitet Tastatureingaben weiter.

## Felder

| Feld | Typ | Zweck |
|------|-----|-------|
| `_buffer` | `TerminalBuffer?` | Aktueller Bildschirmpuffer |
| `_readCts` | `CancellationTokenSource?` | Steuert Abbruch des Lese-Loops |
| `_parser` | `volatile AnsiSequenceParser` | ANSI-Sequenz-Parser |
| **`_readLoopTask`** | — | **Nicht vorhanden** (F12: Task-Referenz von `Task.Run(...)` wird nirgends gespeichert) |

## Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnSessionChanged(DependencyObject, DependencyPropertyChangedEventArgs)` | private static | DP-Callback, delegiert an Instanzmethode |
| `OnSessionChanged(PseudoConsoleSession?)` | private (Zeile 58–96) | Bricht alten `_readCts` ab, initialisiert Buffer/Parser neu, startet `_ = Task.Run(() => ReadLoopAsync(...))` (Zeile 92) **ohne Speicherung der Task-Referenz oder `ContinueWith`-Exception-Logging (F12–F13)** |
| `ReadLoopAsync(PseudoConsoleSession, TerminalBuffer, CancellationToken)` | private async (Zeile 98–134) | Liest Bytes aus `session.OutputStream`, parst und wendet Events auf Buffer an, ruft `Dispatcher.InvokeAsync(InvalidateVisual)`. Äußeres try-catch fängt nur `OperationCanceledException` (Zeile 131–133) ab; **kein generisches `catch (Exception)` (F14)** — Exceptions aus `_parser.Parse`, `buffer.Apply`, `Dispatcher.InvokeAsync` würden unbehandelt bleiben und den Task abbrechen (innere Reads haben eigenes try-catch, Zeile 106–117, das bei jeder Exception `break`t) |
| `OnRender(DrawingContext)` | protected override | Rendert Buffer-Inhalt |
| `GetBrush(Color)` | private | Brush-Cache |
| `CreateFrozenBrush(Color)` | private static | Erstellt eingefrorenen Brush |
| `OnPreviewKeyDown(KeyEventArgs)` | protected override | Sendet Tasteneingaben; try-catch mit leerem catch (Zeile 229) |
| `OnTextInput(TextCompositionEventArgs)` | protected override | Sendet Texteingaben; try-catch mit leerem catch (Zeile 247) |
| `OnMouseDown(MouseButtonEventArgs)` | protected override | Setzt Fokus |
| `OnRenderSizeChanged(SizeChangedInfo)` | protected override | Passt Buffer-/Session-Größe an |
| `MeasureCellSize()` | private | Berechnet Zellengröße für Font |
| `CalculateCols()` / `CalculateRows()` | private | Berechnet Spalten-/Zeilenanzahl |

## Konstruktor / Unloaded-Handler

- Konstruktor (Zeile 40–50) registriert `Unloaded`-Handler: bricht `_readCts` ab und disposed ihn. Der zugrunde liegende `ReadLoopAsync`-Task wird dabei **nicht** referenziert/awaited (kein Cleanup-Warten, F12).

## Kritische Stellen (Bezug zur Anforderung)

- **F12/F13:** Zeile 92 — `_ = Task.Run(() => ReadLoopAsync(session, _buffer, cts.Token));` ohne gespeicherte Task-Referenz und ohne `ContinueWith` zum Loggen abgeschlossener/fehlgeschlagener Läufe
- **F14:** Zeile 98–134 — `ReadLoopAsync` hat nur `catch (OperationCanceledException)` im äußeren try-catch, kein generisches `catch (Exception ex)` für z. B. `_parser.Parse`, `buffer.Apply`, `InvalidateVisual`/`Dispatcher.InvokeAsync`
