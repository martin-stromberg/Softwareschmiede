# F011 – Agent-Auswahl bei Folgeanweisungen

## Einleitung

Diese Funktion hilft Ihnen bei der Steuerung von Folgeanweisungen.
Sie wählen vor dem Senden den passenden Agenten aus.
So landet Ihre Folgeanweisung bei der richtigen Arbeitsweise.
Die Anwendung setzt die Auswahl nach dem Senden wieder sicher zurück.
Der erste Prompt der Aufgabe bleibt dabei unverändert.

---

## Wer nutzt es?

Diese Funktion nutzen Fachanwender in der Aufgabendetailseite.
Sie prüfen Ergebnisse und geben danach gezielte Nachbesserungen.
Dabei entscheiden sie pro Folgeanweisung, welcher Agent arbeiten soll.

---

## Schritt-für-Schritt-Anleitung

1. Sie öffnen eine Aufgabe mit dem Status **In Bearbeitung**.
2. Sie prüfen das Protokoll und warten auf mindestens eine KI-Antwort.
3. Sie sehen den Bereich **Folge-Prompt** mit **Agent auswählen**.
4. Sie lassen die Vorgabe stehen oder wählen einen anderen Agenten.
5. Sie schreiben Ihre Folgeanweisung im Eingabefeld.
6. Sie klicken auf **Senden**.
7. Sie sehen die neue Ausgabe direkt im bestehenden Protokoll.
8. Sie sehen danach wieder den Start-Agenten als neue Vorgabe.

---

## Beispiel

Sie starten eine Aufgabe mit dem Agenten „agent-initial“.
Nach dem ersten Ergebnis möchten Sie nur Tests nachziehen.
Sie wählen dafür bei **Agent auswählen** den Agenten „agent-alt“.
Sie senden: „Bitte passe die Tests an.“
Nach dem Versand steht wieder „agent-initial“ als Vorgabe im Auswahlfeld.

---

## Was passiert im Hintergrund?

Die Anwendung zeigt die Agentenliste nur bei verfügbaren Folgeanweisungen.
Beim ersten Laden übernimmt sie den Start-Agenten als Vorgabe.
Beim Senden nutzt sie genau den aktuell ausgewählten Agenten.
Danach setzt sie die Auswahl wieder auf den Start-Agenten.
Der erste Prompt der Aufgabe wird dabei nicht umgestellt.

---

## Häufige Fragen (FAQ)

**Wann sehe ich den Bereich für Folgeanweisungen?**
Sobald die Aufgabe in **In Bearbeitung** ist und eine KI-Antwort vorliegt.

**Muss ich den Agenten jedes Mal neu auswählen?**
Nein. Standardmäßig ist der Start-Agent bereits vorausgewählt.

**Kann ich vor dem Senden einen anderen Agenten wählen?**
Ja. Sie können die Auswahl vor jeder Folgeanweisung ändern.

**Wird wirklich der ausgewählte Agent verwendet?**
Ja. Ihre Folgeanweisung wird an genau diesen Agenten gesendet.

**Ändert diese Funktion den ersten Prompt der Aufgabe?**
Nein. Das Verhalten des ersten Prompts bleibt unverändert.

---

## Akzeptanzkriterien

1. Der Bereich **Folge-Prompt** mit **Agent auswählen** ist sichtbar und nutzbar.
2. Beim Laden ist der Start-Agent als Standardwert gesetzt.
3. Die Agentenauswahl kann vor dem Senden frei geändert werden.
4. Die Folgeanweisung geht an den aktuell ausgewählten Agenten.
5. Das Verhalten des Start-Prompts bleibt unverändert.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – Gesamtablauf der KI-Bearbeitung
- [F004 – Agentenpakete](./F004-agentenpakete.md) – Agenten verstehen und bereitstellen
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md) – Verlauf und Antworten prüfen
- [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](./F014-standardplugin-ki-plugin-auswahl.md) – Standardauswahl für KI-Prompts verstehen
- [Zurück zur Übersicht](../features.md)
