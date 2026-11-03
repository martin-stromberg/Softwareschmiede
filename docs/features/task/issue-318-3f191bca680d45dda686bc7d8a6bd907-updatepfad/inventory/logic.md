# Logik

## `UpdateService`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `UpdateService(...)` | public | Ctor: `IApplicationVersionProvider`, `IUpdateReleaseClient`, `IUpdatePackageService`, `IUpdateScriptService`, `IApplicationShutdownService`, `ILogger` |
| `CheckForUpdateAsync(UpdateCheckOptions, CancellationToken)` | public | Serialisierte Prüfung (`_checkGate`): lokale Version lesen → `GetLatestReleaseAsync(options)` → Defensivprüfung (Zeilen 51–54: `latest.IsPrerelease && !options.IncludePrereleases` → `NichtPruefbar` als Vertragsverletzung) → `UpdateVersionComparer.IsNewer`-Vergleich |
| `PrepareUpdateAsync(UpdateInfo, IProgress<...>?, CancellationToken)` | public | Delegiert an `IUpdatePackageService.PreparePackageAsync` |
| `StartPreparedUpdateAsync(UpdatePreparationResult, CancellationToken)` | public | Skriptstart via `IUpdateScriptService`, danach `IApplicationShutdownService.Shutdown()` |

## `GitHubReleaseClient`
Datei: `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetLatestReleaseAsync(UpdateCheckOptions, CancellationToken)` | public | Paginiert `https://api.github.com/repos/{Owner}/{Name}/releases?per_page=100` (Request-URI hartcodiert, Zeile 31), wählt höchsten zulässigen Kandidaten per `SemanticUpdateVersion.CompareTo`; Zyklus-/URI-Absicherung |
| `TryCreateCandidate(GitHubRelease, UpdateCheckOptions, out SemanticUpdateVersion)` | private | Kernfilter (Zeilen 109–142): überspringt Drafts/leere Tags, ungültige SemVer-Tags, Prereleases bei `!options.IncludePrereleases` (`isPrerelease = release.Prerelease \|\| parsed.IsPrerelease`) sowie Releases ohne passendes HTTPS-Asset; erzeugt `UpdateInfo` inkl. `IsPrerelease` |
| `IsAllowedFollowUpUri(Uri)` | private | Beschränkt Pagination-Folge-URLs auf `api.github.com` + `/repos/{owner}/{repo}/releases` oder `/repositories/{id}/releases` |
| `IsRepositoryIdReleasesPath(string)` | private static | Erkennt die numerische Repository-Form `/repositories/{id}/releases` |
| `TryGetNextPageUri(HttpResponseMessage, out Uri?)` | private static | Liest `rel="next"`-Links aus dem `Link`-Header |
| `IsAbsoluteHttpsUrl(string)` | private static | Asset-URL-Prüfung |

Private JSON-DTOs: `GitHubRelease` (`tag_name`, `prerelease`, `draft`, `published_at`, `assets`), `GitHubAsset` (`name`, `browser_download_url`).

## `AppEinstellungService` (Update-Teil)
Datei: `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`

| Element | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `UpdateModeKey = "updates.mode"` | public const | Schlüssel des Update-Modus (Zahlenwert) |
| `IncludePrereleasesKey = "updates.includePrereleases"` | public const | Schlüssel der Prerelease-Auswahl (Bool-String) |
| `UpdateSettingsReadTag = "UpdateSettings.Read"` | public const | EF-Abfragetag der gemeinsamen Update-Read-Abfrage (Testinterception über `UpdateSettingsReadFailureInterceptor`) |
| `GetUpdateSettingsAsync(CancellationToken)` | public | Zeilen 388–411: liest beide Schlüssel in einer getaggten Abfrage; Defaults `UpdateMode.NurPruefen`/`false`; ungültige Werte → Defaults; DB-Fehler propagieren |
| `SetUpdateSettingsAsync(UpdateSettings, CancellationToken)` | public | Zeilen 420–458: validiert `UpdateMode` per `Enum.IsDefined`, schreibt beide Schlüssel in einem Speichervorgang (Update-or-Insert) |

Kein Update-Pfad-Schlüssel vorhanden.

## `SettingsViewModel` (Update-Teil)
Datei: `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`

| Element | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `UpdateSettingsSaved` | public event `EventHandler<UpdateSettings>` | Wird direkt nach `SetUpdateSettingsAsync` ausgelöst (Zeile 507), auch wenn spätere Speicherschritte fehlschlagen |
| `UpdateModusOptionen` | public `IReadOnlyList<UpdateModusOption>` | Feste Auswahlliste aus `UpdateModusOption.Alle` |
| `SelectedUpdateMode` | public `UpdateModusOption?` | Gebundene ComboBox-Auswahl |
| `IncludePrereleases` | public `bool` | Gebundene Checkbox „Prerelease-Versionen laden" (Feld `_includePrereleases`, Zeilen 60/77–82) |
| `LadenAsync` | private | Zeilen 457–460: `GetUpdateSettingsAsync` → `SelectedUpdateMode`/`IncludePrereleases` setzen |
| `SpeichernAsync` | private | Zeilen 505–507: `new UpdateSettings(updateModus, IncludePrereleases)` → `SetUpdateSettingsAsync` → `UpdateSettingsSaved` |
| `VerwerfenAsync` | private | Zeile 584–587: lädt neu via `LadenAsync` (kein `UpdateSettingsSaved`) |
| `ValidierePflichtfelder` | private | Zeile 747: validiert Plugin-Pflichtfelder (keine Update-spezifische Pflichtfeldprüfung) |

Commands: `LadenCommand`, `SpeichernCommand`, `VerwerfenCommand` (`AsyncRelayCommand`, Zeilen 363–365).

## `SettingsView.xaml` (Abschnitt „Updates")
Datei: `src/Softwareschmiede.App/Views/SettingsView.xaml` (Zeilen 431–461)

- ComboBox „Update-Modus" (`AutomationProperties.Name="Update-Modus"`, gebunden an `SelectedUpdateMode`/`UpdateModusOptionen`)
- CheckBox „Prerelease-Versionen laden" (`AutomationProperties.Name="Prerelease-Versionen laden"`, `IsChecked` ↔ `IncludePrereleases`) mit erklärendem Hinweistext
- Kein Eingabefeld für einen Update-Pfad vorhanden

## `MainWindowUpdateFlow`
Datei: `src/Softwareschmiede.App/ViewModels/MainWindowUpdateFlow.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ApplyGespeicherteUpdateSettings(UpdateSettings)` | public | Zeilen 121–137: Record-Vergleich → bei Änderung Generation erhöhen, laufenden Ablauf canceln, Angebot entfernen |
| `CancelLaufendenUpdateAblauf()` | public | Bricht `_updateAblaufCts` |
| `InitializeAfterWindowReadyAsync(CancellationToken)` | public | Einmalige Startautomatik; Zeile 173: `new UpdateCheckOptions(settings.IncludePrereleases)`; im Modus `BeiProgrammstartPruefenUndAusfuehren` direkte Installation |
| `PruefenAsync(CancellationToken)` | public | Manuelle Prüfung; Zeile 198: `new UpdateCheckOptions(settings.IncludePrereleases)` |
| `StartenAsync(CancellationToken)` | public | Installationsstart mit erneuter Prüfung; Zeile 220: `new UpdateCheckOptions(settings.IncludePrereleases)` |
| `FuehreUpdateVersuchAsync(...)` | private | Gemeinsames Gerüst: `_updateGate`, `IUpdateVersuchProtokoll.BeginneVersuch/BeendeVersuch`, Generations-Snapshot, Settings-Read, `UpdateMode.Aus`-Guard |
| `InstalliereUpdateAsync(...)` | private | Safety-Check, Fortschrittsdialog, zwei `PruefeSettingsAktualitaetAsync`-Nachweise (vor Vorbereitung, vor Updater-Start), `PrepareUpdateAsync`, `StartPreparedUpdateAsync` |
| `PruefeSettingsAktualitaetAsync(...)` | private | Zeilen 352–374: liest gespeicherte Werte erneut, bricht bei Ungleichheit (`aktuell != snapshot`) oder Generationsdrift ab |
| `LeseUpdateSettingsAsync(CancellationToken)` | private | Scope-basiert `AppEinstellungService.GetUpdateSettingsAsync`; Fehler → `null` + Hinweis |
| `IstUpdateVersuchAktuell(int)`, `UpdateEinstellungenLesefehlerAnzeigen()`, `UpdateAngebotEntfernen()`, `ApplyUpdateCheckResult(...)`, `BuildSafetyMessage(...)` | private | Hilfslogik |

Hinweis-Konstanten: `UpdateEinstellungenNichtLesbarHinweis`, `UpdateEinstellungenGeaendertHinweis`.

## `MainWindowViewModel` (Update-Anbindung)
Datei: `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`

- `NavigateToSettings` (Zeilen 307–316): abonniert `SettingsViewModel.UpdateSettingsSaved` → `OnUpdateSettingsSaved`
- `OnUpdateSettingsSaved` (Zeilen 318–319): dispatcht `UpdateSettings` an `MainWindowUpdateFlow.ApplyGespeicherteUpdateSettings`

## `DatenbankPfadResolver`
Datei: `src/Softwareschmiede/Infrastructure/Data/DatenbankPfadResolver.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ErmittlePfad(baseDirectory, testDbPathOverride)` | public static | Test-Override (`SOFTWARESCHMIEDE_TEST_DB_PATH`) hat Vorrang; produktive Version → `%LocalAppData%\Softwareschmiede\softwareschmiede.db`; RC-/versionslose Version → `{baseDirectory}\softwareschmiede.db` |
| `IstProduktiveVersion(baseDirectory)` | private static | Liest `version.json`-`tagName`: enthält `-rc` (OrdinalIgnoreCase) → nicht produktiv; fehlende/kaputte `version.json` oder fehlender `tagName` → konservativ nicht produktiv |

Konstanten: `DatenbankDateiname`, `VersionsDateiname`, `ReleaseCandidateMarker = "-rc"`. Wird in `App.xaml.cs` `ConfigureServices` (Zeilen 164–166) vor dem DI-Aufbau aufgerufen; die RC-Erkennung basiert auf dem Tag-Format `v{version}-rc.{n}` (staging-ci) vs. `v{version}` (release).

## `ApplicationVersionProvider`
Datei: `src/Softwareschmiede/Application/Services/Updates/ApplicationVersionProvider.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ApplicationVersionProvider(ILogger)` / `(string, ILogger)` | public | Basispfad `AppContext.BaseDirectory` oder explizit (Tests) |
| `GetInstalledVersionAsync(CancellationToken)` | public | Liest `{base}/version.json` (`version`, `tagName`, `commit`, `createdAtUtc`); validiert via `UpdateVersionComparer.TryParse`; fehlende/ungültige Datei → `null` |

Liefert `InstalledVersionInfo` inkl. `TagName` — enthält bereits die Information, ob die Installation ein RC ist (`-rc`-Infix), wird aber aktuell nur zur Versionsnormalisierung genutzt.

## `UpdateVersionComparer`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateVersionComparer.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `TryParse(string?, out SemanticUpdateVersion?)` | public static | Toleriert Whitespace und führendes `v`/`V` |
| `IsNewer(installedVersion, candidateVersion)` | public static | SemVer-Präzedenzvergleich `candidate > installed` |
| `Normalize(string)` | public static | Kanonische Form `X.Y.Z[-prerelease][+meta]`; `FormatException` bei Ungültigkeit |

## `SemanticUpdateVersion`
Datei: `src/Softwareschmiede/Application/Services/Updates/SemanticUpdateVersion.cs`

| Element | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Major`, `Minor`, `Patch` | public get | Kernversion |
| `PrereleaseIdentifiers` | public get | Geordnete Prerelease-Identifier (leer = stabil) |
| `BuildMetadata` | public get | Ohne Rangfolge-Einfluss |
| `IsPrerelease` | public get | `PrereleaseIdentifiers.Count > 0` — SemVer-Prerelease-Erkennung |
| `TryParse(string?, out SemanticUpdateVersion?)` | public static | Strenger SemVer-2.0-Parser |
| `CompareTo`, `Equals`, `GetHashCode`, `ToString` | public | SemVer-Präzedenz (stabil > eigene Prereleases; numerisch < alphanumerisch) |

## `ProtokollierenderUpdateService` (E2E-Dekorator)
Datei: `src/Softwareschmiede.App/Services/Testing/ProtokollierenderUpdateService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CheckForUpdateAsync(UpdateCheckOptions, CancellationToken)` | public | Protokolliert `UpdateCheckStarted` mit `includePrereleases = options.IncludePrereleases` (Zeile 32) und `UpdateCheckCompleted` (status, version, isPrerelease, message) |
| `PrepareUpdateAsync(...)` | public | Protokolliert `PreparationPhase`/`PreparationCompleted`/`PreparationFailed` via synchronem `ProtokollierenderFortschritt` |
| `StartPreparedUpdateAsync(...)` | public | Protokolliert `UpdateStartAttempt`/`UpdateStartSucceeded`/`UpdateStartFailed` |

## `UpdateE2ETestKontext`
Datei: `src/Softwareschmiede.App/Services/Testing/UpdateE2ETestKontext.cs`

Implementiert `IUpdateVersuchProtokoll`; bündelt `UpdateE2ETestConfiguration`, `UpdateE2EProtokoll`, Gate-Mechanik (`WarteAufGate[Async]`, `PruefeGate`; `LesefehlerGateName = "settingsReadFailure"`), Versuchszählung (`BeginneVersuch`/`BeendeVersuch`, `AktuellerVersuch`, `NaechsterMarkierterReadOrdinal`) und `LeseSzenario()`.

## `UpdateSettingsReadFailureInterceptor`
Datei: `src/Softwareschmiede.App/Services/Testing/UpdateSettingsReadFailureInterceptor.cs`

EF-Core-`DbCommandInterceptor`: greift ausschließlich auf SQL mit `UpdateSettingsReadTag`; Komponententest-Modus (`ActivateFailure`/`DeactivateFailure`, `SettingsReadReached`-Event, `SettingsReadCount`) und E2E-Modus (Szenario-gesteuert pro Versuch/Grenze, optionaler Gate-Wait, protokolliert `UpdateSettingsReadReached`/`UpdateSettingsReadFailed`).

## `UpdateFixtureHttpMessageHandler`
Datei: `src/Softwareschmiede.App/Services/Testing/UpdateFixtureHttpMessageHandler.cs`

`HttpMessageHandler` der E2E-Umgebung: bildet exakte URLs auf Szenario-Antworten ab (JSON/ZIP, `Link`-Header, Gate-/StreamGate-Blockaden, absichtliche Transportfehler); unbekannte Requests → `HttpRequestException` ohne Netzwerkfallback.

## DI-Registrierung (`App.ConfigureServices`)
Datei: `src/Softwareschmiede.App/App.xaml.cs`

| Zeilen | Registrierung |
|--------|---------------|
| 163–166 | `DatenbankPfadResolver.ErmittlePfad(AppContext.BaseDirectory, SOFTWARESCHMIEDE_TEST_DB_PATH)` → SQLite-`DbContext` |
| 172–188 | Update-E2E-Aktivierung: `UpdateE2ETestConfiguration.LadeAusUmgebung`, `UpdateE2ETestKontext`, `UpdateSettingsReadFailureInterceptor` als DbContext-Interceptor, `IUpdateVersuchProtokoll` |
| 193 | `IOptions<UpdateOptions>` mit `new UpdateOptions()` (Defaults unverändert) |
| 236 | `AppEinstellungService` (scoped) |
| 250–252 | `ICliUpdateSafetyService`, `IApplicationVersionProvider`, `IUpdateService` |
| 296–304 | `IUpdateProgressDialogService`, `IApplicationShutdownService`, `HttpClient`, `IUpdateReleaseClient` → `GitHubReleaseClient`, `IUpdatePackageService`, `IUpdateScriptService`, `IUpdateProcessLauncher` |
| 305–314 | `MainWindowUpdateDienste` (UpdateService, SafetyService, ProgressDialog, VersuchProtokoll) → `MainWindowOptionaleDienste` |
| 366–395 | E2E-Übersteuerung: `UpdateFixtureHttpMessageHandler`-`HttpClient`, Fixture-`UpdateScriptService`/`UpdatePackageService`/`IUpdateProcessLauncher` (`RecordingUpdateProcessLauncher`), `FixtureCliUpdateSafetyService`, `IUpdateService` → `ProtokollierenderUpdateService` um echten `UpdateService` |
