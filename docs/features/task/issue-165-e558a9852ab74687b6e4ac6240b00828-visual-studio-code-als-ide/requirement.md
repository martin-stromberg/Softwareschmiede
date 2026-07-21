# Anforderung

## Fachliche Zusammenfassung

Die Menüaktion "IDE öffnen" soll weiterhin bevorzugt eine gefundene Visual-Studio-Solution öffnen. Wird für eine Aufgabe keine Visual-Studio-Solution im Arbeitsverzeichnis gefunden, soll die Anwendung optional auf Visual Studio Code zurückfallen und das Arbeitsverzeichnis in VS Code öffnen.

Der Fallback auf Visual Studio Code soll über die Programmeinstellungen aktivierbar sein. Ist die Einstellung deaktiviert oder ist Visual Studio Code auf dem System nicht verfügbar, bleibt das bisherige Verhalten ohne VS-Code-Fallback erhalten.

## Betroffene Klassen und Komponenten

### UI und Menüaktionen

- Menüaktion "IDE öffnen" — Auslöser für das Öffnen der Entwicklungsumgebung zur ausgewählten Aufgabe
- ViewModel/Command-Logik der Aufgaben- oder Detailansicht — Stelle, an der die IDE-Aktion gebunden und ausgeführt wird

### IDE-Erkennung und Startlogik

- Service oder Hilfslogik zum Finden einer Visual-Studio-Solution (`*.sln`) im Arbeitsverzeichnis
- Service oder Hilfslogik zum Starten der IDE für eine Aufgabe
- Neue oder erweiterte Erkennung, ob Visual Studio Code verfügbar ist
- Prozessstart von Visual Studio Code mit dem Arbeitsverzeichnis als Ziel

### Programmeinstellungen

- Einstellungsmodell für Programmeinstellungen
- Persistenz der Programmeinstellungen
- Einstellungsoberfläche, in der der VS-Code-Fallback aktiviert oder deaktiviert werden kann

## Implementierungsansatz

### Zielverhalten

1. Die Aktion "IDE öffnen" ermittelt wie bisher das Arbeitsverzeichnis der Aufgabe.
2. Falls im Arbeitsverzeichnis eine Visual-Studio-Solution gefunden wird, wird diese wie bisher mit Visual Studio geöffnet.
3. Falls keine Visual-Studio-Solution gefunden wird, prüft die Anwendung:
   - Ist der VS-Code-Fallback in den Programmeinstellungen aktiviert?
   - Ist Visual Studio Code auf dem System verfügbar?
4. Sind beide Bedingungen erfüllt, startet die Anwendung Visual Studio Code für das Arbeitsverzeichnis.
5. Ist mindestens eine Bedingung nicht erfüllt, wird kein VS-Code-Fallback ausgeführt und das bisherige Fehler- oder Hinweisverhalten bleibt erhalten.

### VS-Code-Verfügbarkeit

Die Verfügbarkeit von Visual Studio Code sollte robust erkannt werden, ohne harte Annahmen über einen einzelnen Installationspfad zu treffen. Geeignete Prüfungen sind beispielsweise:

- `code` beziehungsweise `code.cmd` ist über `PATH` aufrufbar
- bekannte Windows-Installationspfade für Benutzer- oder Systeminstallation sind vorhanden

Der Prozessstart sollte das Arbeitsverzeichnis als Ordnerziel übergeben, nicht eine Solution-Datei:

```text
code "<arbeitsverzeichnis>"
```

Falls die Anwendung bereits eine zentrale Prozessstart- oder Tool-Erkennungslogik besitzt, soll diese erweitert statt dupliziert werden.

### Programmeinstellung

Es wird eine neue boolesche Einstellung benötigt, beispielsweise:

- `OpenVisualStudioCodeWhenNoSolutionFound`
- Standardwert: `false`

Damit ist der neue Fallback ausdrücklich opt-in und verändert das bestehende Verhalten nach einem Update nicht automatisch.

Die Einstellungsoberfläche soll diese Option unter den bestehenden Programmeinstellungen anbieten, sinngemäß mit der Beschriftung:

- "Visual Studio Code öffnen, wenn keine Visual-Studio-Solution gefunden wurde"

### Fehler- und Hinweisverhalten

Wenn der Fallback aktiviert ist, aber Visual Studio Code nicht gefunden wird, sollte die Anwendung dem Benutzer einen verständlichen Hinweis anzeigen oder das vorhandene Meldungskonzept nutzen. Die Meldung sollte klar zwischen diesen Fällen unterscheiden:

- keine Visual-Studio-Solution gefunden
- VS-Code-Fallback deaktiviert
- VS-Code-Fallback aktiviert, aber Visual Studio Code nicht verfügbar

## Konfiguration

Neue Programmeinstellung:

- Typ: `bool`
- Standardwert: `false`
- Wirkung: Aktiviert den Fallback von "IDE öffnen" auf Visual Studio Code, wenn keine Visual-Studio-Solution im Arbeitsverzeichnis der Aufgabe gefunden wird.

Die Einstellung muss persistiert und beim nächsten Programmstart wieder geladen werden.

## Akzeptanzkriterien

1. Wenn eine Visual-Studio-Solution im Arbeitsverzeichnis vorhanden ist, öffnet "IDE öffnen" weiterhin die Solution wie bisher.
2. Wenn keine Visual-Studio-Solution vorhanden ist, der VS-Code-Fallback aktiviert ist und Visual Studio Code verfügbar ist, öffnet "IDE öffnen" das Arbeitsverzeichnis in Visual Studio Code.
3. Wenn keine Visual-Studio-Solution vorhanden ist und der VS-Code-Fallback deaktiviert ist, wird Visual Studio Code nicht gestartet.
4. Wenn keine Visual-Studio-Solution vorhanden ist, der VS-Code-Fallback aktiviert ist, Visual Studio Code aber nicht verfügbar ist, wird Visual Studio Code nicht gestartet und der Benutzer erhält einen nachvollziehbaren Hinweis.
5. Die neue Einstellung ist in den Programmeinstellungen änderbar, wird gespeichert und nach einem Neustart wieder angewendet.
6. Bestehende Nutzer erhalten nach dem Update kein geändertes Verhalten, solange sie den Fallback nicht aktivieren.

## Offene Fragen

1. Soll Visual Studio Code nur über `PATH` erkannt werden, oder sollen zusätzlich bekannte Windows-Installationspfade geprüft werden?
2. Soll die Anwendung bei nicht gefundenem Visual Studio Code eine direkte Installations- oder Konfigurationshilfe anzeigen, oder genügt ein einfacher Hinweis?
3. Soll der Fallback nur für Aufgaben-Arbeitsverzeichnisse gelten, oder auch für andere Stellen, an denen "IDE öffnen" verwendet wird?
