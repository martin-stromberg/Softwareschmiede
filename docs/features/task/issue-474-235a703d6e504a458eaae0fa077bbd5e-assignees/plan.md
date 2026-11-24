# Umsetzungsplan: Assignee-Auslöser für SCM-Ereignisse (Issue #474)

## Übersicht

Im Modus `ScmAccountMode.SeparateAccount` wird die Notifications-Inbox des separaten Kontos um einen zweiten Auslöser erweitert: `reason = assign` (das überwachte Konto wurde als Assignee gesetzt) löst für Issues und Pull Requests die bestehende Ereignisverarbeitung aus — mit der Referenzbeschreibung als Auftragsinhalt, ohne Erwähnungs-/Keyword-Pflicht. Neu hinzukommen: die Zuweisender-Ermittlung über die Issue-Events-API des Providers (neue Plugin-Fähigkeit `IScmAssignmentSource`), ein assign-bewusster Validierungspfad (leere Beschreibung, berechtigter Zuweisender), eine zuweisungsspezifische Ereignisidentität (damit die `BodyHash`-Dedup eine erneute Zuweisung nicht verschluckt) sowie ein fester Assignee-Herkunftshinweis im zugestellten Prompt. Keine UI-Änderung, keine Migration, keine neue Konfiguration.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Eine `assign`-Notification der Inbox des separaten Kontos auf einer `ScmEventReference` (`Issue` oder `PullRequest`) eines registrierten Repositories, samt abgeleiteter `Description`-Beobachtung als Auftragsinhalt und dem Akteur der jüngsten wirksamen Viewer-Zuweisung (`ScmAssignment`). |
| Auslösende Ereignisse | `Reason = "assign"` + `Kind = Issue`; `Reason = "assign"` + `Kind = PullRequest`; erneute Zuweisung derselben Referenz (neues `assigned`-Event) löst eine erneute Verarbeitung aus. Eine vor der Verarbeitung wieder aufgehobene Zuweisung (`unassigned` auf den Viewer jünger als das letzte `assigned`) zählt nicht als auslösendes Ereignis — sie wird verworfen (Anwenderentscheidung 2026-11-23). Die Zuweisung wird nur akzeptiert, wenn der Zuweisende ∈ {Repository-Owner (`OwnerId`/`Owner`), überwachtes Konto selbst (`ViewerId`/`ViewerLogin`), `TrustedAutomationLogins` (`github-actions[bot]`, `app/github-actions`)} — verbindliche Entscheidung aus dem Issue-Kommentar. |
| Erwartete Wirkung | Ohne lokale Aufgabe: genau eine Aufgabe via `ScmEventTaskResolver` (`CreateFromIssueEventAsync`/`CreateFromPullRequestAsync`), Beschreibung = Auftragsinhalt. Bei bestehender Aufgabe: Wiederverwendung, neues Ereignis wird als Prompt zugestellt (keine Doppelanlage). Der zugestellte Prompt enthält erkennbar, dass er durch eine Assignee-Zuweisung entstanden ist. Verwerfungen (fremder Zuweisender, leere Beschreibung, Fremd-Repository, nicht bestimmbarer Zuweisender) bleiben nachvollziehbar: spezifische `ScmContentSnapshot.Diagnostic` (persistiert, DB-Ebene — analog `AlreadyReactedRejection`, ohne eigene UI-Oberfläche) plus `Inbox:`-präfixierte Diagnose auf dem Description-Quellen-Checkpoint (sichtbar in den Integrationshinweisen, ohne Monitoring-Alarm). |
| Umgang mit fehlenden Objekten | Aufgabe fehlt → über `ScmEventTaskResolver` erzeugen (Bestand). Aufgabe vorhanden → wiederverwenden, Prompt zustellen (Bestand). Beschreibung leer/nur Leerraum → Ereignis verwerfen, `AssignEmptyDescriptionRejection`-Diagnose (neu). Zuweisender nicht bestimmbar (Plugin ohne `IScmAssignmentSource`, kein wirksames `assigned`-Event auf den Viewer oder zwischenzeitlich aufgehobene Zuweisung) → verwerfen, `AssignerUnknownRejection`-Diagnose (neu). Zuweisender nicht im erlaubten Kreis → verwerfen, `AssignerNotAllowedRejection`-Diagnose (neu). Repository nicht registriert → verwerfen mit `Inbox:`-Zählerdiagnose (Bestand). |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Der Auslöser gilt ausschließlich im Modus `SeparateAccount`; im `UserAccount` bleibt das Keyword der einzige Trigger | Verbindliche Entscheidung aus dem Issue-Kommentar; der Inbox-Kanal existiert nur im `SeparateAccount` | bestätigt (Issue-Kommentar) |
| 2 | Unassign löst kein Ereignis aus und bricht keine Aufgabe ab | Verbindliche Entscheidung aus dem Issue-Kommentar; `unassign` erzeugt für das eigene Konto keine Inbox-Notification | bestätigt (Issue-Kommentar) |
| 3 | Weitere Notification-Reasons (`subscribed`, `review_requested`, `ci_activity` u. a.) bleiben wirkungslos | Unverändertes Verhalten des Inbox-Filters | bestätigt (Issue-Kommentar) |
| 4 | Kein separater Schalter und keine UI: der Auslöser folgt `scm.monitoring.accountmode` | Anforderung: keine neue Konfiguration; er ist nicht separat abschaltbar | bestätigt (Issue-Kommentar) |
| 5 | Der auslösende Text muss weder Trigger-Keyword noch `@`-Erwähnung enthalten — gilt nur für den assign-Auslöser, nicht für andere Inhalte derselben Referenz | Verbindliche Entscheidung; Kommentar-/Review-Beobachtungen der Referenz folgen weiter den Mention-Regeln | bestätigt (Issue-Kommentar) |
| 6 | Keine Kommentar-Antwort auf der Referenz bei Assign-Ablehnungen | Es gibt keinen Kommentar-Anker für eine Zuweisung; Diagnose läuft über Snapshot/Checkpoint (Annahme aus `requirement.md`, Frage 5) | bestätigt (Planung) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Zuweisender-Ermittlung (Vertrag) | Neue optionale Fähigkeit `IScmAssignmentSource` mit `GetLatestViewerAssignmentAsync(ScmEventReference, CancellationToken) → Task<ScmAssignment?>` | Die Notifications-API liefert den Zuweisenden nicht; eine eigene Fähigkeit hält `IScmNotificationSource`/`IScmEventSource` unverändert, lässt Nicht-GitHub-Plugins opt-in bleiben und erlaubt eine ehrliche Degradation (ohne Fähigkeit → „Zuweisender nicht bestimmbar") statt Zwangsimplementierung |
| Zuweisender-Ermittlung (GitHub) | REST `GET repos/{owner}/{name}/issues/{n}/events?per_page=100` via `RestPagesAsync` + `ClientFor(ScmAccountMode.SeparateAccount)` | Der Issue-Events-Endpunkt deckt Issues und PRs gleichermaßen ab (PRs sind Issues in der REST-API), liefert `event`, `actor`, `assignee`, `created_at`, `id`, `node_id` seitenweise — kein Preview-Header, keine GraphQL-Timeline-Union nötig; vorhandene Transport-Helfer (Pagination, Rate-Limit-Gates, Fehlerklassifizierung) greifen unverändert |
| Wirksamkeit der Zuweisung | Maßgeblich ist das jüngste `assigned`-Event mit `assignee` = Viewer (`IsViewer`-Vergleich auf `node_id`/`login`); liegt danach ein `unassigned`-Event mit `assignee` = Viewer, gilt die Zuweisung als aufgehoben → `null` (Anwenderentscheidung 2026-11-23: verwerfen) | Konsistent mit „jüngste Zuweisung maßgeblich" (Anforderung, Frage 3), der Unassign-Regel und der bestätigten Entscheidung, eine zwischenzeitlich aufgehobene Zuweisung nicht mehr auszulösen; Zuweisungen an Dritte sind für die Inbox ohnehin unsichtbar |
| Auslöseherkunft auf der Beobachtung | `ScmEventObservation` erhält optionales Feld `ScmEventTrigger? Trigger` (`Reason`, `ActorId`, `ActorLogin`, `EventNodeId`, `OccurredAt`); für den assign-Pfad `Reason = ScmNotification.AssignReason` | Validierung, Store-Revalidierung und Prompt-Zusammensetzung benötigen Herkunft und Zuweisender serialisiert an der Beobachtung; additives JSON-Feld ist abwärtskompatibel (Altbestand: `null` = regulärer Texttrigger) und setzt kein neues `ScmContentKind` voraus — ein neuer Enum-Wert würde `DispatchCoreAsync` (Receipt-Reaktion), `ScmPromptAppendix.SpecificKeyFor` und alle `ContentKind`-Switches berühren |
| Zuweisungsspezifische Ereignisidentität (Dedup) | Der Host dekoriert die `Description`-Beobachtung: `RemoteNodeId = {descriptionNodeId}/assign-{eventNodeId}` | `ContentKey`/`EventKey` enthalten damit die stabile Identität des `assigned`-Events: dieselbe Zuweisung dedupliziert korrekt (gleicher `ContentKey` + `BodyHash`), eine erneute Zuweisung erzeugt ein neues `ScmIncomingEvent`, und die normale `Description`-Snapshot-Dedup des Textes bleibt unberührt. Bei nicht bestimmbarer Zuweisung fester Fallback-Suffix `assign-unbestimmt` (deterministisch, dedup-sicher) |
| Dekorationsort | `ScmEventMonitoringService.RunOnceAsync` dekoriert die `Description`-Beobachtungen assign-abgeleiteter Referenzen (per `with`-Ausdruck); das Plugin liefert unverändert die rohe Textversion | `IScmEventSource.ReadEventsAsync` bleibt trigger-agnostisch; die Auslösesemantik ist Host-Wissen (Inbox-Reason), nicht Provider-Wissen |
| Rejection-Diagnose ohne Aufgabe (Snapshot) | `ScmEventStore.AcceptBatchAsync` erhält optionalen Parameter `Func<ScmEventObservation, string?>? unassignedRejection`; der assign-Pfad übergibt `x => ScmEventValidationService.GetRejection(x, triggerKeyword)` | Heute bekommen Beobachtungen ohne Aufgabe pauschal `UnassignedDiagnostic` — ein fremder Zuweisender wäre sonst nicht als solcher diagnostizierbar. Die Erweiterung ist additiv und ändert das Verhalten aller übrigen Pfade nicht |
| Rejection-Diagnose ohne Aufgabe (Checkpoint/UI) | `AcceptBatchAsync` hebt im assign-Pfad (`unassignedRejection` übergeben, `workItemId == null`, `unassigned == true`) die erste konkrete Ablehnung des Batches zusätzlich als `Inbox:`-präfixierte `Diagnostic` auf den Quellen-Checkpoint (`{InboxDiscardDiagnosticPrefix} {rejection}`); lehnt keine Beobachtung ab, bleibt `UnassignedDiagnostic` | `ScmContentSnapshot.Diagnostic` hat keine UI-Oberfläche: `ScmEventQueueViewModel` (Z. 191–204) aggregiert ausschließlich `ScmObservationCheckpoints.Diagnostic` und filtert `UnassignedDiagnostic` heraus — die Snapshot-Ablehnung allein wäre wie `AlreadyReactedRejection` heute nur DB-nachvollziehbar. Die `Inbox:`-Präfixierung macht die Verwerfung in den Integrationshinweisen sichtbar, ohne den Monitoring-Alarm auszulösen (`alertErrors` blendet `Inbox:`-Diagnosen aus — Routineverwerfung, kein Ausfall) |
| Vorrang bei Mehrdeutigkeit | `unassignedRejection` greift nur bei `unassigned == true`; bei `workItemId == null` durch mehrdeutige Zuordnung bleibt `AmbiguousAssignmentDiagnostic` für Snapshot und Checkpoint führend | Ein lokales Zuordnungsproblem (mehrere Kandidaten-Aufgaben) hat Vorrang vor der fachlichen Ablehnung — selbst eine berechtigte Zuweisung wäre nicht eindeutig zustellbar; Bestandsemantik bleibt unverändert |
| Leerer Description-Batch bei assign-Referenz | Verwerfen wie eine leere Beschreibung: `AcceptBatchAsync` setzt bei `unassignedRejection != null` + `workItemId == null` + `unassigned` + `batch.Observations.Count == 0` die Checkpoint-Diagnose `Inbox: {AssignEmptyDescriptionRejection}` | Ohne Beobachtung entsteht weder Snapshot noch Ereignis — die assign-Notification würde sonst ohne jede Diagnose still versanden (z. B. vom Provider übersprungene Textversion). Eine nicht lesbare Textversion ist fachlich einer leeren Beschreibung gleichgestellt (kein Auftragsinhalt ableitbar). Wirft der Provider stattdessen einen typisierten Fehler, läuft unverändert der Backoff-Pfad |
| Assignee-Herkunftshinweis | Fester Text (Konstante) wird bei `ScmExchangeRequest.Body` in `ScmEventOrchestrator.DispatchCoreAsync` vorangestellt, wenn `observation.Trigger?.Reason == ScmNotification.AssignReason` | Der Hinweis gehört zum Ereignisauftrag (request.json), nicht zum konfigurierbaren `promptSuffix`; gespeicherter Ereignistext, `BodyHash`-Dedup und Trigger-Gegenprüfung bleiben frei davon; Wiederanlauf-Bitgleichheit bleibt gewahrt (Anforderung Frage 4: nur im Prompt bzw. im zugestellten Auftrag) |
| Mention-/Assign-Vorrang je Referenz | In `EnumerateNotificationsAsync` werden `assign`-Benachrichtigungen vor `mention` dedupliziert (`seen`), sodass die assign-Notification die `ResolvedReference.Notification` bestimmt | Bei gleichzeitigem Vorliegen ist die Zuweisung das stärkere Delegationssignal; Mention-Kommentare derselben Referenz bleiben über die Kommentar-Batches regulär trigger-fähig |
| `TrustedAutomationLogins` | Bleibt `private`; Wiederverwendung innerhalb von `ScmEventValidationService` | Die Assign-Akteursprüfung lebt in derselben Klasse und braucht keine Öffnung der Sichtbarkeit |

## Programmabläufe

### Assign-Notification → Aufgabe → Zustellung (Happy Path)

1. `RunOnceAsync` (`SeparateAccount`, Plugin ist `IScmNotificationSource`): `EnumerateNotificationsAsync` liest die Inbox, filtert auf `Reason ∈ {mention, assign}` (assign zuerst dedupliziert), mappt `RepositoryNodeId` gegen die Allowlist und legt `ResolvedReference(null, reference, null, notification)` an.
2. In der Referenzgruppen-Schleife gilt `assignNotification = group…Notification?.Reason == AssignReason`; beim `Description`-Batch wird — sofern `source is IScmAssignmentSource` — `GetLatestViewerAssignmentAsync(reference, ct)` aufgerufen (Fehler → `RecordFailureAsync` auf den Description-`sourceKey`, Backoff).
3. Die Beobachtungen des Batches werden dekoriert: `RemoteNodeId += "/assign-" + (assignment?.EventNodeId ?? "unbestimmt")`, `Trigger = new ScmEventTrigger(AssignReason, assignment?.AssignerId, assignment?.AssignerLogin, assignment?.EventNodeId, assignment?.OccurredAt)`.
4. Trigger-Suche: `batch.Observations.FirstOrDefault(GetRejection is null)` — `GetRejection` verzweigt bei `Trigger.Reason == assign` in die Assign-Regeln (Beschreibung nicht leer, Zuweisender bestimmbar und berechtigt). Qualifizierte Beobachtung → `ScmEventTaskResolver.ResolveAsync` → Anlage (kein Treffer) oder Wiederverwendung (bestehende Aufgabe über `IssueSourceKey`/`PullRequestTaskIdsAsync`).
5. `AcceptBatchAsync(workItemId, sourceKey, batch, unassigned, triggerKeyword, delayKeyword, ct, unassignedRejection)` speichert Snapshot + `ScmIncomingEvent` (EventKey enthält die Zuweisungsidentität); bei `workItemId == null` trägt der Snapshot die spezifische Assign-Ablehnung und der Quellen-Checkpoint die `Inbox:`-präfixierte Ablehnungsdiagnose.
6. `DispatchDueEventsAsync` → `DispatchCoreAsync`: keine `eyes`-Reaktion (`ContentKind == Description`), Revalidation via `GetRejection` (Assign-Regeln am persistierten `Trigger`), `ScmExchangeRequest.Body = AssigneePromptPrefix + observation.Body`, `PrepareAsync`/`WritePromptAsync` → Zustellung an die CLI-Sitzung der Aufgabe.

Beteiligte Klassen/Komponenten: `ScmEventMonitoringService`, `IScmNotificationSource`, `IScmAssignmentSource`, `ScmEventValidationService`, `ScmEventTaskResolver`, `ScmEventStore`, `ScmEventOrchestrator`, `ScmEventExchangeProtocol`.

### Erneute Zuweisung an dieselbe Referenz

1. GitHub meldet die erneute Zuweisung erneut als `assign`-Thread (`updated_at` vorgerückt → Eintrag liegt im `since`-Fenster); dieselbe `ResolvedReference` entsteht.
2. `GetLatestViewerAssignmentAsync` liefert das **neue** `assigned`-Event (neue `EventNodeId`).
3. Die dekorierte Beobachtung erhält einen neuen `ContentKey`/`EventKey` → neue Snapshot-Zeile, kein Dedup-Verschlucken trotz unverändertem Beschreibungstext.
4. `ResolveAsync` findet die bestehende Aufgabe (`IssueSourceKey`/PR-Referenzen) → `Assigned(existingTaskId)` ohne Doppelanlage.
5. `AcceptBatchAsync` legt ein zweites `ScmIncomingEvent` auf derselben `WorkItemId` an; die bestehende Reihenfolge-Sperre (frühere nicht-finale Ereignisse derselben Aufgabe) gilt unverändert.
6. Der Prompt enthält erneut den Assignee-Hinweis.

Beteiligte Klassen/Komponenten: `ScmEventMonitoringService`, `IScmAssignmentSource`, `ScmEventTaskResolver`, `ScmEventStore`, `ScmEventOrchestrator`.

### Verwerfungsfälle

1. **Fremd-Repository:** `RepositoryNodeId` ohne Allowlist-Treffer → `discarded++`, `Inbox:`-Zählerdiagnose (unverändert).
2. **Leere Beschreibung:** dekorierte Beobachtung → `GetRejection` = `AssignEmptyDescriptionRejection` → kein Resolver-Aufruf, Snapshot-Diagnose via `unassignedRejection` + `Inbox:`-Diagnose auf dem Description-Checkpoint, Watermark läuft weiter.
3. **Zuweisender nicht bestimmbar:** Plugin ohne `IScmAssignmentSource` oder `GetLatestViewerAssignmentAsync` = `null` — das umfasst den entschiedenen Fall „assign → unassign vor der Verarbeitung" (jüngstes `unassigned` auf den Viewer nach dem letzten `assigned` → kein gültiges aktuelles assigned-Ereignis → kein Trigger) — → `Trigger` ohne Akteur → `AssignerUnknownRejection` (Snapshot + `Inbox:`-Checkpoint-Diagnose).
4. **Zuweisender nicht berechtigt:** `AssignerNotAllowedRejection` (Snapshot + `Inbox:`-Checkpoint-Diagnose).
5. **Leerer Description-Batch:** Provider liefert für die Referenz keine Beobachtung (keine lesbare Textversion) → kein Snapshot möglich → `Inbox: {AssignEmptyDescriptionRejection}` auf dem Description-Checkpoint; wirft der Provider einen typisierten Fehler, läuft der Backoff-Pfad (`RecordFailureAsync` auf den `sourceKey`).
6. **Mehrdeutige Aufgabenzuordnung:** `unassigned == false` bei `workItemId == null` → `AmbiguousAssignmentDiagnostic` bleibt für Snapshot und Checkpoint führend (Bestand); die assign-Ablehnung tritt dahinter zurück.
7. **Mention-Pfad unverändert:** `mention`-Benachrichtigungen und alle Nicht-Description-Batches laufen über die bestehenden Trigger-/Akteursregeln.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `IScmAssignmentSource` | Interface (`src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmAssignmentSource.cs`) | Optionale Plugin-Fähigkeit: liefert die jüngste wirksame Zuweisung des Viewer-Kontos auf einer Referenz samt Zuweisendem und Ereignisidentität |
| `ScmAssignment` | Record (in `ScmEvents.cs`) | Ergebnis der Zuweisungsabfrage: `EventNodeId` (string), `OccurredAt` (`DateTimeOffset`), `AssignerId`/`AssignerLogin` (string?), `AssigneeId`/`AssigneeLogin` (string?) |
| `ScmEventTrigger` | Record (in `ScmEvents.cs`) | Serialisierte Auslöseherkunft einer Beobachtung: `Reason` (string), `ActorId`/`ActorLogin` (string?), `EventNodeId` (string?), `OccurredAt` (`DateTimeOffset?`) |

## Änderungen an bestehenden Klassen

### `ScmNotification` (Datenmodellklasse, `ScmEvents.cs`)

- **Neue Konstante:** `AssignReason = "assign"` — zweiter auslösender Reason der Inbox.

### `ScmEventObservation` (Datenmodellklasse, `ScmEvents.cs`)

- **Neue Eigenschaft:** `Trigger` (`ScmEventTrigger?`, optionaler Positionsparameter am Ende, Default `null`) — Auslöseherkunft; `null` = regulärer Texttrigger (Bestand). Serialisiert in `ObservationJson`; abwärtskompatibel.

### `ScmEventValidationService` (Service)

- **Neue Konstanten:**
  - `AssignEmptyDescriptionRejection` = `"Referenzbeschreibung ist leer"` — assign-ausgelöste Beobachtung ohne Auftragsinhalt
  - `AssignerUnknownRejection` = `"Zuweisender nicht bestimmbar"` — kein wirksames `assigned`-Event / Fähigkeit fehlt
  - `AssignerNotAllowedRejection` = `"Zuweisung nicht durch berechtigtes Konto"` — Zuweisender ∉ {Owner, Viewer, TrustedAutomationLogins}
- **Geänderte Methoden:** `GetRejection` — frühe Verzweigung `observation.Trigger is { Reason: ScmNotification.AssignReason } trigger` → neuer privater Prüfschritt `GetAssignRejection(observation, trigger)`: 1) `string.IsNullOrWhiteSpace(observation.Body)` → `AssignEmptyDescriptionRejection`; 2) `Trigger.ActorId`/`ActorLogin` beide leer → `AssignerUnknownRejection`; 3) Akteur weder `SameIdentity` zu `OwnerId`/`Owner` noch zu `ViewerId`/`ViewerLogin` noch `IsTrustedAutomationLogin` → `AssignerNotAllowedRejection`; 4) `<!-- softwareschmiede:`-Marker → `"Eigene Antwort"`; sonst `null`. Die Texttrigger-Prüfung (Keyword/`@mention`) und die `UserAccount`-Owner-Regel entfallen im Assign-Pfad; `AlreadyReactedRejection` bleibt Kommentar-Arten vorbehalten.
- Keine Änderung an `TrustedAutomationLogins` (bleibt `private`).

### `ScmEventMonitoringService` (Service)

- **Geänderte Typen:** `ResolvedReference` erhält Feld `ScmNotification? Notification = null`.
- **Geänderte Methoden:**
  - `EnumerateNotificationsAsync`: Filter `x.Reason == MentionReason` → `x.Reason is MentionReason or AssignReason`; Assign-Einträge werden vor Mention dedupliziert (Assign-Vorrang); `resolved.Add` trägt die Notification; Zählerdiagnose läuft über die erweiterte relevante Menge.
  - `RunOnceAsync`: je Referenzgruppe `assignNotification` ermitteln; im `Description`-Batch — falls `assignNotification` gesetzt — `IScmAssignmentSource.GetLatestViewerAssignmentAsync` aufrufen (innerhalb des bestehenden `try` → `RecordFailureAsync`/`sourceKey`-Backoff) und den Batch dekorieren; `AcceptBatchAsync` erhält für diesen Batch `unassignedRejection: x => ScmEventValidationService.GetRejection(x, triggerKeyword)`. Trigger-Suche, Resolver-Aufruf, Watermark-Commits und Dispatch bleiben unverändert.
- Kein `inboxMode`-/Modus-Change — der Assign-Auslöser existiert strukturell nur im `SeparateAccount`.

### `ScmEventStore` (Service)

- **Geänderte Methoden:** `AcceptBatchAsync` — neuer optionaler Parameter `Func<ScmEventObservation, string?>? unassignedRejection = null` (Position hinter `delayKeyword`, `ct` bleibt letzter Parameter). Bei `workItemId == null` gilt als Snapshot-Diagnose: `unassigned == true` → `unassignedRejection?.Invoke(observation) ?? UnassignedDiagnostic`; `unassigned == false` (Mehrdeutigkeit) → unverändert `AmbiguousAssignmentDiagnostic`. Die Checkpoint-Diagnose hebt die assign-Ablehnung in die Integrationshinweise: bei `workItemId == null && unassigned && unassignedRejection != null` gilt `checkpoint.Diagnostic = InboxDiscardDiagnosticPrefix + " " + reason`, wobei `reason` die erste nicht-leere Ablehnung des Batches ist, bei `batch.Observations.Count == 0` `ScmEventValidationService.AssignEmptyDescriptionRejection` und bei Beobachtungen ohne Ablehnung `UnassignedDiagnostic`. Bestehende Aufrufer unverändert.

### `ScmEventOrchestrator` (Service)

- **Neue Konstante:** `internal const string AssigneePromptPrefix` — fester Hinweis (z. B. „Dieses Ereignis wurde durch eine Assignee-Zuweisung auf das überwachte Konto ausgelöst.").
- **Geänderte Methoden:** `DispatchCoreAsync` — bei der `ScmExchangeRequest`-Erzeugung `Body = observation.Trigger?.Reason == ScmNotification.AssignReason ? AssigneePromptPrefix + "\n\n" + observation.Body : observation.Body`; Revalidation-Wartegrund für Assign-Ablehnungen auf einen assign-sprechenden Text anpassen (z. B. „Der Zuweisende ist nicht mehr dem berechtigten Kreis zuzuordnen."), Toleranz für `Kein Trigger`/`NoMentionRejection` unverändert.

### `GitHubPlugin` (Plugin, `GitHubPlugin.Events.cs` / `GitHubPlugin.cs`)

- **Interface-Liste:** `IScmAssignmentSource` ergänzen.
- **Neue Methoden:** `GetLatestViewerAssignmentAsync(ScmEventReference reference, CancellationToken ct) → Task<ScmAssignment?>`:
  - `ValidateReference`; `client = ClientFor(reference.Repository.Account)` (im `SeparateAccount` der Agent-Client).
  - `RestPagesAsync` auf `{RepositoryEndpoint(repository)}/issues/{reference.Number}/events?per_page=100` (gilt für Issues und Pull Requests).
  - Aus den Events `assigned`/`unassigned` mit `assignee` = Viewer (`IsViewer(repository, assignee.node_id, assignee.login)`): jüngstes nach `created_at` (Tie-Break `id`) bestimmen; `unassigned` jünger als das jüngste `assigned` bzw. kein `assigned` → `null`.
  - Treffer → `ScmAssignment(node_id, created_at, actor.node_id, actor.login, assignee.node_id, assignee.login)`; Akteur aus `actor` (Fallback `assigner`).
  - Transportfehler laufen typisiert über `SendEventApiAsync`/`EvaluateResponse`.

### `ScmDispatchFixture` (Test-Infrastruktur)

- `Git.As<IScmAssignmentSource>()`-Setup wird **nicht** im Konstruktor vorbelegt (Fehlen der Fähigkeit muss testbar sein); neue Hilfsmethoden:
  - `ConfigureAssignmentSource(ScmAssignment? assignment)` — liefert die konfigurierte Zuweisung für jede Referenz.
  - `ConfigureDescriptionObservations(string body, string? authorId = "U_1", string? authorLogin = "owner")` — `ReadEventsAsync` liefert für `ContentKind.Description` eine Beobachtung mit gegebenem Text (ein `Setup`, das die anderen Kinds leer beantwortet; komponierbar mit `ConfigureMentionObservations`, da letzteres `ReadEventsAsync` komplett neu setzt — Aufrufreihenfolge dokumentieren).
  - `ConfigurePullRequestRead(...)` — `ReadPullRequestAsync` liefert einen Standard-PR (analog `ConfigureIssue`), sofern nicht schon durch bestehende Task-Assignment-Tests abgedeckt.

### `ScmE2EScenario` / `ScmE2EApiClient` / `ScmE2EFixture` (E2E-Infrastruktur)

- `ScmE2EScenario` erhält `List<ScmE2EAssignment> Assignments`; neuer Record `ScmE2EAssignment(int Number, string EventNodeId, string EventType, string AssignerLogin, string AssignerNodeId, string AssigneeLogin, string AssigneeNodeId, DateTimeOffset CreatedAt)` (`EventType` = `"assigned"`/`"unassigned"`).
- `ScmE2EApiClient`: `GET repos/scm-owner/scm-e2e/issues/{n}/events` aus dem Szenario beantworten — JSON-Array mit `event` (`EventType`), `actor` (`AssignerNodeId`/`AssignerLogin`), `assignee` (`AssigneeNodeId`/`AssigneeLogin`), `created_at` (`CreatedAt`), `id` (laufender Index als Tie-Break) und `node_id` (`EventNodeId`); ungefiltert — die Viewer-Filterung liegt im Plugin. Der Endpunkt gilt per REST-Semantik gleichermaßen für Issue- und PR-Referenzen.
- `ScmE2EFixture.AddAssignment(int number, string assignerLogin, string assignerNodeId, string eventNodeId, string eventType = "assigned", string assigneeLogin = "scm-bot", string assigneeNodeId = "BOT", DateTimeOffset? createdAt = null)` — `createdAt` steuert die Jüngst-Regel bei mehreren Zuweisungsereignissen (erneute Zuweisung, assign → unassign).
- `ScmE2EFixture.AddText` erhält optionalen Parameter `bool pullRequest = false` (bislang hartkodiert `number is 7 or 9`), damit assign-Phasen explizite PR-Beschreibungen setzen können; der synthesisierte Fixture-Fallback „Beschreibung ohne Trigger" (`ScmE2EApiClient`, PR-Beschreibung) bleibt zusätzlich nutzbar.

### Hilfedokumentation (keine Klasse)

- `docs/help/plugins/github-ereignisse/business-rules.md` (Z. 49–51), `ablauf-technisch.md` (Z. 21), `index.md`, `installation.md`, `troubleshooting.md` (Z. 78), `ereignisablaeufe.md`: `reason = assign` als zweiten Inbox-Auslöser dokumentieren — Beschreibung als Auftragsinhalt, Zuweisender-Regel (Owner/separates Konto/Automatisierungskonten), leere Beschreibung → keine Aufgabe, erneute Zuweisung → erneute Verarbeitung auf derselben Aufgabe, Prompt-Hinweis.

## Datenbankmigrationen

Keine. `ScmEventObservation.Trigger` lebt in `ObservationJson` (kein neues Schema); `ScmContentSnapshot`, `ScmIncomingEvent`, `ScmObservationCheckpoint` bleiben unverändert.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `assign`-Notification | `RepositoryNodeId` muss auf ein registriertes Repository desselben Hosts matchen | Verworfen + `Inbox:`-Zählerdiagnose |
| `ScmAssignment` (Plugin-Ergebnis) | Jüngstes `assigned`-Event mit `assignee` = Viewer; späteres `unassigned` auf den Viewer hebt es auf | `null` → `AssignerUnknownRejection` |
| `Trigger.ActorId`/`ActorLogin` | Mindestens eines bestimmbar | `AssignerUnknownRejection` |
| Zuweisender | `SameIdentity` zu `OwnerId`/`Owner` **oder** `ViewerId`/`ViewerLogin` **oder** `IsTrustedAutomationLogin` | `AssignerNotAllowedRejection` |
| `Description`-Body (assign) | Nicht leer/nur Leerraum | `AssignEmptyDescriptionRejection`; keine Aufgabenanlage — Schnittstelle zieht den Wertebereich nicht enger als der Domain-Vertrag |
| `ScmEventTrigger.Reason` | `"assign"` aktiviert den Assign-Prüfpfad; sonst Bestandspfad | — |

## Konfigurationsänderungen

Keine. Der Auslöser folgt `scm.monitoring.accountmode` (`SeparateAccount`).

## Seiteneffekte und Risiken

- **Inbox-Diagnose:** `assign`-Notifications zählen künftig als „verarbeitet" bzw. „verworfen" — E2E-/Integrations-Assertions auf die Zählertexte sind zu prüfen.
- **Assign-Verwerfungsdiagnose auf dem Quellen-Checkpoint:** `Inbox:`-präfixierte Ablehnungen (`Inbox: Zuweisung nicht durch berechtigtes Konto` u. a.) erscheinen in den Integrationshinweisen (nicht-alarmierend) und persistieren auf dem Description-Checkpoint, bis derselbe `sourceKey` erneut verarbeitet wird — fachlich gewollt, da die Ablehnung sonst unsichtbar bliebe; keine Auswirkung auf den Monitoring-Alarm (`alertErrors` blendet `Inbox:` aus).
- **`SinceUtc`-Rücksetzung:** verworfene assign-Beobachtungen behalten `IdentityResolved = false` → die `SinceUtc = null`-Rücksetzung liest die Description-Quelle bei jedem Poll vollständig neu (vorhandenes Verhalten, geringer Mehraufwand, kein Korrekturbedarf).
- **Zusätzlicher REST-Call je assign-Referenz und Zyklus:** solange die Notification im `since`-Fenster liegt (mit 2-h-Sicherheitsrand), wird `issues/{n}/events` erneut gelesen — Rate-Limit-Verhalten wie bei den übrigen Polls (typisierte Gates).
- **Snapshot-Zuwachs:** jede Zuweisung erzeugt eine eigene `ScmContentSnapshot`-Zeile (`assign-{eventNodeId}`-Namensraum) — klein und absichtlich, damit Wiederholungen deduplizieren.
- **`AcceptBatchAsync`-Signatur:** additives optionales Argument — bestehende Aufrufer unverändert; Missbrauchsrisko gering (nur der Assign-Batch übergibt `unassignedRejection`).
- **`Trigger`-Serialisierung:** `ObservationJson` des Bestands ohne Feld → `null`; alte Hosts lesen neue Felder tolerant (Web-Defaults ohne `Disallow`), neue Hosts alte Events ohne `Trigger` im Bestandspfad.
- **Revalidation-Toleranz:** Assign-Ablehnungen bei der Zustellrevalidierung blockieren das Ereignis (konservativ korrekt bei z. B. Owner-Wechsel); der Wartegrund muss assign-sprechend formuliert sein, damit die Meldung verständlich bleibt.
- **`ScmE2EApiClient`:** ohne den neuen `issues/{n}/events`-Endpunkt wirft die Fixture `InvalidOperationException` — die Erweiterung ist Pflichtteil, kein optionaler Testausbau.
- **Besuchte Fehlerklassen:** Schlägt die Events-Abfrage fehl, geht der Description-`sourceKey` in den Backoff; der Inbox-Watermark bleibt stehen (idempotent, bestehendes Verhalten).

## Umsetzungsreihenfolge

1. **Vertragsebene:** `ScmNotification.AssignReason`, `ScmAssignment`, `ScmEventTrigger`, `ScmEventObservation.Trigger`, `IScmAssignmentSource`
   - Voraussetzungen: keine
   - Beschreibung: Konstante und Records in `ScmEvents.cs`; Interface-Datei neu anlegen.

2. **Assign-Validierung in `ScmEventValidationService`**
   - Voraussetzungen: Schritt 1
   - Beschreibung: drei Rejection-Konstanten + `GetRejection`-Verzweigung + privater `GetAssignRejection`-Schritt.

3. **`ScmEventStore.AcceptBatchAsync` um `unassignedRejection` erweitern**
   - Voraussetzungen: keine
   - Beschreibung: optionaler Parameter; Snapshot-Fallback `unassignedDiagnostic`/`AmbiguousAssignmentDiagnostic` je `unassigned`-Flag; `Inbox:`-präfixierte Checkpoint-Diagnose aus Ablehnung bzw. `AssignEmptyDescriptionRejection` bei leerem Batch (setzt die Konstante aus Schritt 2 voraus — Implementierungsdetail: Konstanten können vorgezogen oder mitgeschrieben werden).

4. **`ScmEventMonitoringService`:** `ResolvedReference.Notification`, Inbox-Filter/-Reihenfolge, Dekoration und Zuweisungs-Lookup in `RunOnceAsync`
   - Voraussetzungen: Schritte 1–3
   - Beschreibung: assign-Referenzen in Read-/Resolve-/Accept-Block einspeisen; `unassignedRejection` für den Description-Batch setzen.

5. **Assignee-Hinweis im Prompt (`ScmEventOrchestrator`)**
   - Voraussetzungen: Schritt 1
   - Beschreibung: `AssigneePromptPrefix`-Konstante; `Body`-Voranstellung in `DispatchCoreAsync`; assign-sprechender Revalidierungs-Wartegrund.

6. **`GitHubPlugin`:** `IScmAssignmentSource` + `GetLatestViewerAssignmentAsync`
   - Voraussetzungen: Schritt 1
   - Beschreibung: REST `issues/{n}/events`, Viewer-Filter, jüngste wirksame Zuweisung.

7. **Test-Infrastruktur:** `ScmDispatchFixture`-Helfer, `ScmE2EScenario`/`ScmE2EAssignment`, `ScmE2EApiClient`-Endpunkt, `ScmE2EFixture.AddAssignment`
   - Voraussetzungen: Schritte 1, 6
   - Beschreibung: Mocks und Fixture-Endpunkte für die neue Fähigkeit.

8. **Unit-Tests:** `ScmEventValidationServiceTests` (Assign-Fälle) + neue `GitHubAssignmentSourceTests` (Events-Mapping, Viewer-Filter, unassigned-Aufhebung, typisierte Fehler)
   - Voraussetzungen: Schritte 2, 6, 7
9. **Integrationstests:** `ScmEventMonitoringServiceTests_Assignments` (oder Erweiterung von `…_Notifications`) — inkl. `…_AssignmentQueryFailure_KeepsInboxWatermarkForRetry` (Backoff/Watermark am neuen Aufrufort) und `…_AssignEmptyDescriptionBatch_Discarded`
   - Voraussetzungen: Schritte 4, 5, 7
10. **E2E:** `BotModeAsync` in `E2E_ScmEvents` um assign-Phasen erweitern: Issue #21 positiv (Bestands-`assign`-Notification wird jetzt verarbeitet), Pull Request #23 positiv, Issue #22 fremder Zuweisender (Snapshot-DB-Assertion + `Inbox:`-Diagnose in den Integrationshinweisen), erneute Zuweisung auf #21 (Pflicht)
    - Voraussetzungen: Schritt 7
11. **Hilfedokumentation** aktualisieren
    - Voraussetzungen: Schritte 4–6 (fachlich fixierte Regeln)
12. **Voller Build + stabile Testlanes + `dotnet format --verify-no-changes`**
    - Voraussetzungen: alle vorherigen Schritte

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetRejection_AssignOwnerAssigner_Accepts` | `ScmEventValidationServiceTests` | `Trigger` mit Actor = Owner → `null` |
| `GetRejection_AssignViewerOrAutomation_Accepts` (Theory) | `ScmEventValidationServiceTests` | Actor = `ViewerLogin` bzw. `github-actions[bot]`/`app/github-actions` (case-insensitive) → `null` |
| `GetRejection_AssignForeignAssigner_Rejected` | `ScmEventValidationServiceTests` | Fremd-Login → `AssignerNotAllowedRejection` |
| `GetRejection_AssignEmptyDescription_Rejected` (Theory: `""`, `"   "`) | `ScmEventValidationServiceTests` | leere Beschreibung → `AssignEmptyDescriptionRejection`, unabhängig vom Zuweisenden |
| `GetRejection_AssignUndeterminedAssigner_Rejected` | `ScmEventValidationServiceTests` | `Trigger` ohne Akteur → `AssignerUnknownRejection` |
| `GetViewerAssignment_*` (Mapping, Viewer-Filter, `unassigned`-Aufhebung, jüngstes Event, Pagination, Agent-Client-Nutzung, typisierte Fehler 404/403/429) | `GitHubAssignmentSourceTests` (`src/Softwareschmiede.Tests/Infrastructure/Plugins`, neu, `FakeGitHubEventApi`) | Plugin-Implementierung des Events-Endpunkts |
| `Monitor_SeparatesAccount_AssignNotification_CreatesTaskAndDispatches` | `ScmEventMonitoringServiceTests_Assignments` (`src/Softwareschmiede.IntegrationTests/Services`, neu) | assign → Beschreibung als Trigger → Resolver-Aufgabe → Ereignis → Prompt enthält Assignee-Hinweis |
| `Monitor_SeparatesAccount_AssignOnPullRequest_CreatesTask` | dto. | PR-Zuweisung → `CreateFromPullRequestAsync`-Pfad |
| `Monitor_SeparatesAccount_Reassignment_DeliversToExistingTask` | dto. | bestehende Aufgabe + neues `assigned`-Event (andere `EventNodeId`) → zweites Ereignis auf derselben Aufgabe, keine Doppelanlage; erneuter Poll mit gleichem Event → kein weiteres Ereignis |
| `Monitor_SeparatesAccount_AssignEmptyDescription_CreatesNoTask` | dto. | leere Beschreibung → kein Resolver-Aufruf, Snapshot-Diagnose `AssignEmptyDescriptionRejection` |
| `Monitor_SeparatesAccount_AssignByForeignLogin_Rejected` | dto. | Snapshot-Diagnose `AssignerNotAllowedRejection`, kein Ereignis/keine Aufgabe |
| `Monitor_SeparatesAccount_AssignUndeterminable_Rejected` | dto. | `GetLatestViewerAssignmentAsync` = `null` → `AssignerUnknownRejection`-Diagnose |
| `Monitor_SeparatesAccount_AssignWithoutCapability_Rejected` | dto. | Plugin implementiert `IScmAssignmentSource` nicht → `AssignerUnknownRejection`-Diagnose |
| `Monitor_SeparatesAccount_AssignByAutomation_Accepted` (Theory) | dto. | Zuweisender `github-actions[bot]`/`app/github-actions` → Aufgabe + Zustellung |
| `Monitor_SeparatesAccount_AssignmentQueryFailure_KeepsInboxWatermarkForRetry` | dto. | `GetLatestViewerAssignmentAsync` wirft `ScmProviderException(Temporary)` → `RecordFailureAsync` auf den Description-`sourceKey` (`ErrorCode`, `NextAttemptUtc` gesetzt); `cycle.HadFailure` verhindert den Inbox-Watermark-Commit (kein `WatermarkUtc` auf dem `notifications/…`-Checkpoint); nach `NextAttemptUtc`-Reset und erfolgreicher Antwort wird die Zuweisung im Folge-Poll regulär verarbeitet (Muster `…_InboxFailure_KeepsWatermarkForRetry`) |
| `Monitor_SeparatesAccount_AssignEmptyDescriptionBatch_Discarded` | dto. | `ReadEventsAsync` liefert für `ContentKind.Description` einen leeren Batch → kein Ereignis/keine Aufgabe; Description-`sourceKey`-Checkpoint trägt `Inbox: {AssignEmptyDescriptionRejection}` |
| `Monitor_SeparatesAccount_AssignRejection_RaisesInboxCheckpointDiagnostic` | dto. | assign-`unassignedRejection` + `workItemId == null` + `unassigned` → Snapshot trägt spezifische Ablehnung, Description-`sourceKey`-Checkpoint trägt `Inbox:`-Diagnose (Integrationshinweis-Ebene, kein Alarm); Mehrdeutigkeit (`unassigned == false`) → `AmbiguousAssignmentDiagnostic` bleibt führend |
| Hilfsmethoden `ConfigureAssignmentSource`, `ConfigureDescriptionObservations`, `ConfigurePullRequestRead` | `ScmDispatchFixture` | siehe Änderungen an bestehenden Klassen |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `Monitor_SeparatesAccount_NonMentionReasons_AreIgnored` (`ScmEventMonitoringServiceTests_Notifications`) | Unverändert grün (`review_requested`/`subscribed` bleiben ignoriert); Regressionssicherung für die erweiterte Reason-Menge — nicht anzupassen |
| `E2E_ScmEvents` → `BotModeAsync` | `fixture.AddNotification(21, reason: "assign")` war Ignoriert-Fall; wird jetzt verarbeitet — Endpunkt `issues/{n}/events` muss bedient werden; Negativ-Assertion auf Task 21 wird zum positiven assign-Szenario erweitert (s. u.); `assign`-Notifications zählen in der `Inbox:`-Zählerdiagnose als verarbeitet („2 verarbeitet, 1 verworfen" statt „1 verarbeitet, 1 verworfen") |

### E2E-Tests (primärer Funktionsnachweis)

Der Auslöser ist ein vollständiger Systemablauf (Inbox → Provider-Abfragen → Aufgabe → Dispatch → Prompt an die CLI) mit sichtbarer Wirkung in der Ereignisübersicht; die Konsolidierungsregel (eine Methode, volle Abdeckung) verlangt die Integration in den bestehenden `SeparateAccount`-Lauf `BotModeAsync` statt neuer Methoden.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | assign-Notification auf Issue 21 (Zuweisender `scm-owner`, explizite `AddText`-Beschreibung + `AddAssignment`) → Aufgabe → Ereignis sichtbar → Start → Zustellung; Prompt enthält den Assignee-Hinweis | `E2E_ScmEvents` / `BotModeAsync` | assign löst Aufgabenanlage und Zustellung aus; Prompt trägt die Herkunftsinformation | Gesamter Benutzerfluss über die echte App inkl. Agent-Client-Routing und Simulator-Zustellung; Unit/Integration decken den UI-/Dispatch-Verbund nicht ab |
| Pflicht | assign-Notification auf Pull Request 23 (`pullRequest: true`, Zuweisender `scm-owner`) → `CreateFromPullRequestAsync`-Pfad → PR-Aufgabe mit `PullRequestReference` → Zustellung mit Assignee-Hinweis | dto. | assign-Auslöser gilt gleichermaßen für Pull Requests | Der PR-Ast des Benutzerflusses (Inbox → `issues/{n}/events` → `ReadPullRequestAsync` → `CreateFromPullRequestAsync` → Ereignis → Zustellung) ist ein verbindlicher Anforderungsteil und im UI-/Dispatch-Verbund nur end-to-end belegbar; Fixture-Infrastruktur (`AddNotification(..., pullRequest: true)`, `AddText(..., kind: "Description", pullRequest: true)` bzw. synthesisierte PR-Beschreibung, `GET pulls/{n}`) ist vorhanden |
| Pflicht | assign-Notification auf Issue 22 mit fremdem Zuweisenden → keine Aufgabe/kein Ereignis; Snapshot-Diagnose `Zuweisung nicht durch berechtigtes Konto` per DB-Assertion auf `ScmContentSnapshots` (Muster `AlreadyReactedRejection`-Assertion, `E2E_ScmEvents.cs` Z. 437–445) **und** `Inbox:`-Checkpoint-Diagnose sichtbar in den Integrationshinweisen (`ScmEventDiagnostics`, Expand-Muster wie „1 verworfen") | dto. | Berechtigungsregel: Ablehnung versandet produktiv nicht still | `ScmContentSnapshot.Diagnostic` hat keine UI-Oberfläche (`ScmEventQueueViewModel` liest nur Checkpoint-Diagnosen) — der ehrliche Nachweis kombiniert DB-Assertion auf Snapshot-Ebene mit der `Inbox:`-Sichtbarkeit auf Checkpoint-Ebene |
| Pflicht | Erneute Zuweisung auf Issue 21 (zweites `assigned`-Event, neue `EventNodeId`, gleiche Beschreibung) → zweites `ScmIncomingEvent` auf derselben Aufgabe, keine Doppelanlage; Prompt-Zustellung | dto. | Dedup-Verschlucken ausgeschlossen — „erneute Zuweisung → erneute Verarbeitung" | Benanntes Kernrisiko der Anforderung; die Dekorations-Mechanik ist nur im durchlaufenen Zyklus beweisbar |
| — | Geometrische Assertions | — | — | Kein Dialog/Overlay/Panel betroffen |

Kein E2E für: leere Beschreibung, Zuweisender nicht bestimmbar, fehlende `IScmAssignmentSource`-Fähigkeit, assign→unassign vor Verarbeitung — diese Ablehnungen haben keinen zusätzlichen UI-Fluss (Ablehnungsdiagnosen sind checkpoint-/snapshot-seitig) und die Logik ist durch Unit-/Integrationstests vollständig abgedeckt.

Betroffene bestehende E2E-Tests: `E2E_ScmEvents.BotModeAsync` — siehe Tabelle „Betroffene bestehende Tests"; die übrigen SCM-E2E-Phasen und `Category!=OsInterface`-Läufe bleiben unverändert.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path Issue (neue Aufgabe) | `assign`-Notification (Owner als Zuweisender) auf Issue eines registrierten Repos | `SeparateAccount` aktiv, AgentToken gesetzt, keine Aufgabe zur Referenz | Inbox-Read → Referenz → `issues/{n}/events` → Description-Batch dekoriert → `ResolveAsync` legt Aufgabe → `ScmIncomingEvent` → Dispatch → Prompt | Aufgabe mit Beschreibung als Auftragsinhalt; Prompt mit Assignee-Hinweis zugestellt; Inbox-Checkpoint mit `Inbox:`-Diagnose | Integrationstest `…_CreatesTaskAndDispatches`; E2E-BotModeAsync-Phase |
| 2 | Happy Path Pull Request | `assign`-Notification auf PR | wie 1, PR-Referenz | analog, `CreateFromPullRequestAsync` | PR-Aufgabe + Prompt mit Hinweis | Integrationstest `…_AssignOnPullRequest_CreatesTask`; E2E-`BotModeAsync`-Phase (Pflicht) |
| 3 | Erneute Zuweisung, Aufgabe besteht | zweites `assigned`-Event (neue `EventNodeId`), unveränderte Beschreibung | Aufgabe aus Szenario 1 vorhanden | Referenz erneut → neuer `ContentKey`/`EventKey` → Resolver findet Bestandsaufgabe → zweites `ScmIncomingEvent` → Zustellung nach Reihenfolge-Regel | Zweiter Prompt auf derselben Aufgabe, keine Doppelanlage | Integrationstest `…_Reassignment_DeliversToExistingTask`; E2E (Pflicht) |
| 4 | Leere Beschreibung | `assign`-Notification, Referenzbody leer | keine Aufgabe | Dekoration → `GetRejection` = `AssignEmptyDescriptionRejection` → kein Resolver | Keine Aufgabe/kein Event; Snapshot-Diagnose + `Inbox:`-Checkpoint-Diagnose | Integrationstest `…_AssignEmptyDescription_CreatesNoTask` |
| 5 | Fremder Zuweisender | `assigned`-Event mit `actor` = Dritter | keine Aufgabe | `AssignerNotAllowedRejection` | Keine Aufgabe/kein Event; Snapshot-Diagnose (DB-nachvollziehbar) + `Inbox:`-Diagnose in den Integrationshinweisen | Integrationstest `…_AssignByForeignLogin_Rejected` + `…_AssignRejection_RaisesInboxCheckpointDiagnostic`; E2E-Phase |
| 6 | Zuweisender nicht bestimmbar | Plugin ohne `IScmAssignmentSource`, kein wirksames Viewer-`assigned`-Event oder zwischenzeitlich aufgehobene Zuweisung (assign → unassign vor Verarbeitung) | keine Aufgabe | `Trigger` ohne Akteur → `AssignerUnknownRejection` | Verwerfung mit Diagnose; Inbox-Watermark läuft weiter | Integrationstests `…_AssignUndeterminable_Rejected`, `…_AssignWithoutCapability_Rejected` |
| 7 | Wiederholte Notification derselben Zuweisung | Poll liest `assign`-Thread erneut (`since`-Überlappung) | Zuweisung bereits verarbeitet | gleiche `EventNodeId` → gleicher `ContentKey` + `BodyHash` → Snapshot-Dedup | Kein zweites Ereignis | Integrationstest `…_Reassignment_DeliversToExistingTask` (Wiederholungszweig) |
| 8 | Fehler der Zuweisungsabfrage (Warte-/Wiederaufnahmepfad) | `issues/{n}/events` liefert `Temporary`/`RateLimit` | assign-Notification ausstehend | Ausnahme im Description-Batch → `RecordFailureAsync` auf den Description-`sourceKey` → `cycle.HadFailure` verhindert den Inbox-Watermark-Commit; nächster Poll nach `NextAttemptUtc`-Backoff wiederholt | Während des Wartens: Backoff-Checkpoint (`NextAttemptUtc`, `ErrorCode`) sichtbar; Wiederaufnahme über regulären Poll ohne Nutzeraktion | Integrationstest `Monitor_SeparatesAccount_AssignmentQueryFailure_KeepsInboxWatermarkForRetry` (konkret am neuen Aufrufort); Plugin-Test `429`/`Temporary` |
| 9 | Leerer Description-Batch | `ReadEventsAsync` liefert für die assign-Referenz keine Description-Beobachtung (keine lesbare Textversion) | keine Aufgabe | Dekoration entfällt (keine Beobachtung) → `AcceptBatchAsync` setzt `Inbox: {AssignEmptyDescriptionRejection}` auf den Description-Checkpoint → kein Ereignis | Keine Aufgabe/kein Event; `Inbox:`-Diagnose in den Integrationshinweisen — die Notification versandet nicht still | Integrationstest `…_AssignEmptyDescriptionBatch_Discarded` |
| 10 | Mehrdeutige Aufgabenzuordnung | assign-Notification auf Referenz mit mehreren Kandidaten-Aufgaben | mehrere verknüpfte Aufgaben | `workItemId == null` bei `unassigned == false` → `AmbiguousAssignmentDiagnostic` führend (Snapshot + Checkpoint) | Verwerfung mit Mehrdeutigkeitsdiagnose statt assign-Ablehnung | Bestandstest-Muster (`…_AmbiguityRecovery`); Regression durch `AcceptBatchAsync`-Regel abgesichert |

## Offene Punkte

Keine.
