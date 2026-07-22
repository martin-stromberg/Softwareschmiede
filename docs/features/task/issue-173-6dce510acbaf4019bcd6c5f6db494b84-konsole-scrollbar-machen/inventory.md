# Bestandsaufnahme - Konsole scrollbar machen

## Kurzfazit

Die CLI-Konsole wird aktuell nicht als XAML-Textcontainer gerendert, sondern als eigenes WPF-`FrameworkElement` (`TerminalControl`), das den sichtbaren Ausschnitt eines `TerminalBuffer` per `DrawingContext` zeichnet. In `TaskDetailView.xaml` liegt dieses Control direkt im Content-Grid der CLI-Ansicht und ist nicht in einen vertikalen `ScrollViewer` eingebettet. Die eigentliche Ausgabeverarbeitung laeuft in `PseudoConsoleSession`; dort existiert bereits ein interner Scrollback-Ringpuffer im `TerminalBuffer`, dieser wird aber nicht ueber die oeffentliche Snapshot-/Render-Schnittstelle an die UI weitergegeben.

Die Anforderung betrifft daher primaer die Kopplung aus UI-Layout, `TerminalControl`-Rendering und `TerminalBuffer`-Snapshot. Die Prozessausfuehrung, PseudoConsole-Erzeugung und CLI-Startlogik muessen voraussichtlich nicht geaendert werden.

## Detaildokumente

- [UI und Layout](inventory/ui-layout.md)
- [Terminal-Buffer und PseudoConsoleSession](inventory/terminal-buffer-session.md)
- [Views und ViewModels](inventory/views-viewmodels.md)
- [Tests](inventory/tests.md)

## Relevante Dateien

| Bereich | Datei | Bedeutung |
|---|---|---|
| CLI-Anzeige | `src/Softwareschmiede.App/Controls/TerminalControl.cs` | Eigenes WPF-Render-Control fuer Terminalzellen; kein eingebautes WPF-Scrolling. |
| Einbindung | `src/Softwareschmiede.App/Views/TaskDetailView.xaml` | Platziert `TerminalControl` in der CLI-Ansicht ohne vertikalen `ScrollViewer`. |
| Session-Bindung | `src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs` | Setzt `TerminalConsole.Session` bei DataContext-Wechsel und CLI-Events. |
| ViewModel | `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | Startet CLI, feuert `PseudoConsoleSessionGestartet`, verwaltet CLI-Ansicht und Status. |
| Buffer | `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs` | Haelt sichtbares Grid und internen Scrollback-Ringpuffer mit maximal 1000 Zeilen. |
| Session | `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` | Liest Output, parsed ANSI, aktualisiert Buffer und feuert `BufferChanged`. |
| Parser | `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs` | Wandelt VT100/ANSI-Ausgabe in Terminal-Events um. |

## Beobachtungen

- `TerminalControl` rendert nur die Zeilen aus `TerminalBuffer.GetSnapshot()`. Dieser Snapshot enthaelt aktuell nur das sichtbare Grid, nicht den Scrollback.
- `TerminalBuffer` sammelt beim Scrollen und bei Verkleinerung bereits Scrollback-Zeilen, macht diese aber nur als `ScrollbackCount` intern fuer Tests sichtbar.
- Die sichtbare Zeilenanzahl wird in `TerminalControl.CalculateRows()` aus `ActualHeight / _cellHeight` berechnet und bei Groessenaenderung an `buffer.Resize(cols, rows)` und `session.Resize(cols, rows)` weitergegeben.
- `TaskDetailView.xaml` enthaelt fuer Info- und Diff-Ansichten `ScrollViewer`, fuer die CLI-Ansicht aber direkt `<controls:TerminalControl ... />`.
- Ein einfacher XAML-`ScrollViewer` um `TerminalControl` reicht wahrscheinlich nicht allein, weil das Control seine eigene Hoehe nicht als gesamte Verlaufslaenge meldet und der Buffer-Snapshot keinen Scrollback-Inhalt liefert.

## Offene technische Punkte fuer die Planung

- Soll die Scrollbarkeit auf dem vorhandenen Terminalzellen-Renderer aufbauen oder soll eine zusaetzliche UI-Schicht den Scrollback in das Render-Control integrieren?
- Soll der bestehende `MaxScrollbackLines = 1000` fuer die Anforderung als maximale Verlaufsgroesse gelten?
- Wie soll Auto-Scroll funktionieren: immer ans Ende, oder nur solange der Anwender nicht manuell nach oben gescrollt hat?
- Soll horizontales Scrollen bewusst unveraendert bleiben? Aktuell bestimmt die Breite die Terminalspalten; lange Zeilen werden durch Terminalverhalten statt durch horizontalen UI-Scroll behandelt.
