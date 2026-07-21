← [Zurück zur Übersicht](index.md)

# Dateisystem-Integration — Ablauf für Anwender

## Voraussetzungen

Für die Verwendung dieser Funktionen muss:
- Eine Aufgabe existieren, der ein Repository mit gültigem lokalen Klon zugewiesen wurde.
- Das Arbeitsverzeichnis auf der Festplatte vorhanden und erreichbar sein.

## Schritt-für-Schritt-Anleitung

### Arbeitsverzeichnis öffnen

1. Aufgabe in der Aufgabenliste auswählen und öffnen (Aufgabendetailansicht erscheint).
2. Im Ribbon oben auf der Seite die Gruppe „Werkzeuge" finden.
3. Button „Arbeitsverzeichnis öffnen" klicken.
   - Der Standard-Dateiexplorer des Systems öffnet sich mit dem Arbeitsverzeichnis angezeigt.
   - Sie können nun Dateien navigieren, öffnen oder bearbeiten.

> **Hinweis:** Ist der Button ausgegraut (deaktiviert), existiert kein gültiges Arbeitsverzeichnis für diese Aufgabe.

> **Troubleshooting:** Sollte der Dateiexplorer nicht öffnen, prüfen Sie, ob der konfigurierte Arbeitsverzeichnis-Pfad noch auf der Festplatte vorhanden ist.

### IDE öffnen (mit genau einer Solution)

1. Aufgabe öffnen.
2. Im Ribbon die Gruppe „Werkzeuge" finden.
3. Button „IDE öffnen" klicken.
   - Visual Studio (oder die für `.sln`-Dateien registrierte IDE) öffnet sich automatisch mit der Solution.
   - Alle Projekte der Solution werden geladen.

### IDE öffnen (mit mehreren Solutions)

1. Aufgabe öffnen.
2. Button „IDE öffnen" klicken.
   - Ein Dialog „Solution auswählen" erscheint mit einer Liste aller gefundenen Solutions.
3. Gewünschte Solution in der Liste auswählen (Pfade sind alphabetisch sortiert).
4. Button „OK" klicken.
   - Die gewählte Solution öffnet sich in Visual Studio.
5. Alternativ: Button „Abbrechen" klicken, um den Dialog zu schließen ohne eine Solution zu öffnen.

> **Hinweis:** Der Dialog zeigt die vollständigen Pfade der gefundenen Solutions an.

### IDE öffnen (ohne Solution mit Visual Studio Code)

1. Öffnen Sie **Einstellungen** → **Allgemein**.
2. Aktivieren Sie **Visual Studio Code oeffnen, wenn keine Visual-Studio-Solution gefunden wurde**.
3. Klicken Sie **Speichern**.
4. Öffnen Sie eine Aufgabe mit vorhandenem Arbeitsverzeichnis, aber ohne `*.sln`-Datei.
5. Klicken Sie **IDE öffnen**.
   - Ist Visual Studio Code verfügbar, wird das Arbeitsverzeichnis in VS Code geöffnet.
   - Ist Visual Studio Code nicht verfügbar, erscheint die Meldung: „Keine Visual-Studio-Solution gefunden und Visual Studio Code wurde nicht gefunden."

> **Hinweis:** Wenn später eine `*.sln`-Datei im Arbeitsverzeichnis vorhanden ist, öffnet die Aktion wieder die Solution. Die Solution hat Vorrang vor VS Code.

## Ergebnis

Nach erfolgreicher Ausführung:
- **Arbeitsverzeichnis öffnen:** Der System-Dateiexplorer zeigt die Verzeichnisstruktur des Arbeitsverzeichnisses.
- **IDE öffnen:** Die IDE (z. B. Visual Studio) ist mit der Solution geladen und bereit zur Bearbeitung.
- **IDE öffnen ohne Solution und mit aktiviertem Fallback:** Visual Studio Code zeigt das Arbeitsverzeichnis an.

## Barrierefreiheit

Beide Buttons unterstützen Tastaturnavigation:
- Mit **Tab** können Sie zu den Buttons navigieren.
- Mit **Enter** oder **Leerzeichen** können Sie den Button aktivieren.
- Der Auswahl-Dialog (bei mehreren Solutions) kann vollständig mit Tastatur bedient werden: Pfeiltasten zum Navigieren der Liste, **Enter** zum Bestätigen, **Escape** zum Abbrechen.

Die Buttons zeigen Tooltips, wenn Sie den Mauszeiger über sie halten.
