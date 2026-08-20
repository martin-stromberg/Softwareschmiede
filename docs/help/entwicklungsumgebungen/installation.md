← [Zurück zur Übersicht](index.md)

# Entwicklungsumgebungen — Installation und Konfiguration

## Voraussetzungen

- Mindestens eine IDE muss auf Ihrem Rechner installiert sein (z. B. Visual Studio oder Visual Studio Code)
- Für Visual Studio Code: Der `code`-Befehl muss verfügbar sein (normalerweise automatisch bei Installation)
- Die Softwareschmiede-Anwendung muss ausgeführt werden

## Installationsschritte

Das IDE-Plugin-System wird automatisch bei der Installation bereitgestellt. Es ist keine zusätzliche Konfiguration oder Installation erforderlich. Die IDE-Plugins werden beim Programmstart automatisch erkannt und registriert.

## Konfiguration

Die Konfiguration erfolgt ausschließlich über die Programmeinstellungen (GUI). Es gibt keine Konfigurationsdateien, die manuell bearbeitet werden müssen.

### Aktivierungsstatus

Die Aktivierungsstatus der IDE-Plugins werden in der Datenbank gespeichert:

| Einstellungsschlüssel | Typ | Standardwert | Bedeutung |
|----------------------|-----|--------------|-----------|
| `plugins.enabled.Softwareschmiede.VisualStudio` | Boolean | `true` | Visual Studio IDE-Plugin aktiviert |
| `plugins.enabled.Softwareschmiede.VisualStudioCode` | Boolean | `true` | Visual Studio Code IDE-Plugin aktiviert |

Diese Einstellungen können im Menü **Einstellungen → Plugins → Integrierte Entwicklungsumgebungen** geändert werden.

### Prioritätsreihenfolge

Die Reihenfolge der IDE-Plugins wird durch die folgende Einstellung bestimmt:

| Einstellungsschlüssel | Typ | Standardwert | Beispielwert | Bedeutung |
|----------------------|-----|--------------|--------------|-----------|
| `plugins.ide.order` | String | (nicht gesetzt) | `Softwareschmiede.VisualStudio,Softwareschmiede.VisualStudioCode` | Komma-getrennte Liste der IDE-Plugin-Prefixe in Prioritätsreihenfolge |

Falls diese Einstellung nicht gesetzt ist, wird die Entdeckungsreihenfolge verwendet (normalerweise: Visual Studio, dann Visual Studio Code).

Diese Einstellung wird automatisch aktualisiert, wenn Sie die Reihenfolge in den Einstellungen (Menü → Einstellungen → Plugins → Integrierte Entwicklungsumgebungen) mit den Up/Down-Buttons ändern.

## Umgebungsvariablen

Das IDE-Plugin-System verwendet keine Umgebungsvariablen. Allerdings benötigt **Visual Studio Code** den `code`-Befehl im System-PATH. Falls dieser nicht verfügbar ist, wird bei der Verwendung von VS Code eine Fehlermeldung angezeigt.

**Installation von VS Code CLI (falls erforderlich):**
- Unter Windows: Normalerweise wird der `code`-Befehl während der VS Code Installation automatisch zum PATH hinzugefügt. Falls nicht, können Sie ihn manuell hinzufügen:
  1. Öffnen Sie VS Code
  2. Drücken Sie Strg+Shift+P (Command Palette)
  3. Suchen Sie nach "Shell Command: Install 'code' command in PATH"
  4. Wählen Sie den Befehl aus

## Überprüfung

Um zu prüfen, ob die Installation und Konfiguration erfolgreich waren:

### Test 1: IDE-Plugins werden angezeigt

1. Öffnen Sie **Menü → Einstellungen**
2. Klicken Sie auf den Tab **Plugins**
3. Prüfen Sie, ob Sie im Bereich **Integrierte Entwicklungsumgebungen (IDE)** die folgenden Plugins sehen:
   - Visual Studio
   - Visual Studio Code

Falls diese nicht sichtbar sind, ist das IDE-Plugin-System nicht korrekt geladen.

### Test 2: IDE öffnen funktioniert

1. Öffnen Sie ein beliebiges Projekt in der Softwareschmiede-Anwendung
2. Klicken Sie auf **Menü → IDE öffnen**
3. Die IDE sollte nach wenigen Sekunden öffnen

Falls dies fehlschlägt, überprüfen Sie:
- Ist mindestens eine IDE aktiviert? (Einstellungen → Plugins → Integrierte Entwicklungsumgebungen)
- Sind die IDEs auf dem Rechner installiert?
- Für VS Code: Ist der `code`-Befehl verfügbar? (Öffnen Sie eine PowerShell/Kommandozeile und geben Sie `code` ein)

### Test 3: IDE-Kompatibilität wird korrekt erkannt

1. Öffnen Sie zwei verschiedene Projekte: eines mit `.sln`-Datei, eines ohne
2. Für das Projekt mit `.sln`: Visual Studio sollte geöffnet werden (falls aktiviert)
3. Für das Projekt ohne `.sln`: VS Code sollte geöffnet werden (falls Visual Studio deaktiviert)

Falls dies nicht funktioniert:
- Überprüfen Sie, ob die `.sln`-Datei wirklich im Projekt-Root liegt (nicht in Unterverzeichnissen)
- Überprüfen Sie die Programmausgabe für Fehler (Menü → Hilfe → Protokoll öffnen)
