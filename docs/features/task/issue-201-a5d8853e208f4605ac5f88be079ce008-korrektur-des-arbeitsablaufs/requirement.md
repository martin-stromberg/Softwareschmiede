# Anforderung

## Fachliche Zusammenfassung

Der Arbeitsablauf bei der Bearbeitung einer Aufgabe soll so angepasst werden, dass die Aufgabenseite nach dem Speichern geoeffnet bleibt und der Anwender die Aufgabe nicht erneut aus der Projektliste aufrufen muss.

Zusaetzlich muss der Zustand der KI-Ausfuehrung unabhaengig vom Gesamtstatus der Aufgabe gespeichert werden. Ist die KI-Ausfuehrung beendet, darf sie beim Wechsel zu einer anderen Aufgabe und bei der Rueckkehr nicht automatisch erneut gestartet oder geoeffnet werden. Sie muss ueber die Aktion "Starten" erneut gestartet werden koennen.

Der Gesamtstatus "Beendet" bleibt davon getrennt. Erst beim Beenden der Aufgabe werden das lokal geklonte Repository geloescht und die Aufgabe in den Gesamtstatus "Beendet" versetzt.

## Betroffene Klassen und Komponenten

### Aufgabenerstellung und Aufgabendetailansicht
- Aktion "Neue Aufgabe" im Projektkontext
- Aufgabendetailansicht fuer die Eingabe und Speicherung der Aufgabenbeschreibung
- Speicherablauf nach dem Anlegen einer Aufgabe
- Navigation nach dem Speichern

### Aufgabenstatus und KI-Ausfuehrung
- Datenmodell fuer den Gesamtstatus der Aufgabe
- Zusaetzlicher Status oder gleichwertige Persistenz fuer den Zustand der KI-Ausfuehrung
- Aktion "Starten" zum Starten oder erneuten Starten der KI-Ausfuehrung
- Aktion "Stoppen" zum optionalen Beenden der KI-Ausfuehrung
- Erkennung, ob die KI-Ausfuehrung aktiv, beendet oder noch nicht gestartet ist

### Aufgabenansicht und Navigation
- Anzeige der CLI innerhalb der Aufgabe
- Wiederaufruf einer laufenden Aufgabe nach Navigation zu anderen Aufgaben
- Wiederaufruf einer Aufgabe nach Neustart des Programms
- Vermeidung eines automatischen erneuten Starts bei beendeter KI-Ausfuehrung

### Repository- und Aufgabenabschluss
- Aktion "Beenden" zum endgueltigen Abschluss der Aufgabe
- Loeschen des lokal geklonten Repositorys beim endgueltigen Abschluss
- Setzen des Gesamtstatus "Beendet"

## Funktionale Anforderungen

1. Beim Aufruf der Aktion "Neue Aufgabe" muss die Aufgabendetailansicht fuer die Eingabe der Aufgabenbeschreibung geoeffnet werden.
2. Beim Speichern einer neu angelegten Aufgabe muessen die eingegebenen Einstellungen gespeichert werden.
3. Nach dem Speichern muss die Aufgabenseite der angelegten Aufgabe geoeffnet bleiben.
4. Der Anwender muss die KI-Ausfuehrung ueber die Aktion "Starten" starten koennen.
5. Bei einer laufenden KI-Ausfuehrung muss beim Wechsel zu einer anderen Aufgabe und beim anschliessenden Wiederaufruf die CLI der Aufgabe angezeigt werden.
6. Bei einer laufenden KI-Ausfuehrung muss nach einem Neustart des Programms und dem erneuten Aufruf der Aufgabe die CLI wieder angezeigt werden.
7. Der Anwender muss die KI-Ausfuehrung ueber die Aktion "Stoppen" optional beenden koennen.
8. Der Zustand der KI-Ausfuehrung muss persistent oder aus dem gespeicherten Aufgabenstatus eindeutig ermittelbar sein.
9. Eine beendete KI-Ausfuehrung darf beim Wechsel zu einer anderen Aufgabe und beim anschliessenden Wiederaufruf nicht automatisch erneut gestartet oder geoeffnet werden.
10. Fuer eine beendete KI-Ausfuehrung muss ein eigener Ausfuehrungsstatus vorhanden sein, der vom Gesamtstatus "Beendet" der Aufgabe unterschieden werden kann.
11. Eine beendete KI-Ausfuehrung muss ueber die Aktion "Starten" erneut gestartet werden koennen.
12. Das erneute Starten einer beendeten KI-Ausfuehrung muss den Ausfuehrungsstatus wieder auf aktiv setzen und die CLI anzeigen.
13. Der Anwender muss die Aufgabe ueber die Aktion "Beenden" endgueltig abschliessen koennen.
14. Beim endgueltigen Beenden muss das lokal geklonte Repository geloescht werden.
15. Beim endgueltigen Beenden muss der Gesamtstatus der Aufgabe auf "Beendet" gesetzt werden.
16. Der Gesamtstatus "Beendet" darf nicht allein durch das Beenden der KI-Ausfuehrung gesetzt werden.
17. Eine Aufgabe mit dem Gesamtstatus "Beendet" darf nicht ueber die Aktion "Starten" erneut ausgefuehrt werden koennen.

## Implementierungsansatz

### Speicherung der Aufgabe nach dem Anlegen

Der bestehende Speichervorgang fuer eine neue Aufgabe muss so erweitert oder angepasst werden, dass nach erfolgreicher Speicherung die Aufgabendetailansicht beziehungsweise Aufgabenseite der neuen Aufgabe geoeffnet bleibt. Die Navigation zur Projektseite mit anschliessendem erneuten Aufruf aus der Aufgabenliste darf nicht mehr erforderlich sein.

### Separaten Ausfuehrungsstatus modellieren

Der Aufgabenstatus muss um einen eigenstaendigen Zustand fuer die KI-Ausfuehrung ergaenzt werden oder eine gleichwertige bestehende Statusstruktur muss dafuer verwendet werden. Mindestens muessen die Zustaende "nicht gestartet", "aktiv" und "beendet" unterscheidbar sein.

Der Ausfuehrungsstatus muss vom Gesamtstatus der Aufgabe getrennt behandelt werden. Das Beenden oder Stoppen der KI-Ausfuehrung darf das lokal geklonte Repository nicht loeschen und darf den Gesamtstatus nicht auf "Beendet" setzen.

### Wiederherstellung und Anzeige der CLI

Beim Aufruf einer Aufgabe muss der gespeicherte Ausfuehrungsstatus ausgewertet werden. Bei einer aktiven Ausfuehrung wird die CLI wie bisher angezeigt beziehungsweise wieder verbunden. Bei einer beendeten Ausfuehrung darf keine automatische CLI-Ausfuehrung gestartet werden; stattdessen muss die Aufgabe in ihrem beendeten Ausfuehrungszustand angezeigt werden.

Die Statusauswertung muss sowohl beim Wechsel zwischen Aufgaben als auch nach einem Neustart des Programms funktionieren.

### Starten und erneutes Starten

Die Aktion "Starten" muss fuer eine noch nicht gestartete sowie fuer eine zuvor beendete KI-Ausfuehrung zur Verfuegung stehen, sofern die Aufgabe nicht den Gesamtstatus "Beendet" besitzt. Beim Starten wird der Ausfuehrungsstatus auf aktiv gesetzt und die CLI-Ausfuehrung gestartet.

### Endgueltiges Beenden

Die Aktion "Beenden" muss weiterhin den endgueltigen Abschluss der Aufgabe ausloesen. Dabei sind das lokal geklonte Repository zu loeschen und der Gesamtstatus der Aufgabe auf "Beendet" zu setzen. Der Gesamtstatus muss unabhaengig vom Ausfuehrungsstatus behandelt werden.

## Konfiguration

Keine neue Konfiguration erforderlich.

## Nicht-Ziele

- Aenderung der fachlichen Ausfuehrung der KI oder der CLI-Befehle
- Aenderung der Aufgabenbeschreibung oder des Inhalts neu angelegter Aufgaben
- Automatisches Loeschen des lokal geklonten Repositorys beim Stoppen der KI-Ausfuehrung
- Setzen des Gesamtstatus "Beendet" beim Stoppen oder anderweitigen Beenden der KI-Ausfuehrung
- Wiederaufnahme einer bereits beendeten KI-Ausfuehrung ohne explizite Aktion "Starten"
- Aenderung des endgueltigen Loesch- und Abschlussverhaltens der Aktion "Beenden"

## Offene Fragen

1. Welche konkrete Bezeichnung soll der neue Ausfuehrungsstatus in der Benutzeroberflaeche erhalten?
2. Welche Ansicht oder welcher Hinweis soll angezeigt werden, wenn die KI-Ausfuehrung beendet ist und die CLI deshalb nicht automatisch geoeffnet wird?
3. Soll die Aktion "Stoppen" den Ausfuehrungsstatus immer auf "beendet" setzen, oder sollen zusaetzliche Zwischenzustaende wie "pausiert" oder "abgebrochen" unterschieden werden?
4. Wie soll mit einer Aufgabe umgegangen werden, deren KI-Ausfuehrung beim Programmende aktiv war, aber beim Neustart nicht wiederhergestellt werden kann?
5. Soll die Aktion "Starten" bei einer Aufgabe mit beendetem Ausfuehrungsstatus dieselbe Beschriftung behalten oder als erneutes Starten kenntlich gemacht werden?

