← [Zurück zur Übersicht](index.md)

# Entwicklungsumgebungen — Ablauf für Anwender

## Voraussetzungen

- Sie haben ein oder mehrere IDEs installiert (z. B. Visual Studio, Visual Studio Code)
- Die IDE wird in den Programmeinstellungen aktiviert (Standard: beide aktiviert)
- Sie arbeiten in einem Repository, das im System registriert ist

## Schritt-für-Schritt-Anleitung

### IDE öffnen

Öffnen Sie eine Aufgabe mit vorhandenem Arbeitsverzeichnis und klicken Sie im Ribbon (Gruppe „Werkzeuge") der Aufgabendetailansicht auf den Button **IDE öffnen**.

Das System führt folgende Schritte durch:

### Schritt 1: Automatische IDE-Erkennung

Das Programm prüft automatisch, welche installierten IDEs mit Ihrem Repository kompatibel sind:

- **Visual Studio** wird bevorzugt, wenn das Repository eine Datei namens `*.sln` (Solution) oder `*.slnx` (neu) im Hauptverzeichnis enthält. Dies ist typischerweise der Fall bei C#- oder .NET-Projekten.
- **Visual Studio Code** wird als Universallösung verwendet, wenn kein anderes spezialisiertes Plugin aktiv ist oder keine Solution-Datei vorhanden ist.

> **Hinweis:** Die Prüfung geschieht vollautomatisch. Findet Visual Studio dabei mehrere Solution-Dateien im Arbeitsverzeichnis, erscheint ein Auswahl-Dialog; in allen anderen Fällen wird das beste Programm direkt gestartet, ohne dass Sie einen Dialog sehen.

### Schritt 2: IDE startet

Die erkannte IDE öffnet sich automatisch mit Ihrem Repository:

- **Visual Studio** lädt die Solution-Datei und stellt alle Projekte dar.
- **Visual Studio Code** öffnet das Verzeichnis als Workspace.

## Ergebnis

Nach wenigen Sekunden haben Sie Ihre gewünschte IDE mit dem Repository geöffnet und können sofort an Ihrem Code arbeiten.

## Prioritäten ändern

Falls Sie eine andere IDE bevorzugen oder einzelne IDEs deaktivieren möchten:

1. Öffnen Sie **Menü → Einstellungen** → Tab **Plugins**
2. Im Bereich **Integrierte Entwicklungsumgebungen (IDE)** (linke Spalte) sehen Sie eine Liste aller verfügbaren IDEs
3. Wählen Sie ein Plugin in der Liste aus; im Inhaltsbereich rechts erscheint dessen Checkbox **Plugin aktiviert**, mit der Sie es aktivieren oder deaktivieren
4. Mit den **Pfeilen** (↑/↓) neben jedem Listeneintrag können Sie die Reihenfolge der IDEs ändern — die oben stehende IDE wird bevorzugt
5. Speichern Sie die Einstellungen mit **Speichern**

> **Wichtig:** Sie müssen mindestens eine IDE aktiviert lassen. Das Programm wird Ihnen nicht erlauben, alle IDEs gleichzeitig zu deaktivieren.

### Beispiel: Visual Studio Code bevorzugen

Auch wenn eine Solution-Datei vorhanden ist, möchten Sie VS Code verwenden:

1. Öffnen Sie Einstellungen → Plugins
2. In der IDE-Liste: Verschieben Sie "Visual Studio Code" mit den Pfeilen nach oben
3. Speichern Sie
4. Nächstes Mal, wenn Sie IDE öffnen, wird VS Code bevorzugt (auch für Repositories mit `.sln`)

## Barrierefreiheit

- Die Liste der IDEs in den Einstellungen kann mit der **Tastatur** navigiert werden (Tab, Pfeile)
- Checkboxen können mit **Leertaste** aktiviert/deaktiviert werden
- Die Pfeile zum Verschieben sind über **Tab** erreichbar und können mit **Enter** betätigt werden
