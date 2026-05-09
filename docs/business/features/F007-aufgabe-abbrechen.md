# F007 – Aufgabe abbrechen

## Einleitung

Manchmal muss eine Aufgabe vorzeitig gestoppt werden – weil die Aufgabenbeschreibung unklar war, weil die KI in eine falsche Richtung gearbeitet hat oder weil sich das Vorhaben geändert hat.
Das Abbrechen einer Aufgabe stoppt die KI sofort und macht alle lokalen Änderungen rückgängig.
Es werden keine Änderungen in Ihre GitHub-Ablage übertragen.
Das Protokoll der bisherigen Arbeit bleibt vollständig erhalten.
Die Aufgabe kann danach mit einer überarbeiteten Beschreibung neu gestartet werden.

![Aufgabe abbrechen](../images/F007-abbrechen.png)

---

## Wer nutzt es?

**Softwareentwickler**, die merken, dass eine laufende KI-Bearbeitung nicht das gewünschte Ergebnis liefert.
Das Abbrechen ist immer sicher – es gelangt nichts in die GitHub-Ablage, was nicht gewollt ist.

---

## Schritt-für-Schritt-Anleitung

### Laufende Aufgabe abbrechen

1. Sie öffnen die laufende Aufgabe.
2. Sie klicken auf die Schaltfläche **Aufgabe abbrechen**.
3. Ein Bestätigungsdialog erscheint mit dem Hinweis, dass alle lokalen Änderungen verloren gehen.
4. Sie klicken auf **Ja, abbrechen**.
5. Die KI wird sofort gestoppt.
6. Die lokale Arbeitskopie wird vollständig gelöscht.
7. Die Aufgabe wechselt in den Status **Abgebrochen**.

### Offene Aufgabe (noch nicht gestartet) löschen

1. Sie öffnen die Aufgabe mit dem Status **Offen**.
2. Sie klicken auf **Aufgabe löschen**.
3. Sie bestätigen die Nachfrage.
4. Die Aufgabe wird entfernt. Es gibt keine Arbeitskopie, die gelöscht werden müsste.

### Abgebrochene Aufgabe neu starten

1. Sie öffnen die abgebrochene Aufgabe.
2. Sie klicken auf **Aufgabenbeschreibung bearbeiten** und passen den Text an.
3. Sie klicken auf **KI starten**.
4. Die Softwareschmiede erstellt eine neue Arbeitskopie und beginnt von vorne.

---

## Beispiel

Sie haben die Aufgabe „Neue Startseite gestalten" gestartet.
Die KI beginnt zu arbeiten, aber Sie sehen in der Ausgabe, dass sie ein völlig anderes Design umsetzt, als Sie sich vorgestellt haben.
Sie klicken auf **Aufgabe abbrechen** und bestätigen die Nachfrage.
Die KI stoppt sofort. Alle Änderungen auf Ihrem Rechner werden gelöscht.
In der GitHub-Ablage ist nichts verändert worden.
Sie öffnen die Aufgabe erneut, ergänzen die Beschreibung mit konkreten Gestaltungshinweisen und starten die KI neu.

---

## Was passiert im Hintergrund?

Beim Abbrechen sendet die Softwareschmiede ein Stoppsignal an die KI.
Die lokale Arbeitskopie – also alle Dateien, die die KI auf Ihrem Rechner angelegt oder verändert hat – wird vollständig entfernt.
In Ihrer GitHub-Ablage bleibt alles unverändert, denn es wurden keine Daten hochgeladen.
Das Protokoll der bisherigen Arbeit bleibt gespeichert, damit Sie nachvollziehen können, was bis zum Abbruch passiert ist.

---

## Häufige Fragen (FAQ)

**Kann ich eine abgebrochene Aufgabe nicht doch noch abschließen?**
Nein. Sobald eine Aufgabe abgebrochen wurde, ist die lokale Arbeitskopie gelöscht. Es gibt nichts mehr zum Hochladen. Sie müssen die Aufgabe neu starten.

**Verliere ich meine Aufgabenbeschreibung beim Abbrechen?**
Nein. Titel, Beschreibung, Agentenpaket-Auswahl und das Protokoll bleiben erhalten. Nur die lokalen Code-Änderungen der KI werden gelöscht.

**Was passiert, wenn ich versehentlich auf „Abbrechen" klicke?**
Der Bestätigungsdialog schützt Sie vor versehentlichem Abbrechen. Erst nach Ihrer ausdrücklichen Bestätigung wird die Aufgabe abgebrochen.

**Kann ich eine Aufgabe abbrechen, die gerade im Status „In Bearbeitung" ist, aber die KI noch nicht gestartet wurde?**
Ja. Sie können in jedem aktiven Status abbrechen. Wenn die KI noch nicht gestartet wurde, entfällt lediglich das Löschen der Arbeitskopie.

**Wird die GitHub-Ablage durch das Abbrechen verändert?**
Nein. Das Abbrechen hat keinerlei Auswirkungen auf Ihre GitHub-Ablage.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – KI starten und beobachten
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md) – Protokoll nach dem Abbruch einsehen
- [F006 – Aufgabe abschließen](./F006-aufgabe-abschliessen.md) – Regulärer Abschluss einer Aufgabe
- [F002 – Aufgabenverwaltung](./F002-aufgabenverwaltung.md) – Neue Aufgabe nach dem Abbruch anlegen
- [Zurück zur Übersicht](../features.md)
