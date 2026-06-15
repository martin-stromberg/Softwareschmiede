# Aufgaben & KI-Entwicklungsprozess

Aufgaben sind die Arbeitseinheiten der Softwareschmiede. Jede Aufgabe beschreibt eine Entwicklungsanforderung, die ein KI-CLI-Tool in einem isolierten Git-Branch bearbeitet. Die Softwareschmiede klont das Repository, legt den Branch an und bettet das CLI-Fenster des KI-Tools direkt in die Aufgabendetailansicht ein.

Die Aufgabendetailansicht bietet ein Ribbon-Menü für schnellen Zugriff auf Aktionen (Speichern, Löschen, Starten, Beenden) und zeigt je nach Status unterschiedliche Inhalte: Edit-Panel zum Bearbeiten von Titel und Anforderung (Status: Neu), CLI-Panel mit eingebettetem Terminalfenster (Status: Gestartet/InArbeit/Wartend), oder Diff-Panel zur Anzeige von Änderungen (Status: Beendet).

## Inhalt

- [Beschreibung](beschreibung.md)
- [Technischer Ablauf](ablauf-technisch.md)
- [Ablauf für Anwender](ablauf-anwender.md)
- [Datenmodell](datenmodell.md)
- [Business Rules](business-rules.md)
