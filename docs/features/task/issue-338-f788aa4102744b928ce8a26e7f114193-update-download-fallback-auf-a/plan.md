# Umsetzungsplan: Update-Download mit Fallback auf die GitHub-API-Asset-URL

## Übersicht

`UpdateInfo` wird um eine optionale Fallback-Download-URL (GitHub-API-Asset-Pfad) erweitert, die `GitHubReleaseClient` aus den Asset-Feldern `url`/`id` ableitet. `UpdatePackageService.DownloadAsync` wird zweistufig: Liefert die primäre `browser_download_url` einen 5xx-Status, wird einmalig auf den API-Asset-Pfad (`Accept: application/octet-stream`, `User-Agent`) ausgewichen; scheitert auch dieser, bleibt das bisherige Fehlerverhalten (`HttpRequestException` → „Update konnte nicht vorbereitet werden") unverändert. Betroffen sind `UpdateModels.cs`, `GitHubReleaseClient.cs`, `UpdatePackageService.cs`, die E2E-Testinfrastruktur (`UpdateFixtureHttpMessageHandler`, `UpdateE2EFixture`, `E2E_UpdateSettings`) sowie Unit-Tests. Keine UI- und keine Contract-Änderungen.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| `UpdateInfo`-Erweiterung | `Uri? FallbackDownloadUrl = null` als optionaler 7. positioneller Parameter am Ende des Records | `UpdateCheckResult` nutzt in derselben Datei bereits optionale positionelle Parameter (`UpdateInfo? Update = null`). Mit Defaultwert bleiben alle 10 bestehenden Konstruktionsstellen kompilierfähig; nur die Produktiv-Stelle (`TryCreateCandidate`) setzt den Wert explizit. |
| Herkunft der Fallback-URL | `GitHubAsset.url` bevorzugt (validiert); bei fehlendem/ungültigem `url` Ableitung aus `GitHubAsset.id` als `https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/assets/{id}`; sonst `null` | GitHub liefert `url` immer direkt mit. `id` ist die dokumentierte Alternative und per Konstruktion auf `api.github.com` gepinnt. Das Asset bleibt auch ohne gültige Fallback-URL nutzbar (`FallbackDownloadUrl = null`). |
| Validierung der `url` | Neue `IsAllowedAssetApiUrl`-Prüfung: absolut + HTTPS + Host `api.github.com` (OrdinalIgnoreCase) | Konsistent zum Host-/Schema-Pinning in `IsAllowedFollowUpUri` — die servergelieferte URL darf den Download nicht auf einen Fremdhost lenken. |
| `DownloadAsync`-Signatur | `DownloadAsync(UpdateInfo update, string partialZipPath, IProgress<UpdatePreparationProgress>? progress, CancellationToken ct)` | `PreparePackageAsync` besitzt die `UpdateInfo` bereits; beide URLs liegen in einem Objekt. Private Methode — kein Contract-Bruch. |
| Fallback-Auslöser | Nur empfangene HTTP-Statuscodes ≥ 500 (5xx) auf der Response, d. h. vor Stream-Beginn | Wortlaut der Anforderung („transiente 5xx-Fehler") und geklärte Entscheidung: Kein Fallback bei Transportfehlern ohne HTTP-Antwort, `429`, Timeout oder Abbruch mitten im Stream. Zu diesem Zeitpunkt existiert die `.download`-Datei noch nicht (`FileMode.CreateNew` erst nach Statusprüfung) — der Fallback ist immer sauber, ohne Teildatei-Behandlung; ein Neustart mitten im Stream brächte Teildatei-Löschung und Fortschrittsrücksprung mit sich und bleibt bewusst außen vor. |
| Fallback-Request-Header | `Accept: application/octet-stream` (`MediaTypeWithQualityHeaderValue`) + `User-Agent: Softwareschmiede/1.0` (`ProductInfoHeaderValue`) nur auf dem Fallback-Request | Die GitHub-API liefert ohne `Accept: application/octet-stream` JSON-Metadaten statt des Binärstroms und lehnt Requests ohne `User-Agent` mit 403 ab. Der geteilte `HttpClient` setzt keine Default-Header — der Stil folgt `GitHubReleaseClient` (Zeilen 47-49). Der primäre Request bleibt headerlos wie bisher. |
| Retry-Strategie | Genau ein Fallback-Versuch, keine Schleife, keine Verzögerung, keine mehrstufige Strategie | Anforderung: „automatisch einmalig auf diesen Fallback-Pfad ausweichen" — geklärte Entscheidung. |
| Sichtbarkeit des Fallbacks | Kein UI-Hinweis; nur `LogWarning` im Protokoll | Geklärte Entscheidung: Der Fallback ist ein interner, nicht sichtbarer Mechanismus; `MainWindowUpdateFlow`, Fortschrittsdialog und `UpdateHinweis` bleiben unverändert. |
| Geltungsbereich | Fallback ausschließlich in `UpdatePackageService.DownloadAsync` | Geklärte Entscheidung: Einzige Stelle, die eine `browser_download_url` abruft; `GitHubReleaseClient` nutzt bereits den API-Pfad — keine weiteren Download-Stellen. |
| `Klassifiziere` im `UpdateFixtureHttpMessageHandler` | Prüfung `url.Contains("/releases/assets/")` → `"asset"` vor der bestehenden `/releases`-Prüfung | Eine API-Asset-URL `.../releases/assets/{id}` enthält `/releases` und würde sonst als `"release"` protokolliert — semantisch ist sie aber ein Asset-Download. Mit der Korrektur liefern `art == "asset"`-Auswertungen die ehrliche Anzahl tatsächlicher Paket-Downloads. |
| E2E-Default-Antworten | Die API-Asset-URLs werden in `SetzeStandardAntworten` regulär registriert (Status 200 + ZIP-`BodyDatei`) | Spiegelt die Produktivsituation (beide Routen liefern dasselbe Paket). Fehlerszenarien setzen den Status der jeweiligen Antwort explizit — Muster wie bei `AssetHttpFehlerPhaseAsync`/`DefektesZipPhaseAsync`. |

## Programmabläufe

### Release-Kandidat mit Fallback-URL aufbauen

1. `GitHubReleaseClient.GetLatestReleaseAsync` paginiert die Release-Liste und ruft pro Release `TryCreateCandidate` auf (unverändert).
2. `TryCreateCandidate` wählt das Asset wie bisher (`Name == _options.AssetName` OrdinalIgnoreCase + `IsAbsoluteHttpsUrl(BrowserDownloadUrl)`).
3. Neu: `CreateFallbackDownloadUrl(asset)` liefert die optionale Fallback-URL:
   - `asset.Url` besteht `IsAllowedAssetApiUrl` (absolut, HTTPS, Host `api.github.com`) → `new Uri(asset.Url, UriKind.Absolute)`.
   - Sonst: `asset.Id > 0` → `new Uri($"https://api.github.com/repos/{_options.RepositoryOwner}/{_options.RepositoryName}/releases/assets/{asset.Id}")`.
   - Sonst: `null`.
4. `TryCreateCandidate` übergibt die abgeleitete URL als letztes Argument an `new UpdateInfo(...)` (Zeile 135-141).

Beteiligte Klassen/Komponenten: `GitHubReleaseClient`, `GitHubAsset`, `UpdateInfo`, `UpdateOptions` (`RepositoryOwner`/`RepositoryName`)

### Paket-Download mit einmaligem Fallback

1. `PreparePackageAsync` ruft `DownloadAsync(update, partialZipPath, progress, ct)` auf (statt bisher `update.DownloadUrl`).
2. `DownloadAsync` sendet via `SendDownloadRequestAsync(update.DownloadUrl, useApiHeaders: false, ct)` einen GET ohne Header (`HttpCompletionOption.ResponseHeadersRead`).
3. Liefert die Response `IsSuccessStatusCode == false` **und** `(int)response.StatusCode >= 500` **und** `update.FallbackDownloadUrl is not null`:
   - `_logger.LogWarning(...)` mit Statuscode und beiden URLs (Konvention wie übrige Warnungen des Service).
   - Die primäre Response wird disposed.
   - `SendDownloadRequestAsync(update.FallbackDownloadUrl, useApiHeaders: true, ct)` sendet den zweiten GET mit `Accept: application/octet-stream` und `User-Agent: Softwareschmiede/1.0`.
4. Auf der finalen Response wird `EnsureSuccessStatusCode()` aufgerufen — wirft wie bisher `HttpRequestException`, wenn auch der Fallback (oder die primäre URL ohne Fallback-Option) scheitert. Die Exception läuft über den bestehenden Catch-all in `PreparePackageAsync` (Aufräumen der Partials) zu `MainWindowUpdateFlow` → „Update konnte nicht vorbereitet werden."
5. Der Streaming-Teil (81920-Byte-Buffer, `FileMode.CreateNew`, `ContentLength`-basierte Fortschrittsmeldung `UpdatePreparationPhase.Download`) bleibt unverändert und läuft für beide Pfade identisch; `total` stammt aus der jeweils erfolgreichen Response.

Beteiligte Klassen/Komponenten: `UpdatePackageService`, `UpdateInfo`, `HttpClient` (geteilter Singleton), `UpdatePreparationProgress`

## Neue Klassen

Keine — die Änderung erweitert ausschließlich bestehende Klassen und Test-Infrastruktur.

## Änderungen an bestehenden Klassen

### `UpdateInfo` (Datenmodell, `UpdateModels.cs` Zeilen 21-27)

- **Neue Parameter/Eigenschaften:** `FallbackDownloadUrl` (`Uri?`, optionaler letzter positioneller Parameter `= null`) — GitHub-API-Asset-URL für den einmaligen Download-Fallback bei 5xx der primären `DownloadUrl`; XML-`<param>`-Doku ergänzen.

### `GitHubAsset` (private geschachtelte Klasse in `GitHubReleaseClient.cs` Zeilen 227-234)

- **Neue Eigenschaften:** `Url` (`string`, `[JsonPropertyName("url")]`, Default `string.Empty`) — API-Asset-URL des GitHub-Schemas.
- **Neue Eigenschaften:** `Id` (`long`, `[JsonPropertyName("id")]`) — numerische Asset-ID als Bezugsgröße für die URL-Ableitung.

### `GitHubReleaseClient` (`GitHubReleaseClient.cs`)

- **Neue Methoden:** `CreateFallbackDownloadUrl(GitHubAsset asset)` → `Uri?` — Ableitungslogik: `url` bevorzugt (nach `IsAllowedAssetApiUrl`), sonst `id`-basiert aufgebaut, sonst `null`.
- **Neue Methoden:** `IsAllowedAssetApiUrl(string url)` → `bool` (private static) — `Uri.TryCreate` absolut + Scheme HTTPS + Host `api.github.com` (OrdinalIgnoreCase).
- **Geänderte Methoden:** `TryCreateCandidate` (Zeilen 125-141) — nach der Asset-Auswahl `CreateFallbackDownloadUrl(asset)` aufrufen und als letztes Argument an `new UpdateInfo(...)` übergeben.

### `UpdatePackageService` (`UpdatePackageService.cs`)

- **Geänderte Methoden:** `PreparePackageAsync` (Zeile 66) — `await DownloadAsync(update, partialZipPath, progress, ct)` statt `update.DownloadUrl`.
- **Geänderte Methoden:** `DownloadAsync` (Zeilen 91-118) — Signatur auf `(UpdateInfo update, string partialZipPath, IProgress<UpdatePreparationProgress>? progress, CancellationToken ct)`; zweistufiger Ablauf gemäß Programmablauf „Paket-Download mit einmaligem Fallback"; `LogWarning` beim Umschalten mit Statuscode und beiden URLs.
- **Neue Methoden:** `SendDownloadRequestAsync(Uri url, bool useApiHeaders, CancellationToken ct)` → `Task<HttpResponseMessage>` (private) — baut `HttpRequestMessage(HttpMethod.Get, url)`; bei `useApiHeaders == true` werden `User-Agent: Softwareschmiede/1.0` und `Accept: application/octet-stream` gesetzt; `SendAsync` mit `HttpCompletionOption.ResponseHeadersRead`.

### `UpdateFixtureHttpMessageHandler` (E2E-Infrastruktur, App-Projekt)

- **Geänderte Methoden:** `Klassifiziere` (Zeilen 122-129) — vor der `/releases`-Prüfung zuerst `url.Contains("/releases/assets/", StringComparison.OrdinalIgnoreCase)` → `"asset"` prüfen.

### `UpdateE2EFixture` (Runner-Fixture, `src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs`)

- **Neue Eigenschaften:** `StableAssetApiUrl` (`string`) und `RcAssetApiUrl` (`string`) — API-Asset-URLs der Form `https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/assets/{id}` mit festen IDs (z. B. `91001`/`91002`); muss Host `api.github.com` tragen, damit `IsAllowedAssetApiUrl` die URL akzeptiert.
- **Geänderte Methoden:** `ErzeugeReleaseListenJson` (Zeilen 433-466) — Asset-Objekte um `url` (die jeweilige `*AssetApiUrl`) und `id` (die zugehörige feste ID) ergänzen, damit `TryCreateCandidate` eine `FallbackDownloadUrl` liefert.
- **Geänderte Methoden:** `SetzeStandardAntworten` (Zeilen 133-154) — zusätzliche `UpdateE2EAntwort`-Einträge für `StableAssetApiUrl` und `RcAssetApiUrl` (Status 200, `BodyDatei` des jeweiligen ZIPs).

### `E2E_UpdateSettings` (`src/Softwareschmiede.Tests/E2E/E2E_UpdateSettings.cs`)

- **Geänderte Methoden:** `ErzeugeRcUndHoeheresStableReleaseJson` (Zeilen 1413-1435) und `ErzeugeReleaseListenJson` (Zeilen 1438-1452) — Asset-Objekte um `url`/`id` ergänzen (RC → `fixture.RcAssetApiUrl`, Stable → `fixture.StableAssetApiUrl`, ungenutztes Stable-Asset → eigene `api.github.com`-Assets-URL/ID).
- **Geänderte Methoden:** `AssetHttpFehlerPhaseAsync` (Zeilen 781-802) — zusätzlich die Antwort auf `fixture.StableAssetApiUrl` auf `Status = 500` setzen (beide Routen scheitern) und `Assert.Equal(1, ...)` für `HttpRequest` mit `url == fixture.StableAssetApiUrl` ergänzen; XML-Doku anpassen.
- **Neue Methoden:** `AssetFallbackErfolgPhaseAsync(UpdateE2EFixture fixture)` — neue Phase in `Startup_SafetyCancelAndErrorsRemainUsable` (E-06), siehe Programmablauf/E2E-Tests. Eigene Assertions — **nicht** `AssertiereInstallationsNachweis` verwenden, da dessen `Assert.Single(art == "asset")` bei erfolgreichem Fallback zwei Asset-Requests sieht.
- **Geänderter Ablauf:** `Startup_SafetyCancelAndErrorsRemainUsable` (Zeilen 635-697) — Aufruf der neuen Phase nach `LauncherFehlerPhaseAsync` und dem `ProzessStart → "Erfolg"`-Reset (Zeile 668), vor den T-09-Phasen; Test-XML-Doku um Punkt (c3) ergänzen.

### `RoutingHttpHandler` (Test-Hilfsklasse, `src/Softwareschmiede.Tests/Helpers/RoutingHttpHandler.cs`)

- **Neue Eigenschaften:** `RequestedAcceptHeaders` (`IReadOnlyList<string?>`) — pro Request parallel zu `_requestedUrls` den Wert von `request.Headers.Accept?.ToString()` protokollieren; ermöglicht den Nachweis von `Accept: application/octet-stream` auf dem Fallback-Request.

### `UpdatePackageServiceTests` (`src/Softwareschmiede.Tests/Application/Services/Updates/UpdatePackageServiceTests.cs`)

- **Geänderte Hilfsmethoden:** `CreateUpdateInfo` (Zeilen 153-154) — optionaler Parameter `Uri? fallbackDownloadUrl = null`, wird als letztes Argument an das Record durchgereicht.
- **Geänderte Hilfsmethoden:** `CreateSut` (Zeilen 138-151) — Überladung mit `HttpMessageHandler`-Parameter, damit `RoutingHttpHandler` statt `BytesHttpHandler` injiziert werden kann (URL-differenzierte Antworten).

### `GitHubReleaseClientTests` (`src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests.cs`)

- **Geänderte Testdaten:** Release-JSON des Happy-Path-Tests um `url`/`id` der Assets ergänzen; Assertion `result.Release.FallbackDownloadUrl` hinzufügen.
- **Neue Testmethoden:** siehe Abschnitt „Tests".

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `GitHubAsset.url` | Absolute HTTPS-URL mit Host `api.github.com` (`IsAllowedAssetApiUrl`) | Ungültig → Ableitung aus `id` versuchen; ohne `id` → `FallbackDownloadUrl = null` (Asset bleibt nutzbar, kein Fallback) |
| `GitHubAsset.id` | `> 0` für die URL-Ableitung | `<= 0` oder fehlend → `FallbackDownloadUrl = null` |
| Download-Response (primär) | Fallback nur bei `(int)StatusCode >= 500` | 2xx → normaler Download; 4xx/nicht-5xx → `EnsureSuccessStatusCode` wirft sofort (kein Fallback); `FallbackDownloadUrl == null` → bisheriger Fehlerpfad |
| Download-Response (Fallback) | `EnsureSuccessStatusCode` wie bisher | Nicht-2xx → `HttpRequestException` → bisheriger Fehlerpfad („Update konnte nicht vorbereitet werden") |

## Konfigurationsänderungen

Keine. Der Fallback ist festes, immer aktives Verhalten; `UpdateOptions.RepositoryOwner`/`RepositoryName` liefern die Bausteine für die `id`-Ableitung; `CheckTimeout` bleibt unberührt (gilt weiterhin nur für den Release-Abruf).

## Seiteneffekte und Risiken

- **E2E `AssetHttpFehlerPhaseAsync`:** Sobald die Fixture-JSONs `url`/`id` liefern und `SetzeStandardAntworten` die API-URL registriert, würde der bisherige 500er auf der primären URL einen erfolgreichen Fallback auslösen (Installation läuft durch statt `PreparationFailed`). Die Phase muss zwingend mitangepasst werden (beide Antworten auf 500).
- **`AssertiereInstallationsNachweis`:** Durch die `Klassifiziere`-Korrektur werden API-Asset-Requests als `"asset"` protokolliert. In Happy-Path-Szenarien (primär 200) bleibt `Assert.Single(art == "asset")` korrekt. In Fallback-Erfolgsszenarien wären es zwei Asset-Requests — die neue Phase darf diese Hilfsmethode daher nicht verwenden.
- **`UpdateInfo`-Gleichheit/`ToString`:** Der neue Record-Member geht in Equality und `ToString` ein. Bestehende Vergleiche (`result.Update.Should().Be(update)`) nutzen dieselbe Instanz bzw. Standardwerte — unkritisch.
- **Test-Konstruktionsstellen:** Durch den optionalen Parameter bleiben `UpdateServiceTests` (Z. 15, 96-97), `UpdateServiceTests_Options` (Z. 15-16, 47-48), `MainWindowViewModelUpdateTestBase` (Z. 25-27) und `MainWindowViewModelTests_UpdateStartup` (Z. 25-27) ohne Änderung kompilierfähig — kein Churn.
- **Sonstige Release-JSON-Fixtures** (`GitHubReleaseClientTests_Filters`, `_Pagination`, `UpdateServiceTests_PrereleaseChain.ReleasesJson`): Bleiben ohne `url`/`id` gültig — `TryCreateCandidate` liefert dann `FallbackDownloadUrl = null`, Kandidaten werden weiter erzeugt. Keine Anpassung erforderlich.
- **E2E-Phasen mit primärer 2xx-Antwort** (`DownloadAbbruchPhaseAsync` StreamGate-Abbruch, `DefektesZipPhaseAsync`, T-09-Phasen, `Startup_ModesDriveUpdatePipeline`, `Fixture_UsesIsolatedRealUpdatePipeline`): Kein 5xx → kein Fallback-Request → unverändert. Die geklärte 5xx-only-Auslösung hält diese Tests stabil: Transportfehler und `StreamGate`-Abbrüche (wie in `DownloadAbbruchPhaseAsync`) lösen keinen Fallback-Request aus.

## Umsetzungsreihenfolge

1. **`UpdateInfo` um `FallbackDownloadUrl` erweitern**
   - Voraussetzungen: Keine.
   - Beschreibung: Optionalen Parameter `Uri? FallbackDownloadUrl = null` an `UpdateModels.cs` anhängen, XML-Doku ergänzen.

2. **`GitHubAsset` und Fallback-Ableitung in `GitHubReleaseClient`**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `Url`/`Id` an `GitHubAsset` deserialisieren; `IsAllowedAssetApiUrl` und `CreateFallbackDownloadUrl` implementieren; `TryCreateCandidate` übergibt die URL an `UpdateInfo`.

3. **`UpdatePackageService.DownloadAsync` zweistufig umbauen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `SendDownloadRequestAsync` (Header-Varianten) einführen; `DownloadAsync` auf `UpdateInfo` umstellen mit 5xx-Fallback + `LogWarning`; `PreparePackageAsync`-Aufruf anpassen.

4. **`Klassifiziere` im `UpdateFixtureHttpMessageHandler` erweitern**
   - Voraussetzungen: Keine.
   - Beschreibung: `/releases/assets/` → `"asset"` vor der `/releases`-Prüfung.

5. **`UpdateE2EFixture` um API-Asset-URLs erweitern**
   - Voraussetzungen: Schritt 2 (semantisch — die Fixture-URLs müssen der Validierungsregel `api.github.com` genügen); Schritt 4 für konsistente Protokollierung.
   - Beschreibung: `StableAssetApiUrl`/`RcAssetApiUrl`-Properties, `url`/`id` in `ErzeugeReleaseListenJson`, Standard-Antworten für beide API-URLs.

6. **`E2E_UpdateSettings`-JSON-Erzeuger ergänzen**
   - Voraussetzungen: Schritt 5 (Fixture-Properties).
   - Beschreibung: `url`/`id` in `ErzeugeRcUndHoeheresStableReleaseJson` und `ErzeugeReleaseListenJson`.

7. **Unit-Tests `UpdatePackageServiceTests` erweitern**
   - Voraussetzungen: Schritt 3; `RoutingHttpHandler` (`src/Softwareschmiede.Tests/Helpers/RoutingHttpHandler.cs`) vorhanden.
   - Beschreibung: `RequestedAcceptHeaders` an `RoutingHttpHandler`; `CreateUpdateInfo`/`CreateSut`-Überladungen; drei neue Fallback-Testmethoden.

8. **Unit-Tests `GitHubReleaseClientTests` erweitern**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: `url`/`id` im Happy-Path-JSON + `FallbackDownloadUrl`-Assert; neue Tests für `id`-Ableitung und ungültige `url`.

9. **Unit-Test `UpdateFixtureHttpMessageHandlerTests` ergänzen**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: Test, dass ein Request auf eine `.../releases/assets/{id}`-URL im Protokoll `art = "asset"` trägt.

10. **E2E-Phasen anpassen und ergänzen**
    - Voraussetzungen: Schritte 3, 5, 6.
    - Beschreibung: `AssetHttpFehlerPhaseAsync` auf „beide URLs 500" umstellen; neue `AssetFallbackErfolgPhaseAsync` in `Startup_SafetyCancelAndErrorsRemainUsable` einhängen.

11. **Build, Formatierung, Tests**
    - Voraussetzungen: Schritte 1-10.
    - Beschreibung: `dotnet format Softwareschmiede.slnx`, vollständiger Build, stabile Testlane `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` (bei E2E-Läufen in der Sandbox `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` setzen; niemals `Softwareschmiede.App.exe` beenden).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `PreparePackageAsync_ShouldUseFallbackDownloadUrl_WhenPrimaryReturns5xx` | `UpdatePackageServiceTests` | Primäre URL → 500, Fallback-URL → 200 + gültiges ZIP: Download+Entpacken+Skript erfolgreich; `RequestedUrls` = [primär, fallback]; `RequestedAcceptHeaders` des Fallback-Requests enthält `application/octet-stream` |
| `PreparePackageAsync_ShouldThrow_WhenPrimaryAndFallbackReturn5xx` | `UpdatePackageServiceTests` | Beide URLs → 500: `HttpRequestException`, weder `release.zip` noch `release.zip.download` bleiben; `RequestedUrls` = [primär, fallback] |
| `PreparePackageAsync_ShouldNotUseFallback_WhenPrimaryReturnsNon5xxError` | `UpdatePackageServiceTests` | Primäre URL → 404, Fallback konfiguriert: `HttpRequestException` sofort; `RequestedUrls` = [primär] (kein Fallback-Request) |
| `RequestedAcceptHeaders` (Erweiterung) | `RoutingHttpHandler` (Hilfsklasse) | Protokolliert `request.Headers.Accept` parallel zu `RequestedUrls` für den Header-Nachweis |
| `CreateSut`-/`CreateUpdateInfo`-Überladungen | `UpdatePackageServiceTests` (Hilfsmethoden) | Injektion eines `HttpMessageHandler` bzw. einer `FallbackDownloadUrl` |
| `GetLatestReleaseAsync_ShouldMapAssetApiUrlToFallbackDownloadUrl` (Erweiterung des Happy-Path-Tests `...ShouldReturnRelease_WhenAssetExists`) | `GitHubReleaseClientTests` | Asset-JSON mit `url`/`id` → `Release.FallbackDownloadUrl` = API-Asset-URL |
| `GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsMissing` | `GitHubReleaseClientTests` | Asset-JSON nur mit `id` → `FallbackDownloadUrl` = `https://api.github.com/repos/{owner}/{repo}/releases/assets/{id}` |
| `GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsInvalid` | `GitHubReleaseClientTests` | `url` auf Fremdhost/ungültig + `id` → `id`-Ableitung; Variante ohne `id` → `FallbackDownloadUrl == null`, Release wird trotzdem geliefert |
| `SendAsync_KlassifiziertApiAssetUrlAlsAsset` | `UpdateFixtureHttpMessageHandlerTests` | Request auf `.../releases/assets/{id}` schreibt `HttpRequest` mit `art = "asset"` ins Protokoll |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `UpdatePackageServiceTests.CreateUpdateInfo` / `CreateSut` (Hilfsmethoden) | Neue optionale Parameter für `FallbackDownloadUrl` und `HttpMessageHandler`-Injektion |
| `GitHubReleaseClientTests.GetLatestReleaseAsync_ShouldReturnRelease_WhenAssetExists` | JSON-Fixture um `url`/`id` ergänzen; `FallbackDownloadUrl`-Assert |

Keine weiteren: `UpdateServiceTests`, `UpdateServiceTests_Options`, `MainWindowViewModelUpdateTestBase`, `MainWindowViewModelTests_UpdateStartup`, `GitHubReleaseClientTests_Filters`, `GitHubReleaseClientTests_Pagination`, `UpdateServiceTests_PrereleaseChain` und `UpdatePackageServiceTests.PreparePackageAsync_ShouldKeepDownloadAtomic_WhenDownloadFails` bleiben durch den optionalen Record-Parameter bzw. fehlende `url`/`id`-Felder (`FallbackDownloadUrl = null`) unverändert gültig.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Primärer Download liefert 5xx → einmaliger Fallback auf API-Asset-URL → Installation läuft vollständig durch (PreparationCompleted, Launcher-Erfolg, ShutdownRequested, entpacktes Paket mit Stable-Marker); genau ein Request je URL | `E2E_UpdateSettings.cs` / `Startup_SafetyCancelAndErrorsRemainUsable` (E-06) — neue Phase `AssetFallbackErfolgPhaseAsync` zwischen `LauncherFehlerPhaseAsync`/`ProzessStart`-Reset und den T-09-Phasen | Happy Path des neuen Features: Der Fallback rettet den Download bei transientem 5xx |
| Primärer Download **und** Fallback liefern 5xx → `PreparationFailed`, Fehlertext „konnte nicht vorbereitet", kein Start/Shutdown | `E2E_UpdateSettings.cs` / `Startup_SafetyCancelAndErrorsRemainUsable` — angepasste Phase `AssetHttpFehlerPhaseAsync` | Unverändertes Fehlerverhalten, wenn auch der Fallback scheitert |

**Ablauf `AssetFallbackErfolgPhaseAsync` (geplant):** `StableDownloadUrl`-Antwort auf `Status = 500` setzen (`StableAssetApiUrl` bleibt via `SetzeStandardAntworten` auf 200+ZIP) → `RestartAppPreservingDatabase` → `StartupUpdateCompleted` abwarten → Assert: je 1 `HttpRequest` auf `StableDownloadUrl` und `StableAssetApiUrl`, 1 `PreparationCompleted`, `UpdateProcessStartRecorded` mit `"Erfolg"`, 1 `ShutdownRequested`, Markerinhalt `E2E-Paket 1.3.0 (stable-marker)` im entpackten Verzeichnis → anschließend `StableDownloadUrl`-Status auf 200 zurücksetzen und `(mainWindow, menu)` zurückgeben.

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AssetHttpFehlerPhaseAsync` (`E2E_UpdateSettings.cs`, in `Startup_SafetyCancelAndErrorsRemainUsable`) | Mit registrierter Fallback-Antwort würde der bisherige Einzel-500er auf der primären URL einen erfolgreichen Fallback auslösen; die Phase muss beide Routen auf 500 setzen und den Fallback-Request zusätzlich assertieren |

## Offene Punkte

Keine.
