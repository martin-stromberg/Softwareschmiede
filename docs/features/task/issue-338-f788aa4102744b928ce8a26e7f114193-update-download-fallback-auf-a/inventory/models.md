# Datenmodell

## `UpdateInfo`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 21-27)

Positionelles `sealed record` — alle Konstruktionsstellen müssen bei einer Erweiterung angepasst werden. Eine Fallback-Download-URL existiert **nicht**.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Version` | `string` | Semantische Release-Version ohne führendes `v`, inkl. Prerelease-Suffix und Build-Metadaten |
| `TagName` | `string` | GitHub-Release-Tag |
| `AssetName` | `string` | Name des Release-Assets |
| `DownloadUrl` | `Uri` | Direkte Download-URL des Release-Assets (aus `browser_download_url`) |
| `PublishedAt` | `DateTimeOffset?` | Optionaler Veröffentlichungszeitpunkt |
| `IsPrerelease` | `bool` | Prerelease-Klassifikation (GitHub-Flag `prerelease` oder SemVer-Suffix) |

**Konstruktionsstellen (vollständig):**
- Produktiv: `GitHubReleaseClient.TryCreateCandidate` — `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs:135-141` (einzige Produktiv-Stelle)
- Tests: `UpdateServiceTests.cs:15, 96-97` · `UpdateServiceTests_Options.cs:15-16, 47-48` · `UpdatePackageServiceTests.cs:153-154` (`CreateUpdateInfo`, target-typed `new(...)`) · `MainWindowViewModelUpdateTestBase.cs:25-27` (`NeuesUpdate`, target-typed `new(...)`) · `MainWindowViewModelTests_UpdateStartup.cs:25-27` (`ReleaseCandidateUpdate`, target-typed `new(...)`)

**Verwendungsstellen (Durchreicher, keine Konstruktion):** `UpdateReleaseLookupResult.Release`/`Gefunden`, `UpdateCheckResult.Update`/`UpdateVerfuegbar` (UpdateModels.cs), `IUpdateService.PrepareUpdateAsync`, `IUpdatePackageService.PreparePackageAsync`, `UpdateService.PrepareUpdateAsync` (UpdateService.cs:92-96), `MainWindowUpdateFlow.VerfuegbaresUpdate`/`InstalliereUpdateAsync` (MainWindowUpdateFlow.cs:65, 288), `ProtokollierenderUpdateService.PrepareUpdateAsync` (Zeile 51), `MainWindowViewModel.VerfuegbaresUpdate` (Zeile 79).

## `UpdateOptions`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateOptions.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `RepositoryOwner` | `string` (init, Default `"martin-stromberg"`) | GitHub-Repository-Owner — Baustein der API-Asset-URL `repos/{owner}/{repo}/releases/assets/{id}` |
| `RepositoryName` | `string` (init, Default `"Softwareschmiede"`) | GitHub-Repository-Name — Baustein der API-Asset-URL |
| `AssetName` | `string` (init, Default `"release.zip"`) | Erwarteter Release-Asset-Name (Selektion in `TryCreateCandidate`, Dateiname in `PreparePackageAsync`) |
| `UpdateDirectoryName` | `string` (init, Default `"updates"`) | Update-Arbeitsverzeichnis relativ zum Programmverzeichnis |
| `CheckTimeout` | `TimeSpan` (init, Default 15 s) | Wird nur in `GitHubReleaseClient.GetLatestReleaseAsync` per linked CTS angewendet; `UpdatePackageService.DownloadAsync` nutzt es **nicht** |
| `ExecutableName` | `string` (init, Default `"Softwareschmiede.exe"`) | Erwartete EXE im Paket-Root (Validierung) |

## `GitHubRelease` / `GitHubAsset` (private Deserialisierungsmodelle)
Datei: `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs` (Zeilen 209-234, private geschachtelte Klassen)

### `GitHubRelease`
| Eigenschaft | Typ | JSON-Feld |
|-------------|-----|-----------|
| `TagName` | `string?` | `tag_name` |
| `Prerelease` | `bool` | `prerelease` |
| `Draft` | `bool` | `draft` |
| `PublishedAt` | `DateTimeOffset?` | `published_at` |
| `Assets` | `List<GitHubAsset>` (Default `[]`) | `assets` |

### `GitHubAsset`
| Eigenschaft | Typ | JSON-Feld |
|-------------|-----|-----------|
| `Name` | `string` (Default `string.Empty`) | `name` |
| `BrowserDownloadUrl` | `string` (Default `string.Empty`) | `browser_download_url` |

Die Asset-Felder `url` (API-Asset-URL) und `id` (numerische Asset-ID) des GitHub-Schemas werden derzeit **nicht** deserialisiert.

## `UpdateReleaseLookupResult`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 70-93)

| Member | Typ | Beschreibung / Zweck |
|--------|-----|----------------------|
| `Erfolg` | `bool` (get-only) | Technisch erfolgreiche Abfrage vs. Fehlschlag |
| `Release` | `UpdateInfo?` (get-only) | Neuester zulässiger Kandidat oder `null` |
| `Fehlgeschlagen` | `static UpdateReleaseLookupResult` | Fehlgeschlagene Abfrage |
| `KeinTreffer` | `static UpdateReleaseLookupResult` | Erfolgreiche Abfrage ohne Kandidaten |
| `Gefunden(UpdateInfo)` | `static` Factory | Erfolgreiches Ergebnis mit Release |

## `UpdateCheckResult`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 112-122)

Positionelles Record `(UpdateCheckStatus Status, UpdateInfo? Update = null, string? Message = null)` mit Factories `KeinUpdate`, `UpdateVerfuegbar(UpdateInfo, string?)`, `NichtPruefbar`.

## `UpdatePreparationProgress`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeile 141)

Positionelles Record `(UpdatePreparationPhase Phase, double? Percent, string Message)` — Fortschrittsmeldung; im Download-Pfad wird `Percent` aus `ContentLength` berechnet (`UpdatePackageService.cs:101, 112-116`).

## `UpdatePreparationResult`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 149-154)

Positionelles Record `(string ZipPath, string ExtractedDirectory, string ScriptPath, string LogPath, bool RequiresElevation)`.

## `UpdateCheckOptions`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 59-63)

Positionelles Record `(string UpdatePfad)` mit abgeleiteter Eigenschaft `Kanal` (`UpdatePfadKanalResolver.ErmittleKanal`).

## `UpdateSettings`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeile 55)

Positionelles Record `(UpdateMode Modus, string UpdatePfad)` — persistierter Einstellungs-Snapshot; für den Fallback ohne Bedeutung.

## `InstalledVersionInfo`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 8-12)

Positionelles Record `(string Version, string? TagName, string? Commit, DateTimeOffset? CreatedAtUtc)` — nur für den Update-Check, nicht für den Download.

---

## E2E-Szenariomodell (Test-Infrastruktur, App-Projekt)

### `UpdateE2ESzenario`
Datei: `src/Softwareschmiede.App/Services/Testing/UpdateE2ESzenario.cs` (Zeilen 11-36)

| Eigenschaft | Typ | JSON-Feld | Beschreibung / Zweck |
|-------------|-----|-----------|----------------------|
| `Antworten` | `List<UpdateE2EAntwort>` | `responses` | Abbildung exakter Request-URLs auf Antworten |
| `Lesefehler` | `UpdateE2ELesefehler?` | `settingsReadFailure` | Kontrollierter Settings-Lesefehler |
| `CliSicherheit` | `UpdateE2ECliSicherheit?` | `cliSafety` | Simulierte riskante CLI-Aufgaben |
| `ProzessStart` | `UpdateE2EProzessStart?` | `processStart` | Gesteuertes Launcher-Ergebnis |

Methode `FindeAntwort(string? url)` (Zeilen 32-35): exakter URL-Vergleich per `string.Equals(..., StringComparison.OrdinalIgnoreCase)` — ein Fallback-Request auf eine nicht konfigurierte API-Asset-URL liefert `null` und führt im Handler zu `UnknownHttpRequest` + `HttpRequestException`.

### `UpdateE2EAntwort`
Datei: `src/Softwareschmiede.App/Services/Testing/UpdateE2ESzenario.cs` (Zeilen 39-83)

| Eigenschaft | Typ | JSON-Feld | Beschreibung / Zweck |
|-------------|-----|-----------|----------------------|
| `Url` | `string` | `url` | Exakte absolute URL der Abbildung |
| `Status` | `int` (Default 200) | `status` | HTTP-Statuscode der Antwort |
| `Header` | `Dictionary<string,string>?` | `headers` | Zusätzliche Antwort-Header (z. B. `Link`) |
| `BodyDatei` | `string?` | `bodyFile` | Antwortdatei relativ zur Testwurzel (ZIP/JSON) |
| `Inhalt` | `string?` | `body` | Inline-Antworttext |
| `ContentType` | `string?` | `contentType` | Content-Type (Default `application/json`, `.zip`-BodyDatei → `application/zip`) |
| `Gate` | `string?` | `gate` | Antwort blockiert bis `{gate}.release`/`{gate}.cancel` |
| `StreamGate` | `string?` | `streamGate` | Antwortstream blockiert nach `StreamGateBytes` |
| `StreamGateBytes` | `long?` | `streamGateBytes` | Freie Bytes vor dem Blockieren (Default: Hälfte) |
| `Fehler` | `bool` | `fault` | Wirft statt Antwort einen `HttpRequestException`-Transportfehler |

### `UpdateE2ELesefehler` / `UpdateE2ECliSicherheit` / `UpdateE2EProzessStart`
Datei: `src/Softwareschmiede.App/Services/Testing/UpdateE2ESzenario.cs` (Zeilen 90-162)

Für den Download-Fallback nicht relevant (Settings-Lesefehler-Steuerung, CLI-Sicherheits-Simulation, Launcher-Ergebnis).

### `UpdateE2ETestConfiguration`
Datei: `src/Softwareschmiede.App/Services/Testing/UpdateE2ETestConfiguration.cs`

Statische Testkonfiguration (`Testwurzel`, `SzenarioDateiPfad`, `ProtokollDateiPfad`, `SzenarioId`, `GatesVerzeichnis`, `InstallationsVerzeichnis`); wird über die Umgebungsvariablen `SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG` + `SOFTWARESCHMIEDE_TEST_DB_PATH` aktiviert. Für den Fallback unverändert nutzbar.

### `UpdateE2EProtokollEintrag`
Datei: `src/Softwareschmiede.App/Services/Testing/UpdateE2EProtokoll.cs` (Zeilen 222-252)

JSONL-Eintrag (`Seq`, `ProzessId`, `SzenarioId`, `Ereignis`, `Daten`, `Utc`, `Quelle`) — Grundlage aller E2E-Nachweise; relevante Ereignisnamen für den Download: `HttpRequest` (Daten: `url`, `method`, `art`), `HttpResponse` (`url`, `status`), `UnknownHttpRequest` (`url`), `HttpRequestBlocked`/`HttpRequestReleased` (`url`, `gate`), `PreparationPhase`/`PreparationCompleted`/`PreparationFailed`.
