← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Beschreibung

## Zweck

Anfragen aus GitHub-Issues und Pull Requests können einer vorhandenen Aufgabe zugeordnet und an deren KI-CLI weitergegeben werden. So lässt sich die Arbeit an einer Aufgabe aus der Diskussion heraus anstoßen und in der Softwareschmiede nachverfolgen.

## Funktionsweise

Die Anwendung prüft regelmäßig Texte in GitHub-Issues und Pull Requests. Überwacht werden repository-weit alle offenen Issues und Pull Requests jedes aktivierten Projekt-Repositories; geschlossene Referenzen kommen hinzu, sobald sie seit der letzten Prüfung Aktivität zeigen. Dazu zählen Beschreibungen, Kommentare, Review-Kommentare und veröffentlichte Pull-Request-Reviews. Ein Text wird nur angenommen, wenn er `$Softwareschmiede` enthält und sein Autor beziehungsweise bei einer Bearbeitung der Bearbeiter der angemeldete Repository-Owner ist.

Eingehende Texte erscheinen unter **SCM-Ereignisse**. Angenommene Kommentare und veröffentlichte Pull-Request-Reviews erhalten eine 👀-Reaktion; Beschreibungen nicht. Wenn keine CLI zur Verarbeitung bereitsteht, bleibt der Eintrag dort sichtbar und GitHub erhält einen kurzen Wartehinweis. Die KI kann die passende Antwort im ursprünglichen Issue oder Pull Request selbst wählen.

Für die CLI-Verarbeitung unterstützt die Anwendung aktuell Codex CLI und Claude CLI nur dann, wenn deren erkannte Version, Schreibberechtigungen und tatsächlicher Zugriff auf den privaten Antwortkanal geprüft werden können. Die Anwendung ändert diese Berechtigungen nicht. Der KI-Simulator funktioniert nur, wenn die Anwendung ihn eigens im SCM-Modus startet. GitHub Copilot CLI, Devin und andere Plugins können derzeit nicht für diese Ereigniszustellung verwendet werden; ein Plugin in der Auswahlliste bedeutet nicht automatisch, dass es diesen Kanal unterstützt.

## Beispiele

- Ein Repository-Owner kommentiert in einem mit einer Aufgabe verknüpften Issue: „$Softwareschmiede Bitte prüfe den Fehler beim Export.“ Die Anfrage erscheint bei **SCM-Ereignisse** und kann an die passende CLI gehen.
- Ein Owner ändert die Beschreibung eines verknüpften Pull Requests und ergänzt den Trigger. Die neue Textfassung wird als Anfrage aufgenommen.
- Ist keine geeignete CLI verfügbar, bleibt der Eintrag wartend. Nach Behebung des Grundes kann er in der Ereignisübersicht erneut geprüft oder geöffnet werden.

## Einschränkungen

- Derzeit überwacht nur das GitHub-SCM-Plugin Ereignisse. Andere SCM-Plugins erhalten dadurch keine Ereignisüberwachung.
- Es werden nur Repositories überwacht, die an einem Projekt erfasst und aktiviert sind. Texte an Referenzen ohne zuordenbare lokale Aufgabe werden zwar erkannt und gespeichert, erscheinen aber derzeit nicht als zustellbare Einträge in der Queue — erst nach Verknüpfung mit einer Aufgabe werden sie wirksam. Geschlossene Referenzen werden nur dann einbezogen, wenn sie innerhalb des laufenden Aktivitätsfensters geändert wurden.
- Polling sieht den bei einer Abfrage aktuellen Textstand. Zwischenzeitlich überschriebene oder gelöschte Textfassungen können nicht rekonstruiert werden.
- Die Ereignisverarbeitung erfordert eine gültige GitHub-Anmeldung und passende Repository-Berechtigungen. Auch mit Codex oder Claude CLI kann ein Eintrag wartend bleiben, wenn Version, Berechtigungen oder Zugriff auf den geprüften Antwortkanal nicht unterstützt werden.
