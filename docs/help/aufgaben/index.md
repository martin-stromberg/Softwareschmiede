# Aufgaben & KI-Entwicklungsprozess

Aufgaben sind die Arbeitseinheiten der Softwareschmiede. Jede Aufgabe beschreibt eine Entwicklungsanforderung, die ein KI-CLI-Tool in einem isolierten Git-Branch bearbeitet. Der Aufgabenworkflow wurde optimiert: Ein einzelner „Starten"-Button kombiniert Repository-Klon, Branch-Erstellung, automatische `issue.md`-Generierung und CLI-Start in einem Schritt. Die KI-Plugin-Auswahl erfolgt per Dialog mit optionaler Speicherung als Projekt-Standard.

Die Aufgabendetailansicht bietet ein Ribbon-Menü für schnellen Zugriff auf Aktionen (Speichern, Löschen, Starten, Beenden, Plugin ändern) und eine explizite Ansichtsleiste für `Info`, `CLI` und `Diff`. Die Info-Ansicht mit den Aufgabenstammdaten bleibt auch bei gestarteten, wartenden und beendeten Aufgaben erreichbar; CLI und Diff werden je nach Aufgabenstatus angeboten. Beim Prozessstart werden automatisch eine lokale `issue.md` mit der Aufgabenbeschreibung und ein `.gitignore`-Eintrag für diese Datei erstellt. Aufgaben aus Git-Plugins können auch ohne Issue-Bezug gestartet werden.

Die Navigationsmenü-Seitenleiste zeigt bis zu 20 derzeit aktive Aufgaben als Kacheln mit Titel und KI-Ausführungsstatus an, um schnellen Zugriff auf laufende Arbeiten zu ermöglichen. Das Dashboard zeigt die gleiche Aufgabenliste an; die Menüsektion wird automatisch verborgen wenn das Dashboard aktiv ist, um Redundanz zu vermeiden.

## Inhalt

- [Beschreibung](beschreibung.md)
- [Technischer Ablauf](ablauf-technisch.md)
- [Ablauf für Anwender](ablauf-anwender.md)
- [API](api.md)
- [Datenmodell](datenmodell.md)
- [Business Rules](business-rules.md)
