# Tasks: Update-Download mit Fallback auf die GitHub-API-Asset-URL

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `UpdateInfo` in `UpdateModels.cs` um optionalen Parameter `Uri? FallbackDownloadUrl = null` erweitern (inkl. XML-Doku) | Offen | — |
| 2 | Logik | `GitHubAsset` in `GitHubReleaseClient.cs` um `Url` (`url`) und `Id` (`id`) erweitern | Offen | — |
| 3 | Logik | `IsAllowedAssetApiUrl` (absolut + HTTPS + Host `api.github.com`) in `GitHubReleaseClient` hinzufügen | Offen | — |
| 4 | Logik | `CreateFallbackDownloadUrl` in `GitHubReleaseClient` implementieren (`url` bevorzugt, `id`-Ableitung, sonst `null`) | Offen | — |
| 5 | Logik | `TryCreateCandidate` übergibt `FallbackDownloadUrl` an `UpdateInfo` | Offen | — |
| 6 | Logik | `SendDownloadRequestAsync` in `UpdatePackageService` hinzufügen (Fallback-Variante mit `Accept: application/octet-stream` + `User-Agent`) | Offen | — |
| 7 | Logik | `DownloadAsync` auf `UpdateInfo`-Signatur umstellen und 5xx-Fallback (einmalig, `LogWarning`, `EnsureSuccessStatusCode`) implementieren | Offen | — |
| 8 | Logik | `PreparePackageAsync`-Aufruf von `DownloadAsync` auf `update` umstellen | Offen | — |
| 9 | E2E-Infrastruktur | `Klassifiziere` in `UpdateFixtureHttpMessageHandler`: `/releases/assets/` vor `/releases` als `"asset"` klassifizieren | Offen | — |
| 10 | E2E-Infrastruktur | `StableAssetApiUrl`/`RcAssetApiUrl`-Properties in `UpdateE2EFixture` ergänzen | Offen | — |
| 11 | E2E-Infrastruktur | `ErzeugeReleaseListenJson` in `UpdateE2EFixture` um `url`/`id` der Assets ergänzen | Offen | — |
| 12 | E2E-Infrastruktur | `SetzeStandardAntworten` in `UpdateE2EFixture` um Antworten für beide API-Asset-URLs (200 + ZIP) erweitern | Offen | — |
| 13 | E2E-Infrastruktur | `ErzeugeRcUndHoeheresStableReleaseJson` und `ErzeugeReleaseListenJson` in `E2E_UpdateSettings.cs` um `url`/`id` ergänzen | Offen | — |
| 14 | Tests | `RoutingHttpHandler` um `RequestedAcceptHeaders` (Accept-Header-Protokollierung) erweitern | Offen | — |
| 15 | Tests | `CreateUpdateInfo` (Parameter `fallbackDownloadUrl`) und `CreateSut` (`HttpMessageHandler`-Überladung) in `UpdatePackageServiceTests` erweitern | Offen | — |
| 16 | Tests | `PreparePackageAsync_ShouldUseFallbackDownloadUrl_WhenPrimaryReturns5xx` in `UpdatePackageServiceTests` schreiben | Offen | — |
| 17 | Tests | `PreparePackageAsync_ShouldThrow_WhenPrimaryAndFallbackReturn5xx` in `UpdatePackageServiceTests` schreiben | Offen | — |
| 18 | Tests | `PreparePackageAsync_ShouldNotUseFallback_WhenPrimaryReturnsNon5xxError` in `UpdatePackageServiceTests` schreiben | Offen | — |
| 19 | Tests | `GetLatestReleaseAsync_ShouldReturnRelease_WhenAssetExists` um `url`/`id`-JSON und `FallbackDownloadUrl`-Assert erweitern | Offen | — |
| 20 | Tests | `GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsMissing` in `GitHubReleaseClientTests` schreiben | Offen | — |
| 21 | Tests | `GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsInvalid` (inkl. `null`-Variante ohne `id`) in `GitHubReleaseClientTests` schreiben | Offen | — |
| 22 | Tests | `SendAsync_KlassifiziertApiAssetUrlAlsAsset` in `UpdateFixtureHttpMessageHandlerTests` schreiben | Offen | — |
| 23 | E2E-Tests | `AssetHttpFehlerPhaseAsync` anpassen: beide Routen auf 500 setzen + Fallback-Request-Assert + Doku | Offen | — |
| 24 | E2E-Tests | `AssetFallbackErfolgPhaseAsync` neu schreiben und in `Startup_SafetyCancelAndErrorsRemainUsable` nach `LauncherFehlerPhaseAsync` einhängen (Doku der Testmethode ergänzen) | Offen | — |
| 25 | Verifikation | `dotnet format Softwareschmiede.slnx`, vollständiger Build, `dotnet test ... --filter "Category!=OsInterface"` (in Sandbox mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`) | Offen | — |
