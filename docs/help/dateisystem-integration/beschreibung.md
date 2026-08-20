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

Löst über das IDE-Plugin-System (siehe [IDE-Plugin-System in der README](../../../README.md)) automatisch das passende IDE-Plugin für das Arbeitsverzeichnis auf und öffnet damit:

- **Visual Studio ist explizit kompatibel**, wenn eine `.sln`-/`.slnx`-Datei auf oberster Ebene des Arbeitsverzeichnisses gefunden wird:
  - **Bei genau einer Solution:** Öffnet diese direkt ohne Dialog.
  - **Bei mehreren Solutions:** Zeigt einen Auswahl-Dialog mit allen gefundenen Solutions (alphabetisch sortiert nach Dateinamen). Der Benutzer wählt die gewünschte Solution und bestätigt mit OK. Bei Abbruch wird keine Solution geöffnet.
- **Ohne gefundene Solution (oder wenn das Visual-Studio-Plugin deaktiviert ist):** Fällt automatisch auf das erste aktive Fallback-Plugin zurück (standardmäßig Visual Studio Code), das das Arbeitsverzeichnis direkt öffnet.
- Der Button ist bereits aktiv, sobald ein gültiges, vorhandenes Arbeitsverzeichnis existiert — unabhängig davon, ob eine `.sln`-Datei vorhanden ist, da das konkrete Plugin erst beim Klick aufgelöst wird.

Welche IDE-Plugins aktiv sind und in welcher Reihenfolge sie geprüft werden, steuern Sie über **Einstellungen → Plugins → Integrierte Entwicklungsumgebungen (IDE)**.
Solutions werden nur auf der obersten Verzeichnisebene des Arbeitsverzeichnisses gesucht (nicht rekursiv).

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

1. Eine Aufgabe ohne `*.sln`-Datei, aber mit vorhandenem Arbeitsverzeichnis öffnen.
2. Im Ribbon auf „IDE öffnen" klicken.
3. Visual Studio Code öffnet automatisch das Arbeitsverzeichnis (Fallback-Plugin, standardmäßig aktiv), sofern `code`/`code.cmd` über `PATH` oder eine typische Windows-Installation gefunden wird.

## Arbeitsverzeichnis-Auflösung

Beide Aktionen berücksichtigen das konfigurierte Arbeitsunterverzeichnis (`RepositoryStartKonfiguration.WorkingDirectoryRelativePath`) des Repositories:

- **Mit konfiguriertem Unterverzeichnis (z. B. `src/backend`):** 
  - „Arbeitsverzeichnis öffnen" zeigt das Unterverzeichnis an (z. B. `C:\path\repo\src\backend`), nicht den Repository-Root.
  - „IDE öffnen" durchsucht das Unterverzeichnis nach Solutions (nicht den Root).
  - Falls Solutions im Unterverzeichnis existieren, werden diese gefunden und geöffnet; Solutions im Root werden ignoriert.
  - Das Unterverzeichnis wird auch als Arbeitsverzeichnis an das aufgelöste Fallback-Plugin (standardmäßig Visual Studio Code) übergeben.

- **Ohne konfiguriertes Unterverzeichnis (oder `.` = Root):**
  - Beide Aktionen verwenden den Repository-Root — das Verhalten ist identisch mit Repositories ohne Unterverzeichnis-Konfiguration.

Diese Funktionalität unterstützt Mono-Repos mit mehreren, räumlich getrennten Subprojekten: Ist das Arbeitsverzeichnis auf `backend/` konfiguriert, werden nur Solutions im `backend/`-Ordner berücksichtigt, und das Öffnen des Dateiexplorers zeigt ausschließlich diesen Ordner.

## Einschränkungen

- Die Anwendung prüft nicht, ob die IDE (z. B. Visual Studio) auf dem System installiert ist. Ist sie nicht vorhanden oder kein Betriebssystem-Handler für `.sln`-Dateien registriert, wird eine Fehlermeldung angezeigt.
- Ist Visual Studio Code als Fallback-Plugin aktiv, aber nicht auffindbar, zeigt die Aufgabendetailansicht einen Hinweis statt einen Prozess zu starten.
- Solutions werden nur auf der obersten Verzeichnisebene des (aufgelösten) Arbeitsverzeichnisses erkannt (keine rekursive Suche in Unterverzeichnissen).
- Das Arbeitsverzeichnis muss auf der Festplatte vorhanden sein. Ist der konfigurierte Pfad gelöscht oder nicht erreichbar, werden die Buttons inaktiv und eine Fehlermeldung angezeigt.
