# Logik: Assign-Auslöser und „Eigene Antwort"-Filterkette

Die assign-ausgelöste Verarbeitung einer Referenzbeschreibung durchläuft heute zwei aufeinanderfolgende „Eigene Ausgabe"-Schutzebenen, die beide eine vom Überwachungskonto geschriebene, marker-tragende Beschreibung verwerfen.

## `GitHubPlugin` (Ereignisteil)
Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ReadEventsAsync` (Z. 151–212) | public (`IScmEventSource`) | Liest eine Textquelle vollständig. Description-Zweig (Z. 164–169): ein GraphQL-Call (`issue`/`pullRequest(number)` + `TextFields`), danach `AddObservation(request, node, null, …)`. Der `ScmEventReadRequest` trägt keinen Auslösekontext — das Plugin kann nicht erkennen, ob der Read assign-ausgelöst ist. |
| `AddObservation` (Z. 370–402) | private static | Baut eine `ScmEventObservation` aus einem GraphQL-Knoten. **Vorfilter Z. 384:** `IsViewer(repo, authorId, authorLogin) && body.Contains("<!-- softwareschmiede:", OrdinalIgnoreCase)` ⇒ `return` — eine vom Ereignis-Konto selbst geschriebene, marker-tragende Textversion (inkl. der von `IssueBodyUpdate` geschriebenen Beschreibung) wird **für alle ContentKind-Arten** verworfen, ohne dass eine Beobachtung entsteht. Gilt somit auch für `ScmContentKind.Description`. |
| `IsViewer` (Z. 650–652) | private static | Identitätsvergleich gegen `ViewerId`/`ViewerLogin` (ID vor Login, Ordinal/OrdinalIgnoreCase). Wird auch in `GetLatestViewerAssignmentAsync` und `ReconcileResponseAsync` verwendet. |
| `GetLatestViewerAssignmentAsync` (Z. 307–337) | public (`IScmAssignmentSource`) | `GET repos/{owner}/{name}/issues/{n}/events` (deckt Issues **und** Pull Requests ab): wählt das jüngste `assigned`/`unassigned` mit `assignee` = Viewer; `assigned` liefert `ScmAssignment` mit `actor`-Fallback `assigner`, `unassigned`/kein Treffer ⇒ `null`. |
| `EnumerateNotificationsAsync` (Z. 271–300) | public (`IScmNotificationSource`) | `GET notifications?all=true&participating=true` (nur Agent-Client, `ScmAccountMode.SeparateAccount`); bildet Issue-/PR-Threads auf `ScmNotification` ab, `reason` roh übernommen. |
| `ClientFor` (Z. 485–492) | private | Konto-Routing: `SeparateAccount` ⇒ `_agentEventApiClient` (wirft `Authentication`-Fehler ohne AgentToken). |
| `GetEventRepositoryAsync` / `ResolveEventRepositoryGraphQlAsync` / `ResolveEventRepositoryRestAsync` (Z. 57–127) | public/private | Kanonische Auflösung inkl. `ViewerId`/`ViewerLogin` des Ereignis-Kontos — die Vergleichsbasis des Marker-Vorfilters. |

## `GitHubPlugin` (Antwortteil — Marker-Produzent)
Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.EventResponses.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteResponseAsync` (Z. 43–89) | public (`IScmEventResponder`) | `IssueBodyUpdate` ⇒ `PATCH issues/{n}` mit `body = composed` (Z. 76–77); `composed` endet auf `request.Marker` (`ResponseBody`, Z. 177: `"{Body}\n\n{Marker}"`). Damit ist der Autor der Remote-Beschreibung das Ereignis-Konto und der Text trägt `<!-- softwareschmiede:action:<id> -->` — genau die Kombination, die `AddObservation` Z. 384 verwirft. |
| `ReconcileIssueBodyAsync` (Z. 94–104) | private | Marker-basierter Idempotenznachweis auf dem Issue-Body (kein Viewer-Vergleich nötig, weil der Issue-Autor nicht das Ereignis-Konto sein muss). |

## `ScmEventValidationService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetRejection` (Z. 51–84) | public static | Einstieg aller Ablehnungsprüfungen (Aufnahme und Zustellungs-Gegenprüfung). `Trigger.Reason == "assign"` ⇒ Zweig in `GetAssignRejection` (Z. 55–56). Texttrigger-Pfad: frei stehende Erwähnung/Keyword (Z. 61–64), `AlreadyReactedRejection` nur für Kommentar-Arten (Z. 68–70), Viewer-Owner-Regel nur `UserAccount` (Z. 71–72), Akteur = Autor bzw. Editor (Z. 73–80), **Marker-Regel Z. 81–82: `body.Contains("<!-- softwareschmiede:", Ordinal)` ⇒ `"Eigene Antwort"`**. |
| `GetAssignRejection` (Z. 94–114) | private static | Assign-Akzeptanzkette: leerer Body ⇒ `AssignEmptyDescriptionRejection` (Z. 98–99); Zuweisender nicht bestimmbar ⇒ `AssignerUnknownRejection` (Z. 100–101); Zuweisender ∉ {Owner, Viewer, `TrustedAutomationLogins`} ⇒ `AssignerNotAllowedRejection` (Z. 102–105); **marker-tragender Body ⇒ `"Eigene Antwort"` (Z. 106–107)**; sonst angenommen. Fallback Z. 113: Ablehnung wird verworfen, wenn der reguläre Texttrigger (`GetRejection` mit `Trigger = null`, inkl. dessen Marker-Check) die Beobachtung legitimiert — der Fallback kann eine Marker-Beschreibung also selbst nie legitimieren. |
| `NormalizeTriggerKeyword` (Z. 42–43) | public static | Leerwert ⇒ `DefaultTriggerKeyword` (`$Softwareschmiede`). |
| `HasFreeStandingTrigger` (Z. 117–129) | private static | Trigger nur frei stehend (keine unmittelbaren Zitatzeichen davor/danach). |
| `SameIdentity` (Z. 131–135) / `IsTrustedAutomationLogin` (Z. 139–141) | private static | ID-vor-Login-Vergleich bzw. Vertrauensliste `github-actions[bot]`, `app/github-actions` (Z. 37). |

Konstanten: `DefaultTriggerKeyword` (Z. 11), `NoMentionRejection` (Z. 15), `AlreadyReactedRejection` (Z. 19), `AssignEmptyDescriptionRejection` (Z. 23), `AssignerUnknownRejection` (Z. 27), `AssignerNotAllowedRejection` (Z. 31), `TrustedAutomationLogins` (Z. 37).

## `ScmEventMonitoringService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunOnceAsync` (Z. 42–308) | public override | Poll-Zyklus: Einstellungen lesen, Alias-Gruppen auflösen, im `SeparateAccount` die Inbox enumerieren (Z. 152–164), danach je Referenzgruppe × ContentKind lesen. `assignNotification` = Referenzgruppe enthält eine Notification mit `Reason == AssignReason` (Z. 187). |
| Assign-Dekoration (Z. 217–236, innerhalb `RunOnceAsync`) | — | Nach `ReadEventsAsync(Description)`: `GetLatestViewerAssignmentAsync` (wenn `IScmAssignmentSource`) und `with`-Dekoration jeder Beobachtung — `RemoteNodeId += "/assign-{eventNodeId}"` (Fallback `"unbestimmt"`) und `Trigger = new ScmEventTrigger("assign", …)`. **Der Plugin-Vorfilter hat zu diesem Zeitpunkt die Viewer-Marker-Beschreibung bereits verworfen — ein leerer Batch bleibt leer.** |
| `unassignedRejection`-Aufrufstelle (Z. 265–270) | — | `AcceptBatchAsync` erhält bei assign-ausgelöstem Description-Batch das Lambda `x => GetRejection(x, triggerKeyword)` (dort greift `GetAssignRejection`); sonst `null`. |
| Resolver-Auswahl (Z. 242–253) | — | Zweistufige Aufgabenwahl (regulär vor zweck-markiert); bei `unassigned` und Kandidaten `ScmEventTaskResolver.ResolveAsync` mit der ersten nicht abgelehnten Beobachtung als Trigger. |
| `EnumerateNotificationsAsync` (Z. 350–400) | private | Liest Inbox je Host; `reason ∈ {mention, assign}`, `assign` gewinnt die Deduplizierung je Referenz (Z. 376–378); Watermark-Commit erst nach fehlerfreiem Zyklus. |
| `BuildAliasGroups` (Z. 320–345) / `IsInaccessible` (Z. 406–407) | private | Alias-Gruppierung bzw. Isolierung nicht erreichbarer Repositories im Inbox-Modus. |

## `ScmEventStore`
Datei: `src/Softwareschmiede/Application/Services/ScmEventStore.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AcceptBatchAsync` (Z. 65–159) | public | Atomare Aufnahme: je Beobachtung Snapshot pflegen, `GetRejection` bzw. `unassignedRejection` auswerten (Z. 89–93), bei gültiger Beobachtung `ScmIncomingEvent` anlegen (inkl. Verzögerungsdirektive, Z. 108–134). Checkpoint-Diagnose Z. 146–152: unzugeordneter Assign-Batch ⇒ `Inbox: {firstRejection}`, **leerer Batch ⇒ `Inbox: ` + `EmptyDescriptionBatchDiagnostic`** (Z. 148–149). |
| `EmptyDescriptionBatchDiagnostic` (Z. 25–31) | public const | `"Keine verwertbare Referenzbeschreibung"` — Kommentar benennt plugin-seitig herausgefilterte eigene Marker-Texte als Hauptursache. |
| `Observation` (Z. 38–39) | public static | Deserialisiert `ObservationJson` (inkl. `Trigger`). |
| `InboxDiscardDiagnosticPrefix` (Z. 34) | public const | `"Inbox:"` — Routine-Diagnosen ohne Unterbrechungshinweis. |
| `AmbiguousAssignmentDiagnostic` / `UnassignedDiagnostic` (Z. 22/24) | public const | Zuordnungsdiagnosen. |
| `EnqueueDelayComment` / `PauseForDelayAsync` / `ClaimTaskAsync` / `RenewTaskAsync` / `ReleaseTaskAsync` (Z. 163–212) | private/public | Verzögerungsquittung, Pausen, Aufgabenleases. |

## `ScmEventOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DispatchDueEventsAsync` (Z. 45–77) | public | Priorisierte Auswahl fälliger Ereignisse (`ScmDispatchPriority`), ruft `DispatchAsync`. |
| `DispatchAsync` / `OpenFromQueueAsync` / `MutateAndDispatchAsync` (Z. 80–131, 504–531) | public/private | Aufgabenlease + Übergabe an `DispatchCoreAsync`. |
| `DispatchCoreAsync` (Z. 133–396) | private | Zustellpfad. Zustellungs-Gegenprüfung Z. 219–232: `GetRejection` auf der persistierten Beobachtung mit frisch aufgelöstem Repository — Assign-Beobachtungen laufen erneut durch `GetAssignRejection`; jede nicht-`Kein Trigger`/`NoMention`-Ablehnung (inkl. `"Eigene Antwort"`) ⇒ `WaitAsync` mit assign-spezifischem Grund „Der Zuweisende ist nicht mehr dem berechtigten Kreis zuzuordnen." |
| `PromptBody` (Z. 538–540) | private static | `Trigger.Reason == "assign"` ⇒ `AssigneePromptPrefix + "\n\n" + Body`; Grundlage von `request.json` (Z. 336–338) und Wiederanlauf-Abgleich (Z. 545). |
| `AssigneePromptPrefix` (Z. 33) | internal const | Fester Herkunftshinweis im zugestellten Prompt. |
| `ReconcileAttemptAsync` / `ReconcilePreparationAttemptAsync` (Z. 542–…, 930–1079) | private | Wiederanlauf-/Aufbereitungs-Abgleich; erzeugt `IssueBodyUpdate`-/`Comment`-Outbox-Aktionen aus CLI-`messages.Actions` (Z. 982–1020). |
| `DispatchPreparationAsync` (aufgerufen Z. 149–152) | private | Dedizierter Ausführungspfad zweck-markierter Ereignisse ohne Trigger-Validierung. |

## `ScmEventTaskResolver`
Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ResolveAsync` (Z. 48–52) | public | Referenzart-Verzweigung Issue/PR; wird vom Monitor nur mit einer nicht abgelehnten Beobachtung als `trigger` aufgerufen — ein leerer Assign-Batch erreicht ihn nie. |
| `ResolveIssueAsync` / `ResolvePullRequestAsync` (Z. 54–126) | private | Eindeutigkeits-/Mehrdeutigkeitsauswertung, Bestandssuche (`Purpose == null` vor `Purpose == "IssueAufbereitung"`), ggf. Aufgabenanlage (`CreateFromIssueEventAsync`/`CreateFromPullRequestAsync`, Aufgabenbody via `ComposeBody` aus Referenz-/Triggertext). |
| `IssueSourceKey` / `IssueGateKey` (Z. 190–204) | internal static | Kanonischer Dedup-Schlüssel bzw. deterministisches prozessinternes Issue-Gate. |

## `ScmIssuePreparationService`
Datei: `src/Softwareschmiede/Application/Services/ScmIssuePreparationService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Purpose` (Z. 30) | public const | `"IssueAufbereitung"` — Zweck-Token der Aufbereitungsaufgaben/-ereignisse. |
| `DetectAndEnqueueAsync` (Z. 145–181) | public | „Issue erstellt"-Erkennung am Enumerationskanal (`UserAccount`); reiht das interne, trigger-freie Aufbereitungsereignis ein, dessen CLI-Ausführung später per `IssueBodyUpdate` die marker-tragende Beschreibung schreibt. |
| `EnqueuePreparationUnderGateAsync` (Z. 198–266) | private | Check-then-Create des internen `ScmIncomingEvent` unter dem Issue-Gate. |
| `BaselineKey` / `PreparationSourceKey` / `PreparationEventKey` / `EvaluationCheckpointKey` / `SkipEvaluationAsync` / `CreateBaselineAsync` / `CompleteEvaluationAsync` (Z. 35–129) | public static/internal | Checkpoints und deduplizierende Schlüssel der Aufbereitung. |

## `ScmDispatchPriority` (Auswahlhilfe)
Datei: `src/Softwareschmiede/Application/Services/ScmDispatchPriority.cs` (nur indirekt relevant — Reihenfolge des Dispatch, keine Marker-/Assign-Logik).
