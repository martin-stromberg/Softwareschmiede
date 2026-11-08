# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

Zweiter Review-Durchlauf: Alle 24 Code-Planelemente bleiben nach den Refactorings aus `review-code.1.md` vollständig und plangemäß im Working Tree umgesetzt; die Verifikation (Task 25) ist inzwischen erfolgt (`test-results.md`: Build 0 Fehler/0 Warnungen, stabile Lane 2200/2200 bestanden, 1 plattformbedingter Skip, Format-Check sauber).

## Umgesetzte Planelemente

### Produktivcode

- [x] `UpdateInfo` (`src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs:21-29`) — erweitert um optionalen 7. positionellen Parameter `Uri? FallbackDownloadUrl = null` inkl. XML-`<param>`-Doku
- [x] Feld `Url` (`string`, `[JsonPropertyName("url")]`) in `GitHubAsset` (`src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs:256-257`) — vorhanden
- [x] Feld `Id` (`long`, `[JsonPropertyName("id")]`) in `GitHubAsset` (`GitHubReleaseClient.cs:259-260`) — vorhanden
- [x] Methode `CreateFallbackDownloadUrl(GitHubAsset)` → `Uri?` in `GitHubReleaseClient` (`GitHubReleaseClient.cs:147-155`) — `url` bevorzugt (nach `IsAllowedAssetApiUrl`), sonst `id`-Ableitung über `RepositoryApiUrl($"/releases/assets/{asset.Id}")` bei `Id > 0`, sonst `null`
- [x] Methode `IsAllowedAssetApiUrl(string)` → `bool` (private static) in `GitHubReleaseClient` (`GitHubReleaseClient.cs:225-228`) — absolut + HTTPS + Host `api.github.com` via Konstante `GitHubApiHost` (OrdinalIgnoreCase)
- [x] `TryCreateCandidate` (`GitHubReleaseClient.cs:136-144`) — übergibt `CreateFallbackDownloadUrl(asset)` als letztes Argument an `new UpdateInfo(...)`
- [x] `PreparePackageAsync` (`src/Softwareschmiede/Infrastructure/Services/Updates/UpdatePackageService.cs:67`) — ruft `DownloadAsync(update, partialZipPath, progress, ct)` auf
- [x] `DownloadAsync` (`UpdatePackageService.cs:92-134`) — Signatur auf `UpdateInfo` umgestellt; Fallback exakt bei `!IsSuccessStatusCode && (int)StatusCode >= 500 && FallbackDownloadUrl is not null`; `LogWarning` mit Statuscode und beiden URLs; primäre Response disposed; genau ein Fallback-Versuch; `EnsureSuccessStatusCode()` auf der finalen Response; Streaming-Teil (81920-Buffer, `FileMode.CreateNew`, `ContentLength`-Fortschritt) unverändert
- [x] `SendDownloadRequestAsync(Uri, bool useApiHeaders, CancellationToken)` → `Task<HttpResponseMessage>` (private) in `UpdatePackageService` (`UpdatePackageService.cs:136-146`) — bei `useApiHeaders`: `User-Agent: Softwareschmiede/1.0` (`ProductInfoHeaderValue`) + `Accept: application/octet-stream` (`MediaTypeWithQualityHeaderValue`); `ResponseHeadersRead`; primärer Request bleibt headerlos
- [x] `Klassifiziere` in `UpdateFixtureHttpMessageHandler` (`src/Softwareschmiede.App/Services/Testing/UpdateFixtureHttpMessageHandler.cs:122-131`) — `/releases/assets/` → `"asset"` vor der bestehenden `/releases`-Prüfung

### E2E-Infrastruktur

- [x] `StableAssetApiUrl`/`RcAssetApiUrl` (+ `StableAssetId = 91001`/`RcAssetId = 91002`) in `UpdateE2EFixture` (`src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs:34-50`) — `api.github.com`-Host; nach Refactoring über gemeinsame Fabriken gebaut: `RepositoryApiUrl`-Stamm (Zeilen 34-35) und `AssetApiUrl(long)` (Zeile 374); `ErstelleAssetJson`-Fabrik (Zeilen 381-388) erzeugt die Asset-JSON-Objekte
- [x] `ErzeugeReleaseListenJson` in `UpdateE2EFixture` (`UpdateE2EFixture.cs:478-511`) — beide Asset-Objekte über `ErstelleAssetJson` mit `url` (jeweilige `*AssetApiUrl`) und `id` (feste ID) ergänzt
- [x] `SetzeStandardAntworten` (`UpdateE2EFixture.cs:168-179`) — `UpdateE2EAntwort`-Einträge für `StableAssetApiUrl` und `RcAssetApiUrl` (Status 200, `BodyDatei` des jeweiligen ZIPs)
- [x] `ErzeugeRcUndHoeheresStableReleaseJson` (`src/Softwareschmiede.Tests/E2E/E2E_UpdateSettings.cs:1459-1489`) — RC-Asset mit `RcAssetApiUrl`/`RcAssetId`; ungenutztes Stable-Asset mit eigener `api.github.com`-Assets-URL/ID `91999` via `UpdateE2EFixture.AssetApiUrl(91999)`
- [x] `ErzeugeReleaseListenJson` in `E2E_UpdateSettings.cs` (`E2E_UpdateSettings.cs:1492-1507`) — Stable-Asset mit `StableAssetApiUrl`/`StableAssetId` via `ErstelleAssetJson`
- [x] `AssetHttpFehlerPhaseAsync` (`E2E_UpdateSettings.cs:788-814`) — beide Routen (`StableDownloadUrl` + `StableAssetApiUrl`) auf 500; zusätzliches `Assert.Equal(1, ...)` für Request auf `StableAssetApiUrl`; XML-Doku angepasst
- [x] `AssetFallbackErfolgPhaseAsync` (`E2E_UpdateSettings.cs:882-903`) — neue Phase: primär 500, API-URL 200 → `RestartAppPreservingDatabase`, `StartupUpdateCompleted`, danach `AssertiereInstallationsNachweis` mit `weitereErwarteteAssetUrls: [StableAssetApiUrl]` (deckt je 1 Request pro URL, `PreparationCompleted`, Markerinhalt `E2E-Paket {StableVersion} (stable-marker)`, `UpdateProcessStartRecorded` = `"Erfolg"`, `ShutdownRequested` ab), danach Reset der primären Antwort auf 200
- [x] `AssertiereInstallationsNachweis` (`E2E_UpdateSettings.cs:1266-1312`) — um optionalen Parameter `weitereErwarteteAssetUrls` erweitert; **alle ursprünglichen Prüfungen vollständig erhalten**: exakter Asset-URL-Listenabgleich (`art == "asset"`), `PreparationCompleted`-Singularität + `extractedDirectory`-Version, `version.json`-Inhalt, Markerinhalt via `LesePaketExeInhalt`, `scriptPath`-Existenz, `UpdateProcessStartRecorded`-Singularität + `workingDirectory` + `-ExtractedDirectory`-Argument, `UpdateStartAttempt`/`UpdateStartSucceeded`-Singularität, `ShutdownRequested` nach Launcher-Seq
- [x] Eingehängt in `Startup_SafetyCancelAndErrorsRemainUsable` (`E2E_UpdateSettings.cs:672-674`) nach `LauncherFehlerPhaseAsync` + `ProzessStart`-Reset (Zeile 670), vor den T-09-Phasen; Test-XML-Doku um Punkt (c3) ergänzt (Zeilen 628-629)

### Tests / Test-Hilfsklassen

- [x] `RequestedAcceptHeaders` (`IReadOnlyList<string?>`) in `RoutingHttpHandler` (`src/Softwareschmiede.Tests/Helpers/RoutingHttpHandler.cs:15, 34, 66`) — protokolliert `request.Headers.Accept?.ToString()` parallel zu `RequestedUrls`
- [x] `CreateSut`-Überladung mit `HttpMessageHandler` in `UpdatePackageServiceTests` (`src/Softwareschmiede.Tests/Application/Services/Updates/UpdatePackageServiceTests.cs:219-231`); bestehende Überladung delegiert auf `BytesHttpHandler` (Zeile 217)
- [x] `CreateUpdateInfo` um `Uri? fallbackDownloadUrl = null` erweitert (`UpdatePackageServiceTests.cs:233-234`)
- [x] `PreparePackageAsync_ShouldUseFallbackDownloadUrl_WhenPrimaryReturns5xx` (`UpdatePackageServiceTests.cs:110-134`) — primär 500, Fallback 200+ZIP; `RequestedUrls` = [primär, fallback]; `RequestedAcceptHeaders[1]` enthält `application/octet-stream`; Paket vollständig vorbereitet
- [x] `PreparePackageAsync_ShouldThrow_WhenPrimaryAndFallbackReturn5xx` (`UpdatePackageServiceTests.cs:138-156`) — `HttpRequestException`, keine `release.zip`/`.download`-Artefakte, `RequestedUrls` = [primär, fallback]
- [x] `PreparePackageAsync_ShouldNotUseFallback_WhenPrimaryReturnsNon5xxError` (`UpdatePackageServiceTests.cs:160-180`) — primär 404 → sofort `HttpRequestException`; `RequestedUrls` = [primär]
- [x] `GetLatestReleaseAsync_ShouldReturnRelease_WhenAssetExists` erweitert (`src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests.cs:17-47`) — Asset-JSON mit `url`/`id`, Assert auf `Release.FallbackDownloadUrl`
- [x] `GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsMissing` (`GitHubReleaseClientTests.cs:51-73`)
- [x] `GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsInvalid` (`GitHubReleaseClientTests.cs:76-105`) — Theory: Fremdhost + `id` → `id`-Ableitung; ungültige `url` + `id` → `id`-Ableitung; Fremdhost ohne `id` → `FallbackDownloadUrl == null`, Release trotzdem geliefert
- [x] `SendAsync_KlassifiziertApiAssetUrlAlsAsset` in `UpdateFixtureHttpMessageHandlerTests` (`src/Softwareschmiede.Tests/App/Services/Testing/UpdateFixtureHttpMessageHandlerTests.cs:93-107`) — Request auf `.../releases/assets/{id}` schreibt `art = "asset"` ins Protokoll

### Verifikation

- [x] Task 25 — `dotnet build Softwareschmiede.slnx` erfolgreich (0 Warnungen, 0 Fehler); stabile Lane `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`: 2200/2200 bestanden, 1 plattformbedingter Skip; `dotnet format`-Check sauber — Nachweis in `test-results.md`

## Hinweise

- Die Refactorings aus `review-code.1.md` sind umgesetzt, ohne Plan-Elemente zu entfernen: `GitHubApiHost`-Konstante + `RepositoryApiUrl`-Builder in `GitHubReleaseClient` (Zeilen 13, 157-158 — auch `IsAllowedFollowUpUri` und die Basis-URL nutzen jetzt die Konstante), `ErstelleAssetJson`/`AssetApiUrl`-Fabriken in `UpdateE2EFixture`, `AssertiereInstallationsNachweis` mit optionalem `weitereErwarteteAssetUrls`-Parameter.
- Bewusste, abgenommene Planabweichung: `AssetFallbackErfolgPhaseAsync` nutzt nun doch `AssertiereInstallationsNachweis` — das war im Plan zunächst untersagt (wegen `Assert.Single(art == "asset")` bei zwei Asset-Requests), wurde aber im Code-Review ausdrücklich empfohlen und ist durch die Parameterisierung semantisch korrekt (geordneter Listenabgleich `[primär, fallback]` statt Singularität). Der Nachweisumfang ist dadurch größer als geplant, nicht kleiner.
- `StableAssetApiUrl`/`RcAssetApiUrl` sind weiterhin `public static readonly`-Felder statt Properties — funktional deckungsgleich mit dem Plan.
- Alle Validierungsregeln des Plans (5xx-only-Auslöser vor Stream-Beginn, `api.github.com`-Pinning, `Id > 0`, Fehlerpfad via `EnsureSuccessStatusCode`) sind unverändert im Code abgebildet.
