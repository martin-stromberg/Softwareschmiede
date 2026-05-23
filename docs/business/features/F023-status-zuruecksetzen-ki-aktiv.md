# F023 – Status zurücksetzen bei `KI Aktiv`

## Einleitung

Manchmal bleibt eine Aufgabe im Status **KI aktiv**, obwohl keine Verarbeitung mehr läuft. Dann können Sie in **AufgabeDetail** den Status zurücksetzen, damit die Aufgabe wieder eine neue Anfrage annimmt.
Die Schaltfläche heißt in der Oberfläche **„Status zurücksetzen“**. Sie ist getrennt von der Recovery-Funktion **„Aufgabe wiederherstellen“**.

---

## Wer nutzt es?

- Anwender, die nach einem unterbrochenen Lauf direkt weiterarbeiten möchten
- Support/Teamleitung, wenn eine Aufgabe formal noch auf **KI aktiv** steht, aber faktisch frei ist

---

## Schritt-für-Schritt-Anleitung

1. Öffnen Sie die betroffene Aufgabe in der Detailseite.
2. Prüfen Sie, dass der Status **KI aktiv** angezeigt wird.
3. Stellen Sie sicher, dass keine Verarbeitung mehr läuft.
4. Klicken Sie auf **↩️ Status zurücksetzen**.
5. Bestätigen Sie mit **Ja, Status zurücksetzen**.
6. Nach dem Neuladen steht die Aufgabe wieder in **In Bearbeitung** und kann erneut bearbeitet werden.

---

## Wann ist die Aktion verfügbar?

- nur in **AufgabeDetail**
- nur bei Aufgaben im Status **KI aktiv**
- nur wenn keine aktive KI-Verarbeitung läuft

Wenn noch eine Verarbeitung läuft, bleibt die Aktion deaktiviert oder wird mit einem Hinweis abgelehnt.

---

## Was macht die Aktion?

- setzt den Status auf **In Bearbeitung**
- macht eine neue Anfrage wieder möglich
- fragt vor der Ausführung nach einer Bestätigung

---

## Was macht sie bewusst nicht?

- Sie beendet keine laufende KI-Ausführung.
- Sie ersetzt nicht die Recovery-Funktion **„Aufgabe wiederherstellen“**.
- Sie verändert keine Inhalte der Aufgabe außer dem Status.

---

## Hintergrund

- `AufgabeDetail` steuert Sichtbarkeit, Bestätigung und die Rückmeldung an den Anwender.
- Der Laufzustand wird vor der Aktion geprüft, damit nur freie Aufgaben zurückgesetzt werden.
- Die eigentliche Statusänderung erfolgt über die vorhandene Aufgabenlogik im Hintergrund.

---

## Häufige Fragen (FAQ)

**Warum ist „Status zurücksetzen“ manchmal gesperrt?**  
Dann läuft noch eine Verarbeitung oder der Laufzustand ist gerade nicht sicher prüfbar.

**Ist das dasselbe wie „Aufgabe wiederherstellen“?**  
Nein. „Status zurücksetzen“ ist für freie Aufgaben im Status `KI aktiv`. „Aufgabe wiederherstellen“ ist für festhängende Läufe gedacht.

**Geht dabei das Protokoll verloren?**  
Nein, das Protokoll bleibt erhalten.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – Weiterarbeiten nach einem Reset
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md) – Verlauf nach dem Reset nachvollziehen
- [F016 – Fehlerbehandlung & Recovery](./F016-fehlerbehandlung-und-recovery.md) – Festhängende Aufgaben wiederherstellen
- [Zurück zur Übersicht](../features.md)
