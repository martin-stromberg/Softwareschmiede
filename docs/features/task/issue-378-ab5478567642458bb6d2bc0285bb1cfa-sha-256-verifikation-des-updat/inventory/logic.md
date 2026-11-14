# Logik

## `UpdatePackageService`

Datei: `src/Softwareschmiede/Infrastructure/Services/Updates/UpdatePackageService.cs`

Implementiert `IUpdatePackageService`. Download → Entpacken → Validierung → Skripterzeugung. Wird aufgerufen von `UpdateService.PrepareUpdateAsync`. Zwei Konstruktoren: Produktiv (`AppContext.BaseDirectory`) und mit explizitem `baseDirectory` für Tests/E2E.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PreparePackageAsync` (Z. 44–90) | `public` | Legt Arbeitsverzeichnisse an, bereinigt Altartefakte, führt `Download` → Rename `.download`→`release.zip` → Leer-Prüfung → `Entpacken` → `ValidateExtractedPackage` → `UpdateVorbereiten` (Skript) aus; reportet `UpdatePreparationProgress` pro Phase. Catch-all (Z. 83–89) löscht `*.download`, `release.zip` und `extractedDirectory` und rethrowed. **Kein Manifest-Abruf, keine Hash-Prüfung.** |
| `DownloadAsync` (Z. 92–134) | `private` | GET auf `update.DownloadUrl`; bei Nicht-Erfolg **und** Status ≥ 500 **und** vorhandener `FallbackDownloadUrl` einmaliger Zweitversuch auf die API-Asset-URL; `EnsureSuccessStatusCode`; Streaming in `*.download` (81920-Byte-Puffer, `FileMode.CreateNew`, Prozentfortschritt aus `ContentLength`). |
| `SendDownloadRequestAsync` (Z. 136–146) | `private` | Baut den GET-`HttpRequestMessage`; bei `useApiHeaders` werden `User-Agent: Softwareschmiede/1.0` und `Accept: application/octet-stream` gesetzt. |
| `ValidateExtractedPackage` (Z. 148–157) | `private` | Wirft `InvalidDataException`, wenn `Softwareschmiede.exe` oder `version.json` im Paket-Root fehlt. |
| `CanWriteToBaseDirectory` (Z. 159–173) | `private` | Schreibprobe für `RequiresElevation`. |
| `EnsureInsideBase` / `EnsureInsideDirectory` (Z. 175–194) | `private`/`private static` | Pfadsicherung: wirft `InvalidOperationException` bei Verzeichnisaustritt. |
| `SanitizePathSegment` (Z. 196–201) | `private static` | Ersetzt ungültige Dateinamenzeichen im Versionspfad. |
| `DeleteFileIfExists` / `DeleteDirectoryIfExists` (Z. 203–215) | `private static` | Geschütztes Aufräumen innerhalb der Update-Wurzel. |

Relevante Imports: `System.IO.Compression` (ZIP), `System.Net.Http.Headers`. `System.Security.Cryptography`/`System.Text.Json` werden hier **nicht** verwendet.

## `GitHubReleaseClient`

Datei: `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs`

Implementiert `IUpdateReleaseClient`. Konstante `GitHubApiHost = "api.github.com"` (Z. 13) für Host-Pinning. JSON-Parsing via `System.Text.Json` mit `JsonPropertyName`-DTOs.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetLatestReleaseAsync` (Z. 28–109) | `public` | GET `repos/{owner}/{repo}/releases?per_page=100` mit `User-Agent`+`Accept: application/vnd.github+json`, Pagination über `Link`-Header (Zyklus- und Fremd-URL-Schutz), `CheckTimeout` via gelinktem CTS; wählt höchste zulässige SemVer des Kanals; Fehler → `UpdateReleaseLookupResult.Fehlgeschlagen`. |
| `TryCreateCandidate` (Z. 111–145) | `private` | Filter: `draft`, ungültiger Tag (`UpdateVersionComparer.TryParse`), Kanal-Mismatch, fehlendes Asset `_options.AssetName` mit absoluter HTTPS-`browser_download_url` → übersprungen. Baut `UpdateInfo` mit `CreateFallbackDownloadUrl(asset)`. **Sucht aktuell kein `update.json`-Asset.** |
| `CreateFallbackDownloadUrl` (Z. 147–155) | `private` | Nimmt `asset.Url`, wenn `IsAllowedAssetApiUrl`; sonst aus `asset.Id` via `RepositoryApiUrl("/releases/assets/{id}")` abgeleitet; sonst `null`. |
| `RepositoryApiUrl` (Z. 157–158) | `private` | `https://api.github.com/repos/{owner}/{repo}{path}`. |
| `IsAllowedFollowUpUri` / `IsRepositoryIdReleasesPath` (Z. 160–179) | `private`/`private static` | Folge-URLs müssen HTTPS auf `api.github.com` und dem Repository-Releases-Pfad (`/repos/{o}/{r}/releases` oder `/repositories/{id}/releases`) entsprechen. |
| `TryGetNextPageUri` (Z. 181–219) | `private static` | Parst `rel="next"` aus dem `Link`-Header. |
| `IsAbsoluteHttpsUrl` (Z. 221–223) | `private static` | Absolute-HTTPS-Prüfung für Download-URLs. |
| `IsAllowedAssetApiUrl` (Z. 225–228) | `private static` | HTTPS + Host == `api.github.com`. |

## `UpdateService`

Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateService.cs`

Implementiert `IUpdateService`; orchestriert, ändert nichts an Paketinhalten.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CheckForUpdateAsync` (Z. 34–89) | `public` | Serialisiert über `_checkGate`; installierte Version lesen → `IUpdateReleaseClient.GetLatestReleaseAsync` → Kanal-/Neuheitsprüfung → `UpdateCheckResult`; Exceptions → `NichtPruefbar`. |
| `PrepareUpdateAsync` (Z. 92–96) | `public` | Reiner Delegat an `_packageService.PreparePackageAsync` — Exceptions (inkl. eines künftigen Verifikationsfehlers) propagieren unverändert zum Aufrufer. |
| `StartPreparedUpdateAsync` (Z. 99–103) | `public` | `_scriptService.StartScriptAsync` → `_shutdownService.Shutdown()`. |

## `MainWindowUpdateFlow`

Datei: `src/Softwareschmiede.App/ViewModels/MainWindowUpdateFlow.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InstalliereUpdateAsync` (Z. 287–345) | `private` | CLI-Sicherheitsprüfung → Fortschrittsdialog → Aktualitätsnachweis → `PrepareUpdateAsync` → erneuter Nachweis → `MarkUpdaterStarting` → `StartPreparedUpdateAsync`. `catch (OperationCanceledException)` → Abbruch-Hinweis; `catch (Exception)` (Z. 334–339) → `SetError("Update konnte nicht vorbereitet werden. Details wurden protokolliert.")` + `UpdateHinweis`. Der generische Fehlerpfad deckt jede Vorbereitungs-Exception ab. |
| `PruefeSettingsAktualitaetAsync` (Z. 352–374) | `private` | Settings-Re-Read vor Vorbereitung und vor Updater-Start. |
| `FuehreUpdateVersuchAsync` (Z. 237–285) | `private` | Gate, Busy-Flags, Generationsschutz, Versuchsprotokoll. |
| `InitializeAfterWindowReadyAsync` / `PruefenAsync` / `StartenAsync` (Z. 160–230) | `public` | Einstiegspunkte: Startautomatik, manuelle Prüfung, geführter Start. |

## `UpdateProgressViewModel`

Datei: `src/Softwareschmiede.App/ViewModels/UpdateProgressViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Apply(UpdatePreparationProgress)` (Z. 78–97) | `public` | Ignoriert Reports im Terminalzustand; `switch` auf `Phase` → `PhaseText` (`"Download"`, `"Entpacken"`, `"Update-Vorbereitung"`, Fallback `"Vorbereitung"`), `Message`, `IsIndeterminate`, `Percent`. Ein neuer Enum-Wert würde aktuell auf den Fallback laufen. |
| `SetError(string)` (Z. 100–107) | `public` | Terminal-Fehleranzeige (`HasError`, `CanClose`, `CanCancel=false`). |
| `MarkUpdaterStarting` (Z. 110–116) | `public` | Übergang in „wird gestartet". |
| `RequestCancel` (Z. 118–127) | `private` | Abbrechen über `_cancelAction` (CTS aus `InstalliereUpdateAsync`). |

## `ProtokollierenderUpdateService` (E2E-Dekorator)

Datei: `src/Softwareschmiede.App/Services/Testing/ProtokollierenderUpdateService.cs`

Wickelt den echten `UpdateService` im E2E-Modus: `CheckForUpdateAsync` → `UpdateCheckStarted`/`UpdateCheckCompleted`; `PrepareUpdateAsync` (Z. 50–81) → `PreparationCompleted` bzw. `PreparationFailed` (`error` = Exception-Message); jede `UpdatePreparationProgress`-Meldung wird synchron als `PreparationPhase`-Ereignis (`phase`, `percent`, `message`) protokolliert (`ProtokollierenderFortschritt`, Z. 108–120). `StartPreparedUpdateAsync` → `UpdateStartAttempt`/`UpdateStartSucceeded`/`UpdateStartFailed`.

## `UpdateFixtureHttpMessageHandler` (E2E-HTTP-Grenze)

Datei: `src/Softwareschmiede.App/Services/Testing/UpdateFixtureHttpMessageHandler.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SendAsync` (Z. 29–97) | `protected override` | Klassifiziert die URL, protokolliert `HttpRequest`; lädt das Szenario pro Request neu; `FindeAntwort(url)` exakt (OrdinalIgnoreCase) — **unbekannte URLs werfen `HttpRequestException` + `UnknownHttpRequest`-Ereignis**; `Fehler`-Antworten werfen Transportfehler; `Gate` blockiert die Antwort; `StreamGate` blockiert den Stream nach N Bytes; Header/Status/ContentType aus der Antwortdefinition. |
| `LadeAntwortInhalt` (Z. 99–120) | `private` | `Inhalt` (UTF-8) oder `BodyDatei` relativ zur Testwurzel (Austrittsschutz) → Bytes. |
| `Klassifiziere` (Z. 122–131) | `private static` | `asset` (`/releases/assets/` oder `*.zip`), `release` (`/releases`), sonst `sonstige` — eine `update.json`-URL fiele derzeit unter `sonstige` bzw. enthielte `/releases` → `release`. |

## `UpdateE2ETestKontext` / `UpdateE2EProtokoll` / `UpdateE2EEreignisse`

Dateien: `src/Softwareschmiede.App/Services/Testing/UpdateE2ETestKontext.cs`, `UpdateE2EProtokoll.cs`

`UpdateE2ETestKontext` implementiert `IUpdateVersuchProtokoll` (Versuchszählung, markierte Settings-Reads, Gate-Wartezeiten); `UpdateE2EProtokoll` ist der thread-sichere JSONL-Schreiber; `UpdateE2EEreignisse` (Z. 9–85) listet alle Ereignisnamen — u. a. `HttpRequest` (Daten: `url`, `method`, `art`), `UnknownHttpRequest`, `PreparationPhase`, `PreparationCompleted`, `PreparationFailed`.

## `UpdateScriptService` / `UpdateProcessLauncher` (nicht betroffen, Vertrag bleibt)

Dateien: `src/Softwareschmiede/Infrastructure/Services/Updates/UpdateScriptService.cs`, `src/Softwareschmiede/Infrastructure/Services/Updates/`(`UpdateProcessLauncher`)

Erzeugt `updates/update.ps1` und startet es via `IUpdateProcessLauncher`. Kein Bezug zum Paket-Inhalt; laut requirement.md unverändert.

## Hilfsfunktionen

- `UpdatePfadKanalResolver` (`src/Softwareschmiede/Application/Services/Updates/UpdatePfadKanalResolver.cs`): Konstanten `ProduktivversionenPfad = "Produktivversionen"`, `VorabversionenPfad = "Vorabversionen"`; `ErmittleKanal`, `NormalisierePfad`, `IstGueltigerPfad`, `ErmittleKanalFuerTagName`.
- `UpdateVersionComparer` (`src/Softwareschmiede/Application/Services/Updates/UpdateVersionComparer.cs`): `TryParse`, `IsNewer`, `Normalize`.
- `SemanticUpdateVersion` (`src/Softwareschmiede/Application/Services/Updates/SemanticUpdateVersion.cs`): SemVer-Modell mit `CompareTo`, `IsPrerelease`.

## Release-Workflow (Kontext, nicht Teil der Code-Änderung)

`.github/actions/build-and-package/action.yml` — Composite-Action: Publish → Rename zu `Softwareschmiede.exe` → `version.json` → Verify → `release.zip` (`Compress-Archive`, Z. 94–96) → `update.json` mit SHA-256/Größe/URL (Z. 98–141). Uploads: `release.yml` (Z. 169 `gh release create … release.zip update.json`, Z. 180 `gh release upload … --clobber`) und `staging-ci.yml` (Z. 325 analog für Prereleases).
