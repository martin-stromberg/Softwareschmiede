# Continue: Rückmeldungen zu "Autonome Aufgabe / Projektleiter-Modus"

Dieses Feature (`issue-205-47562196e8894189b9b9980fcd48a71c-automatisierte-produktentwickl`) war
zum Zeitpunkt dieser Notiz bereits über den lifecycle-Abschluss hinaus (`continue.md` war zuvor
in `continue-done.md` umbenannt und das gesamte Verzeichnis gelöscht, siehe Commit
`616f042 feat: Autonome Aufgabe / Projektleiter-Modus abschliessen`). Dieses Verzeichnis wird hier
ausschließlich wieder angelegt, um eine neue Rückmeldung aus dem manuellen Test der ausgelieferten
Funktion festzuhalten. **Es ist noch keine Analyse oder Umsetzung erfolgt** — nur Erfassung der
Rückmeldung, wie vom Anwender angefordert.

## Offene Punkte

- [ ] **Anlage-Workflow für autonome Aufgaben ist konzeptionell falsch umgesetzt.**

  **Beobachtetes Symptom:** Im Initialisierungsdialog einer autonomen Aufgabe führt der Klick auf
  den Button „Anlegen" (Branch-Erstellung) zu der Fehlermeldung
  „Kein lokaler Klon der Aufgabe vorhanden".

  **Diagnose des Anwenders:** Die aktuelle Implementierung geht offenbar davon aus, dass zum
  Zeitpunkt der Branch-Erstellung im Dialog bereits ein lokaler Repository-Klon existiert — analog
  zum bisherigen Workflow beim Start einer regulären (nicht-autonomen) Aufgabe, bei dem das Starten
  der Aufgabe einen Klon des Repositories anlegt. Für autonome Aufgaben ist das so nicht
  vorgesehen und auch nicht sinnvoll: Ein Klon kann zu diesem Zeitpunkt noch gar nicht existieren.

  **Vom Anwender gewünschtes Verhalten:** Für autonome Aufgaben muss der Ablauf invertiert werden:
  Die gesamte konzeptionierte Verzeichnisstruktur — inklusive des eigentlichen
  Repository-Klons, der in einem Unterverzeichnis liegt — darf erst beim **Absenden des gesamten
  Initialisierungsdialogs** angelegt werden, nicht vorher und nicht während der Dialog-Interaktion
  (z. B. beim Klick auf „Anlegen" für einen einzelnen Branch innerhalb des noch offenen Dialogs).

  **Status:** Nur als Rückmeldung erfasst. Anwender hat ausdrücklich um Erfassung gebeten und noch
  keine Umsetzung angefordert — auf weitere Anweisung warten, bevor Code-Analyse oder Fix begonnen
  wird.

## Fehlgeschlagene Tests

_(keine — dieser Eintrag betrifft manuelles Testen der laufenden Anwendung, nicht die automatisierte Test-Suite)_
