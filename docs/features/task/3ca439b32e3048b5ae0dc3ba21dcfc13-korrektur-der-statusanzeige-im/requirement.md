# Anforderung

## Fachliche Zusammenfassung

Die Statusanzeige aktiver Aufgaben im Programmmenue muss korrigiert werden. Aktuell wird dort in bestimmten Situationen der Status "Bereit" angezeigt, obwohl die zugehoerige CLI tatsaechlich arbeitet. Beim Oeffnen derselben Aufgabe zeigt die Fusszeile korrekt "Ausfuehrung laeuft" an.

Die Anzeige im Programmmenue muss denselben fachlichen Laufzustand widerspiegeln wie die Detailansicht beziehungsweise Fusszeile der Aufgabe. Eine aktive CLI-Ausfuehrung darf im Programmmenue nicht als "Bereit" dargestellt werden.

## Betroffene Klassen und Komponenten

### Programmmenue
- Auflistung aktiver Aufgaben im Programmmenue
- Statusanzeige einzelner aktiver Aufgaben im Menue
- Aktualisierung der Menueeintraege bei Statusaenderungen laufender Aufgaben

### Aufgaben- und Laufstatusmodell
- Datenmodell oder ViewModel fuer aktive Aufgaben
- Laufstatus der CLI-Ausfuehrung einer Aufgabe
- Ableitung des angezeigten Status aus Aufgabenstatus, Run-Informationen und CLI-Laufstatus

### Aufgaben-Detailansicht und Fusszeile
- Statusanzeige in der geoeffneten Aufgabe
- Fusszeilentext "Ausfuehrung laeuft"
- Bestehende Logik, die den korrekten laufenden Zustand bereits erkennt

## Funktionale Anforderungen

1. Das Programmmenue muss fuer aktive Aufgaben den tatsaechlichen aktuellen Laufstatus anzeigen.
2. Wenn die CLI fuer eine Aufgabe arbeitet, darf das Programmmenue fuer diese Aufgabe nicht "Bereit" anzeigen.
3. Der Status im Programmmenue muss konsistent zur Statusanzeige in der geoeffneten Aufgabe sein.
4. Der Status im Programmmenue muss konsistent zur Fusszeile sein, insbesondere zum Zustand "Ausfuehrung laeuft".
5. Statusaenderungen einer aktiven Aufgabe muessen im Programmmenue zeitnah aktualisiert werden.
6. Die Korrektur darf die bestehende Anzeige von tatsaechlich bereiten Aufgaben nicht verschlechtern.
7. Die Korrektur muss auch fuer Aufgaben funktionieren, die bereits im Programmmenue sichtbar sind, bevor ihr Laufstatus aktualisiert wird.

## Implementierungsansatz

### Statusquelle vereinheitlichen

Die Logik fuer die Statusanzeige im Programmmenue soll daraufhin geprueft werden, ob sie eine andere oder unvollstaendige Statusquelle nutzt als die Detailansicht beziehungsweise Fusszeile. Falls die Detailansicht den korrekten Zustand bereits ermittelt, sollte das Programmmenue dieselbe Quelle, denselben ViewModel-Wert oder dieselbe Ableitungslogik verwenden.

### Laufenden CLI-Zustand korrekt ableiten

Die Anzeige "Bereit" darf nur verwendet werden, wenn fuer die Aufgabe keine aktive CLI-Ausfuehrung laeuft. Existiert ein aktiver Run oder ein Laufstatus, der eine laufende Ausfuehrung beschreibt, muss daraus ein laufender Status fuer das Programmmenue abgeleitet werden.

Besonders zu pruefen sind Zustaende, in denen initiale oder zwischengespeicherte Werte im Menue noch "Bereit" enthalten, waehrend die Aufgabe selbst bereits eine laufende CLI-Ausfuehrung hat.

### Aktualisierung im Programmmenue sicherstellen

Falls das Programmmenue gebundene ViewModels oder gecachte Aufgabenlisten verwendet, muessen Statusaenderungen korrekt propagiert werden. Dazu gehoert insbesondere, dass Property-Change-Benachrichtigungen, Reload-Mechanismen oder Event-Handler den Menueeintrag aktualisieren, sobald der Laufstatus der Aufgabe geaendert wird.

## Konfiguration

Keine neue Konfiguration erforderlich.

## Nicht-Ziele

- Aenderung der CLI-Ausfuehrungslogik selbst
- Aenderung der Fusszeilenanzeige, sofern diese bereits korrekt ist
- Neue Statuswerte oder neue Statusbegriffe einfuehren
- Umgestaltung des Programmmenues ausserhalb der fehlerhaften Statusanzeige
- Aenderung der Aufgabenverwaltung ausserhalb der Anzeige- und Aktualisierungslogik

## Offene Fragen

1. Welche technische Statusquelle verwendet die Fusszeile fuer "Ausfuehrung laeuft"?
2. Welche technische Statusquelle verwendet das Programmmenue aktuell fuer "Bereit"?
3. Tritt die falsche Anzeige nur beim Start einer CLI-Ausfuehrung auf oder auch nach Statuswechseln waehrend eines laufenden Runs?
4. Soll im Programmmenue exakt derselbe Text wie in der Fusszeile angezeigt werden oder ein menuespezifischer Kurzstatus?
5. Gibt es automatisierte Tests fuer Statuswechsel aktiver Aufgaben im Programmmenue?
