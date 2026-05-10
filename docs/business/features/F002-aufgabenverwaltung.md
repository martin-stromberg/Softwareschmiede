# F002 – Aufgabenverwaltung

## Einleitung

Die Aufgabenverwaltung ist das Herzstück der täglichen Arbeit mit der Softwareschmiede.
Hier legen Sie fest, was die KI umsetzen soll.
Eine Aufgabe beschreibt genau ein Vorhaben – zum Beispiel das Hinzufügen einer neuen Funktion oder das Beheben eines Fehlers.
Jede Aufgabe durchläuft klare Stadien vom Anlegen bis zum Abschluss.
So behalten Sie den Überblick, was gerade passiert und was bereits erledigt ist.

![Aufgabenübersicht](../images/F002-aufgabenübersicht.png)

---

## Wer nutzt es?

**Softwareentwickler**, die ihrer KI konkrete Arbeitsaufträge erteilen möchten.
Sie formulieren die Aufgabe und die Softwareschmiede übergibt sie an die KI.
Der gesamte Verlauf – von der Beschreibung bis zum Ergebnis – bleibt gespeichert.

---

## Schritt-für-Schritt-Anleitung

### Neue Aufgabe anlegen (frei formuliert)

1. Sie öffnen das gewünschte Projekt im Dashboard.
2. Sie klicken auf **Neue Aufgabe**.
3. Sie geben einen **Titel** für die Aufgabe ein (z.B. „Passwort-vergessen-Funktion hinzufügen").
4. Sie schreiben im Feld **Beschreibung** genau, was umgesetzt werden soll.
5. Sie wählen ein **Agentenpaket** aus der Liste aus.
6. Sie klicken auf **Aufgabe anlegen**. Die Aufgabe erscheint mit dem Status **Offen**.

### Neue Aufgabe aus GitHub-Aufgabensystem übernehmen

1. Sie öffnen das gewünschte Projekt.
2. Sie klicken auf **Aus GitHub-Aufgabe erstellen**.
3. Die Softwareschmiede zeigt eine Liste der offenen Aufgaben aus Ihrer GitHub-Ablage.
4. Sie wählen die gewünschte Aufgabe aus.
5. Titel und Beschreibung werden automatisch übernommen.
6. Sie wählen ein **Agentenpaket** und klicken auf **Aufgabe anlegen**.

### Status einer Aufgabe verfolgen

Jede Aufgabe zeigt ihren aktuellen Status:

| Status | Bedeutung |
|--------|-----------|
| **Offen** | Die Aufgabe ist angelegt, die KI hat noch nicht begonnen. |
| **In Bearbeitung** | Die KI arbeitet gerade. Eine Arbeitskopie wurde erstellt. |
| **KI aktiv** | Die KI schreibt gerade Code und sendet Ausgaben. |
| **Abgeschlossen** | Die Aufgabe ist erledigt, ein Vorschlag wurde an GitHub übermittelt. |
| **Fehlgeschlagen** | Ein Fehler ist aufgetreten. Details finden Sie im Protokoll. |
| **Abgebrochen** | Die Aufgabe wurde manuell abgebrochen. |

---

## Beispiel

Sie bemerken, dass die Anmeldeseite Ihrer Software einen Fehler hat.
Sie öffnen das Projekt „Buchungssoftware Büro" und klicken auf **Neue Aufgabe**.
Als Titel tragen Sie ein: „Fehler bei der Anmeldung beheben".
In der Beschreibung erklären Sie: „Beim Anmelden mit einem langen Passwort erscheint eine Fehlermeldung. Das soll behoben werden."
Sie wählen das Agentenpaket „Standard-Entwicklung" und klicken auf **Aufgabe anlegen**.
Die Aufgabe erscheint sofort in der Liste mit dem Status **Offen**.

---

## Was passiert im Hintergrund?

Wenn Sie eine Aufgabe anlegen, speichert die Softwareschmiede alle Angaben.
Sobald Sie die KI starten, wird eine separate Arbeitskopie des Codes erstellt.
So bleibt der ursprüngliche Code unberührt, bis Sie die Änderungen ausdrücklich freigeben.
Der Status aktualisiert sich automatisch – Sie müssen nichts manuell umschalten.

---

## Häufige Fragen (FAQ)

**Wie detailliert muss die Aufgabenbeschreibung sein?**
Je genauer Sie beschreiben, was gewünscht ist, desto besser arbeitet die KI. Beschreiben Sie das erwartete Ergebnis, nicht den Lösungsweg.

**Kann ich eine Aufgabe nachträglich bearbeiten?**
Titel und Beschreibung können Sie ändern, solange die Aufgabe noch den Status **Offen** hat. Laufende Aufgaben können nicht mehr verändert werden.

**Was passiert, wenn die Aufgabe fehlschlägt?**
Die Aufgabe wechselt in den Status **Fehlgeschlagen**. Das Protokoll zeigt den genauen Fehler. Sie können die Aufgabe korrigieren und neu starten.

**Kann ich mehrere Aufgaben gleichzeitig starten?**
Ja, pro Projekt können mehrere Aufgaben gleichzeitig laufen. Jede bekommt ihre eigene Arbeitskopie.

**Bleiben abgeschlossene Aufgaben sichtbar?**
Ja, sie sind weiterhin im Projekt einsehbar und enthalten das vollständige Protokoll.

---

## Verwandte Funktionen

- [F001 – Projektverwaltung](./F001-projektverwaltung.md) – Projekte anlegen und verwalten
- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – KI starten und Ausgabe beobachten
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md) – Den Verlauf einer Aufgabe einsehen
- [F006 – Aufgabe abschließen](./F006-aufgabe-abschliessen.md) – Ergebnis freigeben und Aufgabe beenden
- [F007 – Aufgabe abbrechen](./F007-aufgabe-abbrechen.md) – Aufgabe ohne Speichern beenden
- [F009 – Arbeitsverzeichnis konfigurieren](./F009-arbeitsverzeichnis-konfigurieren.md) – Basispfad der lokalen Klone festlegen
- [Zurück zur Übersicht](../features.md)
