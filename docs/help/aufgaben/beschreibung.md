# Aufgaben & KI-Entwicklungsprozess — Beschreibung

## Zweck

Eine Aufgabe kapselt eine Entwicklungsanforderung: Titel, Beschreibung und optional eine Issue-Referenz aus dem Git-Provider. Die Softwareschmiede führt die Aufgabe KI-gestützt durch, indem sie das Repository klont, einen Branch anlegt und den KI-Agenten mit dem Prompt startet.

## Funktionsweise

### Lebenszyklus

Eine Aufgabe durchläuft folgende Status:

| Status | Bedeutung |
|--------|-----------|
| `Offen` | Angelegt, noch nicht gestartet |
| `InBearbeitung` | Prozess gestartet, KI nicht aktiv |
| `KiAktiv` | KI-Agent läuft gerade |
| `TestsLaufen` | Automatisierte Tests laufen |
| `Abgeschlossen` | Erfolgreich beendet, Klon gelöscht |
| `Fehlgeschlagen` | KI-Fehler oder Abbruch |
| `Archiviert` | Dauerhaft archiviert |

### Ausführungsregister

Die Aufgabendetailansicht hat drei Register:

- **Aufgabe** — Anforderungsbeschreibung und Kennzahlen
- **Ausführung** — KI-Plugin wählen, Agentenpaket, Prompt senden, Terminal, Protokoll
- **Projektverzeichnis** — Repository-Explorer mit Dateivorschau und Git-Aktionen (Commit, Push, Pull, Pull Request)

### KI-Hintergrundausführung

Der `KiAusfuehrungsService` läuft als Singleton. KI-Läufe werden als `Task` im Hintergrund gehalten — der Anwender kann wegnavigieren und zurückkehren, der Lauf läuft weiter. Ausgaben werden gepuffert und können nach Rückkehr nachgelesen werden.

### Kontextsteuerung

Für Folgeanweisungen steuert der Kontextmodus, ob der bisherige Gesprächsverlauf mitgegeben wird:

- **Kontext mitgeben** — Bisherige Kontextdatei wird referenziert
- **Kontext ignorieren** — Kein Kontextpräfix, Verlauf wird weiter fortgeschrieben
- **Kontext neu beginnen** — Bisherige Kontextdateien werden gelöscht, frischer Start

### Rate-Limit-Vorschlag

Erkennt die KI ein Rate-Limit, speichert der `EntwicklungsprozessService` automatisch einen Prompt-Vorschlag mit Ausführungszeitpunkt. Der Anwender sieht einen Hinweis und kann den Zeitplan übernehmen oder manuell abweichen.

## Beispiele

1. Aufgabe „Login-Bug beheben" im Projekt „Backend-API" anlegen.
2. Entwicklung starten: GitHub als SCM-Plugin, Claude CLI als KI-Plugin, Agentenpaket „Entwicklung" wählen.
3. Prompt eingeben: „Behebe den NullReferenceException in AuthController.cs".
4. KI bearbeitet den Branch, Protokoll erscheint live in der Ausführungsansicht.
5. Im Register „Projektverzeichnis" geänderte Dateien reviewen, ggf. commiten und pushen.
6. Pull Request erstellen, Aufgabe abschließen.

## Einschränkungen

- Für eine Aufgabe kann immer nur ein KI-Lauf gleichzeitig aktiv sein.
- Das lokale Klonverzeichnis wird beim Abschließen oder Abbrechen gelöscht.
- Die Aufgabenwiederherstellung (Recovery) steht nur zur Verfügung, wenn der letzte Heartbeat älter als 5 Minuten ist.
