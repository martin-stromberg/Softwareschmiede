# F002 – Aufgabenverwaltung

## Einleitung

In der Aufgabenverwaltung legen Sie fest, was die KI umsetzen soll.  
Jede Aufgabe beschreibt ein klares Ziel in einem Projekt.  
Sie können Aufgaben frei formulieren oder aus einer bestehenden GitHub-Issue übernehmen.  
So startet die Umsetzung immer mit einem nachvollziehbaren Auftrag.

![Aufgabenübersicht](../images/F002-aufgabenübersicht.png)

---

## Wer nutzt es?

Diese Funktion nutzen Fachanwender, die tägliche Arbeitsaufträge vorbereiten.  
Neue Mitarbeitende nutzen sie, um strukturiert in bestehende Projekte einzusteigen.

---

## Schritt-für-Schritt-Anleitung

1. Sie öffnen ein Projekt und klicken auf **Neue Aufgabe**.
2. Optional wählen Sie oben eine vorhandene Issue aus.
3. Sie prüfen oder ergänzen **Titel** und **Anforderungsbeschreibung**.
4. Sie klicken auf **Aufgabe anlegen**.
5. Die Aufgabe öffnet sich direkt in der Detailansicht.
6. Unter dem Titel sehen Sie dort den Klartext **„Projekt: <Name>“** (oder **„Projekt: ohne projekt“** bei fehlender Zuordnung).
7. Dort starten Sie später über **Entwicklung starten** den Lauf.
8. Wenn Sie die Aufgabe nie starten möchten, verwenden Sie auf der Detailseite direkt **Verwerfen** und wählen anschließend **Archivieren** oder **Dauerhaft löschen**.

---

## Beispiel

In Ihrem Projekt gibt es eine offene Issue für eine fehlende Filterfunktion.  
Sie wählen die Issue in der Aufgabenmaske aus.  
Titel und Beschreibung werden automatisch übernommen.  
Sie klicken auf **Aufgabe anlegen** und starten danach die Umsetzung.

---

## Was passiert im Hintergrund?

Die Anwendung speichert Titel, Beschreibung und die Zuordnung zum Projekt.  
Wenn Sie eine Issue wählen, übernimmt sie die vorhandenen Texte als Startpunkt.  
Beim Start der Entwicklung wird später ein eigener Arbeitsbereich für diese Aufgabe genutzt.
Zusätzlich bleibt die Issue-Verknüpfung erhalten, damit Branch und Pull Request automatisch zur richtigen Issue passen.
Auf der Detailseite wird der Projektname als reiner Text direkt unter dem Aufgabentitel angezeigt.

---

## Häufige Fragen (FAQ)

**Muss ich eine Issue auswählen?**  
Nein. Die Auswahl ist optional.

**Warum sehe ich keine Issue-Liste?**  
Dann konnte die Liste nicht geladen werden oder es gibt keine offenen Issues.

**Kann ich Titel und Beschreibung später ändern?**  
Ja, solange die Aufgabe noch nicht gestartet wurde.

**Wird eine neue Aufgabe sofort ausgeführt?**  
Nein. Sie starten sie später in der Detailansicht.

**Kann ich eine offene Aufgabe direkt archivieren oder löschen?**
Ja. Dafür gibt es auf der Detailseite die Aktion **Verwerfen** mit den Pfaden **Archivieren** oder **Dauerhaft löschen**.

---

## Verwandte Funktionen

- [F001 – Projektverwaltung](./F001-projektverwaltung.md)
- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md)
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md)
- [F006 – Aufgabe abschließen](./F006-aufgabe-abschliessen.md)
- [F007 – Aufgabe abbrechen](./F007-aufgabe-abbrechen.md)
- [F019 – Issue-, Branch- und PR-Verknüpfung](./F019-issue-branch-pr-verknuepfung.md)
- [Zurück zur Übersicht](../features.md)
