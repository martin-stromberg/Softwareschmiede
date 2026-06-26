# Projekte — Beschreibung

## Zweck

Ein Projekt bündelt einen thematischen Entwicklungsbereich. Es hält mindestens ein Git-Repository und beliebig viele Aufgaben. Aus der Projektdetailansicht heraus werden neue Aufgaben angelegt und bestehende verwaltet.

## Funktionsweise

Ein Projekt hat einen Namen, eine optionale Beschreibung und einen Status (`Aktiv` oder `Archiviert`). Nur aktive Projekte erscheinen im Dashboard und können Aufgaben erhalten.

### Projektdetailansicht

Die Projektdetailansicht ist das zentrale Bearbeitungswerkzeug für Projekte und ist in ein Office-ähnliches Ribbon-Menü und zwei Inhalts-Kacheln unterteilt.

**Ribbon-Menü** mit vier Aktionsgruppen:

**Navigation**
- **Zurück** (←): Kehrt zur Projektübersicht zurück.

**Projekt**
- **Speichern** (💾): Speichert Änderungen am Projektnamen und der Beschreibung. Beim Anlegen eines neuen Projekts wird es hier erstmalig in der Datenbank erstellt und die Ansicht schaltet in den Bearbeitungsmodus.
- **Löschen** (🗑): Löscht das Projekt nach Bestätigungsabfrage. Diese Aktion kann nicht rückgängig gemacht werden und löscht auch alle zugeordneten Aufgaben.

**Aufgaben**
- **Neue Aufgabe** (➕): Erstellt sofort eine neue Aufgabe für das Projekt mit dem Standardtitel "Neue Aufgabe" und öffnet ihre Detailansicht.
- **Filter** (🔽): Zeigt/verbirgt ein Overlay-Panel zum Filtern der Aufgabenliste nach Status (Alle, Aktiv, Archiviert) über Radio-Buttons.

**Repository**
- **Zuweisen** (🔗): Öffnet einen Dialog zur Auswahl und Zuweisung eines vorhandenen Git-Repository zum Projekt.
- **Öffnen** (🌐): Öffnet das aktuell ausgewählte Repository im Standard-Webbrowser (Funktion deaktiviert ohne Auswahl).

**Inhalts-Kacheln:**
- **Projekt-Kachel** (oben, immer sichtbar): Zeigt das Projektsymbol (📁), einen bearbeitbaren Projektnamen und eine bearbeitbare Projektbeschreibung. Der Name ist Pflichtfeld und das „Speichern"-Button wird deaktiviert, solange das Feld leer ist.
- **Aufgaben-Kachel** (unten, nur bei bestehendem Projekt): Listet alle Aufgaben des Projekts mit Titel und Status auf. Doppelklick öffnet die Aufgabendetailansicht inline; das Filter-Overlay ermöglicht die Einschränkung auf aktive oder archivierte Aufgaben.

### Repositories

Einem Projekt lassen sich ein oder mehrere Git-Repositories zuordnen. Die Repository-Zuweisung erfolgt über einen Dialog, der eine explizite Auswahl des SCM-Plugins (Source Code Management) ermöglicht:

1. **SCM-Plugin-Auswahl:** Nach Öffnen des Dialogs wählt der Benutzer das gewünschte SCM-Plugin aus einer Dropdown-Liste (z.B. „GitHub", „LocalDirectory Plugin").
2. **Repository-Filterung:** Nach Plugin-Auswahl werden nur Repositories dieser Quelle angezeigt. Die Liste wird automatisch gefiltert nach dem Plugin-Typ und alphabetisch sortiert.
3. **Fehlerbehandlung:** Falls keine SCM-Plugins installiert sind, zeigt der Dialog ein Hilfe-Panel statt der Eingabekomponenten. Der Dialog ist dann nicht funktional.

Jedes Repository verweist auf einen Plugin-Typ (z.B. `SourceCodeManagement` oder `DevelopmentAutomation`) sowie die Repository-URL. Beim Starten einer Aufgabe wird das passende Repository automatisch ermittelt.

Repositories können eine `RepositoryStartKonfiguration` enthalten, die beim Starten einer Aufgabe ein Startskript ausführt (z.B. `npm install`). Das Skript kann auch nachträglich manuell ausgelöst werden.

## Beispiele

- Projekt „Backend-API" mit einem GitHub-Repository und mehreren Aufgaben für Features und Bugfixes. Der Projektname und die Beschreibung werden in der Detailansicht angepasst, Repositories über den „Zuweisen"-Button hinzugefügt.
- Projekt „Lokale Tools" mit einem `LocalDirectoryPlugin`-Repository, das direkt auf ein Verzeichnis zeigt.

### Repository-Suggestions (Projektübersicht)

Auf der Projektübersichtsseite erscheint unterhalb der Projektkacheln ein **Suggestions-Panel** mit unzugeordneten Repositories. Dieses Panel zeigt automatisch alle Repositories an, die aus verfügbaren SCM-Plugins stammen, aber noch nicht einem Projekt zugeordnet wurden.

**Funktionsweise:**
- Die Liste wird sortiert nach dem Datum der letzten Änderung (neueste zuerst), um aktive Repositories prominent anzuzeigen.
- Jeder Eintrag zeigt den Repository-Namen und eine relative Zeitangabe der letzten Änderung (z.B. "vor 2 Stunden").
- Ein Doppelklick auf einen Eintrag erstellt automatisch ein neues Projekt mit dem Repository-Namen und ordnet das Repository zu.
- Das Panel wird beim Laden der Projektübersicht und beim Zurücknavigieren von der Projektdetailansicht aktualisiert, sodass neu zugeordnete Repositories sofort verschwinden.

**Fehlerbehandlung:**
- Falls ein SCM-Plugin bei der Abfrage seiner Repositories fehlschlägt, wird dieses Plugin übersprungen; andere Plugins werden weiterhin abgefragt. Ein Fehler wird im Anwendungsprotokoll vermerkt.
- Falls keine Repositories vorhanden sind oder alle bereits zugeordnet wurden, bleibt das Panel leer (mit entsprechender Überschrift).

## Einschränkungen

- Archivierte Projekte erscheinen nicht im Dashboard, sind aber weiterhin in der Projektliste sichtbar.
- Hat ein Projekt mehrere aktive Repositories und eine Aufgabe referenziert keines davon eindeutig, wird der Entwicklungsprozessstart verweigert.
- Der Aufgabenfilter ist nur ein Anzeigefilter und beeinflusst nicht die Datenbankabfrage; alle Aufgaben werden geladen.
- Das Suggestions-Panel zeigt keine Repositories an, wenn alle verfügbaren Repositories bereits zugeordnet wurden.
