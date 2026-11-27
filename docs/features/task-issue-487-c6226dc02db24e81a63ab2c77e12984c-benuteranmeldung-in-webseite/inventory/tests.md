# Bestandsaufnahme: Tests und Test-Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-27, Build ab ca. 06:37 +0100 (CET), Testläufe 06:37–06:46 +0100
- Branch und Commit-ID: `task/issue-487-c6226dc02db24e81a63ab2c77e12984c-benuteranmeldung-in-webseite` @ `7430f1af69f33a82dc2890c9b2145d38e58a28f4` („Backmerge from main to staging (#486)")
- Uncommittete Änderungen im getesteten Stand: nur untracked `docs/features/task-issue-487-…/` (zum Laufzeitpunkt `requirement.md`, `todo.md`; die Inventar-Artefakte selbst); kein Produktiv-/Testcode verändert
- Testumgebung und Runtime-/SDK-Versionen: Windows (Host `DESKTOP-CM8OBSG`), Git-Bash-Agent-Sandbox; .NET SDK `11.0.100` (zusätzlich `10.0.404` installiert), Target `net11.0` / `net11.0-windows10.0.17763.0`; xunit.v3 4.0.3, Microsoft.NET.Test.Sdk 18.12.0
- Umgebungsvariablen der Läufe: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (Sandbox-Regel aus `CLAUDE.md`; keine ConPTY-Tests im `Category!=OsInterface`-Filter enthalten — ohne Effekt, defensiv gesetzt)
- Ermittelte Testsuiten und Quellen der Testbefehle: `.github/workflows/pr-staging-ci.yml` + `staging-ci.yml` definieren drei reguläre Suiten (`Softwareschmiede.Tests`, `Softwareschmiede.IntegrationTests` — jeweils `--filter "Category!=OsInterface"`, sowie `Softwareschmiede.StatusServer.Tests` ohne Filter) plus eine getrennte `Category=OsInterface`-Lane; `CLAUDE.md` dokumentiert die reguläre Lane als stabilen Pfad und verlangt vollen Build vor `dotnet test`

## Vorlauf: Build

| Befehl | Arbeitsverzeichnis | Ergebnis |
|--------|--------------------|----------|
| `dotnet build Softwareschmiede.slnx -c Debug` | Repo-Root | Erfolg — 0 Warnungen, 0 Fehler, ~24 s; alle 16 Projekte inkl. `Softwareschmiede.App` (kein Datei-Lock, keine `Softwareschmiede.App.exe` lief) |

## Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| StatusServer.Tests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test server/Softwareschmiede.StatusServer.Tests/Softwareschmiede.StatusServer.Tests.csproj -c Debug` | Repo-Root | 0 | 44 | 0 | 0 | [statusserver-tests-baseline.trx](test-results/statusserver-tests-baseline.trx) |
| Softwareschmiede.Tests (regulär) | `… dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category!=OsInterface"` | Repo-Root | 0 | 2878 | 0 | 3 | [softwareschmiede-tests-baseline.trx](test-results/softwareschmiede-tests-baseline.trx) |
| Softwareschmiede.IntegrationTests (regulär) | `… dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj -c Debug --filter "Category!=OsInterface"` | Repo-Root | 0 | 436 | 0 | 0 | [integration-tests-baseline.trx](test-results/integration-tests-baseline.trx) |

Summe regulärer Lane: 3361 Tests — 3358 bestanden, 0 fehlgeschlagen, 3 übersprungen.
Laufzeiten: StatusServer ~2,7 s; Haupt-Tests ~3,8 min; Integrationstests ~1,1 min.

## Nachgewiesene bestehende Testfehler

**Keine.** Alle ausgeführten Tests bestanden; der Ausgangszustand der regulären Lane
ist fehlerfrei und dient als Vergleichs-Nachweis für spätere Läufe.

### Übersprungene Tests im Ausgangslauf (keine Fehler)

| Test-ID | Suite / Dateipfad | Grund | Nachweis |
|---------|-------------------|-------|----------|
| `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_Linux_RemainsUnchanged` | `src/Softwareschmiede.Tests` | Plattformbedingt — Linux-Assertion, unter Windows übersprungen (`NotExecuted`) | TRX oben |
| `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_RemainsUnchanged` | `src/Softwareschmiede.Tests` | Plattformbedingt — macOS-Assertion, unter Windows übersprungen (`NotExecuted`) | TRX oben |
| `Softwareschmiede.Tests.Application.Services.WorkingDirectoryOpenServiceTests.Open_OnNotWindows_ThrowsPlatformNotSupportedException` | `src/Softwareschmiede.Tests` | Plattformbedingt — Nicht-Windows-Assertion, unter Windows übersprungen (`NotExecuted`) | TRX oben |

## Testlücken und Ausführungsprobleme

- **`Category=OsInterface`-Lane nicht ausgeführt** (`Softwareschmiede.Tests` +
  `Softwareschmiede.IntegrationTests` mit Filter `Category=OsInterface`): Diese Lane
  enthält E2E- (FlaUI/WPF), ConPTY-, Clipboard- und echte Prozessstart-Tests und ist
  laut `CLAUDE.md` bewusst eine getrennte, umgebungssensitive Lane. In dieser
  Agent-Sandbox sind die 14 ConPTY-E2E-Tests dokumentarisch nicht lauffähig
  (Kindprozess wird nicht isoliert → `TimeoutException`), und FlaUI-E2E-Tests
  benötigen eine interaktive Desktop-Session. Kein Test dieser Lane betrifft den
  StatusServer (Anforderungs-Scope). Über den Ausgangszustand dieser Tests kann
  daher **keine Aussage** getroffen werden — sie gelten als „nicht ausgeführt", nicht
  als bestanden/fehlgeschlagen.
- `launchSettings.json` des StatusServers ist im Repo nicht vorhanden (von
  `server/README.md` referenziert) — für `dotnet test`/`WebApplicationFactory`
  irrelevant, lokaler `dotnet run`-Port dadurch abweichend möglich.

## Testklassen (StatusServer.Tests)

Projekt: `server/Softwareschmiede.StatusServer.Tests` — `net11.0`, `xunit.v3`,
`Microsoft.AspNetCore.Mvc.Testing` 11.0.0, `Microsoft.AspNetCore.SignalR.Client`
11.0.0, `FluentAssertions` 8.11.0, `Moq` 4.21.0, coverlet 10.1.1;
`TestContext.Current.CancellationToken` als Abbruchtoken-Idiom.

### `StatusWebApplicationFactory`
Datei: `server/Softwareschmiede.StatusServer.Tests/StatusWebApplicationFactory.cs` — `WebApplicationFactory<Program>`

| Member | Beschreibung |
|--------|--------------|
| `RelayTokens` (init, Default `["test-token"]`) | Befüllt `Relay:Tokens:{i}` via `builder.UseSetting` — **entfällt** beim DB-Umbau (Token-Seeding dann über Datenbank) |
| `PageUsername` / `PagePassword` (init) | Befüllt `Page:Username`/`Page:Password` — **entfällt** |
| `DevToken` (init) | Befüllt `Relay:DevToken` — **entfällt** |
| `ConfigureWebHost` | `UseEnvironment(Production)` + `UseSetting`-Overrides. Umbau nötig: isolierte SQLite-DB je Testlauf (z. B. Temp-Pfad oder `:memory:`) statt Konfigurations-Seeding |

### `PageAccessTests`
Datei: `server/Softwareschmiede.StatusServer.Tests/PageAccessTests.cs` — **ersatzlos zu ersetzen durch Setup-/Login-/Rollen-Tests**

- `Page_WithoutCredentials_ReturnsOk` — `/` und `/status.json` frei ohne Credentials
- `Page_WithBasicAuth_UnauthorizedWithoutCredentials` — 401 + `WWW-Authenticate: Basic`
- `Page_WithBasicAuth_AuthorizedReturnsOk` — korrekte Credentials → 200
- `Page_WithBasicAuth_WrongPasswordUnauthorized` — falsches Passwort → 401
- `Relay_IsNotProtectedByPageBasicAuth` — `/relay` von Basic-Auth ausgenommen

### `RelayTokenValidatorTests`
Datei: `server/Softwareschmiede.StatusServer.Tests/Security/RelayTokenValidatorTests.cs` — **Umbau auf DB-Tokens**

- `CreateSut(tokens, devToken, environment)` — `Options.Create(StatusServerOptions)` + `Mock<IHostEnvironment>` — Hilfsmethode muss auf DB-Kontext umgestellt werden
- `IsValid_KnownToken_ReturnsTrue` — konfigurierte Tokens akzeptiert
- `IsValid_UnknownOrEmptyToken_ReturnsFalse` (Theory: `null`, `""`, `"   "`, `"token-c"`)
- `IsValid_EmptyTokenList_RejectsAll`
- `IsValid_DevToken_OnlyInDevelopment` (Theory: Development→true, Production/Staging→false)

### `StatusRelayRoundtripTests`
Datei: `server/Softwareschmiede.StatusServer.Tests/Relay/StatusRelayRoundtripTests.cs` — `IClassFixture<StatusWebApplicationFactory>` + `IAsyncDisposable`; echte `HubConnection` über Long-Polling gegen den TestServer

- `CreateConnection(token)` — Hilfsmethode: `HubConnectionBuilder` mit `AccessTokenProvider`, `HttpMessageHandlerFactory` = `_factory.Server.CreateHandler()`
- `RegisterAndPushSnapshot_InstanceAppearsOnline` — Register + Push → Instanz erscheint in `/status.json` und `/`
- `RegisterInstance_WrongProtocolVersion_IsRejected` — Ablehnung mit Grund bei Version 999
- `PushSnapshot_WithoutRegistration_Fails` — `HubException`
- `Connect_InvalidToken_RegistrationFails` — Abbruch bei ungültigem Token
- `Connect_MissingToken_RegistrationFails` — Abbruch ohne Token
- `Disconnect_InstanceMarkedOffline` — `"connected":false` + `"isStale":true` nach `StopAsync` (Poll-Schleife 5 s)

**Achtung beim Umbau:** Diese Tests lesen `/` und `/status.json` über
`factory.CreateClient()` **ohne** Authentifizierung — mit `RequireAuthorization`
müssen sie künftig eine Session herstellen oder die JSON-Prüfung umstellen.
Token-Seeding erfolgt aktuell implizit über `RelayTokens`-Default `["test-token"]`.

### `InstanceSnapshotCacheTests`
Datei: `server/Softwareschmiede.StatusServer.Tests/Status/InstanceSnapshotCacheTests.cs` — 9 `[Fact]`-Methoden, unverändert relevant (Auth-unabhängig):
`MarkConnected_WithoutSnapshot_IsConnectedAndNotStale`, `UpdateSnapshot_StoresSnapshotAndAdvancesLastSeen`, `GetAll_ConnectedButOldSnapshot_IsStale`, `MarkDisconnected_LastConnection_InstanceOfflineAndStale`, `GetAll_MultipleInstances_TrackedSeparately`, `MarkDisconnected_OneOfTwoConnections_RemainsConnected`, `MarkConnected_SameConnectionIdOnOtherInstance_RemovesFromOldInstance`, `GetAll_DisconnectedLongerThanRetention_EvictsInstance`, `GetAll_ConnectedLongerThanRetention_KeepsInstance`

### `StatusPageComposerTests`
Datei: `server/Softwareschmiede.StatusServer.Tests/Status/StatusPageComposerTests.cs` — 9 `[Fact]`-Methoden (Sections, GitHub-Links, Offline-Note, Empty-State, GitHub-Fehler, HTML-Encoding, Null-Collections, Non-Http-Scheme, Instance-Tags, Mobile-CSS); Hilfsmethoden `CreateSut(TestTimeProvider)`, `OnlineInstance()`, `GitHubRepos()`. Stil-/Test-Vorbild für die neuen Formularseiten-Renderer.

### `GitHubStatusServiceTests`
Datei: `server/Softwareschmiede.StatusServer.Tests/GitHub/GitHubStatusServiceTests.cs` — 5 `[Fact]`-Methoden (Aggregat, TTL-Cache, 304-ETag-Revalidierung, HTTP-Fehler mit Zeitstempel, ungültiger Repo-Eintrag); Hilfen `FakeHandler : HttpMessageHandler` (mit `CallCount`), `CreateSut(handler, time, repositories, cacheTtlSeconds)`, `Json(content, etag)`.

## Hilfsmethoden

### `TestTimeProvider`
Datei: `server/Softwareschmiede.StatusServer.Tests/TestSupport/TestTimeProvider.cs` — `internal sealed : TimeProvider`
- `Now` (`DateTimeOffset`, setzbar, Default 2026-01-01 12:00 UTC), `GetUtcNow()`, `Advance(TimeSpan)` — deterministische Zeit für Cache-/Composer-/GitHub-Tests; könnte auch Lockout-/Sitzungsdauer-Tests tragen
