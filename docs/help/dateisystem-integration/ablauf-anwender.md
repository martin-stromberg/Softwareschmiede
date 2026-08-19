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
3. Haupt-Teil des Split-Buttons „IDE öffnen" klicken (der Dropdown-Pfeil daneben ist ausgeblendet, da nur ein Einstiegspunkt existiert).
   - Visual Studio (oder die für `.sln`-Dateien registrierte IDE) öffnet sich automatisch mit der Solution.
   - Alle Projekte der Solution werden geladen.

### IDE öffnen (mit mehreren Solutions)

Der Button „IDE öffnen" ist als Split-Button mit zwei Teilen umgesetzt: einem Haupt-Teil (öffnet direkt) und einem Dropdown-Teil (Pfeil-Symbol daneben, nur sichtbar, wenn mehr als eine Solution gefunden wurde).

**Schnell öffnen (Haupt-Teil):**
1. Aufgabe öffnen.
2. Haupt-Teil des Buttons „IDE öffnen" klicken.
   - Die erste gefundene Solution öffnet sich sofort in Visual Studio, ohne Rückfrage.

**Gezielt auswählen (Dropdown-Teil):**
1. Aufgabe öffnen.
2. Auf den Dropdown-Pfeil neben „IDE öffnen" klicken.
   - Ein Dialog „Solution auswählen" erscheint mit einer Liste aller gefundenen Solutions.
3. Gewünschte Solution in der Liste auswählen (Pfade sind alphabetisch sortiert).
4. Button „OK" klicken.
   - Die gewählte Solution öffnet sich in Visual Studio.
5. Alternativ: Button „Abbrechen" klicken, um den Dialog zu schließen ohne eine Solution zu öffnen.

> **Hinweis:** Der Dialog zeigt die vollständigen Pfade der gefundenen Solutions an. Er erscheint ausschließlich über den Dropdown-Teil des Split-Buttons — der Haupt-Teil öffnet immer direkt die erste Solution, ohne Rückfrage.

### IDE öffnen (ohne Solution mit Visual Studio Code)

1. Öffnen Sie eine Aufgabe mit vorhandenem Arbeitsverzeichnis, aber ohne `*.sln`-Datei.
2. Klicken Sie **IDE öffnen**.
   - Es ist keine separate Einstellung nötig: Ist kein IDE-Plugin explizit kompatibel (z. B. keine `.sln`-Datei gefunden), wird automatisch das erste aktive Fallback-Plugin verwendet — standardmäßig Visual Studio Code.
   - Ist Visual Studio Code verfügbar, wird das Arbeitsverzeichnis in VS Code geöffnet.
   - Ist Visual Studio Code nicht verfügbar, erscheint eine Fehlermeldung.

> **Hinweis:** Wenn später eine `*.sln`-Datei im Arbeitsverzeichnis vorhanden ist, öffnet die Aktion wieder die Solution. Die Solution hat Vorrang vor dem Fallback-Plugin. Möchten Sie Visual Studio Code als Fallback nicht verwenden, deaktivieren Sie das Plugin unter **Einstellungen → Plugins → Integrierte Entwicklungsumgebungen (IDE)** (mindestens ein IDE-Plugin muss aktiv bleiben).

## Konfiguriertes Arbeitsunterverzeichnis

Falls beim Repository ein **Arbeitsunterverzeichnis** konfiguriert wurde (z. B. über die Projekt-Einstellungen):

1. **„Arbeitsverzeichnis öffnen"** öffnet das konfigurierte Unterverzeichnis (z. B. `Projekt\src\backend`), nicht den Repository-Root.
2. **„IDE öffnen"** sucht nach Solutions nur im konfigurierten Unterverzeichnis. Solutions im Repository-Root werden nicht berücksichtigt.
3. Das **Fallback-Plugin** (standardmäßig Visual Studio Code) startet mit dem konfigurierten Unterverzeichnis als Arbeitsverzeichnis.

> **Hinweis:** Wenn die Arbeit in einem Mono-Repo auf ein spezielles Subprojekt beschränkt sein soll, konfiguriert die Projekt-Verwaltung das entsprechende Unterverzeichnis. Anschließend verwenden alle Ribbon-Aktionen automatisch dieses Unterverzeichnis, statt den Gesamtrepository-Pfad.

## Ergebnis

Nach erfolgreicher Ausführung:
- **Arbeitsverzeichnis öffnen:** Der System-Dateiexplorer zeigt die Verzeichnisstruktur des Arbeitsverzeichnisses (aufgelöstes Unterverzeichnis oder Repository-Root).
- **IDE öffnen:** Die IDE (z. B. Visual Studio) ist mit der gefundenen Solution geladen und bereit zur Bearbeitung.
- **IDE öffnen ohne Solution:** Das aktive Fallback-Plugin (standardmäßig Visual Studio Code) zeigt das Arbeitsverzeichnis (aufgelöstes Unterverzeichnis) an.

## Barrierefreiheit

Alle Buttons unterstützen Tastaturnavigation, einschließlich der beiden Teile des Split-Buttons „IDE öffnen":
- Mit **Tab** können Sie zum Haupt-Teil und (falls sichtbar) zum Dropdown-Teil des Buttons navigieren.
- Mit **Enter** oder **Leerzeichen** können Sie den jeweiligen Teil aktivieren.
- Der Auswahl-Dialog (bei mehreren Solutions, nur über den Dropdown-Teil erreichbar) kann vollständig mit Tastatur bedient werden: Pfeiltasten zum Navigieren der Liste, **Enter** zum Bestätigen, **Escape** zum Abbrechen.

Die Buttons zeigen Tooltips, wenn Sie den Mauszeiger über sie halten.
