# Bestandsaufnahme: Assignee-Auslöser für SCM-Ereignisse (Issue #474)

Analyse des bestehenden SCM-Ereignisverarbeitungs-Pfads (Notifications-Inbox, Validierung, Aufgabenauflösung, Inbox-Aufnahme, Dispatch/Prompt-Zustellung und GitHub-Plugin) bezogen auf die Anforderung, im Modus `ScmAccountMode.SeparateAccount` zusätzlich `reason = assign`-Notifications als Auslöser zu verarbeiten (siehe `requirement.md`).

## Zusammenfassung

**Vorhanden:**

- Die Notifications-Inbox des separaten Kontos ist vollständig implementiert: `IScmNotificationSource.EnumerateNotificationsAsync` (Vertrag), `GitHubPlugin.EnumerateNotificationsAsync` (`GET /notifications`, liefert **alle** Reasons inklusive `assign` — kein Subject-/Reason-Filter im Plugin) und die hostseitige Auswertung in `ScmEventMonitoringService.EnumerateNotificationsAsync` (Repository-Node-ID-Allowlist, Inbox-Checkpoint/Watermark, `InboxDiscardDiagnosticPrefix`-Zählerdiagnose).
- `ScmNotification` trägt bereits `Reason`, `ThreadId`, `UpdatedAt`, `Kind` (Issue/PullRequest) und `Number` — der GitHub-Mapping-Pfad liefert `assign`-Threads unverändert mit.
- Die komplette nachgelagerte Pipeline ist trigger-agnostisch: Referenz → `ReadEventsAsync` (inkl. `ScmContentKind.Description`-Quelle) → `ScmEventValidationService.GetRejection` → `ScmEventTaskResolver` (Wiederverwendung via `IssueSourceKey`/`PullRequestTaskIdsAsync` bzw. Anlage via `WorkItemService.CreateFromIssueEventAsync`/`CreateFromPullRequestAsync`) → `ScmEventStore.AcceptBatchAsync` → `ScmEventOrchestrator.DispatchDueEventsAsync` → Prompt über `ScmEventExchangeProtocol`.
- Die Akteurs-Vertrauenslogik existiert bereits als wiederverwendbares Muster: Owner-Identitätsvergleich (`SameIdentity`), `TrustedAutomationLogins` (`github-actions[bot]`, `app/github-actions`), `ViewerLogin` auf `ScmEventRepository` — allerdings aktuell auf Autor/Editor der Textversion bezogen, nicht auf einen Zuweisenden.
- Test-Infrastruktur deckt den Inbox-Pfad ab: `ScmDispatchFixture.AddNotification`, `ScmE2EFixture.AddNotification(..., reason, pullRequest)`, `ScmE2ENotification`, `ScmE2EApiClient` (`GET notifications`-Fixture), `GitHubNotificationSourceTests`, `ScmEventMonitoringServiceTests_Notifications`.

**Fehlend / noch nicht vorhanden:**

- **Reason-Filter:** `ScmEventMonitoringService.EnumerateNotificationsAsync` (Z. 345) filtert ausschließlich `x.Reason == ScmNotification.MentionReason`; `assign`-Notifications werden derzeit ignoriert (zählen auch nicht in die Verwerfungsdiagnose). `ScmNotification` definiert nur `MentionReason`; es gibt keine `AssignReason`-Konstante.
- **Reason-Weitergabe:** Die private Record `ResolvedReference` trägt kein Herkunfts-/Reason-Feld; die Information, *warum* eine Referenz beobachtet wird, geht nach der Inbox-Ableitung verloren.
- **Zuweisender-Ermittlung:** Es existiert kein Vertrag und keine Implementierung zur Abfrage des Akteurs einer Zuweisung (keine Timeline-/Events-Abfrage im GitHub-Plugin, keine Methode auf `IScmNotificationSource`/`IScmEventSource`).
- **Assign-Validierung:** `ScmEventValidationService.GetRejection` kennt ausschließlich den Texttrigger (Keyword/`@mention`); eine Description ohne Erwähnung wird im `SeparateAccount` mit `NoMentionRejection` abgelehnt. Es gibt keine Ablehnungsregeln für „leere Beschreibung" oder „Zuweisender nicht berechtigt". `TrustedAutomationLogins` ist `private`.
- **Dedup bei erneuter Zuweisung:** `ScmEventStore.AcceptBatchAsync` überspringt Beobachtungen mit unverändertem `BodyHash` (Z. 74-76); `ScmEventObservation.EventKey`/`Revision` enthält keine Zuweisungsidentität — eine erneute Zuweisung bei gleichbleibender Beschreibung würde heute verschluckt.
- **Assignee-Herkunft im Prompt:** `ScmExchangeRequest`/`BuildPrompt` kennt keinen Auslöser-Herkunftshinweis; einzige Erweiterungsstelle ist der konfigurierbare `promptSuffix` (`ScmPromptAppendix`).
- **Tests:** Keine Tests für `assign`; der einzige `assign`-Bezug ist ein Negativ-Fall in `E2E_ScmEvents` (Notification mit `reason = "assign"` wird ignoriert).

**Test-Ausgangszustand:** Vollständiger Build erfolgreich (0 Warnungen, 0 Fehler). Beide stabilen Testlanes grün: `Softwareschmiede.Tests` 2800 bestanden / 0 fehlgeschlagen / 3 übersprungen; `Softwareschmiede.IntegrationTests` 417 bestanden / 0 fehlgeschlagen / 0 übersprungen. Keine nachgewiesenen Testfehler. Details und Nachweise: [Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `ScmNotification`, `ScmEventObservation`, `ScmEventRepository`, `ScmEventReference`, `ScmEventReadRequest`, `ScmEventBatch`, `ScmIncomingEvent`, `ScmExchangeRequest`/`ScmExchangeAttempt`
- [Logik](inventory/logic.md) — `ScmEventMonitoringService`, `ScmEventValidationService`, `ScmEventTaskResolver`, `ScmEventStore`, `ScmEventOrchestrator`, `ScmEventExchangeProtocol`, `ScmPromptAppendix`, `ScmCheckpointSupport`, `ScmEventExchangeService`, `GitHubPlugin` (Ereignis-Teil)
- [Enums](inventory/enums.md) — `ScmReferenceKind`, `ScmContentKind`, `ScmAccountMode`, `ScmProviderFailureKind`, `ScmResponseKind`, `ScmTaskResolutionKind`
- [Interfaces](inventory/interfaces.md) — `IScmNotificationSource`, `IScmEventSource`, `IScmEventResponder`, `IGitHubEventApiClient`
- [Tests](inventory/tests.md) — Test-Ausgangszustand, Testklassen und Hilfsmethoden
