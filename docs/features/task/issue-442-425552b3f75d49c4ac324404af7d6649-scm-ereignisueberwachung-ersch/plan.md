# Umsetzungsplan: SCM-Ereignisüberwachung erschöpft GitHub-REST-Rate-Limit durch zyklische Repository-Neuauflösung

## Übersicht

Die zyklische SCM-Repository-Auflösung wird restrukturiert, damit sie das primäre GitHub-REST-Rate-Limit nicht mehr erschöpft: `GitHubPlugin.GetEventRepositoryAsync` löst Aliase über eine einzige GraphQL-Abfrage (Viewer-Identität + Repository-Metadaten + `viewerPermission` in einem Call auf dem freien GraphQL-Kontingent) auf, cached erfolgreiche `ScmEventRepository`-Ergebnisse TTL-basiert pro kanonischem Schlüssel und Konto, und sperrt nach einem Rate-Limit-403 den betroffenen (Konto, Host, Budget-Typ)-Transport fail-fast bis `X-RateLimit-Reset`. Hostseitig gruppieren die drei Monitoring-Dienste Aliase über den kanonischen Schlüssel (neue optionale Vertragsschnittstelle `IScmRepositoryAliasNormalizer`), wodurch Alias-Mischformen und Checkpoint-Keys zusammenlaufen. Betroffen: `plugins/Softwareschmiede.Plugin.GitHub/`, `src/Softwareschmiede.Plugin.Contracts/`, `src/Softwareschmiede/Application/Services/` und `src/Softwareschmiede.App/App.xaml.cs`.

## Scope-Klärung

|| Feld | Festlegung |
||------|------------|
|| Verarbeitungseinheit | Auflösung eines lokalen Repository-Alias in die kanonische Provider-Identität (`ScmEventRepository`: Node-ID, Owner, Name, Viewer-Identität, `CanWrite`, `NumericRepositoryId`) je (Plugin-Prefix, Host, Konto). Beobachtete Repository-/Referenzmenge bleibt unverändert — geändert wird nur Häufigkeit und Transport der Auflösung. |
|| Auslösende Ereignisse | Poll-Zyklen von `ScmEventMonitoringService`, `ScmIssuePreparationMonitoringService`, `ScmBackmergeMonitoringService`, `ScmTaskCompletionMonitoringService` (`PeriodicBackgroundService` im `ScmEventOptions.PollInterval`); jede Zustellung aus `ScmEventOrchestrator` (`:193`, `:654`); jede Outbox-Ausführung via `GitHubPlugin.ExecuteResponseAsync` (`:49`). |
|| Erwartete Wirkung | REST-Call-Volumen der Auflösung sinkt von ~76 Calls/min auf ~0 (Auflösung läuft auf dem GraphQL-Kontingent: 1 Call je Repository pro TTL-Fenster). `repository/*`-Checkpoints ohne RateLimit-Backoff, `IssueAufbereitung`-Retries überstehen den ersten Call, freiwerdendes REST-Kontingent steht `gh`-CLI-Aufrufen und den eigentlichen Lese-/Antwort-Calls zur Verfügung. Bei erschöpftem Kontingent scheitern Folgecalls desselben (Konto, Host, Budget-Typ)-Transports sofort mit `ScmProviderException(RateLimit, …, RetryAfterUtc)` — ohne HTTP-Request. |
|| Umgang mit fehlenden Objekten | Cache-Eintrag (Auflösung): **erzeugen** beim ersten erfolgreichen Zugriff, **wiederverwenden** bis TTL-Ablauf, **verwerfen** bei Ablauf, bei `Authentication`-Fehler des Kontos (ganzer Konto-Bestand) und bei `Permission`-Fehler (Konto+Host-Bestand). Fehlgeschlagene Auflösung: **nicht cachen** — `repository/*`-Checkpoint-Backoff (`ScmCheckpointSupport.RecordFailureAsync`, `retryPermanent: true`) bleibt maßgeblich. Rate-Limit-Sperre: **erzeugen** beim ersten `RateLimit`-Fehler mit `RetryAfterUtc`, **verwerfen** nach Reset-Ablauf. Token-/Kontowechsel: Cache-Schlüssel enthält das Konto (Client-Instanz-Trennung `_eventApiClient`/`_agentEventApiClient`); ein toter Token führt über `Authentication`-Fehler zur Cache-Räumung des Kontos, ein gewechselter Token ohne Fehlerfall verfällt spätestens per TTL. |

### Scope-Ausschlüsse

|| # | Einschränkung / Ausschluss | Begründung | Status |
||---|----------------------------|------------|--------|
|| 1 | Kein `PollInterval`-/Scheduler-Eingriff — der 60-s-Poll bleibt Sicherheitsnetz | Der Fehler liegt in der ungecachten Auflösung, nicht im Poll-Rhythmus | bestätigt (2026-11-21) |
|| 2 | Kein Pflicht-Mitglied an `IScmEventSource`; Semantik von `GetEventRepositoryAsync` bleibt | Stattdessen neue **optionale** Vertragsschnittstelle `IScmRepositoryAliasNormalizer` (separates Interface, kein Member-Zwang) für hostseitige Kanonisierung | bestätigt (2026-11-21) |
|| 3 | Keine Änderung an `GH_TOKEN`/git-Token-Trennung und am CLI-seitigen Kontingentverbrauch | Außerhalb des Auflösungspfads | bestätigt (2026-11-21) |
|| 4 | Folgeschäden (`ai:*`-Actions `Failed`, `gh auth status`-Fehlalarm, verpasste Ereignisse) werden nicht separat repariert | Beheben sich durch Wiederherstellung der Überwachung bzw. Backoff-Auslauf | bestätigt (2026-11-21) |
|| 5 | Bitbucket und andere Provider unberührt | Nur `GitHubPlugin` implementiert `IScmEventSource`; hostseitige Kanonisierung läuft über optionales Interface — nicht implementierende Quellen behalten Roh-Alias-Gruppierung | bestätigt (2026-11-21) |
|| 6 | Serilog-`fileSizeLimitBytes` (1-GiB-Tageslog-Stillstand) nicht in diesem Issue | Nebenbefund ohne fachlichen Zusammenhang; empfohlen als separates Folge-Issue | bestätigt (2026-11-21) |
|| 7 | Revalidierungssemantik in Dispatch/Antwort bleibt gleich (`RepositoryId`-/`ViewerId`-Vergleich, `CanWrite`-Prüfung), wird aber bis zur TTL aus dem Cache bedient — Erkennungslatenz bei Berechtigungsentzug/Umbenennung verlängert sich um ≤ TTL | Bewusster Trade-off gegen TTL-Default von 15 min; GitHub-REST folgt Umbenennungen serverseitig, fehlende Schreibrechte scheitern remote weiterhin typisiert | bestätigt (2026-11-21) |
|| 8 | `IsInaccessible` wird nicht um `RateLimit` erweitert — ein Rate-Limit-Fehler bei der Auflösung blockiert weiterhin den Provider-Lauf (`complete = false`) | Konservativ (unaufgelöster Alias könnte jedes Repository meinen); durch das Fail-fast-Gate kostet der blockierte Zyklus keine HTTP-Requests mehr, und falsch als `RateLimit` geführte Checkpoints (Nebenbefund `receipt-scanner`) heilen nach Reset-Ablauf selbst | bestätigt (2026-11-21) |

## Designentscheidungen

|| Komponente / Bereich | Gewählter Ansatz | Begründung |
||----------------------|-----------------|------------|
|| Auflösungstransport | **GraphQL primär** (`query($owner,$name){viewer{login id} repository(owner:$owner,name:$name){id databaseId name owner{login id} viewerPermission}}`), **REST-Fallback** (bisherige zwei Calls) nur bei `Temporary`- und `RateLimit`-GraphQL-Fehlern — nicht bei `Authentication`/`Permission`/`Permanent` | Ein Call ersetzt zwei REST-Calls und verlagert die Auflösung vollständig auf das freie GraphQL-Kontingent; GraphQL `id` ≡ REST `node_id`, `databaseId` ≡ numerische ID, `viewerPermission` (`ADMIN`/`MAINTAIN`/`WRITE`) ≡ `permissions.push‖admin‖maintain`. Der Fallback erhält GHES-Kompatibilität, ohne im Normalbetrieb REST-Kontingent zu verbrauchen. `Authentication`/`Permission` würden im REST identisch scheitern — Fallback wäre reine Verschwendung. |
|| GraphQL-Fehler-Mapping | `GraphAsync` ordnet `errors[]`-Typ `NOT_FOUND` zusätzlich `Permanent` zu | Ein fehlendes/privates Repository liefert `repository: null` + `NOT_FOUND` → direkt `Permanent`, ohne unnötigen REST-Fallback-Call (genau der `receipt-scanner`-Fall). `RATE_LIMITED` aus `errors[]` setzt zudem eine kurze lokale Sperre (kein Server-Reset bekannt, s. Gate). |
|| Auflösungscache | **Plugin-intern** in `GitHubPlugin` (`ConcurrentDictionary`-Feld, kanonischer Schlüssel aus `ParseEventRepository`: `(Host, Owner, Name, Account)` lowercased; `TimeProvider`-basiert) | Deckt alle sechs Call-Sites (3 Monitoring-Schleifen, Completion, Orchestrator ×2, Outbox) ohne Interface-Änderung und ohne Host-Wissen ab. `ScmEventRepository` ist ein unveränderliches Record — direkt cachebar. Alternative hostseitiger Service (`IMemoryCache` nach `DirectoryStructureBrowserService`-Vorbild) verworfen: würde nur Host-Call-Sites abdecken und ließe `ExecuteResponseAsync` außen vor. Fehler werden nie gecacht; nebenläufige Doppelauflösung ist toleriert (idempotente Reads). |
|| Separater Viewer-Cache | **Kein separater Viewer-Cache** | Die GraphQL-Abfrage liefert Viewer + Repository in einem Call; der Viewer wird so oft wie das Repository erneuert (pro TTL-Miss). Ein getrennter Viewer-Cache mit abweichender Lebensdauer würde `ViewerId` und `RepositoryId` zeitlich entkoppeln und die Identitäts-Revalidierung (`ExecuteResponseAsync`) unnötig inkonsistent machen. |
|| Hostseitige Alias-Kanonisierung | Neue **optionale Vertragsschnittstelle** `IScmRepositoryAliasNormalizer` (`NormalizeRepositoryAlias(alias) → kanonische Form https://{host}/{owner}/{name}`); `GitHubPlugin` implementiert sie über `ParseEventRepository`; Host-Helfer `ScmRepositoryAliasKey` fällt bei nicht implementiertem Interface oder Parse-Fehler auf den Roh-Alias zurück | Gruppierungs- und Checkpoint-Schlüssel werden kanonisch: `https://github.com/o/r`, `o/r`, `git@github.com:o/r.git` laufen auf dieselbe Gruppe und denselben Checkpoint. Providerneutral: jede Quelle kann das Interface implementieren, Bitbucket bleibt unverändert auf Roh-Alias. Die kanonische Form ist selbst ein parsebarer Alias — Gruppenschlüssel, Checkpoint-Key und Auflösungseingabe sind identisch. Verwaiste Alt-Checkpoints unter denselben Präfixen werden per Sweep entfernt (kein DB-Migrationsproblem, siehe unten). |
|| Rate-Limit-Gate | **In `GitHubPlugin.SendEventApiAsync`**: Sperrtabelle `(IGitHubEventApiClient-Instanz, Host, BudgetTyp)` → Sperr-bis-Zeitpunkt; vor `client.SendAsync` Gate-Check → `ScmProviderException(RateLimit, …, retryAfterUtc: Sperr-bis)` ohne HTTP; nach `EvaluateResponse`-`RateLimit` mit `RetryAfterUtc` Gate setzen | `SendEventApiAsync` ist der zentrale Engpass für Auflösung, Reads, Enumeration, Notifications und Antwortpfade — alle Call-Sites abgedeckt, Klassifikation (`EvaluateResponse`) wird wiederverwendet statt dupliziert. Client-Instanz = Konto-Dimension implizit. Budget-Typ (`Endpoint == "graphql"`) trennt REST- und GraphQL-Kontingent — eine REST-Erschöpfung darf die GraphQL-Auflösung nicht blockieren (sonst wäre die Lastverlagerung wirkungslos). `UploadAsync`-Pfad bleibt ungated (separater Host, selten, kein Problemverursacher). |
|| Gate ohne Server-Reset | GraphQL-`RATE_LIMITED` aus `errors[]` (kein `RetryAfterUtc` verfügbar) → feste lokale Sperre von 60 s | `RATE_LIMITED`-GraphQL-Fehler tragen keinen Reset-Zeitpunkt; eine kurze feste Sperre (ein Poll-Intervall) verhindert Feuer frei, ohne das Kontingent länger als nötig zu blockieren. |
|| Cache-Invalidierung | TTL (Default **15 min**, konfigurierbar) + **Eviction**: `Authentication`-Fehler → alle Einträge des betroffenen Konto-Clients verwerfen; `Permission`-Fehler → Einträge des (Client, Host) verwerfen | TTL allein würde eine `CanWrite`-Widerrufung bis zu 15 min verzögern; die gezielte Eviction bei Fehlern kostet nur eine Neuauflösung und deckt den realistischen Fall (Berechtigung/Token geändert) zeitnah ab. Token-Wechsel ohne Fehler verfällt per TTL. |
|| Konfiguration der TTL | Neue Optionsklasse `ScmResolutionCacheOptions` in `Softwareschmiede.Plugin.Contracts`, gebunden an Section `ScmEvents`; Plugin erhält sie plus `TimeProvider` als **optionale Konstruktorparameter an allen drei Konstruktoren der Delegationskette** (Defaults: `null` → 15 min, `TimeProvider.System`) — entscheidend: auch am kürzesten (3-Parameter-)Konstruktor | Das Plugin referenziert nur `Softwareschmiede.Plugin.Contracts` und kann `ScmEventOptions` (Host-Assembly) nicht sehen — die Optionsklasse muss in die Contracts. **Verifiziert:** `ActivatorUtilities.CreateInstance` (`PluginManager.cs:289`) wählt nur Konstruktoren, deren nicht-optionale Parameter vollständig aus DI bedient werden; `IGitHubEventApiClient` ist in DI **nicht** registriert, die beiden Client-Parameter des längsten Konstruktors (`GitHubPlugin.cs:148`) tragen **keine** Defaults — der längste und der mittlere Konstruktor können nie gewählt werden, gewählt wird ausschließlich der 3-Parameter-Konstruktor (`GitHubPlugin.cs:132`). Die neuen Parameter müssen daher an diesem Konstruktor hängen und durch die Kette zum längsten Konstruktor weitergereicht werden, der sie in `_resolutionTtl`/`_timeProvider` speichert; `ScmResolutionCacheOptions` wird in `App.xaml.cs` als Singleton-Optionswert registriert, `TimeProvider` ist bereits registriert (`App.xaml.cs:310`) — beide werden so injiziert. Verworfene Alternative: `= null`-Defaults an den Client-Parametern des längsten Konstruktors (macht ihn DI-wählbar, koppelt die Konstruktorwahl aber an die Parameterzahl-Reihenfolge und verwässert die Testseam-Signatur). Tests/Direktinstanziierung (z. B. `ScmE2EPluginManager`, `GitHubEventSourceTests`) nutzen weiterhin den längsten Konstruktor und übergeben Options/`FakeTimeProvider` explizit. |
|| Verwaiste Auflösungs-Checkpoints | **Laufzeit-Sweep** statt Migration: je Dienst und Plugin werden `ScmObservationCheckpoint`-Zeilen unter dem Auflösungs-Präfix gelöscht, deren Key keiner aktuellen kanonischen Gruppe entspricht | Der Key-Schema-Wechsel (Roh-Alias → kanonisch) würde Alt-Zeilen (inkl. Fehlerdiagnosen, die in der Integrationsdiagnose sichtbar bleiben können) verweisen lassen; ein Prune beim Gruppenaufbau entfernt sie deterministisch, ohne EF-Migration oder Datenverlust-Risiko für andere Key-Namespaces. |

## Programmabläufe

### Repository-Auflösung mit Cache und GraphQL-Primärpfad

1. `GetEventRepositoryAsync` prüft `PluginPrefix`, wählt `client = ClientFor(repository.Account)`, kanonisiert den Alias via `ParseEventRepository` → `(host, owner, name)` (bisheriges Verhalten, nun auch Schlüsselbildung).
2. Cache-Lookup im `_resolutionCache` unter `{account}|{host}|{owner}|{name}` (lowercased): Treffer mit `ExpiresUtc > TimeProvider-Now` → gespeichertes `ScmEventRepository` zurückgeben (kein Transport-Call).
3. Miss: GraphQL-Resolution `viewer{login id} repository(owner,name){id databaseId name owner{login id} viewerPermission}` via `GraphAsync(client, host, …, mutation: false)`.
4. Mapping: `repository.id`→`RepositoryId`, `owner.id`→`OwnerId`, `databaseId`→`NumericRepositoryId`, `viewer{login,id}`→`ViewerLogin`/`ViewerId`, `viewerPermission` ∈ {`ADMIN`,`MAINTAIN`,`WRITE`} → `CanWrite`; `repository: null` oder `NOT_FOUND`-Fehler → `Permanent`-Exception. Antwort-Owner/-Name validiert (`ValidateSegment`).
5. Bei `ScmProviderException` mit `FailureKind` ∈ {`Temporary`, `RateLimit`} → REST-Fallback: bisheriger Zweischritt `GET user` + `GET repos/{owner}/{name}` (unverändertes Mapping); bei `Authentication`/`Permission`/`Permanent` → Exception durchreichen.
6. Ergebnis in `_resolutionCache` mit `ExpiresUtc = now + RepositoryResolutionCacheTtl` ablegen und zurückgeben. Fehlschlag (beide Pfade) → nichts speichern.
7. `ExecuteResponseAsync` (`:49`), `ScmEventOrchestrator` (`:193`/`:654`) und `ScmTaskCompletionMonitoringService` (`:205`) rufen weiterhin `GetEventRepositoryAsync` mit kanonischem `https://host/owner/name`-Alias bzw. Roh-`RepositoryUrl` — sie profitieren transparent vom Cache; alle Identitäts-/`CanWrite`-Prüfungen bleiben unverändert.

Beteiligte Klassen/Komponenten: `GitHubPlugin`, `ParseEventRepository`, `GraphAsync`, `EventApiAsync`, `ScmResolutionCacheOptions`, `ScmEventRepository`

### Rate-Limit-Fail-fast-Gate

1. Jeder `SendEventApiAsync`-Aufruf bildet den Gate-Schlüssel `(client, request.Host, request.Endpoint == "graphql")`.
2. Vor `client.SendAsync`: aktiver Gate-Eintrag mit Sperr-bis > Now → `ScmProviderException(ScmProviderFailureKind.RateLimit, "GitHub-Kontingent erschöpft; nächster Versuch nach dem Rate-Limit-Reset.", retryAfterUtc: Sperr-bis)` — kein HTTP-Request.
3. `EvaluateResponse` liefert `RateLimit` mit `RetryAfterUtc` (bereits vorhanden: 429 oder 403 + `X-RateLimit-Remaining: 0`/`Retry-After`) → Gate-Eintrag setzen, Exception durchreichen.
4. `GraphAsync` wirft `RateLimit` aus `errors[]` (`RATE_LIMITED`) → Gate-Eintrag für `(client, host, graphQl: true)` mit `now + 60 s` setzen (kein Server-Reset bekannt), Exception durchreichen.
5. Nach Ablauf des Sperr-Zeitpunkts verfällt der Eintrag implizit (nächster Call geht durch). `ScmCheckpointSupport.ApplyFailure` übernimmt `RetryAfterUtc` ins Checkpoint-Backoff wie bisher — gesperrte Quellen ruhen bis zum Reset ohne weiteren Request.

Beteiligte Klassen/Komponenten: `GitHubPlugin`, `SendEventApiAsync`, `EvaluateResponse`, `GraphAsync`, `ScmProviderException`, `ScmCheckpointSupport`

### Kanonische Alias-Gruppierung und Checkpoint-Prune (Monitoring-Dienste)

1. Pro Plugin je Durchlauf: jeder beteiligte Roh-Alias (`linked.RepositoryId`, `repository.RepositoryUrl`) läuft durch `ScmRepositoryAliasKey.CanonicalOrRaw(source, alias)`: `source is IScmRepositoryAliasNormalizer` → `NormalizeRepositoryAlias` (kanonische `https://host/owner/name`-Form), sonst/→Fehler → Roh-Alias.
2. Gruppierung über den kanonischen Schlüssel (case-insensitiv); der Gruppenwert sammelt **alle** beteiligten lokalen `GitRepository`-Einträge und verknüpften Referenzen — bei `ScmEventMonitoringService` ändert sich der Gruppenwert von `(Repository, Linked)` zu einer Sammlung, damit zusammengelegte Alias-Formen alle lokalen Kandidaten erhalten.
3. Checkpoint-Key `{prefix}/{pluginPrefix}/{escape(kanonischerSchlüssel.ToLowerInvariant())}` statt `{prefix}/{pluginPrefix}/{escape(rohAlias.ToLowerInvariant())}` — gleicher Aufbau, neuer Schlüsselinhalt.
4. Vor der Auflösungsschleife: `ScmCheckpointSupport.PruneResolutionCheckpointsAsync` löscht unter dem Präfix (`repository/`, `issueaufbereitung-repository/`, `backmerge-repository/` je Dienst) alle Checkpoint-Zeilen, deren Key keiner aktuellen Gruppe entspricht (verwaiste Alt-Alias-Checkpoints, entfernte Repositories); bei Löschungen `ScmEventChangeNotifier.Notify()`.
5. Auflösung via `source.GetEventRepositoryAsync(new ScmRepositoryContext(repository.Id, plugin.PluginPrefix, kanonischerSchlüssel, accountMode))` — der kanonische Schlüssel ist ein parsebarer HTTPS-Alias; bei Roh-Fallback der Roh-Alias (Plugin wirft bei ungültiger Form `Permanent`, unter Roh-Key checkpointet — Verhalten wie bisher).
6. Erfolgs-/Fehlerpfad unverändert: Checkpoint-Entfernung + `Notify()`, bzw. `RecordFailureAsync` mit `retryPermanent: true`, Isolations-/Vollständigkeitslogik (`complete`, `IsInaccessible`) unverändert.

Beteiligte Klassen/Komponenten: `ScmEventMonitoringService`, `ScmIssuePreparationMonitoringService`, `ScmBackmergeMonitoringService`, `ScmRepositoryAliasKey`, `ScmCheckpointSupport`, `IScmRepositoryAliasNormalizer`, `GitHubPlugin.ParseEventRepository`

### Cache-Eviction bei Fehlern

1. In `SendEventApiAsync`/`GraphAsync` bzw. am Fehlerpfad der Auflösung: `ScmProviderException` mit `FailureKind.Authentication` → alle `_resolutionCache`-Einträge des betroffenen Clients (Konto) entfernen (Token abgelaufen/gewechselt → `ViewerId` potenziell falsch).
2. `FailureKind.Permission` → Einträge des (Client, Host) entfernen (Berechtigungslage könnte sich geändert haben; nächste Auflösung lädt frische `CanWrite`).
3. `RateLimit`/`Temporary`/`Permanent` → keine Eviction (Backoff bzw. Einzelfall, Cache bleibt valide).

Beteiligte Klassen/Komponenten: `GitHubPlugin` (`_resolutionCache`, `SendEventApiAsync`, `GraphAsync`, `GetEventRepositoryAsync`)

## Neue Klassen

|| Klasse | Typ | Zweck |
||--------|-----|-------|
|| `ScmResolutionCacheOptions` (`Softwareschmiede.Plugin.Contracts/Domain/Options/`) | Optionsklasse | `RepositoryResolutionCacheTtl` (`TimeSpan`, Default 15 min); gebunden an `ScmEvents`-Section, im Plugin injizierbar |
|| `IScmRepositoryAliasNormalizer` (`Softwareschmiede.Plugin.Contracts/Domain/Interfaces/`) | Interface (optional) | `string NormalizeRepositoryAlias(string alias)` → kanonische `https://{host}/{owner}/{name}`-Form; darf `ScmProviderException(Permanent)` bei unparsebarem Alias werfen |
|| `ScmRepositoryAliasKey` (`src/Softwareschmiede/Application/Services/`) | interne statische Klasse | `CanonicalOrRaw(IScmEventSource, string alias)` → normalisierter Schlüssel oder Roh-Alias-Fallback |

Keine neuen Datenmodellklassen, keine Migrationen — Cache und Gate sind prozessinterne Zustände.

## Änderungen an bestehenden Klassen

### `GitHubPlugin` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` + `GitHubPlugin.Events.cs`)

- **Implementiert zusätzlich:** `IScmRepositoryAliasNormalizer` — `NormalizeRepositoryAlias` delegiert auf `ParseEventRepository` und gibt `https://{host}/{owner}/{name}` zurück.
- **Neue Felder:** `_resolutionCache` (`ConcurrentDictionary<string,(ScmEventRepository Value, DateTimeOffset ExpiresUtc)>`), `_rateLimitGates` (`ConcurrentDictionary<(IGitHubEventApiClient Client, string Host, bool GraphQl), DateTimeOffset>`), `_timeProvider`, `_resolutionTtl`.
- **Geänderte Konstruktoren (gesamte Delegationskette):** Die optionalen Parameter `ScmResolutionCacheOptions? resolutionOptions = null` und `TimeProvider? timeProvider = null` werden am **Ende aller drei Konstruktoren** ergänzt und entlang der Kette weitergereicht — gespeichert werden sie nur im längsten Konstruktor (`GitHubPlugin.cs:148`): der 3-Parameter-Konstruktor (`:132`) erhält sie als 4./5. Parameter und delegiert weiter, der 4-Parameter-Konstruktor (`:138`) als 5./6. Parameter. **Grund:** `ActivatorUtilities.CreateInstance` (`PluginManager.cs:289`) wählt nur Konstruktoren, deren nicht-optionale Parameter alle aus DI auflösbar sind; `IGitHubEventApiClient` ist nicht registriert und die Client-Parameter tragen keine Defaults — produktiv wird ausschließlich der (dann 5-parameterige) kürzeste Konstruktor gewählt, der die in DI registrierte `ScmResolutionCacheOptions` und `TimeProvider` injiziert bekommt. Die Client-Testseams (`eventApiClient`/`agentEventApiClient`) bleiben absichtlich nicht-optional: ihre Defaults würden den längsten Konstruktor DI-wählbar machen und die Konstruktorwahl an die Parameterzahl-Reihenfolge koppeln. Rückwärtskompatibel für alle Direktinstanziierungen.
- **Geänderte Methode `GetEventRepositoryAsync`:** Cache-Lookup vor Transport; GraphQL-Primärabfrage statt `GET user`/`GET repos/…`; REST-Fallback bei `Temporary`/`RateLimit`; Mapping `viewerPermission`→`CanWrite`, `id`→`RepositoryId`, `databaseId`→`NumericRepositoryId`; `Permanent`-Klassifikation bei `repository: null`/`NOT_FOUND`; erfolgreiche Ergebnisse mit TTL-Eintrag speichern.
- **Geänderte Methode `SendEventApiAsync`:** Gate-Check vor `client.SendAsync` (Fail-fast `RateLimit` mit `RetryAfterUtc`, kein HTTP); Gate-Eintrag bei `EvaluateResponse`-`RateLimit`+`RetryAfterUtc`; Cache-Eviction bei `Authentication` (Konto) bzw. `Permission` (Client+Host).
- **Geänderte Methode `GraphAsync`:** `errors[]`-Typ `NOT_FOUND` → `Permanent` (zusätzlich zu `RATE_LIMITED`/`FORBIDDEN`/`UNAUTHORIZED`); `RATE_LIMITED` setzt 60-s-Gate-Eintrag für `(client, host, graphQl: true)`.
- **`ExecuteResponseAsync` (`GitHubPlugin.EventResponses.cs`):** kein Code-Eingriff nötig — profitiert transparent vom Cache in `GetEventRepositoryAsync`; `RepositoryId`/`ViewerId`-Vergleich und `CanWrite`-Prüfung bleiben semantisch identisch.

### `ScmEventMonitoringService` (`src/Softwareschmiede/Application/Services/`)

- **Geänderte Methode `RunOnceAsync`:** Alias-Gruppierung über `ScmRepositoryAliasKey.CanonicalOrRaw` statt Roh-`RepositoryId`/`RepositoryUrl`; Gruppenwert sammelt alle beteiligten `GitRepository`/Linked-Einträge; Checkpoint-Key aus kanonischem Schlüssel; Auflösung übergibt kanonischen Schlüssel als Alias; `PruneResolutionCheckpointsAsync`-Aufruf für Präfix `repository/`.
- **`IsInaccessible`:** unverändert (`Permanent`/`Permission` only).

### `ScmIssuePreparationMonitoringService` / `ScmBackmergeMonitoringService` (`src/Softwareschmiede/Application/Services/`)

- **Geänderte Methode `RunOnceAsync`:** gleiche Umstellung (kanonische Gruppierung, Checkpoint-Key, kanonische Auflösungseingabe, Prune für `issueaufbereitung-repository/` bzw. `backmerge-repository/`).

### `ScmCheckpointSupport` (`src/Softwareschmiede/Application/Services/`)

- **Neue Methode `PruneResolutionCheckpointsAsync`:** löscht `ScmObservationCheckpoint`-Zeilen unter einem Auflösungs-Präfix, deren `SourceKey` nicht in der Keep-Menge kanonischer Gruppenschlüssel liegt; `Notify()` bei Änderung.

### `ScmE2EApiClient` (`src/Softwareschmiede.App/Services/Testing/`)

- **Geänderte Methode `Respond`:** GraphQL-Branch erkennt die Auflösungsabfrage (`query` enthält `viewerPermission`) und liefert `viewer`/`repository`/`viewerPermission` gemäß `scenario.CanWrite` (`ADMIN`/`WRITE` vs. `READ`); neue Szenario-Flag `RateLimitRest` (o. ä.) → 403 mit `X-RateLimit-Remaining: 0`/`X-RateLimit-Reset` für Nicht-GraphQL-Endpunkte (Gate-Abdeckung im E2E).

### `ScmE2EScenario` (`src/Softwareschmiede.App/Services/Testing/ScmE2ETestConfiguration.cs`)

- **Neue Eigenschaft:** `RateLimitRest` (`bool`) — Fixture-Schalter für die REST-Rate-Limit-Simulation.

### `ScmE2EPluginManager` (`src/Softwareschmiede.App/Services/Testing/`)

- **Geänderte `GitHubPlugin`-Instanziierung:** übergibt `ScmResolutionCacheOptions` mit kurzer Fixture-TTL (≈ 3 s), damit Cache-Miss-Erneuerung im E2E-Zeitfenster beobachtbar ist.

### `App` (`src/Softwareschmiede.App/App.xaml.cs`)

- **DI-Registrierung:** `services.Configure<ScmResolutionCacheOptions>(context.Configuration.GetSection("ScmEvents"))` und `services.AddSingleton(sp => sp.GetRequiredService<IOptions<ScmResolutionCacheOptions>>().Value)`, damit `ActivatorUtilities` den Optionswert über den 3-Parameter-Konstruktor ins Plugin injiziert (`TimeProvider` ist bereits unter `App.xaml.cs:310` registriert).

### `Softwareschmiede.IntegrationTests.csproj` (`src/Softwareschmiede.IntegrationTests/`)

- **Neue `ProjectReference`:** `..\..\plugins\Softwareschmiede.Plugin.GitHub\Softwareschmiede.Plugin.GitHub.csproj` — die geplanten Auflösungs-Integrationstests brauchen das echte `GitHubPlugin` und `IGitHubEventApiClient`. Verifiziert: `GitHubPlugin`, `IGitHubEventApiClient` und die Transport-Records (`GitHubEventApiRequest`/`GitHubEventApiResponse`/`GitHubAssetUploadRequest`) sind `public` — kein `InternalsVisibleTo`-Eingriff am Plugin-csproj nötig; das Projekt referenziert das Plugin bisher nicht (`Softwareschmiede.IntegrationTests.csproj:48–53`).
- **Neuer `Compile`-Link:** `..\Softwareschmiede.Tests\Helpers\FakeGitHubEventApi.cs` (Link `Helpers\FakeGitHubEventApi.cs`) — Warteschlangen-Fake wird wie die bestehenden Links auf `ScmDispatchFixture.cs`/`TempDirectoryCleanup.cs` eingebunden; seine `internal`-Member (`Requests`, `Add`, `AddGraph`, `AssertComplete`) werden beim Linken assembly-intern und sind aus den Testmethoden erreichbar. Der Fake benötigt nur `public` Plugin-Typen plus `xunit`-`Assert` — beide vorhanden.
- **Fixture-Aufbau der neuen Tests:** Die Auflösungs-Integrationstests instanziieren das echte `GitHubPlugin` über den längsten Konstruktor (`Mock.Of<ICliRunner>()`, `Mock`-/`NullLogger`, `FakeGitHubEventApi` als User- und Agent-Client, `netrcPath` auf einen fehlenden Pfad, kurze `ScmResolutionCacheOptions`-TTL) und registrieren es als `IGitPlugin`-Quelle in einem Service-Aufbau nach `ScmDispatchFixture`-Vorbild (SQLite, `ScmEventMonitoringService`, `ScmEventStore`). Die Stamm-Fixture ist dafür nicht direkt wiederverwendbar, weil sie `Git` fest als `Mock<IGitPlugin>` aufbaut — und eine Moq-Quelle `IScmRepositoryAliasNormalizer` nicht implementiert (Roh-Alias-Fallback), womit sie kanonische Gruppierung/Prune nicht nachweisen kann.

## Datenbankmigrationen

Keine. `ScmObservationCheckpoint` ist ein generischer Key-Value-Datensatz; der Wechsel auf kanonische Keys erzeugt neue Zeilen, verwaiste Alt-Zeilen entfernt der Laufzeit-Sweep (`PruneResolutionCheckpointsAsync`).

## Validierungsregeln

|| Feld / Objekt | Regel | Fehlerfall |
||---------------|-------|------------|
|| `ScmResolutionCacheOptions.RepositoryResolutionCacheTtl` | Muss > `TimeSpan.Zero` sein; Werte außerhalb `[1 min, 24 h]` werden auf die Grenzen geklemmt | Ungültiger/leerer Konfigwert → Default 15 min, kein Startfehler |
|| GraphQL-Auflösungsantwort | `repository` muss Objekt sein; `viewer` muss `id`+`login` liefern; `viewerPermission` in bekannter Wertemenge | `repository: null`/`NOT_FOUND` → `Permanent` („für das Konto nicht sichtbar"); fehlende/unkannte Felder → `Permanent` bzw. REST-Fallback |
|| Kanonische Segmente | `ValidateSegment` auf servergeliefertem Owner/Name (bestehend) | `Permanent` bei ungültigem Bezeichner |

## Konfigurationsänderungen

|| Eintrag | Typ | Standardwert | Zweck |
||---------|-----|--------------|-------|
|| `ScmEvents:ResolutionCacheTtl` (`ScmResolutionCacheOptions.RepositoryResolutionCacheTtl`) | `TimeSpan` | `00:15:00` | Lebensdauer eines erfolgreich aufgelösten `ScmEventRepository` im Plugin-Cache; keine UI-Einstellung (technische Option) |

Keine appsettings-Datei im Repo vorhanden — die Section `ScmEvents` existiert nur als Optionsbindung (`App.xaml.cs:222`); der Default greift ohne Dateieintrag.

## Seiteneffekte und Risiken

- **GraphQL-`NOT_FOUND` → `Permanent` (alle Pfade):** `GraphAsync` klassifiziert `NOT_FOUND` künftig `Permanent` statt `Temporary` — betrifft auch `ReadEventsAsync`/Enumeration: eine zwischenzeitlich gelöschte Referenz sperrt ihren Quell-Checkpoint dauerhaft (`long.MaxValue`, kein `retryPermanent`) statt zu wiederholen. Fachlich korrekt (gelöscht ist final), aber ein Verhaltenssprung für transiente NOT_FOUND-Randfälle — in den Tests gegen die bestehende Matrix abzusichern.
- **Erkennungslatenz bei Widerruf/Umbenennung:** Dispatch- und Outbox-Revalidierung lesen bis zur TTL aus dem Cache — `CanWrite`-Entzug oder Repository-Ersatz werden ≤ 15 min später erkannt. Remote-Writes scheitern weiterhin typisiert (`Permission`), Outbox-Reconcile fängt Folgefehler ab (Scope-Ausschluss 7).
- **REST-Fallback-Verbrauch:** Bei GraphQL-`Temporary`-Störungen fällt die Auflösung auf 2 REST-Calls/Miss zurück — im REST-Erschöpfungs-Szenario greift dann das Gate (sofortiger `RateLimit` ohne HTTP).
- **`receipt-scanner`-Checkpoint:** Bestehende als `RateLimit` geführte Checkpoints behalten ihr Backoff bis `NextAttemptUtc`; danach klassifiziert die Neuauflösung korrekt (`Permanent` via `NOT_FOUND`) — Selbstheilung ohne Eingriff.
- **`WaitForPollAsync(8)` (E2E-Fixture):** Das feste Mindest-Call-Muster pro Zyklus sinkt um die entfallenden Auflösungs-Calls; falls ein Zyklus < 8 Remote-Aufrufe produziert, ist die Konstante zu senken oder auf Endpunkt-Zählung (`WaitForCallsAsync`) umzustellen — bei der Umsetzung gegen `scm-calls.jsonl` zu verifizieren.
- **Checkpoint-Key-Wechsel:** Assertions auf konkrete `repository/*`-Schlüssel in Integrationstests bleiben unverändert gültig, solange Moq-Quellen `IScmRepositoryAliasNormalizer` nicht implementieren (Roh-Alias-Fallback). Tests mit echtem `GitHubPlugin` sehen kanonische Keys.

## Umsetzungsreihenfolge

1. **Vertragserweiterungen in `Softwareschmiede.Plugin.Contracts`**
   - Voraussetzungen: Keine
   - `ScmResolutionCacheOptions` (Domain/Options) und `IScmRepositoryAliasNormalizer` (Domain/Interfaces) anlegen.

2. **`GitHubPlugin`: Zeit-/Options-Infrastruktur, Cache- und Gate-Felder**
   - Voraussetzungen: Schritt 1
   - Optionale Parameter `resolutionOptions`/`timeProvider` **an allen drei Konstruktoren entlang der Delegationskette** ergänzen (auch am 3-Parameter-Konstruktor — `ActivatorUtilities` wählt produktiv ausschließlich diesen, da `IGitHubEventApiClient` nicht in DI registriert ist und die Client-Parameter keine Defaults tragen); nur der längste Konstruktor speichert sie in `_timeProvider`/`_resolutionTtl`; `_resolutionCache`, `_rateLimitGates` einführen.

3. **`GitHubPlugin.GetEventRepositoryAsync`: Cache + GraphQL-Primärpfad + REST-Fallback**
   - Voraussetzungen: Schritt 2
   - Kanonischen Cache-Schlüssel aus `ParseEventRepository`; GraphQL-Resolution mit Mapping (`viewerPermission`, `databaseId`, `id`); Fallback-Kriterien; `NOT_FOUND`→`Permanent`-Erweiterung in `GraphAsync`; `NormalizeRepositoryAlias`-Implementierung.

4. **`GitHubPlugin.SendEventApiAsync`: Rate-Limit-Gate + Cache-Eviction**
   - Voraussetzungen: Schritt 2 (Gate-Felder); funktional unabhängig von Schritt 3, gemeinsam zu testen
   - Gate-Check/-Setzen, Budget-Typ-Trennung, 60-s-Sperre für `errors[]`-`RATE_LIMITED`, Eviction bei `Authentication`/`Permission`.

5. **Hostseitige Kanonisierung: `ScmRepositoryAliasKey` + `ScmCheckpointSupport.PruneResolutionCheckpointsAsync`**
   - Voraussetzungen: Schritt 1 (`IScmRepositoryAliasNormalizer`)

6. **Drei Monitoring-Dienste umstellen (`ScmEventMonitoringService`, `ScmIssuePreparationMonitoringService`, `ScmBackmergeMonitoringService`)**
   - Voraussetzungen: Schritt 5
   - Kanonische Gruppierung/Checkpoint-Keys, kanonische Auflösungseingabe, Prune-Aufruf, Gruppenwert sammelt alle lokalen Repositories/Links.

7. **DI-/E2E-Verdrahtung**
   - Voraussetzungen: Schritt 1
   - `App.xaml.cs`: `Configure<ScmResolutionCacheOptions>` + Singleton-Optionswert; `ScmE2EPluginManager`: Fixture-TTL übergeben; `ScmE2EApiClient`/`ScmE2EScenario`: Auflösungsabfrage und `RateLimitRest`-Flag.

8. **Integrationstest-Projekt verdrahten**
   - Voraussetzungen: Keine (csproj-Eingriff, unabhängig von Schritten 2–7; für die Kompilierung der Auflösungstests in Schritt 9 zwingend)
   - `Softwareschmiede.IntegrationTests.csproj`: `ProjectReference` auf `Softwareschmiede.Plugin.GitHub` und `Compile`-Link auf `..\Softwareschmiede.Tests\Helpers\FakeGitHubEventApi.cs` ergänzen.

9. **Tests anpassen und neu schreiben** (s. Abschnitt Tests)
   - Voraussetzungen: Schritte 1–8
   - `GitHubEventSourceTests.Identity` auf GraphQL-Fixture umstellen, betroffene Assertions anpassen, neue Cache-/Gate-/Fallback-/Eviction-Tests (inkl. Permission-Eviction und Konto-Gate-Isolation), Integrations-Budget-/Alias-/Prune-Tests (echtes Plugin + Fake), E2E-Methode.

10. **Technische Doku nachziehen**
   - Voraussetzungen: Schritte 3–6
   - `docs/help/plugins/github-ereignisse/ablauf-technisch.md`: Auflösungspfad (GraphQL+Cache+Gate), kanonische Checkpoint-Keys dokumentieren.

## Tests

### Neue Tests

|| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
||--------------------|------------|-------------------------------------|
|| `IdentityGraph` (ersetzt/ergänzt `Identity`) | `GitHubEventSourceTests` (Helper) | Plant die GraphQL-Auflösungsantwort (`viewer`, `repository` mit `id`/`databaseId`/`viewerPermission`) statt `user` + `repos/…` |
|| `Resolution_UsesSingleGraphQLCall` | `GitHubEventSourceTests` | `GetEventRepositoryAsync` erzeugt genau 1 `graphql`-Request mit `viewerPermission` im Body; korrektes Mapping aller Felder |
|| `Resolution_CacheHit_ServesWithoutTransport` | `GitHubEventSourceTests` | Zweite Auflösung desselben Alias ohne weiteren `SendAsync`-Aufruf (`api.Requests` unverändert, Fake-Warteschlange unberührt) |
|| `Resolution_AliasForms_ShareCacheEntry` | `GitHubEventSourceTests` | `https://…​.git`, `git@…`, `ssh://…`, `owner/repo` treffen denselben Cache-Eintrag (nur 1 Remote-Auflösung für alle Formen) |
|| `Resolution_CacheExpires_AfterTtl` | `GitHubEventSourceTests` | `FakeTimeProvider` über TTL geschoben → erneuter Transport-Call |
|| `Resolution_SeparatesAccounts` | `GitHubEventSourceTests` | User- und Agent-Konto getrennte Cache-Einträge (bestehender `SeparatesAccount`-Test bleibt, Zählung 2→1 anpassen) |
|| `Resolution_GraphQLTemporary_FallsBackToRest` | `GitHubEventSourceTests` | GraphQL-`Temporary` (Fehler/`errors[]` ohne Typ) → bisherige `GET user`+`GET repos`-Sequenz liefert identisches `ScmEventRepository` |
|| `Resolution_GraphQLAuth_NoFallback` | `GitHubEventSourceTests` | `Authentication`/`Permission` aus GraphQL → Exception direkt, kein REST-Call danach |
|| `Resolution_NotFound_IsPermanent` | `GitHubEventSourceTests` | `repository: null`/`NOT_FOUND` → `Permanent`, kein REST-Fallback, kein Cache-Eintrag (nächster Aufruf versucht erneut) |
|| `RateLimit_Rest403_GatesSubsequentRestCalls` | `GitHubEventSourceTests` (oder `GitHubRateLimitGateTests`) | 403+`Remaining:0`+`Reset` → Folge-REST-Call wirft `RateLimit` mit `RetryAfterUtc`, ohne `api.Requests` zu wachsen |
|| `RateLimit_GraphQL_ContinuesDuringRestGate` | dto. | GraphQL-Auflösung funktioniert während REST-Sperre (Budget-Typ-Trennung) |
|| `RateLimit_Gate_ExpiresAfterReset` | dto. | `FakeTimeProvider` über Reset → nächster Call geht wieder durch |
|| `RateLimit_GraphQlErrors_ShortLockout` | dto. | `errors[]` `RATE_LIMITED` → 60-s-Sperre für GraphQL-Calls, `RetryAfterUtc` in der Exception |
|| `RateLimit_Gate_IsolatesAccountClients` | `GitHubEventSourceTests` | Rate-Limit-Gate des Agent-Clients sperrt ausschließlich Agent-Calls — der User-Client (gleicher Host, gleiche Budget-Dimension) läuft ungehindert weiter; pinnt die Client-Instanz-Dimension des Gate-Schlüssels |
|| `Cache_Authentication_EvictsAccountEntries` | `GitHubEventSourceTests` | 401/`Authentication` → alle Einträge des Kontos verworfen (nächste Auflösung ruft Transport); Einträge des anderen Kontos bleiben |
|| `Cache_Permission_EvictsClientHostEntries` | `GitHubEventSourceTests` | 403/`Permission` (nicht-RateLimit) → Einträge des (Client, Host) verworfen, Einträge anderer Hosts und des anderen Kontos bleiben; nächste Auflösung lädt `CanWrite` frisch |
|| `Monitor_ResolutionBudget_BoundedAcrossPolls` | `Softwareschmiede.IntegrationTests` (neue Klasse, z. B. `ScmResolutionBudgetTests`) | Echtes `GitHubPlugin` + `FakeGitHubEventApi` + echter `ScmEventMonitoringService` über ≥ 2 `RunOnceAsync`-Läufe: Auflösungs-Request-Zählung ≤ 1 je Repository, Monitoring läuft weiter (Enumerations-Calls weiterhin vorhanden) |
|| `Monitor_AliasForms_MergeToOneCheckpoint` | `Softwareschmiede.IntegrationTests` | Zwei Alias-Formen (`https://…` und `owner/name` bzw. `.git`-Suffix) für dasselbe Remote → ein kanonischer Checkpoint-Key, eine Auflösung |
|| `Monitor_LegacyAliasCheckpoints_ArePruned` | `Softwareschmiede.IntegrationTests` | Vorbefüllter `repository/{prefix}/{rohAlias}`-Checkpoint wird beim ersten kanonischen Lauf entfernt |

### Betroffene bestehende Tests

|| Test / Testklasse | Grund der Anpassung |
||-------------------|---------------------|
|| `GitHubEventSourceTests.Identity` | Liefert heute 2 REST-Calls (`user` + `repos/…`) → auf GraphQL-Auflösungsantwort umstellen |
|| `GetEventRepositoryAsync_SeparatesAccount_UsesAgentToken` | `Assert.Equal(2, agent.Requests.Count)` → 1 GraphQL-Call |
|| `RepositoryContext_UsesStableIdsAndPermissions` | Erwartet ausschließlich `GET`-Requests → GraphQL ist `POST`; Fixture/Assertion anpassen |
|| `GraphErrors_RejectPartialData` (`GitHubEventSourceTests`) | Test der `errors[]`-Typ-Matrix (`GitHubEventSourceTests.cs:317`): die neue `NOT_FOUND`→`Permanent`-Zuordnung ist dort als eigener Matrix-Fall explizit zu pinnen — nicht nur über `Resolution_NotFound_IsPermanent` am Auflösungspfad |
|| `ChangedViewer_*`, `ExistingReaction_*`, `CommentTimeout_*`, `PullRequestResponse_*`, `IssueBodyUpdate_*`, `CommentAttachments_*` (alle `ExecuteResponseAsync`-Tests) | Pro Outbox-Aktion fällt die Auflösung auf 1 GraphQL-Call (bzw. 0 bei Cache-Hit über mehrere Aufrufe) — eingeplante Fake-Schritte anpassen |
|| `ScmE2EApiClient`-Fixture-Nutzung aller `E2E_ScmEvents`-Tests | Auflösung wandert von `GET user`/`GET repos/…` auf GraphQL — Fixture muss die neue Abfrage beantworten; `WaitForPollAsync(8)`-Konstante ggf. senken |

### E2E-Tests (primärer Funktionsnachweis)

Die Anforderung berührt **keinen über Nutzeraktion erreichbaren Ablauf**: Auslöser ist ausschließlich der Hintergrund-Poll der Monitoring-Dienste; es gibt weder neue UI-Elemente noch veränderte Dialoge. Die eigentliche Anforderung — „die Request-Anzahl pro Poll-Zyklus sinkt" — ist ein zeitlich aggregiertes Systemverhalten, das Unit- und Integrationstests nur an isolierten Komponenten (Plugin-Instanz, Service) nachweisen. Der vollständige Wirksamkeitsnachweis erfordert den produktiven Pfad durchgehend: Optionsbindung (`App.xaml.cs`) → `ActivatorUtilities`-Plugin-Instanziierung → `PeriodicBackgroundService`-Takt → reale Plugin-Cache-/Gate-Logik → Transport. Dieser Nachweis ist über die bestehende `ScmE2E`-Infrastruktur möglich, die jeden `IGitHubEventApiClient`-Aufruf in `scm-calls.jsonl` protokolliert — die Messgröße liegt exakt an der zu ändernden Schicht. Daher wird eine einzige konsolidierte E2E-Methode geplant (FlaUI/`OsInterface`, nicht in dieser Sandbox ausführbar, aber CI-/Sitzungs-lauffähig):

|| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
||-----------|----------|------------------------|-------------------------------|-------------------|
|| Pflicht | `ScmEvents_RepositoryResolution_StaysWithinBudgetAndGates_OnRateLimit` — App startet mit Fixture (UserAccount, kurze Cache-TTL ~3 s): (a) über ≥ 5 Poll-Zyklen wächst die Zahl der Auflösungs-Aufrufe (`graphql` mit `viewerPermission`) höchstens im TTL-Raster (~1–2), während die Gesamt-Callzahl weiter steigt (Monitoring läuft); (b) es erscheinen **keine** `Endpoint:"user"`-/`Endpoint:"repos/scm-owner/scm-e2e"`-REST-Auflösungszeilen; (c) nach Umschalten `RateLimitRest=true` wächst die REST-Call-Zahl nicht weiter (Gate vor dem Transport wirft), GraphQL-Auflösungen laufen weiter (Budget-Trennung) | `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs` (+ `ScmE2EApiClient`-/Szenario-Erweiterung) | REST-Auflösungsvolumen ≈ 0; Fail-fast ohne HTTP bei erschöpftem Kontingent; GraphQL unabhängig | Einzige Stufe, die DI-Bindung, Plugin-Discovery, echten Poll-Takt und Transport-Messung gemeinsam nachweist — genau das geforderte „Request-Anzahl pro Poll-Zyklus"-Kriterium der Abnahme |

Bestehende E2E-Anpassungen: `ScmE2EApiClient` muss die GraphQL-Auflösungsabfrage beantworten (Pflicht, sonst brechen alle `E2E_ScmEvents`-Tests); `WaitForPollAsync`-Konstante gegen das neue Call-Muster verifizieren.

## Akzeptanzszenarien

|| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
||---|----------|----------|------------------|----------|-------------------|----------|
|| 1 | Happy Path: Auflösung ohne REST-Verbrauch über Zyklen | Poll-Zyklus von `ScmEventMonitoringService` (und parallel IssueAufbereitung/Backmerge/Completion, Dispatch, Outbox) | Repositories in gemischten Alias-Formen verknüpft (`https://…​.git`, `o/r`); Cache leer; gültiges Token | 1. Zyklus: je Alias-Gruppe kanonischer Schlüssel → Cache-Miss → **1 GraphQL-Call** `viewer`+`repository` → `ScmEventRepository` gecacht. 2.–n. Zyklus, Dispatch- und Outbox-Aufrufe: Cache-Hit, **kein** Transport-Call. Nach TTL: genau 1 erneute GraphQL-Auflösung je Repository | `repository/*`-Checkpoints fehlerfrei, Ereignisse werden eingelesen; REST-`user`/`repos`-Calls je Zyklus = 0, GraphQL-Auflösungen ≤ 1 je (Host, Konto, Repo, TTL) | Integrations-Budgettest (Request-Zählung über ≥ 2 Läufe) + E2E-Messung `scm-calls.jsonl` |
|| 2 | REST-Kontingent erschöpft: Fail-fast + GraphQL läuft weiter | GitHub antwortet 403 + `X-RateLimit-Remaining: 0` + `X-RateLimit-Reset` auf einen REST-Call | Laufende Überwachung; gemischte REST-/GraphQL-Last | Gate setzt `(client, host, REST)` bis Reset; alle folgenden REST-Aufrufe (Reads, Kommentarlisten, Mutations) werfen sofort `ScmProviderException(RateLimit, RetryAfterUtc)` — 0 HTTP-Requests; GraphQL-Auflösung/-Reads laufen ungestört; Checkpoints ruhen per `RetryAt` bis Reset | Keine weiteren REST-Requests in `scm-calls.jsonl` während der Sperre; `repository/*`-Checkpoints zeigen `RateLimit`+Backoff statt eskalierender Fehlerzahlen; nach Reset normaler Lauf | Unit-/Gate-Tests + E2E-Aspekt (c) |
|| 3 | Auflösung schlägt fehl (Repository unsichtbar) | Poll-Zyklus; Repository gelöscht/ohne Zugriff | Kein Cache-Eintrag; Alias vorhanden | GraphQL liefert `repository: null`/`NOT_FOUND` → `Permanent`; **kein** REST-Fallback, **kein** Cache-Eintrag; `RecordFailureAsync` mit `retryPermanent` → Backoff | Checkpoint `repository/…` (kanonischer Key) mit `ErrorCode=Permanent`, `NextAttemptUtc` per Backoff; Isolationslogik unverändert (`complete=false` bzw. Inbox-Isolierung); nächster Versuch frühestens nach Backoff | Unit-Test `Resolution_NotFound_IsPermanent` + bestehender Backoff-Test `…IsRetriedAfterBackoff…` |
|| 4 | Alias-Mischformen konvergieren | Poll-Zyklus | Zwei `GitRepository`-/Link-Einträge als `https://github.com/o/r` und `o/r` | Beide normalisieren auf `https://github.com/o/r` → eine Gruppe, ein Checkpoint-Key, eine Auflösung (Cache-Hit des zweiten Zugriffs ohnehin); `localRepositories` sammelt beide lokalen Einträge | Genau 1 Remote-Auflösung und 1 Auflösungs-Checkpoint je kanonischem Repository; Alt-Checkpoints unter Roh-Alias-Keys werden gelöscht | Integrationstest `…MergeToOneCheckpoint` + `…ArePruned` |
|| 5 | Berechtigungs-/Token-Änderung zur Laufzeit | Remote: `CanWrite` entzogen oder Token gewechselt/ungültig | Cache-Eintrag aktiv (TTL läuft) | `Authentication`-Fehler (irgendein Pfad des Kontos) → alle Cache-Einträge des Kontos verworfen; `Permission` → (Client, Host)-Einträge verworfen; nächste Auflösung lädt `CanWrite`/`ViewerId` frisch; ohne Fehlerfall verfällt der Eintrag spätestens per TTL | Keine dauerhaft stale `CanWrite`-/`ViewerId`-Revalidierung; Dispatch-/Outbox-Prüfung arbeitet nach Eviction mit aktualisierter Identität | Unit-Tests `Cache_Authentication_EvictsAccountEntries` + `Cache_Permission_EvictsClientHostEntries` |

## Offene Punkte

Keine.
