# Bestandsaufnahme: Update-Download mit Fallback auf die GitHub-API-Asset-URL

Bestandsaufnahme der Update-Pipeline (Release-Abfrage, Paket-Download, Vorbereitung, UI-Fluss sowie Unit-Test- und E2E-Infrastruktur) bezogen auf die Anforderung aus `requirement.md`: Bei transienten 5xx-Fehlern der `browser_download_url` soll der Paket-Download einmalig auf den GitHub-API-Asset-Pfad ausweichen.

## Zusammenfassung

- **`UpdateInfo`** (`UpdateModels.cs:21-27`) ist ein positionelles Record mit genau sechs Parametern (`Version`, `TagName`, `AssetName`, `DownloadUrl`, `PublishedAt`, `IsPrerelease`) — eine Fallback-URL existiert noch nicht. Es gibt **genau eine Produktiv-Konstruktionsstelle** (`GitHubReleaseClient.TryCreateCandidate`, `GitHubReleaseClient.cs:135-141`) und **zehn Konstruktionsstellen in Tests** (direkt und über Hilfsmethoden, siehe [Tests](inventory/tests.md)).
- **`GitHubAsset`** (`GitHubReleaseClient.cs:227-234`) deserialisiert derzeit nur `name` und `browser_download_url` — die Asset-Felder `url` (API-Asset-URL) und `id` werden noch nicht gelesen.
- **`UpdatePackageService.DownloadAsync`** (`UpdatePackageService.cs:91-118`) ist ein einstufiger Download: ein `HttpRequestMessage`-GET **ohne jegliche Header** über den geteilten `HttpClient`, `EnsureSuccessStatusCode()`, dann Streaming in `{AssetName}.download` (`FileMode.CreateNew`) mit `ContentLength`-basierter Fortschrittsmeldung. Es gibt keinen Retry, keinen zweiten URL-Pfad und kein Timeout (anders als `GitHubReleaseClient`, der `UpdateOptions.CheckTimeout` per linked CTS anwendet).
- Der geteilte `HttpClient` wird in `App.xaml.cs:313-314` als nackter Singleton (`new HttpClient()`) registriert — **keine Default-Header**. `GitHubReleaseClient` setzt `User-Agent: Softwareschmiede/1.0` und `Accept: application/vnd.github+json` pro Request; der Paket-Download setzt nichts.
- **Aufrufkette:** `MainWindowUpdateFlow.InstalliereUpdateAsync` → `IUpdateService.PrepareUpdateAsync` → `UpdatePackageService.PreparePackageAsync` → `DownloadAsync(update.DownloadUrl, ...)`. Alle Interface-Signaturen reichen bereits die komplette `UpdateInfo` durch — ein zusätzliches Feld am Record ändert keine Contracts.
- **Fehlerverhalten bei fehlgeschlagenem Download:** `EnsureSuccessStatusCode` wirft `HttpRequestException` → `PreparePackageAsync` räumt partielle Dateien auf und wirft weiter → `MainWindowUpdateFlow` fängt `Exception` und zeigt „Update konnte nicht vorbereitet werden." (`MainWindowUpdateFlow.cs:334-339`).
- **E2E-Infrastruktur:** `UpdateFixtureHttpMessageHandler` bildet exakte Request-URLs via `UpdateE2ESzenario.FindeAntwort` (OrdinalIgnoreCase) auf Antworten ab; unbekannte URLs werfen `HttpRequestException` ohne Netzwerkfallback. Die Fixture-JSONs (`UpdateE2EFixture.ErzeugeReleaseListenJson`, `E2E_UpdateSettings` Zeilen 1413-1452) erzeugen Assets nur mit `browser_download_url`. Wichtig: `Klassifiziere` ordnet eine API-Asset-URL (`.../releases/assets/{id}`) wegen des Teilstrings `/releases` der Art `release` zu, nicht `asset` — und `AssertiereInstallationsNachweis` erwartet `Assert.Single` auf `art == "asset"`.
- **E2E-Test `AssetHttpFehlerPhaseAsync`** (`E2E_UpdateSettings.cs:781-802`) simuliert bereits einen Asset-500er und erwartet genau einen `HttpRequest` auf die Stable-URL plus `PreparationFailed` — diese Phase deckt den heutigen Fehlerpfad ab, den ein Fallback verändern würde.
- **Unit-Test-Handler:** `BytesHttpHandler` (privat in `UpdatePackageServiceTests`) beantwortet **jede** Anfrage mit demselben Status/Body — URL-unterscheidende Fallback-Tests sind damit nicht abbildbar; `RoutingHttpHandler` (`Tests/Helpers`) routet dagegen exakt per URL und protokolliert `RequestedUrls`.

## Details

- [Datenmodell](inventory/models.md) — `UpdateInfo`, `UpdateOptions`, `GitHubRelease`/`GitHubAsset`, Ergebnis-Records, E2E-Szenariomodell
- [Logik](inventory/logic.md) — `GitHubReleaseClient`, `UpdatePackageService`, `UpdateService`, `MainWindowUpdateFlow`, `UpdateFixtureHttpMessageHandler`, `UpdateE2ETestKontext`, `ProtokollierenderUpdateService`, `UpdateE2EFixture`, DI-Registrierung
- [Enums](inventory/enums.md) — `UpdateMode`, `UpdateKanal`, `UpdateCheckStatus`, `UpdatePreparationPhase`
- [Interfaces](inventory/interfaces.md) — `IUpdateReleaseClient`, `IUpdatePackageService`, `IUpdateService` und übrige Update-Contracts
- [Tests](inventory/tests.md) — Unit-Testklassen, Test-Hilfsklassen, E2E-Fixture und E2E-Szenarien
