# Bestandsaufnahme — GitHub-Token-Kompatibilität mit `ghs_`-Installationstokens

**Quelle:** Issue #453 / Aufgaben-ID `5023201a-4a15-4fea-a845-7e25aec10e97`
**Branch:** `task/issue-453-5023201a4a154feaa8457e25aec10e97-github-token-kompatibilitaet-m`
**Stichtag der Bestandsaufnahme:** 2026-11-22 (W. Europe Standard Time, UTC+01:00)
**Commit:** `e480710803689e47f7de20f2d5a2f1acad949a8a`
**Anforderung:** [requirement.md](requirement.md)

## Zusammenfassung

Der benutzerseitig eingetragene GitHub-Token durchläuft eine unverzweigte Kette: Er wird in der
`SettingsView` über eine `PasswordBox` (`PluginSettingFieldType.Secret`-Felder `Token` und
`AgentToken` des `GitHubPlugin`) unverändert in `PluginSettingEntry.Value` übernommen, über
`PluginSettingsService.SetValue` unter dem Schlüssel `{PluginPrefix}.{FieldKey}`
(`Softwareschmiede.GitHub.Token` / `Softwareschmiede.GitHub.AgentToken`) in den
`ICredentialStore` geschrieben (produktiv `WindowsCredentialStore` via `CredWriteW`) und von
dort von vier Verbrauchergruppen gelesen:

1. **Prozesslose HTTP-API** — `GitHubHttpApiClient` setzt den Token als
   `AuthenticationHeaderValue("Bearer", token)` für alle REST-/GraphQL-Aufrufe des Plugins
   (Ereignisquelle, Responder, Notifications, Nicht-Ereignis-APIs).
2. **`gh`-CLI** — `GetGhEnvironment()` übergibt `GH_TOKEN` als Prozessumgebung
   (`gh issue create`, `gh pr create`, `gh pr merge`, `gh pr ready`, `gh pr review`).
3. **git-CLI** — `GetGitEnvironment()` setzt `GH_TOKEN` plus einen hostgebundenen
   `http.extraheader` über `GIT_CONFIG_COUNT`/`GIT_CONFIG_KEY_0`/`GIT_CONFIG_VALUE_0`
   (Basic-Auth `x-access-token:<token>`, Base64-kodiert) für Clone/Push/Pull/`ls-remote`/
   PR-Checkout.
4. **Agenten-CLIs** — `IScmAgentAccess.GetAgentEnvironmentAsync` liefert dieselbe Umgebung,
   die `AiExecutionService.WithScmAgentEnvironmentAsync` in
   `TerminalSessionStartSpec.EnvironmentVariables` des Agenten-Prozesses mischt.

Zentrale Befunde der Codeanalyse (nur Fakten, keine Bewertung):

- **Keine Längen- oder Formatvalidierung** auf der gesamten Kette — weder in der `PasswordBox`
  (kein `MaxLength`), in `PluginSettingsService`, `WindowsCredentialStore.SetCredential`,
  `GitHubHttpApiClient` noch in den Umgebungs-Helfern. Der Token fließt als `string` durch.
- **Plattformgrenze ungeprüft:** `WindowsCredentialStore.SetCredential` prüft die
  `CredWriteW`-Grenze `CRED_MAX_CREDENTIAL_BLOB_SIZE` (2560 Byte ≈ 1280 UTF-16-Zeichen)
  nicht vorab; bei Überschreitung wirft der Aufruf eine `InvalidOperationException` mit dem
  Win32-Fehlercode. Ein ~520-stelliges `ghs_`-Token (~1040 Byte UTF-16) liegt rechnerisch
  unter der Grenze.
- **`GET /user`-Abhängigkeiten:** `CheckHealthAsync`, `GetAvailableRepositoriesAsync`
  (`GET user/repos`) und der REST-Fallback `ResolveEventRepositoryRestAsync` (`GET user`
  + `GET repos/{owner}/{name}`) hängen an benutzerbezogenen Endpunkten, die für
  Installationstokens laut GitHub-Dokumentation 403 liefern. `CheckHealthAsync` hat
  nach aktuellem Codebestand keinen produktiven Aufrufer (nur `IAiPlugin.CheckHealthAsync`
  wird gerufen — verifiziert per Volltextsuche, siehe [inventory/logic.md](inventory/logic.md)).
- **Viewer-Semantik:** `ResolveEventRepositoryGraphQlAsync` befüllt `ViewerId`/`ViewerLogin`
  aus `viewer{login id}` und `CanWrite` aus `viewerPermission`; mit `ghs_` wäre der Viewer
  eine Bot-Identität `<app-slug>[bot]`. `ScmEventValidationService.GetRejection` verlangt im
  Modus `UserAccount`, dass der Viewer der Repository-Owner ist — ein Bot-Viewer verwirft
  damit alle Ereignisse. `TrustedAutomationLogins` enthält nur `github-actions[bot]` /
  `app/github-actions`, keine generischen App-Bot-Logins.
- **Fehlerbilder:** `EvaluateResponse` mappt 401 → `Authentication` (→ `EvictResolutions`
  + Hinweistext „Personal Access Token erneuern"), 403 → `Permission`. Beide Texte sind
  PAT-zentriert.
- **`AgentToken`:** Im Modus `SeparateAccount` wählt `ClientFor`/`CredentialKeyFor` den
  `AgentToken`-Credential-Key für die Ereignispfade; `GetAgentEnvironmentAsync` stellt ihn
  dem Agenten bereit. Nicht-Ereignis-Pfade laufen bewusst weiter unter dem Benutzer-Token.
- **Mas­kierung:** `SanitizeSensitiveOutput` ersetzt beide Tokens literal durch `***` und
  maskiert `oauth2:…@`-URL-Einbettungen per `EmbeddedTokenPattern` — längenunabhängig.
- **Legacy-`_netrc`:** Nur einmalige Bestandsbereinigung beim Plugin-Konstruktor
  (`RemoveLegacyNetrcEntries`); keine netrc-Persistenz mehr.

## Details

- [inventory/models.md](inventory/models.md) — Datenmodell: `PluginConfiguration`,
  `PluginSettingField`/`PluginSettingEntry`, `ScmEventRepository`, `ScmRepositoryContext`,
  `ScmEventObservation`, `GitHubEventApiRequest`/`GitHubEventApiResponse`,
  `GitHubAssetUploadRequest`, `ScmProviderException` u. a.
- [inventory/logic.md](inventory/logic.md) — Token-Kettenglieder im Detail:
  UI-Eingabe → CredentialStore → Bearer-Header → `GH_TOKEN`/`GIT_CONFIG_*`-Umgebung →
  `gh`-/git-CLI → Agenten-Env → Ereignis-/Viewer-/CanWrite-Logik → Fehler-/Eviction-Pfade →
  `SanitizeSensitiveOutput`/`RemoveLegacyNetrcEntries` → Monitoring-/Dispatch-Verbraucher.
- [inventory/interfaces.md](inventory/interfaces.md) — `ICredentialStore`, `ICliRunner`,
  `IGitHubEventApiClient`, `IScmAgentAccess`, `IScmEventSource`, `IScmEventResponder`,
  `IScmNotificationSource`, `IScmRepositoryAliasNormalizer`, `IGitPlugin`, `IPlugin`.
- [inventory/enums.md](inventory/enums.md) — `PluginSettingFieldType`, `ScmAccountMode`,
  `ScmProviderFailureKind`, `ScmReferenceKind`, `ScmContentKind`, `ScmResponseKind`,
  `PluginType`, `PullRequestCompletionStrategy`, `PullRequestMergeMethod`.
- [inventory/tests.md](inventory/tests.md) — **Test-Ausgangszustand** (Build 0 Fehler,
  stabile Lane 2782/0/3) und die vorhandenen Testklassen/-seams für den Token-Bereich.

## Test-Ausgangszustand (Kurzfassung)

| Aspekt | Ergebnis |
|--------|----------|
| `dotnet build Softwareschmiede.slnx` | Erfolgreich, 0 Warnungen, 0 Fehler (16,9 s) |
| Stabile Lane (`Category!=OsInterface`) | **2782 erfolgreich, 0 fehlgeschlagen, 3 übersprungen** (2785 gesamt, 5 m 13 s, Exit-Code 0) |
| Übersprungen | 3 plattformbedingte Skips (Linux/macOS-Testfälle), token-unabhängig |

Vollständige Angaben (Befehle, SDK-Version, Log-Verweis): [inventory/tests.md](inventory/tests.md),
Rohprotokoll: [inventory/test-results/baseline-stable-lane.log](inventory/test-results/baseline-stable-lane.log).
