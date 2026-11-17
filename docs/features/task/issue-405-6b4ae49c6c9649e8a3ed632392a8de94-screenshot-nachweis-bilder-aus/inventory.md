# Bestandsaufnahme: Screenshot-Nachweis — Bilder aus CLI-Läufen hostseitig hochladen

**Quelle:** Issue #405 · übersetzte Anforderung: [requirement.md](requirement.md)
**Stand:** Branch `task/issue-405-6b4ae49c6c9649e8a3ed632392a8de94-screenshot-nachweis-bilder-aus`, unveränderter Repository-Bestand. Keine Bewertung, keine Umsetzung; Tests wurden nicht ausgeführt.

## Ergebnis in Kürze

Der SCM-Antwortkanal (`ScmEventExchangeProtocol`) ist ein gesichertes, git-exkludiertes Dateiprotokoll unter `.softwareschmiede-scm-exchange/{cliRunId}/{attemptId}/` — es transportiert heute ausschließlich JSON-Korrelationsmeldungen (`accepted.json`, `actions/*.json`, `results/*.json`, `complete.json`); ein Bild-/Datei-Einsammelweg existiert nicht. Der Abschluss (`complete.json` → `ScmExchangeComplete`) wird strikt deserialisiert (`UnmappedMemberHandling.Disallow`) und semantisch validiert; der `report`-Text wird gelesen, aber hostseitig nirgends an GitHub gesendet — der „Abschlussbericht" ist faktisch die `body` einer `comment`-Kanalaktion (bzw. selbst per `gh` gesetzter Agenten-Text). An GitHub gesendete Texte laufen ausschließlich über `ScmResponseRequest.Body` → `GitHubPlugin.ResponseBody` → `ExecuteResponseAsync`/`ReconcileResponseAsync` mit Marker-Reconcile. Der HTTP-Transport (`GitHubHttpApiClient`/`IGitHubEventApiClient`) kann nur JSON-Bodies gegen `api.github.com` bzw. `{host}/api/v3` — weder ein binärer Upload noch der Fremd-Origin `uploads.github.com` ist abbildbar; auch ein `PATCH` existiert nirgends im Plugin. `repository_id` im Sinne der numerischen Repo-ID wird aktuell nicht gelesen (`ScmEventRepository.RepositoryId` = `node_id`).

## Detaildokumente

- [inventory/austauschprotokoll.md](inventory/austauschprotokoll.md) — Protokoll-Records, Verzeichnis-/Dateipfade, Sicherheitskonventionen, `complete.json`-Parsing/-Validierung
- [inventory/github-plugin.md](inventory/github-plugin.md) — Authentifizierung/Konto-Routing, HTTP-Transport, API-Hilfen, Kommentar-/PR-Antwortwege, Fehlertaxonomie
- [inventory/orchestrierung-und-prompt.md](inventory/orchestrierung-und-prompt.md) — Orchestrator/Outbox/Adapter, `promptSuffix`-Fluss, Upload-/MIME-Konventionen, Agenten-Simulator
- [inventory/tests-und-doku.md](inventory/tests-und-doku.md) — Testlandschaft (Unit/Integration/E2E, Fakes/Fixtures) und Dokumentationsorte

## 1. SCM-Event-Austauschprotokoll und Dateipfade

`src/Softwareschmiede/Application/Services/ScmEventExchangeProtocol.cs` (479 Z.):

- `DirectoryName = ".softwareschmiede-scm-exchange"` (Z. 83) unter dem tatsächlichen CLI-`WorkingDirectory`; Besitz-Marker `.channel-v1` (Z. 86, 210–219).
- Versuchslayout `{runId:D}/{attemptId:D}/`: `request.json` (unveränderlich, bitgleich geprüft), `accepted.json`, `actions/{actionId:D}.json`, `results/{actionId:D}.json`, `complete.json`. Kein `screenshots/`-Unterverzeichnis; `PrepareAsync` erzeugt nur `actions/` + `results/` (Z. 186–187) und verweigert existierende Versuchsverzeichnisse (Z. 183–184).
- Sicherheitskonventionen, die für jede neue Datei-Einsammelstelle gelten: `EnsureSafePath` (Z. 391–396, Reparse-Point-Verbot auf jedem Pfadglied), `CreatePrivateDirectory`/`VerifyPrivateDirectory` (Z. 398–438, Owner-ACL bzw. 0700 + optionale `RestrictedWriteSids`), `EnsureGitExcludedAsync` (Z. 440–458, `info/exclude` + `check-ignore`), `PreparationLock` (Z. 88).
- `ReadAsync` (Z. 229–271) liest **nur** die drei JSON-Arten; fremde Dateien im Versuchsverzeichnis werden ignoriert (Strenge gilt nur in `actions/`). Zwei identische Reads nötig (`ReadStableAsync`, Z. 356–369) — Auslösung über `ScmEventExchangeService` (2-Sekunden-Poll) und Orchestrator-Pfade.
- `MaximumMessageBytes = 1 MiB` (Z. 85) gilt für jede via `ReadBoundedAsync` gelesene Kanaldatei (Z. 371–380); einen Binär-Lesepfad gibt es nicht.
- Persistenz des Versuchs am Ereignis: `ScmIncomingEvent.ExchangePath`, `ExchangeRestrictedWriteSidsJson`, `DeliveryAttemptId`, `CliRunId` (`src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs` Z. 45–55); Rekonstruktion in `ReconcileAttemptAsync` (Orchestrator Z. 383–390). `ResendAsync` (Z. 305–319) löst einen neuen Versuch mit neuem `attemptId`-Verzeichnis aus.

## 2. `complete.json`-Parsing und -Validierung

- `ScmExchangeComplete` (Protocol Z. 52–54): `ActionIds`, `NoRemoteAction`, `Reason`, `Report`, `PullRequestNumber` — **kein `attachments`-Feld**.
- Deserialisierung mit `JsonSerializerDefaults.Web` + **`UnmappedMemberHandling.Disallow`** (Z. 89–93): Ein unbekanntes Feld fällt heute in `JsonException` → `Diagnostic` → `VerarbeitungUngeklärt` (Z. 267–270; Orchestrator Z. 393–397).
- Validierung (Z. 257–263): `ActionIds` ≤ 128/eindeutig/nicht leer-Elemente; `PullRequestNumber > 0`; `NoRemoteAction` ⇒ leere `ActionIds` + `Reason`; sonst `ActionIds` oder nicht-leerer `Report` nötig.
- `completeSchema`-Prompttext (Z. 305) + `instruction` (Z. 300) dokumentieren das Schema; `BuildPrompt` Z. 296–312.
- Orchestrator-Abschluss (`ScmEventOrchestrator.cs` Z. 426–436): `Verarbeitet` erst bei Mengengleichheit erfolgreicher Kanalaktionen (bzw. `noRemoteAction` ohne Aktionen). **`complete.Report` wird nicht weiterverarbeitet/versendet** — es dokumentiert nur agentenseitig ausgeführte Remoteaktionen. `PullRequestNumber` → `LinkReportedPullRequestAsync` (Z. 464–482).

## 3. Prompt-Anhänge / `promptSuffix`

- Fünf frei konfigurierbare Texte (`scm.prompt.append.{general,issue,issuecomment,pr,prcomment}`, `AppEinstellungService` Z. 52–64) — **keine ausgelieferte Vorgabe**; gespeichert in `AppEinstellungen` via `SetSettingAsync` (Z. 161), gelesen gebündelt via `GetScmPromptAppendicesAsync` (Z. 389–390).
- Auflösung `ScmPromptAppendix` (`ScmPromptAppendix.cs`: `SpecificKeyFor`/`Combine`/`Resolve`) erst beim Zustellversand im Orchestrator (Z. 202); `PrepareAsync(promptSuffix:)` → `ScmExchangeAttempt.PromptSuffix` (Protocol Z. 32) → `promptSuffix`-Feld im JSON-Prompt (Z. 309–310). `request.json`, Inbox-Text, Dedup und Triggerprüfung bleiben anhangsfrei; die Kanalprobe trägt nie einen Anhang.
- UI-Fläche: `SettingsViewModel` Z. 545–550 (Einstellungen → Automatisierung → Aufgaben ausführen).

## 4. GitHub-Plugin-Authentifizierung und HTTP-/API-Hilfen

- `GitHubPlugin` (einziger `IScmEventResponder`): Konto-Routing `ClientFor` (Events.cs Z. 334–341) auf `_eventApiClient` (Token `Softwareschmiede.GitHub.Token`) / `_agentEventApiClient` (`…AgentToken`), Token pro Request aus `ICredentialStore` (GitHubPlugin.cs Z. 21–22, 156–157).
- `IGitHubEventApiClient`/`GitHubHttpApiClient`: `GitHubEventApiRequest(Host, Method, Endpoint, JsonBody)` — nur JSON-`StringContent`; `BuildRequestUri` mappt `github.com`→`api.github.com`, sonst `{host}/api/v3` (GitHubHttpApiClient.cs Z. 63–72). **`uploads.github.com` ist mit diesem Mapping nicht erreichbar** (würde zu `{host}/api/v3/…`); `ValidateHost` (Z. 77–88) würde den DNS-Host zwar annehmen, die URI-Bildung aber falsch zusammensetzen. Bearer nur im `Authorization`-Header (Z. 43–44).
- Fehlersemantik: `SendEventApiAsync` (Events.cs Z. 348–374) mit `ScmProviderFailureKind`-Matrix, `Retry-After`, `OutcomeUncertain` bei Mutationen — Outbox-Retries hängen daran (`ScmEventOutbox` Z. 63–73).
- `GetEventRepositoryAsync` (Events.cs Z. 29–47): `GET user` + `GET repos/{o}/{r}`; `RepositoryId` ← **`node_id`**, nicht die numerische `id` — letztere wird nirgends im Bestand gelesen (für `repository_id` des Upload-Endpoints relevant).
- `PATCH`/`PUT`/Multipart/Binary-Content: **nichts vorhanden** (Repository-weite Suche; einziger `StreamContent` ist der E2E-Testseam `UpdateFixtureHttpMessageHandler`).

## 5. `ResponseBody` / Kommentar- und PR-Antwortwege

- `ResponseBody(request)` (`GitHubPlugin.EventResponses.cs` Z. 140): `Body + "\n\n" + Marker`; genutzt für `Comment`- und `PullRequest`-Antworten (Z. 62–71). Reconcile vor POST via `ReconcileResponseAsync` (Z. 10–36) über Marker-Containment; Identitäts-/Rechte-Gegenprüfung in `ExecuteResponseAsync` (Z. 45–49).
- `ScmResponseRequest` (Contracts `ScmEvents.cs` Z. 58–65) trägt `Body`, `Title`, Branches — kein Anhangskonzept; serialisiert in `ScmOutgoingAction.RequestJson` (Entity).
- Erzeuger im Orchestrator: receipt-`eyes` (Z. 96–100), `waiting`-Kommentar (Z. 500–507), KI-Kanalaktionen `actions/*.json` (Z. 406–408: `action.Body` → `request.Body`).
- Eigenes Kommentar-Markup: `<!-- softwareschmiede:action:{id} -->` (Antworten) und `<!-- softwareschmiede:event:{id}:waiting -->` (Wartehinweis); eigene Marker-Texte werden beim Einlesen ausgeschlossen (`AddObservation`, Events.cs Z. 246).

## 6. Upload-/HTTP-Content-/MIME-Konventionen

- Kein produktiver Datei-Upload-Code: keine `ByteArrayContent`/`StreamContent`/`MultipartFormDataContent`-Verwendung außerhalb des E2E-Testseams.
- Einzige MIME-/Dateivalidierungskonvention: `BenachrichtigungsEinstellungenService` (Audio-Upload Benutzer→DB): Extension-/MIME-Whitelists (Z. 15–30), 10-MB-Limit, Magic-Bytes-Signatur (Z. 154–256), Extension→MIME-Normalisierung (Z. 194–208), `SanitizeDateiname` (Z. 183–192).

## 7. Relevante Tests und Dokumentationsorte

Kurzfassung — Details in [tests-und-doku.md](inventory/tests-und-doku.md):

- **Protokoll/Prompt:** `ScmEventExchangeServiceTests` (u. a. `promptSuffix`, `complete`-Validierungsmatrix, Prompt-Schema), `ScmPromptAppendixTests`, `AppEinstellungServiceTests_ScmMonitoring`.
- **Plugin/Transport:** `GitHubHttpApiClientTests` (Host-Mapping, Header, Token-Grenze), `GitHubEventSourceTests` (Marker-Reconcile, `ResponseBody`-Verwendung, Viewer-Gegenprüfung), `FakeGitHubEventApi`-Warteschlangenfake, `GitHubNotificationSourceTests`.
- **Ende-zu-Ende-Dispatch:** `ScmDispatchFixture` + `ScmEventDispatchTests*` (schreiben `complete.json` direkt ins `ExchangePath`-Verzeichnis — Präzedenz für abgelegte Dateien), `ScmEventCliExchangeAccessTests*`, KiSimulator-Plugin (deterministischer Agent), FlaUI-`E2E_ScmEvents.cs` + `ScmE2ERuntimeControl`.
- **Doku:** `docs/help/plugins/github-ereignisse/` (9 Dateien; `report`/`pullRequestNumber`/`promptSuffix`/Anhangs-Regeln), `docs/help/einstellungen/{datenmodell,installation}.md` (Schlüsseltabelle `scm.prompt.append.*`).

## Festgestellte Bestandslücken (faktenbezogen, ohne Bewertung)

1. **Kein `attachments`-Feld möglich ohne Schema-Änderung:** `UnmappedMemberHandling.Disallow` lässt heutige `complete.json` mit Fremdfeld in `Diagnostic`/`VerarbeitungUngeklärt` enden.
2. **Kein Einsammelpfad für Nicht-JSON-Dateien:** `ReadAsync` ignoriert alles außer den drei Meldungstypen; `ReadBoundedAsync`-1-MiB-Limit ist auf JSON zugeschnitten.
3. **Upload-Host nicht abbildbar:** `GitHubEventApiRequest`/`BuildRequestUri` können weder `uploads.github.com` noch Nicht-JSON-Bodies; `ValidateNextPage`-Hostbindung würde Fremd-Origins ebenfalls ablehnen.
4. **Numerische `repository_id` fehlt:** `ScmEventRepository.RepositoryId` trägt `node_id`; `GET /repos/{o}/{r}` liefert `id` zusätzlich, wird aber nicht gelesen/persistiert.
5. **Kein nachträgliches Editieren:** kein `PATCH`-Aufruf im Plugin; PR-Texte können nur bei Erstellung (`pullRequest`-Aktion) gesetzt werden.
6. **Kein hostseitiger „Abschlussbericht":** `complete.Report` wird nicht an GitHub gesendet; Einbettung müsste sich an Kanalaktionen (`comment`/`pullRequest` über `ResponseBody`) oder an neue Antwortwege hängen.
7. **Anhangstexte ohne Vorgabe:** `scm.prompt.append.*` ist leere Anwenderkonfiguration; der Konventionspfad müsste entweder in eine neue Vorgabe/Doku oder in den Prompt selbst (`promptSuffix`/`instruction`) einfließen.
8. **Idempotenz-Anker vorhanden:** Outbox-Reconcile über Marker-Containment + `AlreadyExisted`; `ResendAsync` erzeugt neue `attemptId`-Verzeichnisse (alte Ablagen wären versuchslokal gebunden).
