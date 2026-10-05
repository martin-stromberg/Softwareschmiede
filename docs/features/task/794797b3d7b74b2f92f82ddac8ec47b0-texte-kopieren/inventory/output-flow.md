# Daten- und Ausgabe-Fluss

## Live-Ausgabe

Die Aufgabenansicht erhält ihre aktive Session über `TaskDetailViewModel` und setzt sie am `TerminalControl`. Die Session stellt `Buffer`, `BufferChanged`, `InputStream`, `WriteInputAsync`, Resize- und Replay-Funktionen bereit. Bei neuer Ausgabe wird der Terminalbuffer aktualisiert; `TerminalControl` reagiert auf `BufferChanged`, aktualisiert Scrollinformationen und rendert erneut.

Die sichtbare Ausgabe ist somit ein gerenderter Zustand des `TerminalBuffer`, nicht bloß der zuletzt protokollierte CLI-Text. Cursorbewegungen, Überschreiben bestehender Zellen, Farben, Scrollback und alternative Bildschirme können die Beziehung zwischen ursprünglichen Bytes und sichtbaren Zeichen beeinflussen. Für einen aus der aktuellen Anzeige kopierten Ausschnitt ist der Buffer-Snapshot die maßgebliche Datenquelle.

## Replay und Export

`KonsolenTestDialog.xaml` nutzt dasselbe `TerminalControl` mit einer Replay-Session. Die Replay-Session besitzt keinen echten Eingabekanal (`Stream.Null`); Navigationstasten bleiben dort teilweise unbehandelt, damit der umschließende ScrollViewer scrollen kann.

`TaskDetailView.xaml` bietet außerdem getrennte Aktionen zum Exportieren der protokollierten Rohausgabe (`.raw`) und einer CLI-Aufzeichnung (`.clireplay`). Diese Exportpfade sind eigenständige Funktionen und implementieren keine Auswahl/Kopierfunktion für den gerade sichtbaren Terminalinhalt.

## Verhaltensfragen

Die übersetzte Anforderung lässt offen, ob `Ctrl+C` kopieren soll oder weiterhin an die laufende CLI weitergegeben wird. Ebenso ist offen, ob eine bestehende Markierung beim Eintreffen neuer Ausgabe beibehalten, angepasst oder aufgehoben werden soll. Die aktuelle Render-/Buffer-Architektur legt dafür noch kein Verhalten fest.
