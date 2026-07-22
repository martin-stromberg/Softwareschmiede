# UI und Layout

## TaskDetailView.xaml

Die Aufgaben-Detailansicht verwendet ein Haupt-Grid mit Ribbon, Fehlerbereich, Hauptinhalt und Fusszeile. Der Hauptinhalt liegt in `Grid.Row="2"` und besitzt eine Kopfzeile fuer die Ansichts-Toggles sowie einen Contentbereich mit mehreren ueber `Visibility` geschalteten Ansichten.

Relevante Stellen:

- `TaskDetailView.xaml:230-233`: Contentbereich mit `RowDefinition Height="*"` fuer die eigentliche Ansicht.
- `TaskDetailView.xaml:273-393`: Info-Ansicht ist in einem `ScrollViewer` mit `VerticalScrollBarVisibility="Auto"` eingebettet.
- `TaskDetailView.xaml:395-397`: CLI-Ansicht ist ein direkt platziertes `TerminalControl` ohne `ScrollViewer`.
- `TaskDetailView.xaml:399-416`: Diff-Ansicht ist ebenfalls in einem `ScrollViewer` eingebettet.

Damit unterscheidet sich die CLI-Anzeige strukturell von den anderen scrollbaren Detailansichten.

## TerminalControl.cs

`TerminalControl` ist kein `TextBox`, `RichTextBox`, `ItemsControl` oder XAML-`UserControl`, sondern ein direkt von `FrameworkElement` abgeleitetes Custom-Control. Es zeichnet Hintergrund, Zellen, Text und Cursor in `OnRender(DrawingContext)`.

Relevante Stellen:

- `TerminalControl.cs:16`: `public sealed class TerminalControl : FrameworkElement`
- `TerminalControl.cs:33-43`: `Session` als Dependency Property.
- `TerminalControl.cs:102`: Start von `OnRender`.
- `TerminalControl.cs:244-258`: Bei Groessenaenderung werden Buffer und PseudoConsole auf die aus der Control-Groesse berechneten Spalten/Zeilen resized.
- `TerminalControl.cs:279-288`: `CalculateCols()` und `CalculateRows()` berechnen sichtbare Terminaldimensionen aus `ActualWidth` und `ActualHeight`.

## Konsequenz fuer Scrollbarkeit

Ein vertikaler WPF-`ScrollViewer` braucht ein scrollbares Extent. Das aktuelle `TerminalControl` meldet aber keine Verlaufshoehe, sondern rendert immer genau den sichtbaren Bufferzustand in seiner aktuellen `ActualHeight`. Ohne Erweiterung des Controls oder der bereitgestellten Daten haette ein Wrapper-`ScrollViewer` keinen Zugriff auf alte Zeilen und wahrscheinlich kein sinnvolles Scroll-Extent.

Eine belastbare Loesung muss daher mindestens eine der folgenden Flaechen adressieren:

- `TerminalBuffer` stellt sichtbares Grid plus Scrollback als Snapshot bereit.
- `TerminalControl` kann aus diesem Verlauf eine groessere logische Renderflaeche oder einen eigenen Scrolloffset zeichnen.
- `TaskDetailView.xaml` bettet die CLI-Anzeige so ein, dass der vertikale Scrollzustand erreichbar bleibt.
