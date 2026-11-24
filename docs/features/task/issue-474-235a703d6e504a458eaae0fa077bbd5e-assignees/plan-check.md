# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

Zweite Prüfrunde. Alle fünf Befunde aus `plan-check.1.md` wurden verifiziert als geschlossen:

| Vorheriger Befund | Schließung im Plan | Verifikation |
|--------------------|---------------------|--------------|
| E2E für `assign` auf Pull Request fehlte | Pflicht-Phase „assign-Notification auf Pull Request 23" (plan.md E2E-Tabelle), `ScmE2EFixture.AddText` um `pullRequest`-Parameter, `AddAssignment`-Helfer, Tasks-Datei #20 | Bestätigt: `AddText` hardkodiert `PullRequest = number is 7 or 9` (ScmE2EFixture.cs Z. 101) — Parameter nötig und geplant; `GET pulls/{n}` existiert (ScmE2EApiClient.cs Z. 148–182); `issues/{n}/events` gilt für PRs (REST-Semantik) |
| Kein Monitoring-Level-Test für Fehler der Zuweisungsabfrage | `Monitor_SeparatesAccount_AssignmentQueryFailure_KeepsInboxWatermarkForRetry` (Neue-Tests-Tabelle + Akzeptanzszenario 8), Tasks-Datei #19 | Bestätigt: Aufruf liegt im bestehenden `try` (ScmEventMonitoringService.cs Z. 203–248) → `RecordFailureAsync(sourceKey)` + `cycle.HadFailure = true` blockiert den Inbox-Watermark-Commit (Z. 253) wie geplant |
| E2E „erneute Zuweisung" nur Soll | Jetzt Pflicht (E2E-Tabelle, plan.md) | Bestätigt |
| „Sichtbar in Integrationshinweisen" traf den Bestand nicht | `AcceptBatchAsync` hebt die erste konkrete Ablehnung als `Inbox:`-präfixierte Diagnose auf den Quellen-Checkpoint (Designentscheidung + Änderungsliste) | Bestätigt gegen Code: `checkpoint.Diagnostic` existiert und wird gesetzt (ScmEventStore.cs Z. 116–122); `ScmEventQueueViewModel` Z. 191–205 aggregiert alle Checkpoint-Diagnosen ≠ `UnassignedDiagnostic` in die Integrationshinweise und filtert `Inbox:`-Präfixe aus `alertErrors` (kein Alarm) — exakt das geplante Verhalten |
| Fehlende Aufgabe in `…-tasks.md` | Tasks-Datei #19 (Backoff/Watermark, leerer Batch, `Inbox:`-Checkpoint) und #20 (E2E-Phasen) vorhanden | Bestätigt |

Zudem wurden die Hinweise der ersten Runde adressiert: leerer Description-Batch (Designentscheidung + Szenario 9 + `…_AssignEmptyDescriptionBatch_Discarded`), Mehrdeutigkeits-Vorrang (Designentscheidung + Assertion in `…_AssignRejection_RaisesInboxCheckpointDiagnostic`), expliziter `AddText`-Body für #21, `SinceUtc`-Rücksetzung als benannter Seiteneffekt.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| `assign`-Auslöser für Issues (Notifications-Inbox, `SeparateAccount`) | Reason-Filter `MentionReason or AssignReason`, `ResolvedReference.Notification`, Description-Batch-Dekoration (`RemoteNodeId`-Suffix + `Trigger`), `GetAssignRejection`, bestehende Resolve-/Accept-/Dispatch-Pipeline | Integration `…_AssignNotification_CreatesTaskAndDispatches`; E2E `BotModeAsync` Phase #21 (Pflicht) | Abgedeckt |
| `assign`-Auslöser für Pull Requests | Gleicher Pfad, `CreateFromPullRequestAsync` via Resolver; `issues/{n}/events` deckt PRs ab; `AddNotification(pullRequest: true)` + `AddText(pullRequest: true)` | Integration `…_AssignOnPullRequest_CreatesTask`; E2E Phase #23 (Pflicht) | Abgedeckt |
| Zuweisung nur bei Zuweisender ∈ {Repository-Owner, separates Konto, Automatisierungskonten} | `IScmAssignmentSource.GetLatestViewerAssignmentAsync` (REST `issues/{n}/events`, jüngstes `assigned` auf Viewer) + `GetAssignRejection`-Regel 3 (`SameIdentity` Owner/Viewer, `IsTrustedAutomationLogin`) | Unit `GetRejection_Assign*`-Serie (Owner, Viewer, Automation case-insensitive, fremd); Integration `…_AssignByForeignLogin_Rejected`, `…_AssignByAutomation_Accepted`; E2E Phase #22 | Abgedeckt |
| Leere Beschreibung → keine Aufgabe | `AssignEmptyDescriptionRejection` (Regel 1, vor Akteursprüfung); leerer Batch → `Inbox:`-Diagnose auf Description-Checkpoint | Unit-Theory (`""`, `"   "`); Integration `…_AssignEmptyDescription_CreatesNoTask`, `…_AssignEmptyDescriptionBatch_Discarded` | Abgedeckt |
| Erneute Zuweisung → erneute Verarbeitung / Zustellung an bestehende Aufgabe | Dekoration `RemoteNodeId += "/assign-{eventNodeId}"` → neuer `ContentKey`/`EventKey` unterläuft `BodyHash`-Dedup (ScmEventStore.cs Z. 74–76); Resolver-Wiederverwendung via `IssueSourceKey`/`PullRequestTaskIdsAsync` | Integration `…_Reassignment_DeliversToExistingTask` (inkl. Wiederholungszweig = Dedup-Sicherung); E2E Pflicht-Phase | Abgedeckt |
| Prompt enthält Assignee-Herkunftshinweis | `AssigneePromptPrefix` vorangestellt an `ScmExchangeRequest.Body` in `DispatchCoreAsync` (request.json/Prompt, nicht `promptSuffix`; Ereignistext bleibt frei) | Prompt-Assertion in `…_CreatesTaskAndDispatches`; E2E Phase #21 | Abgedeckt |
| Assign → Unassign vor Verarbeitung → verworfen (Anwenderentscheidung 2026-11-23) | `unassigned` auf Viewer jünger als letztes `assigned` → `null` → `Trigger` ohne Akteur → `AssignerUnknownRejection` | Plugin-Test `unassigned`-Aufhebung (`GitHubAssignmentSourceTests`); Integration `…_AssignUndeterminable_Rejected` (Tasks-Datei #18 explizit); Szenario 6 | Abgedeckt |
| Nur Modus „separates Konto" | Strukturell: Inbox-Kanal nur bei `inboxMode` (ScmEventMonitoringService.cs Z. 89, 150); Scope-Ausschluss 1 bestätigt | Bestandsregression (`Monitor_ModeSwitch_BackToEnumeration` u. a.) | Abgedeckt |
| Zuweisender nicht bestimmbar → verwerfen | `AssignerUnknownRejection` (Fähigkeit fehlt, kein wirksames `assigned`-Event, aufgehobene Zuweisung) | Unit `…_AssignUndeterminedAssigner_Rejected`; Integration `…_AssignUndeterminable_Rejected`, `…_AssignWithoutCapability_Rejected` | Abgedeckt |
| Nicht registriertes Repository → verwerfen mit `Inbox:`-Diagnose | Unverändert (Allowlist-Match, `discarded++`, Zählerdiagnose) | Bestand `…_ForeignNotification_DiscardedWithDiagnostic`; E2E #19 | Abgedeckt |
| Weitere Reasons (`subscribed`, `review_requested` u. a.) wirkungslos | Unverändert; Scope-Ausschluss 3 | `…_NonMentionReasons_AreIgnored` (unverändert grün) | Abgedeckt |
| Auslösender Text braucht weder Keyword noch `@`-Erwähnung | `GetAssignRejection` ersetzt die Trigger-Prüfung für `Trigger.Reason == "assign"`; Scope-Ausschluss 5 | Implizit in allen Assign-Tests | Abgedeckt |
| Verwerfungen bleiben nachvollziehbar (keine stille Verwerfung) | Snapshot-`Diagnostic` (DB-Ebene) + `Inbox:`-präfixierte Diagnose auf dem Description-Quellen-Checkpoint (Integrationshinweise, kein Alarm) | Integration `…_AssignRejection_RaisesInboxCheckpointDiagnostic`; E2E Phase #22 (DB-Assertion + Integrationshinweise) | Abgedeckt |
| Fehler der Zuweisungsabfrage → Backoff/Wiederaufnahme | Aufruf im Batch-`try` → `RecordFailureAsync` auf Description-`sourceKey`; `cycle.HadFailure` hält Inbox-Watermark; Wiederaufnahme über regulären Poll | Integration `…_AssignmentQueryFailure_KeepsInboxWatermarkForRetry`; Plugin-Test typisierte Fehler (404/403/429) | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| assign-Notification → Aufgabe → Ereignis → Dispatch → Prompt mit Assignee-Hinweis (Issue) | `E2E_ScmEvents.BotModeAsync`, Issue #21 (Pflicht, Bestands-Ignoriert-Fall wird positiv) | Abgedeckt |
| assign-Notification → PR-Aufgabe → Zustellung mit Hinweis (Pull Request) | `BotModeAsync`, PR #23 (Pflicht) | Abgedeckt |
| Fremder Zuweisender → keine Aufgabe; Ablehnung nachvollziehbar | `BotModeAsync`, Issue #22 (Pflicht): DB-Assertion auf `ScmContentSnapshots.Diagnostic` (Muster `AlreadyReactedRejection`, E2E_ScmEvents.cs Z. 437–445) + `Inbox:`-Diagnose in `ScmEventDiagnostics` | Abgedeckt |
| Erneute Zuweisung → zweites Ereignis auf derselben Aufgabe, keine Doppelanlage | `BotModeAsync` (Pflicht) | Abgedeckt |
| Leere Beschreibung / Zuweisender nicht bestimmbar / fehlende `IScmAssignmentSource`-Fähigkeit / assign→unassign | Kein E2E | Nicht erforderlich mit Begründung: kein zusätzlicher UI-Fluss (Ablehnungen sind checkpoint-/snapshot-seitig); Unit-/Plugin-/Integrationstests decken die deterministische Logik vollständig ab |
| Geometrische Assertions | — | Nicht erforderlich mit Begründung: kein Dialog/Overlay/Panel betroffen |

## Hinweise

- Kernmechanik erneut gegen den Code verifiziert und stimmig: `resolved.Clear()` (ScmEventMonitoringService.cs Z. 154) macht Inbox-Referenzen stets `unassigned` → Trigger-Suche (Z. 221–228) und Resolver laufen auch bei bestehender Aufgabe → Wiederverwendung greift; `AcceptBatchAsync`-Signatur (Z. 50–51) nimmt den optionalen `unassignedRejection`-Parameter additive auf; `IsViewer`/`RestPagesAsync`/`ClientFor`/`SendEventApiAsync` existieren als private Member derselben (partiellen) `GitHubPlugin`-Klasse (GitHubPlugin.Events.cs Z. 367, 448, 613); `ScmEventObservation` ist ein positionelles Record mit optionalem Endparameter-Muster (ScmEvents.cs Z. 16–19) — `Trigger = null` als Endparameter ist kompatibel.
- `NextCheck(null, …)` rendert sticky `Inbox:`-Diagnosen mit dem Suffix „(Prüfung ausstehend)" (ScmEventQueueViewModel.cs Z. 350–351) — identisch zur bestehenden Inbox-Zählerdiagnose, kosmetisch konsistent, kein Korrekturbedarf.
- `Inbox:`-Ablehnungsdiagnosen auf Description-Checkpoints persistieren, bis derselbe `sourceKey` erneut verarbeitet wird — vom Plan bewusst so entschieden; beachten, dass sich solche Einträge in den Integrationshinweisen kumulieren können, solange keine neue Notification dieselbe Referenz erneut verarbeitet.
- Designentscheidung „Assign-Vorrang vor Mention bei Inbox-Dedup" hat keinen dedizierten Test (fachliche Nebenbedingung: eine gleichzeitige legitime `@scm-bot`-Erwähnung im Issue-Body wird durch eine fremde Zuweisung mitgedeckelt; Mention-Kommentare bleiben trigger-fähig). Fachlich begründet dokumentiert; bei Bedarf wäre ein kleiner Integrationstest für die Dedup-Reihenfolge denkbar — keine Lücke im Sinne der Akzeptanzkriterien.
- Zählerdiagnose-Beispielwerte im Plan („2 verarbeitet, 1 verworfen") sind Näherungen — `verarbeitet` zählt gefilterte Notifications inkl. deduplizierter Duplikate; die bestehende E2E-Assertion (`Contains("1 verworfen")`) bleibt davon unberührt und korrekt.
