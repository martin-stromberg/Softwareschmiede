# F012 – Kontextsteuerung bei Folgeanweisungen

## Einleitung

Mit dieser Funktion steuern Sie jede Folgeanweisung bewusst.
Sie entscheiden vor dem Senden, wie viel Verlauf berücksichtigt wird.
So vermeiden Sie Missverständnisse bei langen Arbeitsläufen.
Sie können den bisherigen Verlauf nutzen, ausblenden oder neu starten.
Das hilft bei klaren Ergebnissen und schnellen Korrekturen.

---

## Wer nutzt es?

Diese Funktion nutzen Fachanwender in der Aufgabendetailseite.
Sie prüfen Zwischenergebnisse und geben danach neue Anweisungen.
Dabei wählen sie je Nachricht den passenden Umgang mit dem bisherigen Verlauf.

---

## Schritt-für-Schritt-Anleitung

1. Sie öffnen eine Aufgabe mit aktivem Bereich **Folge-Prompt**.
2. Sie schreiben Ihre nächste Anweisung in das Eingabefeld.
3. Sie wählen den gewünschten Modus: **Kontext mitgeben**, **Kontext ignorieren** oder **Kontext neu beginnen**.
4. Bei **Kontext neu beginnen** prüfen Sie den Hinweis zum Neustart des Verlaufs.
5. Sie klicken auf **Senden**.
6. Sie lesen die neue Antwort im Protokoll und entscheiden den nächsten Schritt.

---

## Beispiel

Sie haben eine lange Aufgabe zur Rechnungsprüfung.
Die bisherigen Antworten drehen sich um Pflichtfelder.
Jetzt wollen Sie nur noch den Export klären.
Sie wählen **Kontext ignorieren** und senden: „Bitte nur den CSV-Export prüfen.“
Die nächste Antwort bleibt auf dieses Thema fokussiert.

---

## Was passiert im Hintergrund?

Die Anwendung merkt sich den bisherigen Verlauf je Aufgabe.
Beim Modus **Kontext mitgeben** wird dieser Verlauf vor Ihre neue Anweisung gesetzt.
Beim Modus **Kontext ignorieren** geht nur Ihre neue Anweisung weiter.
Beim Modus **Kontext neu beginnen** startet der Verlauf ab dieser Nachricht neu.
Wenn der Verlauf zu lang wird, fasst die Anwendung ihn auf das Wesentliche zusammen.

---

## Häufige Fragen (FAQ)

**Wann nutze ich Kontext mitgeben?**  
Wenn die neue Anweisung auf früheren Antworten aufbauen soll.

**Wann nutze ich Kontext ignorieren?**  
Wenn Sie ein Thema klar trennen und neu fokussieren möchten.

**Was bewirkt Kontext neu beginnen?**  
Der bisherige Verlauf wird für den nächsten Lauf ersetzt.

**Kann ich den Modus bei jeder Nachricht ändern?**  
Ja. Sie wählen den Modus vor jeder Folgeanweisung neu.

**Sehe ich später noch, welcher Modus genutzt wurde?**  
Ja. Der Ablauf bleibt im Protokoll nachvollziehbar.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – Folgeanweisungen im Gesamtablauf verstehen
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md) – Antworten und Schritte nachvollziehen
- [F011 – Agent-Auswahl bei Folgeanweisungen](./F011-agent-auswahl-bei-folgeanweisungen.md) – Passenden Agenten pro Folgeanweisung wählen
- [Zurück zur Übersicht](../features.md)
