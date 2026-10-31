# Umsetzungsplan: GitHub-Notifications als Eingangskanal für SCM-Ereignisse (Kontomodus) + prozessloser HTTP-Transport

## Übersicht

Zwei zusammenhängende Änderungen: (1) Der `ScmEventMonitoringService` erhält einen umschaltbaren Kontomodus (`scm.monitoring.accountmode`, Standard `Benutzerkonto`). Im Modus `SeparatesKonto` wird statt der repository-weiten Enumeration ausschließlich die GitHub-Notifications-Inbox eines eigenen Bot-Kontos (zweites PAT in den GitHub-Plugin-Einstellungen) überwacht; aus `reason = mention`-Einträgen werden `ScmEventReference`s abgeleitet und in den bestehenden Read-/Accept-/Dispatch-Pfad eingespeist. (2) Der GitHub-API-Transport wird von `gh`-Kindprozessen (gemessen ~150 Prozesse/Minute, pro Prozess ein `conhost.exe`-Aufblitzen) auf direkte HTTP-Aufrufe per `HttpClient` umgestellt: `GhEventApiClient` wird durch `GitHubHttpApiClient` ersetzt, der `IGitHubEventApiClient`-Vertrag bleibt unverändert, beide Konto-Clients teilen die Implementierung und unterscheiden sich nur im Token-Selektor. Alle periodisch genutzten sowie alle lesenden GitHub-API-Pfade des `GitHubPlugin` werden prozesslos; verbleiben auf `gh` nur drei ereignisgebundene Mutations-Komfortbefehle (`issue create`, `pr create`, `pr merge`/`pr review`). Betroffen sind `plugins/Softwareschmiede.Plugin.GitHub` (neuer Client, konvertierte Methoden), `Softwareschmiede.Plugin.Contracts` (neuer Enum, `IScmNotificationSource`, `Konto`-Selektor), `ScmEventMonitoringService`/`ScmEventValidationService`/`ScmEventOrchestrator`/`KiAusfuehrungsService` (modusabhängiges Verhalten) sowie `SettingsViewModel`/`SettingsView.xaml` (Modusauswahl) und `ScmEventQueueViewModel` (Verwerfungsdiagnose ohne Fehlalarm). Der primäre Funktionsnachweis läuft über eine Erweiterung des konsolidierten SCM-E2E `ScmEventsE2ETests.ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` um eine Bot-Modus-Phase; die Transportumstellung selbst hat keinen eigenen Benutzerfluss und wird über Unit-/Integrationstests sowie denselben konsolidierten E2E abgesichert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| HTTP-Ereignistransport | Neue Klasse `GitHubHttpApiClient : IGitHubEventApiClient` ersetzt `GhEventApiClient` vollständig als Produktivimplementierung; `IGitHubEventApiClient`, `GitHubEventApiRequest(Host, Method, Endpoint, JsonBody)` und `GitHubEventApiResponse(StatusCode, Body, Headers)` bleiben unverändert | Die gesamte Plugin-Logik (`SendEventApiAsync`/`EventApiAsync`/`GraphAsync`/`RestPagesAsync`, Fehlerklassifizierung, Pagination via `Link`, Backoff via `X-RateLimit-*`/`Retry-After`) arbeitet unverändert weiter; die Test-Naht (Mocks, `ScmE2EApiClient`) greift identisch. Der `IGitHubEventApiClient`-Vertrag ist transportneutral — `gh api --include` wird exakt durch `HttpClient` ersetzt, inkl. Statuscode/Body/Response-Headern (sogar besser: Fehler-Bodies, die `gh` verschluckte, stehen jetzt zur Verfügung) |
| Token-Bereitstellung im Client | `GitHubHttpApiClient(Func<string?> tokenProvider)`: das Plugin übergibt `() => _credentialStore.GetCredential(GitHubTokenCredentialKey)` bzw. `...GetCredential(GitHubAgentTokenCredentialKey)`; der Selektor wird pro Request ausgewertet | Entspricht der bisherigen `environmentFactory`-Auswertung pro Aufruf (Token-Wechsel wirkt ohne Neustart); beide Konto-Clients teilen eine Implementierung und unterscheiden sich nur im Credential-Key — exakt die geforderte Struktur. Token fließt ausschließlich `ICredentialStore` → `Authorization: Bearer`-Header |
| Base-URL- und Header-Mapping | `Host == "github.com"` → `https://api.github.com/{endpoint}`; Enterprise-Host → `https://{host}/api/v3/{endpoint}`; Sonderfall `Endpoint == "graphql"` → `https://api.github.com/graphql` bzw. `https://{host}/api/graphql`. Pflicht-Header: `Accept: application/vnd.github+json`, `X-GitHub-Api-Version: 2022-11-28`, `User-Agent: Softwareschmiede`, `Authorization: Bearer {token}` nur bei vorhandenem Token; `JsonBody` als `application/json`-Content | `gh api` setzte exakt dieselben Accept-/Versions-Header (`IGitHubEventApiClient.cs` Z. 30–31); `User-Agent` ist von der GitHub-API zwingend gefordert. Das Enterprise-Mapping entspricht `ValidateNextPage` (Z. 297–298), das `api.github.com`/„/api/v3/" bereits als Referenzschema codiert |
| `HttpClient`-Lebensdauer | Statisch geteilter `HttpClient` innerhalb von `GitHubHttpApiClient` (Lazy-Singleton); ein zweiter Konstruktor akzeptiert einen `HttpClient`/`HttpMessageHandler` für Tests. Kein DI-`HttpClient`, kein neuer Plugin-Konstruktor-Parameter | Der DI-registrierte `HttpClient` (`App.xaml.cs` Z. 299–300) wird im Update-E2E durch `UpdateFixtureHttpMessageHandler` ersetzt, der unbekannte Requests **ohne Netzwerkfallback** verwirft — ein DI-bezogener Plugin-Transport würde dort brechen. Ein statischer Client vermeidet Socket-Erschöpfung und hält `ActivatorUtilities`-Plugin-Konstruktion sowie `PluginSymbolTests` (kürzester Ctor) unverändert |
| Umfang der Prozesslosigkeit | **Alle** lesenden und periodisch genutzten Pfade laufen über `IGitHubEventApiClient` → `GitHubHttpApiClient`: kompletter Ereignispfad, `GetPullRequestStatusAsync` (periodisch: `PullRequestMonitoringService` 2-Min-Intervall, `ScmTaskCompletionMonitoringService` Z. 165, `ScmBackmergeService` Z. 104), `GetPullRequestWorkflowRunsAsync`/`RunWorkflowListAsync`, `CheckHealthAsync` sowie die direkten `gh api`-/`gh`-Lesebefehle `GetIssuesAsync`, `GetAlertsAsync`, `GetIssueTemplatesAsync`/`LoadTemplateContentAsync`, `GetOpenPullRequestsAsync`, `GetAvailableRepositoriesAsync`, `GetRepositoryStructureLoadResultAsync` | Die Überwachung erzeugt damit null `gh`-Prozesse; die reinen GET-Pfade sind 1:1-REST-Abbildungen über denselben Transport — ein halb migrierter Zustand mit zwei Auth-/Fehlerwegen wäre schwerer zu warten |
| Verbleibende `gh`-Pfade | `CreateIssueAsync` (`gh issue create`, Z. 465), `CreatePullRequestAsync` (`gh pr create`, Z. 987), `CompletePullRequestAsync` (`gh pr merge`/`gh pr review --approve`, Z. 1145) bleiben auf `gh` | Ausschließlich ereignisgebundene, seltene Mutationen (user-/agenteninitiiert bzw. ≤1× je PR): kein Poll-Volumen, kein Dauer-Flackern. Ihre Flag-Semantik (`--auto`-Auto-Merge, `--admin`-Bypass, `IsNoCommitsBetweenFailure`-stderr-Heuristiken) hat keine triviale REST-Entsprechung — das Migrationsrisiko in Schreibpfaden überwiegt den Nutzen. **Antwort auf die Rückfrage:** `gh` bleibt Produktivvoraussetzung nur für diese drei seltenen Mutationspfade und als optionales Werkzeug in Agenten-Sitzungen; für die Überwachung und alle Lesepfade ist `gh` danach nicht mehr erforderlich |
| `CheckHealthAsync` | `GET /user` über den Benutzer-`IGitHubEventApiClient`; 2xx = gesund, sonst `false` | Direkter Ersatz für `gh auth status` (gefordert); deckt Token-Gültigkeit ab, die `gh auth status` prüfte |
| Fehlervertrag konvertierter Methoden | Konvertierte Methoden rufen `SendEventApiAsync`/`EventApiAsync`/`GraphAsync` mit `Konto = Benutzerkonto` auf und bilden `ScmProviderException`/Nicht-2xx auf ihren bisherigen Vertrag ab (`[]`, `Failed(...)`, `InvalidOperationException`, `false`). `IsNotFound`/`IsAuthenticationFailure` werden für die HTTP-Pfade auf Statuscodes (404, 401/403) + Response-Body umgestellt statt stderr-Text | Die Statusklassifizierung (`Authentication`/`Permission`/`RateLimit`/`Temporary`/`Permanent` inkl. `Retry-After`/`X-RateLimit-Reset`-Auswertung, `SendEventApiAsync` Z. 255–281) wird nicht dupliziert; Aufrufer-Verhalten (`PullRequestMonitoringService` fängt `Exception` → `SetRetryableErrorAsync`; `NotSupportedException` gesondert) bleibt kompatibel |
| `GetPullRequestStatusAsync`-Transport | GraphQL-Query `repository{pullRequest(number:$number){id number title url state headRefName baseRefName headRefOid mergeCommit{oid} mergeStateStatus}}` via `GraphAsync` | `mergeStateStatus` existiert nur in GraphQL (`gh pr view --json` nutzt intern GraphQL); die Query liefert exakt die JSON-Form, die `ParsePullRequestStatus` heute liest — Parser und Aufrufer-Vertrag bleiben unverändert |
| Workflow-Runs-Transport | `GET repos/{id}/actions/runs?head_sha={sha}&per_page=100` (ohne SHA ohne Parameter) über neuen Objekt-Seitenhelfer; `ParseWorkflowRuns` liest snake_case (`id`→`databaseId`, `display_title`, `head_sha`, `head_branch`, `html_url`→url) | `gh run list --commit {sha}` entspricht dem `head_sha`-Filter; Antwort-Wrapper `{total_count, workflow_runs[]}` passt nicht zum Top-Level-Array von `RestPagesAsync` — eigener Helfer mit derselben `Link`-/`ValidateNextPage`-Semantik |
| Token-Grenze Nicht-Ereignis-APIs | Alle konvertierten Nicht-Ereignis-Methoden laufen unter dem Benutzer-Token (`_eventApiClient`). `Konto`-Routing gilt ausschließlich für `IScmEventSource`/`IScmEventResponder`/`IScmNotificationSource`/`GetAgentEnvironmentAsync`; `GetPullRequestStatusAsync`, Workflow-Runs, Alerts, Templates, Trees, Repository-/Issue-/PR-Listen und `CheckHealthAsync` bleiben Benutzer-Token-Pfade — auch im `SeparatesKonto` (dort hat das Benutzer-Token weiterhin `repo`-Scope-Pflicht) | Dokumentiert die bestehende Asymmetrie bewusst: Im `SeparatesKonto` läuft die Issue-Abschlussprüfung (`ScmTaskCompletionMonitoringService` Z. 191) über das AgentToken, die PR-Abschlussprüfung (Z. 165) über das Benutzer-Token — funktional korrekt, da `repositoryId` dort keinen Konto-Kontext trägt |
| Token-Schutz | `GitHubHttpApiClient` loggt nichts (insbesondere niemals Request-/Response-Header); `ScmProviderException`-Messages enthalten nur Status/Endpoint (Muster `SendEventApiAsync` Z. 279); kein Token in URLs (kein `access_token`-Query-Parameter), Logs, Exception-Messages, Test-Artefakten oder Doku; `SanitizeSensitiveOutput` maskiert **beide** Tokens; Tests nur mit Fake-Tokens | Verbindliche Anforderung; die einzige Stelle, an der der Token das Plugin verlässt, ist der `Authorization`-Header — `HttpRequestException`-Messages enthalten maximal die URI, nie Header |
| Verwerfungsdiagnose ohne Fehlalarm | Neues Diagnostic-Präfix (Konstante, z. B. `ScmEventStore.InboxDiscardDiagnosticPrefix` = `"Inbox:"`), das `ScmEventQueueViewModel` (Z. 170–171) zusätzlich zu `UnassignedDiagnostic` aus `sourceErrors` ausfiltert | Jede nicht-leere Checkpoint-`Diagnostic` löst sonst den `MonitoringNotice`-Hinweis „Überwachung unterbrochen" aus (Z. 192–198) — der Routine-Zähler verworfener Fremd-Notifications wäre ein Fehlalarm; `StartsWith` ist EF-Core-SQLite-übersetzbar |
| Konto-Selektor | Neues Enum `ScmKontoModus` in `Softwareschmiede.Plugin.Contracts` (`Benutzerkonto = 0`, `SeparatesKonto = 1`), einmal als App-Einstellung und einmal als Credential-Selektor verwendet | Ein Enum für beide Zwecke vermeidet Abbildungslogik; muss in Contracts liegen, weil es auf Contract-Records und Interface-Signaturen erscheint und das Plugin nur Contracts referenziert (`ScmBackmergeModus` liegt zwar in `Softwareschmiede/Domain/Enums`, ist aber für das Plugin nicht sichtbar) |
| Träger des Kontos | `ScmEventRepository` (`ScmEvents.cs` Z. 14–15, aktuell 9 Positions-Komponenten) erhält eine 10. Positions-Komponente `Konto` (Default `Benutzerkonto`); `ScmRepositoryContext` erhält den optionalen Konstruktorparameter `konto` (Default `Benutzerkonto`) und die Eigenschaft `Konto` | Der Modus propagiert dadurch automatisch über `ScmEventReference` → `ScmEventReadRequest` → `ScmEventObservation` (`ObservationJson`) → `ScmResponseRequest`, ohne dass eine Interface-Signatur von `IScmEventSource`/`IScmEventResponder` geändert werden muss. Alte `ObservationJson`-Blobs deserialisieren mit `Benutzerkonto` (= Altsemantik); die Revalidierung beim Dispatch nutzt automatisch das Konto der Aufnahme. Alle bestehenden Konstruktionen bleiben quellkompatibel |
| Notification-Eingang | Neue optionale Plugin-Fähigkeit `IScmNotificationSource` (Gateway): liefert rohe `ScmNotification`-Einträge pro Host; die Referenz-Ableitung und Repository-Zuordnung bleibt im Host | Die kanonische Repository-Identität kann nur der Host gegen registrierte `GitRepository`-Aliasse auflösen (`GetEventRepositoryAsync`); das Plugin bleibt credentialfreie Transport- und Parsing-Schicht — gleiches Muster wie `IScmEventSource` |
| Bot-Authentifizierung im Plugin | Zweiter Credential-Key `Softwareschmiede.GitHub.AgentToken` + zweiter injizierbarer `IGitHubEventApiClient` (`_agentEventApiClient`, Default `GitHubHttpApiClient` mit `() => _credentialStore.GetCredential(GitHubAgentTokenCredentialKey)`) | Hält `IGitHubEventApiClient` unverändert; beide Clients teilen `GitHubHttpApiClient` und unterscheiden sich nur im Token-Selektor — die geforderte HTTP-Basis für beide Konten. Die bestehende Constructor-Injection-Testnaht (Mocks, `ScmE2EApiClient`) wird um einen optionalen Parameter am längeren Ctor erweitert (der 3-Argument-Ctor bleibt der kürzeste — `PluginSymbolTests`-Vorgabe) |
| Modusabhängige Validierung | `ScmEventValidationService.GetRejection` verzweigt intern auf `observation.Reference.Repository.Konto` — keine Signaturänderung. Trigger je Modus (geklärter Punkt 4): im `Benutzerkonto` bleibt die Prüfung auf das konfigurierte Trigger-Keyword (`scm.trigger.keyword`) exakt wie bisher; im `SeparatesKonto` triggert stattdessen die frei stehende `@mention` des separaten Kontos | Alle vier Aufrufstellen (`ScmEventMonitoringService` Z. 142, `ScmEventStore.AcceptBatchAsync` Z. 59, `ScmEventOrchestrator` Z. 136, indirekt Resolver-Suche) bleiben unverändert; die Validierungssemantik reist mit der persistierten Observation und überlebt Moduswechsel korrekt |
| Checkpoint-Namensraum | `notifications/{pluginPrefix}/{host}` als Watermark-/Backoff-Checkpoint der Inbox, `ScmObservationCheckpoint.SourceKey` unverändert | Freier String-Namensraum ist dafür vorgesehen; Inhalts-Checkpoints und `EventKey`-Dedup bleiben kontounabhängig — dieselbe Referenz ist in beiden Modi dieselbe Quelle |
| Modusumstellung `RunOnceAsync` | Verzweigung pro Poll-Zyklus: Alias-Auflösung läuft in beiden Modi (mit `Konto` im Kontext), im `SeparatesKonto` wird `EnumerateRepositoryAsync` durch Inbox-Polling je aufgelöstem Host ersetzt; der anschließende Read-/Resolve-/Accept-/Dispatch-Block wird unverändert wiederverwendet | Maximale Wiederverwendung des etablierten Pfads; Modus wirkt ohne Neustart, weil die Einstellung wie `GetScmMonitoringEnabledAsync`/`GetScmTriggerKeywordAsync` pro Zyklus gelesen wird |
| `IScmAgentAccess` | `GetAgentEnvironmentAsync` erhält den Parameter `ScmKontoModus konto` **vor** `ct` (`GetAgentEnvironmentAsync(konto, ct)`) | Einzige Contract-Methode ohne Kontextobjekt — das Konto kann nicht über ein Record-Feld ankommen. Die Signaturänderung bricht bewusst die drei bestehenden Aufrufstellen: `KiAusfuehrungsService.WithScmAgentEnvironmentAsync` (Z. 378), `ScmDispatchFixture` (Z. 75), `ScmEventDispatchTests_AgentActions` (Z. 26/52) — alle werden angepasst. `GitHubPlugin` ist einzige Implementierung; die Methode bleibt ein `GH_TOKEN`-/`http.extraheader`-Lieferant für die Agenten-Prozessumgebung (unverändert prozessbasiert, kein API-Pfad) |
| Modusauswahl in der UI | `ScmKontoModusOptionen.cs` (`ScmKontoModusTexte` + `ScmKontoModusOption`-Record) als 1:1-Nachbau von `ScmBackmergeModusOptionen.cs`; ComboBox `AutomationProperties.Name="Konto-Modus"` im Abschnitt „SCM-Ereignisse" der `SettingsView.xaml` | Vorgeschriebenes UI-Muster: Enum-Einstellungen werden als beschriftete Optionsliste in einer ComboBox abgebildet (Vorlage `BackmergeModusOptionen`/`SelectedBackmergeModus`, `SettingsView.xaml` Z. 327–345) |
| AgentToken-Pflichtprüfung | Neue Validierung `ValidiereScmKontoModus` in der `ValidierePflichtfelder`-Kette: bei `SeparatesKonto` muss das `AgentToken`-Feld des GitHub-Plugins nicht leer sein. Geprüft wird der **effektive** Wert — bei gerade ausgewählten GitHub-Plugin-Einstellungen (`_selectedPlugin`) der Formularwert des `AgentToken`-Eintrags, sonst der persistierte Wert via `PluginSettingsService.GetValue` | `ValidierePflichtfelder` läuft vor `SpeicherePluginEinstellungen` (Z. 469 vs. Z. 491); eine reine Store-Prüfung würde „Token eintragen + Modus wählen + einmal speichern" fälschlich ablehnen. Das Plugin-Feld selbst bleibt `IsRequired = false`, da es nur modusabhängig Pflicht ist |
| E2E-Credential-Store | Im SCM-E2E-Modus (`scmTestConfiguration`, `App.xaml.cs` Z. 349–357) wird `ICredentialStore` durch einen prozesslosen In-Memory-`ScmE2ECredentialStore` ersetzt, den `PluginSettingsService` und das `GitHubPlugin` über `ScmE2EPluginManager` gemeinsam nutzen; `EmptyCredentialStore` entfällt | Macht den AgentToken-Pflichtfeld-Fehlerfall deterministisch E2E-testbar (Token initial leer → Validierungsfehler → per UI eingetragen → gültig) und verhindert, dass die Test-App echte `Softwareschmiede.GitHub.*`-Credentials im `WindowsCredentialStore` der Testmaschine liest oder beschreibt. Der `Token`-Eintrag wird im Plugin-Manager-Konstruktor gesetzt; `AgentToken` bleibt initial leer |
| Viewer-Identität des Fixture-Agent-Clients | `ScmE2EApiClient` erhält einen Viewer-Parameter (`viewerLogin`/`viewerNodeId`, Default `scm-owner`/`OWNER`); `ScmE2EPluginManager` injiziert eine zweite Instanz als Agent-Client mit `scm-bot`/`BOT` | Ein einziger Fixture-Client liefert bei `GET user` für beide Konten dieselbe Identität — eine `@scm-owner`-Mention wäre dann trivialerweise „vom Owner" und könnte ein falsches Client-Routing nicht entdecken. Mit `scm-bot` schlägt jedes Lesen unter dem Benutzer-Client fehl (`ViewerLogin` = `scm-owner` ≠ Mention `@scm-bot`), sodass der E2E das Konto-Routing tatsächlich beweist |
| `calls.jsonl` bei zwei Fixture-Clients | Der `_sync`-Lock in `ScmE2EApiClient` (Z. 11) wird zu einem gemeinsamen statischen Lock | Zwei Instanzen (Benutzer-/Agent-Client) schreiben sonst gleichzeitig `File.AppendAllText` in dieselbe Datei; ein statischer Lock ist die robuste Variante |
| `SetPluginSecretField` (E2E) | `Focus()` + Tastatureingabe (`SendInput`/`KEYEVENTF_UNICODE`-Muster aus `ElementWaitHelper.ReplaceTextBoxText`, Z. 261–314), **nicht** ValuePattern | WPF-`PasswordBox` unterstützt über UIA weder `ValuePattern` noch `TextPattern` (Sicherheitsdesign); der `AutomationProperties.Name`-Marker reicht zum Auffinden, die Tastatureingabe zum Befüllen |

## Programmabläufe

### HTTP-Transport: `GitHubHttpApiClient.SendAsync`

1. `tokenProvider()` aufrufen (pro Request — Credential-Änderungen wirken ohne Neustart; bei `null`/leer wird ohne `Authorization`-Header gesendet → 401 fließt in die bestehende `Authentication`-Klassifizierung).
2. Request-URI bilden: `Host == "github.com"` → `https://api.github.com/` + `Endpoint` (inkl. Query); sonst `https://{Host}/api/v3/` + `Endpoint`. Sonderfall `Endpoint == "graphql"` → `https://api.github.com/graphql` bzw. `https://{Host}/api/graphql`. `Host` wird als DNS-Hostname validiert (nur `https`, keine UserInfo/Query/Fragment-Bestandteile — Muster `ParseEventRepository`/`ValidateNextPage`).
3. `HttpRequestMessage` mit den Pflicht-Headern `Accept: application/vnd.github+json`, `X-GitHub-Api-Version: 2022-11-28`, `User-Agent: Softwareschmiede` und — nur bei vorhandenem Token — `Authorization: Bearer {token}`; `JsonBody` als `StringContent` (`application/json`, UTF-8). Keine Temp-Dateien, kein Prozessstart.
4. Antwort lesen: `StatusCode` → `int`, `Content` → `Body`-String, alle Response- und Content-Header → `IReadOnlyDictionary<string,string>` (`Link`, `X-RateLimit-Remaining`/`Reset`, `Retry-After` fließen so in die bestehende Pagination/Fehlerklassifizierung). Nicht-2xx-Status werden **nicht** als Exception geworfen — `SendEventApiAsync` klassifiziert Statuscodes wie bisher (inkl. jetzt verfügbarer Fehler-Body).
5. Transportfehler (`HttpRequestException`, Timeout/nicht-aufruferbedingter Abbruch) → `ScmProviderException(ScmProviderFailureKind.Temporary, "GitHub-Transport nicht erreichbar.", outcomeUncertain: request.Method != "GET" && request.Endpoint != "graphql")` — exakte Spiegelung der bisherigen `gh`-Semantik (`IGitHubEventApiClient.cs` Z. 63–64/77–81); `OperationCanceledException` des Aufrufers wird durchgereicht.
6. Kein Logging im Client — insbesondere niemals Request- oder Response-Header; Exception-Messages enthalten nur Status/Endpoint, nie Token oder Header.

Beteiligte Klassen/Komponenten: `GitHubHttpApiClient`, `IGitHubEventApiClient`, `GitHubEventApiRequest`/`GitHubEventApiResponse`, `ICredentialStore`, `GitHubPlugin.SendEventApiAsync`.

### Umstellung der Nicht-Ereignis-API-Pfade auf den HTTP-Client

Alle folgenden Methoden laufen über `_eventApiClient` (Benutzer-Token; `Konto`-Routing entfällt hier bewusst — siehe Token-Grenze) und behalten ihren Aufrufer-Vertrag. Host/Owner/Name wird per `ParseEventRepository(repositoryId)` aufgelöst — `owner/repo`-Eingaben ergeben `github.com`, URL-Eingaben den enthaltenen Host (GHE-fähig, bisher implizit über `gh`-Auth). `ScmProviderException` bzw. Nicht-2xx wird je Methode auf den bisherigen Fehlervertrag abgebildet:

| Methode (Z.) | Bisher | HTTP-Ersatz | Fehlervertrag (unverändert) |
|---|---|---|---|
| `GetIssuesAsync` (Z. 369–393) | `gh issue list --json` | `GET repos/{o}/{r}/issues?state=open&per_page=100`, paginiert; Einträge mit `pull_request`-Objekt filtern (`gh` blendet PRs aus, REST nicht); `ParseIssues` liest `html_url` statt `url` (REST-`url` ist die API-URL) | `[]` bei Fehler |
| `GetAlertsAsync` (Z. 396–442) | `gh api --paginate --slurp code-scanning/alerts` | `GET repos/{o}/{r}/code-scanning/alerts?state=open&per_page=100`, paginiert; Slurp-Nesting entfällt, `EnumerateCodeScanningAlertElements` vereinfacht sich | `[]` bei 404/Auth-Fehler/sonstigem Fehler (inkl. sanitisierter Log-Meldung) |
| `GetIssueTemplatesAsync`/`LoadTemplateContentAsync` (Z. 500–593) | `gh api repos/{id}/contents/...` | `GET repos/{id}/contents/.github/ISSUE_TEMPLATE` + `GET repos/{id}/contents/{path}` | 404 → leere Liste; sonst `Failed(...)` |
| `GetOpenPullRequestsAsync` (Z. 757–788) | `gh api --paginate --slurp pulls?state=open` | `GET repos/{id}/pulls?state=open&per_page=100`, paginiert; Slurp-Nesting entfällt (Parser liest bereits REST-snake_case) | `[]` bei Fehler |
| `GetPullRequestStatusAsync` (Z. 1041–1067) | `gh pr view --json` | GraphQL-Query (Felder identisch zur bisherigen `--json`-Liste) via `GraphAsync`; `ParsePullRequestStatus` unverändert | `InvalidOperationException` bei Fehler |
| `GetPullRequestWorkflowRunsAsync`/`RunWorkflowListAsync` (Z. 1070–1104, 1168–1191) | `gh run list --json … --commit` | `GET repos/{id}/actions/runs?head_sha={sha}&per_page=100` über neuen Objekt-Seitenhelfer (`{total_count, workflow_runs}`-Wrapper); `ParseWorkflowRuns` auf snake_case umgestellt | `InvalidOperationException` bei Fehler |
| `CheckHealthAsync` (Z. 1346–1351) | `gh auth status` | `GET user` über Benutzer-Client | `false` bei Nicht-2xx/Transportfehler |
| `GetAvailableRepositoriesAsync` (Z. 1386–1419) | `gh repo list --json` | `GET user/repos?affiliation=owner&per_page=100`, paginiert; Feld-Mapping `full_name`→`nameWithOwner`, `html_url`→`url`, `updated_at`/`created_at` | `[]` bei Fehler |
| `GetRepositoryStructureLoadResultAsync` (Z. 1469–1504) | `gh api repos/{id}/git/trees/{branch}?recursive=1` | identischer REST-Endpunkt via Client | `Failed(...)` bei Fehler |
| `CreateIssueAsync`, `CreatePullRequestAsync`, `CompletePullRequestAsync` | `gh issue/pr create`, `gh pr merge`/`review` | **unverändert auf `gh`** (ereignisgebundene Mutationen) | unverändert |

Folge-`GetPullRequestStatusAsync`-Aufrufe innerhalb `CreatePullRequestAsync` (Z. 1017) und `CompletePullRequestAsync` (Z. 1159) laufen automatisch über HTTP. `GetGhEnvironment` bleibt für die drei verbleibenden `gh`-Mutationen **und** für `GetDefaultBranchAsync` (`git ls-remote` mit `GH_TOKEN`-Env, Z. 1426–1430) bestehen; `git`-Aufrufe (`CloneRepositoryAsync`, `PushBranchAsync`, `PullAsync`, `GetRemoteBranchesAsync`, `GetDefaultBranchAsync`, `NormalizeRemoteUrlAsync`, `ConfigureGitCredentialsAsync`) bleiben unverändert prozessbasiert — sie sind lokale Repository-Operationen, kein API-Pfad (seltene `conhost`-Blitzer, ereignisgebunden).

Beteiligte Klassen/Komponenten: `GitHubPlugin` (`GitHubPlugin.cs`, `GitHubPlugin.Events.cs`), `GitHubHttpApiClient`, `IGitHubEventApiClient`, `PullRequestMonitoringService`, `ScmTaskCompletionMonitoringService`, `ScmBackmergeService`.

### Modusauswahl laden und speichern

1. `SettingsViewModel.LadenAsync` liest `AppEinstellungService.GetScmKontoModusAsync` und setzt `SelectedKontoModus = ScmKontoModusOption.FuerModus(modus)` (Muster `SelectedBackmergeModus`, Z. 439–440).
2. `SettingsViewModel.SpeichernAsync` prüft `SelectedKontoModus != null` (Abbruch mit Fehlermeldung, Muster Z. 478–482) und ruft `SetScmKontoModusAsync`.
3. `ValidierePflichtfelder` (Kette Z. 725–732) wird um `ValidiereScmKontoModus` erweitert: Ist `SeparatesKonto` gewählt, muss der effektive `AgentToken`-Wert des GitHub-Plugins nicht leer sein (Formularwert bei geladenen GitHub-Einstellungen, sonst persistierter Wert); sonst `FehlerMeldung` mit Hinweis auf Einstellungen → Plugins → GitHub → Authentifizierung und Abbruch.
4. `SettingsView.xaml` zeigt die ComboBox `Konto-Modus` im Abschnitt „SCM-Ereignisse" (nach dem Trigger-Keyword, vor „Backmerge-Pull-Requests") mit erläuterndem `TextBlock`.

Beteiligte Klassen/Komponenten: `SettingsViewModel`, `SettingsView.xaml`, `ScmKontoModusOptionen`, `AppEinstellungService`, `PluginSettingsService`, `GitHubPlugin` (`GetSettingGroups`).

### Poll-Durchlauf im Modus Benutzerkonto

Unverändert: `RunOnceAsync` führt Alias-Auflösung, `EnumerateRepositoryAsync`, `ReadEventsAsync` und `AcceptBatchAsync` wie bisher aus; alle erzeugten `ScmRepositoryContext`/`ScmEventRepository` tragen `Konto = Benutzerkonto` (Default), das Plugin nutzt den bisherigen `Token`-Credential. `GetRejection` wendet die bisherigen Regeln an. Sämtliche Plugin-API-Aufrufe laufen prozesslos über `GitHubHttpApiClient`.

Beteiligte Klassen/Komponenten: `ScmEventMonitoringService`, `IScmEventSource`, `ScmEventStore`, `ScmEventValidationService`, `GitHubHttpApiClient`.

### Poll-Durchlauf im Modus SeparatesKonto

1. `RunOnceAsync`: `GetScmMonitoringEnabledAsync` (unverändert), dann `GetScmKontoModusAsync` pro Zyklus → `SeparatesKonto`.
2. Alias-Auflösung wie bisher über alle aktiven `GitRepository`-Einträge des Plugins, aber `ScmRepositoryContext` mit `Konto = SeparatesKonto` → `GetEventRepositoryAsync` läuft unter dem AgentToken (HTTP-Client mit AgentToken-Selektor) und liefert `ScmEventRepository` mit `Viewer*` = Bot-Identität und `Konto = SeparatesKonto`. Fehler/Backoff bleiben über `repository/{prefix}/{alias}`-Checkpoints. Verknüpfte Referenzen aus `Aufgaben` (`linked`) werden im `SeparatesKonto` nicht als Beobachtungsquellen übernommen — ausschließlich Notification-Referenzen zählen; die Auflösung dient nur dem Repository-Allowlist-Aufbau.
3. Statt `EnumerateRepositoryAsync`: je `IScmNotificationSource`-Plugin und je distinctem `Host` der aufgelösten Repositories `EnumerateNotificationsAsync(host, sinceUtc, ct)` aufrufen. `sinceUtc` = Checkpoint-Watermark `notifications/{pluginPrefix}/{host}` minus `2 × PollInterval`-Überlappung (Muster `EnumerateRepositoryAsync` Z. 201–203); Fehler → `RecordFailureAsync` auf demselben Checkpoint.
4. Notifications filtern: nur `Reason == "mention"`; Threads, deren `(Host, RepositoryNodeId)` keiner aufgelösten `ScmEventRepository` entsprechen, werden still verworfen; die Anzahl verarbeiteter/verworfener Fremd-Notifications wird als `Diagnostic` mit dem `InboxDiscardDiagnosticPrefix` am Inbox-Checkpoint sichtbar gemacht (geklärte Punkte 6/7) — in der SCM-Ereignisübersicht über `ScmEventQueueViewModel.Diagnostics`/`ScmEventDiagnostics` sichtbar, **ohne** `MonitoringNotice` auszulösen (Präfix-Ausnahme).
5. Pro Treffer `resolved.Add((null, new ScmEventReference(repository, notification.Kind, notification.Number)))` — die Referenz trägt `Konto` bereits über `repository`. Duplikate je `(RepositoryId, Kind, Number)` werden zusammengeführt.
6. Anschließend läuft der bestehende Block ab Z. 113 unverändert: pro Referenz × `ScmContentKind` `ReadEventsAsync` (unter AgentToken, da `Reference.Repository.Konto`) mit eigenem Inhalts-Checkpoint; `GetRejection` wendet Bot-Regeln an (Trigger ist hier die `@mention` des separaten Kontos — nur im Notifications-Modus; die Keyword-Prüfung bleibt exklusiv dem `Benutzerkonto` vorbehalten, geklärter Punkt 4); unzugeordnete Referenzen mit gültiger Erwähnung laufen über `ScmEventTaskResolver.ResolveAsync` (Aufgabenanlage/Wiederverwendung); `AcceptBatchAsync` nimmt dedupliziert in die Inbox auf.
7. Inbox-Watermark (`notifications/{pluginPrefix}/{host}`) wird erst committet, wenn die Inbox gelesen **und** alle daraus abgeleiteten Quellen des Zyklus erfolgreich verarbeitet wurden; sonst bleibt der Watermark stehen und die Notification erscheint im nächsten `since`-Fenster erneut (idempotent, da Snapshots/`EventKey` bereits akzeptierte Versionen deduplizieren). Verhindert Ereignisverlust bei Teilfehlern zwischen Inbox-Lesen und Inbox-Aufnahme. Beim Commit wird `Diagnostic` (mit `InboxDiscardDiagnosticPrefix`) gesetzt und `ScmEventChangeNotifier.Notify()` aufgerufen, damit die Übersicht aktualisiert.
8. Dispatch-Schleife ab Z. 160 unverändert.

Beteiligte Klassen/Komponenten: `ScmEventMonitoringService`, `IScmNotificationSource`, `IScmEventSource`, `ScmObservationCheckpoint`, `ScmEventTaskResolver`, `ScmEventStore`, `ScmEventOrchestrator`, `ScmEventChangeNotifier`, `GitHubHttpApiClient` (Agent-Token-Instanz).

### Repository-Auflösung, Validierung und Antworten unter dem Bot-Konto

1. `GitHubPlugin.GetEventRepositoryAsync` liest `repository.Konto`, wählt den zugehörigen `IGitHubEventApiClient` (`ClientFor`) und liefert `ScmEventRepository` mit eingestempeltem `Konto` und der Viewer-Identität des jeweiligen Kontos (`GET user` + `GET repos/{o}/{r}` — beides prozesslose HTTP-Aufrufe).
2. `ReadEventsAsync`, `ReadIssueAsync`, `ReadPullRequestAsync`, `EnumerateReferencesAsync`, `ExecuteResponseAsync`, `ReconcileResponseAsync` routen alle internen API-Hilfsaufrufe (`SendEventApiAsync`/`EventApiAsync`/`GraphAsync`/`RestPagesAsync`) über `request.Reference.Repository.Konto` bzw. `repository.Konto` an den passenden Client — Viewer-Revalidierung (`ViewerId`, `CanWrite`) und `IsViewer`-Markerfilter laufen damit automatisch gegen die Bot-Identität.
3. `ScmEventOrchestrator.DispatchCoreAsync` baut den `ScmRepositoryContext` zur Revalidierung (Z. 130) mit `observation.Reference.Repository.Konto` → `GetEventRepositoryAsync` läuft unter dem Token der Aufnahme; der Toleranzvergleich bei Z. 139 (`revalidation != "Kein Trigger"`, String-Literal) wird um die neue Ablehnungs-Konstante „Keine Erwähnung des separaten Kontos" erweitert (analog „Kein Trigger": Trigger wurde bei der Aufnahme geprüft).
4. `ScmEventTaskResolver` (Z. 92) übergibt `reference.Repository.Konto` in den neu erzeugten `ScmRepositoryContext`.
5. `ScmTaskCompletionMonitoringService` (Z. 191–193) liest `GetScmKontoModusAsync` und übergibt den aktiven Modus in den `ScmRepositoryContext` (geklärter Punkt 3: konsistent das AgentToken für alle SCM-Zugriffe im `SeparatesKonto` — begrenzt auf die Ereignis-Träger; `GetPullRequestStatusAsync` Z. 165 bleibt dokumentiertermaßen Benutzer-Token, da kein Konto-Kontext existiert).
6. `KiAusfuehrungsService.WithScmAgentEnvironmentAsync` liest `GetScmKontoModusAsync` über einen Scope (`_scopeFactory` vorhanden, Z. 32/48) und ruft `agentAccess.GetAgentEnvironmentAsync(konto, ct)` → `GH_TOKEN`/git-`http.extraheader` des gewählten Kontos für die CLI-Agenten-Sitzung (im `SeparatesKonto` auch die Agenten-Sitzung unter dem AgentToken; das ist Prozessumgebung für vom Agenten selbst gestartete `gh`/git-Aufrufe — unverändert und kein eigener API-Pfad der Anwendung).

Beteiligte Klassen/Komponenten: `GitHubPlugin` (`GitHubPlugin.Events.cs`, `GitHubPlugin.EventResponses.cs`, `IGitHubEventApiClient.cs`, `GitHubHttpApiClient.cs`), `ScmEventOrchestrator`, `ScmEventTaskResolver`, `ScmTaskCompletionMonitoringService`, `KiAusfuehrungsService`, `AppEinstellungService`.

### Notification-Lesen im GitHubPlugin

1. `EnumerateNotificationsAsync(host, sinceUtc, ct)`: validiert `host` (Provider-Host), ruft `GET notifications?all=true&participating=true` (plus `&since={ISO}` bei vorhandenem `sinceUtc`) über den Agent-Client paginiert (`RestPagesAsync`-Muster, prozesslos) und parst Threads.
2. Mapping je Thread: `id` → `ThreadId`, `repository.node_id`/`owner.login`/`name` → Repository-Koordinaten, `subject.type` (`Issue`/`PullRequest`) → `Kind`, Nummer aus `subject.url` (letztes Pfadsegment), `reason` → `Reason`, `updated_at` → `UpdatedAt`. Subjects anderer Typen (Release, CheckSuite, Discussion …) werden übersprungen.
3. Fehlerklassifizierung läuft über den vorhandenen `SendEventApiAsync`-Pfad (`ScmProviderException` mit `Authentication`/`Permission`/`RateLimit`/`Temporary`/`Permanent`); fehlendes AgentToken wird als `Authentication`-Fehler mit Hinweis auf Einstellungen → Plugins → GitHub → Authentifizierung gemeldet.

Beteiligte Klassen/Komponenten: `GitHubPlugin` (`GitHubPlugin.Events.cs`), `IScmNotificationSource`, `ScmNotification`, `IGitHubEventApiClient`, `GitHubHttpApiClient`.

### E2E-Fluss der Bot-Modus-Phase (im konsolidierten SCM-E2E)

1. Negativfall UI-Validierung: Einstellungen öffnen → Tab „Allgemein" → ComboBox `Konto-Modus` auf „Separates Konto" → `SaveSettings` → `InvalidOperationException` mit `FehlerMeldung` (AgentToken fehlt). Danach `DiscardChanges()` aufrufen — setzt den Fehlerbanner zurück und belegt zugleich „Modus bleibt `Benutzerkonto`" (verhindert, dass die Fail-Fast-Banner-Prüfung von `ElementWaitHelper.WaitForElement`, Z. 77–89, das nachfolgende Warten auf den Erfolgs-Toast mit dem alten Banner verwechselt).
2. Token eintragen: Tab „Plugins" → `Softwareschmiede.GitHub.Eintrag` wählen → `AgentToken`-Secret-Feld per Focus + Tastatureingabe füllen (`SetPluginSecretField`, **kein** ValuePattern) → zurück zu „Allgemein" → `Konto-Modus` „Separates Konto" → `SaveSettings` → „Einstellungen gespeichert." (ein Speicherschritt, da die Prüfung den Formularwert wertet).
3. Live-Umschaltung: der nächste Poll ruft `GET notifications` am Agent-Client (nachweisbar über `calls.jsonl`-Eintrag via `WaitForCallsAsync`).
4. Mention-Notification (`AddNotification`, Repository `REPO`, Issue #18) + `AddText` mit `@scm-bot`-Body → Referenz → Read → Resolver legt Aufgabe an → Ereignis `Wartend` → über UI starten → `Verarbeitet` inkl. Empfangsreaktion/Antwort.
5. Fremd-Notification (`AddNotification` mit abweichender `RepositoryNodeId`) → verworfen, kein Inbox-Eintrag → Verwerfungsdiagnose am `notifications/…`-Checkpoint: Der „Integrationshinweise"-`Expander` (enthält `ScmEventDiagnostics`) wird per UI aufgeklappt; alternativ/zusätzlich Assertion auf `ScmEventMonitoringNotice` (muss **nicht** auslösen — Präfix-Ausnahme) und auf den Checkpoint in der Fixture-DB.
6. Rückschaltung per `ScmE2EFixture.SetKontoModus(Benutzerkonto)` (DB-Ebene) → nächster Poll enumeriert wieder repository-weit → `$Softwareschmiede`-Trigger-Ereignis läuft wie bisher.

Beteiligte Klassen/Komponenten: `ScmEventsE2ETests`, `ScmE2EFixture`, `ScmE2EScenario`, `ScmE2EApiClient`, `ScmE2EPluginManager`, `ScmE2ECredentialStore`, `SettingsView` (E2E-View), `ScmEventQueueView`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `GitHubHttpApiClient` | Klasse (`plugins/Softwareschmiede.Plugin.GitHub/GitHubHttpApiClient.cs`, implementiert `IGitHubEventApiClient`) | Prozessloser GitHub-API-Transport via `HttpClient`: Base-URL-Mapping (github.com/GHE/GraphQL), Pflicht-Header inkl. `Authorization: Bearer` aus `Func<string?>`-Token-Selektor, Response-Header-Weitergabe, typisierte Transportfehler; statisch geteilter `HttpClient` + injizierbarer Handler für Tests |
| `ScmKontoModus` | Enum (`src/Softwareschmiede.Plugin.Contracts/Domain/Enums/ScmKontoModus.cs`, Namespace `Softwareschmiede.Domain.Enums`) | `Benutzerkonto = 0`, `SeparatesKonto = 1`; App-Einstellung und Credential-Selektor auf Contract-Records |
| `IScmNotificationSource` | Interface (`src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmNotificationSource.cs`, Namespace `Softwareschmiede.Domain.Interfaces`) | Optionale Plugin-Fähigkeit: `EnumerateNotificationsAsync(string host, DateTimeOffset? sinceUtc, CancellationToken)` → `Task<IReadOnlyList<ScmNotification>>` |
| `ScmNotification` | Datenmodellklasse (record, `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` oder eigene Datei `ScmNotification.cs`) | Ein Inbox-Eintrag: `ThreadId`, `RepositoryNodeId`, `Host`, `Owner`, `Name`, `Kind` (`ScmReferenceKind`), `Number`, `Reason`, `UpdatedAt` |
| `ScmKontoModusTexte` + `ScmKontoModusOption` | statische Klasse + record (`src/Softwareschmiede.App/ViewModels/ScmKontoModusOptionen.cs`) | Anzeige-Labels und Optionsliste der Modusauswahl (Vorlage `ScmBackmergeModusOptionen.cs`) |
| `ScmE2ENotification` | record (`src/Softwareschmiede.App/Services/Testing/ScmE2ETestConfiguration.cs`) | Fixture-Eintrag einer Inbox-Notification: `Number`, `Kind`/PR-Flag, `RepositoryNodeId`, `Reason`, `UpdatedAt`, `ThreadId` — inklusive Fremd-Repository-Variante über abweichende `RepositoryNodeId` |
| `ScmE2ECredentialStore` | Klasse (`src/Softwareschmiede.App/Services/Testing/ScmE2ECredentialStore.cs`, implementiert `ICredentialStore`) | In-Memory-Credential-Store der SCM-E2E-App; ersetzt `WindowsCredentialStore` und `EmptyCredentialStore` im Testmodus |
| `GitHubHttpApiClientTests` | Testklasse (`src/Softwareschmiede.Tests/Infrastructure/Plugins/`) | Transport-Tests des HTTP-Clients (Fake-`HttpMessageHandler`, nur Fake-Tokens) |
| `GitHubNotificationSourceTests` | Testklasse (`src/Softwareschmiede.Tests/Infrastructure/Plugins/`) | Transport-Mock-Tests des Notification-Pfads (Vorlage `GitHubEventSourceTests`) |
| `AppEinstellungServiceTests_KontoModus` | Testklasse (`src/Softwareschmiede.Tests/Application/Services/`) | Getter-/Setter-/Default-Verhalten der Modus-Einstellung |
| `ScmEventMonitoringServiceTests_Notifications` | Testklasse (`src/Softwareschmiede.IntegrationTests/Services/`) | Integrationstests des Bot-Modus über `ScmDispatchFixture` |

## Änderungen an bestehenden Klassen

### `IGitHubEventApiClient.cs` (Datei mit Interface + Records)

- **Entfernt:** `GhEventApiClient` (Z. 24–87) — vollständig ersetzt durch `GitHubHttpApiClient`; damit entfallen `ICliRunner`-Abhängigkeit, `--include`-Header-Parsing und die Temp-Datei (`--input softwareschmiede-gh-*.json`, Z. 37–44/84) des Transports.
- **Unverändert:** `IGitHubEventApiClient`, `GitHubEventApiRequest`, `GitHubEventApiResponse`.

### `GitHubPlugin` (partial, `GitHubPlugin.cs`/`GitHubPlugin.Events.cs`/`GitHubPlugin.EventResponses.cs`)

- **Neue Konstanten:** `GitHubAgentTokenCredentialKey = "Softwareschmiede.GitHub.AgentToken"`.
- **Geänderte Felder:** `_eventApiClient` — Default `new GitHubHttpApiClient(() => _credentialStore.GetCredential(GitHubTokenCredentialKey))` statt `GhEventApiClient`; **Neues Feld:** `_agentEventApiClient` (`IGitHubEventApiClient`, Default `GitHubHttpApiClient` mit `GitHubAgentTokenCredentialKey`-Selektor; optionale Injection über den längeren Konstruktor).
- **Geänderte Methoden (HTTP-Umstellung):** `GetIssuesAsync`, `GetAlertsAsync`, `GetIssueTemplatesAsync`, `LoadTemplateContentAsync`, `GetOpenPullRequestsAsync`, `GetPullRequestStatusAsync`, `GetPullRequestWorkflowRunsAsync`/`RunWorkflowListAsync`, `CheckHealthAsync`, `GetAvailableRepositoriesAsync`, `GetRepositoryStructureLoadResultAsync` — Transport über `IGitHubEventApiClient` statt `_cliRunner.RunAsync("gh", …)` (Mapping-Tabelle siehe Programmablauf); `ParseIssues` liest `html_url`; `ParseWorkflowRuns` liest snake_case-Felder; `EnumerateCodeScanningAlertElements`/`ParseOpenPullRequests` verlieren das `--slurp`-Nesting.
- **Unverändert auf `gh`/`git`:** `CreateIssueAsync`, `CreatePullRequestAsync`, `CompletePullRequestAsync` (Mutationen) sowie alle `git`-Pfade; `GetGhEnvironment` bleibt für diese **und** für `GetDefaultBranchAsync` (`git ls-remote`).
- **Geänderte Methoden (Kontomodus):** `GetSettingGroups` — neues `PluginSettingField` `AgentToken` (`PluginSettingFieldType.Secret`, `IsRequired = false`, Beschreibung mit Scope-Hinweis `notifications`+`repo`) in der Gruppe „Authentifizierung"; `GetEventRepositoryAsync`/`ReadEventsAsync`/`EnumerateReferencesAsync`/`ReadIssueAsync`/`ReadPullRequestAsync`/`ExecuteResponseAsync`/`ReconcileResponseAsync` — Konto-Routing aller API-Aufrufe anhand `Konto` des Kontexts/der Referenz, Ergebnis-`ScmEventRepository` mit `Konto` stempeln; `GetAgentEnvironmentAsync(ScmKontoModus konto, CancellationToken ct)` — Credential-Auswahl per `CredentialKeyFor(konto)`; `SanitizeSensitiveOutput` — maskiert **beide** Tokens (Erweiterung der Ein-Token-Signatur, Z. 202–219, inkl. aller Aufrufstellen); fehlendes AgentToken bei `SeparatesKonto`-Aufrufen → `ScmProviderException(Authentication, …)` mit Einstellungshinweis.
- **Neue Methoden:** `EnumerateNotificationsAsync` (`IScmNotificationSource`) — `GET /notifications` paginiert über Agent-Client, Thread→`ScmNotification`-Mapping, Subject-Typ-Filter; `ClientFor`/`CredentialKeyFor` — Client- und Credential-Auswahl je Konto; private Transport-Helfer (`SendEventApiAsync`/`EventApiAsync`/`GraphAsync`/`RestPagesAsync`) erhalten einen Konto-Parameter bzw. den ausgewählten Client; neuer Objekt-Seitenhelfer für den `actions/runs`-Wrapper (`{total_count, workflow_runs}`) mit derselben `Link`-/`ValidateNextPage`-Semantik.

### `ScmEventRepository` (Datenmodellklasse, `ScmEvents.cs` Z. 14–15)

- **Neue Eigenschaften:** `Konto` (`ScmKontoModus`, 10. Positions-Komponente mit Default `Benutzerkonto`) — Credential-Selektor, propagiert über alle Records, die eine Referenz enthalten.

### `ScmRepositoryContext` (Datenmodellklasse, `ScmRepositoryContext.cs`)

- **Neue Eigenschaften:** `Konto` (`ScmKontoModus`, optionaler Konstruktorparameter `konto` mit Default `Benutzerkonto`) — wählt bei `GetEventRepositoryAsync` das Konto und wird in das Ergebnis eingestempelt.

### `ScmEventValidationService` (Klasse)

- **Geänderte Methoden:** `GetRejection` — verzweigt auf `observation.Reference.Repository.Konto` (geklärte Punkte 4/5). Bei `Benutzerkonto` bleibt der Trigger-Keyword-Check (`scm.trigger.keyword`) exakt wie bisher (Z. 27–39). Bei `SeparatesKonto` triggert stattdessen die `@mention` des separaten Kontos: Erwähnungs-Check `HasFreeStandingTrigger(observation.Body, "@" + repository.ViewerLogin)` auf allen `ScmContentKind`s (Erwähnung in Beschreibung oder Kommentar gleichwertig, keine zusätzliche Keyword-Pflicht) mit neuer Ablehnung „Keine Erwähnung des separaten Kontos"; die Regel „Angemeldeter Benutzer ist nicht Repository-Owner" (Z. 29–30) entfällt; „Bearbeiter nicht bestimmbar", Autor-/Editor-Identität gegen `OwnerId`/`Owner` („Textversion stammt nicht vom Repository-Owner", Z. 31–37 — im Bot-Modus gegen `Owner*` statt `Viewer*` geprüft, da der Viewer das Bot-Konto ist) und „Eigene Antwort" bleiben.
- **Neue Konstanten:** Ablehnungstext für fehlende Erwähnung (z. B. `KeineErwähnungRejection`), damit `ScmEventOrchestrator` und Tests gegen denselben String prüfen.

### `AppEinstellungService` (Klasse)

- **Neue Konstanten:** `ScmKontoModusKey = "scm.monitoring.accountmode"`.
- **Neue Methoden:** `GetScmKontoModusAsync` — fehlende/ungültige Werte → `Benutzerkonto` (Muster `GetScmBackmergeModusAsync`, Z. 187–193); `SetScmKontoModusAsync` — `Enum.IsDefined`-Prüfung, `ArgumentOutOfRangeException` (Muster Z. 199–205).

### `ScmEventMonitoringService` (Klasse)

- **Geänderte Methoden:** `RunOnceAsync` — liest `GetScmKontoModusAsync` pro Zyklus; übergibt `Konto` beim Bau der `ScmRepositoryContext`s der Alias-Auflösung (Z. 79); verzweigt im `SeparatesKonto` nach der Alias-Auflösung in den Inbox-Pfad (kein `EnumerateRepositoryAsync`, keine verknüpften Referenzen aus `Aufgaben` als Beobachtungsquellen — ausschließlich Notification-Referenzen); danach unverändert weiter in den Read-/Resolve-/Accept-Block (ab Z. 113) und die Dispatch-Schleife (ab Z. 160).
- **Neue Methoden:** `EnumerateNotificationsAsync` (private) — Inbox-Polling je Host mit Checkpoint `notifications/{pluginPrefix}/{host}`, `sinceUtc`-Fenster mit Überlappung, `Reason`-Filter, Repository-Match gegen aufgelöste `ScmEventRepository`, Verwerfungsdiagnose mit `InboxDiscardDiagnosticPrefix` für Fremd-Repositories, `RecordFailureAsync`-Backoff, verzögerte Watermark-Commit-Regel inkl. `Diagnostic`-Schreiben und `Notify()` (siehe Programmablauf).

### `ScmEventStore` (Klasse)

- **Neue Konstanten:** `InboxDiscardDiagnosticPrefix` (z. B. `"Inbox:"`) — markiert Routine-Zähler verworfener Fremd-Notifications, die keinen `MonitoringNotice`-Alarm auslösen dürfen.

### `ScmEventQueueViewModel` (Klasse)

- **Geänderte Blöcke:** `sourceErrors`-Query (Z. 170–171) — zusätzlich `UnassignedDiagnostic` werden Diagnostics mit `InboxDiscardDiagnosticPrefix` ausgefiltert (`StartsWith`), damit die Verwerfungsdiagnose kein „Überwachung unterbrochen"-`MonitoringNotice` auslöst; sie bleibt im `Diagnostics`-Text sichtbar (Z. 174–176 liest dieselbe Query — Ausfilterung nur für den Alarm, nicht für die Anzeige: die Diagnose soll sichtbar sein, aber nicht als Unterbrechung gelten → Umsetzung: separate Abfrage bzw. Filterung ausschließlich für `MonitoringNotice`, `Diagnostics` erhält sie weiterhin).

### `IScmAgentAccess` (Interface)

- **Geänderte Methoden:** `GetAgentEnvironmentAsync` — neuer Parameter `ScmKontoModus konto` vor `ct`. Alle drei bestehenden Aufrufstellen werden angepasst: `KiAusfuehrungsService.WithScmAgentEnvironmentAsync` (Z. 378), `ScmDispatchFixture` (Z. 75, `It.IsAny<ScmKontoModus>()`), `ScmEventDispatchTests_AgentActions` (Z. 26 und Z. 52).

### `ScmEventOrchestrator` (Klasse)

- **Geänderte Methoden:** `DispatchCoreAsync` — `ScmRepositoryContext` bei der Repository-Revalidierung (Z. 130) mit `observation.Reference.Repository.Konto`; Toleranzvergleich bei Z. 139 um die Konstante „Keine Erwähnung des separaten Kontos" erweitern.

### `ScmEventTaskResolver` (Klasse)

- **Geänderte Methoden:** `ResolvePullRequestAsync` (Z. 92) — `ScmRepositoryContext` mit `Konto` der auslösenden Referenz (dient `CreateFromPullRequestAsync`, kein Plugin-Aufruf).

### `ScmTaskCompletionMonitoringService` (Klasse)

- **Geänderte Methoden:** Issue-/PR-Statusprüfung (Z. 191–195) — `ScmRepositoryContext` mit dem aktiven `GetScmKontoModusAsync`-Modus (nur Issue-Pfad; der PR-Pfad Z. 165 bleibt dokumentiertermaßen Benutzer-Token).

### `KiAusfuehrungsService` (Klasse)

- **Geänderte Methoden:** `WithScmAgentEnvironmentAsync` — liest `GetScmKontoModusAsync` (Scope über `_scopeFactory`, Z. 32/48) und reicht den Modus an `GetAgentEnvironmentAsync` weiter.

### `SettingsViewModel` (Klasse)

- **Neue Eigenschaften:** `KontoModusOptionen` (`IReadOnlyList<ScmKontoModusOption>`), `SelectedKontoModus` (`ScmKontoModusOption?`).
- **Geänderte Methoden:** `LadenAsync` — Modus laden; `SpeichernAsync` — Null-Absicherung + `SetScmKontoModusAsync`; `ValidierePflichtfelder` — zusätzliches Glied `ValidiereScmKontoModus` (AgentToken-Pflicht bei `SeparatesKonto` gegen den effektiven Wert).
- **Neue Methoden:** `ValidiereScmKontoModus` — siehe Designentscheidung „AgentToken-Pflichtprüfung".

### `SettingsView.xaml` (View)

- **Neue Elemente:** `TextBlock` „Kontomodus" + `ComboBox` (`AutomationProperties.Name="Konto-Modus"`, `ItemsSource=KontoModusOptionen`, `SelectedItem=SelectedKontoModus`, `ItemTemplate`→`Label`) + erläuternder `TextBlock` im Abschnitt „SCM-Ereignisse" (nach Trigger-Keyword, Muster Z. 327–345).
- **Geänderte Elemente:** `SecretTemplate` (Z. 17–25) — die `PasswordBox` erhält `AutomationProperties.Name="{Binding Field.Key}"`, damit Secret-Felder (vorerst `Token`, neu `AgentToken`) per FlaUI adressierbar sind. Voraussetzung für den E2E-Bedienschritt „AgentToken eintragen".

### `App.xaml.cs` (Test-Infrastruktur-Verdrahtung)

- **Geänderte Blöcke:** `scmTestConfiguration`-Block (Z. 349–357) — registriert `ICredentialStore` als `ScmE2ECredentialStore` (letzte Registrierung gewinnt), damit `PluginSettingsService` und das Fixture-`GitHubPlugin` denselben Test-Store nutzen.

### `ScmE2EApiClient` (Test-Infrastruktur, `Services/Testing/`)

- **Geänderte Methoden:** Konstruktor mit Viewer-Parametern (`viewerLogin`/`viewerNodeId`, Default `scm-owner`/`OWNER`); `Respond` — `GET user` liefert die parametrierte Identität; neuer Endpunkt-Branch `GET notifications` liefert Threads aus `scenario.Notifications` (Felder `id`, `repository.node_id`/`owner`/`name`, `subject.type`/`url`, `reason`, `updated_at`), `since`-Parameter tolerant; `_sync`-Lock wird statisch (geteilte `calls.jsonl` zweier Instanzen); `Node()`-Autoren-Mapping optional um Bot-Login ergänzen; `Remote()`-Autor optional parametrierbar (analog Viewer).
- **Neue Elemente:** Tolerante Fixture-Branches für neu über den Client geroutete Endpunkte, sofern E2E-Pfade sie berühren (`repos/{id}/actions/runs` → leere `workflow_runs`-Liste; PR-Status-GraphQL-Query mit `mergeStateStatus` → szenariobasierte Standardantwort) — bisher warfen diese Pfade in der E2E-App über `ScmE2EGitRunner` (nur `git` erlaubt) eine Exception; sie treffen nun den Fixture-Transport.

### `ScmE2EScenario` (Test-Infrastruktur, `ScmE2ETestConfiguration.cs`)

- **Neue Eigenschaften:** `Notifications` (`List<ScmE2ENotification>`) — serialisierter Inbox-Bestand des Szenarios.

### `ScmE2EPluginManager` (Test-Infrastruktur, `Services/Testing/`)

- **Geänderte Methoden:** Konstruktor — neuer `ICredentialStore`-Parameter (der DI-`ScmE2ECredentialStore`), setzt `Softwareschmiede.GitHub.Token` einmalig (Fixture-Wert), injiziert `GitHubPlugin` mit zwei `ScmE2EApiClient`-Instanzen: Benutzer-Client (`scm-owner`/`OWNER`) und Agent-Client (`scm-bot`/`BOT`); die nested `EmptyCredentialStore`-Klasse entfällt.

### `ScmE2EFixture` (Test-Helfer)

- **Neue Methoden:** `AddNotification` — Szenario-Eintrag einer Inbox-Notification inkl. Fremd-Repository-Variante (`repositoryNodeId`-Parameter); `SetKontoModus` — schreibt den Modus über `AppEinstellungService.SetScmKontoModusAsync` auf DB-Ebene; `WaitForCallsAsync(endpointFragment, minCount)` — wartet auf neue `calls.jsonl`-Einträge eines Endpunkts (z. B. `notifications`).
- **Geänderte Methoden:** `WaitForPollAsync` (Z. 149–159) — zählt aktuell fest 8 API-Aufrufe pro Poll; wird um einen Parameter für die erwartete Mindestzahl erweitert bzw. für Bot-Phasen durch `WaitForCallsAsync` ersetzt, da das Aufrufmuster im `SeparatesKonto` (`GET notifications` + Reads statt Enumeration) abweicht.

### `SettingsView` (E2E-View, `src/Softwareschmiede.Tests/E2E/Views/`)

- **Neue Methoden:** `SetKontoModus(label)`/`GetKontoModus()`/`WaitForKontoModus(label)` — Spiegel des `SetUpdateMode`-Musters (`SelectComboBoxItemByClick` + `WaitForSelectedComboBoxItem`); `SetPluginSecretField(fieldKey, value)` — setzt ein Secret-`PluginSettingField` (PasswordBox) per `Focus()` + Tastatureingabe (`SendInput`-Muster aus `ElementWaitHelper.ReplaceTextBoxText`, **kein** ValuePattern).

## Datenbankmigrationen

Keine. `ScmObservationCheckpoint.SourceKey` ist ein freier String (neuer `notifications/…`-Namensraum ohne Schemaänderung); `ScmIncomingEvent.ObservationJson` ist ein JSON-Blob — das neue `Konto`-Feld serialisiert/deserialisiert ohne Migration (Altdaten → Default `Benutzerkonto`); `AppEinstellung` ist generischer Key-Value-Speicher. Die Transportumstellung ist persistenzfrei.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `GitHubEventApiRequest.Host` (HTTP-Transport) | DNS-Hostname; Ziel-URI ausschließlich `https` auf `api.github.com` bzw. `{host}/api/v3` (`graphql`-Sondermapping); kein UserInfo/Fragment | `ScmProviderException(Permanent)` bzw. `Temporary`-Transportfehler |
| `scm.monitoring.accountmode` | Nur definierte `ScmKontoModus`-Werte (`Enum.IsDefined`) | `SetScmKontoModusAsync` wirft `ArgumentOutOfRangeException`; ungültiger persistierter Wert → Fallback `Benutzerkonto` |
| `SelectedKontoModus` | Pflichtauswahl beim Speichern | Abbruch mit Fehlermeldung (Muster `SelectedBackmergeModus`, Z. 478–482) |
| GitHub `AgentToken` | Pflicht, wenn `SeparatesKonto` gewählt ist — geprüft gegen den effektiven Wert (Formularwert bei geladenen GitHub-Einstellungen, sonst persistierter Wert via `PluginSettingsService`); das Plugin-Feld selbst bleibt `IsRequired = false`, da nur modusabhängig nötig | Speichern bricht mit `FehlerMeldung` ab, die auf Einstellungen → Plugins → GitHub → Authentifizierung verweist (geklärter Punkt 1) |
| `ScmEventObservation` im `SeparatesKonto` | Frei stehende `@`-Erwähnung des Viewer-Logins (Trigger nur im Notifications-Modus; `scm.trigger.keyword` bleibt ausschließlich Trigger im `Benutzerkonto`); Autor/Editor = Repository-Owner (`OwnerId`/`Owner`); kein `<!-- softwareschmiede:`-Marker | Ablehnung „Keine Erwähnung des separaten Kontos" / „Textversion stammt nicht vom Repository-Owner" / „Eigene Antwort" (geklärte Punkte 4/5) |
| `ScmNotification` | Nur `Reason == "mention"`; `(Host, RepositoryNodeId)` muss zu einem aktiven, registrierten `GitRepository` auflösen | Verwerfen; Anzahl als `Diagnostic` mit `InboxDiscardDiagnosticPrefix` am Inbox-Checkpoint (geklärte Punkte 6/7), in der SCM-Ereignisübersicht sichtbar, ohne `MonitoringNotice` |
| Token (beide Credential-Keys) | Fließt ausschließlich `ICredentialStore` → `Authorization`-Header; nie URL/Query, Log, Exception-Message, Test-Artefakt | `SanitizeSensitiveOutput` maskiert beide Werte; `GitHubHttpApiClient` loggt nichts |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `scm.monitoring.accountmode` | `int` (`ScmKontoModus`) | `0` (`Benutzerkonto`) | Anwendungseinstellung; Einstellungen → Allgemein → SCM-Ereignisse; wirkt pro Poll ohne Neustart |
| `Softwareschmiede.GitHub.AgentToken` | `ICredentialStore`-Eintrag über `PluginSettingField` `AgentToken` (`Secret`) | — | PAT des separaten GitHub-Kontos; Einstellungen → Plugins → GitHub → Authentifizierung |

Keine neuen NuGet-Pakete nötig: `System.Net.Http` ist Framework-Bestandteil; `HttpClient` folgt dem `GitHubReleaseClient`-Muster.

## Seiteneffekte und Risiken

- **`IGitHubEventApiClient`-Signatur:** Einfügen von `konto` vor `ct` in `GetAgentEnvironmentAsync` bricht kompiliertechnisch drei bestehende Aufrufstellen, die alle angepasst werden: `KiAusfuehrungsService` Z. 378, `ScmDispatchFixture` Z. 75 und `ScmEventDispatchTests_AgentActions` Z. 26/52 (`It.IsAny<CancellationToken>()`-Moq-Setups matchen die neue Signatur nicht mehr).
- **Fehler-Bodies jetzt verfügbar:** `gh api` gab bei Fehlern `"{}"` als Body zurück (`IGitHubEventApiClient.cs` Z. 58–62); HTTP liefert den echten Fehler-Body. `SendEventApiAsync` klassifiziert ausschließlich über Statuscode — unverändert sicher; Methoden, die Body-Text werten (`IsNotFound`, `IsAuthenticationFailure`), werden auf Statuscodes umgestellt.
- **`url`-Semantik `ParseIssues`:** REST liefert `url` = API-URL und `html_url` = Browser-URL; `gh issue list --json url` lieferte die Browser-URL. Umstellung auf `html_url` ist zwingend, sonst landen API-URLs in der Anzeige.
- **Workflow-Runs-Feldnamen:** `gh run list --json` lieferte camelCase (`databaseId`, `headSha`, `headBranch`, `displayTitle`, `url`); REST liefert snake_case (`id`, `head_sha`, `head_branch`, `display_title`, `html_url`) im `{workflow_runs}`-Wrapper — Parser-Mapping zwingend, sonst leere Felder.
- **`GetAvailableRepositoriesAsync`-Semantik:** `GET /user/repos?affiliation=owner` bildet `gh repo list`-Default („eigene" Repos) ab; das `owner`-Feld des gh-Outputs wird von `AvailableRepository` nicht verwendet und entfällt. GHE-Einschränkung: der Endpunkt ist faktisch auf `api.github.com` fixiert — die Methode kennt keinen Zielhost (bisher `gh`-Default-Host); für GHE-Hosts bleibt die bisherige Semantik unverändert ungenau (dokumentiert, kein Regressionsschritt).
- **`HttpClient`-Timeout:** `GitHubHttpApiClient` setzt explizit `Timeout = 100s` (Default bewusst übernommen); lange `git/trees?recursive=1`-Antworten waren auch über `gh` unbeschränkt — kein Verhaltensunterschied, aber bei Bedarf nachjustierbar.
- **Kontexte ohne `Konto`:** `ProjectDetailViewModel` (Z. 945/976) und `AufgabeService` (Z. 322) erzeugen bzw. entgegennehmen `ScmRepositoryContext` ohne `Konto` — verifiziert unkritisch: Diese Kontexte erreichen kein `GetEventRepositoryAsync` (nur `ScmRequirement`-Aufbau bzw. `IsPullRequestLinkedAsync`/Anlagedaten); alle drei `GetEventRepositoryAsync`-Aufrufstellen (Monitoring Z. 79, Orchestrator Z. 130, CompletionService Z. 191) sind abgedeckt, plus der `ScmRepositoryContext` im Resolver (Z. 92, für Anlagedaten).
- **Backmerge:** Im `SeparatesKonto` läuft `EnumerateRepositoryAsync` nicht → `ScmBackmergeService.EvaluateDiscoveredReferencesAsync` erhält keine Entdeckungen; automatische Backmerges sind im Bot-Modus faktisch deaktiviert (dokumentierte Konsequenz der exklusiven Inbox-Überwachung; ggf. als Folgeanforderung klären, ob eine reine Metadaten-Enumeration für Backmerge beibehalten werden soll). Unabhängig davon: `ScmBackmergeService` ruft im `Benutzerkonto` `GetPullRequestStatusAsync`/`GetDefaultBranchAsync`/`CompletePullRequestAsync` — `GetPullRequestStatusAsync` wird prozesslos (HTTP), `GetDefaultBranchAsync` bleibt `git ls-remote` (seltener lokaler Prozess), der Admin-Merge bleibt ein seltener `gh`-Aufruf.
- **Record-Gleichheit `ScmEventRepository`:** Die neue Komponente `Konto` fließt in die Wertegleichheit ein — dieselbe physische Repository unter zwei Konten ergibt zwei Dictionary-Schlüssel (`localRepositories`, Z. 55/81). Gewollt (unterschiedliche Viewer-Identität), aber bei Moduswechsel innerhalb eines Laufs nicht mischbar.
- **Geteilte Dedup-/Checkpoint-Namensräume:** Moduswechsel erzeugt keine Doppelevents (gleiche `ContentKey`/`EventKey`); Notifications außerhalb des `since`-Fensters gehen verloren — durch die verzögerte Watermark-Commit-Regel abgesichert.
- **`ScmTaskCompletionMonitoringService`:** Liest Issues unter dem jeweils aktiven Konto — im Bot-Modus braucht das Bot-Konto Lesezugriff auf alle beobachteten Repositories; die PR-Abschlussprüfung bleibt (Token-Grenze) Benutzer-Token — das Benutzer-Token bleibt Pflichtcredential.
- **Sicherheit:** `SanitizeSensitiveOutput` muss beide Tokens maskieren — die heutige Ein-Token-Signatur (Z. 202–219) wird erweitert; der dedizierte Test-Nachweis wird auf das AgentToken ausgeweitet. `GitHubHttpApiClient` darf niemals Header/Token loggen; `HttpRequestException`-Messages enthalten maximal die URI (ohne Credentials — Tokens stehen nie in URLs). Das AgentToken folgt denselben Regeln (`GH_TOKEN`-Env für Agenten, kein Token in Prompts/Logs/Persistenz, `GIT_TERMINAL_PROMPT=0`).
- **Rest-`gh`-Konsolen:** `CreateIssueAsync`, `CreatePullRequestAsync`, `CompletePullRequestAsync` und Agenten-eigene `gh`-Aufrufe erzeugen weiterhin seltene `conhost`-Blitzer (ereignisgebunden, nicht im Poll-Takt) — bewusst akzeptierte Restmenge; vollständige Eliminierung ist eine mögliche Folgeanforderung.
- **E2E-Credential-Isolierung:** Mit dem `ScmE2ECredentialStore` verlieren Fixture-App-Schreibvorgänge auf Plugin-Einstellungsfelder ihre Wirkung auf den echten `WindowsCredentialStore` der Testmaschine; `EmptyCredentialStore` entfällt — der Fixture-`Token` wird im Plugin-Manager gesetzt. Der Store ist prozessbunden: Nach `RestartAppPreservingDatabase` sind eingetragene Secrets wieder leer — die Bot-Modus-Phase ist daher ans Ende des konsolidierten Tests gelegt (kein Neustart danach).
- **E2E-Transport-Fixture:** Konvertierte Plugin-Methoden (`GetPullRequestStatusAsync`, `CheckHealthAsync`, Listenpfade) treffen in der SCM-E2E-App nun `ScmE2EApiClient` statt des `ScmE2EGitRunner`-Verbots — neue Endpoint-Branches nötig (siehe Änderungen); `CheckHealthAsync` liefert dort jetzt `true` über `GET user`.
- **GHE/Mehrfachhosts:** Ein einziger AgentToken gilt für alle Hosts des Plugins; die `GH_ENTERPRISE_TOKEN`-Spiegelung des `gh`-Pfads entfällt — HTTP sendet den gespeicherten Token direkt an den Zielhost (gleiche Semantik); `ParseEventRepository` macht die Nicht-Ereignis-Pfade erstmals GHE-fähig (vorher implizit github.com).
- **`PluginSymbolTests`-Ctor-Wahl:** Der Reflection-Helfer nimmt den kürzesten Konstruktor — der neue `agentEventApiClient`-Parameter darf nur am längeren `GitHubPlugin`-Ctor hängen, der 3-Argument-Ctor bleibt unberührt.
- **Dokumentation:** `docs/help/plugins/github-ereignisse/` (architektur.md „GitHub-API-Aufrufe laufen über `gh`", installation.md „gh ist verfügbar", ablauf-technisch.md, business-rules.md `gh pr merge --admin`), `docs/help/plugins/installation.md` (Z. 7 „gh installiert **und eingeloggt**" — durch HTTP-Umstellung nur noch für die drei Rest-Mutationen nötig) sowie `docs/help/aufgaben/ablauf-technisch.md` (`gh api`-/`gh pr view`-/`gh run list`-Nennungen) und `docs/help/projekte/dialog-repository-auswahl.md` (`gh api git/trees`) sind mitzupflegen — Transport-Aussagen und die neue `gh`-Restgrenze aktualisieren.

## Umsetzungsreihenfolge

1. **`GitHubHttpApiClient` anlegen und als Default verdrahten**
   - Voraussetzungen: Keine (Framework-`HttpClient`; `ICredentialStore`, `ICliRunner`, `ILogger` bereits im Plugin-Ctor).
   - Beschreibung: `GitHubHttpApiClient(Func<string?> tokenProvider)` + Test-Ctor mit `HttpClient`/Handler; Base-URL-/GraphQL-Mapping, Pflicht-Header, Response-Header-Weitergabe, typisierte Transportfehler; `GitHubPlugin`-3-Argument-Ctor erstellt `GitHubHttpApiClient` statt `GhEventApiClient`; `GhEventApiClient` entfernen. Damit läuft der gesamte `IGitHubEventApiClient`-Pfad (Monitoring, Notifications später, Responder) prozesslos.

2. **Periodische Nicht-Ereignis-Pfade umstellen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `GetPullRequestStatusAsync` (GraphQL), `GetPullRequestWorkflowRunsAsync`/`RunWorkflowListAsync` (REST `actions/runs` + Objekt-Seitenhelfer + snake_case-Parser), `CheckHealthAsync` (`GET /user`). Host-Auflösung via `ParseEventRepository` (`owner/repo` → `github.com`).

3. **Übrige Lese-Pfade umstellen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `GetIssuesAsync` (PR-Filter + `html_url`), `GetAlertsAsync`, `GetIssueTemplatesAsync`/`LoadTemplateContentAsync`, `GetOpenPullRequestsAsync`, `GetAvailableRepositoriesAsync`, `GetRepositoryStructureLoadResultAsync`; `IsNotFound`/`IsAuthenticationFailure` für HTTP-Pfade auf Statuscode-Basis; `--slurp`-Nesting entfernen.

4. **`ScmKontoModus`-Enum anlegen** (`Softwareschmiede.Plugin.Contracts/Domain/Enums/ScmKontoModus.cs`)
   - Voraussetzungen: Keine.
   - Beschreibung: Enum `Benutzerkonto = 0`, `SeparatesKonto = 1` im Namespace `Softwareschmiede.Domain.Enums` (Vorlage `PullRequestProvider`).

5. **`Konto` auf `ScmEventRepository` und `ScmRepositoryContext` ergänzen**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: 10. Positions-Komponente mit Default auf `ScmEventRepository`; optionaler Konstruktorparameter/Eigenschaft auf `ScmRepositoryContext`.

6. **`ScmNotification`-Record und `IScmNotificationSource` anlegen**
   - Voraussetzungen: Schritt 4 (`ScmReferenceKind` existiert).
   - Beschreibung: Record-Felder siehe „Neue Klassen"; Interface-Doku: liefert rohe Inbox-Einträge pro Host unter dem separaten Konto.

7. **`AppEinstellungService` um `ScmKontoModusKey` + Getter/Setter erweitern**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: `scm.monitoring.accountmode`; Muster `Get-/SetScmBackmergeModusAsync`.

8. **`ScmEventValidationService` modusabhängig machen**
   - Voraussetzungen: Schritt 5.
   - Beschreibung: `GetRejection` verzweigt auf `Konto`; Erwähnungs-Check via `HasFreeStandingTrigger(body, "@" + ViewerLogin)`; neue Ablehnungs-Konstante; Owner-Autor-Regel und Marker-Regel bleiben.

9. **`GitHubPlugin`: AgentToken-Feld, zweiter HTTP-Client, Konto-Routing, `GetAgentEnvironmentAsync(konto, ct)`, `EnumerateNotificationsAsync`**
   - Voraussetzungen: Schritte 1, 5, 6, 8 (Interface-Vertrag), `IGitHubEventApiClient`/`GitHubHttpApiClient` vorhanden.
   - Beschreibung: `AgentToken`-`PluginSettingField` in `GetSettingGroups`; `CredentialKeyFor`/`ClientFor`; zweiter `GitHubHttpApiClient` mit AgentToken-Selektor (optionaler `agentEventApiClient` nur am längeren Ctor); Konto-Parameter durch private Transport-Helfer; Notification-Parsing via `RestPagesAsync`-Muster; `SanitizeSensitiveOutput` für beide Tokens.

10. **`IScmAgentAccess`-Parameter + `KiAusfuehrungsService`-Durchreichung**
    - Voraussetzungen: Schritte 4, 7, 9.
    - Beschreibung: `konto` vor `ct`; `WithScmAgentEnvironmentAsync` liest Modus über Scope; `ScmDispatchFixture` Z. 75 und `ScmEventDispatchTests_AgentActions` Z. 26/52 kompilieren wieder (Moq-Setups auf neue Signatur).

11. **`ScmEventMonitoringService`: SeparatesKonto-Pfad + `ScmEventStore`-/`ScmEventQueueViewModel`-Diagnose-Ausnahme**
    - Voraussetzungen: Schritte 6–9.
    - Beschreibung: Modus lesen; `Konto` in Alias-Auflösung; `EnumerateNotificationsAsync`-Helfer mit Inbox-Checkpoint, Verwerfungsdiagnose mit `InboxDiscardDiagnosticPrefix` und Watermark-Commit-Regel; Notification→Referenz-Mapping; `MonitoringNotice`-Ausfilterung des Präfix in `ScmEventQueueViewModel`.

12. **`ScmEventOrchestrator`, `ScmEventTaskResolver`, `ScmTaskCompletionMonitoringService` konto-fähig machen**
    - Voraussetzungen: Schritte 5, 7, 8.
    - Beschreibung: `Konto` in die erzeugten `ScmRepositoryContext`s übernehmen; Toleranzliste des Orchestrators um die Erwähnungs-Ablehnung erweitern.

13. **Settings-UI: `ScmKontoModusOptionen`, `SettingsViewModel`, `SettingsView.xaml` (inkl. Secret-Template-Marker)**
    - Voraussetzungen: Schritte 7, 9 (`AgentToken`-Feld für die Pflichtprüfung), bestehende Muster `ScmBackmergeModusOptionen`/ComboBox.
    - Beschreibung: Optionsliste, Laden/Speichern/Null-Guard, `ValidiereScmKontoModus` (effektiver AgentToken-Wert), ComboBox + Erläuterungstext, `AutomationProperties.Name` auf dem Secret-`PasswordBox`-Template.

14. **Unit- und Integrationstests**
    - Voraussetzungen: Schritte 1–13; `ScmDispatchFixture` mit `As<IScmNotificationSource>()`-Erweiterung (in diesem Schritt anzulegen).
    - Beschreibung: Neue Testklassen (`GitHubHttpApiClientTests`, `GitHubNotificationSourceTests`, `AppEinstellungServiceTests_KontoModus`, `ScmEventMonitoringServiceTests_Notifications`) und Fixture-Erweiterung gemäß „Tests"; betroffene Bestandstests anpassen (`GitHubPluginTests` auf Client-Mocks umstellen).

15. **E2E-Infrastruktur und konsolidierte Bot-Modus-Phase**
    - Voraussetzungen: Schritte 9–13; `ScmE2EApiClient`/`ScmE2EScenario`/`ScmE2EFixture`/`ScmE2EPluginManager`/`SettingsView` (E2E) vorhanden.
    - Beschreibung: `ScmE2ECredentialStore` + Registrierung, Agent-Client-Injektion mit `scm-bot`-Viewer, `GET /notifications`-Antwort + neue Endpoint-Branches im Fixture-Transport, `Notifications` im Szenario, `AddNotification`-/`SetKontoModus`-/`WaitForCallsAsync`-Hilfen, `WaitForPollAsync` parametrisiert, `SettingsView`-Helfer (`SetKontoModus`, `SetPluginSecretField` per Tastatur); Bot-Modus-Phase inkl. UI-Validierungsnegativfall mit `DiscardChanges()`-Reset, Fremd-Notification + Diagnostic-Nachweis im „Integrationshinweise"-Expander und Rückschaltung im konsolidierten SCM-E2E.

16. **Dokumentation `docs/help/` aktualisieren**
    - Voraussetzungen: Schritte 1–15.
    - Beschreibung: HTTP-Transport statt `gh`-Aufrufe (architektur.md, installation.md, ablauf-technisch.md in `github-ereignisse/` sowie `aufgaben/ablauf-technisch.md`, `projekte/dialog-repository-auswahl.md`), Kontomodus, Inbox-Pfad, `gh`-Restgrenze (drei Mutationen + Agenten-Werkzeug), Token-Regeln und geänderte Validierungsregeln nachziehen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `SendAsync_MapsGithubComToApiDotGithubDotCom` / `_EnterpriseHostMapsToApiV3` / `_GraphqlEndpointMapsToGraphqlPath` (beide Host-Arten) | `GitHubHttpApiClientTests` | Base-URL-Mapping inkl. `api.github.com`/`{host}/api/v3`/`api/graphql`-Sonderfall |
| `SendAsync_SetsRequiredHeaders_AndBearerOnlyWhenTokenPresent` / `_OmitsAuthorization_WhenNoToken` | `GitHubHttpApiClientTests` | `Accept`, `X-GitHub-Api-Version`, `User-Agent`, `Authorization: Bearer` ausschließlich bei Token — Assertion auf Request-Header des Fake-Handlers |
| `SendAsync_ForwardsResponseHeaders` (`Link`, `X-RateLimit-*`, `Retry-After`) / `_ReturnsErrorStatusAndBody` | `GitHubHttpApiClientTests` | Pagination-/Backoff-Header in `GitHubEventApiResponse.Headers`; Nicht-2xx liefert Status + echten Body statt Exception |
| `SendAsync_TransportError_ThrowsTemporary` (Timeout/`HttpRequestException`, `outcomeUncertain`-Matrix GET/POST/graphql) / `_TokenNeverAppearsInOutputs` | `GitHubHttpApiClientTests` | Typisierte Fehler mit bisheriger `outcomeUncertain`-Semantik; kein Token in Exception-Message/Response |
| `GetIssuesAsync_FiltersPullRequestEntries_UsesHtmlUrl` / `GetAlertsAsync_PaginatesViaLinkHeader` / `GetOpenPullRequestsAsync_*` / `GetIssueTemplatesAsync_*` / `GetRepositoryStructureLoadResultAsync_*` / `GetAvailableRepositoriesAsync_MapsRestFields` (Umstellung der bestehenden Fälle auf `IGitHubEventApiClient`-Mock) | `GitHubPluginTests` / `GitHubPluginTests_GetRepositoryStructureAsync` | Konvertierte Lesepfade: Endpoint-Mapping, Feld-Mapping (snake_case/`html_url`), Fehlervertrag (`[]`/`Failed`) |
| `GetPullRequestStatusAsync_UsesGraphqlQuery` / `_FailureThrowsInvalidOperation` | `GitHubPluginTests` | GraphQL-Feldliste ↔ `ParsePullRequestStatus`; Fehlervertrag |
| `GetPullRequestWorkflowRunsAsync_HeadShaFilterMapsToQueryParam` / `_ParsesSnakeCaseRuns` / `_DistinguishesDisplayTitleByWorkflowName` | `GitHubPluginTests` | `head_sha`-Filter, `workflow_runs`-Wrapper, Feld-Mapping |
| `CheckHealthAsync_TrueOn2xx` / `_FalseOn401` | `GitHubPluginTests` | `GET /user`-Ersatz für `gh auth status` |
| `Notice_DiscardDiagnostic_DoesNotTriggerMonitoringNotice` | `ScmEventQueueViewModelTests` | `InboxDiscardDiagnosticPrefix`-Diagnostics erscheinen in `Diagnostics`, lösen aber kein `MonitoringNotice` aus |
| `GetScmKontoModusAsync_WithoutEntry_DefaultsBenutzerkonto` / `_RoundtripsPersistedValue` / `_InvalidValue_FallsBackToDefault` / `SetScmKontoModusAsync_UndefinedValue_Throws` | `AppEinstellungServiceTests_KontoModus` | Default, Persistenz, Fehlertoleranz, `Enum.IsDefined`-Guard (Muster `AppEinstellungServiceTests_Backmerge`) |
| `SeparatesKonto_RequiresFreeStandingMention` / `_QuotedMention_DoesNotTrigger` / `_MentionAuthorMustBeOwner` / `_ViewerNeedNotBeOwner` / `_OwnMarker_StillRejected` | `ScmEventValidationServiceTests` | Bot-Regeln: Erwähnung frei stehend; Owner-Autor-Regel greift; Viewer-==Owner-Regel entfällt; Marker-Regel bleibt |
| `Notifications_ParseThreadsToScmNotification` / `_SkipsNonIssuePrSubjects` / `_SinceAppendedToEndpoint` / `_PaginatesAllPages` / `_Failures_AreTypedAndSanitized` (inkl. Assertion auf Maskierung des **AgentToken**, nicht nur des Benutzer-Tokens) / `_AgentToken_IsMaskedInErrorOutput` / `_MissingAgentToken_ThrowsAuthentication` / `_UsesAgentClient_NotUserClient` | `GitHubNotificationSourceTests` | Thread→`ScmNotification`-Mapping, Subject-Filter, `since`-Parameter, Pagination, typisierte Fehler, Client-Auswahl (zwei injizierte `IGitHubEventApiClient`-Mocks), Sanitizing beider Tokens |
| `GetEventRepositoryAsync_SeparatesKonto_UsesAgentToken` / `ReadEventsAsync_RoutesByReferenceKonto` / `Response_RoutesByReferenceKonto` | `GitHubNotificationSourceTests` oder `GitHubEventSourceTests` | Konto-Routing aller `IScmEventSource`/`IScmEventResponder`-Methoden an den richtigen Client |
| `GetAgentEnvironmentAsync_SeparatesKonto_UsesAgentToken` / `_Benutzerkonto_UsesToken` | `GitHubPluginTests` | Credential-Auswahl je Modus (Muster `GetAgentEnvironmentAsync_WithToken_*`) — Signatur nun `(konto, ct)` |
| `GetSettingGroups_ContainsAgentTokenSecret` | `GitHubPluginTests` | `AgentToken`-Feld vom Typ `Secret` in Gruppe „Authentifizierung" |
| `LadenCommand_LoadsKontoModusDefault` / `SpeichernCommand_PersistsKontoModus` / `Speichern_SeparatesKontoOhneAgentToken_BrichtAb` | `SettingsViewModelTests` | Default-Anzeige, Persistenz, modusabhängige Pflichtvalidierung |
| `As<IScmNotificationSource>()`-Default-Setup + `AddNotification`-Hilfe | `ScmDispatchFixture` (Helper) | Gemockter Notification-Feed (leere Liste als Default) |
| `Monitor_SeparatesKonto_MentionNotification_CreatesTaskAndEvent` / `_ForeignRepoNotification_IsDiscardedWithDiagnostic` (inkl. `InboxDiscardDiagnosticPrefix` im Checkpoint) / `_NonMentionReason_IsIgnored` / `_NotificationReadFailure_RetriesViaWatermark` / `_ModeSwitchBackToBenutzerkonto_ResumesEnumeration` | `ScmEventMonitoringServiceTests_Notifications` | End-to-end des Host-Pfads mit echter SQLite-Inbox: Notification→Referenz→Task-Auflösung→Accept→Dispatch; Fremd-Repo-Filter + Diagnostic; Reason-Filter; Watermark-/Backoff-Semantik; Moduswechsel pro Poll |
| `Dispatch_SeparatesKontoEvent_RevalidatesUnderAgentKonto` | `ScmEventMonitoringServiceTests_Notifications` oder `ScmEventDispatchTests*` | Orchestrator-Revalidierung nutzt `Konto` der Observation |
| `CompletionService_PassesActiveKonto_ToRepositoryContext` (optional, Plan-Check-Hinweis) | `ScmTaskCompletionMonitoringServiceTests` | `context.Konto` im `IScmEventSource`-Mock des Completion-Pfads; analog `konto`-Durchlauf in `KiAusfuehrungsService` |
| `SetKontoModus`/`GetKontoModus`/`WaitForKontoModus`/`SetPluginSecretField` | `SettingsView` (E2E-View) | Bedienung der `Konto-Modus`-ComboBox und von Secret-Pluginfeldern per FlaUI (Focus+Tastatur) |
| `AddNotification`/`SetKontoModus`/`WaitForCallsAsync` | `ScmE2EFixture` | Notification-Feed, DB-gestützte Modusumschaltung, endpoint-basiertes Warten |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `GitHubPluginTests` (konvertierte Methoden: `GetIssuesAsync_*`, `GetAlertsAsync_*`, `GetIssueTemplatesAsync_*`, `GetOpenPullRequestsAsync_*`, `GetPullRequestStatusAsync`/`GetPullRequestWorkflowRunsAsync`-Fälle, `CheckHealthAsync_*`, `GetAvailableRepositoriesAsync`-Fälle) | `ICliRunner`-Mocks auf `"gh"`-Aufrufe entfallen → `IGitHubEventApiClient`-Mock über den 4-Argument-Ctor injizieren; Fixture-JSON auf REST-Form umstellen (`html_url`, snake_case, `workflow_runs`-Wrapper); `url`-Assertionen anpassen |
| `GitHubPluginTests_GetRepositoryStructureAsync` | `gh api git/trees`-Mocks → Client-Mock; `git ls-remote`-Mocks (Default-Branch) bleiben |
| `GitHubPluginTests` (verbleibende `gh`-Fälle: `CreateIssueAsync`, `CreatePullRequestAsync`, `CompletePullRequestAsync`) | Bleiben auf `ICliRunner`-Mocks — keine Änderung, aber Regression prüfen, dass `GetPullRequestStatusAsync`-Folgeaufrufe über den Client laufen |
| `ScmDispatchFixture` (`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`) | `IScmAgentAccess.GetAgentEnvironmentAsync`-Moq-Setup muss die neue `(konto, ct)`-Signatur matchen (Z. 75); `As<IScmNotificationSource>()`-Default ergänzen |
| `ScmEventDispatchTests_AgentActions` (`src/Softwareschmiede.IntegrationTests/Services/`, Z. 26 und Z. 52) | Beide `GetAgentEnvironmentAsync`-Moq-Setups (`It.IsAny<CancellationToken>()`) brechen kompiliertechnisch — auf `(konto, ct)` umstellen |
| `SettingsViewModelTests` (`LadenCommand_LoadsScmMonitoringDefaults`, `SpeichernCommand_PersistsScmMonitoringSettings`) | Neue gebundene Eigenschaft `SelectedKontoModus` laden/persistieren mitprüfen; Pflichtfeld-Kette verlängert |
| `GitHubPluginTests` (`GetAgentEnvironmentAsync_*` u. a.) | Signatur mit `konto`-Parameter; zusätzlicher optionaler Konstruktorparameter (`agentEventApiClient`) |
| `ScmEventQueueViewModelTests` | `MonitoringNotice`-Verhalten bei `InboxDiscardDiagnosticPrefix`-Diagnostics |
| `ScmEventMonitoringServiceTests_*` (alle Integration-Dateien) | Kompilierung gegen geänderte Fixture; Verhalten im Default-Modus muss unverändert bleiben (Regression) |
| `ScmEventValidationServiceTests` (Bestand) | Bestehende Fälle laufen unter `Benutzerkonto`-Default — keine inhaltliche Änderung, aber neue Fälle kommen in dieselbe Klasse |
| `GitHubEventSourceTests` | Plugin-Konstruktor-Aufrufe bleiben kompatibel; ggf. gemeinsame Aufbauhelfer für zweiten Client; `ScmRepositoryContext`-/`ScmEventRepository`-Konstruktionen bleiben quellkompatibel (Default-Parameter). **`GhTransport_UsesArgumentListAndJsonFile_NotBodyOrTokenArguments` (Z. ~367–390) instanziiert `GhEventApiClient` direkt → entfällt/ersetzen durch `GitHubHttpApiClient`-Fälle** |
| `ScmE2EFixture.WaitForPollAsync` | Feste Erwartung „+8 Aufrufe/Poll" (Z. 149–159) gilt nur für den Benutzerkonto-Pfad — Parametrisierung für Bot-Aufrufmuster erforderlich |
| `ScmE2EApiClient`-Konstruktion in `ScmE2EPluginManager` | Zweite Client-Instanz mit Bot-Viewer (`scm-bot`/`BOT`) als Agent-Client |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Modusauswahl über die echte Einstellungs-UI: `Konto-Modus`-ComboBox wählen → Fehlerfall „Separates Konto ohne AgentToken" mit sichtbarer `FehlerMeldung` → `DiscardChanges()`-Reset → AgentToken im Plugins-Tab per Tastatur eintragen → gemeinsam speichern → „Einstellungen gespeichert." | `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs` (`ScmEventsE2ETests.ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline`), Bot-Modus-Phase auf derselben App-Instanz (Muster `SetUpdateMode`/`SaveSettings`/`WaitForSettingsSaved` aus `E2E_UpdateSettings.cs`; Fehlerfall-Muster `Assert.Throws<InvalidOperationException>` auf `SaveSettings` aus `E2E_PluginAktivierung.cs` Z. 71) | „Auswahl des Kontomodus unter Einstellungen → Allgemein → SCM-Ereignisse"; „Pflichtfeld-Validierung (AgentToken nötig bei `SeparatesKonto`)" — Requirement-Bedingung „neue Settings-Marker" ist mit `AutomationProperties.Name="Konto-Modus"` erfüllt | Neuer, für Anwender sichtbarer UI-Flow inkl. auslösbarem Fehlerfall; `SettingsViewModelTests` prüfen nur ViewModel-Logik, nicht die echte Bedienung der ComboBox/das Sichtbarwerden der Fehlermeldung |
| Pflicht | SeparatesKonto-Pipeline: `mention`-Notification einspeisen (`AddNotification` + `@scm-bot`-Text) → Referenz → Aufgabenanlage → Ereignis aufgenommen → Dispatch/Zustellung → Empfangsreaktion; Bot-Identität des Agent-Clients (`scm-bot`) beweist das Konto-Routing | Dieselbe Methode/Phase | „Aufgabe wird durch `@mention` des separaten Kontos zugewiesen"; „Modus wirkt ohne Neustart" | Einziger Test, der den echten Gesamtpfad (Einstellung → Poll → Notification-Feed → Inbox → Dispatch → Antwort) in der laufenden App nachweist; Unit-/Integrationstests ersetzen keinen echten Moduswechsel |
| Pflicht | Fremd-Repo-Notification (`repositoryNodeId` ≠ `REPO`) wird verworfen: kein Inbox-Eintrag, Verwerfungsdiagnose am `notifications/…`-Checkpoint sichtbar in `ScmEventDiagnostics` (Expander „Integrationshinweise" wird per UI aufgeklappt) **ohne** `MonitoringNotice`; non-`mention`-Notification wird ignoriert; Rückschaltung auf `Benutzerkonto` via `SetKontoModus` → Keyword-Pfad läuft wieder | Dieselbe Methode/Phase | „Nur registrierte, aktivierte `GitRepository`s werden verarbeitet — Fremd-Notifications werden nicht verarbeitet" (inkl. geklärter Frage 7: Diagnostic sichtbar); „`Benutzerkonto` = bisheriges Verhalten"; Reason-Filter (geklärte Frage 6) | Die Verwerfungsdiagnose ist ein über die UI beobachtbares Verhalten; der Integrationstest deckt den Host-Pfad ab, nicht die Sichtbarkeit/Alarmfreiheit in der laufenden App |

Neue E2E-Infrastruktur (Hilfsarbeiten, keine eigenen Testszenarien): `ScmE2EScenario.Notifications` + `ScmE2ENotification`, `ScmE2EApiClient`-Antwort für `GET /notifications` + parametrierte Viewer-Identität (`scm-bot`/`BOT` für den Agent-Client) + statischer `calls.jsonl`-Lock + neue Endpoint-Branches (`actions/runs`, PR-Status-GraphQL) für die HTTP-umgestellten Methoden, `ScmE2ECredentialStore` + DI-Registrierung unter `scmTestConfiguration` (ersetzt `EmptyCredentialStore`; `Token` wird im Plugin-Manager gesetzt, `AgentToken` startet leer), `ScmE2EFixture.AddNotification`/`SetKontoModus`/`WaitForCallsAsync`, `WaitForPollAsync`-Parametrisierung, `SettingsView`-Helfer (`SetKontoModus`, `SetPluginSecretField` per Focus+Tastatur), `AutomationProperties.Name` am Secret-`PasswordBox`-Template.

Zur Transportumstellung ist **kein** eigenes E2E-Szenario erforderlich: Sie ändert keinen über UI oder Nutzeraktion erreichbaren Ablauf — dieselben `IGitHubEventApiClient`-Aufrufe laufen, nur ohne Kindprozess. Das Aufblitzen eines `conhost`-Fensters ist über UI-Automatisierung nicht beobachtbar; der Funktionspfad ist durch `GitHubHttpApiClientTests` (Header/Mapping/Fehler), die Plugin-Tests und den bestehenden konsolidierten SCM-E2E (echte Pipeline gegen den Fixture-Client) abgedeckt.

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ScmEventsE2ETests.ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` | Erweiterung um die Bot-Modus-Phase (UI-Moduswechsel + Notification-Feed + Fremd-Notification + Rückschaltung); zugleich im Ausgangslauf dokumentiert fehlgeschlagen (Timeout `Eingabebereitschaft unbekannt` in `MissingVariantsAsync`, `inventory/tests.md`) — vor Erweiterung Ursache klären/stabilisieren, siehe `CLAUDE.md`-Hinweis zu `WaitForElement`-Timeouts |

## Offene Punkte

Alle neun ursprünglichen Rückfragen wurden vom Anwender beantwortet; zusätzlich sind die Klärungspunkte der nachträglich ergänzten HTTP-Transport-Anforderung (Base-URL-Mapping, Pflicht-Header, `gh auth status`-Ersatz, Umfang der Umstellung, `gh`-Restvoraussetzung, gemeinsame Client-Implementierung beider Konten, Token-Schutz) als Designentscheidungen eingearbeitet. Zur Nachverfolgbarkeit sind die gegebenen Antworten hier dokumentiert:

| # | Punkt | Antwort des Anwenders |
|---|-------|------------------------|
| 1 | Authentifizierung des separaten Kontos (zweiter PAT vs. `gh auth`-Mehrfachkonten; Token-Scopes) | Wie vorgeschlagen übernommen: zweiter PAT als `PluginSettingField` `AgentToken` (`Secret`) → Credential-Key `Softwareschmiede.GitHub.AgentToken`; kein `gh auth`-Mehrfachkonto (die App authentifiziert ausschließlich über `GH_TOKEN`-Umgebungsvariablen). Scopes: `notifications` + `repo` (Lesen der Referenzinhalte und Antwortaktionen). |
| 2 | Repository-Zugriff des Bot-Kontos | Wie vorgeschlagen übernommen: Bot-Konto als Collaborator mit Schreibrecht einladen; der vorhandene `CanWrite`-Check greift automatisch, weil im Bot-Modus `Viewer*` = Bot-Identität ist. |
| 3 | Welches Token wirkt wo (Inbox, Responder, `GetAgentEnvironmentAsync`) | Wie vorgeschlagen übernommen: konsistent das AgentToken für alle SCM-Zugriffe im `SeparatesKonto` (Inbox, Repository-Auflösung, Reads, `IScmEventResponder`, Agenten-`GH_TOKEN`); im `Benutzerkonto` unverändert das Benutzer-Token. Der `Konto`-Selektor auf `ScmEventRepository` macht die Wahl je Aufruf explizit und umstellbar. Präzisierung durch HTTP-Umstellung/Token-Grenze: die Enumeration gilt für Ereignis-Träger; Nicht-Ereignis-APIs (`GetPullRequestStatusAsync`, Workflow-Runs, Listenpfade, `CheckHealthAsync`) laufen immer unter dem Benutzer-Token. |
| 4 | Trigger-Keyword vs. `@mention` | Es wird unterschieden: im Modus `Benutzerkonto` bleibt die Prüfung auf das konfigurierte Trigger-Keyword (`scm.trigger.keyword`) exakt wie bisher; im Modus `SeparatesKonto` (Überwachung der Notifications-Inbox) triggert stattdessen die `@mention` des separaten Kontos. Die `@mention` ersetzt das Keyword also **nicht** global, sondern ist der Trigger nur im Notifications-Modus. |
| 5 | Berechtigung zum Zuweisen | Wie vorgeschlagen übernommen: Owner-Regel beibehalten — nur Erwähnungen durch den Repository-Owner (Autor/Editor-Identität gegen `OwnerId`/`Owner`) lösen aus, gleiche Sicherheitsstufe wie bisher, ohne neue Berechtigungsabfrage. |
| 6 | Notification-Typen und Read-Status | Wie vorgeschlagen übernommen: nur `reason == "mention"`; Mark-as-read/Done via API nicht implementieren — Deduplizierung ausschließlich lokal über Checkpoints/Snapshots/`EventKey`. |
| 7 | Nicht-Projekt-Repositories | Wie vorgeschlagen übernommen: still verwerfen, aber die Anzahl verarbeiteter/verworfener Fremd-Notifications als `Diagnostic` am `notifications/{pluginPrefix}/{host}`-Checkpoint sichtbar machen — mit `InboxDiscardDiagnosticPrefix`, damit der Routine-Zähler kein `MonitoringNotice`-Fehlalarm wird (Plan-Check-Hinweis eingearbeitet). |
| 8 | Umschalten des Modus | Wie vorgeschlagen übernommen: exklusiv pro Poll-Zyklus, kein Parallelbetrieb; geteilte Checkpoints/Snapshots machen den Wechsel verlustfrei; bereits aufgenommene Ereignisse behalten ihr `Konto` aus `ObservationJson` für die Revalidierung. |
| 9 | Andere SCM-Provider | Wie vorgeschlagen übernommen: vorerst nur GitHub; `IScmNotificationSource` ist optionale Fähigkeit — Bitbucket/LocalDirectory bleiben ohne Implementierung und damit im `SeparatesKonto` wirkungslos (keine Inbox). |
