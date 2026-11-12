← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Datenmodell

## Entitäten

### `ScmIncomingEvent`

Enthält den dauerhaft angenommenen Ereignistext und Ursprung, Aufgabe, Status, Wartegrund, Versuchs- und Laufkennung sowie Zustellzeiten und Wiederholungsinformationen. Der im `ObservationJson` eingebettete Beobachtungspayload trägt optional `ViewerReactions` — die Inhalte der vom lesenden Ereignis-Konto gesetzten Reaktionen auf der Textfassung (GraphQL-`ReactionContent`-Werte); sie sind die Grundlage der instanzübergreifenden Duplikaterkennung. Ältere Datensätze ohne das Feld deserialisieren zu `null` und verhalten sich unverändert.

### `ScmObservationCheckpoint`

Speichert Wasserstand, Fehlerzahl, nächste Prüfzeit und Diagnose je Quelle. `SourceKey` kennt folgende Namensräume: `repository/{provider}/{alias}` (Auflösung lokaler Repository-Adressen), `enumeration/{provider}/{host}/{repoId}` (repository-weite Referenzermittlung der Ereignisüberwachung), `notifications/{provider}/{host}` (Notifications-Inbox des separaten Kontos; nur im Überwachungsmodus `SeparatesKonto`, trägt die `Inbox:`-Verwerfungsdiagnose für nicht registrierte Repositories), `{provider}/{host}/{repoId}/{kind}/{number}/{contentKind}` (einzelne Textquellen) sowie die Backmerge-Namensräume `backmerge-repository/{prefix}/{alias}` und `backmerge-enumeration/{provider}/{host}/{repoId}` des eigenständigen Backmerge-Prüfzyklus (`ScmBackmergeMonitoringService`) und `backmerge/{provider}/{host}/{repoId}/{nummer}` (Bewertungsstand je Pull-Request-Kandidat, `ScmBackmergeService`).

### `ScmContentSnapshot`

Speichert Inhaltshash und Revision einer beobachteten Textfassung sowie den Auflösungsstatus ihrer Identität und eine optionale Diagnose — etwa den Ablehnungsgrund „Bereits mit dem Konto reagiert" für eine verworfene Kommentarversion, auf die das Ereignis-Konto bereits reagiert hat.

### `ScmOutgoingAction`

Verfolgt Empfangsreaktionen, Wartehinweise und angeforderte Antworten einschließlich Aktionstyp, Remote-ID, Versuchszahl und Fehlerstatus.

### `ScmTaskLease`

Koordiniert die exklusive Verarbeitung von Ereignissen einer Aufgabe über Worker und UI-Aktionen hinweg.

## Beziehungen

Ein Ereignis gehört fachlich zu einer Aufgabe und kann mehrere ausgehende Aktionen enthalten. Checkpoints und Snapshots sind unabhängig nach Quelle beziehungsweise Textversion indiziert. Eine Aufgaben-Lease serialisiert Ereignisse desselben Aufgabenbereichs. Die konkreten Zuordnungen und Fremdschlüssel werden in `SoftwareschmiededDbContext` konfiguriert.

Für die Abschlussüberwachung trägt die Aufgabe zwei Felder: `ScmAbschlussErkanntUtc` (UTC-Zeitpunkt, ab dem die verknüpfte SCM-Referenz erstmals geschlossen/gemergt beobachtet wurde — persistierter Beginn der Wartezeit) und `ScmAbschlussReferenz` (Anzeigebezeichnung der Referenz). Vom Agenten im Abschluss gemeldete Pull Requests (`complete.json`-Feld `pullRequestNumber`) werden als `PullRequestReferenz` mit Rolle `CreatedByTask` an der Aufgabe gespeichert.
