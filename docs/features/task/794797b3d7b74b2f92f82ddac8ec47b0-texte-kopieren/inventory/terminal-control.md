# TerminalControl: Darstellung und Eingabe

## Control und Einbettung

`src/Softwareschmiede.App/Controls/TerminalControl.cs` implementiert `TerminalControl : FrameworkElement, IScrollInfo`. Das Control ist fokussierbar, rendert die aktive `ITerminalSession` und übernimmt deren `BufferChanged`-Ereignis. Es zeichnet jede sichtbare Terminalzelle über `DrawingContext` und `FormattedText`; es ist damit kein nativer Texteditor und besitzt keine WPF-Textauswahl.

Die Aufgabenansicht bindet das Control in `src/Softwareschmiede.App/Views/TaskDetailView.xaml` innerhalb des `TerminalScrollViewer` als `TerminalConsole` ein. `TaskDetailView.xaml.cs` setzt bei Sessionwechseln `TerminalConsole.Session` und fokussiert das Control bei Interaktion mit dem Terminalbereich. Ein Preview-Mousedown-Handler auf dem äußeren `ScrollViewer` fokussiert den Terminalbereich; der bestehende Handler unterscheidet Scrollbar-Klicks vom restlichen Bereich.

Auch `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml` enthält ein `TerminalControl` für Replay. Eine Änderung der Auswahl direkt im Control beträfe deshalb wahrscheinlich Live-CLI und Replay gemeinsam.

## Maus und Tastatur

`TerminalControl.OnMouseDown` setzt den Tastaturfokus auf das Control. Es gibt derzeit keine Implementierungen für Mausbewegung, Drag-Auswahl, Auswahlzustand oder Markierungsrendering. `OnRender` zeichnet ausschließlich Buffer-Hintergründe, Zellzeichen und den Terminalcursor.

`OnPreviewKeyDown` leitet kodierte Tasten über `ITerminalSession.WriteInputAsync` weiter. `Ctrl+V` ist ein Sonderfall: Wenn ein Input-Stream existiert, wird der Zwischenablageinhalt gelesen und in die Session geschrieben. Ein Kopierpfad oder Umgang mit einer vorhandenen Textauswahl fehlt. Da übrige Tastenkombinationen grundsätzlich an den Terminalprozess gehen können, ist die Bedeutung von `Ctrl+C` im Live-Betrieb konfliktträchtig.

`TerminalBuffer.GetSnapshot()` stellt den Zustand zum Rendern bereit. Auswahl muss Zellkoordinaten in diesem Buffer-Kontext berücksichtigen; die Darstellung hat Scrolling und Cursorposition, weshalb sichtbare Mausposition und logische Zeile/Spalte um Scroll-Offsets sowie Scrollback abweichen können. Der Buffer kann außerdem Terminal-Steuersequenzen und Bewegungen des Cursors abbilden; er ist kein einfacher append-only Textstream.
