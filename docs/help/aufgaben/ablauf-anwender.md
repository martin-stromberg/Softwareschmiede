# Aufgaben & KI-Entwicklungsprozess — Ablauf für Anwender

## Voraussetzungen

- Ein Projekt mit mindestens einem aktiven Git-Repository ist vorhanden.
- Ein KI-Plugin (z.B. Claude CLI) und ein SCM-Plugin (z.B. GitHub) sind konfiguriert.
- Die Softwareschmiede läuft als WPF-Desktopanwendung unter Windows 11.

## Schritt-für-Schritt-Anleitung

### 1. Neue Aufgabe anlegen

Navigiere über die Seitenleiste zu **Projekte**, öffne das gewünschte Projekt und lege eine neue Aufgabe an. Vergib einen Titel und optional eine Anforderungsbeschreibung. Bestätige mit „Anlegen".

### 2. Repository einrichten

Öffne die Aufgabe in der Aufgabendetailansicht. Klicke **Gestartet setzen**:

- Die Anwendung klont das Repository in das konfigurierte Arbeitsverzeichnis.
- Ein `task/`-Branch wird angelegt.
- Der Status wechselt auf **Gestartet**.

> **Hinweis:** Das Arbeitsverzeichnis muss in den Einstellungen konfiguriert sein.

### 3. CLI starten

1. Im Dropdown **KI-Plugin** das gewünschte Plugin auswählen (z.B. „Claude CLI").
2. Optional: Zusätzliche CLI-Parameter eingeben.
3. **CLI starten** klicken.

Das CLI-Fenster erscheint eingebettet in die Aufgabenansicht. Der Status wechselt auf **In Arbeit**. Die KI ist nun interaktiv bedienbar.

### 4. Mit der KI arbeiten

Das eingebettete CLI-Fenster verhält sich wie ein natives Terminalfenster. Prompts können direkt eingegeben werden. Das Protokoll der Sitzung wird laufend in der Aufgabe gespeichert.

### 5. CLI beenden

Beendet sich das CLI-Programm selbst, aktualisiert die Ansicht automatisch den Status. Alternativ kann der Anwender **CLI stoppen** klicken (graceful shutdown: 5 s Wartezeit, dann Kill).

### 6. Aufgabe abschließen

**Aufgabe abschließen** klicken. Der Status wechselt auf **Beendet**.

## Ergebnis

Die Aufgabe ist mit Status **Beendet** abgelegt. Alle Protokolleinträge bleiben erhalten.

## Sonderfälle

- **Rate-Limit (Status Wartend):** Das CLI gibt einen Rate-Limit-Marker aus; Status wechselt auf „Wartend". Ein Prompt-Vorschlag wird gespeichert. Über „Wiederherstellen" (Recovery-Banner auf dem Dashboard) kann die Aufgabe auf „Gestartet" zurückgesetzt und das CLI erneut gestartet werden.
- **Aufgabe wiederherstellen:** Erscheint auf dem Dashboard das Banner „X Aufgabe(n) benötigen Wiederherstellung", kann durch Klick auf **Wiederherstellen** der Status zurückgesetzt werden. Voraussetzung: kein aktiver CLI-Prozess und Heartbeat älter als 5 Minuten.

## Barrierefreiheit

Die Seitenleiste kann mit dem Hamburger-Button (☰) ein- und ausgeklappt werden. Navigationselemente sind per Tastatur erreichbar. Das eingebettete CLI-Fenster verhält sich wie eine native Windows-Anwendung.
