# Logik — Bestandsaufnahme Assignee-Auslöser (Issue #474)

## `ScmEventMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`

`PeriodicBackgroundService` im `ScmEventOptions.PollInterval`-Rhythmus (Standard 60 s). Beobachtet lokal verknüpfte SCM-Referenzen (`UserAccount`: zusätzlich repository-weit) und persistiert Quellen.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync` | `public override` | Zyklus: Einstellungen lesen (Monitoring-Schalter, Trigger-/Delay-Keyword, `ScmAccountMode`, Issue-Aufbereitung), Aufgaben-/Repository-Bestand laden, je Plugin Alias-Gruppen auflösen (`BuildAliasGroups`), Inbox- oder Enumerationskanal wählen, je Referenzgruppe × ContentKind `ReadEventsAsync` → Trigger-Suche → `ScmEventTaskResolver` → `ScmEventStore.AcceptBatchAsync`, Inbox-Watermark-Commits, `ScmEventOrchestrator.DispatchDueEventsAsync` |
| `EnumerateNotificationsAsync` (Z. 321-367) | `private` | Liest die Inbox eines Hosts über `IScmNotificationSource`; **filtert Z. 345 ausschließlich `x.Reason == ScmNotification.MentionReason`**; mappt `RepositoryNodeId` auf registrierte Repositories desselben Hosts; dedupliziert pro `RepositoryNodeId/Kind/Number`; legt `ResolvedReference(null, reference, null)` an; merkt Watermark-Commit mit `InboxDiscardDiagnosticPrefix`-Zählerdiagnose („N verarbeitet, M verworfen (Repository nicht registriert).") vor; Fehler → `RecordFailureAsync` (Inbox-Backoff). Nicht-mention-Reasons werden **still ignoriert** (zählen nicht als verworfen). |
| `BuildAliasGroups` (Z. 291-316) | `private` | Alias-Gruppierung über `ScmRepositoryAliasKey.CanonicalOrRaw`: verknüpfte Referenzen + aktive Repositories je kanonischem Alias |
| `IsInaccessible` (Z. 373-374) | `private static` | `Permanent`/`Permission` = „Repository für das Konto nicht sichtbar" (isoliert statt Durchlauf-Abbruch) |

Wesentliche Ablaufdetails für die Anforderung:
- `inboxMode` (Z. 89): `accountMode == ScmAccountMode.SeparateAccount && plugin is IScmNotificationSource`. Nur dann läuft der Inbox-Kanal; im `UserAccount` gibt es keinen Inbox-Bezug.
- `resolved.Clear()` (Z. 154): Im `SeparateAccount` werden verknüpfte Aufgabenreferenzen **nicht** als Quellen übernommen — die Inbox ist die einzige Referenzquelle.
- ContentKind-Auswahl je Referenz (Z. 183-185): Issue → `Description` + `IssueComment`; PullRequest → zusätzlich `ReviewComment` + `PullRequestReview`.
- Trigger-Suche für die Aufgabenanlage (Z. 221-228): nur wenn die Referenz **keiner** lokalen Aufgabe zugeordnet ist (`unassigned`) und das lokale Repository aufgelöst ist, wird `batch.Observations.FirstOrDefault(x => ScmEventValidationService.GetRejection(x, triggerKeyword) is null)` als auslösende Beobachtung gesucht und `ScmEventTaskResolver.ResolveAsync` aufgerufen. Gilt je ContentKind-Batch — die `Description`-Beobachtung kann heute nur qualifizieren, wenn sie selbst den Mention-Trigger enthält.
- Issue-Aufbereitungs-Erkennung (Z. 233-239) läuft ausdrücklich **nicht** im Inbox-Modus.
- Inbox-Watermark-Commit (Z. 253-272) erst bei fehlerfreiem Zyklus (`!cycle.HadFailure`).

## `ScmEventValidationService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`

Statischer Trigger-/Akteurs-Check vor Auftragsanlage.

| Mitglied | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DefaultTriggerKeyword` | `public const` | `"$Softwareschmiede"` |
| `NoMentionRejection` | `public const` | `"Keine Erwähnung des separaten Kontos"` — Ablehnungsgrund im `SeparateAccount` ohne frei stehende `@ViewerLogin`-Erwähnung |
| `AlreadyReactedRejection` | `public const` | `"Bereits mit dem Konto reagiert"` — Kommentar-Arten mit `ViewerHasReacted` |
| `TrustedAutomationLogins` | `private static readonly` | `["github-actions[bot]", "app/github-actions"]` — vertrauenswürdige Automatisierungskonten (case-insensitive) |
| `NormalizeTriggerKeyword` | `public static` | Leere Keyword-Werte → Standard |
| `GetRejection` (Z. 39-68) | `public static` | Prüfschritte in Reihenfolge: 1) Trigger — `SeparateAccount`: frei stehende `@ViewerLogin`-Erwähnung; `UserAccount`: frei stehendes Keyword — sonst `NoMentionRejection`/`"Kein Trigger"`. 2) `ViewerHasReacted` auf Kommentar-Arten → `AlreadyReactedRejection`. 3) `UserAccount`: Viewer muss Owner sein → `"Angemeldeter Benutzer ist nicht Repository-Owner"`. 4) Akteur (Editor bei Edit, sonst Author) bestimmbar → `"Bearbeiter nicht bestimmbar"`. 5) Akteur = Owner (`SameIdentity` auf `OwnerId`/`Owner`) oder `IsTrustedAutomationLogin` → `"Textversion stammt nicht vom Repository-Owner"`. 6) `<!-- softwareschmiede:`-Marker → `"Eigene Antwort"`. `null` = akzeptiert. |
| `HasFreeStandingTrigger` | `private static` | Trigger zählt nur unzitiert (kein Quote-Zeichen davor/dahinter) |
| `SameIdentity` | `private static` | ID-Vergleich vor Login-Vergleich (ID ordinal, Login ordinal-ignore-case) |
| `IsTrustedAutomationLogin` | `private static` | Login ∈ `TrustedAutomationLogins` (case-insensitive) |

Für die Anforderung relevant: Es gibt **keinen** assign-bewussten Pfad — eine `Description` ohne `@ViewerLogin` wird im `SeparateAccount` mit `NoMentionRejection` abgelehnt; ein Zuweisender-Akteur ist im Modell nicht vorhanden.

## `ScmEventTaskResolver`
Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ResolveAsync` | `public` | Verteilt nach `reference.Kind` auf Issue-/PR-Auflösung; `trigger` = die qualifizierende Beobachtung |
| `ResolveIssueAsync` | `private` | `ReadIssueAsync` → zweistufige Kandidatensuche (regulär vor `Purpose == ScmIssuePreparationService.Purpose`, Mehrfachtreffer → `Ambiguous`) unter `ScmTaskCoordinator.Shared.RunAsync(IssueGateKey)` → Anlage via `workItems.CreateFromIssueEventAsync(projectId, composed, repositoryId, IssueSourceKey(reference), ct)` mit `composed = issue with { Body = ComposeBody(issue.Body, trigger) }` |
| `ResolvePullRequestAsync` | `private` | `ReadPullRequestAsync` → `PullRequestTaskIdsAsync` (Dedup über `PullRequestReferences`) → Kontextaufgabensuche (Branch-/Closing-Issue-Match) → Anlage via `workItems.CreateFromPullRequestAsync`; `InvalidOperationException` (Unique-Index-Anlagerennen) → Re-Lookup |
| `ComposeBody` | `private static` | Auftragstext = Referenzbeschreibung (ohne Duplikat des Triggertexts) + Triggertext + URL, Absätze per Leerzeile |
| `IssueSourceKey` | `internal static` | Kanonischer Dedup-Schlüssel `provider/host/repoId/kind/number` (Namensraum wie `ContentKey`) — verhindert Doppelanlage |
| `IssueGateKey` | `internal static` | SHA-256 über `IssueSourceKey` (erste 16 Bytes als Guid) — prozessinternes Serialisierungsgate |

Für die Anforderung: Die bestehende Wiederverwendungs-/Dedup-Mechanik (Kandidatensuche + `IssueSourceKey`-/`PullRequestReferences`-Einmaligkeit) ordnet ein zweites Ereignis derselben Referenz automatisch derselben Aufgabe zu.

## `ScmEventStore`
Datei: `src/Softwareschmiede/Application/Services/ScmEventStore.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AmbiguousAssignmentDiagnostic` | `public const` | `"Mehrdeutige Zuordnung zu Aufgaben"` |
| `UnassignedDiagnostic` | `public const` | `"Keine Aufgabe zugeordnet"` |
| `InboxDiscardDiagnosticPrefix` | `public const` | `"Inbox:"` — Routine-Verwerfungsdiagnose, kein Alarm |
| `Observation` | `public static` | Deserialisiert `ScmIncomingEvent.ObservationJson` |
| `ActionId` | `public static` | Deterministische Aktions-ID (SHA-256 über `eventId:purpose`) |
| `RetryAt` | `public static` | Backoff: 30 s / 2 min / 10 min / 30 min + Jitter bzw. `RetryAfterUtc` |
| `AcceptBatchAsync` (Z. 50-129) | `public` | Transaktional je Quelle: je Beobachtung Snapshot-Lookup per `ContentKey`; **Dedup Z. 74-76: unveränderter `BodyHash` + `IdentityResolved` + keine Zuordnungsdiagnose → `continue`**; Validierung `GetRejection` (nur bei `workItemId != null`, sonst Zuordnungsdiagnose); EventKey-Dedup über `ScmIncomingEvents`; `ScmIncomingEvent`-Anlage inkl. `ScmDelayDirective.Parse`-Auswertung (Verzögerung/`waiting`-Kommentar/Pause); Checkpoint-Update + `Notify` |
| `EnqueueDelayComment` | `private` | `waiting`-Outbox-Aktion (Empfangs-/Verwerfungskommentar) |
| `PauseForDelayAsync` | `private` | Aufgabe bis Direktiven-Ziel pausieren (verlängernd, nur startbare Stati) |
| `ClaimTaskAsync` / `RenewTaskAsync` / `ReleaseTaskAsync` | `public` | Instanzübergreifende Aufgabenleases (`ScmTaskLeases`) |

Für die Anforderung: Die Snapshot-Dedup (Z. 74-76) ignoriert eine Beobachtung mit unverändertem `BodyHash`, sobald `IdentityResolved` gesetzt ist — eine erneute Zuweisung mit gleichem Beschreibungstext erzeugt heute **kein** neues `ScmIncomingEvent`.

## `ScmEventOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` (1154 Zeilen; relevante Ausschnitte)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DeliveryStabilityPeriod` | `internal static` | 10 s Stabilitätsfrist vor Prompt-Zustellung |
| `DispatchDueEventsAsync` | `public` | Fällige Ereignisse in `ScmDispatchPriority`-Reihenfolge dispatchen |
| `DispatchAsync` / `OpenFromQueueAsync` | `public` | Zustellung eines einzelnen Ereignisses unter Aufgabenlease |
| `DispatchCoreAsync` | `private` | Zustellkern: `Purpose == IssueAufbereitung` → eigener Pfad (`DispatchPreparationAsync`); Receipt-Reaktion (`eyes`) nur für ContentKind ≠ `Description`; Hostaktionen nachholen; `DeliveryAttemptId != null` → `ReconcileAttemptAsync`; Reihenfolge-/Block-Prüfungen; Revalidation `GetRejection` (Toleranz für `"Kein Trigger"`/`NoMentionRejection`); CLI-Auflösung/-Start, `CheckExchangeAccessAsync`, `PrepareAsync`, `WritePromptAsync` |

Prompt-Zusammensetzung (Z. 315-317): `ScmPromptAppendix.Resolve` liefert den konfigurierbaren Suffix; `ScmExchangeRequest` trägt `observation.Body` als `Body`, `observation.ContentKind.ToString()` als `EventKind`, `owner/name` als `Repository`, `observation.Url` als `OriginUrl`. **Kein Auslöser-Herkunftsfeld.**

## `ScmEventExchangeProtocol` (Prompt-Teil)
Datei: `src/Softwareschmiede/Application/Services/ScmEventExchangeProtocol.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PrepareAsync` (Z. 276-304) | `public` | Legt Versuchsverzeichnis + `request.json` an; optionaler `promptSuffix`-Parameter landet auf `ScmExchangeAttempt.PromptSuffix` |
| `BuildPrompt` (Z. 558-575) | `public static` | Einzeiliger JSON-Prompt: `instruction`, `request`, `requestPath`, `attachmentsPath`, Schemata; **`promptSuffix`-Feld nur bei gesetztem `PromptSuffix`** — konfigurierbar, kein fester Herkunftshinweis |
| `ValidateRequest` | `private static` | Korrelationspflichtfelder der Protokollversion 1 |

## `ScmPromptAppendix`
Datei: `src/Softwareschmiede/Application/Services/ScmPromptAppendix.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SettingKeys` | `public static` | Fünf Schlüssel: `scm.prompt.append.general`, `.issue`, `.issuecomment`, `.pr`, `.prcomment` |
| `SpecificKeyFor` | `public static` | `ContentKind == Description` → Referenz-Anhang (Issue/PR); sonst Kommentar-Anhang der Referenzart |
| `Combine` / `Resolve` | `public static` | Allgemein + spezifisch mit Leerzeile kombiniert; `null` bei keiner Konfiguration |

## `ScmEventExchangeService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventExchangeService.cs`

2-s-`PeriodicBackgroundService`; ruft `DispatchAsync` für alle Ereignisse mit gesetztem `DeliveryAttemptId` (Kanalabgleich). Trigger-agnostisch.

## `ScmCheckpointSupport`
Datei: `src/Softwareschmiede/Application/Services/ScmCheckpointSupport.cs` (`internal static`)

`RecordFailureAsync`/`ApplyFailure` (Backoff-Matrix, `Authentication`/`Permission` dauerhaft, `Permanent` nur mit `retryPermanent`), `ResolutionKey`, `PruneResolutionCheckpointsAsync`, `EnumerateRepositoryAsync` (Enumerations-Watermark).

## `GitHubPlugin` (Ereignis-Teil)
Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs` (partial; 647 Zeilen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetEventRepositoryAsync` | `public` | Auflösung primär per GraphQL (`viewer`, `repository`, `viewerPermission`), REST-Fallback bei `Temporary`/`RateLimit`; TTL-Cache je Client-Instanz |
| `NormalizeRepositoryAlias` | `public` | Kanonische Form `https://{host}/{owner}/{name}` |
| `ReadEventsAsync` | `public` | Je `ContentKind`: `Description` → GraphQL `issue/pullRequest { TextFields }`; `PullRequestReview` → `reviews` (PENDING ausgeschlossen, ≤ Watermark); `IssueComment`/`ReviewComment` → REST `/comments?per_page=100[&since=…]` + GraphQL-Node je Kommentar; `AddObservation` filtert Viewer-Eigentexte mit Marker und sammelt `ViewerReactions` |
| `EnumerateReferencesAsync` | `public` | Offene Issues/PRs + geschlossene mit Aktivität seit `sinceUtc` (GraphQL-Pagination) |
| `ReadIssueAsync` / `ReadPullRequestAsync` | `public` | REST-Detailreads für die Aufgabenanlage |
| `EnumerateNotificationsAsync` (Z. 271-300) | `public` | `GET notifications?all=true&participating=true&per_page=100[&since=]` über `ClientFor(SeparateAccount)` (Agent-Token); mappt `subject.type` Issue/PullRequest auf `ScmNotification` inkl. **`reason` unverändert — alle Reasons werden geliefert, kein Filter**; Nummer aus letztem `subject.url`-Segment; andere Subject-Typen werden übersprungen |
| `ClientFor` | `private` | Kontowahl: `SeparateAccount` → Agent-Client (`GitHubAgentTokenCredentialKey`); fehlendes Token → `Authentication`-Fehler |
| Transport-Helfer | `private` | `RestPagesAsync` (Link-Pagination, Zielvalidierung), `GraphAsync` (Fehlerklassifizierung, Rate-Limit-Gates, Cache-Eviction), `SendEventApiAsync`/`EvaluateResponse`/`OnTransportFailure`, `SinceParameter` (`since` minus 2 h `SinceSafetyMargin` gegen DST-Verschiebung) |

Für die Anforderung: Es existiert **keine** Timeline-/Events-Abfrage zur Ermittlung des Zuweisenden — weder REST `issues/{n}/events`/`timeline` noch GraphQL `AssignedEvent`.
