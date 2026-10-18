← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Beschreibung

## Zweck

Anfragen aus GitHub-Issues und Pull Requests können einer vorhandenen Aufgabe zugeordnet und an deren KI-CLI weitergegeben werden. So lässt sich die Arbeit an einer Aufgabe aus der Diskussion heraus anstoßen und in der Softwareschmiede nachverfolgen.

## Funktionsweise

Die Anwendung prüft regelmäßig Texte in GitHub-Issues und Pull Requests. Überwacht werden repository-weit alle offenen Issues und Pull Requests jedes aktivierten Projekt-Repositories; geschlossene Referenzen kommen hinzu, sobald sie seit der letzten Prüfung Aktivität zeigen. Dazu zählen Beschreibungen, Kommentare, Review-Kommentare und veröffentlichte Pull-Request-Reviews. Ein Text wird nur angenommen, wenn er das konfigurierte Trigger-Keyword (Standard `$Softwareschmiede`, einstellbar unter **Einstellungen → Allgemein → SCM-Ereignisse**) enthält und sein Autor beziehungsweise bei einer Bearbeitung der Bearbeiter der angemeldete Repository-Owner ist. Die gesamte Überwachung lässt sich dort auch deaktivieren: Dann werden keine GitHub-Quellen abgefragt und keine Ereignisse automatisch zugestellt; bereits eingegangene Ereignisse bleiben erhalten und lassen sich weiterhin manuell öffnen, prüfen oder abbrechen.

Eingehende Texte erscheinen unter **SCM-Ereignisse**. Angenommene Kommentare und veröffentlichte Pull-Request-Reviews erhalten eine 👀-Reaktion; Beschreibungen nicht. Nach den Sicherheits- und Owner-Prüfungen wird die zugehörige Aufgabe automatisch gestartet — einschließlich der aus einem Ereignis neu angelegten Aufgaben: Die Anwendung klont das Repository, legt den Aufgaben-Branch an und startet die aufgelöste Standard-CLI (Aufgaben-, Projekt- oder globaler Standard). Wenn keine CLI zur Verarbeitung bereitsteht, bleibt der Eintrag dort sichtbar und GitHub erhält einen kurzen Wartehinweis. Die KI kann die passende Antwort im ursprünglichen Issue oder Pull Request selbst wählen.

Bei der Zustellung kann die Anwendung konfigurierte Zusatztexte an den Prompt anhängen: Unter **Einstellungen → Allgemein → SCM-Ereignisse** lassen sich je Ereignisart (Allgemein, Issue, Issue-Kommentar, Pull Request, Pull-Request-Kommentar inklusive Reviews) mehrzeilige Prompt-Anhänge hinterlegen — etwa Hinweise auf verfügbare Skills oder Bearbeitungsregeln, die nicht in jedem Kommentar wiederholt werden müssen. Ein leeres Feld hängt nichts an; die Trigger-Prüfung und der gespeicherte Ereignistext bleiben unverändert.

Für die CLI-Verarbeitung unterstützt die Anwendung aktuell Codex CLI, Claude CLI und Devin CLI nur dann, wenn deren erkannte Version, Schreibberechtigungen und tatsächlicher Zugriff auf den privaten Antwortkanal geprüft werden können. Die Anwendung ändert diese Berechtigungen nicht. Bei Devin CLI wird ein unterstützter `--permission-mode` (`accept-edits`, `dangerous` oder `bypass`) sowie `--respect-workspace-trust false` empfohlen; weitere freie Startparameter lassen die Prüfung bewusst fehlschlagen. Der KI-Simulator funktioniert nur, wenn die Anwendung ihn eigens im SCM-Modus startet. GitHub Copilot CLI und andere Plugins können derzeit nicht für diese Ereigniszustellung verwendet werden; ein Plugin in der Auswahlliste bedeutet nicht automatisch, dass es diesen Kanal unterstützt.

## Beispiele

- Ein Repository-Owner kommentiert in einem mit einer Aufgabe verknüpften Issue: „$Softwareschmiede Bitte prüfe den Fehler beim Export.“ Die Anfrage erscheint bei **SCM-Ereignisse** und kann an die passende CLI gehen.
- Ein Owner ändert die Beschreibung eines verknüpften Pull Requests und ergänzt den Trigger. Die neue Textfassung wird als Anfrage aufgenommen.
- Ist keine geeignete CLI verfügbar, bleibt der Eintrag wartend. Nach Behebung des Grundes kann er in der Ereignisübersicht erneut geprüft oder geöffnet werden.

## Einschränkungen

- Derzeit überwacht nur das GitHub-SCM-Plugin Ereignisse. Andere SCM-Plugins erhalten dadurch keine Ereignisüberwachung.
- Es werden nur Repositories überwacht, die an einem Projekt erfasst und aktiviert sind. Trifft ein qualifizierter Text an einer Referenz ohne lokale Aufgabe ein, wird die passende Aufgabe automatisch angelegt (Projekt aus dem überwachten Repository, Titel und Auftragstext aus den Ereignisdaten samt Quellverweis) und das Ereignis regulär verarbeitet. Geschlossene Referenzen werden nur dann einbezogen, wenn sie innerhalb des laufenden Aktivitätsfensters geändert wurden.
- Polling sieht den bei einer Abfrage aktuellen Textstand. Zwischenzeitlich überschriebene oder gelöschte Textfassungen können nicht rekonstruiert werden.
- Die Ereignisverarbeitung erfordert eine gültige GitHub-Anmeldung und passende Repository-Berechtigungen. Auch mit Codex oder Claude CLI kann ein Eintrag wartend bleiben, wenn Version, Berechtigungen oder Zugriff auf den geprüften Antwortkanal nicht unterstützt werden.
