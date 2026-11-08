# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### GitHubReleaseClient.cs (GitHubReleaseClient)

- **Kopplung und Erweiterbarkeit — Hardcodierte Werte** — Der GitHub-API-Host `api.github.com` steht nach dieser Änderung vierfach literal in der Datei: in der Basis-URL (Zeile 31), in der abgeleiteten Fallback-URL (Zeile 151) sowie zweimal als sicherheitsrelevanter Host-Pin in `IsAllowedFollowUpUri` (Zeile 160) und `IsAllowedAssetApiUrl` (Zeile 223). Da der Host an zwei Stellen für die URL-Allowlist entscheidend ist, birgt die Streuung als Zeichenkettenliteral ein Drift-Risiko.

  Empfehlung: Eine private Konstante für den Host (z. B. `private const string GitHubApiHost = "api.github.com";`) einführen und für die URL-Konstruktion einen kleinen privaten Helfer nutzen, sodass die Allowlist-Prüfungen und die Interpolation denselben Wert verwenden.

### UpdateE2EFixture.cs / E2E_UpdateSettings.cs (Fixture-JSON-Erzeugung)

- **Doppelter Code** — Das anonyme Asset-Objekt `{ name, url, id, browser_download_url }` ist fünfmal nahezu identisch ausgeprägt: zweimal in `UpdateE2EFixture.ErzeugeReleaseListenJson` (Zeilen 471–478 und 491–498) sowie dreimal in `E2E_UpdateSettings.cs` (`ErzeugeRcUndHoeheresStableReleaseJson`, Zeilen 1480–1487 und 1497–1504, sowie `ErzeugeReleaseListenJson`, Zeilen 1521–1528). In Zeile 1500 wird die API-Asset-URL zusätzlich per String-Interpolation mit `new UpdateOptions()` neu zusammengebaut, statt die Fixture-Konstanten bzw. einen gemeinsamen Builder zu nutzen; analog bauen `ReleaseApiUrl`, `StableAssetApiUrl` und `RcAssetApiUrl` in `UpdateE2EFixture` (Zeilen 35–50) den gleichen URL-Stamm `https://api.github.com/repos/{Owner}/{Name}` dreimal unabhängig auf.

  Empfehlung: Einen gemeinsamen Fabrik-Helfer für das Asset-JSON (z. B. `private static object ErstelleAssetJson(string name, string apiUrl, long id, string downloadUrl)` auf der Fixture) einführen und alle fünf Stellen darauf umstellen; die URL-Bildung der drei `static readonly`-Felder über einen gemeinsamen privaten Builder (z. B. `AssetApiUrl(long id)`) führen.

### E2E_UpdateSettings.cs (End2EndTest.AssetFallbackErfolgPhaseAsync)

- **Fehlende Kapselung / Testqualität** — `AssetFallbackErfolgPhaseAsync` (Zeilen 882–917) implementiert den Installationsnachweis eigenständig neu und prüft dabei nur eine Teilmenge von `AssertiereInstallationsNachweis`: Es fehlen der `scriptPath`-Existenznachweis, der `-ExtractedDirectory`-Launcher-Argument-Check, die Singularität von `UpdateStartAttempt`/`UpdateStartSucceeded` und die geforderte Reihenfolge `ShutdownRequested` nach dem Launcher-Start. Die im Kommentar (Zeilen 876–881) genannte Begründung — der Helper erwarte genau einen Asset-Abruf — ließe sich über einen Parameter abbilden statt über eine abgespeckte Nachimplementierung.

  Empfehlung: `AssertiereInstallationsNachweis` so parameterisieren, dass die erwarteten Asset-URLs als Liste übergeben werden (Standard: eine URL), und die Phase (c3) über denselben vollständigen Nachweis mit `{ StableDownloadUrl, StableAssetApiUrl }` laufen lassen.

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs`
- `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs`
- `src/Softwareschmiede/Infrastructure/Services/Updates/UpdatePackageService.cs`
- `src/Softwareschmiede.App/Services/Testing/UpdateFixtureHttpMessageHandler.cs`
- `src/Softwareschmiede.Tests/Helpers/RoutingHttpHandler.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdatePackageServiceTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests.cs`
- `src/Softwareschmiede.Tests/App/Services/Testing/UpdateFixtureHttpMessageHandlerTests.cs`
- `src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_UpdateSettings.cs`
