# Tests — Ausgangszustand und vorhandene Token-Seams

## Ausgangszustand (Baseline vor jeder Änderung)

| Aspekt | Wert |
|--------|------|
| Datum/Uhrzeit | 2026-11-22, ~16:30–16:38 Uhr, W. Europe Standard Time (UTC+01:00) |
| Branch | `task/issue-453-5023201a4a154feaa8457e25aec10e97-github-token-kompatibilitaet-m` |
| Commit | `e480710803689e47f7de20f2d5a2f1acad949a8a` |
| Uncommittete Änderungen | nur untracked: `docs/features/task/issue-453-…/` (diese Inventar-Dateien) |
| .NET SDK | `11.0.100` (`dotnet --version`); Ziel-Framework `net11.0` / `net11.0-windows10.0.17763.0` |

### Build

- Befehl: `dotnet build Softwareschmiede.slnx`
- Ergebnis: **Erfolgreich**, 0 Warnungen, 0 Fehler, Dauer 16,9 s — alle Projekte inkl.
  `Softwareschmiede.App` (kein MSB3027/MSB3026-Dateilock).

### Stabile Testlane

- Befehl: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`
- Exit-Code: **0**
- Ergebnis: **2782 erfolgreich · 0 fehlgeschlagen · 3 übersprungen** (2785 gesamt), Dauer 5 m 13 s
- Rohprotokoll: [test-results/baseline-stable-lane.log](test-results/baseline-stable-lane.log)
- Kein `--no-build` verwendet (Tests liefen auf dem vollständigen Vorbau).

Übersprungene Tests (alle plattformbedingt, token-unabhängig):

| Test | Datei | Grund |
|------|-------|-------|
| `SystemShutdownServiceTests.ResolveShutdownCommand_Linux_RemainsUnchanged` | `src/Softwareschmiede.Tests/Infrastructure/Services/` | Linux-Fall unter Windows übersprungen |
| `SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_RemainsUnchanged` | `src/Softwareschmiede.Tests/Infrastructure/Services/` | macOS-Fall unter Windows übersprungen |
| `WorkingDirectoryOpenServiceTests.Open_OnNotWindows_ThrowsPlatformNotSupportedException` | `src/Softwareschmiede.Tests/Application/Services/` | Nicht-Windows-Fall übersprungen |

**Nachgewiesene preexisting Fehler: keine.** Die Baseline ist vollständig grün.

## Vorhandene Testklassen und Seams für den Token-Bereich

### `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs` (2959 Zeilen)

Fixture: `Mock<ICliRunner>` + `Mock<ICredentialStore>` + `FakeGitHubEventApi`;
`_sut = new GitHubPlugin(cliRunner, credentialStore, logger, _api, null, CreateMissingNetrcPath())`
(Zeilen 21–33) — `CreateMissingNetrcPath` (37–38) isoliert die netrc-Bereinigung.

Token-relevante Tests/Seams:

| Bereich | Zeilen | Inhalt |
|---------|--------|--------|
| `HasGitHubExtraHeader` (private Assertion) | 2947–2954 | prüft `GIT_CONFIG_COUNT=1`, `GIT_CONFIG_KEY_0` und `GIT_CONFIG_VALUE_0 = "AUTHORIZATION: basic " + Base64("x-access-token:"+token)` — direkte Seam für Längen-Assertions bei `ghs_` |
| `CheckHealthAsync`-Tests | 703–733 | true bei `GET user` 2xx, false bei 401 — Basis für die 403-Bewertung |
| `GetAgentEnvironmentAsync_*` | 1456–1581 | GH_TOKEN, `GIT_TERMINAL_PROMPT`, hostgebundener extraheader, GHE-Hosts, Ports, SSH-Formen, fail-closed ohne Host, SeparatesKonto→AgentToken |
| `SanitizeSensitiveOutput_*` | 1274–1328, 1594–1613 | Maskierung des Tokens bzw. AgentTokens in git-Fehlertexten inkl. `oauth2:…@` |
| `GetAvailableRepositoriesAsync_*` | 1692–1737 | Mapping + Fehler → leere Liste |
| `SeparatesAccount_WithoutAgentToken_*` | 1615–1629 | `Authentication`-Fehler vor jedem Transport-Call |
| git-Operationen mit Env-Assertion | u. a. 517, 1013, 1201, 1929, 1991, 1997, 2022, 2704–2943 | Clone/Push/Pull/`ls-remote`/PR-Checkout: `GH_TOKEN` + extraheader, `GIT_SSH_COMMAND`-Abwesenheit, GHE-Varianten |
| `PluginMetadata_ShouldExposeExpectedValues` | 735–751 | Setting-Gruppen/`Token`-Pflichtfeld |
| `GetSettingGroups_ContainsOptionalAgentTokenField` | 1583–1592 | `AgentToken` als optionales Secret |

### `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubHttpApiClientTests.cs` (311 Zeilen)

| Bereich | Zeilen | Inhalt |
|---------|--------|--------|
| `SendAsync_MapsHostToCorrectBaseUrl` | 13–29 | github.com vs. GHE-URI-Abbildung (inkl. `api/graphql`) |
| `SendAsync_InvalidHost/ForeignComponents` | 31–61 | Host-Validierung, kein Request bei fremden Origins |
| `SendAsync_RequiredHeaders_BearerOnlyWithToken` | 63–88 | `Authorization: Bearer <token>` — `Parameter.Should().Be(token)` ist die direkte Seam für ~520-stellige Token |
| `SendAsync_TokenProviderIsEvaluatedPerRequest` | 90–106 | Tokenwechsel ohne Neustart |
| `SendAsync_TokenNeverAppearsInOutputs` | 182–203 | Token nicht in URL/Body/Headern/Fehlertexten |
| `SendAsync_TransportError/ErrorStatus` | 145–169 | Temporary-Matrix, `OutcomeUncertain` |
| `UploadAsync_*` | 205–304 | Bearer beim Upload, Host-Grenze, Größenlimit, typed Errors |
| `Client`/`DelegateHandler`-Helfer | 306–311 | `HttpMessageHandler`-Injection des Produktiv-Clients |

### `src/Softwareschmiede.Tests/Helpers/FakeGitHubEventApi.cs`

Warteschlangenbasierter `IGitHubEventApiClient` (54 Zeilen): `Add`/`AddJson`/`AddGraph`/
`AddStatus`/`AddFailure` für REST/GraphQL, `AddUpload*` für Uploads, `Requests`/
`UploadRequests`-Aufzeichnung, `AssertComplete`. Zentrale Seam aller Ereignispfad-Tests.

### `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubEventSourceTests.cs` (1764 Zeilen)

Token-/Viewer-relevante Tests (Auswahl):

| Test | Zeilen | Inhalt |
|------|--------|--------|
| `RepositoryContext_UsesStableIdsAndPermissions` | 28–45 | Auflösung → `ViewerId`/`ViewerLogin`/`CanWrite` (GraphQL-Pfad) |
| `BotAuthor_LoginFlowsIntoObservation` | 69–81 | Bot-Autoren fließen in die Observation |
| `ViewerReactions_*` / `MissingReactionGroups_*` | 194–267 | `viewerHasReacted`-Abbildung |
| `Failures_AreTypedAndSanitized` | 286–309 | Status→`ScmProviderFailureKind`-Matrix (401/403/…) |
| `AccessFailures_ExplainTokenSettingsAndRecovery` | 310–326 | PAT-Hinweistexte der `Authentication`/`Permission`-Fehler |
| `GraphErrors_RejectPartialData` | 327–343 | GraphQL-`errors[]`-Behandlung |
| Reconcile/Comment/Attachment-Tests | 344–700+ | `IsViewer`-Reconcile, Marker, Upload-Kette |

### `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubNotificationSourceTests.cs`

Notifications-Inbox-Pfad (`EnumerateNotificationsAsync`, Modus `SeparateAccount`).

### `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginNetrcCleanupTests.cs`

`RemoveLegacyNetrcEntries`-Bereinigung (Stanzas, Dateilöschung, Fremd-Hosts) —
nutzt den `netrcPath`-Konstruktorparameter als Seam.

### `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubSinceWindowTests.cs`

`SinceSafetyMargin`-/Pagination-Zeitfenster (nicht token-relevant, gleiche Kette).

### `src/Softwareschmiede.Tests/Application/Services/ScmEventValidationServiceTests.cs` (252 Zeilen)

`GetRejection`-Matrix ohne Provideraufrufe: Keyword-/Mention-Trigger je
`ScmAccountMode`, Owner-Regel, `TrustedAutomationLogins` (`github-actions[bot]`,
`app/github-actions` akzeptiert; `dependabot[bot]`, `renovate[bot]` abgelehnt —
Zeilen 183–229), `ViewerHasReacted`, Marker. Direkte Seam für die
Bot-Viewer-/Trusted-Logins-Bewertung.

### `src/Softwareschmiede.Tests/ServiceIntegration/PluginSettingsServiceIntegrationTests.cs` (111 Zeilen)

`PluginSettingsService` Roundtrip über `InMemoryCredentialStore` (Zeilen 81–92) +
`FakeSettingsPlugin` (95–111): `SetValue`/`GetValue`/`HasValue`/`DeleteValue` —
nahtlose Erweiterungsstelle für Langtoken-Roundtrips.

### Weitere Seams

- `ScmE2ECredentialStore` (`src/Softwareschmiede.App/Services/Testing/ScmE2ECredentialStore.cs`):
  prozessgebundener In-Memory-`ICredentialStore` des SCM-E2E-Modus, registriert in
  `App.xaml.cs` Zeilen 413–414.
- `CredentialStoreSnapshot` (`src/Softwareschmiede.Tests/Infrastructure/Services/CredentialStoreSnapshot.cs`):
  Sichert/Wiederherstellt Credential-Werte — nützlich bei Tests gegen den echten
  `WindowsCredentialStore` (existiert noch kein solcher Test).
- `InMemoryCredentialStoreForSettings`
  (`src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs` Zeile 1156):
  Credential-Store-Double der SettingsViewModel-Tests.
- E2E (FlaUI): `src/Softwareschmiede.Tests/E2E/` — u. a. `E2E_IdePluginSettings.cs`
  (Plugin-Einstellungen), `E2E_ScmEvents.cs`, `ScmE2EFixture.cs`; laufen in der
  `Category=OsInterface`-Lane (außerhalb der stabilen Lane).
- Kein dedizierter `WindowsCredentialStore`-Unit-Test vorhanden (P/Invoke-Pfad
  ungetestet).
