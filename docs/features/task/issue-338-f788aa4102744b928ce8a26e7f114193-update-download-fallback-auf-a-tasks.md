# Tasks: Update-Download mit Fallback auf die GitHub-API-Asset-URL

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `UpdateInfo` in `UpdateModels.cs` um optionalen Parameter `Uri? FallbackDownloadUrl = null` erweitern (inkl. XML-Doku) | Erledigt | `GitHubReleaseClientTests.GetLatestReleaseAsync_ShouldReturnRelease_WhenAssetExists` |
| 2 | Logik | `GitHubAsset` in `GitHubReleaseClient.cs` um `Url` (`url`) und `Id` (`id`) erweitern | Erledigt | `GitHubReleaseClientTests.GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsMissing` |
| 3 | Logik | `IsAllowedAssetApiUrl` (absolut + HTTPS + Host `api.github.com`) in `GitHubReleaseClient` hinzufügen | Erledigt | `GitHubReleaseClientTests.GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsInvalid` |
| 4 | Logik | `CreateFallbackDownloadUrl` in `GitHubReleaseClient` implementieren (`url` bevorzugt, `id`-Ableitung, sonst `null`) | Erledigt | `GitHubReleaseClientTests.GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsMissing` / `_WhenUrlIsInvalid` |
| 5 | Logik | `TryCreateCandidate` übergibt `FallbackDownloadUrl` an `UpdateInfo` | Erledigt | `GitHubReleaseClientTests.GetLatestReleaseAsync_ShouldReturnRelease_WhenAssetExists` |
| 6 | Logik | `SendDownloadRequestAsync` in `UpdatePackageService` hinzufügen (Fallback-Variante mit `Accept: application/octet-stream` + `User-Agent`) | Erledigt | `UpdatePackageServiceTests.PreparePackageAsync_ShouldUseFallbackDownloadUrl_WhenPrimaryReturns5xx` (Accept-Header-Assert) |
| 7 | Logik | `DownloadAsync` auf `UpdateInfo`-Signatur umstellen und 5xx-Fallback (einmalig, `LogWarning`, `EnsureSuccessStatusCode`) implementieren | Erledigt | `UpdatePackageServiceTests.PreparePackageAsync_ShouldUseFallbackDownloadUrl_WhenPrimaryReturns5xx` / `ShouldThrow_WhenPrimaryAndFallbackReturn5xx` / `ShouldNotUseFallback_WhenPrimaryReturnsNon5xxError` |
| 8 | Logik | `PreparePackageAsync`-Aufruf von `DownloadAsync` auf `update` umstellen | Erledigt | `UpdatePackageServiceTests.PreparePackageAsync_ShouldUseFallbackDownloadUrl_WhenPrimaryReturns5xx` |
| 9 | E2E-Infrastruktur | `Klassifiziere` in `UpdateFixtureHttpMessageHandler`: `/releases/assets/` vor `/releases` als `"asset"` klassifizieren | Erledigt | `UpdateFixtureHttpMessageHandlerTests.SendAsync_KlassifiziertApiAssetUrlAlsAsset` |
| 10 | E2E-Infrastruktur | `StableAssetApiUrl`/`RcAssetApiUrl`-Properties in `UpdateE2EFixture` ergänzen | Erledigt | `End2EndTest.Startup_SafetyCancelAndErrorsRemainUsable` (Phase `AssetFallbackErfolgPhaseAsync`) |
| 11 | E2E-Infrastruktur | `ErzeugeReleaseListenJson` in `UpdateE2EFixture` um `url`/`id` der Assets ergänzen | Erledigt | `End2EndTest.Startup_SafetyCancelAndErrorsRemainUsable` (Phase `AssetFallbackErfolgPhaseAsync`) |
| 12 | E2E-Infrastruktur | `SetzeStandardAntworten` in `UpdateE2EFixture` um Antworten für beide API-Asset-URLs (200 + ZIP) erweitern | Erledigt | `End2EndTest.Startup_SafetyCancelAndErrorsRemainUsable` (Phase `AssetFallbackErfolgPhaseAsync`) |
| 13 | E2E-Infrastruktur | `ErzeugeRcUndHoeheresStableReleaseJson` und `ErzeugeReleaseListenJson` in `E2E_UpdateSettings.cs` um `url`/`id` ergänzen | Erledigt | `End2EndTest.Startup_SafetyCancelAndErrorsRemainUsable` (Phase `AssetFallbackErfolgPhaseAsync`) |
| 14 | Tests | `RoutingHttpHandler` um `RequestedAcceptHeaders` (Accept-Header-Protokollierung) erweitern | Erledigt | `UpdatePackageServiceTests.PreparePackageAsync_ShouldUseFallbackDownloadUrl_WhenPrimaryReturns5xx` |
| 15 | Tests | `CreateUpdateInfo` (Parameter `fallbackDownloadUrl`) und `CreateSut` (`HttpMessageHandler`-Überladung) in `UpdatePackageServiceTests` erweitern | Erledigt | `UpdatePackageServiceTests.PreparePackageAsync_ShouldUseFallbackDownloadUrl_WhenPrimaryReturns5xx` (nutzt beide) |
| 16 | Tests | `PreparePackageAsync_ShouldUseFallbackDownloadUrl_WhenPrimaryReturns5xx` in `UpdatePackageServiceTests` schreiben | Erledigt | Test selbst |
| 17 | Tests | `PreparePackageAsync_ShouldThrow_WhenPrimaryAndFallbackReturn5xx` in `UpdatePackageServiceTests` schreiben | Erledigt | Test selbst |
| 18 | Tests | `PreparePackageAsync_ShouldNotUseFallback_WhenPrimaryReturnsNon5xxError` in `UpdatePackageServiceTests` schreiben | Erledigt | Test selbst |
| 19 | Tests | `GetLatestReleaseAsync_ShouldReturnRelease_WhenAssetExists` um `url`/`id`-JSON und `FallbackDownloadUrl`-Assert erweitern | Erledigt | Test selbst |
| 20 | Tests | `GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsMissing` in `GitHubReleaseClientTests` schreiben | Erledigt | Test selbst |
| 21 | Tests | `GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsInvalid` (inkl. `null`-Variante ohne `id`) in `GitHubReleaseClientTests` schreiben | Erledigt | Test selbst |
| 22 | Tests | `SendAsync_KlassifiziertApiAssetUrlAlsAsset` in `UpdateFixtureHttpMessageHandlerTests` schreiben | Erledigt | Test selbst |
| 23 | E2E-Tests | `AssetHttpFehlerPhaseAsync` anpassen: beide Routen auf 500 setzen + Fallback-Request-Assert + Doku | Erledigt | `End2EndTest.Startup_SafetyCancelAndErrorsRemainUsable` |
| 24 | E2E-Tests | `AssetFallbackErfolgPhaseAsync` neu schreiben und in `Startup_SafetyCancelAndErrorsRemainUsable` nach `LauncherFehlerPhaseAsync` einhängen (Doku der Testmethode ergänzen) | Erledigt | `End2EndTest.Startup_SafetyCancelAndErrorsRemainUsable` |
| 25 | Verifikation | `dotnet format Softwareschmiede.slnx`, vollständiger Build, `dotnet test ... --filter "Category!=OsInterface"` (in Sandbox mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`) | Erledigt | `test-results.md`: Build 0 Fehler/0 Warnungen, 2200/2200 Tests bestanden (1 plattformbedingter Skip), Format-Check sauber |
