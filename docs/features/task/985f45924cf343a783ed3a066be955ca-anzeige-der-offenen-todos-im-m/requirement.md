# Anforderung

## Fachliche Zusammenfassung

Im Programmmenue werden aktive Aufgaben aufgelistet. In dieser Auflistung soll fuer jede Aufgabe zusaetzlich sichtbar sein, wie viele Todos dieser Aufgabe noch unerledigt sind.

Die Anzahl soll als neues anklickbares Label direkt bei der jeweiligen aktiven Aufgabe angezeigt werden. Ein Klick auf dieses Label soll ein Fenster oeffnen, das die offenen Todos der ausgewaehlten Aufgabe anzeigt.

## Betroffene Klassen und Komponenten

### Programmmenue
- Auflistung aktiver Aufgaben im Programmmenue
- Darstellung einzelner aktiver Aufgaben im Menue
- Neues Label fuer die Anzahl offener Todos je Aufgabe
- Klick-Interaktion auf dem neuen Label

### Aufgaben- und Todo-Daten
- Datenmodell oder ViewModel der aktiven Aufgaben
- Zugriff auf Todos einer Aufgabe
- Filterung unerledigter Todos
- Berechnung der Anzahl offener Todos

### Dialog- oder Fensterlogik
- Neues Fenster oder Dialog zur Anzeige offener Todos
- Uebergabe der ausgewaehlten Aufgabe an das Fenster
- Anzeige der gefilterten offenen Todos
- Leerzustand, falls keine offenen Todos vorhanden sind

## Funktionale Anforderungen

1. In der Auflistung aktiver Aufgaben im Programmmenue muss je Aufgabe die Anzahl unerledigter Todos angezeigt werden.
2. Die angezeigte Anzahl muss auf den Todos der jeweiligen Aufgabe basieren.
3. Erledigte Todos duerfen nicht in die Anzahl einfliessen.
4. Die Anzahl muss als neues Label dargestellt werden.
5. Das neue Label muss anklickbar sein.
6. Ein Klick auf das Label muss ein Fenster fuer die jeweilige Aufgabe oeffnen.
7. Das Fenster muss die offenen Todos der ausgewaehlten Aufgabe anzeigen.
8. Werden fuer eine Aufgabe keine offenen Todos gefunden, muss das Fenster einen nachvollziehbaren Leerzustand anzeigen.
9. Die Anzeige im Programmmenue muss mit bestehenden Aktualisierungen der aktiven Aufgaben konsistent bleiben.

## Implementierungsansatz

### Offene Todos ermitteln

Die bestehende Datenbasis der Aufgaben muss um die fuer das Programmmenue benoetigten Todo-Informationen erweitert oder aus bereits geladenen Daten abgeleitet werden. Fuer jede aktive Aufgabe wird die Menge der Todos gefiltert, deren Status nicht erledigt ist. Aus dieser Menge wird die Anzahl offener Todos berechnet.

Falls die aktiven Aufgaben im Programmmenue ueber ein ViewModel dargestellt werden, sollte dort eine Eigenschaft fuer die Anzahl offener Todos bereitgestellt werden. Dadurch bleibt die Anzeige an die bestehende UI-Bindung angebunden und kann bei Aktualisierungen der Aufgabe oder Todos automatisch neu bewertet werden.

### Label im Programmmenue anzeigen

Die Vorlage oder Komponente fuer einzelne aktive Aufgaben im Programmmenue wird um ein neues Label erweitert. Das Label zeigt die Anzahl offener Todos an und muss visuell als interaktives Element erkennbar sein.

Die Beschriftung sollte auch bei `0` offenen Todos eindeutig bleiben, zum Beispiel durch eine Zahl oder eine kurze Todo-bezogene Kennzeichnung. Ob das Label bei `0` offenen Todos sichtbar bleibt oder deaktiviert wird, muss im bestehenden Bedienkonzept konsistent entschieden werden.

### Fenster mit offenen Todos oeffnen

Der Klick auf das Label ruft eine bestehende Dialog- oder Fenster-Infrastruktur auf oder fuehrt ein neues kleines Fenster ein. Das Fenster erhaelt die betroffene Aufgabe oder deren ID und zeigt nur die offenen Todos dieser Aufgabe an.

Das Fenster sollte mindestens den Todo-Text anzeigen. Falls im Datenmodell vorhanden und im bestehenden UI-Stil ueblich, koennen weitere Informationen wie Prioritaet, Erstellzeitpunkt oder Faelligkeit angezeigt werden.

## Konfiguration

Keine neue Konfiguration erforderlich.

## Nicht-Ziele

- Todos im neuen Fenster bearbeiten, erledigen oder loeschen
- Neue Todos aus dem Fenster heraus erstellen
- Aenderung der fachlichen Todo-Statuslogik
- Aenderung der bestehenden Navigation im Programmmenue ausserhalb des neuen Labels
- Anzeige erledigter Todos im neuen Fenster

## Offene Fragen

1. Soll das Label auch angezeigt und anklickbar sein, wenn eine Aufgabe `0` offene Todos hat?
2. Soll das Fenster rein lesend sein, oder sollen offene Todos dort direkt als erledigt markiert werden koennen?
3. Welche Todo-Informationen sollen im Fenster neben dem Todo-Text angezeigt werden?
4. Soll die Anzahl offener Todos live aktualisiert werden, wenn sich Todos im Hintergrund aendern?
5. Soll das Fenster modal sein oder parallel zur Hauptanwendung offen bleiben koennen?
