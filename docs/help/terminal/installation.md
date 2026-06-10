# CLI-Terminal — Installation und Konfiguration

## Voraussetzungen

| Abhängigkeit | Version |
|-------------|---------|
| Node.js | ≥ 18 |
| `node-pty` | Wie in `package.json` |
| `ws` (WebSocket) | Wie in `package.json` |

## Installationsschritte

1. In das Backend-Verzeichnis wechseln:
   ```
   cd src/Softwareschmiede/terminal-backend
   ```

2. Abhängigkeiten installieren:
   ```
   npm install
   ```

3. Backend starten:
   ```
   node server.js
   ```

   Erwartete Ausgabe:
   ```
   Terminal backend running on ws://localhost:3001
   ```

## Konfiguration

| Parameter | Wert | Beschreibung |
|-----------|------|--------------|
| Port | `3001` | WebSocket-Port (fest kodiert in `server.js` und `CliTerminal.razor`) |
| Shell (Windows) | `powershell.exe` | Wird für die PTY-Session genutzt |
| Cols / Rows | 120 / 30 | Initiale Terminalgröße |

## Überprüfung

Öffne eine Aufgabe im Status „In Bearbeitung", wechsle in das Register „Ausführung" und wähle ein KI-Plugin. Das Terminal sollte innerhalb weniger Sekunden erscheinen und die CLI-Initialisierungsausgabe anzeigen.

Erscheint das Terminal nicht oder bleibt es leer, prüfe:
- Ob `node server.js` in `terminal-backend/` läuft.
- Ob Port 3001 nicht durch eine Firewall blockiert ist.
- Browser-Konsole auf WebSocket-Verbindungsfehler prüfen.
