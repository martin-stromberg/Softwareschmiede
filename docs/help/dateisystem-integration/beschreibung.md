← [Zurück zur Übersicht](index.md)

# Dateisystem-Integration — Beschreibung

## Zweck

Die Dateisystem-Integration ermöglicht es Benutzern, direkt aus der Aufgabendetailansicht heraus auf das Arbeitsverzeichnis zuzugreifen und Projektdateien zu öffnen — ohne dass das Arbeitsverzeichnis erst manuell über externe Programme gesucht werden muss.

## Funktionsweise

Das Feature stellt zwei neue Aktionsbuttons im Ribbon der Aufgabendetailansicht bereit:

### Arbeitsverzeichnis öffnen

Öffnet das Arbeitsverzeichnis des geklonten Repositories im Standard-Dateiexplorer des Betriebssystems:
- **Windows:** Startet `explorer.exe` mit dem Arbeitsverzeichnis-Pfad
- **Linux:** Startet `xdg-open` mit dem Verzeichnis
- **macOS:** Startet `open` mit dem Verzeichnis

Der Button ist nur aktiv, wenn ein gültiges Arbeitsverzeichnis vorhanden ist (d. h., ein Repository mit lokalen Klon wurde zugewiesen).

### IDE öffnen

Öffnet eine Visual-Studio-Solution (`.sln`-Datei) des Arbeitsverzeichnisses mit dem beim Betriebssystem registrierten Standard-Handler (üblicherweise Visual Studio):

- **Bei genau einer Solution:** Öffnet diese direkt ohne Dialog.
- **Bei mehreren Solutions:** Zeigt einen Auswahl-Dialog mit allen gefundenen Solutions (alphabetisch sortiert nach Dateinamen). Der Benutzer wählt die gewünschte Solution und bestätigt mit OK.
- **Bei Abbruch:** Die ausgewählte Solution wird nicht geöffnet.
- **Ohne Solution:** Standardmäßig ist der Button deaktiviert. Wenn in den Programmeinstellungen der VS-Code-Fallback aktiviert ist, öffnet der Button stattdessen das Arbeitsverzeichnis in Visual Studio Code.

Solutions werden nur auf der obersten Verzeichnisebene des Arbeitsverzeichnisses gesucht (nicht rekursiv).
Gefundene `*.sln`-Dateien haben immer Vorrang vor dem VS-Code-Fallback.

## Beispiele

### Arbeitsverzeichnis durchsuchen

1. Aufgabe in der Aufgabenliste öffnen.
2. Im Ribbon (Gruppe „Werkzeuge") auf Button „Arbeitsverzeichnis öffnen" klicken.
3. Der Dateiexplorer öffnet sich und zeigt die Dateien des Arbeitsverzeichnisses.

### Solution in Visual Studio öffnen

1. Ein Repository mit mindestens einer `*.sln`-Datei im Arbeitsverzeichnis zuweisen.
2. Aufgabe öffnen.
3. Im Ribbon (Gruppe „Werkzeuge") auf Button „IDE öffnen" klicken.
4. Ist genau eine Solution vorhanden: Visual Studio öffnet sich mit dieser Solution.
5. Sind mehrere Solutions vorhanden: Auswahl-Dialog erscheint → Solution wählen → OK klicken → Visual Studio öffnet die gewählte Solution.

### Arbeitsverzeichnis in Visual Studio Code öffnen

1. In den Einstellungen im Tab „Allgemein" die Option „Visual Studio Code oeffnen, wenn keine Visual-Studio-Solution gefunden wurde" aktivieren und speichern.
2. Eine Aufgabe ohne `*.sln`-Datei, aber mit vorhandenem Arbeitsverzeichnis öffnen.
3. Im Ribbon auf „IDE öffnen" klicken.
4. Visual Studio Code öffnet das Arbeitsverzeichnis, sofern `code`/`code.cmd` über `PATH` oder eine typische Windows-Installation gefunden wird.

## Einschränkungen

- Die Anwendung prüft nicht, ob die IDE (z. B. Visual Studio) auf dem System installiert ist. Ist sie nicht vorhanden oder kein Betriebssystem-Handler für `.sln`-Dateien registriert, wird eine Fehlermeldung angezeigt.
- Der VS-Code-Fallback ist opt-in und standardmäßig deaktiviert. Ist er aktiviert, aber Visual Studio Code nicht auffindbar, zeigt die Aufgabendetailansicht einen Hinweis.
- Solutions werden nur auf der obersten Verzeichnisebene erkannt (keine rekursive Suche in Unterverzeichnissen).
- Das Arbeitsverzeichnis muss auf der Festplatte vorhanden sein. Ist der konfigurierte Pfad gelöscht oder nicht erreichbar, sind die Buttons inaktiv.
