# Umsetzungsplan: SCM-Antwort-Art „Issue-Beschreibung aktualisieren" (Schritt 4, #412-Grundlage)

Umgesetzt wird die reine Schnittstellen-/Infrastruktur-Erweiterung aus Projektplan-Schritt 4 / Entscheidung 5: ein neuer `ScmResponseKind` für das Ersetzen einer Issue-Beschreibung, der über denselben Outbox-/Responder-Rahmen wie `receipt`/`waiting`/Agenten-Aktionen persistiert, abgeglichen, wiederholt und gesperrt wird. Das GitHub-Plugin schreibt per `PATCH issues/{n}`; der Titel bleibt unberührt. Kein produktiver Aufrufer in diesem Schritt — die Nachweise laufen über Unit- und Integrationstests.

## Umsetzungsidee

### 1. Vertrag: `ScmResponseKind.IssueBodyUpdate` (`Softwareschmiede.Plugin.Contracts`)

`src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs`:

```csharp
/// <summary>Vom Antwortkanal erlaubte Remoteaktion. <see cref="IssueBodyUpdate"/> ersetzt
/// ausschließlich die Beschreibung einer Issue-Referenz — der Titel bleibt ausdrücklich
/// unberührt. Neue Werte werden aus Kompatibilitätsgründen (numerische JSON-Persistenz in
/// ScmOutgoingAction.RequestJson) ausschließlich am Ende angehängt.</summary>
public enum ScmResponseKind { Comment, Reaction, PullRequest, IssueBodyUpdate }
```

- `IssueBodyUpdate` am Ende (Wert 3): `RequestJson` serialisiert Enums numerisch; ein Einsortieren würde den Altbestand falsch lesen. *(Annahme A2 — Wertname; Alternativen `IssueBody`/`Description` verworfen: `IssueBodyUpdate` benennt Vorgang und Grenze unmissverständlich und vermeidet die Kollision mit `ScmContentKind.Description`.)*
- `ScmResponseRequest`-Dokumentation (Z. 60) ergänzen: Bei `IssueBodyUpdate` trägt `Body` die komplette neue Beschreibung; `Title`, `Reaction`, `HeadBranch`, `BaseBranch`, `Attachments` und `RemoteCommentId`/`RemoteNodeId` sind ohne Wirkung; die Remote-Seite erhält den Body samt `Marker` (Reconcile-Anker).
- `IScmEventResponder`-Dokumentation erweitern: Plugins, die eine angeforderte Antwort-Art nicht unterstützen, melden einen typisierten `ScmProviderException`-Fehler (regelmäßig `Permanent`) — sie ignorieren die Anfrage niemals still (`ScmResponseResult` ohne Remoteaktion) und melden sie nicht als Erfolg.

### 2. GitHub-Plugin (`GitHubPlugin.EventResponses.cs`)

- **`ValidateResponse`:** neuer `case ScmResponseKind.IssueBodyUpdate:` — `Reference.Kind` muss `ScmReferenceKind.Issue` sein (PR-Beschreibungen sind ein anderer Endpunkt und nicht Teil der Art — Scope-Ausschluss, Annahme A1) und `Body` darf nicht leer sein; sonst `Permanent`-Fehler. (Attachments scheitert für diese Art bereits an der bestehenden Attachment-Prüfung.)
- **`ReconcileResponseAsync`:** nach `ValidateResponse` früher Seitenpfad wie bisher; für `IssueBodyUpdate` neuer Pfad `ReconcileIssueBodyAsync` — `GET {RepositoryEndpoint}/issues/{n}` (Objekt, keine Pagination), Treffer wenn `body` den `request.Marker` enthält → `ScmResponseResult(Required(issue, "id"), SafeResponseUrl(request, issue), AlreadyExisted: true)`; sonst `null`. Kein Viewer-Vergleich — der Marker allein identifiziert die eigene Aktion (Issue-Autor ist regelmäßig nicht das Ereignis-Konto); 404/sonstige Fehler laufen über `EvaluateResponse` typisiert (404 → `Permanent`, ehrlich).
- **`ExecuteResponseAsync`:** nach bestehender Ursprungs-/Schreibrechtsprüfung und Reconcile wird das Methoden-Tupel um `PATCH` erweitert:

```csharp
var composed = request.Kind is ScmResponseKind.Comment or ScmResponseKind.PullRequest or ScmResponseKind.IssueBodyUpdate
    ? await ComposeBodyAsync(request, client, repository, current, ct)
    : null;
var (method, endpoint, body) = request.Kind switch
{
    ScmResponseKind.Comment => ("POST", $"{RepositoryEndpoint(repository)}/issues/{request.Reference.Number}/comments", (object)new { body = composed }),
    ScmResponseKind.IssueBodyUpdate => ("PATCH", $"{RepositoryEndpoint(repository)}/issues/{request.Reference.Number}", (object)new { body = composed }),
    ScmResponseKind.Reaction => ("POST", ReactionEndpoint(request), (object)new { content = request.Reaction }),
    ScmResponseKind.PullRequest => ("POST", $"{RepositoryEndpoint(repository)}/pulls", (object)new { title = request.Title, body = composed, head = request.HeadBranch, @base = request.BaseBranch }),
    _ => throw Permanent("Unbekannte GitHub-Antwortaktion.")
};
result = await EventApiAsync(client, repository.Host, method, endpoint, body, true, ct);
```

  `ComposeBodyAsync` liefert ohne Attachments bereits `ResponseBody(request)` = `Body + "\n\n" + Marker` — die Remote-Beschreibung besteht aus der neuen Fassung plus dem ausgeblendeten Aktions-Marker. *(Annahme A3 — Marker im Issue-Body: identisch zum Kommentar-Muster, Voraussetzung für abgleichsicheres Reconcile; Nebeneffekt erwünscht: `AddObservation` überspringt `<!-- softwareschmiede:`-Texte des Viewers ohnehin — die selbst geschriebene Fassung löst keine Folgebeobachtung aus.)*
- Resultat: `id` aus der PATCH-Antwort → `RemoteId`, `SafeResponseUrl` → `Url`. Fehlersemantik kommt unverändert aus `SendEventApiAsync`/`EvaluateResponse` (Mutation → `OutcomeUncertain` bei ungewissem Ausgang; 401/403 → Sperre, 429/5xx → Wiederholung, 404/422 → `Permanent`).

### 3. Hostaktionen-Nachhol-Schleife (`ScmEventOrchestrator.DispatchCoreAsync`)

Die Schleife über nicht erfolgreiche Hostaktionen (Z. 147-149) whitelisted heute `waiting`/`attachments` und würde eine neue Zweck-Kennung zwar dispatch-würdig machen (`DispatchDueEventsAsync`-Auswahl ist zweckunabhängig), aber nie ausführen. Umstellung auf die Semantik „alle hostseitigen Aktionen":

```csharp
// Hostaktionen aller Zwecke werden unabhängig vom Zustellstatus nachgeholt; "ai:"-Aktionen
// bleiben dem versuchgebundenen Exchange-Abgleich vorbehalten.
foreach (var action in await db.ScmOutgoingActions.Where(x => x.EventId == item.Id
    && !x.Purpose.StartsWith("ai:") && x.Status != "Succeeded").ToListAsync(ct))
    await outbox.ExecuteAsync(action, responder, ct);
```

- `receipt` fällt künftig zusätzlich in die Schleife — harmlos (`ExecuteAsync` kehrt bei `Succeeded`/nicht fälligem `NextAttemptUtc` sofort zurück) und schließt nebenbei die bisherige Lücke, dass eine fehlgeschlagene `receipt`-Aktion erst beim nächsten Ereignis-Dispatch erneut lief.
- `ai:`-Zwecke bleiben ausgenommen — sie gehören zu einem Zustellversuch und werden ausschließlich im `ReconcileAttemptAsync`-Pfad mit Exchange-Ergebnisdatei abgeglichen (unverändert). EF Core übersetzt `StartsWith("ai:")` nach `LIKE 'ai:%'` (SQLite); alle `ai:`-Zwecke entstehen einheitlich in Kleinschreibung (`"ai:" + Guid:D`, `EnsureAsync` Z. 38).
- **Zweck-Konvention:** Die neue Art trägt den Outbox-Zweck `"issuebody"` mit `ScmEventStore.ActionId(eventId, "issuebody")` als deterministischer Aktions-ID — konsistent zu `receipt`/`waiting`/`attachments`; Schritt 6 übernimmt die Konvention beim Aufbereitungs-Rückschrieb (dokumentierte Festlegung, damit Tests und Folgeschritt denselben Schlüssel nutzen). *(Annahme A4.)*

### 4. Tests

**Unit-Tests `GitHubEventSourceTests`** (reale Plugin-Logik, `FakeApi`-Transport):

- `IssueBodyUpdate_Execute_PatchesDescriptionWithMarker`: `Identity` + Reconcile-GET (`issues/7`, Body ohne Marker) + `PATCH issues/7` (Response `id`/`html_url`); Assert: Methode `PATCH`, Endpunkt `repos/owner/repo/issues/7`, JSON-`body` = neue Beschreibung + Marker, `RemoteId`, kein `title`-Feld im Request-Body.
- `IssueBodyUpdate_AlreadyApplied_ReconcilesWithoutWrite`: Remote-Body enthält Marker → `AlreadyExisted`, ausschließlich GET-Anfragen, kein PATCH (Doppelschreiben-Nachweis).
- `IssueBodyUpdate_RequiresIssueReferenceAndBody` (Theory): `PullRequest`-Referenz oder leerer Body → `Permanent`-Fehler, kein Transportaufruf.
- `IssueBodyUpdate_Failures_TypedProviderErrors` (Theory): 403 → `Permission` ohne `OutcomeUncertain`; 401 → `Authentication`; 429 mit `Retry-After` → `RateLimit` + `RetryAfterUtc`; 500 → `Temporary` + `OutcomeUncertain`; 404 → `Permanent`.
- `IssueBodyUpdate_RoutesByReferenceKonto` (Theory): Benutzerkonto/SeparatesKonto — alle Aufrufe laufen über den zum Konto gehörenden Client (Muster `Response_RoutesByReferenceKonto`).

**Integrationstests `ScmEventDispatchTests_IssueBodyUpdate`** (neue Datei, `ScmDispatchFixture`):

- `IssueBodyAction_ExecutesViaOutboxWithDeterministicId`: Ereignis anlegen (`fixture.AddAsync`), Outbox-Aktion per `EnsureAsync(item, "issuebody", request(ActionId = ScmEventStore.ActionId(eventId, "issuebody"), Kind = IssueBodyUpdate, Body))` einreihen, `fixture.DispatchAsync` → `fixture.Responses` enthält genau eine `IssueBodyUpdate`-Anfrage an den gespeicherten Ursprung; `ScmOutgoingAction` ist `Succeeded` mit `RemoteId`.
- `IssueBodyAction_AlreadyApplied_SkipsExecute` : `ReconcileResponseAsync` liefert `AlreadyExisted`-Ergebnis → `ExecuteResponseAsync` wird für diese Art nicht aufgerufen (Verify), Aktion `Succeeded`, `RemoteId` übernommen.
- `IssueBodyAction_TemporaryFailure_RetriesAfterBackoff`: `ReconcileResponseAsync` → `null`, `ExecuteResponseAsync` wirft `Temporary` einmal, dann Erfolg; nach erstem Dispatch ist die Aktion `Failed` mit `NextAttemptUtc` > Jetzt; nach `FakeTimeProvider`-Vorlauf + erneutem Dispatch `Succeeded`.
- `IssueBodyAction_PermissionFailure_LocksUntilRetry`: `Permission`-Fehler → `Failed`, `NextAttemptUtc == long.MaxValue`, `ErrorCode == "Permission"`; `orchestrator.RetryAsync` hebt die Sperre auf, erneuter Dispatch führt aus.
- `IssueBodyAction_UnsupportedPlugin_ReportsHonestError`: Responder wirft für `IssueBodyUpdate` `Permanent("nicht unterstützt")` → Aktion `Failed` mit `ErrorCode == "Permanent"` und Fehlertext — kein stiller Erfolg, keine Ignorierung.

## Akzeptanzszenarien

1. **Happy Path (Integration):** Ein Host reiht eine deterministische `issuebody`-Outbox-Aktion für ein gespeichertes Ereignis ein; der Dispatch führt sie über `IScmEventResponder.ExecuteResponseAsync` aus — genau ein `IssueBodyUpdate`-Request an den validierten Ursprung, Aktion endet `Succeeded` mit `RemoteId`. **Nachweis:** Integrationstest 1.
2. **Abgleich nach Neustart/Ausfall (Reconcile):** Die Aktion wurde remote bereits angewendet (Marker im Issue-Body bzw. `AlreadyExisted`-Ergebnis) — Reconcile erkennt sie, `ExecuteResponseAsync` läuft nicht, kein Doppelschreiben. **Nachweis:** Unit-Test „AlreadyApplied" + Integrationstest 2.
3. **Ungewisser Ausgang:** Schreibversuch mit ungewissem Ergebnis (`OutcomeUncertain`) → Aktion `Uncertain`, nur noch abgeglichen. **Nachweis:** Fehlersemantik geerbt von `EvaluateResponse`/`ExecuteAsync` (Unit 500-Fall; Outbox-Pfad durch Integrationstest 3 abgedeckt).
4. **Sperrfehler:** Authentication/Permission → harte Sperre (`long.MaxValue`), aufhebbar über „Erneut prüfen" (`RetryAsync`). **Nachweis:** Unit 401/403 + Integrationstest 4.
5. **Plugin ohne Fähigkeit:** typisierter ehrlicher Fehler, persistiert und sichtbar — kein stilles Ignorieren. **Nachweis:** Integrationstest 5 + Vertragsdokumentation.

## Offene Punkte

Keine — alle Entscheidungen aus Projektplan/Entscheidung 5 verbindlich; Annahmen A1–A4 oben dokumentiert.

## Testbedarf

- Unit: 5 neue Testfälle in `GitHubEventSourceTests` (siehe §4).
- Integration: 5 neue Testfälle in `ScmEventDispatchTests_IssueBodyUpdate.cs` (siehe §4).
- Keine E2E-/FlaUI-Tests — keine UI-Änderung (Ausführung ausschließlich über den automatisierten Outbox-Pfad).
- Reale End-to-End-Abnahme: Nicht durchführbar im Sinne eines Benutzerflusses — kein produktiver Aufrufer und kein echter GitHub-Call in diesem Schritt (ausdrückliche Vorgabe: „Mock-/Integrationsebene reicht, kein echter GitHub-Call"); dokumentiert in `test-results.md`.
