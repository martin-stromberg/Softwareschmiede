# Kundenanforderung - Arbeitsverzeichnis fuer BitBucket

## Fachliche Zusammenfassung

In der Projektkonfiguration kann fuer SCM-Plugins ein Arbeitsverzeichnis innerhalb des Remote-Repositories ausgewaehlt werden. Fuer das GitHub-SCM-Plugin funktioniert diese Auswahl bereits, weil die Verzeichnisstruktur aus dem Remote-Repository ausgelesen und als Auswahl angeboten wird.

Beim BitBucket-SCM-Plugin wird aktuell nur der Eintrag `.` angeboten. Dadurch koennen Benutzer kein Unterverzeichnis des BitBucket-Repositories als Arbeitsverzeichnis auswaehlen, obwohl diese Funktion fachlich analog zu GitHub erwartet wird.

Das BitBucket-SCM-Plugin muss die Verzeichnisstruktur des Remote-Repositories auslesen und der bestehenden Arbeitsverzeichnis-Auswahl bereitstellen. Zusaetzlich muss ein robuster Fallback eingefuehrt werden: Wenn die Verzeichnisstruktur nicht abgerufen werden kann, darf die Projektanlage oder -bearbeitung nicht blockieren. Statt der Auswahlbox muss dann ein normales Eingabefeld angeboten werden, ueber das das Arbeitsverzeichnis manuell angegeben werden kann.

## Betroffene Klassen und Komponenten

### SCM-Plugins

- **BitBucket-SCM-Plugin**: Muss die Remote-Verzeichnisstruktur aus dem BitBucket-Repository abrufen koennen.
- **GitHub-SCM-Plugin**: Dient als bestehende Referenzimplementierung fuer das Abrufen und Bereitstellen der Remote-Verzeichnisstruktur.
- **Gemeinsame SCM-Plugin-Schnittstellen**: Sind betroffen, falls das Abrufen von Arbeitsverzeichnis-Kandidaten ueber ein gemeinsames Interface oder Modell abstrahiert ist.

### Projektkonfiguration und UI

- **Arbeitsverzeichnis-Auswahl in der Projektkonfiguration**: Muss fuer BitBucket dieselbe Funktionalitaet wie fuer GitHub erhalten.
- **Fallback-Darstellung fuer das Arbeitsverzeichnis**: Muss zwischen Auswahlbox und Texteingabefeld wechseln koennen, abhaengig davon, ob Remote-Verzeichnisse erfolgreich geladen wurden.
- **Validierung und Speicherung des Arbeitsverzeichnisses**: Muss sowohl ausgewaehlte Werte aus der Liste als auch manuell eingegebene Pfade akzeptieren.

### Remote-Repository-Zugriff

- **BitBucket-API-Zugriff**: Muss die Verzeichnisstruktur eines Repositories abrufen, inklusive relevanter Authentifizierung, Branch-/Ref-Auswahl und Fehlerbehandlung.
- **Fehlerbehandlung beim Remote-Abruf**: Muss erkennen, ob die Verzeichnisstruktur nicht geladen werden kann, und diesen Zustand an die UI weitergeben.

## Implementierungsansatz

### BitBucket-Verzeichnisstruktur auslesen

- Analysiere die bestehende GitHub-Implementierung fuer die Arbeitsverzeichnis-Auswahl und uebertrage das fachliche Verhalten auf das BitBucket-SCM-Plugin.
- Implementiere im BitBucket-SCM-Plugin das Abrufen der Verzeichnisstruktur aus dem Remote-Repository.
- Stelle sicher, dass mindestens `.` und alle relevanten Unterverzeichnisse als Arbeitsverzeichnis-Kandidaten geliefert werden.
- Behandle typische Fehlerfaelle wie fehlende Berechtigung, nicht erreichbare API, ungueltiges Repository, fehlender Branch oder API-Rate-Limits kontrolliert.

### Gemeinsames Verhalten fuer SCM-Plugins

- Nutze bestehende gemeinsame Schnittstellen oder Modelle fuer Arbeitsverzeichnis-Kandidaten, falls vorhanden.
- Falls GitHub aktuell eine plugin-spezifische Sonderloesung nutzt, pruefe, ob die Logik so erweitert werden kann, dass BitBucket dieselbe UI-Funktionalitaet ohne Duplikation erhaelt.
- Das Verhalten der Projektkonfiguration soll unabhaengig vom SCM-Anbieter konsistent sein.

### Fallback auf manuelle Eingabe

- Fuehre einen Fallback-Zustand ein, der aktiv wird, wenn keine Verzeichnisstruktur aus dem Remote-Repository geladen werden kann.
- In diesem Zustand wird statt der Auswahlbox ein normales Eingabefeld fuer das Arbeitsverzeichnis angezeigt.
- Das Eingabefeld muss mindestens den Wert `.` zulassen und manuell eingegebene relative Unterverzeichnisse speichern koennen.
- Bereits gespeicherte manuelle Werte duerfen nicht verloren gehen, wenn ein spaeterer Remote-Abruf fehlschlaegt.
- Der Fallback soll auch fuer andere SCM-Plugins nutzbar sein, sofern deren Verzeichnisstruktur-Abruf fehlschlaegt.

## Akzeptanzkriterien

1. Bei BitBucket-Repositories wird die Remote-Verzeichnisstruktur geladen und in der Arbeitsverzeichnis-Auswahl angeboten.
2. Die Auswahl fuer BitBucket enthaelt nicht nur `.`, sofern das Repository Unterverzeichnisse besitzt.
3. Das bestehende GitHub-Verhalten bleibt unveraendert funktionsfaehig.
4. Wenn die Verzeichnisstruktur nicht abgerufen werden kann, wird ein Texteingabefeld statt der Auswahlbox angezeigt.
5. In diesem Fallback kann ein Arbeitsverzeichnis manuell eingegeben und gespeichert werden.
6. Ein manuell eingegebenes Arbeitsverzeichnis wird beim erneuten Oeffnen der Projektkonfiguration korrekt angezeigt.
7. Fehler beim Abrufen der Remote-Verzeichnisstruktur fuehren nicht zum Abbruch der Projektanlage oder Projektbearbeitung.

## Konfiguration

Es ist keine neue produktive Konfiguration vorgesehen. Bestehende BitBucket-Zugangsdaten, Repository-Informationen und Branch-/Ref-Einstellungen sollen fuer den Abruf der Verzeichnisstruktur wiederverwendet werden.

## Offene Fragen

1. Welche BitBucket-Variante wird unterstuetzt: Bitbucket Cloud, Bitbucket Server/Data Center oder beide?
2. Welcher Branch oder Ref soll fuer das Auslesen der Verzeichnisstruktur verwendet werden, falls das Projekt mehrere Branches kennt?
3. Soll der Fallback nur bei technischen Fehlern greifen oder auch dann, wenn das Repository leer ist beziehungsweise keine Unterverzeichnisse enthaelt?
4. Muss die manuelle Eingabe validiert werden, oder soll jeder relative Pfad akzeptiert werden?
5. Soll dem Benutzer im Fallback-Zustand eine Fehlermeldung angezeigt werden, die erklaert, warum die Auswahlbox nicht verfuegbar ist?
