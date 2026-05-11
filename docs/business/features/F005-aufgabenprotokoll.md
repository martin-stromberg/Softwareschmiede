# F005 – Aufgabenprotokoll

## Einleitung

Das Aufgabenprotokoll ist das Tagebuch jeder Aufgabe in der Softwareschmiede.
Es zeichnet lückenlos auf, was die KI getan hat, welche Nachrichten ausgetauscht wurden und wann sich der Status verändert hat.
So können Sie jederzeit nachvollziehen, wie eine Aufgabe bearbeitet wurde – auch Wochen später.
Das Protokoll bleibt auch nach dem Abschluss oder dem Abbruch einer Aufgabe vollständig erhalten.
Es lässt sich nicht nachträglich verändern.

![Aufgabenprotokoll](../images/F005-protokoll.png)

---

## Wer nutzt es?

**Softwareentwickler**, die den Verlauf einer KI-Bearbeitung nachvollziehen oder bei Fehlern analysieren möchten.
Das Protokoll ist besonders hilfreich, wenn eine Aufgabe fehlgeschlagen ist oder wenn man das Ergebnis der KI verstehen möchte.

---

## Schritt-für-Schritt-Anleitung

### Protokoll einer Aufgabe öffnen

1. Sie öffnen das gewünschte Projekt im Dashboard.
2. Sie klicken auf die gewünschte Aufgabe.
3. Sie klicken auf **Protokoll anzeigen** oder auf die Registerkarte **Protokoll**.
4. Das Protokoll erscheint als chronologische Liste von Einträgen.

### Einträge lesen und verstehen

Jeder Eintrag zeigt:
- **Zeitstempel** – Wann genau ist dieser Eintrag entstanden?
- **Typ** – Was für eine Art von Eintrag ist es? (siehe Tabelle unten)
- **Inhalt** – Die eigentliche Nachricht oder Aktion

### Neues Protokollformat verstehen

Das Protokoll folgt jetzt einem festen, gut lesbaren Aufbau.
Sie sehen oben immer eine Datumszeile wie **# 2026-05-11**.
Darunter folgen einzelne Arbeitsschritte wie **## Schritt 1**, **## Schritt 2** und so weiter.
So erkennen Sie sofort, wann die Arbeit stattfand und in welcher Reihenfolge die Schritte liefen.
Die Webansicht zeigt diesen Aufbau klar formatiert an.
Unsichere oder fremde Inhalte werden vor der Anzeige automatisch bereinigt.
Falls die Formatierung einmal nicht korrekt geladen wird, zeigt die Seite den Inhalt in einer einfachen Ersatzansicht.
So bleibt das Protokoll trotzdem lesbar.

**Übersicht der Eintragstypen:**

| Typ | Symbol | Bedeutung |
|-----|--------|-----------|
| Nachricht der KI | 💬 | Die KI hat etwas mitgeteilt, erklärt oder gefragt. |
| Ihre Nachricht | 👤 | Eine Anweisung oder Ergänzung, die Sie gesendet haben. |
| Code-Änderung | 📝 | Die KI hat eine Datei erstellt, bearbeitet oder gelöscht. |
| Automatische Aktion | ⚙️ | Die Softwareschmiede hat im Hintergrund etwas getan (z.B. Arbeitskopie erstellt). |
| Status-Änderung | 🔀 | Die Aufgabe hat einen neuen Status erreicht. |
| Fehler | ❌ | Es ist ein Fehler aufgetreten. Die Fehlermeldung steht direkt daneben. |
| Abschluss | ✅ | Die Aufgabe wurde erfolgreich abgeschlossen. |

### Im Protokoll suchen

1. Sie klicken in das **Suchfeld** am oberen Rand des Protokolls.
2. Sie tippen einen Suchbegriff ein (z.B. „Fehler" oder einen Dateinamen).
3. Die Einträge, die Ihren Suchbegriff enthalten, werden hervorgehoben.

### Fehler im Protokoll analysieren

1. Sie suchen nach dem roten ❌-Symbol oder filtern nach dem Typ **Fehler**.
2. Klicken Sie auf den Eintrag, um die vollständige Fehlermeldung zu lesen.
3. Prüfen Sie, welche Einträge unmittelbar davor stehen – das hilft, die Ursache zu verstehen.

---

## Beispiel

Sie haben letzte Woche eine Aufgabe gestartet, die fehlgeschlagen ist.
Heute möchten Sie verstehen, was schiefgelaufen ist.
Sie öffnen die Aufgabe und klicken auf **Protokoll anzeigen**.
Sie scrollen zu den letzten Einträgen und sehen ein ❌-Symbol.
Der Eintrag zeigt: „Die Datei konnte nicht gefunden werden."
Sie sehen, dass die KI kurz davor eine Datei umbenannt hat, die sie anschließend gesucht hat.
Mit diesem Wissen formulieren Sie die Aufgabe präziser und starten neu.

---

## Was passiert im Hintergrund?

Jede Aktion in der Softwareschmiede wird automatisch als Protokolleintrag gespeichert.
Ob die KI eine Nachricht schickt, eine Datei ändert oder ein Fehler auftritt – alles landet sofort im Protokoll.
Das Protokoll wird nicht verändert oder gelöscht, selbst wenn Sie die Aufgabe abbrechen oder das Projekt archivieren.

---

## Häufige Fragen (FAQ)

**Kann ich Protokolleinträge löschen?**
Nein. Das Protokoll ist unveränderlich. Das schützt die Nachvollziehbarkeit der Arbeit.

**Wie lange wird das Protokoll aufbewahrt?**
Solange das Projekt und die Aufgabe in der Softwareschmiede vorhanden sind, bleibt das Protokoll erhalten. Erst wenn Sie die Aufgabe oder das Projekt löschen, wird auch das Protokoll entfernt.

**Kann ich das Protokoll exportieren?**
Diese Funktion ist derzeit nicht verfügbar.

**Warum sehe ich Zeilen wie „# 2026-05-11“ und „## Schritt 3“?**
Das ist das neue feste Protokollformat. Es zeigt Datum und Arbeitsschritte klar und einheitlich.

**Was passiert bei einem Darstellungsfehler im Browser?**
Die Seite nutzt automatisch eine einfache Ersatzansicht. So bleibt der Inhalt lesbar.

**Was bedeutet es, wenn im Protokoll sehr viele Code-Änderungen stehen?**
Das ist normal bei umfangreichen Aufgaben. Die KI bearbeitet viele Dateien, um eine Funktion vollständig umzusetzen.

**Gibt es eine Begrenzung der Protokolllänge?**
Nein. Das Protokoll wächst mit der Dauer und Komplexität der Aufgabe mit.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – Echtzeit-Ausgabe der KI beobachten
- [F006 – Aufgabe abschließen](./F006-aufgabe-abschliessen.md) – Aufgabe beenden und Protokoll abschließen
- [F007 – Aufgabe abbrechen](./F007-aufgabe-abbrechen.md) – Abbruch und Protokolleintrag
- [Technischer Ablauf (Flow)](../../flows/README.md) – Kurzer Überblick über technische Abläufe
- [Technische Schnittstellen (API)](../../api/README.md) – Kurzüberblick für technische Vertiefung
- [Zurück zur Übersicht](../features.md)
