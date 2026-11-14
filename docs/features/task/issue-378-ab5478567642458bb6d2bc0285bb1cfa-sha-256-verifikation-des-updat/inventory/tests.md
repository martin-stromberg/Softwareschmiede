# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-14, Build ca. 12:36, Testläufe ca. 12:37–12:45 (W. Europe Standard Time, UTC+01:00)
- Branch und Commit-ID: `task/issue-378-ab5478567642458bb6d2bc0285bb1cfa-sha-256-verifikation-des-updat` @ `8965b0f60ff9930577c0d023bfec869b6ee9b116`
- Uncommittete Änderungen im getesteten Stand: nur die neuen Feature-Dokumente unter `docs/features/task/issue-378-…-sha-256-verifikation-des-updat/` (`requirement.md`, `inventory/`); keine Änderungen an Produktiv- oder Testcode.
- Testumgebung und Runtime-/SDK-Versionen: .NET SDK 10.0.403, Ziel-Frameworks `net10.0` / `net10.0-windows10.0.17763.0`, Windows 10/11 (Host `DESKTOP-CM8OBSG`), Debug-Konfiguration, xUnit.net VSTest Adapter v4.0.1. `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war bei allen Läufen gesetzt (Sandbox-Auflagen; wirkt nur auf ConPTY-E2E-Tests der OsInterface-Lane).
- Build vor den Tests: `dotnet build Softwareschmiede.slnx` — erfolgreich, 0 Warnungen, 0 Fehler (~27 s).
- Ermittelte Testsuiten und Quellen der Testbefehle: CI-Workflow `.github/workflows/pr-staging-ci.yml` (Zeilen 121–132 regulär, 217–226 OsInterface) definiert vier Lanes — je `Category!=OsInterface` und `Category=OsInterface` für `Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests`. Projektdokumentation (CLAUDE.md) nennt die `Category!=OsInterface`-Lane als stabile Regulär-Lane; OsInterface enthält die umgebungssensitiven FlaUI-/ConPTY-/OS-E2E-Tests (`[OsInterface]`-Attribut, `src/Softwareschmiede.Tests/Infrastructure/Testing/OsInterfaceAttribute.cs`).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 — Regulär-Lane Softwareschmiede.Tests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=baseline-regular-lane.trx" --results-directory docs/features/task/issue-378-…/inventory/test-results` | Repo-Root | 0 | 2424 | 0 | 3 | [TRX](test-results/baseline-regular-lane.trx), [Konsole](test-results/baseline-regular-lane.console.log) |
| 2 — Regulär-Lane IntegrationTests | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface" --logger "trx;LogFileName=baseline-integration-regular-lane.trx" --results-directory docs/features/task/issue-378-…/inventory/test-results` | Repo-Root | 0 | 280 | 0 | 0 | [TRX](test-results/baseline-integration-regular-lane.trx), [Konsole](test-results/baseline-integration-regular-lane.console.log) |
| 3 — OsInterface-Subset (Update-E2E) | `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~End2EndTest.General_Update" --logger "trx;LogFileName=baseline-update-e2e.trx" --results-directory docs/features/task/issue-378-…/inventory/test-results` | Repo-Root | 0 | 2 | 0 | 0 | [TRX](test-results/baseline-update-e2e.trx), [Konsole](test-results/baseline-update-e2e.console.log) |

### Nachgewiesene bestehende Testfehler

Keine. Alle drei ausgeführten Läufe endeten mit 0 fehlgeschlagenen Tests.

### Testlücken und Ausführungsprobleme

- **3 übersprungene Tests in Lauf 1** — alle plattformbedingt via `SkippableFact`/`Skip.If`, keine Fehler (identisch zur Vorbelegung der Issue-381-Inventur):
  - `Softwareschmiede.Tests.Application.Services.ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — gilt nur für Nicht-Windows.
  - `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_Linux_BleibtUnveraendert` — Linux-spezifisch.
  - `Softwareschmiede.Tests.Infrastructure.Services.SystemShutdownServiceTests.ResolveShutdownCommand_MacOS_BleibtUnveraendert` — macOS-spezifisch.
- **OsInterface-Lanes nur teilweise ausgeführt:** Von der `Category=OsInterface`-Lane des `Softwareschmiede.Tests`-Projekts wurde nur das feature-relevante Update-Subset (Lauf 3, die zwei konsolidierten Fakten `General_UpdateFixtureSmoke_E2E` und `General_UpdateStartvarianten_E2E`) ausgeführt — beide bestanden in dieser Sandbox. Die übrige FlaUI-E2E-Suite (~35 E2E-Dateien mit App-Fenster-Starts, ConPTY-, Clipboard- und Credential-Store-Tests) sowie die komplette OsInterface-Lane des `Softwareschmiede.IntegrationTests`-Projekts wurden nicht ausgeführt: umgebungssensitive, langlaufende Lane; für den nicht ausgeführten Umfang ist der Ausgangszustand unbekannt und es wird keine Aussage getroffen.
- **ConPTY-Tests:** `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war gesetzt; die betroffenen ConPTY-E2E-Tests liegen in der OsInterface-Lane und waren ohnehin nicht Teil der ausgeführten Subsets.

## Testklassen

### `UpdatePackageServiceTests`

Datei: `src/Softwareschmiede.Tests/Application/Services/Updates/UpdatePackageServiceTests.cs` — direkte Unit-Tests der Paketvorbereitung (Regulär-Lane). `CreateSut` injiziert `HttpMessageHandler`-Doubles und eine Temp-Basis; `FakeScriptService` ersetzt `IUpdateScriptService`.

- `PreparePackageAsync_ShouldDownloadExtractAndCreateScript_WhenZipIsValid` — Happy Path: ZIP liegt danach vor, Exe + `version.json` entpackt; Fortschrittsreports enthalten `Download`, `Entpacken`, `UpdateVorbereiten`.
- `PreparePackageAsync_ShouldUsePrereleaseVersionPath_WhenPackageIsValid` — Prerelease-Version `1.4.0-rc.1` wird unter dem vollständigen SemVer-Pfad entpackt.
- `PreparePackageAsync_ShouldHandleBaseDirectoryWithSpaces` — Basispfad mit Leerzeichen funktioniert.
- `PreparePackageAsync_ShouldThrow_WhenPackageIsMissingRequiredRootFiles` (Theory, 2 Fälle) — fehlende `Softwareschmiede.exe` bzw. `version.json` → `InvalidDataException`.
- `PreparePackageAsync_ShouldThrowInvalidDataAndCleanExtraction_WhenPackagePayloadIsInvalid` (MemberData `InvalidPackagePayloads`: leeres Array, `[1,2,3]`, ZIP ohne Pflichtdateien) — `InvalidDataException` + vollständiges Aufräumen (`release.zip`, `.download`, `extracted/{Version}` weg).
- `PreparePackageAsync_ShouldKeepDownloadAtomic_WhenDownloadFails` — HTTP-500 der primären URL → `HttpRequestException`, kein finales/partielles ZIP. (Kein `FallbackDownloadUrl` gesetzt.)
- `PreparePackageAsync_ShouldUseFallbackDownloadUrl_WhenPrimaryReturns5xx` — 5xx der primären URL → genau ein Fallback-Request mit `Accept: application/octet-stream` auf die API-Asset-URL; Reihenfolge der `RequestedUrls` geprüft.
- `PreparePackageAsync_ShouldThrow_WhenPrimaryAndFallbackReturn5xx` — beide 5xx → `HttpRequestException`, aufgeräumt.
- `PreparePackageAsync_ShouldNotUseFallback_WhenPrimaryReturnsNon5xxError` — 404 → sofort Fehler, kein Fallback-Request.
- `PreparePackageAsync_ShouldRejectUpdateDirectoryOutsideBaseEvenWithSharedPrefix` — `UpdateDirectoryName = "..\\app2"` → `InvalidOperationException`, Sentinel unberührt.

*Kein Test deckt einen Manifest-Abruf oder eine Hash-Prüfung ab — dieser Pfad existiert nicht.*

### `GitHubReleaseClientTests` / `GitHubReleaseClientTests_Filters` / `GitHubReleaseClientTests_Pagination`

Dateien: `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests{,_Filters,_Pagination}.cs` — Release-Auswahl, Fallback-URL-Ableitung, Kanalfilter, Pagination, URL-Pinning.

- `GetLatestReleaseAsync_ShouldReturnRelease_WhenAssetExists` — `UpdateInfo` mit `Version`, `IsPrerelease`, `DownloadUrl`, `FallbackDownloadUrl` aus `url`.
- `GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsMissing` — Fallback aus `id` abgeleitet.
- `GetLatestReleaseAsync_ShouldDeriveFallbackUrlFromAssetId_WhenUrlIsInvalid` (Theory) — fremde/ungültige `url` → ID-Ableitung; ohne ID `null`.
- `GetLatestReleaseAsync_ShouldReturnNull_WhenReleaseIsNotUsable` (Theory) — leere Liste/unbrauchbare Einträge → `KeinTreffer`; HTTP-500 → `Fehlgeschlagen`.
- `GetLatestReleaseAsync_ShouldReturnNull_WhenResponseIsNotAReleaseList` (Theory) — Nicht-Listen-JSON → `Fehlgeschlagen`.
- `GetLatestReleaseAsync_ShouldReturnNull_WhenRequestTimesOut` — `CheckTimeout` → `Fehlgeschlagen`.
- `GetLatestReleaseAsync_FiltersExclusivelyByChannel` / `…FiltersGitHubPrereleaseFlag` / `…FiltersSemVerPrereleaseSuffix` (Theories) — Kanal-Exklusivität.
- `GetLatestReleaseAsync_SkipsDraftInvalidTagAndUnusableAssetEntries` — Draft, ungültiger Tag, leeres/fremdes/relatives/ftp-Asset → übersprungen; `RELEASE.ZIP` (Großschreibung) wird OrdinalIgnoreCase akzeptiert (`AssetName = "RELEASE.ZIP"`).
- `GetLatestReleaseAsync_PreservesPrereleaseVersion` — RC-Version normalisiert.
- `GetLatestReleaseAsync_SelectsHighestAcrossPages` / `…LaterPageFailureDiscardsCandidate` / `…LaterPageJsonFailureDiscardsCandidate` / `…LaterPageTimeoutDiscardsCandidate` / `…RejectsPaginationCycle` / `…RejectsDisallowedFollowUpUrls` (Theory, 5 Fälle) / `…AllowsNumericRepositoryIdFollowUp` / `…PropagatesCallerCancellation` — Pagination und Pinning.

*Kein Test verlangt ein `update.json`-Asset; simulierte Release-Antworten enthalten jeweils nur das `release.zip`-Asset.*

### `UpdateServiceTests` / `UpdateServiceTests_Options` / `UpdateServiceTests_PrereleaseChain`

Dateien: `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests{,_Options,_PrereleaseChain}.cs` — Orchestrierung; `IUpdatePackageService` via `Mock.Of<>` (Moq), `UpdateInfo` wird dort teils mit 6 Argumenten konstruiert (optionaler `FallbackDownloadUrl`).

- `CheckForUpdateAsync_*` — Verfügbarkeit, `KeinTreffer`, `Fehlgeschlagen`, fehlende lokale Version, Kanalkonflikt-Warnung, Optionspropagation, Kanal-Mismatch-Ausschluss, Prerelease-Kette.
- `StartPreparedUpdateAsync_ShouldNotShutdown_WhenScriptStartFails` — kein Shutdown ohne erfolgreichen Skriptstart.
- `GetLatestReleaseAsync_LaterPageFailureDiscardsCandidate` (PrereleaseChain) — echter `GitHubReleaseClient` hinter `UpdateService`.

### `UpdateProgressViewModelTests` / `UpdateProgressDialogTests`

Dateien: `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateProgressViewModelTests.cs`, `src/Softwareschmiede.Tests/App/Views/UpdateProgressDialogTests.cs`

- `Apply_ShouldUpdateProgressState`, `SetError_ShouldEnableClose`, `CancelCommand_ShouldInvokeCancellationAndDisableCancel`, `MarkUpdaterStarting_ShouldEnableCloseAndDisableCancel`, `Apply_ShouldIgnoreLateProgressAfterTerminalState` — ViewModel-Verhalten.
- `Show_ShouldNotThrowBindingException` — Dialog-Binding.

### `UpdateFixtureHttpMessageHandlerTests`

Datei: `src/Softwareschmiede.Tests/App/Services/Testing/UpdateFixtureHttpMessageHandlerTests.cs` — 8 Unit-Tests des E2E-HTTP-Handlers: konfigurierte Antwort mit Status/Headern, unbekannte URL → `HttpRequestException` + `UnknownHttpRequest`, API-Asset-URL-Klassifizierung (`"asset"`), `fault`-Transportfehler, Antwort-Gate, Stream-Gate, Testwurzel-Austrittsschutz, ZIP-ContentType-Ableitung.

### `End2EndTest` (Update-Szenarien, OsInterface-Lane)

Dateien: `src/Softwareschmiede.Tests/E2E/E2E_UpdateFixture.cs`, `src/Softwareschmiede.Tests/E2E/E2E_UpdateSettings.cs`, gebündelt in `src/Softwareschmiede.Tests/E2E/MainTest.cs` (`[OsInterface]` + `[Trait("Category","E2E")]`).

- `General_UpdateFixtureSmoke_E2E` (MainTest.cs:69–73) — `Fixture_UsesIsolatedRealUpdatePipeline` + `Fixture_SettingsReadFailureTargetsPhaseAfterDatabaseInitialization`: echte Release-Listen-/ZIP-Pipeline über den Fixture-Handler, Stable-/RC-Auswahl, Phasen- und HTTP-Protokollereignisse, kontrollierte Launcher-/Shutdown-Grenze, Unknown-Request-Nachweis, Lesefehler-Grenzen.
- `General_UpdateStartvarianten_E2E` (MainTest.cs:77–86) — sieben Szenarien: Settings-Persistenz, Startautomatik je Modus, Kanal-/Asset-Auswahl, Invalidierung bei geänderten Einstellungen, Fehler-/Abbruch-Robustheit (Download-Abbruch, Asset-HTTP-Fehler, defektes ZIP, API-Fallback-Erfolg, Launcher-Fehler), Einmaligkeit der Startautomatik.

*Die Fixture erzeugt und liefert aktuell keine `update.json` — ein neuer Pflicht-Manifestabruf würde alle diese Szenarien am Schritt „Unbekannte Update-E2E-Anfrage" scheitern lassen.*

## Hilfsmethoden

### `UpdateE2EFixture`

Datei: `src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs` — isolierte Update-E2E-Testwurzel.

- `SetzeStandardAntworten(bool mitRcRelease = true)` — Release-Listen-Antwort (`ReleaseApiUrl`) + ZIP-Antworten für `StableDownloadUrl`, `RcDownloadUrl` und beide API-Asset-URLs (`StableAssetApiUrl`, `RcAssetApiUrl`).
- `ErzeugeReleaseListenJson` (private, Z. 487–508) — baut die GitHub-Release-Liste (Stable + optional RC), je Release **genau ein Asset** via `ErstelleAssetJson`.
- `ErstelleAssetJson(apiUrl, assetId, downloadUrl)` (Z. 374–381) — anonymes Asset-Objekt (`name` = `UpdateOptions.AssetName`, `url`, `id`, `browser_download_url`).
- `ErstelleReleaseJson(version, prerelease, publishedAt, assetJson)` (Z. 389–397) — anonymes Release-Objekt (`tag_name`, `prerelease`, `draft`, `published_at`, `assets[1]`).
- `ErstellePaketZip(zipPfad, version, marker)` (Z. 406–421) — schreibt die Paket-ZIP selbst (Exe + `version.json`); der SHA-256 des ZIP ist damit zur Laufzeit berechenbar, wird aber nirgends ermittelt oder ausgeliefert.
- `AktualisiereSzenario`/`SpeichereSzenario`, Gate-Methoden (`OeffneGate`, `BrecheGateAb`, `SetzeGateZurueck`), `AktiviereLesefehler`/`DeaktiviereLesefehler`, `LeseProtokoll`/`WarteAufEreignisAsync`/`ZaehleEreignis`, `ErzeugeStartUmgebung`, `SetzeUpdateEinstellungenAsync`/`AktualisiereUpdateEinstellungenAsync`, `AssetApiUrl(long)`, `LesePaketExeInhalt`.
- Konstanten: `InstallierteVersion = "1.2.0"`, `StableVersion = "1.3.0"`, `RcVersion = "1.4.0-rc.1"`, `StableAssetId = 91001`, `RcAssetId = 91002`, `ReleaseApiUrl`.

### `RoutingHttpHandler`

Datei: `src/Softwareschmiede.Tests/Helpers/RoutingHttpHandler.cs` — routet exakte URLs auf Handler; nicht konfigurierte URLs liefern 404 + `"[]"`; `RequestedUrls`/`RequestedAcceptHeaders` protokollieren Aufrufe; `AddPage` (Link-Header) und `AddHandler` (freie Delegates). Für einen Manifest-Abruf direkt nutzbar (eigene Route).

### `StaticHttpHandler`

Datei: `src/Softwareschmiede.Tests/Helpers/StaticHttpHandler.cs` — eine feste Status/Body-Antwort für jede Anfrage.

### `TempDirectory`

Datei: `src/Softwareschmiede.Tests/Helpers/TempDirectory.cs` — `IDisposable`-Tempverzeichnis (`temp.Path`, optionaler expliziter Pfad); rekursives Löschen im `Dispose`.

### Private Test-Doubles in `UpdatePackageServiceTests`

Datei: `src/Softwareschmiede.Tests/Application/Services/Updates/UpdatePackageServiceTests.cs` (Z. 205–285)

- `CreateSut(baseDirectory, zipBytes|handler, options)` — SUT-Fabrik.
- `CreateUpdateInfo(version, isPrerelease, fallbackDownloadUrl)` — `UpdateInfo` mit 7 Argumenten.
- `CreateZip(includeExe, includeVersion, version)` — erzeugt Paket-ZIP-Bytes im Speicher.
- `FakeScriptService : IUpdateScriptService` — schreibt `updates/update.ps1` als Dummy.
- `BytesHttpHandler` — liefert feste Bytes/Status für jede Anfrage.

### `UpdateE2ESzenario` / `UpdateE2EAntwort`

Datei: `src/Softwareschmiede.App/Services/Testing/UpdateE2ESzenario.cs` — siehe [models.md](models.md); Antwort-Mapping ist exakte-URL-basiert, ein weiterer `UpdateE2EAntwort`-Eintrag für eine `update.json`-URL ist ohne Handler-Änderung möglich.
