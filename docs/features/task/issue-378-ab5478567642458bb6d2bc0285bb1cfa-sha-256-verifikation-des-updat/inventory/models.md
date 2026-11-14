# Datenmodell

## `UpdateInfo`

Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 22–29)

Positional Record; wird von `GitHubReleaseClient.TryCreateCandidate` erzeugt und per `IUpdateService.PrepareUpdateAsync` / `IUpdatePackageService.PreparePackageAsync` an die Vorbereitung übergeben. `FallbackDownloadUrl` ist als letzter Parameter optional (Standard `null`) — bestehende Test-Aufrufstellen konstruieren `UpdateInfo` mit 6 oder 7 Argumenten.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Version` | `string` | Semantische Release-Version ohne führendes `v` (inkl. Prerelease-Suffix/Build-Metadaten), via `UpdateVersionComparer.Normalize` |
| `TagName` | `string` | GitHub-Release-Tag (z. B. `v1.2.3`) |
| `AssetName` | `string` | Tatsächlicher Name des ausgewählten Release-Assets (Groß-/Kleinschreibung wie geliefert, z. B. `RELEASE.ZIP`) |
| `DownloadUrl` | `Uri` | Direkte Download-URL (`browser_download_url`, absolute HTTPS-URL) |
| `PublishedAt` | `DateTimeOffset?` | Veröffentlichungszeitpunkt |
| `IsPrerelease` | `bool` | GitHub-Flag `prerelease` ODER SemVer-Prerelease-Suffix |
| `FallbackDownloadUrl` | `Uri?` | GitHub-API-Asset-URL für den einmaligen 5xx-Download-Fallback (Standard `null`) |

*Keine Manifest-/Hash-Felder vorhanden.*

## `UpdateOptions`

Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateOptions.cs` (Zeilen 4–23)

`init`-Eigenschaften; Registrierung als feste Instanz `Options.Create(new UpdateOptions())` in `src/Softwareschmiede.App/App.xaml.cs:209` — keine Konfigurationsbindung, keine Benutzereinstellung.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `RepositoryOwner` | `string` | GitHub-Repository-Owner (`"martin-stromberg"`) |
| `RepositoryName` | `string` | GitHub-Repository-Name (`"Softwareschmiede"`) |
| `AssetName` | `string` | Name des erwarteten Release-Assets (`"release.zip"`) |
| `UpdateDirectoryName` | `string` | Update-Arbeitsverzeichnis relativ zum Programmverzeichnis (`"updates"`) |
| `CheckTimeout` | `TimeSpan` | Timeout nur für die Release-Prüfung (`GetLatestReleaseAsync`), 15 s; Downloads/Manifestabrufe laufen über das Aufrufer-`CancellationToken` |
| `ExecutableName` | `string` | Erwartete Exe im Paket-Root (`"Softwareschmiede.exe"`) |

*Kein `ManifestAssetName` o. ä. vorhanden.*

## `UpdatePreparationResult`

Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 151–156)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ZipPath` | `string` | Pfad der finalen `release.zip` unter `updates/download/` |
| `ExtractedDirectory` | `string` | Pfad `updates/extracted/{Version}` |
| `ScriptPath` | `string` | Pfad des erzeugten `update.ps1` |
| `LogPath` | `string` | Pfad der Update-Logdatei |
| `RequiresElevation` | `bool` | Ob der Skriptstart erhöhte Rechte anfordern soll |

## `UpdatePreparationProgress`

Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeile 143)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Phase` | `UpdatePreparationPhase` | Aktuelle Phase (siehe [enums.md](enums.md)) |
| `Percent` | `double?` | Optionaler Prozentwert (nur Download) |
| `Message` | `string` | Anzeigetext für den Fortschrittsdialog |

## `UpdateCheckOptions`

Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 61–65)

Record mit `UpdatePfad` sowie abgeleiteter `Kanal`-Eigenschaft (`UpdatePfadKanalResolver.ErmittleKanal`).

## `UpdateReleaseLookupResult`

Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 72–95)

Unterscheidet fehlgeschlagene Abfrage von erfolgloser Kandidatensuche: `Erfolg` (`bool`), `Release` (`UpdateInfo?`); Fabrik-Properties `Fehlgeschlagen`, `KeinTreffer`, Fabrikmethode `Gefunden(UpdateInfo)`.

## `UpdateCheckResult`

Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 114–124)

`Status` (`UpdateCheckStatus`), `Update` (`UpdateInfo?`), `Message` (`string?`); Fabrikmethoden `KeinUpdate`, `UpdateVerfuegbar(UpdateInfo, string?)`, `NichtPruefbar`.

## `InstalledVersionInfo`, `UpdateSettings`, `CliUpdateSafetyResult`

Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 8–12, 57, 161–165)

- `InstalledVersionInfo(Version, TagName?, Commit?, CreatedAtUtc?)` — lokale `version.json`.
- `UpdateSettings(Modus, UpdatePfad)` — persistiertes Einstellungspaar.
- `CliUpdateSafetyResult(RiskyTaskCount, RiskyTasks)` mit `RequiresConfirmation` — Sicherheitsdialog vor der Vorbereitung.

## `update.json`-Manifestschema (Release-Workflow)

Erzeuger: `.github/actions/build-and-package/action.yml`, Schritt „Create update manifest" (Zeilen 98–141). Datei wird neben `release.zip` als Release-Asset hochgeladen (`release.yml`:169/180, `staging-ci.yml`:325). Der `sha256` wird per `Get-FileHash ... .ToLowerInvariant()` kleingeschrieben (Zeile 121); `sizeBytes` ist `(Get-Item release.zip).Length` (Zeile 122); `assetUrl` zeigt auf `…/releases/download/{tag}/release.zip` (Zeile 123).

| Feld | Typ | Beschreibung / Zweck |
|------|-----|----------------------|
| `version` | `string` | Release-Version ohne führendes `v` |
| `releaseNotes` | `string` | `"Softwareschmiede release {tag}"` |
| `publishedAt` | `string` | ISO-8601-UTC-Zeitpunkt (`ToString('o')`) |
| `assets[]` | Array | Genau ein Eintrag pro Manifest |
| `assets[].platform` | `string` | `"windows"` |
| `assets[].runtimeIdentifier` | `string` | `"win-x64"` |
| `assets[].assetName` | `string` | `"release.zip"` |
| `assets[].assetUrl` | `string` | Browser-Download-URL des Pakets |
| `assets[].sha256` | `string` | SHA-256-Hexwert der `release.zip`, kleingeschrieben |
| `assets[].sizeBytes` | `long` | Dateigröße der `release.zip` in Bytes |

*Kein Consumer dieses Schemas im C#-Code vorhanden — kein DTO, kein Parser.*

## Private DTOs des `GitHubReleaseClient`

Datei: `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs` (Zeilen 230–261), `System.Text.Json` mit `JsonPropertyName`-Mapping.

| DTO | Felder |
|-----|--------|
| `GitHubRelease` | `TagName` (`tag_name`), `Prerelease` (`prerelease`), `Draft` (`draft`), `PublishedAt` (`published_at`), `Assets` (`assets`, `List<GitHubAsset>`) |
| `GitHubAsset` | `Name` (`name`), `BrowserDownloadUrl` (`browser_download_url`), `Url` (`url` — API-Asset-URL), `Id` (`id`) |

## `UpdateE2ESzenario` / `UpdateE2EAntwort` (E2E-Szenariomodell)

Datei: `src/Softwareschmiede.App/Services/Testing/UpdateE2ESzenario.cs`

`UpdateE2ESzenario` (Zeilen 11–36): `Antworten` (`List<UpdateE2EAntwort>`), `Lesefehler`, `CliSicherheit`, `ProzessStart`; `FindeAntwort(url)` sucht exakte URL-Treffer (OrdinalIgnoreCase).

`UpdateE2EAntwort` (Zeilen 39–83): `Url`, `Status`, `Header`, `BodyDatei` (relativ zur Testwurzel), `Inhalt` (Inline-Body), `ContentType`, `Gate`, `StreamGate`, `StreamGateBytes`, `Fehler` (absichtlicher Transportfehler). *Kein Mechanismus zur Hash-Berechnung — Antwortbodies sind statisch (Inline-Text oder Datei).*

## `UpdateE2EProtokollEintrag`

Datei: `src/Softwareschmiede.App/Services/Testing/UpdateE2EProtokoll.cs` (Zeilen 223–252)

JSONL-Eintrag: `Seq`, `ProzessId`, `SzenarioId`, `Ereignis`, `Daten`, `Utc`, `Quelle`. Ereignisnamen in `UpdateE2EEreignisse` (Zeilen 9–85) — darunter `HttpRequest`/`HttpResponse`/`UnknownHttpRequest` (Daten: `url`, `art`) sowie `PreparationPhase` (Daten: `phase`, `percent`, `message`).
