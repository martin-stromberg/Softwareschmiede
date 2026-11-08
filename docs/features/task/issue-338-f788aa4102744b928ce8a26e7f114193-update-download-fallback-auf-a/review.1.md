# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Alle 24 Code-Planelemente (Tasks 1-24) sind vollständig und plangemäß im Working Tree umgesetzt (uncommitted Änderungen). Einzig die abschließende Verifikation (Task 25: `dotnet format`, Build, Testlauf) wurde noch nicht ausgeführt — sie ist im Lifecycle als Schritt 8b vorgesehen (`todo.md`) und `test-results.md` liegt noch nicht vor.

## Umgesetzte Planelemente

### Produktivcode

- [x] `UpdateInfo` (`src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs:21-29`) — erweitert um optionalen 7. positionellen Parameter `Uri? FallbackDownloadUrl = null` inkl. XML-`<param>`-Doku
- [x] Feld `Url` (`string`, `[JsonPropertyName("url")]`) in `GitHubAsset` (`src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs:251-252`) — vorhanden
- [x] Feld `Id` (`long`, `[JsonPropertyName("id")]`) in `GitHubAsset` (`GitHubReleaseClient.cs:254-255`) — vorhanden
- [x] Methode `CreateFallbackDownloadUrl(GitHubAsset)` → `Uri?` in `GitHubReleaseClient` (`GitHubReleaseClient.cs:145-153`) — `url` bevorzugt (nach `IsAllowedAssetApiUrl`), sonst `id`-Ableitung `https://api.github.com/repos/{owner}/{repo}/releases/assets/{id}` bei `Id > 0`, sonst `null`
- [x] Methode `IsAllowedAssetApiUrl(string)` → `bool` (private static) in `GitHubReleaseClient` (`GitHubReleaseClient.cs:220-223`) — absolut + HTTPS + Host `api.github.com` (OrdinalIgnoreCase)
- [x] `TryCreateCandidate` (`GitHubReleaseClient.cs:135-142`) — übergibt `CreateFallbackDownloadUrl(asset)` als letztes Argument an `new UpdateInfo(...)`
- [x] `PreparePackageAsync` (`src/Softwareschmiede/Infrastructure/Services/Updates/UpdatePackageService.cs:67`) — ruft `DownloadAsync(update, partialZipPath, progress, ct)` auf
- [x] `DownloadAsync` (`UpdatePackageService.cs:92-134`) — Signatur auf `UpdateInfo` umgestellt; Fallback exakt bei `!IsSuccessStatusCode && (int)StatusCode >= 500 && FallbackDownloadUrl is not null`; `LogWarning` mit Statuscode und beiden URLs; primäre Response disposed; genau ein Fallback-Versuch; `EnsureSuccessStatusCode()` auf der finalen Response; Streaming-Teil (81920-Buffer, `FileMode.CreateNew`, `ContentLength`-Fortschritt) unverändert
- [x] `SendDownloadRequestAsync(Uri, bool useApiHeaders, CancellationToken)` → `Task<HttpResponseMessage>` (private) in `UpdatePackageService` (`UpdatePackageService.cs:136-146`) — bei `useApiHeaders`: `User-Agent: Softwareschmiede/1.0` (`ProductInfoHeaderValue`) + `Accept: application/octet-stream` (`MediaTypeWithQualityHeaderValue`); `ResponseHeadersRead`; primärer Request bleibt headerlos
- [x] `Klassifiziere` in `UpdateFixtureHttpMessageHandler` (`src/Softwareschmiede.App/Services/Testing/UpdateFixtureHttpMessageHandler.cs:124-125`) — `/releases/assets/` → `"asset"` vor der bestehenden `/releases`-Prüfung

### E2E-Infrastruktur

- [x] `StableAssetApiUrl`/`RcAssetApiUrl` (+ `StableAssetId = 91001`/`RcAssetId = 91002`) in `UpdateE2EFixture` (`src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs:38-50`) — `api.github.com`-Host, statische Member (statt Instanz-Properties, funktional gleichwertig; Aufrufe als `UpdateE2EFixture.StableAssetApiUrl`)
- [x] `ErzeugeReleaseListenJson` in `UpdateE2EFixture` (`UpdateE2EFixture.cs:468-501`) — beide Asset-Objekte um `url` (jeweilige `*AssetApiUrl`) und `id` (feste ID) ergänzt
- [x] `SetzeStandardAntworten` (`UpdateE2EFixture.cs:168-180`) — `UpdateE2EAntwort`-Einträge für `StableAssetApiUrl` und `RcAssetApiUrl` (Status 200, `BodyDatei` des jeweiligen ZIPs)
- [x] `ErzeugeRcUndHoeheresStableReleaseJson` (`src/Softwareschmiede.Tests/E2E/E2E_UpdateSettings.cs:1475-1505`) — RC-Asset mit `RcAssetApiUrl`/`RcAssetId`; ungenutztes Stable-Asset mit eigener `api.github.com`-Assets-URL/ID `91999`
- [x] `ErzeugeReleaseListenJson` in `E2E_UpdateSettings.cs` (`E2E_UpdateSettings.cs:1518-1528`) — Stable-Asset mit `StableAssetApiUrl`/`StableAssetId`
- [x] `AssetHttpFehlerPhaseAsync` (`E2E_UpdateSettings.cs:784-819`) — beide Routen (`StableDownloadUrl` + `StableAssetApiUrl`) auf 500; zusätzliches `Assert.Equal(1, ...)` für Request auf `StableAssetApiUrl`; XML-Doku angepasst
- [x] `AssetFallbackErfolgPhaseAsync` (`E2E_UpdateSettings.cs:876-918`) — neue Phase: primär 500, API-URL 200 → `RestartAppPreservingDatabase`, `StartupUpdateCompleted`, je 1 Request pro URL, 1 `PreparationCompleted`, Markerinhalt `E2E-Paket {StableVersion} (stable-marker)` via `LesePaketExeInhalt`, `UpdateProcessStartRecorded` = `"Erfolg"`, 1 `ShutdownRequested`, danach Reset der primären Antwort auf 200; eigene Assertions (nicht `AssertiereInstallationsNachweis`)
- [x] Eingehängt in `Startup_SafetyCancelAndErrorsRemainUsable` (`E2E_UpdateSettings.cs:672-674`) nach `LauncherFehlerPhaseAsync` + `ProzessStart`-Reset, vor den T-09-Phasen; Test-XML-Doku um Punkt (c3) ergänzt

### Tests / Test-Hilfsklassen

- [x] `RequestedAcceptHeaders` (`IReadOnlyList<string?>`) in `RoutingHttpHandler` (`src/Softwareschmiede.Tests/Helpers/RoutingHttpHandler.cs:15, 34, 66`) — protokolliert `request.Headers.Accept?.ToString()` parallel zu `RequestedUrls`
- [x] `CreateSut`-Überladung mit `HttpMessageHandler` in `UpdatePackageServiceTests` (`src/Softwareschmiede.Tests/Application/Services/Updates/UpdatePackageServiceTests.cs:219-231`); bestehende Überladung delegiert auf `BytesHttpHandler`
- [x] `CreateUpdateInfo` um `Uri? fallbackDownloadUrl = null` erweitert (`UpdatePackageServiceTests.cs:233-234`)
- [x] `PreparePackageAsync_ShouldUseFallbackDownloadUrl_WhenPrimaryReturns5xx` (`UpdatePackageServiceTests.cs:110-134`) — primär 500, Fallback 200+ZIP; `RequestedUrls` = [primär, fallback]; `RequestedAcceptHeaders[1]` enthält `application/octet-stream`; Paket vollständig vorbereitet
- [x] `PreparePackageAsync_ShouldThrow_WhenPrimaryAndFallbackReturn5xx` (`UpdatePackageServiceTests.cs:138-156`) — `HttpRequestException`, keine `release.zip`/`.download`-Artefakte, `RequestedUrls` = [primär, fallback]
- [x] `PreparePackageAsync_ShouldNotUseFallback_WhenPrimaryReturnsNon5xxError` (`UpdatePackageServiceTests.cs:160-180`) — primär 404 → sofort `HttpRequestException`; `RequestedUrls` = [primär]
- [x] `GetLatestReleaseAsync_ShouldReturnRelease_WhenAssetExists` erweitert (`src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests.cs:27-46`) — Asset-JSON mit `url`/`id`, Assert auf `Release.FallbackDownloadUrl`
- [x] `GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsMissing` (`GitHubReleaseClientTests.cs:48-73`)
- [x] `GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsInvalid` (`GitHubReleaseClientTests.cs:75-105`) — Theory: Fremdhost + `id` → `id`-Ableitung; ungültige `url` + `id` → `id`-Ableitung; Fremdhost ohne `id` → `FallbackDownloadUrl == null`, Release trotzdem geliefert
- [x] `SendAsync_KlassifiziertApiAssetUrlAlsAsset` in `UpdateFixtureHttpMessageHandlerTests` (`src/Softwareschmiede.Tests/App/Services/Testing/UpdateFixtureHttpMessageHandlerTests.cs:92-107`) — Request auf `.../releases/assets/{id}` schreibt `art = "asset"` ins Protokoll

## Offene Aufgaben

- [ ] `Task 25 (Verifikation)` — `dotnet format Softwareschmiede.slnx`, vollständiger Build und stabile Testlane `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`) wurden noch nicht ausgeführt; `test-results.md` liegt nicht vor (Lifecycle-Schritt 8b in `todo.md` noch offen)

## Hinweise

- `StableAssetApiUrl`/`RcAssetApiUrl` sind als `public static readonly string`-Felder statt als Properties realisiert — inhaltlich deckungsgleich mit dem Plan (lesender Zugriff auf feste `api.github.com`-URLs); kein Handlungsbedarf.
- `AssetFallbackErfolgPhaseAsync` nutzt `EintraegeSeit`/`DatenText` statt `ZaehleEreignisSeit` für die Request-Zählung — gleichwertige Zählweise, plan-konform (eigene Assertions statt `AssertiereInstallationsNachweis`).
- Alle Validierungsregeln aus dem Plan (5xx-only-Auslöser vor Stream-Beginn, `api.github.com`-Pinning, `id > 0`, Fehlerpfad via `EnsureSuccessStatusCode`) sind im Code exakt abgebildet; kein Fallback bei 4xx/Transportfehlern.
