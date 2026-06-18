← [Zurück zur Übersicht](index.md)

# CLI-Fenster-Einbettung — Ablauf für Anwender

## Voraussetzungen

- Eine Aufgabe ist im Status **Gestartet** (Repository geklont, Branch angelegt).
- Ein KI-Plugin ist in den Einstellungen konfiguriert.

## Schritt-für-Schritt-Anleitung

### 1. KI-Plugin auswählen

In der Aufgabendetailansicht das gewünschte KI-Plugin aus dem Dropdown wählen (z.B. „Claude CLI" oder „GitHub Copilot").

### 2. CLI starten

**CLI starten** klicken. Die Softwareschmiede:

- Startet das CLI-Programm des Plugins im Aufgabenverzeichnis.
- Bettet das CLI-Fenster in die Ansicht ein.
- Wechselt den Aufgabenstatus auf **In Arbeit**.

> **Hinweis:** Das Einbetten kann einen kurzen Moment dauern, bis das CLI-Fenster seinen Start abgeschlossen hat. Falls das Fenster zunächst leer bleibt, kurz warten.

### 3. Mit dem CLI arbeiten

Das eingebettete Fenster verhält sich wie ein normales Terminalfenster. Prompts können direkt getippt werden. Das Fenster füllt automatisch den gesamten verfügbaren Platz der Ansicht aus.

### 4. CLI beenden

Das CLI beendet sich entweder selbst (nach Abschluss einer Sitzung) oder kann über **CLI stoppen** manuell beendet werden. Nach dem Beenden ist der „CLI starten"-Button wieder aktiv.

## Ergebnis

Das CLI hat seine Arbeit verrichtet. Der Anwender kann anschließend mit **Aufgabe abschließen** den Status auf **Beendet** setzen.

## Barrierefreiheit

Das eingebettete CLI-Fenster ist eine native Windows-Anwendung und verhält sich entsprechend: Tastatur, Screenreader und andere Hilfsmittel funktionieren so, wie das jeweilige CLI-Programm sie unterstützt.
