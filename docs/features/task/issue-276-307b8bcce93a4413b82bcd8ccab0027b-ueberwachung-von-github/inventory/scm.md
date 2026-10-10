# SCM- und GitHub-Bestand

## Plugin-Verträge und Anbieter

`IGitPlugin` ist der zentrale Vertrag für SCM-Anbieter. Er enthält Repository-Operationen, `GetIssuesAsync` und `GetOpenPullRequestsAsync`, aber keine Methoden oder Ereignisverträge für Webhooks, Issue-/PR-Kommentare oder Reaktionen. `GitPluginBase<TPlugin>` stellt die gemeinsame Implementierung und optionale Issue-Funktionen bereit.

Implementierungen sind unter anderem `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` und `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`. GitHub verwendet `gh` und `git`; das Token kommt aus dem Credential Store. Der PluginManager erfasst Plugins anhand der Plugin-Verträge und bietet ein Standard-SCM-Plugin. Diese Struktur eignet sich als Integrationspunkt, bietet aktuell aber keinen gemeinsamen Ereignisprozess.

## Bestehende Issue-/PR-Funktionen

Das GitHub-Plugin listet Issues und offene Pull Requests und implementiert Status-/Workflow-Abfragen sowie Issue- und PR-Schreiboperationen. Diese Funktionen werden über explizite Anwendungsabläufe verwendet. Es ist kein Hintergrunddienst erkennbar, der eingehende Kommentare oder Änderungen von Issue-Beschreibungen abonniert.

`PullRequestMonitoringService` ist ein vorhandener `PeriodicBackgroundService`. Er holt fällige, bereits lokal gespeicherte `PullRequestReferenz`-Einträge und fragt beim Provider Status und Workflow-Runs ab. `PullRequestReferenzService` persistiert dabei Monitoring-Phasen. Diese Infrastruktur belegt ein Muster für periodische Provider-Abfragen, deckt aber weder Event-Eingang noch Triggererkennung oder Antwortkommentare ab.

## Datenmodell und Zuordnung

Aufgaben können mit einem Git-Repository, einer `IssueReferenz` und mehreren `PullRequestReferenz`-Datensätzen verbunden sein. Die Referenzen enthalten lokale Provider- und Nummern-/Repository-Identifikatoren. Das bietet einen möglichen Weg, ein Remote-Issue oder einen PR einer bestehenden Aufgabe zuzuordnen. Eine Abbildung eingehender SCM-Ereignisse oder eine gesonderte Zuordnung über Owner/Repository/Issue-Nummer ist nicht erkennbar.

## Tests und Lücken

Es existieren Tests für Plugin-Funktionen und PR-Monitoring, unter anderem `PullRequestMonitoringServiceTests`; ein Ereignis-, Kommentar- oder Reaktionsworkflow ist durch diese Tests nicht abgedeckt. Für die neue Anforderung sind getrennte Tests für providerneutrale Trigger-/Owner-Validierung und GitHub-spezifische API-Integration sinnvoll.

Offen für die Planung sind insbesondere Empfangsmechanismus (Webhook oder Polling), Signatur-/Authentifizierungsmodell, Deduplizierung, Beschreibungstext-Ereignisse und die API-Abbildung von Repository-Owner zu authentifiziertem Akteur.
