# Projekte — Beschreibung

## Zweck

Ein Projekt bündelt einen thematischen Entwicklungsbereich. Es hält mindestens ein Git-Repository und beliebig viele Aufgaben. Aus der Projektdetailansicht heraus werden neue Aufgaben angelegt und bestehende verwaltet.

## Funktionsweise

Ein Projekt hat einen Namen, eine optionale Beschreibung und einen Status (`Aktiv` oder `Archiviert`). Nur aktive Projekte erscheinen im Dashboard und können Aufgaben erhalten.

### Projektdetailansicht

Die Projektdetailansicht zeigt ein Ribbon-Menü mit Aktionen, gruppiert nach Funktionsbereichen:

**Navigation**
- **Zurück**: Kehrt zur Projektübersicht zurück.

**Projekt**
- **Speichern**: Speichert Änderungen am Projektnamen und der Beschreibung. Beim Anlegen eines neuen Projekts wird es hier erstmalig erstellt.
- **Löschen**: Löscht das Projekt nach Bestätigungsabfrage.

**Aufgaben**
- **Neu**: Erstellt eine neue Aufgabe für das Projekt und öffnet deren Detailansicht.
- **Filter**: Zeigt ein Overlay-Panel zum Filtern der Aufgabenliste nach Status (Alle, Aktiv, Archiviert).

**Repository**
- **Zuweisen**: Öffnet einen Dialog zur Auswahl und Zuweisung eines Git-Repository zum Projekt.
- **Öffnen**: Öffnet das ausgewählte Repository im Standard-Webbrowser.

Die Ansicht ist in zwei Kacheln organisiert:
- **Projekt-Kachel**: Zeigt und ermöglicht Bearbeitung von Projektsymbol (📁), Name und Beschreibung. Der Name ist Pflichtfeld für das Speichern.
- **Aufgaben-Kachel**: Listet alle Aufgaben des Projekts mit Status auf. Doppelklick auf eine Aufgabe öffnet deren Detailansicht.

### Repositories

Einem Projekt lassen sich ein oder mehrere Git-Repositories zuordnen. Jedes Repository verweist auf einen Plugin-Typ (z.B. `GitHub` oder `LocalDirectoryPlugin`) sowie die Repository-URL. Beim Starten einer Aufgabe wird das passende Repository automatisch ermittelt.

Repositories können eine `RepositoryStartKonfiguration` enthalten, die beim Starten einer Aufgabe ein Startskript ausführt (z.B. `npm install`). Das Skript kann auch nachträglich manuell ausgelöst werden.

## Beispiele

- Projekt „Backend-API" mit einem GitHub-Repository und mehreren Aufgaben für Features und Bugfixes. Der Projektname und die Beschreibung werden in der Detailansicht angepasst, Repositories über den „Zuweisen"-Button hinzugefügt.
- Projekt „Lokale Tools" mit einem `LocalDirectoryPlugin`-Repository, das direkt auf ein Verzeichnis zeigt.

## Einschränkungen

- Archivierte Projekte erscheinen nicht im Dashboard, sind aber weiterhin in der Projektliste sichtbar.
- Hat ein Projekt mehrere aktive Repositories und eine Aufgabe referenziert keines davon eindeutig, wird der Entwicklungsprozessstart verweigert.
- Der Aufgabenfilter ist nur ein Anzeigefilter und beeinflusst nicht die Datenbankabfrage; alle Aufgaben werden geladen.
