# Tasks: SHA-256-Verifikation des Update-Pakets nachrüsten

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `UpdateInfo` in `UpdateModels.cs` um optionale Parameter `Uri? ManifestDownloadUrl = null` und `Uri? ManifestFallbackDownloadUrl = null` erweitern (inkl. XML-Doku) | Offen | — |
| 2 | Datenmodell | `UpdatePreparationPhase` in `UpdateModels.cs` um Wert `Verifizieren` erweitern | Offen | — |
| 3 | Konfiguration | `UpdateOptions` um init-Eigenschaft `ManifestAssetName = "update.json"` erweitern | Offen | — |
| 4 | Logik | `GitHubReleaseClient.TryCreateCandidate`: `update.json`-Asset auswählen (`ManifestAssetName` + `IsAbsoluteHttpsUrl`) und `ManifestDownloadUrl`/`ManifestFallbackDownloadUrl` (via `CreateFallbackDownloadUrl`) im `UpdateInfo` befüllen; fehlendes Manifest → `null`, kein Filtern | Offen | — |
| 5 | Logik | Private DTOs `UpdateManifest`/`UpdateManifestAsset` (`assets`, `assetName`, `sha256`, `JsonPropertyName`) in `UpdatePackageService` anlegen | Offen | — |
| 6 | Logik | `DownloadManifestAsync` in `UpdatePackageService` implementieren (`ManifestDownloadUrl == null` → `InvalidDataException`; einmaliger 5xx-Fallback via `SendDownloadRequestAsync`; `EnsureSuccessStatusCode`) | Offen | — |
| 7 | Logik | `GetExpectedSha256` in `UpdatePackageService` implementieren (`JsonException`/`null`/fehlender `release.zip`-Eintrag/leerer oder nicht-64-Hex-`sha256` → `InvalidDataException`) | Offen | — |
| 8 | Logik | `VerifyPackageHashAsync` in `UpdatePackageService` implementieren (streaming `SHA256.HashDataAsync`, `Convert.ToHexString`, OrdinalIgnoreCase-Vergleich → `InvalidDataException` bei Mismatch) | Offen | — |
| 9 | Logik | `PreparePackageAsync`: `Verifizieren`-Report + Verifikationsaufruf nach Leer-Prüfung und vor dem `Entpacken`-Report einbauen | Offen | — |
| 10 | UI | `UpdateProgressViewModel.Apply`: `Verifizieren`-Zweig mit `PhaseText = "Verifikation"` ergänzen | Offen | — |
| 11 | E2E-Infrastruktur | `Klassifiziere` in `UpdateFixtureHttpMessageHandler`: `*.json`-URLs → `"manifest"` | Offen | — |
| 12 | E2E-Infrastruktur | `UpdateE2EFixture`: Konstanten `StableManifestAssetId`/`RcManifestAssetId`, `StableManifestAssetApiUrl`/`RcManifestAssetApiUrl`, `StableManifestDownloadUrl`/`RcManifestDownloadUrl` ergänzen | Offen | — |
| 13 | E2E-Infrastruktur | `UpdateE2EFixture`: `ErstelleManifestAssetJson` und `ErzeugeManifestJson` (Workflow-Schema, echter SHA-256/`sizeBytes` der Fixture-ZIP) implementieren | Offen | — |
| 14 | E2E-Infrastruktur | `UpdateE2EFixture`: `ErstelleReleaseJson` um optionalen `manifestAssetJson`-Parameter erweitern; `ErzeugeReleaseListenJson` übergibt Manifest-Assets | Offen | — |
| 15 | E2E-Infrastruktur | `UpdateE2EFixture.SetzeStandardAntworten`: Manifest-Antworten (200 + `ErzeugeManifestJson`) für beide Manifest-URLs registrieren | Offen | — |
| 16 | E2E-Infrastruktur | `ErzeugeRcUndHoeheresStableReleaseJson` in `E2E_UpdateSettings.cs`: RC-Release um Manifest-Asset ergänzen | Offen | — |
| 17 | Tests | `UpdatePackageServiceTests`: `CreateSut` auf `RoutingHttpHandler` mit ZIP- + Manifest-Route umstellen; `CreateUpdateInfo` um Manifest-URL-Parameter erweitern | Offen | — |
| 18 | Tests | `UpdatePackageServiceTests.PreparePackageAsync_ShouldVerifyAndExtract_WhenManifestHashMatches` schreiben (Request-Reihenfolge + `Verifizieren`-Phase); Happy-Path-Test um `Verifizieren`-Assert erweitern | Offen | — |
| 19 | Tests | `UpdatePackageServiceTests.PreparePackageAsync_ShouldThrowInvalidDataAndClean_WhenHashMismatches` schreiben | Offen | — |
| 20 | Tests | `UpdatePackageServiceTests.PreparePackageAsync_ShouldThrow_WhenManifestUrlIsMissing` und `...ShouldThrow_WhenManifestDownloadFails` schreiben | Offen | — |
| 21 | Tests | `UpdatePackageServiceTests.PreparePackageAsync_ShouldUseManifestFallback_WhenManifestReturns5xx` schreiben | Offen | — |
| 22 | Tests | `UpdatePackageServiceTests.PreparePackageAsync_ShouldThrowInvalidData_WhenManifestIsUnusable` (Theory: kein JSON, ohne `assets`, ohne `release.zip`-Eintrag, leerer/unbrauchbarer `sha256`) schreiben | Offen | — |
| 23 | Tests | `GitHubReleaseClientTests`: Release-JSON-Helfer um `update.json`-Asset erweitern; `GetLatestReleaseAsync_ShouldReturnRelease_WhenAssetExists` um Manifest-Asserts ergänzen | Offen | — |
| 24 | Tests | `GitHubReleaseClientTests.GetLatestReleaseAsync_ShouldReturnManifestUrls_WhenManifestAssetExists` und `...ShouldKeepCandidate_WhenManifestAssetIsMissingOrInvalid` (Theory) schreiben | Offen | — |
| 25 | Tests | `UpdateProgressViewModelTests.Apply_ShouldShowVerifikation_WhenPhaseIsVerifizieren` schreiben | Offen | — |
| 26 | Tests | `UpdateFixtureHttpMessageHandlerTests.SendAsync_KlassifiziertManifestUrlAlsManifest` schreiben | Offen | — |
| 27 | E2E-Tests | `DefektesZipPhaseAsync` in `E2E_UpdateSettings.cs`: Manifest-Antwort mit `ErzeugeManifestJson` für die servierte `defekt.zip` neu berechnen | Offen | — |
| 28 | E2E-Tests | `ManipuliertesPaketPhaseAsync` in `E2E_UpdateSettings.cs` neu schreiben (gültige ZIP mit fremdem Hash → `PreparationFailed`, Fehlerdialog, kein Start/Shutdown) und in `Startup_SafetyCancelAndErrorsRemainUsable` einhängen | Offen | — |
| 29 | E2E-Tests | `Fixture_UsesIsolatedRealUpdatePipeline` in `E2E_UpdateFixture.cs`: Assertions auf `PreparationPhase` `phase == "Verifizieren"` und `HttpRequest` `art == "manifest"` ergänzen | Offen | — |
| 30 | Verifikation | `dotnet format Softwareschmiede.slnx --verify-no-changes`, `dotnet build Softwareschmiede.slnx`, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test` (Regulär-Lane beider Testprojekte + Update-E2E-Subset `FullyQualifiedName~End2EndTest.General_Update`, synchron) | Offen | — |
