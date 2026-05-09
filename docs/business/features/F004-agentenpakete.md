# F004 – Agentenpakete

## Einleitung

Agentenpakete bestimmen, wie die KI bei einer Aufgabe vorgeht.
Sie können sich ein Agentenpaket wie ein Regelwerk oder eine Stellenbeschreibung für die KI vorstellen.
Darin steht, welche Grundsätze die KI befolgt, welche Prüfungen sie durchführt und welchen Stil sie beim Schreiben von Code einhält.
Verschiedene Agentenpakete eignen sich für verschiedene Arten von Vorhaben.
Sie wählen vor dem Start einer Aufgabe das passende Paket aus.

![Agentenpakete-Übersicht](../images/F004-agentenpakete.png)

---

## Wer nutzt es?

**Softwareentwickler**, die der KI ein bestimmtes Arbeitsverhalten vorgeben möchten.
Ein Agentenpaket sorgt dafür, dass die KI bei jedem Projekt die gleichen Regeln einhält – ohne dass man sie jedes Mal neu erklären muss.

---

## Schritt-für-Schritt-Anleitung

### Vorhandene Agentenpakete ansehen

1. Sie klicken in der Navigation auf **Agentenpakete**.
2. Sie sehen eine Liste aller verfügbaren Pakete mit Name und Beschreibung.
3. Sie klicken auf ein Paket, um die enthaltenen Anweisungen einzusehen.

### Neues Agentenpaket anlegen

1. Sie klicken auf **Neues Paket**.
2. Sie geben einen **Namen** für das Paket ein (z.B. „Qualitätsgesicherte Entwicklung").
3. Sie schreiben eine kurze **Beschreibung**, wofür dieses Paket gedacht ist.
4. Sie fügen die gewünschten **Anweisungsdateien** hinzu. Das sind Textdateien mit Regeln für die KI.
5. Sie klicken auf **Speichern**. Das Paket steht ab sofort für alle Aufgaben zur Verfügung.

![Neues Agentenpaket anlegen](../images/F004-neues-paket.png)

### Agentenpaket bearbeiten

1. Sie öffnen das gewünschte Paket aus der Liste.
2. Sie klicken auf **Bearbeiten** (Stift-Symbol).
3. Sie ändern Name, Beschreibung oder fügen Anweisungsdateien hinzu oder entfernen sie.
4. Sie speichern mit **Speichern**.

### Agentenpaket löschen

1. Sie öffnen das gewünschte Paket.
2. Sie klicken auf **Löschen** und bestätigen die Nachfrage.

> ⚠️ **Hinweis:** Laufende Aufgaben, die dieses Paket verwenden, sind davon nicht betroffen. Neue Aufgaben können das gelöschte Paket nicht mehr auswählen.

### Agentenpaket für eine Aufgabe auswählen

1. Sie legen eine neue Aufgabe an oder öffnen eine offene Aufgabe.
2. Im Feld **Agentenpaket** wählen Sie das gewünschte Paket aus der Auswahlliste.
3. Die Auswahl wird gespeichert. Die KI nutzt dieses Paket, sobald Sie auf **KI starten** klicken.

---

## Beispiel

Sie möchten sicherstellen, dass die KI bei jeder Aufgabe automatisch Tests schreibt.
Sie öffnen den Bereich **Agentenpakete** und klicken auf **Neues Paket**.
Sie nennen es „Mit automatischen Tests" und beschreiben: „Die KI schreibt zu jeder neuen Funktion auch passende Prüfroutinen."
Sie laden die entsprechende Anweisungsdatei hoch und klicken auf **Speichern**.
Beim nächsten Anlegen einer Aufgabe wählen Sie dieses Paket aus.
Die KI hält sich ab sofort bei dieser Aufgabe an das neue Regelwerk.

---

## Was passiert im Hintergrund?

Wenn Sie ein Agentenpaket auswählen, werden die darin enthaltenen Anweisungsdateien beim Start der KI automatisch mitgegeben.
Die KI liest diese Anweisungen und richtet ihr Verhalten danach aus.
Ohne ein Agentenpaket arbeitet die KI nach ihren Standardvorgaben.

---

## Häufige Fragen (FAQ)

**Muss ich ein Agentenpaket auswählen?**
Nein. Wenn Sie kein Paket auswählen, arbeitet die KI mit ihren Standard-Einstellungen. Für spezifische Anforderungen empfiehlt sich jedoch ein passendes Paket.

**Kann ich ein Agentenpaket während einer laufenden Aufgabe wechseln?**
Nein. Das Paket wird beim Start der KI festgelegt und kann danach nicht mehr geändert werden.

**Wie viele Anweisungsdateien kann ein Paket enthalten?**
Es gibt keine feste Obergrenze. Empfehlenswert ist eine klare, überschaubare Anzahl, damit die KI die Anweisungen gut verarbeiten kann.

**Kann ich Agentenpakete zwischen Projekten teilen?**
Ja. Alle Agentenpakete stehen projektübergreifend zur Verfügung.

**Was sind Anweisungsdateien genau?**
Das sind einfache Textdateien, die in einer strukturierten Form beschreiben, wie die KI vorgehen soll. Inhalt und Format legen Sie selbst fest.

---

## Verwandte Funktionen

- [F002 – Aufgabenverwaltung](./F002-aufgabenverwaltung.md) – Aufgaben anlegen und Agentenpaket zuweisen
- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – KI mit dem gewählten Paket starten
- [Zurück zur Übersicht](../features.md)
