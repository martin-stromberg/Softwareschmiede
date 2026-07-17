# Stabilität & Fehlerbehandlung

Die Softwareschmiede fängt Fehler an allen relevanten Stellen zentral ab und protokolliert sie, statt unkontrolliert abzustürzen. Dazu gehören globale Exception-Handler für UI-Thread, Hintergrund-Threads und unbeobachtete Tasks, eine zentrale Absicherung für Fire-and-Forget-Aufrufe, geschützte Prozess-Event-Handler sowie eine zuverlässige Freigabe nativer ConPTY-Handles.

## Inhalt

- [Beschreibung](beschreibung.md)
- [Technischer Ablauf](ablauf-technisch.md)
- [Architektur](architektur.md)
- [Business Rules](business-rules.md)
- [Fehlerbehebung](troubleshooting.md)
- [OS-Schnittstellen-Tests](os-interface-tests.md)
