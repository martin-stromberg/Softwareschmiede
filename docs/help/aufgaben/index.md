# Aufgaben & KI-Entwicklungsprozess

Aufgaben sind die Arbeitseinheiten der Softwareschmiede. Jede Aufgabe beschreibt eine Entwicklungsanforderung, die ein KI-CLI-Tool in einem isolierten Git-Branch bearbeitet. Der Aufgabenworkflow wurde optimiert: Ein einzelner „Starten"-Button kombiniert Repository-Klon, Branch-Erstellung und CLI-Start in einem Schritt. Die KI-Plugin-Auswahl erfolgt per Dialog mit optionaler Speicherung als Projekt-Standard.

Die Aufgabendetailansicht bietet ein Ribbon-Menü für schnellen Zugriff auf Aktionen (Speichern, Löschen, Starten, Beenden, Plugin ändern) und zeigt je nach Status unterschiedliche Inhalte: Edit-Panel zum Bearbeiten von Titel und Anforderung (Status: Neu), CLI-Panel mit eingebettetem Terminalfenster (Status: Gestartet/Wartend), oder Diff-Panel zur Anzeige von Änderungen (Status: Beendet).

## Inhalt

- [Beschreibung](beschreibung.md)
- [Technischer Ablauf](ablauf-technisch.md)
- [Ablauf für Anwender](ablauf-anwender.md)
- [Datenmodell](datenmodell.md)
- [Business Rules](business-rules.md)
