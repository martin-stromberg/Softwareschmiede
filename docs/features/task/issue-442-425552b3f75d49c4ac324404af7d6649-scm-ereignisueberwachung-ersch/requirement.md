# Anforderung (übersetzt) – SCM-Ereignisüberwachung erschöpft GitHub-REST-Rate-Limit durch zyklische Repository-Neuauflösung

Quelle: GitHub-Issue **#442** (Kundenanforderung vom 2026-11-21, verifiziert am
Konto `softwarewerkstatt`, Kontomodus „Separates Konto").

## Ausgangslage

Das primäre REST-Rate-Limit des Ereignis-Kontos ist dauerhaft erschöpft
(`X-RateLimit-Used: 5000`, `Remaining: 0`), während das GraphQL-Kontingent
praktisch frei ist (~50/5000). Folge: Alle `repository/*`-Checkpoints stehen in
RateLimit-Backoff, es werden keine Ereignisse eingelesen, wartende
`IssueAufbereitung`-Ereignisse scheitern im Retry am ersten Call, und die
`gh`-Aufrufe der Agenten-CLIs (dasselbe Token via `GH_TOKEN`) schlagen mit 403
fehl.

Verifizierte Ursache: `GitHubPlugin.GetEventRepositoryAsync` kostet pro Aufruf
zwei REST-Calls (`GET /user` + `GET /repos/{owner}/{name}`), wird aber von
mehreren Diensten und Pfaden zyklisch bzw. pro Retry ungecacht aufgerufen:

- `ScmEventMonitoringService.RunOnceAsync` löst je Poll-Zyklus
  (`ScmEventOptions.PollInterval` = 60 s, keine appsettings-Überschreibung)
  **jede** Alias-Gruppe neu auf. Die Alias-Gruppierung erfolgt über den rohen
  `RepositoryId`-/`RepositoryUrl`-String, sodass `https://github.com/o/r` und
  `o/r` desselben Repositories zwei getrennte Auflösungen auslösen
  (~19 Repositories × ~2 Alias-Formen ≈ ~38 Gruppen ≈ ~76 REST-Calls/min ≈
  ~4.500/h).
- Dasselbe Alias-Loop-Muster existiert in `ScmIssuePreparationMonitoringService`
  (Checkpoint-Präfix `issueaufbereitung-repository/`) und
  `ScmBackmergeMonitoringService` (`backmerge-repository/`), jeweils unter
  `ScmAccountMode.UserAccount`.
- `ScmEventOrchestrator` ruft die Auflösung bei **jeder** Ereignis-Zustellung
  bzw. jedem stündlichen Retry erneut auf (Schreibrechte-/Identitätsprüfung;
  `DispatchPreparationAsync` beginnt damit).
- `GitHubPlugin.ExecuteResponseAsync` löst pro Outbox-Aktion erneut auf
  (Identitätsvergleich „Repository- oder Benutzeridentität hat sich geändert").
- `ScmTaskCompletionMonitoringService` löst je Issue-verknüpfter Aufgabe auf.

Nebenbefunde aus dem Issue (nicht zwingend Teil des Scopes, s. Offene Fragen):
`receipt-scanner`-Checkpoint mit 470 Failures wird als `RateLimit` statt als
isoliertes `Permanent`/`Permission` geführt, weil `IsInaccessible` nur
`Permanent`/`Permission` kennt und der Fehler während der Kontingenterscheöpfung
als RateLimit klassifiziert wurde; Tageslog stoppte am Serilog-
`fileSizeLimitBytes` (1 GiB); 4 `ai:*`-Outgoing-Actions `Failed`; `gh auth
status`-Fehlalarm.

## Fachliche Zusammenfassung

Die zyklische SCM-Repository-Auflösung wird so restrukturiert, dass sie das
primäre GitHub-REST-Rate-Limit nicht mehr erschöpft: Erfolgreiche Auflösungen
(Repository-Metadaten + Viewer-Identität als `ScmEventRepository`) werden
kanonisch gruppiert, TTL-basiert über Zyklen hinweg gecacht und die
Viewer-Identität pro (Host, Konto) nur einmal bzw. langlebig ermittelt —
vorzugsweise über das freie GraphQL-Kontingent. Zusätzlich sperrt ein
Rate-Limit-403 den betroffenen (Host, Konto)-Transport fail-fast bis
`X-RateLimit-Reset`, statt pro Quelle weiterzufeuern.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die Auflösung eines lokalen Repository-Alias in die
  kanonische Provider-Identität (`ScmEventRepository`: Node-ID, Owner, Name,
  Viewer-Identität, Schreibberechtigung, numerische Repository-ID) je
  (Plugin-Prefix, Host, Konto). Beobachtet wird weiterhin dieselbe Menge an
  Repositories und Referenzen — geändert wird ausschließlich Häufigkeit und
  Transport der Auflösung.
- **Auslösende Ereignisse:** Poll-Zyklus von `ScmEventMonitoringService`,
  `ScmIssuePreparationMonitoringService`, `ScmBackmergeMonitoringService` und
  `ScmTaskCompletionMonitoringService` (`PeriodicBackgroundService` im
  `ScmEventOptions.PollInterval`-Rhythmus); jede Ereignis-Zustellung aus
  `ScmEventOrchestrator` (generischer Pfad und `DispatchPreparationAsync`);
  jede Outbox-Ausführung via `GitHubPlugin.ExecuteResponseAsync`.
- **Erwartete Wirkung:** Das REST-Call-Volumen der Repository-Auflösung sinkt
  von ~76 Calls/min auf Cache-Misses (ein Auflösungs-Call je Repository pro
  TTL-Fenster, Viewer-Identität einmal je (Host, Konto)). Die
  `repository/*`-Checkpoints laufen ohne RateLimit-Backoff durch, Ereignisse
  werden wieder eingelesen, `IssueAufbereitung`-Retries bestehen den ersten
  Call, und freiwerdendes Kontingent steht den eigentlichen
  Beobachtungs-/Dispatch-/Antwort-Calls sowie den `gh`-Aufrufen der CLIs zur
  Verfügung. Bei erschöpftem Kontingent scheitern weitere Calls desselben
  (Host, Konto)-Transports sofort mit `RateLimit`-Fehler samt
  `RetryAfterUtc` — ohne einen weiteren HTTP-Request zu verbrennen.
- **Umgang mit fehlenden Objekten:**
  - Cache-Eintrag (Auflösungsergebnis / Viewer-Identität): **erzeugen** beim
    ersten erfolgreichen Zugriff, **wiederverwenden** bis TTL-Ablauf.
  - Fehlgeschlagene Auflösung: **nicht cachen** — das bestehende
    `repository/*`-Checkpoint-Backoff (`ScmCheckpointSupport.RecordFailureAsync`
    mit `retryPermanent: true`) bleibt maßgeblich.
  - Providerweite Rate-Limit-Sperre: **erzeugen** beim ersten
    `ScmProviderException` mit `FailureKind.RateLimit` und `RetryAfterUtc`,
    **verwerfen** nach Ablauf des Reset-Zeitpunkts.
  - Token-/Kontowechsel: Cache-Schlüssel muss das Konto enthalten (bei
    plugin-internem Cache über die getrennten `IGitHubEventApiClient`-Instanzen
    `_eventApiClient`/`_agentEventApiClient` implizit gegeben); ein neuer Token
    muss nicht mit staler Viewer-Identität arbeiten.
- **Scope-Ausschlüsse:**
  - Keine Änderung des `PollInterval`-Rhythmus oder des Scheduler-Modells —
    der 60-s-Poll bleibt Sicherheitsnetz.
  - Kein neues Interface-Member an `IScmEventSource` als zwingende Vorgabe;
    die Semantik von `GetEventRepositoryAsync` (kanonische Identität inkl.
    Schreibprüfung) bleibt bestehen.
  - Keine Änderung an der `gh`/git-Token-Trennung (`GH_TOKEN` der Agenten-CLIs)
    und kein Eingriff in den CLI-seitigen Kontingentverbrauch.
  - Folgeschäden (`ai:*`-Actions `Failed`, `gh auth status`-Fehlalarm, bereits
    verpasste Ereignisse) werden nicht separat repariert — sie beheben sich
    durch Wiederherstellung der Überwachung bzw. Backoff-Auslauf.
  - Bitbucket und andere Provider: betroffen ist der GitHub-Pfad; nur das
    GitHub-Plugin implementiert `IScmEventSource`. Hostseitige
    Alias-Kanonisierung/Cache-Nutzung soll providerneutral bleiben.
  - Das Serilog-`fileSizeLimitBytes`-Problem (Log stoppt bei 1 GiB) ist ein
    eigenständiger Nebenbefund — nur Teil des Scopes, wenn im Review als
    zusammengehörig entschieden.
  - `viewer`-/Repository-Revalidierung in den Dispatch-/Antwort-Pfaden bleibt
    semantisch unverändert (gleiche Prüfungen, nur ggf. aus Cache bedient);
    eine durch Caching verlängerte Erkennungslatenz bei Berechtigungsentzug ist
    zu bewerten, nicht stillschweigend hinzunehmen.

## Betroffene Klassen und Komponenten

- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`
  → `GetEventRepositoryAsync` (derzeit 2 REST-Calls: `GET user`, `GET
  repos/{owner}/{name}`); `ParseEventRepository` (bereits vorhandene
  Alias-Normalisierung zu `(Host, Owner, Name)`); `SendEventApiAsync` /
  `EvaluateResponse` (RateLimit-Klassifikation und `RetryAfterUtc` aus
  `Retry-After`/`X-RateLimit-Reset` bereits vorhanden).
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubHttpApiClient.cs` und
  `IGitHubEventApiClient` — mögliche Ebene des providerweiten
  Rate-Limit-Fail-fast-Gates (Instanz = Konto; Schlüssel zusätzlich um Host
  und ggf. Budget-Typ REST vs. GraphQL zu differenzieren).
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.EventResponses.cs`
  → `ExecuteResponseAsync` (Identitäts-Revalidierung; profitiert vom
  Auflösungscache, muss `current.RepositoryId`/`ViewerId`-Vergleich und
  `CanWrite`-Prüfung unverändert ausführen).
- `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`
  → `RunOnceAsync` Alias-Schleife: Gruppierung über kanonischen
  (Host/Owner/Name)-Schlüssel statt Roh-Alias; `IsInaccessible`-Logik gegenüber
  dem `receipt-scanner`-Nebenbefund prüfen.
- `src/Softwareschmiede/Application/Services/ScmIssuePreparationMonitoringService.cs`
  und `ScmBackmergeMonitoringService.cs` — identisches
  Auflösungs-Loop-Muster, gleiche Behandlung (Checkpoint-Keys
  `issueaufbereitung-repository/*` bzw. `backmerge-repository/*`; Kanonisierung
  der Keys zu bewerten, da Alt-Checkpoints sonst verwaist).
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`
  (Zeilen ~193 generischer Dispatch, ~654 `DispatchPreparationAsync`) und
  `ScmTaskCompletionMonitoringService.cs` (~205) — Call-Sites, die vom
  Plugin-Cache automatisch profitieren, wenn die Cache-Ebene im Plugin liegt.
- `src/Softwareschmiede/Application/Services/ScmEventOptions.cs` — neue
  Option(en) für Cache-TTL (vorgeschlagen), `PollInterval` unverändert.
- Neu zu erstellen (Alternativen): TTL-Cache für `ScmEventRepository` —
  entweder plugin-intern in `GitHubPlugin` (deckt alle Call-Sites ab,
  empfohlener Ansatzpunkt) oder als hostseitiger Service nach dem Vorbild von
  `DirectoryStructureBrowserService`/`DiffCachingService` (`IMemoryCache`).
- `src/Softwareschmiede.Plugin.Contracts` — unverändert, sofern der Cache
  plugin-intern bleibt (`ScmEventRepository` ist bereits ein unveränderliches
  Record-DTO und damit cachebar).
- Tests: `GitHubEventSourceTests` (u. a. `GetEventRepositoryAsync`-Tests, die
  heute 2 REST-Calls erwarten → bei GraphQL-Umstellung Fixture-Anpassung),
  `ScmEventMonitoringServiceTests_RepositoryResolution/-RepoWide/-Notifications`,
  `ScmEventOrchestratorTests`, `ScmIssuePreparationMonitoringServiceTests`,
  `ScmBackmergeMonitoringServiceTests`, `GitHubHttpApiClientTests` (Gate),
  `ScmDispatchFixture` (Mock-Verhalten von `GetEventRepositoryAsync`).

## Implementierungsansatz

Aus dem Lösungsvorschlag der Anforderung abgeleitet; die Punkte sind als
kombinierbare Maßnahmen zu verstehen, die Reihenfolge/Priorisierung folgt dem
Umsetzungsplan:

1. **Viewer-Identität pro (Host, Konto) cachen:** `GET /user` (bzw.
   `viewer{login id}` bei GraphQL) einmal je Client-Instanz + Host statt pro
   Alias auflösen. `_eventApiClient`/`_agentEventApiClient` bilden die
   Konto-Trennung bereits ab.
2. **Auflösung über GraphQL** (Lastverschiebung auf das freie Kontingent):
   Eine Query `viewer{login id}` + `repository(owner,name){id databaseId name
   owner{login id} viewerPermission}` ersetzt beide REST-Calls. Mapping:
   GraphQL-`id` → `RepositoryId`/`ViewerId`/`OwnerId` (Node-IDs),
   `databaseId` → `NumericRepositoryId`, `viewerPermission`
   (`ADMIN`/`MAINTAIN`/`WRITE`) → `CanWrite`. REST-Pfad ggf. als Fallback
   behalten (GHE-Kompatibilität klären).
3. **Alias-Kanonisierung:** Gruppierungs- und Checkpoint-Schlüssel über das
   kanonische `(Host, Owner, Name)` aus `ParseEventRepository` (bzw.
   providerneutrales Äquivalent), sodass `owner/name`,
   `https://github.com/owner/name` und SSH-Formen auf denselben
   Auflösungsschlüssel fallen.
4. **TTL-Cache der Auflösung:** `ScmEventRepository` je kanonischem Schlüssel +
   Konto über Zyklen hinweg cachen; erfolgreiche Auflösungen werden nicht mehr
   je Zyklus wiederholt; Fehler bleiben im Checkpoint-Backoff. Bestehender
   Erfolgs-Pfad (Checkpoint-Entfernung + `ScmEventChangeNotifier.Notify()`)
   bleibt erhalten.
5. **Providerweites Rate-Limit-Gate (Fail-fast):** Bei `RateLimit`-Fehler mit
   `RetryAfterUtc` weitere Calls desselben (Konto, Host[, Budget-Typ]) bis zum
   Reset-Zeitpunkt sofort mit `ScmProviderException(RateLimit, …, retryAfter)`
   ablehnen — ohne HTTP-Request. Anzusiedeln in `SendEventApiAsync`/
   `GitHubHttpApiClient.SendAsync`, damit Auflösung, Reads, Enumeration,
   Notifications und Antwortpfade gleichermaßen abgedeckt sind.
6. **Nebenbefund `IsInaccessible`:** Prüfen, ob ein `RateLimit`-Checkpoint bei
   der Auflösung weiterhin den kompletten Provider-Lauf (`complete = false`)
   blockieren soll — mit Gate würde der Zyklus ohnehin fail-fast enden; ob die
   Isolationslogik zusätzlich `RateLimit` behandeln soll, ist zu entscheiden.

Relevante Erweiterungspunkte: `IScmEventSource.GetEventRepositoryAsync` (einzige
Auflösungsschnittstelle), `IGitHubEventApiClient.SendAsync` (zentraler
Transport-Engpass), `ScmObservationCheckpoints` (bestehendes Backoff-Modell,
`RetryAt` nutzt bereits `RetryAfterUtc`).

## Konfiguration

- Vorschlag: `ScmEventOptions` um eine Auflösungs-Cache-TTL erweitern (Section
  `ScmEvents` in `appsettings`, analog `PollInterval`/`AcceptanceTimeout`).
  Sinnvoller Default-Rahmen: Minuten bis wenige Stunden; endgültiger Wert im
  Umsetzungsplan festzulegen. Eine UI-/Benutzereinstellung ist nicht
  vorgesehen (Annahme: Cache-TTL ist technische Option, kein Fachschalter).
- `PollInterval` bleibt unverändert konfigurierbar; keine neue Laufzeit-
  Einstellung für das Rate-Limit-Gate (verhält sich deterministisch nach
  `X-RateLimit-Reset`).

## Offene Fragen

1. **Cache-Ebene:** Plugin-intern in `GitHubPlugin` (deckt Orchestrator-,
   Outbox- und alle Monitoring-Call-Sites ohne Interface-Änderung ab) oder
   hostseitiger geteilter Auflösungscache-Service? Plugin-intern wird als
   Ansatzpunkt vorgeschlagen — bitte bestätigen.
2. **TTL und Invalidierung:** Welche Auflösungs-TTL ist fachlich vertretbar?
   Der Cache verzögert die Erkennung von Berechtigungsentzug (`CanWrite`),
   Repository-Umbenennung/-Ersatz (`RepositoryId`-Wechsel) und
   Konto-/Token-Wechsel um bis zu TTL. Reicht TTL allein, oder braucht es
   explizite Invalidierung (z. B. bei `Permission`-/`Permanent`-Fehlern,
   „Erneut prüfen"-Aktionen, Token-Änderung in den Einstellungen)?
3. **GraphQL für `GetEventRepositoryAsync`:** Ist `viewerPermission` auf den
   unterstützten GitHub-Enterprise-Versionen verfügbar und semantisch
   äquivalent zu den REST-`permissions` (`push||admin||maintain`)? REST-Fallback
   vorsehen oder GraphQL-only?
4. **Gate-Granularität:** Gilt die Fail-fast-Sperre nur für REST oder
   budgettyp-spezifisch (primäres REST-Limit, sekundäre/abuse Limits, GraphQL-
   Kontingent getrennt)? Eine REST-Erschöpfung impliziert keine GraphQL-
   Erschöpfung — sollen GraphQL-Calls während einer REST-Sperre weiterlaufen?
5. **`receipt-scanner`/Isolationslogik:** Soll ein `RateLimit`-Fehler bei der
   Alias-Auflösung weiterhin den gesamten Provider-Lauf blockieren
   (`complete = false`) oder — analog `Permanent`/`Permission` im Inbox-Modus —
   isoliert werden bzw. den Zyklus kontrolliert beenden?
6. **Serilog-Limit:** Gehört die Erhöhung/Umstellung von
   `fileSizeLimitBytes` (1 GiB-Tageslog) in dieses Issue oder in ein
   separates Folge-Issue?
7. **Sekundäre Limits:** Soll das Gate auch `Retry-After` ohne
   `X-RateLimit-Remaining: 0` (abuse/secondary rate limit, bereits als
   `RateLimit` klassifiziert) auslösen — gleiche Behandlung oder kürzere
   lokale Sperre?
