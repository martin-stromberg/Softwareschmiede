# Umsetzungsplan - Konsole scrollbar machen

## Zielbild

Die CLI-Ansicht bleibt ein terminalartiger WPF-Renderer auf Basis von `TerminalControl`, erhaelt aber vertikale Scrollbarkeit ueber den bereits vorhandenen Scrollback des `TerminalBuffer`.

Gewuenschtes Verhalten:

- Lange CLI-Ausgaben bleiben bis zur vorhandenen Scrollback-Grenze erreichbar.
- Die Anzeige folgt neuen Ausgaben automatisch, solange der Anwender am Ende des Verlaufs steht.
- Scrollt der Anwender manuell nach oben, bleibt die Position auch bei neuer Ausgabe erhalten.
- Klicks und Tastatureingaben im Terminalbereich fokussieren weiterhin `TerminalControl` und werden an die aktive `PseudoConsoleSession` weitergeleitet.
- Horizontales Verhalten bleibt unveraendert: Die Breite bestimmt die Terminalspalten, lange Zeilen werden nicht als separate horizontale UI-Scrollflaeche geplant.

## Technische Leitentscheidung

Kein einfacher XAML-`ScrollViewer` um das bestehende `TerminalControl` als alleinige Loesung.

Begruendung: `TerminalControl` rendert aktuell nur `TerminalBuffer.GetSnapshot()` mit sichtbarem Grid und meldet keine logische Verlaufshoehe. Die Scrollback-Zeilen existieren zwar im `TerminalBuffer`, sind aber nicht Teil des Snapshots. Die robuste Umsetzung erweitert daher zuerst die Buffer-/Snapshot-Schnittstelle und macht `TerminalControl` selbst scrollbar.

## Umsetzungsschritte

### 1. TerminalBuffer-Snapshot um Scrollback erweitern

Datei: `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs`

- `TerminalBufferSnapshot` um Verlaufsdaten erweitern, z. B.:
  - `TerminalCell[,] ViewportGrid` oder bestehendes `Grid` fuer sichtbare Zeilen beibehalten.
  - `TerminalCell[,] HistoryGrid` oder `TerminalCell[][] ScrollbackRows` fuer Scrollback-Zeilen.
  - `int ScrollbackRows`, `int TotalRows`.
- `GetSnapshot()` muss unter demselben Lock eine konsistente Kopie aus `_scrollback` plus aktuellem `_grid` liefern.
- Reihenfolge: aelteste Scrollback-Zeile zuerst, danach sichtbares Grid.
- Bestehende Semantik von `Rows`, `Cols`, `CursorRow`, `CursorCol` beibehalten, damit vorhandene Tests und Rendererlogik nicht unnoetig brechen.
- Die bestehende Grenze `MaxScrollbackLines = 1000` bleibt bestehen. Damit ist die erreichbare Historie technisch definiert als maximal 1000 Scrollback-Zeilen plus sichtbares Terminal-Grid.
- `ClearAllCells()` leert weiterhin Scrollback und sichtbares Grid; der Snapshot muss danach keinen alten Verlauf mehr enthalten.

### 2. TerminalControl mit ScrollInfo ausstatten

Datei: `src/Softwareschmiede.App/Controls/TerminalControl.cs`

- `TerminalControl` implementiert `IScrollInfo`.
- Interne Scrollzustandsfelder einfuehren:
  - `ScrollOwner`
  - `ExtentHeight`
  - `ViewportHeight`
  - `VerticalOffset`
  - optional `ExtentWidth`, `ViewportWidth`, `HorizontalOffset` als stabile No-op-/Breitenwerte.
- Scroll-Einheiten zeilenbasiert behandeln:
  - `LineUp()` und `LineDown()` verschieben um eine Terminalzeile.
  - `PageUp()` und `PageDown()` verschieben um sichtbare Zeilen minus eine kleine Ueberlappung.
  - `MouseWheelUp()` und `MouseWheelDown()` verschieben um mehrere Zeilen, z. B. 3.
  - `SetVerticalOffset(double offset)` klemmt auf `0..ScrollableHeight`.
- `CanVerticallyScroll` unterstuetzen; horizontales Scrollen bewusst nicht aktivieren.
- `MeasureOverride`/`ArrangeOverride` so belassen oder minimal ergaenzen, dass das Control im `ScrollViewer` weiterhin die verfuegbare Hoehe als sichtbaren Viewport nutzt.
- Bei jeder Groessenaenderung und jedem BufferChanged:
  - Snapshot holen oder Extent aus Snapshot berechnen.
  - `ViewportHeight = sichtbareZeilen`
  - `ExtentHeight = scrollbackZeilen + sichtbareZeilen`
  - `ScrollOwner?.InvalidateScrollInfo()` aufrufen.

### 3. Rendering anhand Scroll-Offset anpassen

Datei: `src/Softwareschmiede.App/Controls/TerminalControl.cs`

- `OnRender()` rendert nicht mehr starr `snapshot.Grid[0..Rows]`, sondern den aus Verlauf und `VerticalOffset` bestimmten Ausschnitt.
- Logischer Verlauf:
  - Zeilen `0..ScrollbackCount-1`: Scrollback.
  - Zeilen `ScrollbackCount..TotalRows-1`: aktuelles sichtbares Grid.
- Sichtbarer Startindex:
  - Wenn Auto-Follow aktiv ist: `max(0, TotalRows - visibleRows)`.
  - Sonst: geklemmter `VerticalOffset`.
- Cursor nur zeichnen, wenn seine logische Zeile im sichtbaren Ausschnitt liegt:
  - `cursorLogicalRow = scrollbackCount + snapshot.CursorRow`
  - `cursorRenderRow = cursorLogicalRow - visibleStart`
- Hintergrund und Textzeichnung koennen dieselbe Zellzeichnungslogik weiterverwenden; nur der Zugriff auf die Zeile wird abstrahiert.
- Bei leerem oder null-Buffer bleibt das bisherige schwarze Hintergrundverhalten erhalten.

### 4. Auto-Follow am Ende implementieren

Datei: `src/Softwareschmiede.App/Controls/TerminalControl.cs`

- Internen Zustand einfuehren, z. B. `_isFollowingEnd = true`.
- Vor einer Extent-Aktualisierung merken, ob der Anwender am Ende war:
  - `VerticalOffset >= ScrollableHeight - epsilon`
  - oder `_isFollowingEnd == true`.
- Bei neuer Ausgabe:
  - Wenn `_isFollowingEnd` wahr ist, `VerticalOffset` auf neues Ende setzen.
  - Wenn der Anwender hochgescrollt hat, Offset beibehalten und nur klemmen, falls der Scrollback-Ring alte Zeilen verworfen hat.
- Bei `SetVerticalOffset()`:
  - `_isFollowingEnd = offset >= ScrollableHeight - epsilon`.
- Bei Sessionwechsel:
  - Offset auf Ende setzen und `_isFollowingEnd = true`, damit vorhandene Ausgabe der neu gebundenen Session direkt am aktuellen Ende sichtbar ist.

### 5. WPF-Einbindung in TaskDetailView

Datei: `src/Softwareschmiede.App/Views/TaskDetailView.xaml`

- `TerminalControl` in einen vertikalen `ScrollViewer` einbetten:
  - `VerticalScrollBarVisibility="Auto"`
  - `HorizontalScrollBarVisibility="Disabled"`
  - `CanContentScroll="True"`
  - bestehende `Visibility`-Bindung auf den ScrollViewer verschieben.
- `TerminalControl` bleibt `x:Name="TerminalConsole"` und `AutomationProperties.Name="TerminalConsole"`, damit `TaskDetailView.xaml.cs` und vorhandene UI-Automation weiter funktionieren.
- Der `ScrollViewer` erhaelt optional einen Automation-Namen wie `TerminalScrollViewer`, falls E2E-Tests gezielt die Scrollbarkeit pruefen sollen.
- Eingabefokus:
  - `TerminalControl.OnMouseDown()` bleibt erhalten.
  - Falls Klicks auf die ScrollViewer-Flache den Fokus nicht setzen, Preview-Mouse-Handling im Control oder ScrollViewer testen und nur bei Bedarf ergaenzen.

### 6. Keine Aenderungen an Prozess- und ViewModel-Logik

Nicht geplant:

- Keine Aenderung an `PseudoConsoleSession.ReadLoopAsync()`.
- Keine Aenderung an CLI-Start, Stop, Status oder Prompt-Vorlagen in `TaskDetailViewModel`.
- Keine Persistenz von CLI-Ausgaben ueber die bestehende Session-Lebensdauer hinaus.
- Keine Aenderung an ANSI-Parsing oder ConPTY-Resize, ausser bestehende Tests zeigen eine direkte Regression.

## Testplan

### Unit-Tests TerminalBuffer

Datei: `src/Softwareschmiede.Tests/Domain/Terminal/TerminalBufferTests.cs`

- Snapshot enthaelt Scrollback-Zeilen in korrekter Reihenfolge vor den sichtbaren Grid-Zeilen.
- Snapshot bleibt eine Kopie: Veraenderungen an zurueckgegebenen Zeilen beeinflussen den Buffer nicht.
- Scrollback-Grenze bleibt wirksam: Nach mehr als 1000 gescrollten Zeilen enthaelt der Snapshot nur die juengsten Scrollback-Zeilen.
- `ScreenClearedEvent(2)` entfernt Scrollback auch im erweiterten Snapshot.

### Unit-Tests TerminalControl

Datei: `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs`

- `TerminalControl` registriert sich als `IScrollInfo` im umgebenden `ScrollViewer` und meldet bei langem Verlauf `ExtentHeight > ViewportHeight`.
- `LineUp`, `LineDown`, `PageUp`, `PageDown` und `SetVerticalOffset` klemmen den Offset korrekt.
- Bei neuer Ausgabe am Ende springt der Offset ans neue Ende.
- Bei manuellem Zurueckscrollen bleibt der Offset bei neuer Ausgabe erhalten.
- Sessionwechsel setzt den Scrollzustand auf Follow-End und deregistriert weiterhin alte `BufferChanged`-Handler.

### UI-/Integrationstest

Bestehende WPF-/E2E-Teststruktur nutzen, sofern vorhanden:

- CLI-Ansicht oeffnen, lange Ausgabe simulieren oder eine Session mit vielen Zeilen binden.
- Pruefen, dass der Terminal-ScrollViewer vertikal scrollbar ist.
- Nach Scroll nach oben pruefen, dass Terminaleingaben weiterhin im `TerminalControl` ankommen.
- Optional: Nach neuer Ausgabe pruefen, dass Auto-Follow nur greift, wenn vorher am Ende gescrollt war.

### Auszufuehrende Befehle

- `dotnet test`

Falls die gesamte Suite wegen externer Abhaengigkeiten zu langsam oder instabil ist, mindestens:

- `dotnet test --filter TerminalBufferTests`
- `dotnet test --filter TerminalControlTests`

## Risiken und Abgrenzungen

- Der bestehende Scrollback ist ein zeilenbasierter Ringpuffer mit 1000 Zeilen. Sehr lange CLI-Ausgaben bleiben damit nicht vollstaendig unbegrenzt erhalten.
- WPF-`ScrollViewer` mit `IScrollInfo` erfordert exakte Pflege von `ExtentHeight`, `ViewportHeight` und `VerticalOffset`; hier liegt das groesste Implementierungsrisiko.
- Terminal-Resize und Scrollback greifen ineinander: Verkleinerung der Terminalhoehe schiebt sichtbare obere Zeilen in den Scrollback und kann dadurch den Scrollbereich erweitern.
- Horizontaler Scroll wird nicht eingefuehrt, weil das Terminal aktuell ueber Spaltenbreite und Terminalverhalten arbeitet.

## Akzeptanzkriterien

- Bei mehr CLI-Ausgabe als sichtbarer Hoehe erscheint eine vertikale Scrollbar in der CLI-Ansicht.
- Aeltere Ausgaben sind per Scrollbar, Mausrad und Page/Line-Scroll erreichbar.
- Neue Ausgaben erscheinen weiterhin am Ende.
- Auto-Follow bleibt aktiv, solange der Anwender am Ende ist.
- Manuelles Zurueckscrollen wird durch neue Ausgabe nicht sofort ueberschrieben.
- Terminaleingaben und Zwischenablage-Einfuegen funktionieren nach der Scroll-Erweiterung weiter.
- Bestehende TerminalBuffer-, TerminalControl- und PseudoConsoleSession-Tests bleiben gruen.

## Offene Punkte

Keine.
