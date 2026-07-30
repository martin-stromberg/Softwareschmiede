# Anforderung

## Fachliche Zusammenfassung

Beim Anlegen eines Issues fuer eine Aufgabe muss die Beschreibung der Aufgabe in der Anwendung bisher nachtraeglich manuell aktualisiert werden, damit sie dem Inhalt des neu angelegten Issues entspricht. Dieser manuelle Zusatzschritt erzeugt Aufwand und birgt das Risiko, dass Aufgabenbeschreibung und Issue-Inhalt auseinanderlaufen.

Der Dialog zur Issue-Anlage soll deshalb eine Option anbieten, mit der die Aufgabenbeschreibung nach erfolgreicher Issue-Erstellung automatisch aktualisiert werden kann. Wird der Dialog mit aktivierter Option bestaetigt und das Issue erfolgreich angelegt, uebernimmt die Anwendung den relevanten Inhalt des neu angelegten Issues in die Beschreibung der zugehoerigen Aufgabe. Schlaegt die Issue-Anlage fehl oder ist die Option nicht aktiviert, darf keine automatische Aktualisierung der Aufgabenbeschreibung erfolgen.

## Betroffene Klassen und Komponenten

### Aufgabenverwaltung

- Aufgabenmodell beziehungsweise Aufgaben-Persistenz mit der Beschreibung einer Aufgabe
- Services oder Commands zum Aktualisieren und Speichern einer Aufgabenbeschreibung
- Aufgaben-Detailansicht, sofern sie die aktualisierte Beschreibung unmittelbar anzeigen muss

### Issue-Anlage

- Dialog zur Anlage eines Issues fuer eine Aufgabe
- ViewModel beziehungsweise Dialog-State der Issue-Anlage
- Command oder Service, der das Issue beim angebundenen Issue-Provider erstellt
- Rueckgabe beziehungsweise Ergebnisobjekt der Issue-Erstellung mit dem tatsaechlich angelegten Issue-Inhalt

### Provider-Integration

- Bestehende Issue-Provider-Integration, insbesondere die Stelle, an der Titel und Beschreibung an den Provider uebergeben werden
- Fehlerbehandlung bei fehlgeschlagener Issue-Erstellung
- Optional vorhandene Provider-spezifische Formatierung oder Normalisierung des Issue-Texts

## Funktionale Anforderungen

1. Der Dialog zur Anlage eines Issues fuer eine Aufgabe muss eine Option zum automatischen Aktualisieren der Aufgabenbeschreibung anbieten.
2. Die Option muss vor dem Bestaetigen des Dialogs durch den Nutzer aktivierbar und deaktivierbar sein.
3. Wird der Dialog ohne aktivierte Option bestaetigt, bleibt das bestehende Verhalten unveraendert: Das Issue wird angelegt, die Aufgabenbeschreibung wird nicht automatisch geaendert.
4. Wird der Dialog mit aktivierter Option bestaetigt und das Issue erfolgreich angelegt, muss die Beschreibung der zugehoerigen Aufgabe automatisch auf den Inhalt des neu angelegten Issues aktualisiert werden.
5. Die Aufgabenbeschreibung darf nur aktualisiert werden, wenn die Issue-Anlage erfolgreich abgeschlossen wurde.
6. Schlaegt die Issue-Anlage fehl, darf die Aufgabenbeschreibung nicht automatisch geaendert werden.
7. Die automatische Aktualisierung muss persistiert werden, sodass die geaenderte Aufgabenbeschreibung nach einem Neustart der Anwendung erhalten bleibt.
8. Nach erfolgreicher automatischer Aktualisierung muss die Aufgabenoberflaeche die neue Beschreibung anzeigen oder beim naechsten Laden der Aufgabe aus der Persistenz erhalten.
9. Die Aktualisierung muss die Beschreibung der Aufgabe betreffen, fuer die das Issue angelegt wurde; andere Aufgaben duerfen nicht veraendert werden.

## Implementierungsansatz

### Dialogoption ergaenzen

Der bestehende Dialog zur Issue-Anlage soll um eine Checkbox oder eine vergleichbare binäre Option erweitert werden. Die Option sollte fachlich eindeutig formuliert sein, zum Beispiel "Aufgabenbeschreibung nach Issue-Anlage aktualisieren". Der Wert wird im Dialog-ViewModel gehalten und beim Bestaetigen zusammen mit den uebrigen Eingaben an die Issue-Anlage weitergegeben.

Der initiale Standardwert sollte konservativ gewaehlt werden. Sofern es im Bestand keine abweichende Konvention gibt, ist ein deaktivierter Standard sinnvoll, damit bestehende Nutzerablaeufe nicht ueberraschend geaendert werden.

### Erfolgsabhaengige Aktualisierung

Die Issue-Anlage soll nach erfolgreicher Provider-Rueckmeldung pruefen, ob die neue Option aktiviert wurde. Nur dann wird die Aufgabenbeschreibung aktualisiert und gespeichert.

Als Quelle fuer die neue Aufgabenbeschreibung soll der Inhalt verwendet werden, der fuer das Issue erstellt wurde beziehungsweise nach erfolgreicher Anlage als Issue-Beschreibung feststeht. Wenn der Provider die Beschreibung veraendert oder normalisiert zurueckliefert, ist in der Bestandsaufnahme zu klaeren, ob die Anwendung den lokal gesendeten Text oder den vom Provider bestaetigten Text uebernehmen soll.

### Persistenz und UI-Aktualisierung

Die Aktualisierung soll ueber den bestehenden Aufgaben-Speicherpfad erfolgen, nicht ueber eine separate Sonderpersistenz. Dadurch bleiben bestehende Mechanismen fuer Aenderungsverfolgung, Aktualisierung der Detailansicht und Speicherung konsistent.

Wenn die Aufgaben-Detailansicht waehrend der Issue-Anlage geoeffnet ist, sollte sie nach der erfolgreichen Aktualisierung den neuen Beschreibungstext anzeigen. Falls im Bestand bereits ein Reload- oder Notification-Mechanismus fuer Aufgabenaktualisierungen existiert, soll dieser genutzt werden.

### Fehlerbehandlung

Wenn die Issue-Anlage erfolgreich ist, aber die anschliessende Aufgabenaktualisierung fehlschlaegt, muss der Fehler sichtbar behandelt werden. Die Issue-Erstellung darf dadurch nicht rueckgaengig gemacht werden, da das externe Issue bereits angelegt ist. Die Anwendung sollte dem Nutzer melden, dass das Issue erstellt wurde, die Aufgabenbeschreibung aber nicht automatisch gespeichert werden konnte.

## Konfiguration

Keine globale Konfiguration erforderlich.

Die neue Option ist eine Dialogentscheidung pro Issue-Anlage. Falls der Bestand bereits nutzerspezifische Dialogpraeferenzen speichert, kann in der Planung geprueft werden, ob der zuletzt verwendete Wert wiederhergestellt werden soll. Dies ist jedoch kein Muss der Anforderung.

## Nicht-Ziele

- Automatische Aktualisierung der Aufgabenbeschreibung bei bereits bestehenden Issues
- Synchronisation zwischen Aufgabe und Issue nach der initialen Issue-Anlage
- Automatisches Zurueckschreiben spaeterer Issue-Aenderungen in die Aufgabe
- Aenderung der fachlichen Regeln fuer die Issue-Erstellung selbst
- Unterstuetzung neuer Issue-Provider

## Offene Fragen

1. Soll die Checkbox standardmaessig aktiviert oder deaktiviert sein?
2. Soll die Aufgabenbeschreibung mit dem lokal eingegebenen Issue-Text oder mit der vom Provider nach erfolgreicher Anlage zurueckgemeldeten Issue-Beschreibung aktualisiert werden?
3. Soll der zuletzt verwendete Checkbox-Wert als Nutzerpraeferenz gespeichert werden?
4. Wie soll die Anwendung reagieren, wenn das Issue erfolgreich angelegt wurde, aber das Speichern der Aufgabenbeschreibung fehlschlaegt?
