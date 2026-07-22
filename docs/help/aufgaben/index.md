# Aufgaben & KI-Entwicklungsprozess

Aufgaben sind die Arbeitseinheiten der Softwareschmiede. Jede Aufgabe beschreibt eine Entwicklungsanforderung, die ein KI-CLI-Tool in einem isolierten Git-Branch bearbeitet. Der Aufgabenworkflow wurde optimiert: Ein einzelner „Starten"-Button kombiniert Repository-Klon, Branch-Erstellung, automatische `issue.md`-Generierung und CLI-Start in einem Schritt. Die KI-Plugin-Auswahl erfolgt per Dialog mit optionaler Speicherung als Projekt-Standard.

Die Aufgabendetailansicht bietet ein Ribbon-Menü für schnellen Zugriff auf Aktionen (Speichern, Löschen, Starten, Beenden, Issue anlegen/zuweisen/öffnen, Plugin ändern, Pull Request erstellen) und eine explizite Ansichtsleiste für `Info`, `CLI` und `Diff`. Ein neues Issue kann aus der Aufgabe mit vorausgefülltem Titel und Beschreibung, optionalem Provider-Template und optionaler KI-Ausfüllhilfe angelegt werden. Die lokale Issue-Referenz wird erst nach erfolgreicher externer Anlage gespeichert; nach der Zuordnung ist die Anlageaktion nicht mehr verfügbar. Die Info-Ansicht mit den Aufgabenstammdaten bleibt auch bei gestarteten, wartenden und beendeten Aufgaben erreichbar; CLI und Diff werden je nach Aufgabenstatus angeboten. Beim Prozessstart werden automatisch eine lokale `issue.md` mit der Aufgabenbeschreibung und ein `.gitignore`-Eintrag für diese Datei erstellt. CLI-Ausgaben aus ConPTY-Sitzungen werden automatisch als `CliOutput`-Einträge im Aufgabenprotokoll gespeichert, damit der Lauf später nachvollziehbar bleibt. Aufgaben aus Git-Plugins können auch ohne Issue-Bezug gestartet werden. Wenn eine Aufgabe aus einem GitHub-Issue stammt, ergaenzt die Pull-Request-Erstellung automatisch eine Closing-Direktive, damit GitHub das Issue beim Merge schliessen kann.

Die Navigationsmenü-Seitenleiste zeigt bis zu 20 derzeit aktive Aufgaben als Kacheln mit Titel und KI-Ausführungsstatus an, um schnellen Zugriff auf laufende Arbeiten zu ermöglichen. Das Dashboard zeigt die gleiche Aufgabenliste an; die Menüsektion wird automatisch verborgen wenn das Dashboard aktiv ist, um Redundanz zu vermeiden.

## Inhalt

- [Beschreibung](beschreibung.md)
- [Technischer Ablauf](ablauf-technisch.md)
- [Ablauf für Anwender](ablauf-anwender.md)
- [API](api.md)
- [Datenmodell](datenmodell.md)
- [Business Rules](business-rules.md)
