# Tasks: SCM-Ereignisüberwachung erschöpft GitHub-REST-Rate-Limit durch zyklische Repository-Neuauflösung

|| # | Bereich | Aufgabe | Status | Testnachweis |
||---|---------|---------|--------|--------------|
|| 1 | Verträge | `ScmResolutionCacheOptions` in `Softwareschmiede.Plugin.Contracts/Domain/Options/` anlegen (`RepositoryResolutionCacheTtl`, Default 15 min) | Offen | — |
|| 2 | Verträge | `IScmRepositoryAliasNormalizer` in `Softwareschmiede.Plugin.Contracts/Domain/Interfaces/` anlegen (`NormalizeRepositoryAlias` → kanonische `https://{host}/{owner}/{name}`-Form) | Offen | — |
|| 3 | Plugin-Logik | `GitHubPlugin`: optionale Parameter `ScmResolutionCacheOptions?`/`TimeProvider?` an **allen drei Konstruktoren** der Delegationskette ergänzen (auch am 3-Parameter-Konstruktor — `ActivatorUtilities` wählt produktiv ausschließlich diesen); nur der längste Konstruktor speichert sie; Felder `_resolutionCache`, `_rateLimitGates`, `_timeProvider`, `_resolutionTtl` einführen | Offen | — |
|| 4 | Plugin-Logik | `GitHubPlugin.GetEventRepositoryAsync`: kanonischen Cache-Schlüssel aus `ParseEventRepository` bilden, TTL-Cache-Lookup vor Transport einbauen | Offen | — |
|| 5 | Plugin-Logik | `GetEventRepositoryAsync`: GraphQL-Auflösungsabfrage (`viewer{login id}` + `repository{…viewerPermission}`) mit Mapping (`id`/`databaseId`/`viewerPermission`/`owner.id`) implementieren | Offen | — |
|| 6 | Plugin-Logik | `GetEventRepositoryAsync`: REST-Fallback (bisherige `GET user`/`GET repos`-Sequenz) nur bei `Temporary`/`RateLimit`; `Authentication`/`Permission`/`Permanent` ohne Fallback durchreichen | Offen | — |
|| 7 | Plugin-Logik | `GraphAsync`: `errors[]`-Typ `NOT_FOUND` → `Permanent` mappen; `RATE_LIMITED` → 60-s-Gate-Eintrag für `(client, host, graphQl)` | Offen | — |
|| 8 | Plugin-Logik | `GitHubPlugin.SendEventApiAsync`: Rate-Limit-Gate — Gate-Check vor `client.SendAsync` (Fail-fast `RateLimit` mit `RetryAfterUtc`, kein HTTP), Gate-Setzen nach `EvaluateResponse`-`RateLimit`; Budget-Typ-Trennung REST/GraphQL | Offen | — |
|| 9 | Plugin-Logik | Cache-Eviction: `Authentication`-Fehler → Konto-Einträge verwerfen; `Permission`-Fehler → (Client, Host)-Einträge verwerfen | Offen | — |
|| 10 | Plugin-Logik | `GitHubPlugin` implementiert `IScmRepositoryAliasNormalizer` über `ParseEventRepository` | Offen | — |
|| 11 | Host-Logik | `ScmRepositoryAliasKey` (intern, statisch) in `Application/Services/` anlegen: `CanonicalOrRaw` mit Normalizer-Interface und Roh-Alias-Fallback | Offen | — |
|| 12 | Host-Logik | `ScmCheckpointSupport.PruneResolutionCheckpointsAsync` hinzufügen (Löschen verwaister Auflösungs-Checkpoints außerhalb der Keep-Menge + `Notify()`) | Offen | — |
|| 13 | Host-Logik | `ScmEventMonitoringService.RunOnceAsync`: kanonische Alias-Gruppierung (Gruppenwert sammelt alle `GitRepository`/Linked-Einträge), kanonischer Checkpoint-Key, kanonische Auflösungseingabe, Prune für `repository/`-Präfix | Offen | — |
|| 14 | Host-Logik | `ScmIssuePreparationMonitoringService.RunOnceAsync`: gleiche Umstellung für `issueaufbereitung-repository/`-Präfix | Offen | — |
|| 15 | Host-Logik | `ScmBackmergeMonitoringService.RunOnceAsync`: gleiche Umstellung für `backmerge-repository/`-Präfix | Offen | — |
|| 16 | Konfiguration | `App.xaml.cs`: `services.Configure<ScmResolutionCacheOptions>(…"ScmEvents")` + Singleton-Optionswert für `ActivatorUtilities` registrieren | Offen | — |
|| 17 | E2E-Infrastruktur | `ScmE2EApiClient`: GraphQL-Branch um Auflösungsabfrage (`viewerPermission`) erweitern; `ScmE2EScenario.RateLimitRest`-Flag für 403+`X-RateLimit-Reset` auf Nicht-GraphQL-Endpunkten | Offen | — |
|| 18 | E2E-Infrastruktur | `ScmE2EPluginManager`: `GitHubPlugin` mit kurzer Fixture-TTL (~3 s `ScmResolutionCacheOptions`) instanziieren | Offen | — |
|| 19 | Tests | `Softwareschmiede.IntegrationTests.csproj`: `ProjectReference` auf `Softwareschmiede.Plugin.GitHub` und `Compile`-Link auf `..\Softwareschmiede.Tests\Helpers\FakeGitHubEventApi.cs` ergänzen (Voraussetzung für Aufgaben 24–25) | Offen | — |
|| 20 | Tests | `GitHubEventSourceTests.Identity` → `IdentityGraph` auf GraphQL-Auflösungsantwort umstellen; betroffene Fake-Schritt-Fixtures aller `ExecuteResponseAsync`-/Auflösungstests anpassen (`SeparatesAccount` 2→1 Call, `RepositoryContext_…` POST statt GET) | Offen | — |
|| 21 | Tests | `GraphErrors_RejectPartialData` (`GitHubEventSourceTests`): `errors[]`-Typ-Matrix um `NOT_FOUND`→`Permanent`-Fall explizit pinnen | Offen | — |
|| 22 | Tests | Neue Cache-Tests in `GitHubEventSourceTests`: Cache-Hit ohne Transport, Alias-Formen-Sharing, TTL-Ablauf (`FakeTimeProvider`), Konto-Trennung, `NotFound`→`Permanent` ohne Cache/Fallback, GraphQL-`Temporary`→REST-Fallback, `Authentication`/`Permission` ohne Fallback, `Cache_Authentication_EvictsAccountEntries` und `Cache_Permission_EvictsClientHostEntries` (beide Eviction-Richtungen separat) | Offen | — |
|| 23 | Tests | Neue Gate-Tests: REST-403+`Remaining:0`+`Reset` sperrt Folge-REST ohne HTTP, GraphQL läuft während REST-Sperre, Gate-Ablauf nach Reset, `RATE_LIMITED`-`errors[]` → 60-s-Sperre, `RetryAfterUtc` in Gate-Exception, `RateLimit_Gate_IsolatesAccountClients` (Agent-Gate sperrt User-Calls nicht) | Offen | — |
|| 24 | Integrationstests | `ScmResolutionBudgetTests` (neu, `Softwareschmiede.IntegrationTests`): echtes `GitHubPlugin` + `FakeGitHubEventApi` + echter `ScmEventMonitoringService` über ≥ 2 Zyklen — Auflösungs-Requests ≤ 1/Repo, Monitoring läuft weiter | Offen | — |
|| 25 | Integrationstests | Tests für Alias-Merging (`Monitor_AliasForms_MergeToOneCheckpoint`) und Checkpoint-Prune (`Monitor_LegacyAliasCheckpoints_ArePruned`) | Offen | — |
|| 26 | E2E-Test | Konsolidierte Methode `ScmEvents_RepositoryResolution_StaysWithinBudgetAndGates_OnRateLimit` in `E2E_ScmEvents.cs`: Auflösungs-Call-Zählung über `scm-calls.jsonl` pro Poll-Fenster (≈TTL-Raster, keine `user`/`repos`-REST-Zeilen), danach `RateLimitRest`-Flip → REST-Calls stoppen, GraphQL läuft | Offen | — |
|| 27 | E2E-Test | `WaitForPollAsync`-Konstante gegen neues Call-Muster pro Zyklus verifizieren/ggf. anpassen | Offen | — |
|| 28 | Doku | `docs/help/plugins/github-ereignisse/ablauf-technisch.md`: Auflösungspfad (GraphQL+Cache+Gate) und kanonische Checkpoint-Keys nachziehen | Offen | — |
