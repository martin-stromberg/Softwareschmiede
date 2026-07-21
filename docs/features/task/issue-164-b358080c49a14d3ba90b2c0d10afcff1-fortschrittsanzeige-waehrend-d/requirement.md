# Anforderung

## Fachliche Zusammenfassung

Beim Starten einer Aufgabe wird das zugehoerige Repository lokal vorbereitet bzw. geklont. Dieser Vorbereitungszustand soll fuer den Anwender sichtbar werden, damit waehrend dieser Phase nicht der Eindruck entsteht, die Anwendung reagiere nicht.

Mindestens muss in der Fusszeile der Statustext `Bereit Repository vor...` angezeigt werden, solange die lokale Repository-Vorbereitung laeuft. Wenn der technische Ablauf Fortschrittsinformationen bereitstellt oder mit vertretbarem Aufwand ableitbar macht, soll in der Fusszeile zusaetzlich der aktuelle Fortschritt angezeigt werden.

## Betroffene Klassen und Komponenten

- Aufgabenstart bzw. Startlogik fuer die lokale Repository-Vorbereitung
- SCM-/Repository-Service, der das Repository lokal vorbereitet oder klont
- UI-Status-/Footer-Komponente, die globale Statusmeldungen anzeigt
- Zustandsmodell oder ViewModel, ueber das laufende Arbeits-/Statusmeldungen an die UI gebunden werden
- Optional: Fortschrittsmodell fuer Clone-/Repository-Operationen, falls bereits vorhanden oder durch den SCM-Client unterstuetzt

## Implementierungsansatz

- Beim Start der Repository-Vorbereitung wird ein global sichtbarer Status gesetzt.
- Der Status lautet mindestens exakt `Bereit Repository vor...`.
- Nach Abschluss, Abbruch oder Fehler der Repository-Vorbereitung wird der Status wieder geloescht oder durch den nachfolgenden passenden Status ersetzt.
- Falls der verwendete Clone-/Repository-Mechanismus Fortschrittsereignisse bereitstellt, werden diese in den Footer-Status integriert.
- Falls keine belastbaren Fortschrittswerte verfuegbar sind, wird nur der Mindeststatus angezeigt; es soll kein ungenauer oder irrefuehrender Prozentwert simuliert werden.
- Fehler in der Repository-Vorbereitung duerfen durch die Fortschrittsanzeige nicht verdeckt werden.

## Akzeptanzkriterien

- Wenn eine Aufgabe gestartet wird und das Repository lokal vorbereitet wird, erscheint in der Fusszeile der Text `Bereit Repository vor...`.
- Der Status bleibt waehrend der Repository-Vorbereitung sichtbar.
- Nach Ende der Repository-Vorbereitung verschwindet der Status oder wird durch den naechsten fachlich korrekten Status ersetzt.
- Falls Fortschrittsdaten verfuegbar sind, zeigt die Fusszeile den aktuellen Fortschritt der Vorbereitung an.
- Die Anzeige funktioniert auch bei laenger dauernden Clone-/Vorbereitungsablaeufen.
- Fehler- und Abbruchfaelle hinterlassen keinen dauerhaft falschen Footer-Status.

## Konfiguration

Keine neue Benutzerkonfiguration erforderlich.

## Offene Fragen

- Welche konkrete Komponente fuehrt das lokale Klonen bzw. die Repository-Vorbereitung aus?
- Gibt der eingesetzte SCM-/Git-Client bereits Clone-Fortschritt weiter, oder ist nur ein textueller Aktivitaetsstatus moeglich?
- Gibt es bereits einen zentralen Footer-/Statusdienst, der fuer solche globalen Arbeitszustaende verwendet werden soll?
