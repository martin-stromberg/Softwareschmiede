# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### UpdateE2EFixture.cs (UpdateE2EFixture)

- **Doppelter Code** — `new UpdateOptions()` wird allein zum Lesen von Standardwerten vierfach instanziiert: zweimal in derselben Interpolation von `RepositoryApiUrl` (Zeile 35), sowie in `ErstelleAssetJson` (Zeile 384), `ErstellePaketZip` (Zeile 402) und `LesePaketExeInhalt` (Zeile 419). Mindestens die Dopplung in Zeile 35 erzeugt zwei Instanzen für einen einzigen Ausdruck.

  Empfehlung: Ein `private static readonly UpdateOptions StandardOptionen = new();` einführen und an allen vier Stellen wiederverwenden (in Zeile 35 dann einmal `StandardOptionen.RepositoryOwner`/`StandardOptionen.RepositoryName`).

- **Doppelter Code** — `SetzeStandardAntworten` (Zeilen 156–179) enthält nach der Erweiterung vier nahezu identische `_szenario.Antworten.Add`-Blöcke, die sich nur in `Url` und `BodyDatei` unterscheiden (Stable-/RC-Download-URL sowie Stable-/RC-Asset-API-URL, jeweils Status 200).

  Empfehlung: Die vier (Url, ZipPfad)-Paare in eine Schleife oder einen kleinen lokalen Helfer überführen, z. B. `foreach (var (url, zip) in new[] { (StableDownloadUrl, StableZipPfad), (RcDownloadUrl, RcZipPfad), (StableAssetApiUrl, StableZipPfad), (RcAssetApiUrl, RcZipPfad) })`.

- **Doppelter Code** — Auf Release-Ebene bleiben anonyme Literale mit identischer Feldstruktur (`tag_name`, `prerelease`, `draft`, `published_at`, `assets`) fünffach erhalten: zweimal in `ErzeugeReleaseListenJson` (Zeilen 482–492 und 497–507) sowie dreimal in `E2E_UpdateSettings.cs` (Zeilen 1463–1474, 1475–1486, 1494–1505). Die Asset-Duplikation wurde behoben, die darüberliegende Release-Ebene nicht.

  Empfehlung: Analog zu `ErstelleAssetJson` eine Fabrik `public static object ErstelleReleaseJson(string version, bool prerelease, DateTimeOffset publishedAt, object assetJson)` auf der Fixture einführen und alle fünf Stellen umstellen.

### GitHubReleaseClientTests.cs (GitHubReleaseClientTests)

- **Doppelter Code** — Das Fallback-URL-Literal `https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases/assets/91001` steht fünffach im neuen Testcode (Zeilen 30, 46, 72, 77, 78) — dieselbe Literal-Streuung, die im Produktivcode gerade über `GitHubApiHost`/`RepositoryApiUrl` konsolidiert wurde.

  Empfehlung: Eine Datei-Konstante (z. B. `private const string AssetApiUrl = "...";`) einführen; sie ist auch in `InlineData` verwendbar. Alternativ die URL wie im Produktivcode aus `new UpdateOptions()`-Defaults plus Asset-ID aufbauen.

### UpdatePackageServiceTests.cs (UpdatePackageServiceTests)

- **Doppelter Code** — Die drei neuen Fallback-Tests wiederholen jeweils wortgleich `var primaryUrl = new Uri("https://example.invalid/release.zip");` und `var fallbackUrl = new Uri("https://api.github.com/.../releases/assets/91001");` (Zeilen 114–115, 141–142, 164–165); dasselbe Primary-Literal steckt zusätzlich in `CreateUpdateInfo` (Zeile 234). Das `AddHandler(primaryUrl → InternalServerError)`-Setup ist zweimal identisch (Zeilen 117–118, 144–145).

  Empfehlung: `private const string PrimaryDownloadUrl`/`FallbackAssetApiUrl` (oder `static readonly Uri`) einführen, in `CreateUpdateInfo` wiederverwenden und das Fehler-Arrange ggf. in einen kleinen Helfer (z. B. `RegistriereFehler(HttpStatusCode)`) auslagern.

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
