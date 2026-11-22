# Logik — Die GitHub-Token-Kette Kettenglied für Kettenglied

Anforderungsgegenstand: Verträglichkeit der gesamten Kette mit ~520-stelligen
`ghs_`-Installationstokens (JWT-Struktur, Zeichensatz `ghs_[A-Za-z0-9.\-_]{36,}`).
Nachfolgend der Codebestand je Glied — nur Fakten.

## 1. Einstellungs-UI (Token-Eingabe)

- `src/Softwareschmiede.App/Views/SettingsView.xaml` — `SecretTemplate` des
  `PluginSettingFieldTemplateSelector` (Zeilen 17–26): `PasswordBox` mit
  `Tag="{Binding}"`, `AutomationProperties.Name="{Binding Field.Key}"`,
  `Loaded="OnPasswordBoxLoaded"`, `PasswordChanged="OnPasswordChanged"`.
  **Kein `MaxLength`, keine Formatvalidierung.**
- `src/Softwareschmiede.App/Views/PluginSettingEntryEditHelper.cs` —
  `OnPasswordBoxLoaded` (Zeilen 12–16) initialisiert `pb.Password` aus `entry.Value`;
  `OnPasswordChanged` (Zeilen 19–23) übernimmt `pb.Password` unverändert nach
  `entry.Value`. WPF-`PasswordBox.Password` ist ein `string` ohne Längenlimit.
- `src/Softwareschmiede.App/Views/SettingsView.xaml.cs` — delegiert die Handler an
  `PluginSettingEntryEditHelper` (Zeilen 86–90).
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` —
  `SaveCommand` = `AsyncRelayCommand(SaveAsync)` (Zeile 533);
  `SavePluginSettingsAsync` (Zeilen 986–1006) ruft je Eintrag
  `_pluginSettingsService.SetValue(plugin, entry.Field, entry.Value)` auf (Zeile 1003);
  `LoadPluginSettings` (Zeilen 961–980) lädt via `GetValue` (Zeile 968).
  `ValidateScmAccountMode` (Zeilen 1022–1048) erzwingt bei Modus `SeparateAccount` ein
  nicht-leeres `AgentToken` — einzige tokenbezogene Validierung (nur Leerheit, kein Format).
- `PluginSettingEntry.Value` ist `string` ohne Transformation
  (`src/Softwareschmiede.App/ViewModels/PluginSettingEntry.cs` Zeilen 18–29).

## 2. Einstellungs-Persistenz

- `src/Softwareschmiede/Application/Services/PluginSettingsService.cs` —
  `SetValue`/`GetValue`/`DeleteValue`/`HasValue` (Zeilen 33–63): reine Durchleitung an
  `ICredentialStore` mit `BuildKey` = `{plugin.PluginPrefix}.{field.Key}` (Zeilen 65–66).
  Keine Validierung, keine Längenbegrenzung.
- `src/Softwareschmiede/Infrastructure/Services/WindowsCredentialStore.cs` —
  `SetCredential` (Zeilen 60–83): `Encoding.Unicode.GetBytes(value)` (UTF-16),
  `CredWriteW` mit `CRED_TYPE_GENERIC` (1) + `CredPersistLocalMachine` (2);
  bei `false`-Rückgabe `InvalidOperationException` mit `Marshal.GetLastWin32Error()`
  (Zeilen 75–77). **Die Plattformgrenze `CRED_MAX_CREDENTIAL_BLOB_SIZE` (2560 Byte ≈
  1280 UTF-16-Zeichen) wird nicht vorab geprüft** — ~520 Zeichen ≈ 1040 Byte lägen
  rechnerisch darunter. `GetCredential` (Zeilen 44–57) liest via `CredReadW` und
  `PtrToStringUni(blob, size/2)` — längengetrieben, kein Puffer-Fix.
- `src/Softwareschmiede/Domain/Entities/PluginConfiguration.cs` — persistiert nur
  `CredentialStoreKey` (Zeile 21), kein Token in der DB (siehe
  [models.md](models.md)).

## 3. Credential-Schlüssel und Felder des GitHub-Plugins

`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`:

- Konstanten (Zeilen 22–23): `GitHubTokenCredentialKey = "Softwareschmiede.GitHub.Token"`,
  `GitHubAgentTokenCredentialKey = "Softwareschmiede.GitHub.AgentToken"`.
- `PluginPrefix` = `Softwareschmiede.GitHub` (Zeile 54) → `BuildKey` ergibt genau diese
  Schlüssel.
- Secret-Felder `Token`/`AgentToken` (Zeilen 67–80, siehe [models.md](models.md)).
- `ClientFor` (`GitHubPlugin.Events.cs` Zeilen 448–455): Modus `SeparateAccount` ohne
  gespeichertes `AgentToken` → `ScmProviderException(Authentication)` mit Hinweistext;
  sonst `_agentEventApiClient` (SeparatesKonto) bzw. `_eventApiClient` (Benutzerkonto).
- `CredentialKeyFor` (Zeilen 459–460): `SeparateAccount` → AgentToken-Key, sonst Token-Key.
- Konstruktor (Zeilen 156–174): baut zwei `GitHubHttpApiClient`-Instanzen mit
  Token-Providern über die beiden Credential-Keys (Zeilen 170–171) und ruft
  `RemoveLegacyNetrcEntries()` auf (Zeile 173).

## 4. HTTP-Transport (`GitHubHttpApiClient`)

`plugins/Softwareschmiede.Plugin.GitHub/GitHubHttpApiClient.cs`:

- `SendAsync` (Zeilen 37–64): `AuthenticationHeaderValue("Bearer", token)` bei
  nicht-leerem Token (Zeile 47); Pflicht-Header `Accept: application/vnd.github+json`,
  `X-GitHub-Api-Version: 2022-11-28`, `UserAgent: Softwareschmiede`; keine
  Längen-/Formatprüfung des Tokens. `AuthenticationHeaderValue` validiert den
  Parameter lediglich gegen ungültige Header-Zeichen — der JWT-Zeichensatz
  (`A–Z a–z 0–9 . - _`) ist unbedenklich.
- `UploadAsync` (Zeilen 67–107): Bearer-Header Zeile 83; Host-Check nur `github.com`
  (Zeile 71); `MaxUploadBytes` = 10 MiB (Zeile 34).
- `ValidateHost` (Zeilen 123–134): DNS-Host ohne UserInfo/Port/Pfad/Query/Fragment —
  Token-Grenze gegen fremde Origins.
- `BuildRequestUri` (Zeilen 109–118): `github.com` → `api.github.com` / `/graphql`,
  GHE-Hosts → `{host}/api/v3` bzw. `{host}/api/graphql`.
- Fehlerbehandlung: `HttpRequestException`/Timeout →
  `ScmProviderException(Temporary)` (Zeilen 59–63, 97–106).
- Geteilter statischer `HttpClient` mit 100-s-Timeout (Zeile 13);
  Token-Provider wird pro Request ausgewertet (Zeilen 41, 82).

## 5. `gh`-CLI-Umgebung

`GitHubPlugin.GetGhEnvironment` (`GitHubPlugin.cs` Zeilen 176–185): setzt
`GH_TOKEN` = Credential des Token-Keys, sonst leere Map.
Verwendung:

- `CreateIssueAsync` (Zeilen 777–787): `gh issue create --repo … --title … --body …`.
- `CreatePullRequestAsync` (Zeilen 1284–1289): `gh pr create --repo … --head … --title … --body … [--base …]`.
- `CompletePullRequestAsync` (Zeile 1441): `gh pr merge … [--merge|--rebase|--squash] [--auto] [--admin]` bzw. `gh pr review … --approve`.
- `MarkPullRequestReadyAsync` (Zeilen 1476–1478): `gh pr ready …`.
- Fehlerauswertung über `IsAuthenticationFailure` (Zeilen 465–476),
  `IsBranchProtectionFailure` (1690–1698), `IsNoCommitsBetweenFailure` (501–508) —
  alle stringbasiert auf `StdErr`, token-unabhängig.

## 6. git-Umgebung und `http.extraheader`

`GitHubPlugin.cs`:

- `GetGitEnvironment` (Zeilen 271–292): `GIT_TERMINAL_PROMPT=0` immer; bei vorhandenem
  Token `GH_TOKEN` + `AddGitHubHttpAuthHeader`.
- `AddGitHubHttpAuthHeader` (Zeilen 223–235): `GIT_CONFIG_COUNT=1`,
  `GIT_CONFIG_KEY_0 = http.https://<host>[:port]/.extraheader`,
  `GIT_CONFIG_VALUE_0 = "AUTHORIZATION: basic " + Base64("x-access-token:" + token)`.
  Bei ~520 Zeichen Token entsteht ein ~720–740 Zeichen langer Wert
  (Base64-Aufblähung ×4/3) — als `ProcessStartInfo.Environment`-Wert, nicht als
  CLI-Argument.
- `TryBuildHttpExtraHeaderKey` (Zeilen 248–269): SSH-/SCP-URLs vorab via
  `ToHttpsRepositoryUrl` normalisiert; nur absolute HTTPS-URIs mit Host liefern einen Key
  (fail-closed); Host als `Uri.IdnHost`, IPv6 mit Klammern, Nicht-Default-Port angehängt.
- Verbraucher von `GetGitEnvironment`: `CloneRepositoryAsync` (1185),
  `ConfigureGitCredentialsAsync` (638, `git config user.name/email` +
  `remote set-url origin` ohne Token-Einbettung), `PushBranchAsync` (1220),
  `PullAsync` (1246), `GetRemoteBranchesAsync` (1727, `git ls-remote --heads`),
  `GetDefaultBranchAsync` (1785, `git ls-remote --symref … HEAD`),
  `CheckoutPullRequestSourceAsync` (1840 → `CheckoutPullRequestSourceCoreAsync` in
  `GitPluginBase` Zeilen 189–222: `git fetch --no-tags <remote> <ref>`,
  `checkout -B`, `rev-parse HEAD`).
- `CloneRepositoryAsync` (Zeilen 1161–1202): SSH→HTTPS-Normalisierung (1167–1169),
  Token-Pflicht bei HTTPS (1173–1177), danach `ConfigureGitCredentialsAsync`.
- `NormalizeRemoteUrlAsync` (Zeilen 522–579): liest `remote.origin.url`, entfernt
  `oauth2:…@`-Einbettungen via `EmbeddedTokenPattern` (Zeile 544), normalisiert
  SSH→HTTPS, schreibt Abweichungen per `remote set-url` zurück.
- `RemoveInsecureSshCommandAsync` (Zeilen 589–627): entfernt exakt den legacy
  `core.sshCommand`-Wert `LegacyInsecureSshCommand` (Zeile 35) per `git config --unset`.
- `GitPluginBase.ToHttpsRepositoryUrl`
  (`src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`
  Zeilen 271–323) + `ScpSshUrlPattern` (Zeile 12): HTTPS/SCP/`ssh://`-Normalisierung.
- `GitPluginBase.ResolvePullRequestFetchRemote` (Zeilen 232–238): Same-Repo → `origin`,
  Fork → `SourceRepositoryUrl`.

## 7. Prozessstart (`CliRunner`)

`src/Softwareschmiede/Infrastructure/Services/CliRunner.cs` — `CreateProcess`
(Zeilen 168–200): Tokens ausschließlich via `ProcessStartInfo.Environment`
(Zeilen 193–197: `startInfo.Environment[key] = value`), niemals in `ArgumentList`
(Zeilen 188–190). `ResolveExecutablePath` (Zeilen 208–283) sucht PATH + bekannte
Installationsorte (u. a. „GitHub CLI"-Standardpfade). `.NET`-Prozessumgebung hat kein
praktisches Längenlimit je Variable; Windows-Umgebungsblock-Grenze (~32k Zeichen je
Prozess) wird von 3 zusätzlichen Variablen bei Weitem nicht erreicht.

## 8. Agenten-Anbindung

- `IScmAgentAccess`
  (`src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmAgentAccess.cs`):
  `GetAgentEnvironmentAsync(account, repositoryUrl, ct)` — Vertrag: Werte können
  Geheimnisse enthalten, gehören ausschließlich in die Prozessumgebung
  (Zeilen 12–25).
- `GitHubPlugin.GetAgentEnvironmentAsync` (`GitHubPlugin.cs` Zeilen 198–213):
  `GIT_TERMINAL_PROMPT=0` immer; bei Token (Key via `CredentialKeyFor(account)`)
  `GH_TOKEN` + `AddGitHubHttpAuthHeader` mit `repositoryUrl`.
- `AiExecutionService.WithScmAgentEnvironmentAsync`
  (`src/Softwareschmiede/Application/Services/AiExecutionService.cs` Zeilen 452–484):
  ruft `agentAccess.GetAgentEnvironmentAsync` mit dem Modus aus
  `AppSettingService.GetScmAccountModeAsync` (Zeile 465); merged die Werte in
  `spec.EnvironmentVariables` (Zeilen 478–483); Fehler → Warnung, Start läuft ohne
  Anbindung weiter (Zeilen 467–471). Aufrufstelle Zeile 314 in
  `StartTerminalSessionCoreAsync`. Bewusst **nicht** Teil des
  `ScmCliAccessEvaluator`-Fingerprints (Doc Zeilen 447–450).
- `AiExecutionService` Zeile 356: `aiPlugin.CheckHealthAsync` als Health-Probe des
  **KI**-Plugins (nicht des Git-Plugins).

## 9. Ereignispfade — Auflösung, Viewer, CanWrite

`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`:

- `GetEventRepositoryAsync` (Zeilen 57–72): kanonischer Cache-Key
  `{clientHash}|{host}|{owner}|{name}` (Zeilen 133–135); Primärtransport GraphQL,
  REST-Fallback nur bei `Temporary`/`RateLimit` (Zeilen 67–69); nur Erfolge werden
  TTL-gecacht (`_resolutionTtl`, Default 15 min via `ScmResolutionCacheOptions`).
- `ResolveEventRepositoryGraphQlAsync` (Zeilen 88–107): eine Abfrage
  `viewer{login id}` + `repository{… viewerPermission}`; `CanWrite` =
  `viewerPermission ∈ {ADMIN, MAINTAIN, WRITE}` (Zeile 105); `ViewerId`/`ViewerLogin`
  aus `viewer` — bei Installationstoken Bot-Identität `<app-slug>[bot]`.
- `ResolveEventRepositoryRestAsync` (Zeilen 112–127): Fallback `GET user` +
  `GET repos/{owner}/{name}`; `CanWrite` aus `permissions.push|admin|maintain`
  (Zeile 125). **`GET user` liefert für Installationstokens 403** →
  `ScmProviderException(Permission)` via `EvaluateResponse` — der Fallback ist mit
  `ghs_` funktional nicht verwendbar.
- `ReadEventsAsync` (151–212), `EnumerateReferencesAsync` (215–233),
  `ReadIssueAsync` (236–255), `ReadPullRequestAsync` (258–268),
  `EnumerateNotificationsAsync` (271–300, `notifications`-Endpunkt, Modus
  `SeparateAccount`), `CollectReferencesAsync` (303–331), `RestPagesAsync`
  (367–400), `EventApiAsync`/`GraphAsync` (402–438).
- `GraphAsync` Fehlermapping (Zeilen 413–433): `RATE_LIMITED`→RateLimit+Gate,
  `NOT_FOUND`→Permanent, `FORBIDDEN`→Permission, `UNAUTHORIZED`→Authentication,
  sonst Temporary; Eviction wie auf HTTP-Ebene (422–423).
- `SendEventApiAsync` (472–497): Rate-Limit-Gate vor jedem Call
  (`_rateLimitGates` Zeilen 474–477); `OnTransportFailure` (504–513) —
  `Authentication` → `EvictResolutions(client, null)` (alle Einträge des Kontos),
  `Permission` → `EvictResolutions(client, host)`.
- `EvaluateResponse` (524–544): 429 oder 403+RateLimit-Header → `RateLimit`;
  401 → `Authentication`; 403 → `Permission`; ≥500 → `Temporary`; sonst `Permanent`.
- `EventFailureMessage` (546–551): PAT-zentrierte Hinweistexte
  („Personal Access Token erneuern" / „…benötigt Zugriff … Lese- und Schreibrechte").
- `EvictResolutions` (141–148), `ArmRateLimitGate` (518–519),
  `RateLimitLockFallbackDuration` = 60 s (Zeile 31),
  `SinceSafetyMargin` = 2 h (Zeile 39).
- `IsViewer` (613–615): `id`-Vergleich ordinal, Login case-insensitive.
- `ValidateReference` (596–604): `ViewerId` Pflicht; `ValidateSegment` (606–610)
  erlaubt `[A-Za-z0-9_.-]+` für Owner/Name.
- `ParseEventRepository` (567–594): Host/Owner/Name aus HTTPS-, SCP- oder
  `ssh://`-Alias; `github.com`-Default.
- Token-Grenze (Doc `ClientFor` Zeilen 440–447): Konto-Routing gilt **nur** für die
  Ereignispfade; Nicht-Ereignis-APIs und gh/git laufen unter dem Benutzer-Token.

## 10. Ereignis-Antworten — Viewer-/CanWrite-Gates

`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.EventResponses.cs`:

- `ExecuteResponseAsync` (Zeilen 43–89): frische Auflösung
  (`GetEventRepositoryAsync`, Zeilen 49–50); `ViewerId`-Ungleichheit oder
  `RepositoryId`-Mismatch → `Permission` „Repository- oder Benutzeridentität hat sich
  geändert." (Zeile 51–52); `!CanWrite` → `Permission` „GitHub-Schreibberechtigung
  fehlt." (Zeile 53); danach Marker-Reconcile + REST-/GraphQL-Mutation.
- `ReconcileResponseAsync` (12–40): `IsViewer`-Abgleich je Kandidat (Zeile 33);
  `ReconcileIssueBodyAsync` (94–104) markerbasiert ohne Viewer-Vergleich;
  `ReconcileReviewReactionAsync` (106–134) `IsViewer` Zeile 128.
- `ComposeBodyAsync` (185–277): Attachment-Upload nur `github.com`;
  `NumericRepositoryId`-Nachlademöglichkeit (196–214).
- `SendUploadApiAsync` (281–299): gleiche Status-Matrix, nie `OutcomeUncertain`.
- `ValidateResponse` (136–175): `ActionId`-Regex `\A[A-Za-z0-9:_-]{1,200}\z`.

## 11. Nicht-Ereignis-APIs (Benutzerkonto)

`GitHubPlugin.cs` — alle über `_eventApiClient` bzw. `GetGhEnvironment`/`GetGitEnvironment`:

- `CheckHealthAsync` (Zeilen 1701–1714): `GET user` auf `github.com` (Zeile 1706),
  `true` bei 2xx, `false` bei Fehler/Ausnahme. **Befund verifiziert: kein produktiver
  Aufrufer von `IGitPlugin.CheckHealthAsync`** — alle produktiven Call-Sites rufen
  `IAiPlugin.CheckHealthAsync` (`ScmEventOrchestrator.cs` Zeilen 257 und 724,
  `AiExecutionService.cs` Zeile 356). Interface-Pflicht: `IGitPlugin.cs` Zeile 117
  (abstrakt in `GitPluginBase.cs` Zeile 75).
- `GetAvailableRepositoriesAsync` (Zeilen 1751–1772):
  `GET user/repos?affiliation=owner&per_page=100` (Zeile 1756) →
  `AvailableRepository`-Mapping; Fehler → Warnung + leere Liste (1767–1771).
  Verbraucher: `ProjectService.cs` Zeile 238, `ProjectListViewModel.cs` Zeile 232,
  `RepositoryAssignViewModel.cs` Zeile 277. **`GET user/repos` liefert für
  Installationstokens 403** → leere Repository-Liste in der UI.
- Weitere API-Verbraucher derselben Kette: `GetIssuesAsync` (688–712),
  `GetAlertsAsync` (715–754), `GetOpenPullRequestsAsync` (1058–1087),
  `GetIssueTemplatesAsync`/`LoadTemplateContentAsync` (812–921),
  `GetPullRequestStatusAsync` (1338–1363, GraphQL),
  `GetPullRequestWorkflowRunsAsync`/`RunWorkflowListAsync` (1366–1506),
  `GetRepositoryStructureLoadResultAsync` (1861–1897, `git/trees`-API).
- `IsAuthenticationFailure` (465–476): String-Heuristik auf `StdErr` inkl.
  „403", „insufficient" — ein abgelaufenes `ghs_` produziert git-/gh-seitig
  dieselben Muster wie ein ungültiger PAT.

## 12. Host-seitige Gates und Monitoring-Verbraucher

- `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs` —
  `GetRejection` (Zeilen 39–68): Trigger-Prüfung (Modus `UserAccount` = Keyword
  `$Softwareschmiede`, `SeparateAccount` = frei stehende `@ViewerLogin`-Mention,
  Zeilen 45–48); `AlreadyReactedRejection` (52–54); im `UserAccount` Viewer==Owner
  (`SameIdentity(OwnerId/Owner ↔ ViewerId/ViewerLogin)`, Zeilen 55–56) — **ein
  `<app-slug>[bot]`-Viewer ist nie der Owner → alle Ereignisse verworfen**;
  Bearbeiter-Regel gegen Owner oder `TrustedAutomationLogins` (Zeilen 62–64) —
  `TrustedAutomationLogins` (Zeile 25) enthält nur `github-actions[bot]` und
  `app/github-actions`, keine generischen App-Bot-Logins; „Eigene Antwort"-Marker (65–66).
  Hilfsfunktionen `HasFreeStandingTrigger` (71–83), `SameIdentity` (85–89),
  `IsTrustedAutomationLogin` (93–95).
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` —
  CanWrite-/Identitäts-Gates bei der Zustellung (Zeilen 194–198:
  `RepositoryId`-Mismatch oder `!CanWrite` → Wartekommentar mit PAT-Hinweistext;
  Zeilen 662–666: selbe Prüfung im Issue-Aufbereitungs-Dispatch). Revalidierung via
  `ScmEventValidationService.GetRejection` gegen frisch aufgelöstes Repository
  (Zeilen 201–210).
- `src/Softwareschmiede/Application/Services/ScmBackmergeService.cs` —
  `CanWrite`-Gate Zeile 50 (`!repository.CanWrite → return`).
- Monitoring (alle über `IScmEventSource.GetEventRepositoryAsync`, d. h. derselbe
  GraphQL/REST-Auflösungspfad):
  `ScmEventMonitoringService.cs` Zeile 103 (Konto = `accountMode` aus Einstellung),
  `ScmBackmergeMonitoringService.cs` Zeilen 77–78 (immer `UserAccount`),
  `ScmIssuePreparationMonitoringService.cs` Zeilen 84–85 (immer `UserAccount`)
  und Zeilen 202–204 (Neuauflösung unter `accountMode` im SeparatesKonto),
  `ScmTaskCompletionMonitoringService.cs` Zeilen 305–307 (immer `UserAccount`).

## 13. Maskierung und Legacy-Bereinigung

- `GitHubPlugin.SanitizeSensitiveOutput` (`GitHubPlugin.cs` Zeilen 478–499):
  leerer Input → „unbekannter Fehler"; literal `Replace(token, "***")` über **beide**
  Credential-Keys (Zeilen 487–494) — längenunabhängig; anschließend
  `EmbeddedTokenPattern` `oauth2:[^@\s]+@` → `oauth2:***@` (Zeilen 26, 496).
  Ein ~520-stelliger Token wird genauso maskiert wie ein kurzer — kein Präfix-
  oder Längenbezug.
- `RemoveLegacyNetrcEntries` (Zeilen 338–410) + `RemoveNetrcStanzas` (422–459):
  einmalige Konstruktor-Bereinigung der `_netrc`/`.netrc` für die Hosts
  `github.com`/`api.github.com` (Zeile 27); exklusiver Dateizugriff
  (`FileShare.None`); Fehler nur Warnung. Pfadauflösung
  `ResolveNetrcPath` (301–317): Konstruktorparameter →
  `NetrcPathEnvironmentVariable` `SOFTWARESCHMIEDE_NETRC_PATH` (Zeile 44) →
  Benutzerprofil.
- `E2E`-Testseam: `ScmE2ECredentialStore`
  (`src/Softwareschmiede.App/Services/Testing/ScmE2ECredentialStore.cs`) —
  prozessgebundener In-Memory-`ICredentialStore`; Verdrahtung in `App.xaml.cs`
  Zeilen 413–414 (`ICredentialStore`-Registrierung im SCM-E2E-Modus).

## 14. Fehlerbild bei Token-Ablauf (401/403)

- 401 auf Ereignispfaden → `ScmProviderFailureKind.Authentication` →
  `EvictResolutions` (gesamtes Konto) + Benutzertext „GitHub-Anmeldung abgelaufen
  oder ungültig … Personal Access Token erneuern" (`EventFailureMessage`,
  `GitHubPlugin.Events.cs` Zeile 548). Für `ghs_` tritt dies nach ~1 h systematisch ein.
- 403 → `Permission` → `EvictResolutions(client, host)` + PAT-Hinweistext (Zeile 549).
- git-/gh-Fehler laufen über `IsAuthenticationFailure`-Heuristik →
  `InvalidOperationException` mit maskiertem `StdErr` (z. B. `CloneRepositoryAsync`
  Zeilen 1188–1198, `CompletePullRequestAsync` Zeilen 1442–1448 →
  `PullRequestCompletionResult.BlockedResult`).
