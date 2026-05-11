# F003 – KI-Entwicklungsprozess

## Einleitung

Der KI-Entwicklungsprozess ist die Kernfunktion der Softwareschmiede.
Sie übergeben der KI eine Aufgabenbeschreibung – und die KI beginnt, selbstständig zu arbeiten.
Sie sehen in Echtzeit, was die KI schreibt, plant und umsetzt.
Bei Bedarf können Sie der KI während der Arbeit weitere Hinweise geben.
Der gesamte Vorgang wird lückenlos im Protokoll festgehalten.

![KI-Entwicklungsansicht](../images/F003-ki-ansicht.png)

---

## Wer nutzt es?

**Softwareentwickler**, die eine Aufgabe an die KI übergeben und den Fortschritt begleiten möchten.
Sie starten den Vorgang und entscheiden, ob und wann sie eingreifen.
Das Ergebnis können Sie prüfen, bevor es dauerhaft gespeichert wird.

---

## Schritt-für-Schritt-Anleitung

### KI starten

1. Sie öffnen eine Aufgabe mit dem Status **Offen**.
2. Sie prüfen, ob Titel, Beschreibung und Agentenpaket korrekt sind.
3. Sie klicken auf **KI starten**.
4. Die Softwareschmiede erstellt eine separate Arbeitskopie des Codes.
5. Die KI beginnt, die Aufgabe zu bearbeiten. Der Status wechselt auf **KI aktiv**.

### Echtzeit-Ausgabe beobachten

- Die Ausgabe der KI erscheint laufend auf dem Bildschirm.
- Sie sehen Nachrichten der KI, bearbeitete Dateien und Zwischenergebnisse.
- Ein **Ladeindikator** zeigt an, dass die KI noch arbeitet.
- Sobald die KI fertig ist, verschwindet der Indikator und eine Zusammenfassung erscheint.

### Folge-Anweisung geben

1. Während die KI arbeitet oder nach einem Schritt, sehen Sie ein **Eingabefeld** am unteren Rand.
2. Sie tippen Ihre Ergänzung ein (z.B. „Bitte auch eine Fehlermeldung anzeigen, wenn das Passwort zu kurz ist.").
3. Sie klicken auf **Senden** oder drücken die **Eingabetaste**.
4. Die KI liest Ihre Ergänzung und arbeitet daran weiter.
5. Wenn Sie die Agentensteuerung im Detail brauchen, nutzen Sie [F011 – Agent-Auswahl bei Folgeanweisungen](./F011-agent-auswahl-bei-folgeanweisungen.md).
6. Wenn Sie den Verlauf gezielt steuern möchten, nutzen Sie [F012 – Kontextsteuerung bei Folgeanweisungen](./F012-kontextsteuerung-folgeanweisungen.md).

### KI bei einem Fehler neu starten

1. Wenn die KI einen Fehler meldet oder stoppt, sehen Sie eine Fehlermeldung in der Ausgabe.
2. Sie können im Eingabefeld eine Korrekturanweisung eingeben und auf **Senden** klicken.
3. Alternativ können Sie die Aufgabe abbrechen und mit einer überarbeiteten Beschreibung neu starten.

---

## Beispiel

Sie haben die Aufgabe „Suchfeld auf der Startseite hinzufügen" angelegt.
Sie klicken auf **KI starten**.
Auf dem Bildschirm erscheinen die ersten Nachrichten der KI: Sie plant zuerst, welche Dateien sie bearbeiten muss.
Dann sehen Sie, wie sie Zeile für Zeile Code schreibt.
Nach einigen Minuten schreibt die KI: „Suchfeld wurde hinzugefügt. Soll ich auch einen Filter einbauen?"
Sie tippen in das Eingabefeld: „Ja, bitte auch nach Kategorie filtern." und senden die Nachricht.
Die KI arbeitet weiter und meldet schließlich den Abschluss.

---

## Was passiert im Hintergrund?

Beim Start der KI lädt die Softwareschmiede den Code aus Ihrer GitHub-Ablage auf Ihren Rechner.
Es wird eine separate Arbeitskopie angelegt, damit der ursprüngliche Code unberührt bleibt.
Das Basis-Arbeitsverzeichnis dieser Arbeitskopie kann in den Einstellungen konfiguriert werden.
Die KI arbeitet in dieser Kopie. Alle Änderungen bleiben lokal, bis Sie den Abschluss bestätigen.
Ihre Folge-Anweisungen werden direkt an die KI weitergeleitet – wie eine Unterhaltung.

---

## Häufige Fragen (FAQ)

**Kann ich die KI während der Arbeit stoppen?**
Ja. Klicken Sie auf **Aufgabe abbrechen**. Die lokale Arbeitskopie wird dann gelöscht.

**Was passiert, wenn ich das Browserfenster schließe, während die KI arbeitet?**
Die KI läuft im Hintergrund auf Ihrem Rechner weiter. Beim nächsten Öffnen sehen Sie den aktuellen Stand im Protokoll.

**Kann ich der KI mehrere Nachrichten senden?**
Ja. Sie können beliebig viele Folge-Anweisungen senden.
Je nach Einstellung kann der bisherige Verlauf genutzt, ignoriert oder neu begonnen werden.

**Wie lange braucht die KI für eine Aufgabe?**
Das hängt von der Komplexität der Aufgabe ab. Einfache Änderungen dauern oft wenige Minuten, komplexe Vorhaben können länger dauern.

**Was tun, wenn die KI keine sinnvolle Ausgabe liefert?**
Klicken Sie auf **Aufgabe abbrechen** und formulieren Sie die Aufgabenbeschreibung genauer. Dann starten Sie eine neue Aufgabe.

---

## Verwandte Funktionen

- [F002 – Aufgabenverwaltung](./F002-aufgabenverwaltung.md) – Aufgaben anlegen und vorbereiten
- [F004 – Agentenpakete](./F004-agentenpakete.md) – Das Verhalten der KI steuern
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md) – Ausgabe der KI nachverfolgen
- [F006 – Aufgabe abschließen](./F006-aufgabe-abschliessen.md) – Ergebnis freigeben
- [F007 – Aufgabe abbrechen](./F007-aufgabe-abbrechen.md) – Vorgang beenden ohne zu speichern
- [F009 – Arbeitsverzeichnis konfigurieren](./F009-arbeitsverzeichnis-konfigurieren.md) – Speicherort der lokalen Klone steuern
- [F010 – Plugin-Prinzip für Integrationen](./F010-plugin-prinzip-integrationen.md) – Ausgelagerte GitHub- und Copilot-Anbindung verstehen
- [F011 – Agent-Auswahl bei Folgeanweisungen](./F011-agent-auswahl-bei-folgeanweisungen.md) – Agent je Folgeanweisung gezielt wählen
- [F012 – Kontextsteuerung bei Folgeanweisungen](./F012-kontextsteuerung-folgeanweisungen.md) – Verlauf je Folgeanweisung bewusst mitgeben, ignorieren oder neu beginnen
- [Zurück zur Übersicht](../features.md)
