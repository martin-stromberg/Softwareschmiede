# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| `assign`-Auslöser für Issues (Notifications-Inbox, `SeparateAccount`) | Reason-Filter-Erweiterung, `ResolvedReference.Notification`, Description-Batch-Dekoration, Assign-Validierung, bestehende Resolve-/Accept-/Dispatch-Pipeline (Plan-Schritte 1–5, Programmablauf) | Integration `Monitor_SeparatesAccount_AssignNotification_CreatesTaskAndDispatches`; E2E `BotModeAsync`-Phase #21 | Abgedeckt |
| `assign`-Auslöser für Pull Requests | Gleicher Pfad; `CreateFromPullRequestAsync` via Resolver; REST `issues/{n}/events` deckt PRs ab; `ConfigurePullRequestRead`-Fixture-Helfer | Nur Integration `…_AssignOnPullRequest_CreatesTask`; **kein E2E** | **Lücke** |
| Zuweisung nur bei Zuweisender ∈ {Repository-Owner, separates Konto, Automatisierungskonten} | `IScmAssignmentSource` + `GitHubPlugin.GetLatestViewerAssignmentAsync` + `GetAssignRejection`-Regel 3 (`SameIdentity` Owner/Viewer, `TrustedAutomationLogins`) | Unit `GetRejection_Assign*`-Serie; Integration `…_AssignByForeignLogin_Rejected`, `…_AssignByAutomation_Accepted`; E2E #22 | Abgedeckt |
| Leere Beschreibung → keine Aufgabe | `AssignEmptyDescriptionRejection` (Regel 1, vor Akteursprüfung) | Unit-Theory (`""`, `"   "`); Integration `…_AssignEmptyDescription_CreatesNoTask` | Abgedeckt |
| Erneute Zuweisung → erneute Verarbeitung / Zustellung an bestehende Aufgabe | `RemoteNodeId`-Dekoration `…/assign-{eventNodeId}` (zuweisungsspezifischer `ContentKey`/`EventKey`, unterläuft `BodyHash`-Dedup ScmEventStore.cs Z. 74–76) + Resolver-Wiederverwendung (`IssueSourceKey`/`PullRequestTaskIdsAsync`) | Integration `…_Reassignment_DeliversToExistingTask` (inkl. Wiederholungszweig); E2E (Soll) | Abgedeckt |
| Prompt enthält Assignee-Herkunftshinweis | `AssigneePromptPrefix` wird in `DispatchCoreAsync` dem `ScmExchangeRequest.Body` vorangestellt (request.json, nicht `promptSuffix`) | Prompt-Assertion im Integrationstest `…_CreatesTaskAndDispatches`; E2E-Pflichtszenario #21 | Abgedeckt |
| Assign → Unassign vor Verarbeitung → verworfen (Anwenderentscheidung 2026-11-23) | Jüngstes `unassigned` auf den Viewer → `null` → `AssignerUnknownRejection` | Plugin-Test `unassigned`-Aufhebung; Integration `…_AssignUndeterminable_Rejected` (Task #18 nennt den Fall explizit) | Abgedeckt |
| Nur Modus `SeparateAccount` | Strukturell garantiert: Inbox-Kanal läuft nur bei `inboxMode` (`ScmEventMonitoringService.cs` Z. 89, 150); Scope-Ausschluss 1 bestätigt | Bestandsregression (`Monitor_ModeSwitch_BackToEnumeration` u. a.) | Abgedeckt |
| Zuweisender nicht bestimmbar → verwerfen | `AssignerUnknownRejection` (Fähigkeit fehlt, kein wirksames `assigned`-Event, aufgehobene Zuweisung) | Unit `…_AssignUndeterminedAssigner_Rejected`; Integration `…_AssignUndeterminable_Rejected`, `…_AssignWithoutCapability_Rejected` | Abgedeckt |
| Nicht registriertes Repository → verwerfen mit `Inbox:`-Diagnose | Unverändert (Allowlist-Match Z. 348–351, Zählerdiagnose Z. 358–359; Plan benennt Zähleränderung als Risiko) | Bestand `…_ForeignNotification_DiscardedWithDiagnostic`; E2E #19 | Abgedeckt |
| Weitere Reasons (`subscribed`, `review_requested` u. a.) wirkungslos | Unverändert; verifiziert: Bestandstest nutzt `review_requested`/`subscribed`, kein `assign` | `…_NonMentionReasons_AreIgnored` (bleibt unverändert grün) | Abgedeckt |
| Auslösender Text braucht weder Keyword noch `@`-Erwähnung | `GetAssignRejection` ersetzt Trigger-Prüfung für `Trigger.Reason == "assign"` | Implizit in allen Assign-Tests (Beschreibung ohne Erwähnung qualifiziert) | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] **E2E für `assign` auf Pull Request fehlt.** Die verbindliche Anforderung gilt ausdrücklich für Issues *und* PRs; der E2E-Abschnitt des Plans enthält ausschließlich Issue-Szenarien (#21, #22, erneute Zuweisung). Der Integrationstest `…_AssignOnPullRequest_CreatesTask` kompensiert den fehlenden End-to-End-Nachweis des Benutzerflusses (Inbox → Provider-Reads → `CreateFromPullRequestAsync` → Ereignis → Zustellung) nicht. Die Infrastruktur ist vorhanden (`ScmE2EFixture.AddNotification(..., pullRequest: true)`, `AddText(..., kind: "Description")`), eine PR-Phase lässt sich kostengünstig in `BotModeAsync` integrieren.
- [ ] **Akzeptanzszenario 8 (Fehler der Zuweisungsabfrage → Backoff → Wiederaufnahme) hat keinen konkreten Monitoring-Level-Test.** Der Nachweis verweist nur auf „Bestandstest-Muster `…_InboxFailure_KeepsWatermarkForRetry` analog" plus Plugin-Fehlertests. In der Tabelle „Neue Tests" fehlt ein Test, der für den neuen Aufrufort konkret belegt: `GetLatestViewerAssignmentAsync` wirft → `RecordFailureAsync` auf den Description-`sourceKey` (Backoff-Checkpoint), `cycle.HadFailure` verhindert den Inbox-Watermark-Commit, Wiederaufnahme im Folge-Poll.
- [ ] **E2E-Nachweis für „erneute Zuweisung" nur als Soll priorisiert.** Das Dedup-Verschlucken ist das benannte Kernrisiko der Anforderung („erneute Zuweisung → erneute Verarbeitung"); die Dekorations-Mechanik sollte im E2E als Pflicht gelten, nicht als Optional.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| assign-Notification → Aufgabe → Ereignis → Dispatch → zugestellter Prompt mit Assignee-Hinweis (Issue) | `E2E_ScmEvents.BotModeAsync`, Phase #21 (Pflicht) | Abgedeckt |
| Fremder Zuweisender → keine Aufgabe, Ablehnungsdiagnose nachvollziehbar | `BotModeAsync`, Phase #22 (Pflicht) | Abgedeckt mit Einschränkung — siehe „Fehlende Planbestandteile": „sichtbar in Integrationshinweisen" trifft den Bestand nicht; nur DB-/Snapshot-Ebene möglich |
| Erneute Zuweisung → zweites Ereignis auf derselben Aufgabe | `BotModeAsync` (Soll) | Abgedeckt (Priorisierung bemängelt, s. o.) |
| assign-Notification auf Pull Request → PR-Aufgabe → Zustellung | Kein E2E geplant | **Lücke** |
| Leere Beschreibung / Zuweisender nicht bestimmbar / Fähigkeit fehlt | Kein E2E | Nicht erforderlich mit Begründung: kein zusätzlicher UI-Fluss (Ablehnungen haben heute keine UI-Oberfläche); Unit- und Integrationstests decken die Logik vollständig ab |
| Assign→Unassign vor Verarbeitung | Kein E2E | Nicht erforderlich mit Begründung: Zeitkantierung (`unassigned` jünger als `assigned`) ist deterministische Plugin-/Validierungslogik, durch `GitHubAssignmentSourceTests` (`unassigned`-Aufhebung) und Integrationstests nachgewiesen |
| Geometrische Assertions | — | Nicht erforderlich mit Begründung: kein Dialog/Overlay/Panel betroffen (Plan benennt dies korrekt) |

## Fehlende oder unvollständige Planbestandteile

- [ ] **Die E2E-Behauptung „Snapshot-Diagnose `Zuweisung nicht durch berechtigtes Konto` sichtbar in Integrationshinweisen/Diagnose" trifft den Bestand nicht.** `ScmEventQueueViewModel` (`src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs`, Z. 191–204) aggregiert ausschließlich `ScmObservationCheckpoints.Diagnostic` und filtert `UnassignedDiagnostic` explizit heraus; `ScmContentSnapshot.Diagnostic` wird von keiner UI-Komponente gelesen. Im geplanten Design landet die Assign-Ablehnung nur auf dem Snapshot (Checkpoint erhält weiter `„Keine Aufgabe zugeordnet"`) und ist damit **nicht** in den Integrationshinweisen sichtbar — genauso wenig wie `AlreadyReactedRejection` heute. Entweder muss der Plan die Ablehnung zusätzlich auf den Checkpoint heben (Verhaltensänderung, abwägen) oder der E2E-Nachweis ist ehrlich auf DB-/Snapshot-Ebene zu formulieren (Muster wie die `AlreadyReactedRejection`-Assertion in `E2E_ScmEvents.cs` Z. 437–445).
- [ ] **Konkreter Monitoring-Test für den neuen Fehlerort fehlt in der Umsetzungsreihenfolge** — siehe Testlücke zu Szenario 8; die Aufgabenliste (`…-tasks.md` #18) enthält ihn ebenfalls nicht.

## Hinweise

- Die Kernmechanik des Plans wurde gegen den Code verifiziert und ist stimmig: `resolved.Clear()` (Z. 154) macht Inbox-Referenzen stets `unassigned` → Trigger-Suche (Z. 221–222) und `ScmEventTaskResolver.ResolveAsync` laufen auch für Referenzen mit bestehender Aufgabe → Wiederverwendung via `IssueSourceKey`/`PullRequestTaskIdsAsync` greift wie beschrieben. Die `RemoteNodeId`-Dekoration erzeugt einen neuen `ContentKey` und umgeht korrekt die `BodyHash`-Dedup (`ScmEventStore.cs` Z. 74–76).
- `IsViewer` existiert bereits als `private static` in `GitHubPlugin.Events.cs` (Z. 613) — die geplante Wiederverwendung innerhalb derselben Klasse ist ohne Schnittstellenänderung möglich.
- Der E2E-Client synthetisiert Beschreibung (`"Beschreibung ohne Trigger"`, `ScmE2EApiClient.cs` Z. 126–127) und Issue-Detail (Z. 156–165) bereits implizit; für #21 ist trotzdem ein expliziter `AddText`-Body sinnvoll, damit der Auftragsinhalt nicht zufällig den Standardtext trägt.
- Verworfene Assign-Beobachtungen behalten `IdentityResolved = false` → `request.SinceUtc = null`-Rücksetzung (Z. 207–210) liest die Description-Quelle bei jedem Poll vollständig neu; vorhandenes Verhalten, geringer Mehraufwand, kein Korrekturbedarf.
- Edge-Case offen: Liefert `ReadEventsAsync` für den Description-Batch eine **leere** Beobachtungsliste (Referenz ohne jede Textversion), entsteht weder Snapshot noch Diagnose — die assign-Notification versandet dann still. Ergibt der Provider in diesem Fall einen typisierten Fehler, greift der Backoff-Pfad; der Plan sollte das gewünschte Verhalten kurz festlegen.
- Abgelehnte Zuweisungen können bei `Ambiguous`-Auflösung die `AssignerNotAllowedRejection` über die `AmbiguousAssignmentDiagnostic` stülpen (`unassignedRejection` greift bei `workItemId == null` für beide Diagnosearten) — fachlich vertretbar, aber im Plan nicht explizit bedacht.
