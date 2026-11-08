# Logik

## `GitHubReleaseClient`
Datei: `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs`

Implementiert `IUpdateReleaseClient`. Abhängigkeiten: `HttpClient`, `IOptions<UpdateOptions>`, `ILogger<GitHubReleaseClient>`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GitHubReleaseClient(HttpClient, IOptions<UpdateOptions>, ILogger<GitHubReleaseClient>)` | public | Konstruktor |
| `GetLatestReleaseAsync(UpdateCheckOptions options, CancellationToken ct = default)` | public | Paginiert die Release-Liste (`https://api.github.com/repos/{owner}/{repo}/releases?per_page=100`), wählt den höchsten kanalkonformen Kandidaten via `TryCreateCandidate`, liefert `UpdateReleaseLookupResult`. Timeout: linked CTS mit `_options.CheckTimeout` (Z. 28-29). Fehler (`HttpRequestException`, `OperationCanceledException`, `JsonException`, `UriFormatException`) → `Fehlgeschlagen`; Caller-Cancellation propagiert (Z. 98-106). |
| `TryCreateCandidate(GitHubRelease release, UpdateCheckOptions options, out SemanticUpdateVersion version)` | private | Filtert Drafts/leere Tags, parst SemVer, prüft Kanal-Exklusivität, wählt das erste Asset mit `Name == _options.AssetName` (OrdinalIgnoreCase) **und** `IsAbsoluteHttpsUrl(BrowserDownloadUrl)` (Z. 125-132). Einzige Produktiv-`UpdateInfo`-Konstruktion (Z. 135-141): `new UpdateInfo(Normalize(tag), tag, asset.Name, new Uri(asset.BrowserDownloadUrl, Absolute), release.PublishedAt, isPrerelease)` — derzeit 6 positionelle Argumente, keine Fallback-URL. |
| `IsAllowedFollowUpUri(Uri uri)` | private | Pinning für Pagination-Folge-URLs: HTTPS + Host `api.github.com` + Pfad `/repos/{owner}/{repo}/releases` oder numerische `/repositories/{id}/releases`-Form (Z. 144-151). |
| `IsRepositoryIdReleasesPath(string path)` | private static | Erkennt `/repositories/{id}/releases` (Z. 156-163). |
| `TryGetNextPageUri(HttpResponseMessage response, out Uri? nextUri)` | private static | Liest `rel="next"` aus dem `Link`-Header (Z. 165-203). |
| `IsAbsoluteHttpsUrl(string url)` | private static | `Uri.TryCreate` absolut + Scheme HTTPS (Z. 205-207) — bestehende Validierung für `browser_download_url`. |

**Request-Details (Z. 47-51):** Pro Seite ein `HttpRequestMessage(HttpMethod.Get, requestUri)` mit `User-Agent: Softwareschmiede/1.0` (`ProductInfoHeaderValue`) und `Accept: application/vnd.github+json` (`MediaTypeWithQualityHeaderValue`); `SendAsync` mit `HttpCompletionOption.ResponseHeadersRead`. Beide Header werden pro Request gesetzt — der geteilte `HttpClient` hat keine Defaults.

**Logging:** `LogWarning` bei Pagination-Zyklus, HTTP-Fehlerstatus, leerer/ungültiger Liste, ungültiger Folge-URL, ungültigem Tag, fehlendem Asset und Gesamtfehlschlag.

## `UpdatePackageService`
Datei: `src/Softwareschmiede/Infrastructure/Services/Updates/UpdatePackageService.cs`

Implementiert `IUpdatePackageService`. Abhängigkeiten: `HttpClient`, `IUpdateScriptService`, `IOptions<UpdateOptions>`, `ILogger<UpdatePackageService>`, `_baseDirectory` (Produktiv `AppContext.BaseDirectory`, per zweitem Konstruktor testbar).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `UpdatePackageService(HttpClient, IUpdateScriptService, IOptions<UpdateOptions>, ILogger<UpdatePackageService>)` | public | Delegiert an den zweiten Konstruktor mit `AppContext.BaseDirectory` |
| `UpdatePackageService(..., string baseDirectory)` | public | Testbarer Konstruktor mit explizitem Basispfad |
| `PreparePackageAsync(UpdateInfo update, IProgress<UpdatePreparationProgress>? progress, CancellationToken ct = default)` | public | Legt `updates/download|extracted` an, räumt Altartefakte, meldet Phase `Download` (0 %), ruft `DownloadAsync(update.DownloadUrl, partialZipPath, progress, ct)` (Z. 66), `File.Move(partialZipPath → zipPath, overwrite: true)`, lehnt 0-Byte-Pakete ab (`InvalidDataException`), Phase `Entpacken` → `ZipFile.ExtractToDirectory` + `ValidateExtractedPackage`, Phase `UpdateVorbereiten` → `CreateScriptAsync`, `RequiresElevation` via `CanWriteToBaseDirectory`. Catch-all: löscht partielle ZIP/ZIP/extracted und wirft weiter (Z. 82-88). |
| `DownloadAsync(Uri downloadUrl, string partialZipPath, IProgress<UpdatePreparationProgress>? progress, CancellationToken ct)` | private | **Einstufiger Download (Z. 91-118):** `HttpRequestMessage(HttpMethod.Get, downloadUrl)` **ohne Header**, `SendAsync` mit `ResponseHeadersRead`, `response.EnsureSuccessStatusCode()` (wirft `HttpRequestException` bei Nicht-2xx), `ContentLength` für Fortschritt, Stream-Kopie mit 81920-Byte-Buffer in `FileStream(partialZipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)`, Fortschrittsmeldungen `UpdatePreparationPhase.Download` mit Prozent. Kein Retry, kein Fallback, kein eigenes Timeout (nur Caller-`ct`), kein `User-Agent`/`Accept`. |
| `ValidateExtractedPackage(string extractedDirectory)` | private | Verlangt `{ExecutableName}` und `version.json` im Paket-Root, sonst `InvalidDataException` |
| `CanWriteToBaseDirectory()` | private | Schreibprobe für `RequiresElevation` (`LogInformation` bei Fehlschlag) |
| `EnsureInsideBase(string path)` | private | Pfad-Confinement unterhalb `_baseDirectory` |
| `EnsureInsideDirectory(string, string, string, bool)` | private static | Generische Pfad-Confinement-Prüfung, wirft `InvalidOperationException` |
| `SanitizePathSegment(string value)` | private static | Ersetzt ungültige Dateinamen-Zeichen durch `_` |
| `DeleteFileIfExists(string path, string updateRoot)` / `DeleteDirectoryIfExists(...)` | private static | Confinement-geprüftes Aufräumen |

**Pfade in `PreparePackageAsync` (Z. 48-54):** `partialZipPath = updates/download/{AssetName}.download`, `zipPath = updates/download/{AssetName}`, `extractedDirectory = updates/extracted/{Sanitize(Version)}`, `logPath = updates/update.log` — alle durch `EnsureInsideBase` gesichert. Die `.download`-Datei wird erst beim Öffnen des `FileStream` (nach erfolgreichem Response-Header) angelegt.

## `UpdateService`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateService.cs`

Implementiert `IUpdateService`. Orchestriert VersionProvider → ReleaseClient → PackageService → ScriptService → Shutdown.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CheckForUpdateAsync(UpdateCheckOptions, CancellationToken)` | public | Gate-serialisierte Prüfung; defensiver Kanal-Ausschluss (Z. 54); `UpdateVersionComparer.IsNewer`; Kanalkonflikt-Warnung (Z. 63-72); Fehler → `NichtPruefbar` |
| `PrepareUpdateAsync(UpdateInfo update, IProgress<UpdatePreparationProgress>?, CancellationToken)` | public | Reicht `update` unverändert an `_packageService.PreparePackageAsync(update, progress, ct)` weiter (Z. 92-96) — kein eigener Zugriff auf `DownloadUrl` |
| `StartPreparedUpdateAsync(UpdatePreparationResult, CancellationToken)` | public | `StartScriptAsync` + `Shutdown` |

## `MainWindowUpdateFlow`
Datei: `src/Softwareschmiede.App/ViewModels/MainWindowUpdateFlow.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InitializeAfterWindowReadyAsync(CancellationToken)` | public | Einmalige Startautomatik; bei `BeiProgrammstartPruefenUndAusfuehren` + `UpdateVerfuegbar` → `InstalliereUpdateAsync` (Z. 183) |
| `PruefenAsync(CancellationToken)` / `StartenAsync(CancellationToken)` | public | Manueller Check / geführter Start (erneuter Check + `InstalliereUpdateAsync`, Z. 228) |
| `InstalliereUpdateAsync(UpdateInfo update, UpdateSettings snapshot, int generation, CancellationToken ct)` | private | Sicherheitsprüfung + Dialog, Fortschrittsdialog, Aktualitätsnachweise, `_updateService.PrepareUpdateAsync(update, progress, updateCts.Token)` (Z. 315), `StartPreparedUpdateAsync`. **Fehlerpfad:** `catch (Exception)` → `LogWarning` + `progressViewModel.SetError("Update konnte nicht vorbereitet werden. Details wurden protokolliert.")` + `UpdateHinweis = "Update konnte nicht vorbereitet werden."` (Z. 334-339); `OperationCanceledException` → „Update-Vorbereitung wurde abgebrochen." (Z. 329-333). |
| `FuehreUpdateVersuchAsync(...)` | private | Gerüst: Update-Gate, Versuchsprotokoll, Busy-Flags, Generations-Snapshot, Settings-Read, Aus-Guard |
| `ApplyUpdateCheckResult`, `UpdateAngebotEntfernen`, `LeseUpdateSettingsAsync`, `PruefeSettingsAktualitaetAsync`, `IstUpdateVersuchAktuell` | private | Angebots-/Settings-Verwaltung |

`UpdateInfo` wird hier nur gehalten (`_verfuegbaresUpdate`/`VerfuegbaresUpdate`) und durchgereicht — kein Zugriff auf `DownloadUrl`.

## `UpdateFixtureHttpMessageHandler`
Datei: `src/Softwareschmiede.App/Services/Testing/UpdateFixtureHttpMessageHandler.cs`

`HttpMessageHandler` des dedizierten E2E-Update-`HttpClient`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SendAsync(HttpRequestMessage, CancellationToken)` | protected override | URL klassifizieren → `HttpRequest`-Event → Szenario neu lesen → `FindeAntwort(url)`; `null` → `UnknownHttpRequest`-Event + `HttpRequestException` (**kein Netzwerkfallback**); `Fehler` → `HttpRequestException`; `Gate` → blockiert bis `.release`/`.cancel`/Timeout (120 s); Body aus `Inhalt` oder `BodyDatei` (Testwurzel-Confinement); `StreamGate` → `GateBlockierterStream`; `Status`/`Header`/`ContentType` auf die Antwort; `HttpResponse`-Event |
| `Klassifiziere(string url)` | private static | `url.Contains("/releases", OrdinalIgnoreCase)` → `"release"`; `url.EndsWith(".zip", OrdinalIgnoreCase)` → `"asset"`; sonst `"sonstige"`. **Hinweis:** Eine API-Asset-URL `https://api.github.com/repos/{o}/{r}/releases/assets/{id}` enthält `/releases` → würde als `"release"` klassifiziert, nicht als `"asset"`. |
| `LadeAntwortInhalt(UpdateE2EAntwort)` | private | `Inhalt` → UTF8-Bytes; `BodyDatei` → Datei innerhalb der Testwurzel (Confinement-Prüfung); sonst leer |
| `GateBlockierterStream` (geschachtelte Klasse) | private sealed | `Stream`, der nach `freieBytes` blockiert bis `.release`/`.cancel` — für echte Teilfortschritt-/Abbruch-Szenarien |

Der Handler prüft **keine Request-Header** — ein `Accept: application/octet-stream`-Fallback-Request wäre für ihn transparent (nur die URL entscheidet).

## `UpdateE2ETestKontext`
Datei: `src/Softwareschmiede.App/Services/Testing/UpdateE2ETestKontext.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LeseSzenario()` | public | Liest `scenario.json` pro Zugriff neu; Fehler → `ScenarioFileError`-Event + `null` |
| `WarteAufGateAsync(string, CancellationToken, TimeSpan)` / `WarteAufGate(...)` | public | Wartet auf `{gate}.release`/`{gate}.cancel` im Gate-Verzeichnis |
| `PruefeGate(string)` | public | `true` bei `.release`, `false` bei `.cancel`, `null` sonst |
| `ProtokolliereWindowReady` / `BeginneVersuch` / `BeendeVersuch` / `NaechsterMarkierterReadOrdinal` | public | Versuchsprotokollierung (`IUpdateVersuchProtokoll`) |

## `ProtokollierenderUpdateService`
Datei: `src/Softwareschmiede.App/Services/Testing/ProtokollierenderUpdateService.cs`

`IUpdateService`-Dekorator der E2E-Umgebung; leitet alle Calls an den echten `UpdateService` weiter und protokolliert: `UpdateCheckStarted`/`UpdateCheckCompleted` (Daten: `updatePfad`, `kanal`, `status`, `version`, `isPrerelease`, `message`), `PreparationPhase` (über synchronen `ProtokollierenderFortschritt`-Passthrough), `PreparationCompleted` (`zipPath`, `extractedDirectory`, `scriptPath`, `requiresElevation`), `PreparationFailed` (`error`), `UpdateStartAttempt`/`UpdateStartSucceeded`/`UpdateStartFailed`.

## `UpdateE2EFixture`
Datei: `src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs`

Runner-seitige Fixture (kein Produktivcode). Für den Fallback relevante Oberfläche:

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `ReleaseApiUrl` | public static readonly | `https://api.github.com/repos/martin-stromberg/Softwareschmiede/releases?per_page=100` (aus Standard-`UpdateOptions`) |
| `StableDownloadUrl` / `RcDownloadUrl` | public | `https://e2e.invalid/{SzenarioId}/release-{version}.zip` — bewusst keine echten Quellen |
| `StableZipPfad` / `RcZipPfad` / `PaketVerzeichnis` / `Testwurzel` / `GatesVerzeichnis` / `SzenarioId` / `InstalliertesVerzeichnis` | public | Fixture-Pfade |
| `SetzeStandardAntworten(bool mitRcRelease = true)` | public | Registriert 3 Standard-Antworten: Release-Liste (`ReleaseApiUrl`, Body `ErzeugeReleaseListenJson`) + ZIP-`BodyDatei` für `StableDownloadUrl` und `RcDownloadUrl` (Z. 133-154) |
| `ErzeugeReleaseListenJson(bool)` | private | Asset-JSONs enthalten **nur** `name` + `browser_download_url` (Z. 443-465) — kein `url`, kein `id` |
| `AktualisiereSzenario(Action<UpdateE2ESzenario>)` / `SpeichereSzenario()` | public | Thread-sichere Szenario-Mutation + Neu-Schreiben der Szenariodatei |
| `OeffneGate` / `BrecheGateAb` / `SetzeGateZurueck` | public | Gate-Dateien schreiben/löschen |
| `SetzeUpdateEinstellungenAsync` / `AktualisiereUpdateEinstellungenAsync` | public | Persistiert `UpdateSettings` in der Fixture-SQLite-DB |
| `ErstellePaketZip(zipPfad, version, marker)` / `LesePaketExeInhalt(verzeichnis)` | public static | ZIP-Pakete mit `Softwareschmiede.exe` + `version.json` |
| `WarteAufEreignisAsync` / `ZaehleEreignis` / `LeseProtokoll` / `Daten` | public | JSONL-Protokoll-Waits und -Auswertung |

## DI-Registrierung (`App.ConfigureServices`)
Datei: `src/Softwareschmiede.App/App.xaml.cs`

- **Produktiv (Z. 313-318):** `services.AddSingleton(sp => new HttpClient())` — ein geteilter `HttpClient` **ohne Default-Header**, den sich `GitHubReleaseClient` (`IUpdateReleaseClient`) und `UpdatePackageService` (`IUpdatePackageService`) teilen; dazu `IUpdateScriptService`, `IUpdateProcessLauncher`.
- **Update-E2E-Modus (Z. 383-410):** `HttpClient` wird als `new HttpClient(new UpdateFixtureHttpMessageHandler(kontext))` überschrieben (letzte Registrierung gewinnt); `UpdatePackageService` und `UpdateScriptService` mit `testwurzel` als Basis; `RecordingUpdateProcessLauncher`, `RecordingApplicationShutdownService`, `FixtureCliUpdateSafetyService`; `IUpdateService` = `ProtokollierenderUpdateService` um den echten `UpdateService`. Damit läuft ein Fallback-Download im E2E vollständig über den Fixture-Handler — jede Fallback-URL braucht einen `UpdateE2EAntwort`-Eintrag.
