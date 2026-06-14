← [Zurück zur Übersicht](index.md)

# Projekte — Ablauf für Anwender

## Projekt erstellen

### Voraussetzungen
- Sie befinden sich auf der Projektübersicht (Dashboard).

### Schritte

1. Klicken Sie auf den „Neu"-Button im Dashboard. Die Projektdetailansicht öffnet sich im Anlage-Modus.
2. Geben Sie einen Projektnamen in das Textfeld ein (Pflichtfeld).
3. Optional: Geben Sie eine Beschreibung ein.
4. Klicken Sie auf den „Speichern"-Button im Ribbon-Menü (Gruppe „Projekt").

### Ergebnis
Das Projekt wird erstellt, die Projektliste aktualisiert sich, und das neue Projekt erscheint im Dashboard und in der Projektübersicht.

> **Hinweis:** Der Projektname ist erforderlich. Der „Speichern"-Button ist deaktiviert, solange das Feld leer ist.

## Projekt bearbeiten

### Voraussetzungen
- Sie haben ein Projekt geöffnet (Projektdetailansicht sichtbar).

### Schritte

1. Bearbeiten Sie den Projektnamen oder die Beschreibung in der Projekt-Kachel.
2. Klicken Sie auf den „Speichern"-Button im Ribbon-Menü (Gruppe „Projekt").

### Ergebnis
Die Änderungen werden gespeichert. Die Ansicht aktualisiert sich mit den neuen Daten.

> **Hinweis:** Sie können das Projektsymbol (📁) nicht ändern; es ist fest definiert.

## Projekt löschen

### Voraussetzungen
- Sie haben ein Projekt geöffnet (Projektdetailansicht sichtbar).

### Schritte

1. Klicken Sie auf den „Löschen"-Button im Ribbon-Menü (Gruppe „Projekt").
2. Eine Bestätigungsabfrage erscheint: „Soll das Projekt wirklich gelöscht werden?"
3. Klicken Sie auf „Ja" zur Bestätigung oder „Nein" zum Abbruch.

### Ergebnis
Das Projekt wird gelöscht, die Ansicht kehrt zur Projektübersicht zurück, und die Projektliste aktualisiert sich.

> **Warnung:** Diese Aktion kann nicht rückgängig gemacht werden. Alle zugehörigen Aufgaben werden ebenfalls gelöscht.

## Repository zuweisen

### Voraussetzungen
- Sie haben ein Projekt geöffnet (Projektdetailansicht sichtbar).
- Es existiert mindestens ein Git-Repository im System (z.B. aus einer früheren Zuweisung).

### Schritte

1. Klicken Sie auf den „Zuweisen"-Button im Ribbon-Menü (Gruppe „Repository").
2. Der Repository-Zuweisungs-Dialog öffnet sich und zeigt alle verfügbaren Repositories.
3. Wählen Sie ein Repository aus der Liste durch Anklicken.
4. Klicken Sie auf den „Zuweisen"-Button im Dialog.

### Ergebnis
Das Repository wird dem Projekt zugeordnet. Der Dialog schließt sich, und das Repository steht für Aufgabenstart zur Verfügung.

> **Hinweis:** Sie können mehrere Repositories einem Projekt zuordnen. Der „Zuweisen"-Button ist deaktiviert, solange kein Repository ausgewählt ist.

## Repository öffnen

### Voraussetzungen
- Sie haben ein Projekt geöffnet (Projektdetailansicht sichtbar).
- Das Projekt hat mindestens ein zugewiesenes Repository.

### Schritte

1. Wählen Sie ein Repository in der Aufgaben-Liste oder aus dem Projekt-Kontext aus (falls angezeigt).
2. Klicken Sie auf den „Öffnen"-Button im Ribbon-Menü (Gruppe „Repository").

### Ergebnis
Das Repository wird im Standard-Webbrowser geöffnet, z.B. auf GitHub, GitLab oder einer anderen Git-Hosting-Plattform.

> **Hinweis:** Der „Öffnen"-Button ist deaktiviert, wenn kein Repository ausgewählt ist. Die URL wird aus der Repository-Konfiguration gelesen.

## Aufgabe erstellen

### Voraussetzungen
- Sie haben ein Projekt geöffnet (Projektdetailansicht sichtbar).

### Schritte

1. Klicken Sie auf den „Neu"-Button im Ribbon-Menü (Gruppe „Aufgaben").
2. Eine neue Aufgabe wird erstellt und deren Detailansicht öffnet sich sofort.
3. Füllen Sie die Aufgabendaten aus (Titel, Beschreibung, etc.).

### Ergebnis
Die neue Aufgabe wird in der Aufgaben-Kachel angezeigt und kann bearbeitet werden.

> **Hinweis:** Die Aufgabe wird mit dem Status „Neu" erstellt und gehört automatisch zum aktuellen Projekt.

## Aufgabenliste filtern

### Voraussetzungen
- Sie haben ein Projekt mit Aufgaben geöffnet (Projektdetailansicht sichtbar).

### Schritte

1. Klicken Sie auf den „Filter"-Button im Ribbon-Menü (Gruppe „Aufgaben").
2. Ein Overlay-Panel erscheint mit drei Filteroptionen (Radio-Buttons):
   - **Alle**: Zeigt alle Aufgaben des Projekts (Standard)
   - **Aktiv**: Zeigt nur Aufgaben mit Status „Neu", „ArbeitsverzeichnisEingerichtet", „Gestartet", „InArbeit" oder „Wartend"
   - **Archiviert**: Zeigt nur Aufgaben mit Status „Archiviert"
3. Wählen Sie einen Filter durch Anklicken der entsprechenden Radio-Button.
4. Klicken Sie erneut auf den „Filter"-Button, um das Panel zu schließen.

### Ergebnis
Der gewählte Filter-Status bleibt aktiv und die Aufgabenliste wird entsprechend angezeigt. Der Filter wird nicht persistiert und wird auf „Alle" zurückgesetzt, wenn Sie die Projektdetailansicht erneut öffnen.

> **Hinweis:** Der Filter ist ein Anzeigefilter; alle Aufgaben werden geladen, aber nur die gefilterten werden angezeigt. Die Daten werden nicht gelöscht, nur ausgeblendet.

## Aufgabe öffnen

### Voraussetzungen
- Sie haben ein Projekt mit Aufgaben geöffnet (Projektdetailansicht sichtbar).

### Schritte

1. Doppelklicken Sie auf eine Aufgabe in der Aufgaben-Kachel, oder
2. Wählen Sie eine Aufgabe aus und drücken Sie Enter.

### Ergebnis
Die Aufgabendetailansicht öffnet sich und zeigt alle Informationen und Optionen für die Aufgabe.

## Navigation

### Zurück zur Projektübersicht

1. Klicken Sie auf den „Zurück"-Button im Ribbon-Menü (Gruppe „Navigation").

### Ergebnis
Sie kehren zur Projektübersicht (Dashboard) zurück. Ungespeicherte Änderungen in der Projektdetailansicht gehen verloren.

> **Hinweis:** Speichern Sie Ihre Änderungen mit dem „Speichern"-Button, bevor Sie die Ansicht verlassen.
