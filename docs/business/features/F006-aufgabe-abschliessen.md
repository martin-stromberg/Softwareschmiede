# F006 – Aufgabe abschließen

## Einleitung

Das Abschließen einer Aufgabe ist der letzte Schritt im Entwicklungsprozess.
Nachdem die KI ihre Arbeit beendet hat, laden Sie die Änderungen in Ihre GitHub-Ablage hoch.
Danach erstellen Sie einen Vorschlag (Pull Request), damit die Änderungen geprüft und in den Hauptcode übernommen werden können.
Die lokale Arbeitskopie auf Ihrem Rechner wird anschließend automatisch gelöscht.
Die Aufgabe gilt dann als **Abgeschlossen** und bleibt mit vollständigem Protokoll erhalten.

![Aufgabe abschließen](../images/F006-abschliessen.png)

---

## Wer nutzt es?

**Softwareentwickler**, die das Ergebnis der KI-Arbeit in ihre GitHub-Ablage übertragen und zur Überprüfung freigeben möchten.
Dieser Schritt erfolgt, nachdem Sie das Ergebnis der KI geprüft und für gut befunden haben.

---

## Schritt-für-Schritt-Anleitung

### Ergebnis prüfen

1. Sie öffnen die Aufgabe nach Abschluss der KI-Arbeit.
2. Sie lesen die Zusammenfassung der KI in der Ausgabe.
3. Sie prüfen im Protokoll, welche Dateien verändert wurden.
4. Wenn das Ergebnis Ihren Erwartungen entspricht, fahren Sie mit dem Hochladen fort.

### Änderungen hochladen (Push)

1. Sie klicken auf die Schaltfläche **Änderungen hochladen**.
2. Die Softwareschmiede überträgt alle Änderungen in Ihre GitHub-Ablage.
3. Eine Bestätigung erscheint, sobald das Hochladen abgeschlossen ist.

### Pull Request erstellen

1. Nach dem Hochladen klicken Sie auf **Pull Request erstellen**.
2. Ein Formular öffnet sich mit vorausgefülltem Titel und Beschreibung (aus der Aufgabe).
3. Sie prüfen und ergänzen die Angaben bei Bedarf.
4. Sie klicken auf **Pull Request senden**.
5. Der Vorschlag erscheint nun in Ihrer GitHub-Ablage und kann dort von anderen geprüft werden.

### Aufgabe als abgeschlossen markieren

1. Nach dem Erstellen des Pull Requests klicken Sie auf **Aufgabe abschließen**.
2. Bestätigen Sie die Nachfrage mit **Ja, abschließen**.
3. Die lokale Arbeitskopie wird automatisch gelöscht.
4. Die Aufgabe wechselt in den Status **Abgeschlossen**.

---

## Beispiel

Die KI hat die Aufgabe „Suchfunktion hinzufügen" abgeschlossen.
Sie lesen die Ausgabe: Die KI meldet, dass das Suchfeld eingebaut und getestet wurde.
Sie klicken auf **Änderungen hochladen** – die Softwareschmiede überträgt alles in wenigen Sekunden.
Dann klicken Sie auf **Pull Request erstellen**.
Im Formular steht bereits der Titel „Suchfunktion hinzufügen" aus Ihrer Aufgabenbeschreibung.
Sie ergänzen einen Kommentar und klicken auf **Pull Request senden**.
Zuletzt klicken Sie auf **Aufgabe abschließen** und bestätigen.
Die Aufgabe steht nun als **Abgeschlossen** in der Liste.

---

## Was passiert im Hintergrund?

Beim Hochladen werden alle von der KI vorgenommenen Änderungen gebündelt und an Ihre GitHub-Ablage übertragen.
Dabei wird ein sogenannter Zweig (eine separate Arbeitslinie) in Ihrer GitHub-Ablage angelegt.
Der Pull Request ist ein offizieller Vorschlag, diese Arbeitslinie mit der Hauptlinie zu verbinden.
Erst wenn jemand den Pull Request genehmigt und zusammenführt, landen die Änderungen im Hauptcode.
Die lokale Kopie auf Ihrem Rechner wird nach dem Abschluss gelöscht, um Speicherplatz zu sparen.

---

## Häufige Fragen (FAQ)

**Was passiert, wenn ich die Aufgabe abschließe, ohne einen Pull Request zu erstellen?**
Die lokale Arbeitskopie wird gelöscht, aber die Änderungen liegen bereits in Ihrer GitHub-Ablage. Sie können den Pull Request später direkt in GitHub erstellen.

**Kann ich nach dem Abschluss noch Änderungen vornehmen?**
Die Aufgabe gilt als abgeschlossen. Für weitere Änderungen legen Sie eine neue Aufgabe an.

**Was passiert, wenn das Hochladen fehlschlägt?**
Die Softwareschmiede zeigt eine Fehlermeldung. Die lokale Arbeitskopie bleibt erhalten, sodass Sie es erneut versuchen können.

**Wer prüft den Pull Request?**
Das hängt von Ihrem Arbeitsablauf ab. In der Regel sehen alle Personen mit Zugriff auf die GitHub-Ablage den Vorschlag und können ihn kommentieren oder genehmigen.

**Wird die Aufgabe automatisch abgeschlossen, wenn der Pull Request genehmigt wird?**
Nein. Sie müssen den Abschluss in der Softwareschmiede manuell bestätigen.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – KI-Arbeit beobachten und steuern
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md) – Verlauf der Aufgabe einsehen
- [F007 – Aufgabe abbrechen](./F007-aufgabe-abbrechen.md) – Aufgabe ohne Speichern beenden
- [Zurück zur Übersicht](../features.md)
