# Bestandsaufnahme: Unvollstaendiges Copy & Paste in der Pseudokonsole

Analysiert wurden der manuelle Clipboard-Paste im WPF-Terminal-Control, die VT100-/Zeilenumbruch-Kodierung, die ConPTY-Session samt Input-Stream, der KI-/CLI-Startpfad und die vorhandene Testabdeckung. Die Ausfuehrung erfolgte direkt durch den Hauptagenten, weil in dieser Umgebung keine Unteragenten-Werkzeuge verfuegbar sind.

## Zusammenfassung

- Der direkte Paste-Pfad liegt in `src/Softwareschmiede.App/Controls/TerminalControl.cs`: `Ctrl+V` wird in `OnPreviewKeyDown` abgefangen, der Clipboard-Text wird in `ReadClipboardAndInsertAsync` gelesen, per `KeyToVt100Encoder.EncodeClipboardText` kodiert und anschliessend als ein zusammenhaengender Byte-Block auf `Session.InputStream.WriteAsync` geschrieben.
- Der Encoder normalisiert Clipboard-Zeilenumbrueche bereits auf einzelne Carriage Returns (`\r`) und kodiert den kompletten Text als UTF-8. Sonderzeichen, Umlaute, Tabs, Klammern, Backticks und generische Typnamen werden nicht speziell geparst und sollten daher erhalten bleiben, solange der String vollstaendig in den Stream geschrieben wird.
- Die Pseudokonsole verwendet `PseudoConsoleSession.InputStream` als gemeinsamen Eingang fuer Tastatureingaben, Clipboard-Paste, initialen Plugin-Befehl und Prompt-Versand. Manuelle Tasten schreiben synchron ohne Flush, Clipboard-Paste schreibt asynchron ohne expliziten Flush, `WritePromptAsync` flusht dagegen explizit.
- Der beobachtete Fehler "nur letzter Abschnitt kommt an" passt nicht zu einer offensichtlichen String-Kuerzung im UI-Handler oder Encoder. Wahrscheinlicher sind Timing-/Stream-Probleme im ConPTY-Input-Pfad: gleichzeitige Writes, fehlender Flush, ein partiell abgeschlossener asynchroner Write, Session-Wechsel/Dispose waehrend des Paste-Vorgangs oder eine Puffer-/Throughput-Grenze der nativen Pipe/CLI.
- Es gibt keinen zentralen, wiederverwendbaren Paste-Service. Die Paste-Logik sitzt privat im UI-Control und ist dadurch nur ueber Reflection-/WPF-Tests pruefbar. Falls Chunking, Flusskontrolle oder Logging benoetigt wird, sollte die Schreiblogik in eine testbare Komponente oder in `PseudoConsoleSession` wandern.
- Die vorhandenen Tests decken Encoder-Normalisierung, einfache Clipboard-Pastes und `WritePromptAsync` ab. Es fehlt ein automatisierter Test fuer langen mehrzeiligen Clipboard-Inhalt mit Stacktrace-artigem Text und ein Test, der mehrere intern erzeugte Schreibvorgaenge auf Vollstaendigkeit und Reihenfolge absichert.
- Claude ist fachlich nur ueber das aktive KI-Plugin relevant. Die gleiche Pseudokonsolen-Infrastruktur wird fuer alle Development-Automation-Plugins genutzt, die ueber `StartWithPseudoConsoleAsync` gestartet werden. Eine Korrektur im gemeinsamen Paste-/Input-Pfad wirkt daher pluginuebergreifend.

## Details

- [UI- und Paste-Pfad](inventory/ui-paste-path.md)
- [Pseudokonsole und Eingabestream](inventory/pseudoconsole-input.md)
- [KI-/CLI-Plugin-Bezug](inventory/cli-plugins.md)
- [Tests und Diagnose](inventory/tests-diagnostics.md)

## Einschätzung fuer die Planung

Der naheliegende Umsetzungsbereich ist der gemeinsame Schreibpfad fuer laengere Eingaben in eine `PseudoConsoleSession`. Eine robuste Loesung sollte den Clipboard-Text vor dem Schreiben snapshotten, sequentielle Writes pro Session erzwingen, bei Bedarf kontrolliert chunkingfaehig sein, jeden Chunk awaiten und flushen sowie Schreibfehler mit Laenge/Chunk-Index protokollieren. Normale Tastatureingaben sollten weiterhin kurze, direkte Writes verwenden koennen, muessen aber gegen gleichzeitige Paste-Operationen abgegrenzt werden.
