# Bestandsaufnahme: Issue-Beschreibungs-Update im SCM-Antwortkanal (Schritt 4, #412-Grundlage)

**Stand:** Branch `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung-schritt-4-issue-body-update`, Basis `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung`. Ergänzend zu `docs/projects/.../inventory.md` (Lücke L5: keine Schnittstelle zum Ändern einer Issue-Beschreibung; Lücke L9: faktisch nur das GitHub-Plugin implementiert die Ereignisfähigkeiten).

## 1. Antwortvertrag (`Softwareschmiede.Plugin.Contracts`, public API mit XML-Doku)

`src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs`:

- `ScmResponseKind { Comment, Reaction, PullRequest }` (Z. 55) — die „vom Antwortkanal erlaubte Remoteaktion". **Kein Wert für eine Beschreibungsänderung.**
- `ScmResponseRequest` (Z. 61-68): `Reference` (host-validierter Ursprung), `ActionId` (deterministisch, host- oder versuchseitig errechenbar), `Kind`, `Body`, `Reaction`, `ContentKind`, `RemoteCommentId`, `RemoteNodeId`, `Title`, `HeadBranch`, `BaseBranch`, `Attachments`. `Marker` (Z. 66-67) liefert `<!-- softwareschmiede:action:{ActionId} -->` — der Wiedererkennungsmarker, mit dem Reconcile eigene Aktionen remote wiederfindet.
- `ScmResponseResult(RemoteId, Url, AlreadyExisted)` (Z. 71): `AlreadyExisted = true` signalisiert „remote bereits angewendet".
- `ScmProviderException(FailureKind, Message, RetryAfterUtc, OutcomeUncertain)` (Z. 86-103) mit `ScmProviderFailureKind { Authentication, Permission, RateLimit, Temporary, Permanent }` (Z. 83).
- **Persistenz-Hinweis:** `ScmOutgoingAction.RequestJson` serialisiert `ScmResponseRequest` mit `JsonSerializerDefaults.Web` (`ScmEventStore.JsonOptions`, `ScmEventStore.cs` Z. 29) — Enums werden **numerisch** gespeichert. Ein neuer `ScmResponseKind`-Wert muss **am Ende** angehängt werden, sonst lesen Altbestände falsche Arten.
- `IScmEventResponder` (`Domain/Interfaces/IScmEventResponder.cs`): zwei Methoden — `ReconcileResponseAsync` („Null bedeutet sicher nicht gefunden; Fehler werfen eine typisierte Ausnahme") und `ExecuteResponseAsync` („Gleicht vor dem Schreiben ab. Bei unbekanntem Schreibergebnis ist OutcomeUncertain gesetzt"). Signaturen müssen nicht geändert werden; die Vertragsdokumentation soll das „ehrliche Fehler statt stiller Ignorierung"-Gebot für nicht unterstützte Arten festschreiben.

## 2. GitHub-Plugin (`plugins/Softwareschmiede.Plugin.GitHub/`)

`GitHubPlugin.EventResponses.cs` — einzige `IScmEventResponder`-Implementierung (`GitHubPlugin.cs` Z. 19: `IScmEventSource, IScmEventResponder, IScmAgentAccess, IScmNotificationSource`; GitHubCopilot-Plugin implementiert keinen Responder):

- `ReconcileResponseAsync` (Z. 12-38): nach `ValidateResponse` Sonderpfad Review-Reaktion, sonst REST-Seiten je Art (Kommentare `/issues/{n}/comments`, PRs `/pulls`, Reaktionen `ReactionEndpoint`); Marker-bzw. Reaktions-Vergleich; Treffer nur wenn `IsViewer(repository, user.node_id, user.login)` — der **Autor der Remoteaktion ist das Ereignis-Konto**. Für ein Body-Update gilt: Die Issue-Beschreibung ist kein Kommentarobjekt; der Abgleich muss `GET issues/{n}` lesen und den Marker im `body` suchen — ein Viewer-Check entfällt, weil der Issue-Autor nicht das Ereignis-Konto sein muss (der Marker allein identifiziert die eigene Aktion).
- `ExecuteResponseAsync` (Z. 41-82): Validierung → **Ursprungsprüfung** (`GetEventRepositoryAsync` — `RepositoryId`/`ViewerId`-Gleichheit und `CanWrite`, sonst `Permission`-Fehler) → Reconcile-vor-Schreiben (`existing is not null → return`) → artbezogener Schreibaufruf. `composed` (Marker-Body) wird für `Comment`/`PullRequest` über `ComposeBodyAsync` gebaut (Z. 64-66); alle Writes laufen als `POST` über `EventApiAsync(…, mutation: true)` (Z. 76). Für `IssueBodyUpdate` braucht es `PATCH issues/{n}` mit dem markierten Body — Methodenwahl muss in den Switch.
- `ValidateResponse` (Z. 114-148): `ActionId`-Regex `^[A-Za-z0-9:_-]{1,200}$`; Attachments nur für `Comment`/`PullRequest`; `default:` → `Permanent("Unbekannte GitHub-Antwortaktion.")` — der ehrliche Fehler für unbekannte Arten existiert bereits als Muster.
- `ResponseBody` (Z. 150): `Body + "\n\n" + Marker`.
- `SafeResponseUrl` (Z. 342-350): `html_url` nur bei https, Default-Port, Host- und Pfad-Präfix des gespeicherten Repositorys — wiederverwendbar.
- `ReactionEndpoint` (Z. 316-327), `GraphReaction` (Z. 329-340): unverändert.

`GitHubPlugin.Events.cs` — Transport- und Fehlergerüst, unverändert nutzbar:

- `EventApiAsync` (Z. 301-307): JSON-(De)Serialisierung; bei Mutationen wird eine nicht auswertbare Antwort als `Temporary`+`OutcomeUncertain` geworfen — exakt die Semantik, die ein PATCH braucht (eine evtl. angewendete Änderung wird danach per Reconcile/Marker wiedergefunden, nicht blind erneut gesendet).
- `SendEventApiAsync` (Z. 348-357): Transportausnahmen → `Temporary`, bei Mutation `outcomeUncertain: true`.
- `EvaluateResponse` (Z. 362-382): Status-Matrix — 401→`Authentication`, 403→`Permission` (Rate-Limit-Auswertung 429/403+`X-RateLimit-Remaining:0`/`Retry-After`→`RateLimit`), ≥500→`Temporary`, sonst→`Permanent` (damit 404→`Permanent`, korrekt für gelöschte Issues); `Retry-After`/`X-RateLimit-Reset` → `RetryAfterUtc`.
- `ClientFor(ScmKontoModus)` (Z. 334-341): Konto-Routing Benutzerkonto/separates Bot-Konto — gilt auch für die neue Art.
- `ReadIssueAsync` (Z. 135-154): beweist `GET {RepositoryEndpoint}/issues/{n}` inkl. `pull_request`-Erkennung — Endpunkt-Pattern für den Reconcile-Read.
- `AddObservation` (Z. 246): Beobachtungen, deren Body `<!-- softwareschmiede:` enthält **und** deren Autor der Viewer ist, werden nicht als Ereignis aufgenommen — ein Marker im Issue-Body markiert die selbst geschriebene Fassung zusätzlich für Schritt 5/6 (Schleifenschutz).

## 3. Outbox und Dispatch (`src/Softwareschmiede/Application/Services/`)

`ScmEventOutbox.cs`:

- `EnsureAsync(item, purpose, request, ct)` (Z. 18-45): parst `request.ActionId` als Guid; **Kollisionsschutz** — bereits belegte ID nur bei identischem Ereignis/Zweck/Inhalt (bei `"waiting"` genügt Zweckgleichheit), sonst `ScmActionIdCollisionException`. `Purpose.StartsWith("ai:")` → `AttemptId = item.DeliveryAttemptId` (versuchsgebundene Agenten-Aktionen).
- `ExecuteAsync(action, responder, ct)` (Z. 48-89): Early-Out bei `Succeeded` bzw. nicht fälligem `NextAttemptUtc`; **Reconcile vor jedem Schreiben**; `Sending`/`Uncertain`-Zustände werden ausschließlich abgeglichen (nie blind erneut gesendet); Fehler → `Failed` bzw. `Uncertain` mit `ErrorCode` und Backoff via `ScmEventStore.RetryAt` (30 s → 2 min → 10 min → 30 min + Jitter, `RetryAfterUtc` vorrangig); `Authentication`/`Permission`/`Permanent` → `NextAttemptUtc = long.MaxValue` (harte Sperre bis manuelles „Erneut prüfen").

`ScmEventStore.cs`:

- `ActionId(eventId, purpose)` (Z. 33-35): `SHA256("{eventId:D}:{purpose}")[..16]` → deterministische, dem Agenten errechenbare Host-Aktions-ID (Zwecke bisher: `"receipt"`, `"waiting"`, `"attachments"`; Agenten-Aktionen: `"ai:{guid}"`).
- `EnqueueDelayComment` (Z. 133-145): Muster „Hostaktion ohne Dispatch-Kontext einreihen" (direkt `ScmOutgoingAction` in die DB).
- `RetryAt` (Z. 37-38): Backoff-Tabelle.

`ScmEventOrchestrator.cs`:

- `DispatchDueEventsAsync` (Z. 32-64, aus Schritt 3): wählt Ereignisse mit fälligen, nicht erfolgreichen Outbox-Aktionen (`db.ScmOutgoingActions.Any(… Status != "Succeeded" && fällig)` Z. 43-44) — **zweckunabhängig**, d. h. eine neue Hostaktion macht ihr Ereignis automatisch dispatch-würdig.
- `DispatchCoreAsync` (Z. 118-342): Plugin-Auflösung (`IScmEventSource`+`IScmEventResponder`, sonst `Wartend` „SCM-Anbieter ist nicht verfügbar", Z. 132-137); Hostaktionen-Nachholung **vor** Zustellung: `receipt`-Reaktion (Z. 139-146) und danach die Schleife über `Purpose == "waiting" || "attachments"` (Z. 147-149). **Lücke für Schritt 4:** eine neue Zweck-Kennung würde zwar das Ereignis fällig machen, aber in dieser Schleife nicht ausgeführt — der Filter muss erweitert werden (nächstliegend: alle Nicht-`ai:`-Zwecke).
- `ReconcileAttemptAsync` (Z. 467-582): `ai:`-Aktionen aus Exchange-Meldungen (`action.Kind`-Mapping Z. 492: `"comment"`/`"reaction"`/default→`PullRequest`) — versuchsgebunden, nicht Teil dieses Schritts.
- `RetryAsync` (Z. 354-364): setzt `NextAttemptUtc` aller nicht erfolgreichen Aktionen eines Ereignisses zurück — hebt damit auch Sperren der neuen Art auf.
- `EnsureHostActionAsync` (Z. 632-646): Kollisionsbehandlung → `VerarbeitungUngeklärt`.

## 4. Test-Infrastruktur

- `ScmDispatchFixture` (`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`): echte SQLite-DB + Services; `Git`-Mock als `IScmEventSource`/`IScmEventResponder` — `ExecuteResponseAsync` protokolliert jedes Request in `fixture.Responses` (Z. 80-81), `ReconcileResponseAsync` ist nicht gesetzt (Moq liefert `Task.FromResult<ScmResponseResult?>(null)`); `AddAsync` legt Projekt/Repo/Aufgabe + Ereignis an (Z. 200-220); `DispatchAsync` scoped `ScmEventOrchestrator` (Z. 275-279); optionales `time`-Argument (FakeTimeProvider) für Backoff-Tests.
- `FakeGitHubEventApi` (`src/Softwareschmiede.Tests/Helpers/FakeGitHubEventApi.cs`): Warteschlangen-Fake für `IGitHubEventApiClient` — jede Anfrage muss per `Add`/`AddStatus`/`AddFailure`/`AddUpload` eingeplant sein; `Assert.Contains(step.Endpoint, request.Endpoint)`; `AssertComplete` erzwingt vollständigen Verbrauch.
- `GitHubEventSourceTests` (`src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs`): Muster — `Identity(api)` (Z. 1065-1071: `GET user` + `GET repos/owner/repo`) vor jeder Antwort (Ursprungsprüfung), dann Reconcile-GET(s), dann Write. Marker-Reconcile-Test `CommentTimeout_ReportsUncertain_AndReconcilesMarker` (Z. 342-359), Fehler-Matrix `ReplyPermissionFailure_IsNotUncertain` (Z. 794-803), Konto-Routing `Response_RoutesByReferenceKonto` (Z. 1033-1051).
- `ScmEventDispatchTests` (`src/Softwareschmiede.IntegrationTests/Services/`): Outbox-/Dispatch-Integration über die Fixture; Datei-Muster `ScmEventDispatchTests_{Thema}.cs`.

## 5. Lücken und Konsequenzen

| Lücke | Befund | Konsequenz für Schritt 4 |
|-------|--------|--------------------------|
| Kein `ScmResponseKind` für Body-Updates | Enum hat nur Comment/Reaction/PullRequest | Neuen Wert am Enum-Ende (numerische JSON-Persistenz) |
| Reconcile deckt nur Listen/GraphQL-Fälle | Body-Update braucht Objekt-GET `issues/{n}` + Marker-Vergleich | Neuer `ReconcileIssueBodyAsync`-Pfad im Plugin |
| Host-Nachhol-Schleife whitelistet Zwecke | `waiting`/`attachments` fest codiert (`ScmEventOrchestrator.cs` Z. 147-149) | Filter auf „alle Nicht-`ai:`-Zwecke" umstellen |
| Write-Pfad kennt nur POST | `ExecuteResponseAsync` sendet ausschließlich `POST` (Z. 76) | Methoden-Tupel um `PATCH` erweitern |
| Validierung ohne Body-Update-Regeln | `default:` → Permanent | Neuer `case`: Issue-Referenz + Body-Pflicht |
| „Ehrlicher Fehler" anderer Plugins | Kein weiterer SCM-Responder vorhanden | Vertragsdoku schreibt das Gebot fest; GitHub wirft bei unbekannten Arten bereits `Permanent` |
