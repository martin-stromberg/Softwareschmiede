# Anforderung

## Fachliche Zusammenfassung

Beim Einfuegen laengerer mehrzeiliger Texte per Copy & Paste in die Pseudokonsole kommt der Inhalt nicht zuverlaessig vollstaendig an. Statt des gesamten eingefuegten Textes wird in manchen Faellen nur ein letzter Teilabschnitt in die Pseudokonsole eingetragen.

Das Problem wurde auffaellig bei Claude beobachtet. Ob es auch bei anderen CLI-Plugins oder Pseudokonsolen-Sitzungen auftritt, ist aktuell nicht bekannt. Der Fehler tritt nicht deterministisch auf.

Ein konkretes Beispiel ist das Einfuegen eines mehrzeiligen .NET-Exception-Stacktraces. Erwartet wird, dass der vollstaendige Text in der Pseudokonsole landet. Tatsächlich kommt nur der letzte Abschnitt an:

```text
ents.Rendering.ComponentState.RenderIntoBatch(RenderBatchBuilder batchBuilder, RenderFragment renderFragment, Exception& renderFragmentException)
```

## Betroffene Klassen und Komponenten

### Pseudokonsole / Terminaleingabe
- Verarbeitung von Paste-Inhalten in der Pseudokonsole
- Uebergabe von Clipboard-Text an die aktive Pseudokonsolen-Sitzung
- Behandlung mehrzeiliger und laengerer Eingaben
- Schreiblogik in den Eingabestrom der Pseudokonsole

### Terminal-UI
- Paste-Handler des Terminal-Controls
- Erfassung von Copy-&-Paste-Ereignissen
- Normalisierung von Zeilenumbruechen und Sonderzeichen vor der Uebergabe an die Pseudokonsole

### KI-/CLI-Ausfuehrung
- Claude-Integration beziehungsweise Claude-CLI-Sitzungen
- Gemeinsame Pseudokonsolen-Infrastruktur fuer andere Plugins, falls diese denselben Paste-Pfad nutzen

### Tests und Diagnose
- Reproduzierbare Tests fuer lange mehrzeilige Paste-Inhalte
- Logging oder Diagnostik fuer Paste-Laenge, uebergebene Chunks und tatsaechlich geschriebene Bytes/Zeichen

## Funktionale Anforderungen

1. Beim Einfuegen per Copy & Paste muss der vollstaendige Clipboard-Text in der aktiven Pseudokonsole ankommen.
2. Mehrzeilige Texte muessen inklusive aller Zeilen und Zeilenumbrueche uebertragen werden.
3. Lange Texte duerfen nicht auf einen letzten Teilabschnitt gekuerzt werden.
4. Das Verhalten muss fuer den beobachteten Claude-Fall korrigiert werden.
5. Der Paste-Pfad darf keine Zeichen verlieren, wenn der Text .NET-Stacktraces, Pfade, generische Typnamen, Sonderzeichen oder Klammern enthaelt.
6. Die Korrektur muss robust gegen nicht deterministische Timing-Probleme sein.
7. Falls Paste-Inhalte intern in mehrere Schreibvorgaenge aufgeteilt werden, muss die Reihenfolge erhalten bleiben und jeder Teil vollstaendig geschrieben werden.
8. Die Korrektur darf normale Tastatureingaben in der Pseudokonsole nicht verschlechtern.
9. Die Korrektur darf Copy & Paste in anderen unterstuetzten CLI-Plugins nicht regressiv beeinflussen.

## Implementierungsansatz

### Paste-Pfad nachvollziehen

Zunaechst muss der komplette Weg eines Paste-Ereignisses untersucht werden: vom Terminal-UI-Handler ueber eventuelle Normalisierung oder Chunking-Logik bis zum Schreiben in den Eingabestrom der Pseudokonsole. Dabei ist besonders zu pruefen, ob asynchrone Schreibvorgaenge nicht awaited werden, ob konkurrierende Paste- oder Eingabeereignisse vorherige Daten ueberschreiben, oder ob nur der letzte Chunk tatsaechlich an die Pseudokonsole gesendet wird.

### Vollstaendige Uebertragung sicherstellen

Die Paste-Verarbeitung soll den Clipboard-Text als stabile Momentaufnahme behandeln und vollstaendig in die aktive Pseudokonsolen-Sitzung schreiben. Wenn die Pseudokonsole grosse Eingaben nicht in einem einzelnen Schreibvorgang zuverlaessig verarbeitet, soll die Eingabe kontrolliert in Chunks geschrieben werden. Jeder Chunk muss abgeschlossen sein, bevor der naechste Chunk geschrieben wird.

Die Implementierung muss darauf achten, dass die Sitzung waehrend des Paste-Vorgangs nicht unbemerkt wechselt oder beendet wird. Fehler beim Schreiben sollen nachvollziehbar geloggt oder behandelt werden, statt still zu einem Teil-Paste zu fuehren.

### Zeilenumbrueche und Sonderzeichen erhalten

Der eingefuegte Stacktrace enthaelt Windows-Pfade, generische Typnamen mit Backticks, spitze oder eckige Klammern, Klammern, Umlaute sowie mehrere Zeilen. Diese Inhalte duerfen nicht durch Normalisierung, Escape-Handling oder Terminal-Sequenz-Verarbeitung abgeschnitten werden.

Zeilenumbrueche sollen in der fuer die Pseudokonsole erwarteten Form uebergeben werden, ohne dass dabei Zeilen wegfallen oder zusammengezogen werden.

### Tests ergaenzen

Es soll mindestens ein automatisierter Test fuer einen langen mehrzeiligen Paste-Inhalt ergaenzt werden. Der Test soll sicherstellen, dass der vollstaendige Text in der Pseudokonsole beziehungsweise im simulierten Eingabestrom ankommt und nicht nur der letzte Teil.

Der in der Anforderung enthaltene Stacktrace eignet sich als Testfall oder als Vorlage fuer einen vergleichbaren Teststring. Falls die echte Claude-CLI im Test nicht stabil automatisierbar ist, soll der gemeinsame Paste-Mechanismus mit einer kontrollierbaren Pseudokonsolen- oder Stream-Testinfrastruktur getestet werden.

## Konfiguration

Keine neue Endanwender-Konfiguration erforderlich.

Falls fuer die Diagnose temporaeres Logging eingefuehrt wird, darf dieses nicht dauerhaft stoerend oder ungefiltert in der produktiven Anwendung verbleiben.

## Nicht-Ziele

- Aenderung der fachlichen Claude-Integration ausserhalb des Paste-Problems
- Einfuehrung eines neuen Clipboard-Managers
- Veraenderung normaler Tastatureingabe-Semantik
- Vollstaendige Neuentwicklung der Pseudokonsolen-Infrastruktur
- Behebung nicht nachweislich zusammenhaengender CLI- oder Rendering-Probleme

## Offene Fragen

1. Wird der Paste-Inhalt aktuell in einem einzelnen Schreibvorgang oder in mehreren Chunks an die Pseudokonsole uebergeben?
2. Tritt der Fehler nur bei Claude auf, oder nutzen andere Plugins denselben fehlerhaften Paste-Pfad?
3. Gibt es eine maximale Eingabelaenge oder Puffergrenze in der verwendeten Pseudokonsolen- oder Terminal-Control-Implementierung?
4. Geht der Text bereits im UI-Paste-Handler verloren, oder erst beim Schreiben in den Pseudokonsolen-Eingabestrom?
5. Muss fuer bestimmte CLIs eine kurze Verzögerung oder Flusskontrolle zwischen Paste-Chunks eingehalten werden?
