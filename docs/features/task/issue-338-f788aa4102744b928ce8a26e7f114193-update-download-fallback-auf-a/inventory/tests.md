# Tests

## Testklassen (Unit)

### `UpdatePackageServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/Updates/UpdatePackageServiceTests.cs`

- `PreparePackageAsync_ShouldDownloadExtractAndCreateScript_WhenZipIsValid` (Z. 18) — Happy Path: Download, Entpacken, Skript, alle drei Phasen im Fortschritt
- `PreparePackageAsync_ShouldUsePrereleaseVersionPath_WhenPackageIsValid` (Z. 37) — Entpackziel mit SemVer-Suffix `1.4.0-rc.1`
- `PreparePackageAsync_ShouldHandleBaseDirectoryWithSpaces` (Z. 52) — Basispfad mit Leerzeichen
- `PreparePackageAsync_ShouldThrow_WhenPackageIsMissingRequiredRootFiles` (Z. 68, Theory) — fehlende EXE/`version.json` → `InvalidDataException`
- `PreparePackageAsync_ShouldThrowInvalidDataAndCleanExtraction_WhenPackagePayloadIsInvalid` (Z. 81, MemberData `InvalidPackagePayloads`) — leeres/korruptes Paket → `InvalidDataException` + keine Artefakte
- `PreparePackageAsync_ShouldKeepDownloadAtomic_WhenDownloadFails` (Z. 96) — **einziger Download-Fehler-Test:** `BytesHttpHandler` mit `HttpStatusCode.InternalServerError` → `HttpRequestException` (aus `EnsureSuccessStatusCode`), weder `release.zip` noch `release.zip.download` bleiben bestehen
- `PreparePackageAsync_ShouldRejectUpdateDirectoryOutsideBaseEvenWithSharedPrefix` (Z. 110) — Pfad-Confinement → `InvalidOperationException`

### `GitHubReleaseClientTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests.cs`

- `GetLatestReleaseAsync_ShouldReturnRelease_WhenAssetExists` (Z. 17) — Asset-JSON nur mit `name` + `browser_download_url`; prüft `Version`, `IsPrerelease`, `DownloadUrl`
- `GetLatestReleaseAsync_ShouldReturnNull_WhenReleaseIsNotUsable` (Z. 51, Theory) — leere Liste, falsches Prerelease, ungültiger Tag, HTTP 500
- `GetLatestReleaseAsync_ShouldReturnNull_WhenResponseIsNotAReleaseList` (Z. 65, Theory) — Nicht-Listen-JSON
- `GetLatestReleaseAsync_ShouldReturnNull_WhenRequestTimesOut` (Z. 77) — `DelayingHttpHandler` + `CheckTimeout = 50 ms`

### `GitHubReleaseClientTests_Filters`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests_Filters.cs`

- `GetLatestReleaseAsync_FiltersExclusivelyByChannel` (Z. 19, Theory) — exklusive Kanalfilterung
- `GetLatestReleaseAsync_FiltersGitHubPrereleaseFlag` (Z. 63, Theory) — GitHub-`prerelease`-Flag
- `GetLatestReleaseAsync_FiltersSemVerPrereleaseSuffix` (Z. 99, Theory) — SemVer-Suffix
- `GetLatestReleaseAsync_SkipsDraftInvalidTagAndUnusableAssetEntries` (Z. 133) — überspringt u. a. relative und `ftp://`-`browser_download_url`-Assets (deckt `IsAbsoluteHttpsUrl` ab)
- `GetLatestReleaseAsync_PreservesPrereleaseVersion` (Z. 205) — RC-Metadaten inkl. `DownloadUrl`

Alle JSON-Fixtures verwenden ausschließlich `"name"` + `"browser_download_url"` — keine `url`/`id`-Asset-Felder.

### `GitHubReleaseClientTests_Pagination`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests_Pagination.cs`

- `GetLatestReleaseAsync_SelectsHighestAcrossPages` (Z. 22, Theory) — Auswahl über zwei Seiten via `RoutingHttpHandler` + `Link`-Header
- `GetLatestReleaseAsync_LaterPageFailureDiscardsCandidate` (Z. 94) — Folgeseiten-Fehler verwirft Funde
- `GetLatestReleaseAsync_LaterPageJsonFailureDiscardsCandidate` (Z. 110)
- `GetLatestReleaseAsync_LaterPageTimeoutDiscardsCandidate` (Z. 125)
- `GetLatestReleaseAsync_RejectsPaginationCycle` (Z. 144)
- `GetLatestReleaseAsync_RejectsDisallowedFollowUpUrls` (Z. 164, Theory) — Host-/Pfad-Pinning (`IsAllowedFollowUpUri`)
- `GetLatestReleaseAsync_AllowsNumericRepositoryIdFollowUp` (Z. 180) — `/repositories/{id}/releases`-Form
- `GetLatestReleaseAsync_PropagatesCallerCancellation` (Z. 206)

### `UpdateServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests.cs`

- `CheckForUpdateAsync_ShouldReturnAvailable_WhenRemoteVersionIsNewer` (Z. 13) — **`new UpdateInfo(...)` Z. 15**
- `CheckForUpdateAsync_ShouldReturnNoUpdate_WhenLookupFindsNoCandidate` (Z. 32)
- `CheckForUpdateAsync_ShouldReturnNotCheckable_WhenLookupFails` (Z. 50)
- `CheckForUpdateAsync_ShouldReturnNotCheckable_WhenLocalVersionIsMissing` (Z. 68)
- `CheckForUpdateAsync_WarnsOnInstallationChannelConflict` (Z. 91, Theory) — **`new UpdateInfo(...)` Z. 96-97**
- `StartPreparedUpdateAsync_ShouldNotShutdown_WhenScriptStartFails` (Z. 118)

### `UpdateServiceTests_Options`
Datei: `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests_Options.cs`

- `CheckForUpdateAsync_PropagatesCurrentOptions` (Z. 13) — **`new UpdateInfo(...)` Z. 15-16**
- `CheckForUpdateAsync_RejectsChannelMismatch` (Z. 44, Theory) — **`new UpdateInfo(...)` Z. 47-48**

### `UpdateServiceTests_PrereleaseChain`
Datei: `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests_PrereleaseChain.cs`

- `CheckForUpdateAsync_FromInstalledPrerelease` (Z. 25) — echter `ApplicationVersionProvider` + echter `GitHubReleaseClient`; `ReleasesJson`-Hilfsmethode (Z. 107-118) erzeugt Assets nur mit `browser_download_url`
- `GetLatestReleaseAsync_LaterPageFailureDiscardsCandidate` (Z. 72)

### `MainWindowViewModelTests_UpdateStartup`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests_UpdateStartup.cs` (Basis: `MainWindowViewModelUpdateTestBase`)

- **`ReleaseCandidateUpdate = new(...)` Z. 25-27** (target-typed `UpdateInfo`-Konstruktion)
- `Constructor_DoesNotCheckUpdates` (Z. 31)
- `WindowReady_StartsOnceWithImmediateResult` (Z. 54, Theory) — modusabhängige Startautomatik
- `Aus_BlocksBothCommandsAndDirectInvocation` (Z. 96)
- `SettingsChange_DiscardsDelayedResultAndStopsPreparation` (Z. 164)
- `Startup_NoUpdateOrNotCheckable_DoesNotInstall` (Z. 244, Theory)
- `Startup_SafetyDeclined_DoesNotContinue` (Z. 280, Theory)
- `Startup_SafetyCancelled_DoesNotContinue` (Z. 315, Theory)
- `Startup_PrepareOrStartFailure_DoesNotShutdown` (Z. 371, Theory) — **`PrepareUpdateAsync` wirft `InvalidOperationException("Asset-Download fehlgeschlagen")` (Z. 391)** → Fehlerzustand im Fortschrittsdialog (deckt den heutigen Fehlerpropagationspfad ab)
- `Startup_ConcurrentCommandsCannotDuplicateInstall` (Z. 424)

### `MainWindowViewModelTests_UpdateSettingsReadFailure`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests_UpdateSettingsReadFailure.cs`

Nutzt `NeuesUpdate` aus der Basisklasse (kein eigener `UpdateInfo`-Konstruktor); `It.IsAny<UpdateInfo>`-Mocks für `PrepareUpdateAsync` (Z. 100, 129, 173, 220).

### `UpdateFixtureHttpMessageHandlerTests`
Datei: `src/Softwareschmiede.Tests/App/Services/Testing/UpdateFixtureHttpMessageHandlerTests.cs`

- `SendAsync_LiefertKonfigurierteAntwortMitStatusUndHeadern` (Z. 50) — exakte URL, `Link`-Header, Content-Type
- `SendAsync_UnbekannteUrl_ScheitertOhneNetzwerkfallback` (Z. 82) — `HttpRequestException` + `UnknownHttpRequest`
- `SendAsync_Transportfehler_WirftKontrollierteAusnahme` (Z. 93) — `Fehler = true`
- `SendAsync_AntwortGate_BlockiertBisZurFreigabe` (Z. 106)
- `SendAsync_StreamGate_LiefertTeilbytesUndBlockiertBeimNaechstenLesen` (Z. 127)
- `SendAsync_AntwortdateiAusserhalbDerTestwurzel_WirdAbgelehnt` (Z. 171)
- `SendAsync_AntwortdateiInnerhalbDerWurzel_WirdGeladen` (Z. 200)

Der Handler prüft nur die Request-URL — Request-Header werden nicht ausgewertet und nicht getestet.

## E2E-Testklassen

### `End2EndTest` (partial) — `E2E_UpdateFixture.cs`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_UpdateFixture.cs`

- `Fixture_UsesIsolatedRealUpdatePipeline` (Z. 21) — komplette echte JSON-/ZIP-Pipeline: Release-Abruf → `UpdateVerfuegbar` → Download → Entpacken → Skript → Launcher-Record → `ShutdownRequested`; Unbekannter-Request-Nachweis; RC-Auswahl nach Neustart. Enthält `Assert.Single` auf `art == "asset"`-Abrufe.
- `Fixture_SettingsReadFailureTargetsPhaseAfterDatabaseInitialization` (Z. 121) — drei Settings-Lesefehler-Grenzen

### `End2EndTest` (partial) — `E2E_UpdateSettings.cs`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_UpdateSettings.cs` (aufgerufen aus `MainTest.General_UpdateStartvarianten_E2E`, `MainTest.cs:72-81`)

- `Settings_AllModesAndUpdatePfadPersist` (Z. 47) — E-01, Settings-Persistenz
- `Startup_ModesDriveUpdatePipeline` (Z. 180) — E-02, u. a. blockierter Download via `StreamGate` auf `StableDownloadUrl` (Z. 231-236)
- `UpdatePfad_SelectsExclusiveChannelAsset` (Z. 291) — E-03, Kanalwahl; `AssertiereInstallationsNachweis` erwartet genau einen `asset`-Abruf auf die jeweilige `DownloadUrl`
- `SavedChangesInvalidatePreviousOffer` (Z. 392) — E-04
- `Startup_NoUpdateOrUncheckableRemainsUsable` (Z. 506) — E-05
- `Startup_SafetyCancelAndErrorsRemainUsable` (Z. 635) — E-06 inkl. Phasen:
  - `SafetyDialogAbgelehntPhaseAsync` (Z. 703) — kein Asset-Abruf bei Abbruch
  - `DownloadAbbruchPhaseAsync` (Z. 734) — `StreamGate`-Abbruch mitten im Download
  - **`AssetHttpFehlerPhaseAsync` (Z. 781)** — setzt `Status = 500` auf der `StableDownloadUrl`-Antwort und erwartet `PreparationFailed`, „konnte nicht vorbereitet" sowie **genau einen** `HttpRequest` auf `fixture.StableDownloadUrl` (Z. 793-794). Das ist der heutige 5xx-Pfad, den ein Fallback erweitern würde (zusätzlicher Request auf die API-Asset-URL).
  - `DefektesZipPhaseAsync` (Z. 808), `LauncherFehlerPhaseAsync` (Z. 835), T-09-Phasen (Z. 875 ff.)
- `Startup_IsOnceAndCommandsStayBlocked` (Z. 1010) — E-07

**E2E-JSON-Erzeuger (private, nur `browser_download_url`):** `ErzeugeRcUndHoeheresStableReleaseJson` (Z. 1413-1435), `ErzeugeReleaseListenJson` (Z. 1438-1452); `AssertiereInstallationsNachweis` (Z. 1224-1266) mit `Assert.Single` auf `art == "asset"`.

## Hilfsmethoden und -klassen

### `UpdatePackageServiceTests` (private, geschachtelt)
- `CreateSut(baseDirectory, zipBytes, statusCode = OK, options = null)` (Z. 138) — baut `HttpClient(new BytesHttpHandler(...))` + Service mit `baseDirectory`
- `CreateUpdateInfo(version = "1.2.3", isPrerelease = false)` (Z. 153) — **zentrale `UpdateInfo`-Factory der Klasse** (target-typed `new(...)`, `DownloadUrl = https://example.invalid/release.zip`); eine Record-Erweiterung ist hier an einer Stelle anzupassen
- `CreateZip(includeExe, includeVersion, version)` (Z. 156) — In-Memory-Release-ZIP
- `InvalidPackagePayloads()` (Z. 130) — Theory-Daten
- `FakeScriptService` (Z. 179) — schreibt `updates/update.ps1`, `StartScriptAsync` no-op
- `BytesHttpHandler` (Z. 191) — **`HttpMessageHandler`, der JEDE Anfrage mit demselben Status/Body beantwortet** — kann einen zweiten Request (Fallback-URL) nicht anders beantworten als den ersten; URL-differenzierte Fallback-Tests benötigen einen routingfähigen Handler (vgl. `RoutingHttpHandler`) oder einen neuen Handler

### `MainWindowViewModelUpdateTestBase`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelUpdateTestBase.cs`

- `NeuesUpdate = new(...)` (Z. 25-27) — geteiltes Standard-`UpdateInfo` (target-typed `new(...)`); eine Record-Erweiterung ist hier anzupassen
- `FertigeVorbereitung` (Z. 30-31) — Standard-`UpdatePreparationResult`
- Mocks: `_updateServiceMock` (`IUpdateService`), `_safetyServiceMock`, `_progressDialogMock`, `_dialogServiceMock`; `CreateSut()`, `SetUpdateSettingsAsync`, `SpeichereUpdateEinstellungenUeberUiAsync`

### `StaticHttpHandler`
Datei: `src/Softwareschmiede.Tests/Helpers/StaticHttpHandler.cs`

Beantwortet jede Anfrage mit derselben Statuscode/Body-Antwort — wie `BytesHttpHandler` nicht URL-differenzierend.

### `RoutingHttpHandler`
Datei: `src/Softwareschmiede.Tests/Helpers/RoutingHttpHandler.cs`

Routet per exakter Request-URL (`OrdinalIgnoreCase`) auf registrierte Handler (`AddPage` mit optionalem `Link`-Header/`nextUrl`, `AddHandler` frei); protokolliert `RequestedUrls` in Aufrufreihenfolge; unbekannte URLs → 404 mit Body `[]`. Geeignetes Muster für URL-differenzierte Download-Fallback-Tests.

### `TempDirectory`
Datei: `src/Softwareschmiede.Tests/Helpers/TempDirectory.cs`

Wegräumendes Temp-Verzeichnis für `baseDirectory`-Tests.

### `UpdateE2EFixture`
Datei: `src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs`

Runner-Fixture (Details siehe [Logik](logic.md)): `ReleaseApiUrl`, `StableDownloadUrl`/`RcDownloadUrl`, `SetzeStandardAntworten`, `AktualisiereSzenario`, Gate- und Protokoll-Hilfen. Für ein Fallback-Szenario müsste die Release-JSON ein `url`/`id`-Asset-Feld liefern **und** die Szenario-`Antworten` einen Eintrag für die API-Asset-URL enthalten.
