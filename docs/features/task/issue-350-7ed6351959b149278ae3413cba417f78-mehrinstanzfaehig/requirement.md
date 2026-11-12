# Übersetzte Anforderung: Mehrinstanzfähigkeit der SCM-Ereignisüberwachung (GitHub Issue #350)

## Fachliche Zusammenfassung

Die Eingangsprüfung neu eingegangener SCM-Ereignisse wird um eine instanzübergreifende Duplikaterkennung erweitert: Beobachtete Kommentare, auf die mit dem konfigurierten Konto bereits remote reagiert wurde, werden als bereits bearbeitet behandelt und nicht als `ScmIncomingEvent` aufgenommen. Hintergrund ist der Parallelbetrieb einer Produktiv- und einer Vorabversion desselben Programms: Beide Instanzen führen getrennte lokale Datenbestände (Checkpoints, `ScmContentSnapshots`, `ScmIncomingEvents.EventKey`), sodass die heutige rein lokale Deduplikation ein doppeltes Verarbeiten desselben Trigger-Kommentars nicht verhindert. Als remote sichtbares Kriterium dient die vom Ereigniskanal ohnehin gesetzte Empfangsreaktion (`ScmResponseKind.Reaction`, `eyes`) bzw. allgemein eine vorhandene Reaktion des aktiven Kontos auf dem Kommentar.

## Fachlicher Scope

- **Verarbeitungseinheit:** Eine eingehende `ScmEventObservation` der Kommentar-Arten `IssueComment`, `ReviewComment` und `PullRequestReview` auf einer Issue-/Pull-Request-Referenz — also jede neu eingelesene Textversion eines Kommentars.
- **Auslösende Ereignisse:** Poll-Zyklus des `ScmEventMonitoringService` (`RunOnceAsync`): Lesen einer Quelle über `IScmEventSource.ReadEventsAsync` — im Modus `Benutzerkonto` über die repository-weite Enumeration, im Modus `SeparatesKonto` über die Notifications-Inbox; die eigentliche Kommentarprüfung ist in beiden Modi identisch.
- **Erwartete Wirkung:** Ein Kommentar, auf den das aktive Ereignis-Konto (Viewer-Identität des `ScmEventRepository`: Benutzerkonto bzw. separates Konto) bereits reagiert hat, erzeugt in der pollen Instanz kein Inbox-Ereignis, keine Aufgabenanlage über `ScmEventTaskResolver` und keine Zustellung an eine CLI. Damit wiederholt die zweite Installation eine von der ersten bereits begonnene ereignisgesteuerte Aufgabe nicht mehr. Die bestehende lokale Deduplikation (Snapshots, `EventKey`, Watermark) bleibt unverändert wirksam.
- **Umgang mit fehlenden Objekten:** Die Reaktionsinformation wird remote vom Provider mitgelesen; es ist kein lokales Objekt neu zu erzeugen oder wiederzuverwenden. Kann die Reaktionslage wegen eines Provider-Fehlers nicht ermittelt werden, gilt der übliche Fehlerpfad der Quelle (Checkpoint-Backoff) — ein Kommentar darf nicht aufgrund eines Lesefehlers stillschweigend ignoriert werden.
- **Scope-Ausschlüsse:**
  - `Description`-Beobachtungen (Issue-/PR-Text) sind ausgenommen: Auf Referenztexte wird heute keine Empfangsreaktion gesetzt (`ScmEventOrchestrator` versieht nur Nicht-`Description`-Beobachtungen mit der `receipt`-Reaktion); es gibt dort kein vergleichbares Remote-Kriterium.
  - Kein lückenloser Schutz gegen echte Gleichzeitigkeit: Lesen beide Instanzen denselben Kommentar, bevor irgendeine Instanz reagiert hat, bleibt ein Rest-Zeitfenster — die Anforderung deckt den Fall „die andere Instanz hat bereits reagiert" ab.
  - Reaktionen fremder Konten unterdrücken keine Verarbeitung; maßgeblich ist ausschließlich das eigene Ereignis-Konto.

## Betroffene Klassen und Komponenten

- `ScmEventObservation` (`src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs`) — neue Eigenschaft für die remote ermittelte Kontoreaktion (z. B. Liste der vom Viewer gesetzten Reaktionsinhalte oder ein `ViewerHasReacted`-Flag); muss abwärtskompatibel in `ScmIncomingEvent.ObservationJson` deserialisierbar bleiben.
- `GitHubPlugin` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`) — `TextFields`-GraphQL-Fragment um Reaktionsdaten erweitern (z. B. `reactionGroups { content viewerHasReacted }`; das je-Kommentar-`node`-Query existiert bereits, es entsteht kein zusätzlicher Request); `AddObservation` befüllt die neue Beobachtungseigenschaft. `viewerHasReacted` ist kontoabhängig und passt über `ClientFor(repository.Konto)` automatisch zum aktiven Modus.
- `ScmEventValidationService` (`src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`) — neuer Ablehnungsgrund in `GetRejection` (z. B. Konstante `BereitsBeantwortetRejection`, Meldung sinngemäß „Bereits mit dem Konto reagiert"), damit `ScmEventStore.AcceptBatchAsync` die Beobachtung als abgelehnten Snapshot statt als Inbox-Ereignis speichert. Alternativ kann das Plugin solche Beobachtungen bereits beim Lesen verwerfen (analog zum `<!-- softwareschmiede:`-Eigenantwort-Filter in `AddObservation`) — Entscheidung im Plan.
- `ScmEventOrchestrator.DispatchCoreAsync` (`src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`, Revalidierung ~Z. 137–141) — Behandlung des neuen Ablehnungsgrunds in der Zustellungs-Gegenprüfung klären (Toleranzliste vs. bewusstes Warten).
- `IScmEventSource` (`src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmEventSource.cs`) — voraussichtlich keine Signaturänderung, wenn das Kriterium auf `ScmEventObservation` reitet.
- Tests — `ScmEventValidationServiceTests` (neuer Ablehnungsfall plus Regressionsfälle), `GitHubEventSourceTests` (Reaktions-Mapping aus der Provider-Antwort), `ScmEventMonitoringServiceTests`/`ScmEventStore`-Tests (kein Inbox-Eintrag bei vorhandener Kontoreaktion; weiteres Einlesen unberührter Kommentare), E2E-Unterstützung in `ScmE2EApiClient`/`ScmE2EScenario` (Kommentar mit vorhandener Viewer-Reaktion) sowie eine Phase im konsolidierten SCM-E2E-Test.

## Implementierungsansatz

- Das neue Kriterium ist remote sichtbar und nicht instanzlokal: Beim Lesen jeder Kommentar-Beobachtung werden die auf dem Kommentar vorhandenen Reaktionen des aktiven Kontos mitgeladen (GitHub GraphQL `reactionGroups`/`viewerHasReacted` auf `IssueComment`, `PullRequestReviewComment` und `PullRequestReview` — alle `Reactable`).
- `ScmEventObservation` erhält eine Zusatzinformation über die Viewer-Reaktion(en); `ScmEventValidationService.GetRejection` lehnt Beobachtungen mit bereits vorhandener Kontoreaktion mit einem eigenen Grund ab. `AcceptBatchAsync` persistiert die Ablehnung als `ScmContentSnapshot.Diagnostic` (kein Inbox-Eintrag, Checkpoint/Watermark läuft normal weiter).
- Bestehende Erweiterungspunkte werden wiederverwendet: `ClientFor(repository.Konto)` liefert den kontoabhängigen Viewer, `IsViewer` das Identitätsmatching, `ScmResponseKind.Reaction`/`ReactionEndpoint` den Schreibpfad der Empfangsreaktion, die der anderen Instanz als Erkennungsmerkmal dient.

## Konfiguration

Keine — das Verhalten ist eine Korrektur der Eingangsprüfung und soll ohne Schalter gelten. Ein Konfigurationsbedarf ist aus der Anforderung nicht ableitbar.

## Offene Fragen

- Zählt als „reagiert" ausschließlich die Empfangsreaktion `eyes` des Ereigniskanals oder jede beliebige Reaktion des Kontos (eine manuell gesetzte 👍-Reaktion unter dem Benutzerkonto würde den Kommentar dann ebenfalls unterdrücken)?
- Was gilt, wenn beide Instanzen unter unterschiedlichen Kontomodi antworten (z. B. Testversion unter `SeparatesKonto`, Produktivversion unter `Benutzerkonto`)? `viewerHasReacted` erkennt nur die eigene Konto-Identität; eine Reaktion des jeweils anderen Kontos wäre damit nicht als „bereits reagiert" sichtbar — ist das akzeptabel, weil beide Installationen dasselbe Konto verwenden, oder muss die Prüfung breiter gefasst werden?
- Soll die Prüfung zusätzlich bei der Zustellungs-Gegenprüfung in `ScmEventOrchestrator.DispatchCoreAsync` remote erneuert werden, um das Rest-Zeitfenster zu schließen (beide Instanzen haben gelesen, bevor eine reagiert hat)? Derzeit prüft die Revalidierung nur den gespeicherten Observationsstand.
- Soll ein deshalb ignoriertes Ereignis eine sichtbare Spur hinterlassen (Snapshot-Diagnose analog „Kein Trigger") oder vollständig still verworfen werden?
