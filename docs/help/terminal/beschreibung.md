# CLI-Terminal — Beschreibung

## Zweck

Das CLI-Terminal ermöglicht die direkte, interaktive Bedienung von Claude CLI oder GitHub Copilot CLI innerhalb der Softwareschmiede. Im Gegensatz zur automatisierten KI-Ausführung kann der Anwender hier frei im Terminal arbeiten — zum Beispiel für manuelle Debugging-Sessions oder exploratives Prompting.

## Funktionsweise

Die Komponente `CliTerminal.razor` bettet einen `xterm.js`-Terminal (via `XtermBlazor`) in die Ausführungsansicht einer Aufgabe ein. Die Terminalausgabe wird über eine WebSocket-Verbindung zu einem Node.js-Backend gestreamt, das eine echte PTY-Shell startet.

### Verbindungsaufbau

1. Bei der ersten Darstellung der Komponente (`OnFirstRender`) wird eine WebSocket-Verbindung zu `ws://localhost:3001` geöffnet.
2. Die Komponente sendet `SET_CWD:<pfad>` — das Backend startet daraufhin eine PowerShell-Shell im angegebenen Arbeitsverzeichnis.
3. Die Komponente sendet `START_CLI:<name>` (`claude` oder `copilot`) — das Backend tippt den Befehl in die Shell ein.
4. Alle weiteren Tastatureingaben werden direkt an die PTY-Shell weitergeleitet.

### Sichtbarkeit

Das Terminal erscheint im Register **Ausführung** der Aufgabendetailansicht, wenn:
- Ein KI-Plugin aktiv ist (`_selectedKiPluginPrefix` gesetzt), und
- Ein lokales Arbeitsverzeichnis vorhanden ist (`AktuellesWorkingDirectory` nicht leer).

## Beispiele

- Manuell `claude chat .` im Aufgabenverzeichnis bedienen.
- Mit dem Copilot CLI explorative Fragen stellen, ohne einen vollständigen KI-Lauf zu starten.

## Einschränkungen

- Das Node.js-Backend (`terminal-backend/server.js`) muss separat gestartet werden.
- Die WebSocket-Adresse `ws://localhost:3001` ist fest kodiert.
- Verbindungsfehler (z.B. Backend nicht gestartet) führen zu einer stillen Exception; das Terminal bleibt leer.
- `CliSessionService` ist registriert, aber derzeit nicht mit der `CliTerminal`-Komponente verbunden (alternative prozessbasierte Implementierung).
